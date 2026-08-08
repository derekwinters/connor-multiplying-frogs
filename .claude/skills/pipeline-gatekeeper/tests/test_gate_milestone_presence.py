"""Tests for the /approve milestone-presence gate.

It refuses and explains; it never fixes the problem itself. A refusal leaves
the issue completely untouched, which these tests assert as hard as they assert
the refusal.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import gates  # noqa: E402


def issue(number=10, milestone="v0.0.1", state="open", labels=(), blockers=()):
    return {
        "number": number,
        "milestone": milestone,
        "state": state,
        "labels": list(labels),
        "blockers": list(blockers),
    }


class MilestonePresenceTests(unittest.TestCase):
    def test_a_milestone_lets_the_approval_through(self):
        verdict = gates.milestone_present(issue(milestone="v0.0.1"))

        self.assertTrue(verdict.ok, verdict.reason)

    def test_no_milestone_is_refused(self):
        verdict = gates.milestone_present(issue(milestone=None))

        self.assertFalse(verdict.ok)
        self.assertEqual("approve-no-milestone", verdict.skip_reason)

    def test_the_refusal_asks_which_milestone(self):
        verdict = gates.milestone_present(issue(milestone=None))

        self.assertIn("which milestone", verdict.reason.lower())

    def test_the_gate_never_picks_a_milestone(self):
        # Not even when only one is open. Auto-correcting at an approval gate
        # decides something the owner was in the middle of deciding.
        verdict = gates.milestone_present(issue(milestone=None))

        self.assertEqual([], verdict.changes)

    def test_it_reads_the_field_and_does_not_scrape_comments(self):
        # Triage sets the milestone field. Reading a "/milestone v0.1" out of a
        # comment would make the gate depend on comment history rather than
        # state, and the two disagree the moment anything is edited.
        verdict = gates.milestone_present(issue(milestone=None), comments=["/milestone v0.1"])

        self.assertFalse(verdict.ok)

    def test_an_empty_string_milestone_is_treated_as_absent(self):
        self.assertFalse(gates.milestone_present(issue(milestone="")).ok)


class CommandCoverageTests(unittest.TestCase):
    def test_the_presence_gate_runs_on_approve(self):
        self.assertIn("milestone-presence", gates.gates_for("approve"))

    def test_the_presence_gate_does_not_run_on_milestone(self):
        # /milestone is what fixes a missing milestone; gating it on having one
        # would make the fix impossible.
        self.assertNotIn("milestone-presence", gates.gates_for("milestone"))

    def test_park_is_not_gated(self):
        self.assertEqual([], gates.gates_for("park"))


if __name__ == "__main__":
    unittest.main()
