"""Tests for drift detection and the auto-fix / flag split."""

import json
import sys
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import reconcile  # noqa: E402

FOCUS = "v0.0.1"
BOT = "github-actions[bot]"

ANALYSIS = "Here is the plan.\n\n## Build checklist\n\n- [ ] do the thing\n"


def issue(number=10, state="open", labels=(), milestone=FOCUS, body="",
          native_blockers=(), comments=()):
    return {
        "number": number,
        "state": state,
        "labels": list(labels),
        "milestone": milestone,
        "body": body,
        "native_blockers": list(native_blockers),
        "comments": list(comments),
    }


def comment(body=ANALYSIS, author=BOT):
    return {"body": body, "author": author, "created_at": "2026-08-08T10:00:00Z"}


def findings(issues, kind=None, **kwargs):
    data = {"issues": list(issues), "focus": FOCUS}
    data.update(kwargs)
    result = reconcile.process(data)
    if kind is None:
        return result["findings"]
    return [f for f in result["findings"] if f["kind"] == kind]


class StripLabelsTests(unittest.TestCase):
    def test_a_closed_issue_with_a_state_label_is_stripped(self):
        found = findings([issue(10, state="closed", labels=("ready-for-work",))],
                         kind="strip_labels")

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["issue"], 10)
        self.assertEqual(found[0]["remove_labels"], ["ready-for-work"])
        self.assertEqual(found[0]["action"], "auto-fix")

    def test_a_closed_issue_keeps_its_area_and_type_labels(self):
        found = findings(
            [issue(10, state="closed", labels=("ready-for-work", "area:ui", "type:bug"))],
            kind="strip_labels")
        self.assertEqual(found[0]["remove_labels"], ["ready-for-work"])

    def test_a_closed_issue_with_no_state_label_is_not_a_finding(self):
        self.assertEqual(
            findings([issue(10, state="closed", labels=("area:ui",))],
                     kind="strip_labels"), [])

    def test_an_open_issue_is_never_stripped(self):
        self.assertEqual(findings([issue(10, labels=("ready-for-work",))],
                                  kind="strip_labels"), [])

    def test_strip_labels_runs_on_the_event_path_too(self):
        """Safe on every pass: it only ever touches an already-closed issue."""
        found = findings([issue(10, state="closed", labels=("in-progress",))],
                         kind="strip_labels", events_only=True)
        self.assertEqual(len(found), 1)


class RequeueTests(unittest.TestCase):
    def stalled(self, **kwargs):
        return findings([issue(10, labels=("in-progress",), comments=[comment()])],
                        kind="requeue", **kwargs)

    def test_in_progress_with_no_pr_and_nothing_on_main_is_requeued(self):
        found = self.stalled()

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["add_labels"], ["ready-for-work"])
        self.assertEqual(found[0]["remove_labels"], ["in-progress"])

    def test_an_open_pr_means_it_is_not_stalled(self):
        found = findings(
            [issue(10, labels=("in-progress",), comments=[comment()])],
            kind="requeue",
            pulls=[{"number": 5, "state": "open", "body": "Closes #10"}])
        self.assertEqual(found, [])

    def test_requeue_is_omitted_entirely_on_the_event_path(self):
        """A just-picked-up issue transiently looks exactly like a stall."""
        self.assertEqual(self.stalled(events_only=True), [])

    def test_an_issue_already_on_main_is_not_a_stall(self):
        """The guard against a re-pick loop."""
        found = findings(
            [issue(10, labels=("in-progress",), comments=[comment()])],
            kind="requeue",
            merged_commits=[{"body": "feat: thing\n\nCloses #10"}])
        self.assertEqual(found, [])

    def test_an_issue_already_on_main_is_merged_but_open_instead(self):
        found = findings(
            [issue(10, labels=("in-progress",), comments=[comment()])],
            kind="flag_merged_but_open",
            merged_commits=[{"body": "feat: thing\n\nCloses #10"}])
        self.assertEqual(len(found), 1)


