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
