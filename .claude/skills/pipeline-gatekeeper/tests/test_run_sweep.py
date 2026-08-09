"""Tests for the sweep's wiring."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import run_sweep  # noqa: E402


class RecordingApi:
    def __init__(self):
        self.calls = []

    def __call__(self, method, path, body=None):
        self.calls.append((method, path, body))
        return {}

    @property
    def label_writes(self):
        return [(p, b) for m, p, b in self.calls if m == "PUT" and p.endswith("/labels")]


def issue(number=10, state="open", labels=(), body="", native_blockers=(),
          comments=(), milestone="v0.0.1"):
    return {
        "number": number, "state": state, "labels": list(labels),
        "body": body, "native_blockers": list(native_blockers),
        "comments": list(comments), "milestone": milestone,
    }


class RequeueGatingTests(unittest.TestCase):
    def stalled_state(self):
        analysis = {"body": "## Build checklist\n\n- [ ] x",
                    "author": "github-actions[bot]",
                    "created_at": "2026-08-08T10:00:00Z"}
        return {"issues": [issue(10, labels=("in-progress",), comments=[analysis])],
                "focus": "v0.0.1"}

    def test_the_cron_pass_requeues_a_stalled_issue(self):
        api = RecordingApi()
        result = run_sweep.run(self.stalled_state(), api, rerender=lambda **kw: None)
        self.assertEqual(result["fixed"], [10])

    def test_the_event_pass_does_not(self):
        api = RecordingApi()
        result = run_sweep.run(self.stalled_state(), api, events_only=True,
                               rerender=lambda **kw: None)

        self.assertEqual(result["fixed"], [])
        self.assertEqual(api.label_writes, [])


class RevisitTests(unittest.TestCase):
    def blocked_state(self, blocker_state="closed"):
        return {
            "issues": [issue(10, labels=("needs-clarification",), body="Blocked by #42")],
            "snapshot": {42: {"state": blocker_state, "labels": []}},
            "focus": "v0.0.1",
        }

    def test_a_cleared_blocker_wakes_the_issue(self):
        api = RecordingApi()
        result = run_sweep.run(self.blocked_state(), api, rerender=lambda **kw: None)
        self.assertEqual(result["woken"], [10])

    def test_waking_posts_an_explanatory_comment(self):
        api = RecordingApi()
        run_sweep.run(self.blocked_state(), api, rerender=lambda **kw: None)

        comments = [b for m, p, b in api.calls if p.endswith("/comments")]
        self.assertTrue(comments)
        self.assertIn("#42", comments[0]["body"])

    def test_an_open_blocker_leaves_it_alone(self):
        api = RecordingApi()
        result = run_sweep.run(self.blocked_state("open"), api, rerender=lambda **kw: None)
        self.assertEqual(result["woken"], [])


class RerenderTests(unittest.TestCase):
    def test_the_board_is_rerendered_once_at_the_end(self):
        renders = []
        state = {
            "issues": [issue(10, state="closed", labels=("ready-for-work",))],
            "focus": "v0.0.1",
        }

        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: renders.append(kw))
        self.assertEqual(len(renders), 1)

    def test_a_sweep_that_changed_nothing_does_not_rerender(self):
        renders = []
        run_sweep.run({"issues": [issue(10, labels=("ai-triage",))], "focus": "v0.0.1"},
                      RecordingApi(), rerender=lambda **kw: renders.append(kw))
        self.assertEqual(renders, [])


class FlagTests(unittest.TestCase):
    def test_flags_are_returned_but_never_written(self):
        api = RecordingApi()
        analysis = {"body": "## Build checklist\n\n- [ ] x",
                    "author": "github-actions[bot]",
                    "created_at": "2026-08-08T10:00:00Z"}
        state = {"issues": [issue(10, labels=("ready-for-work",), milestone=None,
                                  comments=[analysis])],
                 "focus": "v0.0.1"}

        result = run_sweep.run(state, api, rerender=lambda **kw: None)
        kinds = [f["kind"] for f in result["findings"]]

        self.assertIn("flag_orphaned_ready", kinds)
        self.assertEqual(api.label_writes, [])



class ReactiveTriageTests(unittest.TestCase):
    """The sweep fires too, on both of its paths.

    Neither path was wired. A blocker clearing at 09:00, or a stalled issue
    requeued at 02:00, added `ai-triage` and then waited for the *next*
    scheduled round to notice.
    """

    def test_waking_a_blocked_issue_fires_triage(self):
        fired = []
        state = {
            "issues": [issue(10, labels=("needs-clarification",), body="Blocked by #42")],
            "snapshot": {42: {"state": "closed", "labels": []}},
            "focus": "v0.0.1",
        }
        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: None,
                      fire=fired.append)
        self.assertEqual(fired, [10])

    def test_requeuing_to_triage_fires_triage(self):
        fired = []
        state = {"issues": [issue(10, labels=("pending-approval",), comments=[])],
                 "focus": "v0.0.1"}
        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: None,
                      fire=fired.append)
        self.assertEqual(fired, [10])

    def test_a_fix_that_does_not_reach_triage_does_not_fire(self):
        """A stalled `in-progress` issue goes to `ready-for-work`, not triage."""
        fired = []
        analysis = {"body": "## Build checklist\n\n- [ ] x",
                    "author": "github-actions[bot]",
                    "created_at": "2026-08-08T10:00:00Z"}
        state = {"issues": [issue(10, labels=("in-progress",), comments=[analysis])],
                 "focus": "v0.0.1"}
        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: None,
                      fire=fired.append)
        self.assertEqual(fired, [])

    def test_a_quiet_sweep_fires_nothing(self):
        fired = []
        analysis = {"body": "## Build checklist\n\n- [ ] x",
                    "author": "github-actions[bot]",
                    "created_at": "2026-08-08T10:00:00Z"}
        state = {"issues": [issue(10, labels=("pending-approval",), comments=[analysis])],
                 "focus": "v0.0.1"}
        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: None,
                      fire=fired.append)
        self.assertEqual(fired, [])

    def test_an_issue_woken_and_requeued_in_one_sweep_fires_once(self):
        """Two fires are two triage sessions racing, each posting its own plan.

        A revisit does not update the in-memory issue reconcile then reads, so
        without the guard this exact state fires twice.
        """
        fired = []
        state = {
            "issues": [issue(10, labels=("needs-clarification",), body="Blocked by #42")],
            "snapshot": {42: {"state": "closed", "labels": []}},
            "focus": "v0.0.1",
        }
        run_sweep.run(state, RecordingApi(), rerender=lambda **kw: None,
                      fire=fired.append)
        self.assertEqual(fired, [10])

    def test_the_sweep_imports_the_fire(self):
        source = (Path(__file__).resolve().parents[1] / "run_sweep.py").read_text()
        self.assertIn("import fire_routine", source)


if __name__ == "__main__":
    unittest.main()
