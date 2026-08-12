"""Tests for the per-issue snapshot builder."""

import re
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


ONE_ISSUE = re.compile(r"^/issues/(\d+)$")


def an_issue(number, milestone="v0.1", state="open"):
    """One issue in GitHub's own shape — milestone nested, not flattened."""
    return {"number": number, "state": state,
            "milestone": {"title": milestone} if milestone else None}


class FakeApi:
    def __init__(self, blocked_by=(), reactions=(), issues=None):
        self.blocked_by = list(blocked_by)
        self.reactions = list(reactions)
        # Every issue a dependency edge can point at, by number. Anything not
        # here reads back as unreadable.
        self.issues = dict(issues or {})
        self.paths = []

    def __call__(self, method, path, payload=None):
        self.paths.append(path)

        if "dependencies/blocked_by" in path:
            return self.blocked_by
        if "reactions" in path:
            return self.reactions

        match = ONE_ISSUE.match(path)
        if match:
            return self.issues.get(int(match.group(1)), {})

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
        api = FakeApi(blocked_by=[{"number": 21}],
                      issues={20: an_issue(20), 21: an_issue(21)})

        snapshot = self.snapshot(api=api)

        self.assertEqual([20, 21], [edge["number"] for edge in snapshot["issue"]["blockers"]])

    def test_a_blocker_is_an_edge_the_gates_can_read(self):
        """Not a bare number.

        The milestone-order gate reads each edge's own state and milestone, so
        a list of issue numbers is not something it can answer with — it threw
        `AttributeError` on the first `/approve` that had a blocker at all.
        """
        api = FakeApi(issues={20: an_issue(20, milestone="v0.2", state="closed")})

        edge = self.snapshot(api=api)["issue"]["blockers"][0]

        self.assertEqual(
            {"number": 20, "state": "closed", "milestone": "v0.2"}, edge)

    def test_a_soft_dependency_is_an_edge_too(self):
        """`Depends on:` is gated on ordering the same way a blocker is."""
        event = payload()
        event["issue"]["body"] = "Depends on: #30"
        api = FakeApi(issues={30: an_issue(30, milestone="v0.2")})

        snapshot = fetcher.build(event, api, owner="derekwinters")

        self.assertEqual([{"number": 30, "state": "open", "milestone": "v0.2"}],
                         snapshot["issue"]["soft_dependencies"])

    def test_a_depends_on_line_is_not_a_blocker(self):
        event = payload()
        event["issue"]["body"] = "Depends on: #30"

        snapshot = fetcher.build(event, FakeApi(issues={30: an_issue(30)}),
                                 owner="derekwinters")

        self.assertEqual([], snapshot["issue"]["blockers"])

    def test_an_issue_that_both_blocks_and_is_depended_on_is_named_once(self):
        event = payload()
        event["issue"]["body"] = "Blocked by #20\n\nDepends on: #20"

        snapshot = fetcher.build(event, FakeApi(issues={20: an_issue(20)}),
                                 owner="derekwinters")

        self.assertEqual([20], [edge["number"] for edge in snapshot["issue"]["blockers"]])
        self.assertEqual([], snapshot["issue"]["soft_dependencies"])

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
        # A command should still be honoured when the dependency lookup fails.
        # The text blockers are still known, and the gates still read them.
        def explode(method, path, payload=None):
            if "dependencies" in path:
                raise RuntimeError("the API is down")
            if ONE_ISSUE.match(path):
                return an_issue(20)
            return []

        snapshot = fetcher.build(payload(), explode, owner="derekwinters")

        self.assertEqual([20], [edge["number"] for edge in snapshot["issue"]["blockers"]])

    def test_an_edge_nobody_can_read_counts_as_unscheduled(self):
        """Open and with no milestone, so the gates refuse rather than approve.

        Approving over a dependency nobody can see is the expensive direction:
        the issue goes ready, the builder skips it every night, and nothing
        says why. A refusal costs one comment.
        """
        def explode(method, path, payload=None):
            if path == "/issues/20":
                raise RuntimeError("the API is down")
            return []

        snapshot = fetcher.build(payload(), explode, owner="derekwinters")

        self.assertEqual([{"number": 20, "state": "open", "milestone": None}],
                         snapshot["issue"]["blockers"])


if __name__ == "__main__":
    unittest.main()