class RequeueTriageTests(unittest.TestCase):
    def test_a_state_label_with_no_analysis_goes_back_to_triage(self):
        found = findings([issue(10, labels=("pending-approval",))],
                         kind="requeue_triage")

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["add_labels"], ["ai-triage"])
        self.assertEqual(found[0]["remove_labels"], ["pending-approval"])

    def test_an_analysis_comment_means_no_finding(self):
        self.assertEqual(
            findings([issue(10, labels=("pending-approval",), comments=[comment()])],
                     kind="requeue_triage"), [])

    def test_an_outsiders_comment_carrying_a_checklist_does_not_count(self):
        found = findings(
            [issue(10, labels=("pending-approval",),
                   comments=[comment(author="a-passing-contributor")])],
            kind="requeue_triage")
        self.assertEqual(len(found), 1)

    def test_the_owners_own_comment_does_count(self):
        """Triage runs under Derek's account, so his analyses are triage's.

        The accepted cost: a checklist he writes by hand reads as one too.
        """
        self.assertEqual(
            findings([issue(10, labels=("pending-approval",),
                            comments=[comment(author="derekwinters")])],
                     kind="requeue_triage"), [])

    def test_requeue_triage_is_omitted_on_the_event_path(self):
        """A just-triaged issue transiently has the label but not yet the comment."""
        self.assertEqual(
            findings([issue(10, labels=("pending-approval",))],
                     kind="requeue_triage", events_only=True), [])

    def test_an_ai_triage_issue_is_not_a_finding(self):
        self.assertEqual(
            findings([issue(10, labels=("ai-triage",))], kind="requeue_triage"), [])

    def test_a_parked_issue_is_left_alone(self):
        self.assertEqual(
            findings([issue(10, labels=("parked",))], kind="requeue_triage"), [])


class FlagTests(unittest.TestCase):
    def test_an_analysis_with_no_state_label_is_flagged_not_restored(self):
        """The intended state is ambiguous, so a human decides."""
        found = findings([issue(10, labels=("area:ui",), comments=[comment()])],
                         kind="flag_orphaned_analysis")

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["action"], "flag")

    def test_ready_for_work_without_a_milestone_is_flagged(self):
        found = findings([issue(10, labels=("ready-for-work",), milestone=None)],
                         kind="flag_orphaned_ready")
        self.assertEqual(len(found), 1)

    def test_a_prose_only_dependency_is_flagged(self):
        found = findings([issue(10, body="Blocked by #42")], kind="flag_prose_dependency")

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["blockers"], [42])

    def test_a_prose_dependency_backed_by_a_native_edge_is_not_flagged(self):
        self.assertEqual(
            findings([issue(10, body="Blocked by #42", native_blockers=(42,))],
                     kind="flag_prose_dependency"), [])

    def test_a_depends_on_line_is_not_a_prose_dependency(self):
        self.assertEqual(
            findings([issue(10, body="Depends on: #42")], kind="flag_prose_dependency"), [])

    def test_a_dependency_cycle_is_flagged(self):
        issues = [issue(10, native_blockers=(11,)), issue(11, native_blockers=(10,))]
        found = findings(issues, kind="flag_cycle")
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["issues"], [10, 11])

    def test_an_acyclic_graph_is_not_flagged(self):
        issues = [issue(10, native_blockers=(11,)), issue(11)]
        self.assertEqual(findings(issues, kind="flag_cycle"), [])

    def test_two_dashboard_issues_are_flagged(self):
        issues = [issue(10, labels=("dashboard",)), issue(11, labels=("dashboard",))]
        found = findings(issues, kind="flag_dashboard_count")
        self.assertEqual(len(found), 1)

    def test_no_dashboard_issue_is_flagged(self):
        self.assertEqual(len(findings([issue(10)], kind="flag_dashboard_count")), 1)

    def test_exactly_one_dashboard_is_not_flagged(self):
        self.assertEqual(findings([issue(10, labels=("dashboard",))],
                                  kind="flag_dashboard_count"), [])


class NeverClosesTests(unittest.TestCase):
    def test_no_finding_ever_closes_an_issue(self):
        issues = [
            issue(10, labels=("in-progress",), comments=[comment()]),
            issue(11, state="closed", labels=("ready-for-work",)),
            issue(12, labels=("dashboard",)),
        ]
        data = {
            "issues": issues,
            "focus": FOCUS,
            "merged_commits": [{"body": "feat: x\n\nCloses #10"}],
        }

        for finding in reconcile.process(data)["findings"]:
            self.assertNotIn("close", json.dumps(finding).lower())

    def test_merged_but_open_is_a_flag_not_an_auto_fix(self):
        found = findings([issue(10, labels=("in-progress",), comments=[comment()])],
                         kind="flag_merged_but_open",
                         merged_commits=[{"body": "feat: x\n\nCloses #10"}])
        self.assertEqual(found[0]["action"], "flag")


