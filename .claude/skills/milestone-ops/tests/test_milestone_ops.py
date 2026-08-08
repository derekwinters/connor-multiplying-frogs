"""Unit tests for the milestone helper."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import milestone_ops  # noqa: E402


def milestone(number, title, state="open", open_issues=0, closed_issues=0, description=""):
    return {
        "number": number,
        "title": title,
        "state": state,
        "open_issues": open_issues,
        "closed_issues": closed_issues,
        "description": description,
    }


class FakeApi:
    def __init__(self, responses=None):
        self.calls = []
        self.responses = responses or {}

    def __call__(self, method, path, payload=None):
        self.calls.append((method, path, payload))
        return self.responses.get(path, [])


class ResolveTests(unittest.TestCase):
    def api(self):
        return FakeApi({"/milestones?state=all&per_page=100": [
            milestone(1, "v0.0.1"),
            milestone(2, "v0.1"),
            milestone(3, "Direct Involvement Needed"),
        ]})

    def test_a_title_resolves_to_a_number(self):
        # The trap: issue_write's `milestone` takes the NUMBER, not the title.
        self.assertEqual(2, milestone_ops.resolve_number(self.api(), "v0.1"))

    def test_an_unknown_title_is_an_error_listing_what_exists(self):
        with self.assertRaises(milestone_ops.MilestoneError) as caught:
            milestone_ops.resolve_number(self.api(), "v9.9")

        self.assertIn("v0.0.1", str(caught.exception))

    def test_titles_are_compared_exactly(self):
        # v0.1 and V0.1 are different milestones. Normalising and hoping is how
        # work lands in the wrong one.
        with self.assertRaises(milestone_ops.MilestoneError):
            milestone_ops.resolve_number(self.api(), "V0.1")

    def test_a_title_with_spaces_resolves(self):
        self.assertEqual(3, milestone_ops.resolve_number(self.api(), "Direct Involvement Needed"))


class ListTests(unittest.TestCase):
    def test_it_reports_state_and_counts(self):
        api = FakeApi({"/milestones?state=all&per_page=100": [
            milestone(1, "v0.0.1", open_issues=12, closed_issues=40),
        ]})

        rows = milestone_ops.list_milestones(api)

        self.assertEqual(1, len(rows))
        self.assertEqual(("v0.0.1", "open", 12, 40), rows[0][1:5])

    def test_open_milestones_can_be_filtered(self):
        api = FakeApi({"/milestones?state=open&per_page=100": [milestone(1, "v0.0.1")]})

        self.assertEqual(1, len(milestone_ops.list_milestones(api, state="open")))


class OpenIssueCountTests(unittest.TestCase):
    def test_it_reads_the_count_from_the_milestone(self):
        api = FakeApi({"/milestones?state=all&per_page=100": [
            milestone(1, "v0.0.1", open_issues=12),
        ]})

        self.assertEqual(12, milestone_ops.open_issue_count(api, "v0.0.1"))

    def test_a_milestone_with_no_open_issues_is_zero_not_an_error(self):
        api = FakeApi({"/milestones?state=all&per_page=100": [milestone(1, "v0.0.1")]})

        self.assertEqual(0, milestone_ops.open_issue_count(api, "v0.0.1"))


class CloseAndReopenTests(unittest.TestCase):
    def api(self):
        return FakeApi({"/milestones?state=all&per_page=100": [
            milestone(1, "v0.0.1", open_issues=0),
            milestone(2, "v0.1", open_issues=3),
        ]})

    def test_closing_patches_the_state(self):
        api = self.api()

        milestone_ops.close_milestone(api, "v0.0.1")

        self.assertEqual(("PATCH", "/milestones/1", {"state": "closed"}), api.calls[-1])

    def test_closing_a_milestone_with_open_issues_is_refused(self):
        # Closing one with open work hides the work: it stops appearing in the
        # milestone view, and nothing else notices it exists.
        api = self.api()

        with self.assertRaises(milestone_ops.MilestoneError) as caught:
            milestone_ops.close_milestone(api, "v0.1")

        self.assertIn("3", str(caught.exception))

    def test_closing_can_be_forced(self):
        api = self.api()

        milestone_ops.close_milestone(api, "v0.1", force=True)

        self.assertEqual("PATCH", api.calls[-1][0])

    def test_reopening_patches_the_state(self):
        api = self.api()

        milestone_ops.reopen_milestone(api, "v0.0.1")

        self.assertEqual(("PATCH", "/milestones/1", {"state": "open"}), api.calls[-1])


class FrozenTests(unittest.TestCase):
    def test_a_frozen_milestone_is_recognised(self):
        self.assertTrue(milestone_ops.is_frozen("**FROZEN — no new intake.** Scope is settled."))

    def test_an_ordinary_description_is_not_frozen(self):
        self.assertFalse(milestone_ops.is_frozen("# Initialization\n\nInfrastructure setup."))

    def test_an_empty_description_is_not_frozen(self):
        self.assertFalse(milestone_ops.is_frozen(""))
        self.assertFalse(milestone_ops.is_frozen(None))


if __name__ == "__main__":
    unittest.main()
