"""Tests for the dashboard renderer.

The centrepiece is a golden-snapshot test: render the fixture state and compare
byte-for-byte against a committed expected body. A dashboard is a wall of
generated Markdown, and a diff of the whole board is the only review that
catches a section quietly changing shape.
"""

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import render_dashboard as dash  # noqa: E402

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = FIXTURES / "expected_dashboard.md"


def state():
    return json.loads((FIXTURES / "state.json").read_text())


class GoldenTests(unittest.TestCase):
    def test_the_rendered_board_matches_the_golden_file(self):
        rendered = dash.render(state())
        expected = GOLDEN.read_text()

        if rendered != expected:  # pragma: no cover - only on a real mismatch
            self.fail(
                "Rendered dashboard differs from the golden file.\n"
                "If the change is intended, update "
                f"{GOLDEN.relative_to(Path.cwd()) if GOLDEN.is_relative_to(Path.cwd()) else GOLDEN}"
                " and review the diff.\n\n" + rendered)

    def test_rendering_is_byte_stable(self):
        """Same input, same bytes — or every hourly run is a spurious edit."""
        self.assertEqual(dash.render(state()), dash.render(state()))

    def test_the_render_replaces_the_old_body_entirely(self):
        self.assertNotIn("old rendered content", dash.render(state()))


class PieTests(unittest.TestCase):
    def counts(self, data=None):
        return dash.focus_pie(data or state())

    def test_the_four_slices_are_present(self):
        self.assertEqual(
            list(self.counts()), ["Unplanned", "In Planning", "Ready", "Done"])

    def test_every_focus_issue_lands_in_exactly_one_slice(self):
        data = state()
        focus_issues = [
            i for i in data["issues"]
            if i["milestone"] == data["focus"]
        ]

        self.assertEqual(sum(self.counts(data).values()), len(focus_issues))

    def test_parked_counts_as_unplanned(self):
        self.assertEqual(self.counts()["Unplanned"], 2)  # #15 parked, #16 no state

    def test_an_issue_with_no_state_label_counts_as_unplanned(self):
        data = state()
        data["issues"] = [i for i in data["issues"] if i["number"] != 15]
        self.assertEqual(self.counts(data)["Unplanned"], 1)

    def test_closed_counts_as_done_whatever_its_labels(self):
        self.assertEqual(self.counts()["Done"], 1)

    def test_ready_and_in_progress_are_both_ready(self):
        self.assertEqual(self.counts()["Ready"], 3)  # #10, #11, #12

    def test_planning_covers_triage_pending_and_clarification(self):
        self.assertEqual(self.counts()["In Planning"], 2)  # #13, #14

    def test_issues_outside_the_focus_milestone_are_excluded(self):
        """#18 is v0.0.2 and #78 has no milestone."""
        self.assertEqual(sum(self.counts().values()), 8)


class ResolveFocusTests(unittest.TestCase):
    def test_an_override_wins_over_the_marker(self):
        self.assertEqual(dash.resolve_focus(state(), override="v0.0.2"), "v0.0.2")

    def test_the_marker_is_used_when_there_is_no_override(self):
        data = state()
        data.pop("focus")
        self.assertEqual(dash.resolve_focus(data), "v0.0.1")

    def test_an_override_naming_no_live_milestone_is_rejected(self):
        with self.assertRaises(ValueError):
            dash.resolve_focus(state(), override="v9.9.9")

    def test_the_rejection_names_the_live_milestones(self):
        with self.assertRaises(ValueError) as caught:
            dash.resolve_focus(state(), override="v9.9.9")
        self.assertIn("v0.0.1", str(caught.exception))


class ResolveCapTests(unittest.TestCase):
    def test_an_override_wins(self):
        self.assertEqual(dash.resolve_cap(state(), override=5), 5)

    def test_the_marker_is_used_next(self):
        self.assertEqual(dash.resolve_cap(state()), 2)

    def test_the_default_is_three(self):
        data = state()
        data["dashboard_issue"]["body"] = "# Pipeline\n"
        self.assertEqual(dash.resolve_cap(data), 3)

    def test_a_malformed_marker_falls_back_to_the_default(self):
        data = state()
        data["dashboard_issue"]["body"] = "<!-- pipeline-cap: lots -->"
        self.assertEqual(dash.resolve_cap(data), 3)


