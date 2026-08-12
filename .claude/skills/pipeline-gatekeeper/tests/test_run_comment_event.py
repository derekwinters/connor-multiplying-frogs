"""Tests for the gatekeeper's I/O wiring.

The API is injected as a callable, so the ordering decisions — which are the
part that can actually be wrong — are asserted without a network call.
"""

import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import run_comment_event as runner  # noqa: E402

OWNER = "derekwinters"

ONE_ISSUE = re.compile(r"^/issues/(\d+)$")


class RecordingApi:
    """Records every call, answers the reads the runner makes."""

    def __init__(self, reactions=None, dependencies=None, issues=None):
        self.calls = []
        self._reactions = reactions or []
        self._dependencies = dependencies or []
        # The issues a dependency edge might point at, by number, in GitHub's
        # own shape — the snapshot reads each one to learn its milestone.
        self._issues = dict(issues or {})

    def __call__(self, method, path, body=None):
        self.calls.append((method, path, body))

        if path.endswith("/reactions") and method == "GET":
            return self._reactions
        if path.endswith("/blocked_by"):
            return self._dependencies

        match = ONE_ISSUE.match(path)
        if match and method == "GET":
            return self._issues.get(int(match.group(1)), {})

        return {}

    def methods_on(self, fragment):
        return [m for m, p, _ in self.calls if fragment in p]

    @property
    def writes(self):
        return [(m, p, b) for m, p, b in self.calls if m in ("POST", "PATCH", "DELETE")]


