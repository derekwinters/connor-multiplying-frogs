"""Tests for the milestone-order gate.

Invariant: for every open blocker edge A → B, milestone_order(A) >= 
milestone_order(B), and B must be scheduled.

It refuses and never auto-bumps, so a refusal leaving the issue completely
untouched is asserted as hard as the refusal itself.
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


MILESTONES = ["v0.0.1", "v0.1", "v0.2", "Direct Involvement Needed"]


class MilestoneOrderParsingTests(unittest.TestCase):
    def test_a_version_title_is_ordered(self):
        self.assertLess(gates.milestone_order("v0.0.1"), gates.milestone_order("v0.1"))

    def test_a_patch_version_orders_within_its_minor(self):
        self.assertLess(gates.milestone_order("v0.0.1"), gates.milestone_order("v0.0.2"))

    def test_a_major_bump_orders_above_a_minor(self):
        self.assertLess(gates.milestone_order("v0.9"), gates.milestone_order("v1.0"))

    def test_a_non_version_title_is_unordered(self):
        self.assertIsNone(gates.milestone_order("Direct Involvement Needed"))

    def test_no_milestone_is_unordered(self):
        self.assertIsNone(gates.milestone_order(None))


class MilestoneOrderGateTests(unittest.TestCase):
    def check(self, subject_milestone, blockers):
        return gates.milestone_order_ok(
            issue(milestone=subject_milestone, blockers=blockers), MILESTONES)

    def blocker(self, number=20, milestone="v0.0.1", state="open", labels=()):
        return {"number": number, "milestone": milestone, "state": state, "labels": list(labels)}

    def test_a_blocker_in_an_earlier_milestone_is_fine(self):
        verdict = self.check("v0.1", [self.blocker(milestone="v0.0.1")])

        self.assertTrue(verdict.ok, verdict.reason)

    def test_a_blocker_in_the_same_milestone_is_fine(self):
        self.assertTrue(self.check("v0.1", [self.blocker(milestone="v0.1")]).ok)

    def test_a_blocker_in_a_later_milestone_is_refused(self):
        verdict = self.check("v0.0.1", [self.blocker(number=20, milestone="v0.1")])

        self.assertFalse(verdict.ok)
        self.assertEqual("blocker-inversion", verdict.skip_reason)
        self.assertIn("#20", verdict.reason)

    def test_an_unscheduled_blocker_is_refused(self):
        verdict = self.check("v0.0.1", [self.blocker(number=20, milestone=None)])

        self.assertFalse(verdict.ok)
        self.assertEqual("blocker-unscheduled", verdict.skip_reason)

    def test_a_blocker_in_an_unordered_milestone_is_refused(self):
        # Direct Involvement Needed never ships, so a blocker parked there is
        # one nothing will ever build.
        verdict = self.check("v0.0.1",
                             [self.blocker(number=20, milestone="Direct Involvement Needed")])

        self.assertFalse(verdict.ok)
        self.assertEqual("blocker-unscheduled", verdict.skip_reason)

    def test_a_closed_blocker_is_ignored(self):
        verdict = self.check("v0.0.1", [self.blocker(milestone="v0.2", state="closed")])

        self.assertTrue(verdict.ok, verdict.reason)

    def test_the_refusal_changes_nothing(self):
        verdict = self.check("v0.0.1", [self.blocker(milestone="v0.1")])

        self.assertEqual([], verdict.changes)

    def test_it_never_bumps_the_milestone(self):
        # Refuse, never auto-bump: moving the issue would quietly re-plan a
        # milestone's worth of work.
        verdict = self.check("v0.0.1", [self.blocker(milestone="v0.1")])

        self.assertNotIn("milestone", str(verdict.changes))

    def test_every_offending_blocker_is_named(self):
        verdict = self.check("v0.0.1", [
            self.blocker(number=20, milestone="v0.1"),
            self.blocker(number=21, milestone="v0.2"),
        ])

        self.assertIn("#20", verdict.reason)
        self.assertIn("#21", verdict.reason)

    def test_no_blockers_passes(self):
        self.assertTrue(self.check("v0.0.1", []).ok)

    def test_an_unscheduled_subject_is_not_this_gate_s_problem(self):
        # The presence gate handles that, and two gates reporting the same
        # thing produces two acks for one mistake.
        self.assertTrue(self.check(None, []).ok)


class SoftDependencyTests(unittest.TestCase):
    def test_a_soft_dependency_uses_the_same_refuse_rule(self):
        subject = issue(milestone="v0.0.1")
        subject["soft_dependencies"] = [
            {"number": 30, "milestone": "v0.1", "state": "open", "labels": []}]

        verdict = gates.milestone_order_ok(subject, MILESTONES)

        self.assertFalse(verdict.ok)
        self.assertIn("#30", verdict.reason)

    def test_a_closed_soft_dependency_is_ignored(self):
        subject = issue(milestone="v0.0.1")
        subject["soft_dependencies"] = [
            {"number": 30, "milestone": "v0.1", "state": "closed", "labels": []}]

        self.assertTrue(gates.milestone_order_ok(subject, MILESTONES).ok)


class CommandCoverageTests(unittest.TestCase):
    def test_both_gates_run_on_approve(self):
        self.assertEqual({"milestone-presence", "milestone-order"}, set(gates.gates_for("approve")))

    def test_the_order_gate_runs_on_milestone(self):
        # Setting a milestone is the other way to create an inversion.
        self.assertIn("milestone-order", gates.gates_for("milestone"))

    def test_the_presence_gate_does_not_run_on_milestone(self):
        # /milestone is what fixes a missing milestone; gating it on having one
        # would make the fix impossible.
        self.assertNotIn("milestone-presence", gates.gates_for("milestone"))

    def test_park_is_not_gated(self):
        self.assertEqual([], gates.gates_for("park"))


if __name__ == "__main__":
    unittest.main()
