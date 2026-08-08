"""Tests for label merging, acks, and the reaction watermark."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import apply_actions as apply  # noqa: E402
import parse_commands as parser  # noqa: E402


def action(command, argument=""):
    return parser.Action(command, argument)


class LabelMergeTests(unittest.TestCase):
    def test_approve_moves_to_ready_for_work(self):
        result = apply.plan(["pending-approval"], [action("approve")])

        self.assertEqual({"ready-for-work"}, set(result.labels))

    def test_unrelated_labels_are_never_clobbered(self):
        # The label set carries area/type as well as state. A state change that
        # dropped them would lose the triage decision entirely.
        result = apply.plan(
            ["pending-approval", "area:build", "type:task"], [action("approve")])

        self.assertEqual({"ready-for-work", "area:build", "type:task"}, set(result.labels))

    def test_only_one_state_label_survives(self):
        result = apply.plan(["ai-triage", "pending-approval"], [action("approve")])

        self.assertEqual({"ready-for-work"}, set(result.labels))

    def test_park_replaces_whatever_state_was_there(self):
        result = apply.plan(["ready-for-work"], [action("park")])

        self.assertEqual({"parked"}, set(result.labels))

    def test_unpark_returns_to_triage(self):
        self.assertEqual({"ai-triage"}, set(apply.plan(["parked"], [action("unpark")]).labels))

    def test_admit_sets_triage(self):
        self.assertEqual({"ai-triage"}, set(apply.plan([], [action("admit")]).labels))

    def test_revise_returns_to_triage(self):
        result = apply.plan(["pending-approval"], [action("revise", "too big, split it")])

        self.assertEqual({"ai-triage"}, set(result.labels))

    def test_redo_requeues_completed_work(self):
        self.assertEqual({"ready-for-work"}, set(apply.plan([], [action("redo")]).labels))

    def test_several_actions_apply_in_order(self):
        result = apply.plan(["pending-approval"], [action("approve"), action("park")])

        self.assertEqual({"parked"}, set(result.labels))

    def test_milestone_changes_no_labels(self):
        result = apply.plan(["pending-approval"], [action("milestone", "v0.1")])

        self.assertEqual({"pending-approval"}, set(result.labels))
        self.assertEqual("v0.1", result.milestone)

    def test_focus_and_cap_change_no_labels(self):
        result = apply.plan(["dashboard"], [action("focus", "v0.1"), action("cap", "2")])

        self.assertEqual({"dashboard"}, set(result.labels))
        self.assertEqual("v0.1", result.focus)
        self.assertEqual(2, result.cap)


class FiresTriageTests(unittest.TestCase):
    def test_newly_added_triage_fires(self):
        self.assertTrue(apply.fires_triage(["pending-approval"], ["ai-triage"]))

    def test_an_idempotent_re_add_does_not_fire(self):
        # A replay must not fire triage a second time.
        self.assertFalse(apply.fires_triage(["ai-triage"], ["ai-triage"]))

    def test_removing_triage_does_not_fire(self):
        self.assertFalse(apply.fires_triage(["ai-triage"], ["ready-for-work"]))

    def test_no_change_does_not_fire(self):
        self.assertFalse(apply.fires_triage(["ready-for-work"], ["ready-for-work"]))


class AckTests(unittest.TestCase):
    def test_an_applied_action_gets_a_your_move_ack(self):
        text = apply.acknowledgement([action("approve")], [])

        self.assertIn("ready-for-work", text)

    def test_the_ack_says_what_happens_next(self):
        text = apply.acknowledgement([action("approve")], [])

        self.assertIn("next", text.lower())

    def test_a_refusal_explains_and_says_nothing_changed(self):
        skip = parser.Skip("approve-no-milestone", "This issue has no milestone.", "approve")

        text = apply.acknowledgement([], [skip])

        self.assertIn("no milestone", text.lower())
        self.assertIn("nothing", text.lower())

    def test_an_unknown_command_ack_names_the_closest_match(self):
        skip = parser.Skip("unknown-command", "/aprove is not a command. Did you mean /approve?", "aprove")

        self.assertIn("/approve", apply.acknowledgement([], [skip]))

    def test_a_not_owner_skip_produces_no_ack_at_all(self):
        # Replying would let a stranger make the bot post.
        skip = parser.Skip("not-owner", "/approve from someone-else", "approve")

        self.assertEqual("", apply.acknowledgement([], [skip]))

    def test_an_already_applied_skip_produces_no_ack(self):
        skip = parser.Skip("already-applied", "/approve was applied earlier", "approve")

        self.assertEqual("", apply.acknowledgement([], [skip]))

    def test_actions_and_refusals_appear_together(self):
        skip = parser.Skip("cap-invalid", "/cap needs a positive whole number.", "cap")

        text = apply.acknowledgement([action("approve")], [skip])

        self.assertIn("ready-for-work", text)
        self.assertIn("cap", text)


class WatermarkTests(unittest.TestCase):
    def test_the_watermark_is_the_eyes_reaction(self):
        self.assertEqual("eyes", apply.WATERMARK)

    def test_an_applied_comment_is_watermarked(self):
        self.assertTrue(apply.should_watermark([action("approve")], []))

    def test_a_refused_comment_is_still_watermarked(self):
        # It was considered. Without the mark the sweep reconsiders it forever.
        skip = parser.Skip("cap-invalid", "…", "cap")

        self.assertTrue(apply.should_watermark([], [skip]))

    def test_a_stranger_s_comment_is_not_watermarked(self):
        skip = parser.Skip("not-owner", "…", "approve")

        self.assertFalse(apply.should_watermark([], [skip]))

    def test_a_comment_with_nothing_in_it_is_not_watermarked(self):
        self.assertFalse(apply.should_watermark([], []))


class NoIoTests(unittest.TestCase):
    def test_the_module_imports_nothing_that_can_reach_the_network(self):
        import ast

        source = Path(apply.__file__).read_text()
        imported = set()

        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported.update(alias.name.split(".")[0] for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imported.add(node.module.split(".")[0])

        self.assertEqual(set(), imported & {"urllib", "http", "socket", "subprocess", "requests"})


if __name__ == "__main__":
    unittest.main()