class MarkerTests(unittest.TestCase):
    def test_both_markers_are_re_emitted(self):
        rendered = dash.render(state())
        self.assertIn("<!-- pipeline-focus: v0.0.1 -->", rendered)
        self.assertIn("<!-- pipeline-cap: 2 -->", rendered)

    def test_an_override_is_written_into_the_marker(self):
        rendered = dash.render(state(), focus_override="v0.0.2", cap_override=4)
        self.assertIn("<!-- pipeline-focus: v0.0.2 -->", rendered)
        self.assertIn("<!-- pipeline-cap: 4 -->", rendered)

    def test_markers_survive_a_round_trip(self):
        """Render, feed the output back in, and the settings are unchanged."""
        data = state()
        data["dashboard_issue"]["body"] = dash.render(data, cap_override=7)
        data.pop("focus")

        self.assertEqual(dash.resolve_cap(data), 7)
        self.assertEqual(dash.resolve_focus(data), "v0.0.1")


class ReadyQueueTests(unittest.TestCase):
    def test_the_queue_is_headed_by_the_cap(self):
        self.assertIn("cap 2", dash.render(state()))

    def test_ready_issues_appear_in_the_queue(self):
        rendered = dash.render(state())
        self.assertIn("#10", rendered)
        self.assertIn("#11", rendered)

    def test_in_progress_is_not_in_the_ready_queue(self):
        queue = dash.ready_queue(state())
        self.assertNotIn(12, [i["number"] for i in queue])

    def test_a_parked_issue_is_never_in_the_ready_queue(self):
        data = state()
        data["issues"].append({
            "number": 20, "title": "Parked but ready", "state": "open",
            "labels": ["ready-for-work", "parked"], "milestone": "v0.0.1",
            "body": "", "native_blockers": []})
        self.assertNotIn(20, [i["number"] for i in dash.ready_queue(data)])


class UnblockerTests(unittest.TestCase):
    def test_an_issue_blocking_another_is_an_unblocker(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked on 10", "state": "open",
            "labels": ["pending-approval"], "milestone": "v0.0.1",
            "body": "", "native_blockers": [10]})

        stars = dash.compute_unblockers(data)
        self.assertEqual(stars.get(10), [30])

    def test_an_issue_blocking_nothing_gets_no_star(self):
        self.assertEqual(dash.compute_unblockers(state()), {})

    def test_a_blocked_issue_is_not_itself_an_unblocker(self):
        """Starring a blocked issue would point at work nobody can start."""
        data = state()
        data["issues"] += [
            {"number": 30, "title": "Middle", "state": "open", "labels": ["ai-triage"],
             "milestone": "v0.0.1", "body": "", "native_blockers": [31]},
            {"number": 31, "title": "Root", "state": "open", "labels": ["ai-triage"],
             "milestone": "v0.0.1", "body": "", "native_blockers": []},
            {"number": 32, "title": "Leaf", "state": "open", "labels": ["ai-triage"],
             "milestone": "v0.0.1", "body": "", "native_blockers": [30]},
        ]

        stars = dash.compute_unblockers(data)
        self.assertIn(31, stars)
        self.assertNotIn(30, stars)

    def test_a_closed_blocker_is_not_starred(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked on a closed issue", "state": "open",
            "labels": ["ai-triage"], "milestone": "v0.0.1",
            "body": "", "native_blockers": [17]})
        self.assertNotIn(17, dash.compute_unblockers(data))

    def test_text_blockers_count_towards_stars(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked in prose", "state": "open",
            "labels": ["ai-triage"], "milestone": "v0.0.1",
            "body": "Blocked by #10", "native_blockers": []})
        self.assertEqual(dash.compute_unblockers(data).get(10), [30])

    def test_a_star_lists_every_issue_it_unblocks(self):
        data = state()
        for number in (30, 31):
            data["issues"].append({
                "number": number, "title": f"Blocked {number}", "state": "open",
                "labels": ["ai-triage"], "milestone": "v0.0.1",
                "body": "", "native_blockers": [10]})
        self.assertEqual(dash.compute_unblockers(data).get(10), [30, 31])

    def test_starred_issues_sort_to_the_top_of_their_table(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked on 11", "state": "open",
            "labels": ["ai-triage"], "milestone": "v0.0.1",
            "body": "", "native_blockers": [11]})

        queue = dash.ready_queue(data)
        self.assertEqual([i["number"] for i in queue], [11, 10])

    def test_the_star_renders_with_what_it_unblocks(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked on 10", "state": "open",
            "labels": ["ai-triage"], "milestone": "v0.0.1",
            "body": "", "native_blockers": [10]})
        self.assertIn("⭐ unblocks #30", dash.render(data))

    def test_a_blocked_row_is_flagged(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Blocked on 10", "state": "open",
            "labels": ["pending-approval"], "milestone": "v0.0.1",
            "body": "", "native_blockers": [10]})
        self.assertIn("⛔ blocked", dash.render(data))


