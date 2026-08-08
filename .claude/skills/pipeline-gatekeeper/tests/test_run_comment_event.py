"""Tests for the gatekeeper's I/O wiring.

The API is injected as a callable, so the ordering decisions — which are the
part that can actually be wrong — are asserted without a network call.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import run_comment_event as runner  # noqa: E402

OWNER = "derekwinters"


class RecordingApi:
    """Records every call, answers the reads the runner makes."""

    def __init__(self, reactions=None, dependencies=None):
        self.calls = []
        self._reactions = reactions or []
        self._dependencies = dependencies or []

    def __call__(self, method, path, body=None):
        self.calls.append((method, path, body))

        if path.endswith("/reactions") and method == "GET":
            return self._reactions
        if path.endswith("/blocked_by"):
            return self._dependencies
        return {}

    def methods_on(self, fragment):
        return [m for m, p, _ in self.calls if fragment in p]

    @property
    def writes(self):
        return [(m, p, b) for m, p, b in self.calls if m in ("POST", "PATCH", "DELETE")]


def event(body="/approve", author=OWNER, labels=("pending-approval",),
          milestone="v0.0.1", number=10, is_dashboard=False):
    return {
        "issue": {
            "number": number,
            "labels": [{"name": name} for name in labels]
                      + ([{"name": "dashboard"}] if is_dashboard else []),
            "body": "",
            "milestone": {"title": milestone} if milestone else None,
        },
        "comment": {"id": 999, "body": body, "user": {"login": author, "type": "User"}},
    }


class OwnerRecheckTests(unittest.TestCase):
    def test_a_stranger_gets_no_writes_at_all(self):
        api = RecordingApi()
        runner.run(event(author="a-stranger"), api, owner=OWNER)
        self.assertEqual(api.writes, [])

    def test_the_owner_check_happens_in_the_script_too(self):
        """Defense in depth — the workflow filters, and so does this."""
        api = RecordingApi()
        result = runner.run(event(author="someone-else"), api, owner=OWNER)
        self.assertFalse(result.applied)

    def test_a_bot_comment_is_ignored(self):
        api = RecordingApi()
        payload = event()
        payload["comment"]["user"]["type"] = "Bot"
        runner.run(payload, api, owner=OWNER)
        self.assertEqual(api.writes, [])


class RerenderTests(unittest.TestCase):
    def test_the_dashboard_is_rerendered_once_after_a_label_change(self):
        api = RecordingApi()
        rerenders = []

        runner.run(event(), api, owner=OWNER, rerender=lambda **kw: rerenders.append(kw))
        self.assertEqual(len(rerenders), 1)

    def test_the_rerender_happens_after_the_label_writes(self):
        api = RecordingApi()
        order = []

        def note_rerender(**kwargs):
            order.append(("rerender", len(api.writes)))

        runner.run(event(), api, owner=OWNER, rerender=note_rerender)

        self.assertTrue(order)
        # At least the label write has already happened when the render runs.
        self.assertGreater(order[0][1], 0)

    def test_no_label_change_means_no_rerender(self):
        api = RecordingApi()
        rerenders = []

        runner.run(event(body="not a command"), api, owner=OWNER,
                   rerender=lambda **kw: rerenders.append(kw))
        self.assertEqual(rerenders, [])


class FocusAndCapTests(unittest.TestCase):
    def test_focus_persists_via_a_rerender_override(self):
        api = RecordingApi()
        rerenders = []

        runner.run(event(body="/focus v0.0.2", is_dashboard=True), api, owner=OWNER,
                   rerender=lambda **kw: rerenders.append(kw))

        self.assertEqual(len(rerenders), 1)
        self.assertEqual(rerenders[0].get("focus_override"), "v0.0.2")

    def test_cap_persists_via_a_rerender_override(self):
        api = RecordingApi()
        rerenders = []

        runner.run(event(body="/cap 5", is_dashboard=True), api, owner=OWNER,
                   rerender=lambda **kw: rerenders.append(kw))
        self.assertEqual(rerenders[0].get("cap_override"), 5)

    def test_the_issue_body_is_never_patched_directly(self):
        """Markers are written by re-rendering, never by hand-editing."""
        api = RecordingApi()
        runner.run(event(body="/focus v0.0.2", is_dashboard=True), api, owner=OWNER,
                   rerender=lambda **kw: None)

        for method, path, body in api.writes:
            if method == "PATCH" and isinstance(body, dict):
                self.assertNotIn("body", body)


class WatermarkTests(unittest.TestCase):
    def test_an_applied_command_leaves_the_eyes(self):
        api = RecordingApi()
        runner.run(event(), api, owner=OWNER, rerender=lambda **kw: None)
        self.assertIn("POST", api.methods_on("/reactions"))

    def test_an_already_watermarked_comment_is_not_reapplied(self):
        api = RecordingApi(reactions=[
            {"content": "eyes", "user": {"login": "github-actions[bot]"}}])
        result = runner.run(event(), api, owner=OWNER, rerender=lambda **kw: None)
        self.assertFalse(result.applied)


class TokenTests(unittest.TestCase):
    def test_the_api_helper_uses_the_workflow_token_not_a_pat(self):
        source = (Path(__file__).resolve().parents[1] / "_github_api.py").read_text()
        self.assertIn("GITHUB_TOKEN", source)
        for forbidden in ("PERSONAL_ACCESS_TOKEN", "GH_PAT", "PAT_TOKEN"):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
