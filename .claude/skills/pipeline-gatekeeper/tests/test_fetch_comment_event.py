"""Tests for the per-issue snapshot builder."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import fetch_comment_event as fetcher  # noqa: E402


def payload(body="/approve", author="derekwinters", bot=False, is_pr=False, number=10):
    issue = {"number": number, "labels": [{"name": "pending-approval"}],
             "body": "Blocked by #20", "milestone": {"title": "v0.0.1"}}
    if is_pr:
        issue["pull_request"] = {"url": "..."}
    return {
        "issue": issue,
        "comment": {"id": 555, "body": body,
                    "user": {"login": author, "type": "Bot" if bot else "User"}},
    }


class FakeApi:
    def __init__(self, blocked_by=(), reactions=()):
        self.blocked_by = list(blocked_by)
        self.reactions = list(reactions)

    def __call__(self, method, path, payload=None):
        if "dependencies/blocked_by" in path:
            return self.blocked_by
        if "reactions" in path:
            return self.reactions
        return {}


class SkipTests(unittest.TestCase):
    def test_a_pull_request_comment_is_skipped(self):
        # Defence in depth: the workflow filters these too, but a snapshot the
        # parser could act on is one it eventually will.
        self.assertIsNone(fetcher.build(payload(is_pr=True), FakeApi(), owner="derekwinters"))

    def test_a_bot_comment_is_skipped(self):
        self.assertIsNone(fetcher.build(payload(bot=True), FakeApi(), owner="derekwinters"))

    def test_the_gatekeeper_s_own_ack_is_skipped(self):
        # Otherwise its own reply could be parsed as a command.
        event = payload(body="/approve", author="github-actions[bot]", bot=True)

        self.assertIsNone(fetcher.build(event, FakeApi(), owner="derekwinters"))


class SnapshotTests(unittest.TestCase):
    def snapshot(self, **kwargs):
        api = kwargs.pop("api", FakeApi())
        return fetcher.build(payload(**kwargs), api, owner="derekwinters")

    def test_it_carries_the_issue_basics(self):
        snapshot = self.snapshot()

        self.assertEqual(10, snapshot["issue"]["number"])
        self.assertEqual(["pending-approval"], snapshot["issue"]["labels"])
        self.assertEqual("v0.0.1", snapshot["issue"]["milestone"])

    def test_it_carries_the_comment_under_consideration(self):
        snapshot = self.snapshot(body="/approve")

        self.assertEqual("/approve", snapshot["comment"]["body"])
        self.assertEqual(555, snapshot["comment"]["id"])

    def test_blockers_union_text_and_native(self):
        api = FakeApi(blocked_by=[{"number": 21}])

        snapshot = self.snapshot(api=api)

        self.assertEqual([20, 21], sorted(snapshot["issue"]["blockers"]))

    def test_the_watermark_is_read_from_the_comment_s_reactions(self):
        api = FakeApi(reactions=[{"content": "eyes", "user": {"login": "github-actions[bot]"}}])

        self.assertTrue(self.snapshot(api=api)["comment"]["watermarked"])

    def test_someone_else_s_eyes_reaction_is_not_the_watermark(self):
        # A human reacting 👀 out of interest must not silence a command.
        api = FakeApi(reactions=[{"content": "eyes", "user": {"login": "derekwinters"}}])

        self.assertFalse(self.snapshot(api=api)["comment"]["watermarked"])

    def test_the_dashboard_issue_is_recognised(self):
        event = payload()
        event["issue"]["labels"] = [{"name": "dashboard"}]

        snapshot = fetcher.build(event, FakeApi(), owner="derekwinters")

        self.assertTrue(snapshot["issue"]["is_dashboard"])


class ToleranceTests(unittest.TestCase):
    def test_a_payload_with_no_milestone_does_not_throw(self):
        event = payload()
        event["issue"]["milestone"] = None

        self.assertIsNone(fetcher.build(event, FakeApi(), owner="derekwinters")["issue"]["milestone"])

    def test_a_payload_with_no_body_does_not_throw(self):
        event = payload()
        event["issue"]["body"] = None

        self.assertEqual([], fetcher.build(event, FakeApi(), owner="derekwinters")["issue"]["blockers"])

    def test_a_payload_with_no_labels_does_not_throw(self):
        event = payload()
        del event["issue"]["labels"]

        self.assertEqual([], fetcher.build(event, FakeApi(), owner="derekwinters")["issue"]["labels"])

    def test_a_payload_with_no_comment_is_skipped_rather_than_throwing(self):
        self.assertIsNone(fetcher.build({"issue": {"number": 1}}, FakeApi(), owner="d"))

    def test_a_failing_blockers_fetch_does_not_lose_the_snapshot(self):
        # A command should still be honoured when the dependency lookup fails;
        # the gates that need blockers see an empty list and say so.
        def explode(method, path, payload=None):
            if "dependencies" in path:
                raise RuntimeError("the API is down")
            return []

        snapshot = fetcher.build(payload(), explode, owner="derekwinters")

        self.assertEqual([20], snapshot["issue"]["blockers"])


if __name__ == "__main__":
    unittest.main()