class SectionTests(unittest.TestCase):
    def test_the_intake_table_lists_ai_triage_issues(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Fresh intake", "state": "open",
            "labels": ["ai-triage"], "milestone": "v0.0.1",
            "body": "", "native_blockers": []})
        self.assertIn("Fresh intake", dash.render(data))

    def test_pending_approval_issues_are_listed(self):
        self.assertIn("Frog hop animation", dash.render(state()))

    def test_needs_clarification_issues_are_listed(self):
        self.assertIn("Decide the pond background", dash.render(state()))

    def test_the_parked_section_lists_parked_work(self):
        rendered = dash.render(state())
        self.assertIn("⏸️ Parked", rendered)
        self.assertIn("Splash screen", rendered)

    def test_every_issue_table_has_a_milestone_column(self):
        """Four tables render on the fixture: ready, intake, pending, clarify."""
        rendered = dash.render(state())
        self.assertEqual(rendered.count("| Issue | Title | Milestone | Blocked by |"), 4)

    def test_the_reconcile_section_lists_flag_findings(self):
        data = state()
        data["reconcile_findings"] = [
            {"kind": "flag_orphaned_ready", "action": "flag", "issue": 10}]
        rendered = dash.render(data)

        self.assertIn("⚠️ Reconcile", rendered)
        self.assertIn("flag_orphaned_ready", rendered)

    def test_the_reconcile_section_says_so_when_clean(self):
        data = state()
        data["reconcile_findings"] = []
        self.assertIn("Nothing flagged.", dash.render(data))

    def test_auto_fix_findings_are_not_listed(self):
        """Auto-fixes are already fixed; listing them is noise."""
        data = state()
        data["reconcile_findings"] = [
            {"kind": "strip_labels", "action": "auto-fix", "issue": 17}]
        self.assertNotIn("strip_labels", dash.render(data))

    def test_other_milestones_show_progress(self):
        rendered = dash.render(state())
        self.assertIn("v0.0.2", rendered)

    def test_a_fully_done_milestone_is_omitted(self):
        data = state()
        data["issues"] = [i for i in data["issues"] if i["number"] != 18]
        data["issues"].append({
            "number": 40, "title": "All done here", "state": "closed",
            "labels": [], "milestone": "v0.0.2", "body": "", "native_blockers": []})

        rendered = dash.render(data)
        self.assertNotIn("| v0.0.2 |", rendered)

    def test_the_command_reference_is_rendered(self):
        rendered = dash.render(state())
        self.assertIn("/approve", rendered)
        self.assertIn("/park", rendered)