class DonenessTests(unittest.TestCase):
    """What counts as "this landed on main"."""

    def merged(self, body):
        return findings([issue(10, labels=("in-progress",), comments=[comment()])],
                        kind="flag_merged_but_open",
                        merged_commits=[{"body": body}])

    def test_a_closing_keyword_in_the_commit_body_counts(self):
        self.assertEqual(len(self.merged("feat: thing\n\nCloses #10")), 1)

    def test_a_bare_reference_does_not_count(self):
        self.assertEqual(self.merged("feat: thing\n\nSee #10"), [])

    def test_refs_does_not_count(self):
        self.assertEqual(self.merged("feat: thing\n\nRefs #10"), [])

    def test_a_closing_keyword_in_the_title_line_does_not_count(self):
        """Titles carry `(#10)` from squash merges — that is a PR number."""
        self.assertEqual(self.merged("feat: closes #10 eventually"), [])


class ShapeTests(unittest.TestCase):
    def test_process_is_pure(self):
        data = {"issues": [issue(10, labels=("dashboard",))], "focus": FOCUS}
        before = json.dumps(data, sort_keys=True)
        reconcile.process(data)
        self.assertEqual(json.dumps(data, sort_keys=True), before)

    def test_empty_input_is_not_an_error(self):
        result = reconcile.process({})
        self.assertIn("findings", result)

    def test_findings_carry_a_count_and_an_action_split(self):
        data = {"issues": [issue(10, state="closed", labels=("ready-for-work",))],
                "focus": FOCUS}
        result = reconcile.process(data)

        self.assertEqual(result["count"], len(result["findings"]))
        self.assertEqual(result["auto_fix_count"], 1)
        self.assertEqual(result["flag_count"], 1)  # the missing dashboard

    def test_main_reads_stdin_and_writes_json(self):
        data = {"issues": [issue(10, labels=("dashboard",))], "focus": FOCUS}
        out = StringIO()

        with patch.object(sys, "stdin", StringIO(json.dumps(data))), \
                patch.object(sys, "stdout", out):
            self.assertEqual(reconcile.main([]), 0)

        self.assertIn("findings", json.loads(out.getvalue()))

    def test_the_shared_recognizer_is_imported_not_redefined(self):
        """One implementation, per #65 — a second copy drifts into a loop."""
        source = (Path(__file__).resolve().parents[1] / "reconcile.py").read_text()
        self.assertNotIn("def has_analysis_signature", source)

    def test_the_triage_author_set_is_imported_not_redefined(self):
        """The copy that actually drifted: both sides said `github-actions[bot]`
        while every real triage comment came from `claude[bot]`."""
        source = (Path(__file__).resolve().parents[1] / "reconcile.py").read_text()
        self.assertNotIn("TRIAGE_AUTHOR =", source)
        self.assertNotIn("TRIAGE_AUTHORS =", source)

    def test_a_claude_app_analysis_is_seen(self):
        """The regression itself: this read as "no analysis" and requeued forever."""
        issue = {"labels": ["pending-approval"], "comments": [
            {"author": "claude[bot]", "body": "## Build checklist\n\n- [ ] x"}]}
        self.assertTrue(reconcile.has_analysis(issue))

    def test_an_outsiders_analysis_is_not_seen(self):
        issue = {"labels": ["pending-approval"], "comments": [
            {"author": "a-passing-contributor",
             "body": "## Build checklist\n\n- [ ] x"}]}
        self.assertFalse(reconcile.has_analysis(issue))

    def test_the_owners_analysis_is_seen(self):
        """Triage posted as `derekwinters` on #83 and #169, not only as a bot."""
        issue = {"labels": ["pending-approval"], "comments": [
            {"author": "derekwinters", "body": "## Build checklist\n\n- [ ] x"}]}
        self.assertTrue(reconcile.has_analysis(issue))


if __name__ == "__main__":
    unittest.main()