def event(body="/approve", author=OWNER, labels=("pending-approval",),
          milestone="v0.0.1", number=10, is_dashboard=False, issue_body=""):
    return {
        "issue": {
            "number": number,
            "labels": [{"name": name} for name in labels]
                      + ([{"name": "dashboard"}] if is_dashboard else []),
            "body": issue_body,
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


class GateRefusalTests(unittest.TestCase):
    """A refused command must come back as an explanation, not an exception.

    The two refusal tests above it come from the *parser* — not-owner and
    already-watermarked — and never reach a gate. Nothing exercised a gate
    through the runner, so two separate shape mismatches on this path went
    unnoticed until the workflow started failing on Derek's `/approve`:
    the snapshot hands the gates bare issue numbers where they read edges,
    and the refusal was assembled from a `Verdict` field that does not exist.
    """

    def refuse(self, api=None, **kwargs):
        api = api or RecordingApi()
        result = runner.run(event(**kwargs), api, owner=OWNER, rerender=lambda **kw: None)
        return api, result

    def posted(self, api):
        return "\n".join(
            (body or {}).get("body", "")
            for method, path, body in api.writes if path.endswith("/comments"))

    def test_an_approve_with_no_milestone_is_explained(self):
        api, result = self.refuse(milestone=None)

        self.assertFalse(result.applied)
        self.assertIn("milestone", self.posted(api))

    def test_a_blocker_in_a_later_milestone_is_explained(self):
        """The failure in the run: the gate read `20` where it expected an edge."""
        api = RecordingApi(
            issues={20: {"number": 20, "state": "open", "milestone": {"title": "v0.1"}}})

        api, result = self.refuse(api=api, issue_body="Blocked by #20", milestone="v0.0.1")

        self.assertFalse(result.applied)
        self.assertIn("#20", self.posted(api))

    def test_a_soft_dependency_in_a_later_milestone_is_explained(self):
        api = RecordingApi(
            issues={30: {"number": 30, "state": "open", "milestone": {"title": "v0.1"}}})

        api, result = self.refuse(api=api, issue_body="Depends on: #30", milestone="v0.0.1")

        self.assertIn("#30", self.posted(api))

    def test_a_blocker_in_an_earlier_milestone_still_approves(self):
        api = RecordingApi(
            issues={20: {"number": 20, "state": "open", "milestone": {"title": "v0.0.1"}}})

        api, result = self.refuse(api=api, issue_body="Blocked by #20", milestone="v0.1")

        self.assertTrue(result.applied)

    def test_a_closed_blocker_constrains_nothing(self):
        api = RecordingApi(
            issues={20: {"number": 20, "state": "closed", "milestone": {"title": "v0.1"}}})

        api, result = self.refuse(api=api, issue_body="Blocked by #20", milestone="v0.0.1")

        self.assertTrue(result.applied)

    def test_a_refusal_changes_no_labels(self):
        api, _ = self.refuse(milestone=None)

        self.assertEqual([], [p for m, p, _ in api.writes if p.endswith("/labels")])

    def test_a_refusal_is_watermarked(self):
        """Or the sweep reconsiders it forever, re-posting the same refusal."""
        api, _ = self.refuse(milestone=None)

        self.assertIn("POST", api.methods_on("/reactions"))

    def test_the_refusal_carries_the_gate_s_prose_not_its_skip_code(self):
        """`Skip(reason, detail)` is a code and prose, in that order.

        `Verdict` names the same pair the other way round, and swapping them
        posts an acknowledgement reading "was not applied. approve-no-milestone"
        — and, worse, hands `SILENT_SKIPS` a sentence to match against codes.
        """
        api, _ = self.refuse(milestone=None)

        self.assertNotIn("approve-no-milestone", self.posted(api))
        self.assertIn("`/milestone <title>`", self.posted(api))


class ResultingLabelsTests(unittest.TestCase):
    """The sweep's replay writes these back onto its in-memory snapshot.

    Reconcile runs next and derives its fixes from that snapshot, so a replay
    that could not report the labels it left behind would have reconcile "fix"
    the state the command just set.
    """

    def test_an_applied_command_reports_the_labels_it_left(self):
        result = runner.run(event(), RecordingApi(), owner=OWNER,
                            rerender=lambda **kw: None)
        self.assertEqual(result.labels, ["ready-for-work"])

    def test_a_refused_command_reports_no_labels_at_all(self):
        """Not "the labels unchanged" — nothing, so the caller leaves its own
        copy alone rather than overwriting it with a guess."""
        api = RecordingApi(reactions=[
            {"content": "eyes", "user": {"login": "github-actions[bot]"}}])
        result = runner.run(event(), api, owner=OWNER, rerender=lambda **kw: None)
        self.assertIsNone(result.labels)

    def test_a_stranger_reports_no_labels(self):
        result = runner.run(event(author="a-stranger"), RecordingApi(), owner=OWNER)
        self.assertIsNone(result.labels)


class ReactiveTriageTests(unittest.TestCase):
    """The fire is wired in, not merely implemented.

    `fires_triage` and `fire_routine.fire` were both written and both tested,
    and nothing called either — so reactive triage never fired once, and every
    issue waited for the scheduled round. These tests assert the call site
    exists, which is the part that was missing.
    """

    def _fired(self, **kwargs):
        fired = []
        api = RecordingApi()
        runner.run(event(**kwargs), api, owner=OWNER,
                   rerender=lambda **kw: None, fire=fired.append)
        return fired

    def test_admitting_an_issue_fires_triage(self):
        self.assertEqual(self._fired(body="/admit", labels=()), [10])

    def test_revise_fires_triage(self):
        """`/revise` returns the issue to `ai-triage` — it needs analyzing again."""
        self.assertEqual(self._fired(body="/revise wrong pond", labels=("pending-approval",)), [10])

    def test_approve_does_not_fire_triage(self):
        """`/approve` moves to `ready-for-work`. Nothing to triage."""
        self.assertEqual(self._fired(body="/approve", labels=("pending-approval",)), [])

    def test_re_admitting_an_already_admitted_issue_does_not_fire(self):
        """An idempotent re-add is not a new admission — or one stuck comment
        becomes a triage run on every sweep."""
        self.assertEqual(self._fired(body="/admit", labels=("ai-triage",)), [])

    def test_a_refused_command_does_not_fire(self):
        self.assertEqual(
            self._fired(body="/admit", labels=("type:epic",)), [])

    def test_the_runner_imports_the_fire(self):
        """The regression itself: the module never imported `fire_routine`."""
        source = (Path(__file__).resolve().parents[1] / "run_comment_event.py").read_text()
        self.assertIn("import fire_routine", source)


class TokenTests(unittest.TestCase):
    def test_the_api_helper_uses_the_workflow_token_not_a_pat(self):
        source = (Path(__file__).resolve().parents[1] / "_github_api.py").read_text()
        self.assertIn("GITHUB_TOKEN", source)
        for forbidden in ("PERSONAL_ACCESS_TOKEN", "GH_PAT", "PAT_TOKEN"):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