class AttentionSectionScopeTests(unittest.TestCase):
    """Intake, Waiting for you, Needs clarification and Parked are board-wide.

    Only the pie and the ready queue are scoped to the focus milestone. The
    sections that exist to say *somebody has to look at this* are not, because
    the issues that most need looking at are exactly the ones no milestone has
    been decided for yet — an `ai-triage` issue has not been triaged, and
    triage is what assigns the milestone. Scoping Intake to the focus
    milestone makes it structurally near-empty.
    """

    def issue(self, number, title, labels, milestone):
        return {
            "number": number, "title": title, "state": "open",
            "labels": labels, "milestone": milestone,
            "body": "", "native_blockers": []}

    def with_issue(self, *args):
        data = state()
        data["issues"].append(self.issue(*args))
        return data

    def test_intake_lists_an_untriaged_issue_with_no_milestone(self):
        data = self.with_issue(30, "Nobody has scheduled this", ["ai-triage"], None)
        self.assertIn(30, [i["number"] for i in dash.intake(data)])

    def test_intake_lists_an_ai_triage_issue_in_another_milestone(self):
        self.assertIn(18, [i["number"] for i in dash.intake(state())])

    def test_pending_approval_outside_the_focus_milestone_is_listed(self):
        data = self.with_issue(
            31, "Derek: add the signing secret", ["pending-approval"],
            "Direct Involvement Needed")
        self.assertIn(31, [i["number"] for i in dash.pending(data)])

    def test_needs_clarification_outside_the_focus_milestone_is_listed(self):
        data = self.with_issue(32, "App icon", ["needs-clarification"], None)
        self.assertIn(32, [i["number"] for i in dash.needs_clarification(data)])

    def test_parked_outside_the_focus_milestone_is_listed(self):
        data = self.with_issue(33, "Sound effects", ["parked"], "v0.0.2")
        self.assertIn(33, [i["number"] for i in dash.parked(data)])

    def test_an_out_of_focus_row_names_its_own_milestone(self):
        """The Milestone column is what tells these rows apart."""
        rendered = dash.render(state())
        self.assertIn("| #18 | A later-milestone issue | v0.0.2 |", rendered)

    def test_a_row_with_no_milestone_renders_an_em_dash(self):
        data = self.with_issue(30, "Nobody has scheduled this", ["ai-triage"], None)
        self.assertIn("| #30 | Nobody has scheduled this | — |", dash.render(data))

    def test_the_ready_queue_is_still_focus_scoped(self):
        """The builder builds the focus milestone. Widening this queue would
        put work in front of Derek that the nightly builder will not pick up."""
        data = self.with_issue(34, "A later-milestone build", ["ready-for-work"], "v0.0.2")
        self.assertNotIn(34, [i["number"] for i in dash.ready_queue(data)])

    def test_the_pie_is_still_focus_scoped(self):
        data = self.with_issue(35, "Elsewhere", ["ai-triage"], "v0.0.2")
        self.assertEqual(sum(dash.focus_pie(data).values()), 8)


class UnadmittedTests(unittest.TestCase):
    """Intake also carries issues nobody has `/admit`ted, flagged as such.

    They are waiting on a different thing from an `ai-triage` row — the
    nightly run will never pick them up, only `/admit` moves them — so the
    flag is what stops one table meaning two things.
    """

    def with_issue(self, number, title, labels, milestone=None, closed=False):
        data = state()
        data["issues"].append({
            "number": number, "title": title,
            "state": "closed" if closed else "open",
            "labels": labels, "milestone": milestone,
            "body": "", "native_blockers": []})
        return data

    def numbers(self, data):
        return [i["number"] for i in dash.intake(data)]

    def test_an_unadmitted_issue_is_listed_in_intake(self):
        data = self.with_issue(30, "Never admitted", ["type:task"])
        self.assertIn(30, self.numbers(data))

    def test_an_unadmitted_issue_in_the_focus_milestone_is_listed(self):
        self.assertIn(16, self.numbers(state()))

    def test_an_unadmitted_row_is_flagged(self):
        data = self.with_issue(30, "Never admitted", ["type:task"])
        self.assertIn("🚪 not admitted — Never admitted", dash.render(data))

    def test_an_ai_triage_row_is_not_flagged(self):
        """The flag has to tell the two piles apart, so it cannot be on both."""
        self.assertIn("| #18 | A later-milestone issue |", dash.render(state()))

    def test_the_dashboard_issue_is_never_in_intake(self):
        """The board is the pipeline's furniture, not work for the pipeline."""
        self.assertNotIn(78, self.numbers(state()))

    def test_an_epic_is_not_in_intake(self):
        """`/admit` on an epic is refused, so offering it would be a dead end."""
        data = self.with_issue(31, "The whole pond", ["type:epic"])
        self.assertNotIn(31, self.numbers(data))

    def test_a_closed_unadmitted_issue_is_not_in_intake(self):
        data = self.with_issue(32, "Done long ago", ["type:task"], closed=True)
        self.assertNotIn(32, self.numbers(data))

    def test_a_parked_issue_is_not_treated_as_unadmitted(self):
        """`parked` is a state label, and a deliberate one."""
        data = self.with_issue(33, "Set aside", ["parked", "type:task"])
        self.assertNotIn(33, self.numbers(data))

    def test_the_pie_still_adds_up(self):
        """Intake widening must not move an issue between slices."""
        counts = dash.focus_pie(state())
        self.assertEqual(sum(counts.values()), 8)
        self.assertEqual(counts["Unplanned"], 2)


class ParkedExclusionTests(unittest.TestCase):
    def parked_ready(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Parked but labelled ready", "state": "open",
            "labels": ["ready-for-work", "parked"], "milestone": "v0.0.1",
            "body": "", "native_blockers": []})
        return data

    def test_parked_is_excluded_from_the_ready_queue(self):
        numbers = [i["number"] for i in dash.ready_queue(self.parked_ready())]
        self.assertNotIn(30, numbers)

    def test_parked_is_excluded_from_intake_and_planning_tables(self):
        data = state()
        data["issues"].append({
            "number": 30, "title": "Parked mid-triage", "state": "open",
            "labels": ["ai-triage", "parked"], "milestone": "v0.0.1",
            "body": "", "native_blockers": []})
        self.assertNotIn(30, [i["number"] for i in dash.intake(data)])

    def test_parked_still_counts_in_the_unplanned_slice(self):
        """The one deliberate exception — otherwise the pie stops adding up."""
        counts = dash.focus_pie(self.parked_ready())
        self.assertEqual(sum(counts.values()), 9)
        self.assertEqual(counts["Unplanned"], 3)


class AsOfTests(unittest.TestCase):
    """The timestamp, and how it avoids defeating byte-stability."""

    def test_no_timestamp_is_rendered_by_default(self):
        self.assertNotIn("as of", dash.render(state()))

    def test_the_timestamp_is_rendered_when_given(self):
        rendered = dash.render(state(), as_of="8 Aug 2026, 3:04 PM CDT")
        self.assertIn("8 Aug 2026, 3:04 PM CDT", rendered)

    def test_rendering_is_still_byte_stable_for_a_given_timestamp(self):
        stamp = "8 Aug 2026, 3:04 PM CDT"
        self.assertEqual(dash.render(state(), as_of=stamp),
                         dash.render(state(), as_of=stamp))

    def test_two_renders_differing_only_by_timestamp_are_not_a_change(self):
        """Otherwise every scheduled run rewrites the issue for nothing."""
        first = dash.render(state(), as_of="8 Aug 2026, 3:04 PM CDT")
        second = dash.render(state(), as_of="8 Aug 2026, 4:04 PM CDT")

        self.assertNotEqual(first, second)
        self.assertFalse(dash.body_changed(first, second))

    def test_a_real_change_is_still_a_change(self):
        data = state()
        first = dash.render(data, as_of="8 Aug 2026, 3:04 PM CDT")

        data["issues"].append({
            "number": 30, "title": "Something new", "state": "open",
            "labels": ["ready-for-work"], "milestone": "v0.0.1",
            "body": "", "native_blockers": []})
        second = dash.render(data, as_of="8 Aug 2026, 4:04 PM CDT")

        self.assertTrue(dash.body_changed(first, second))

    def test_a_body_that_never_had_a_timestamp_still_compares(self):
        rendered = dash.render(state())
        self.assertFalse(dash.body_changed(rendered, rendered))


class PurityTests(unittest.TestCase):
    def test_render_does_not_mutate_its_input(self):
        data = state()
        before = json.dumps(data, sort_keys=True)
        dash.render(data)
        self.assertEqual(json.dumps(data, sort_keys=True), before)

    def test_the_module_makes_no_network_calls_at_import(self):
        source = (Path(__file__).resolve().parents[1] / "render_dashboard.py").read_text()
        self.assertNotIn("urllib.request.urlopen", source.split("def write_dashboard")[0])


if __name__ == "__main__":
    unittest.main()
