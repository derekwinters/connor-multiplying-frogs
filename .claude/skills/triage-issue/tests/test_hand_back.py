"""Tests for the hand-back write — the comment, its footer, and the label move.

The whole point of this module is that the hand-back is not a judgement the
agent makes at the end of a long analysis. It is one call that either does both
writes, in order, or refuses.
"""

import re
import sys
import unittest
from pathlib import Path

SKILLS = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(SKILLS / "pipeline-gatekeeper"))

import hand_back  # noqa: E402
import parse_commands  # noqa: E402

ANALYSIS = """\
A wrong answer on the Start log moves the frog off the bottom of its lane.

## Build checklist

- [ ] Clamp a back move at the Start log
"""

QUESTION = """\
This one needs a decision before it can be planned.

❓ **Needs from Derek/Connor:** should a frog hop backwards on a wrong answer?
"""

PENDING = "pending-approval"
CLARIFY = "needs-clarification"


class FakeApi:
    """Records every call so ordering can be asserted."""

    def __init__(self, fail_on=None):
        self.calls = []
        self.fail_on = fail_on

    def __call__(self, method, path, body=None):
        self.calls.append((method, path, body))
        if self.fail_on and self.fail_on in path and method == self.fail_on_method:
            raise RuntimeError("the label write failed")
        return {}

    fail_on_method = "PUT"

    @property
    def methods(self):
        return [method for method, _, _ in self.calls]


class LabelPlanTests(unittest.TestCase):
    """`plan_labels` — exactly one state label, and the rest untouched."""

    def test_ai_triage_comes_off_and_the_new_state_goes_on(self):
        result = hand_back.plan_labels(["ai-triage", "area:ai", "type:bug"], PENDING)
        self.assertEqual(sorted(result), ["area:ai", PENDING, "type:bug"])

    def test_the_issue_never_carries_both(self):
        result = hand_back.plan_labels(["ai-triage"], PENDING)
        self.assertNotIn("ai-triage", result)
        self.assertIn(PENDING, result)

    def test_a_different_prior_state_is_replaced_not_stacked(self):
        result = hand_back.plan_labels(["needs-clarification", "area:ui"], PENDING)
        self.assertEqual(sorted(result), ["area:ui", PENDING])

    def test_area_type_and_skip_docs_survive(self):
        labels = ["ai-triage", "area:gameplay", "type:task", "skip-docs"]
        result = hand_back.plan_labels(labels, CLARIFY)
        self.assertEqual(
            sorted(result), ["area:gameplay", CLARIFY, "skip-docs", "type:task"])

    def test_exactly_one_state_label_survives(self):
        messy = ["ai-triage", "pending-approval", "parked", "area:ai"]
        result = hand_back.plan_labels(messy, CLARIFY)
        states = [label for label in result if label in hand_back.STATE_LABELS]
        self.assertEqual(states, [CLARIFY])

    def test_it_is_idempotent(self):
        once = hand_back.plan_labels(["ai-triage", "area:ai"], PENDING)
        twice = hand_back.plan_labels(once, PENDING)
        self.assertEqual(sorted(once), sorted(twice))

    def test_a_state_triage_may_not_set_is_refused(self):
        for state in ("ready-for-work", "in-progress", "parked", "nonsense"):
            with self.assertRaises(ValueError):
                hand_back.plan_labels(["ai-triage"], state)


class FooterTests(unittest.TestCase):
    """The footer names what to type next, and only commands that exist."""

    def _commands_in(self, text):
        return set(re.findall(r"(?<![\w/])/([a-z]+)", text))

    def test_a_pending_approval_footer_offers_approve_and_revise(self):
        footer = hand_back.footer(PENDING)
        self.assertIn("/approve", footer)
        self.assertIn("/revise", footer)

    def test_a_needs_clarification_footer_does_not_offer_approve(self):
        """There is no plan on this route, so there is nothing to approve."""
        self.assertNotIn("/approve", hand_back.footer(CLARIFY))

    def test_a_needs_clarification_footer_says_how_to_send_it_back(self):
        self.assertIn("/revise", hand_back.footer(CLARIFY))

    def test_every_command_named_is_a_real_command(self):
        """The regression this test exists for: `/retriage` does not exist."""
        for state in hand_back.HAND_BACK_STATES:
            for command in self._commands_in(hand_back.footer(state)):
                self.assertIn(
                    command, parse_commands.COMMANDS,
                    f"{state} footer names /{command}, which the parser refuses")

    def test_the_clarification_route_does_not_claim_there_is_a_plan(self):
        """`/revise`'s usual gloss is about rejecting a plan. This route has none."""
        self.assertNotIn("plan is not right", hand_back.footer(CLARIFY))

    def test_the_clarification_route_asks_for_the_answer(self):
        self.assertIn("answer", hand_back.footer(CLARIFY).lower())

    def test_no_line_stacks_two_dashes(self):
        """`- /approve — the plan is right — build it` is a sentence nobody parses."""
        for state in hand_back.HAND_BACK_STATES:
            for line in hand_back.footer(state).splitlines():
                self.assertLessEqual(line.count("—"), 1, line)

    def test_the_glosses_come_from_the_dashboard_list(self):
        """One definition of what a command means, not a third copy."""
        sys.path.insert(0, str(SKILLS / "pipeline-dashboard"))
        import render_dashboard  # noqa: E402

        glosses = dict(render_dashboard.COMMANDS)
        self.assertIn(glosses["/approve"], hand_back.footer(PENDING))

    def test_an_unknown_state_has_no_footer(self):
        with self.assertRaises(ValueError):
            hand_back.footer("ready-for-work")


class CommentTests(unittest.TestCase):
    """`build_comment` — the analysis, then the footer, in that order."""

    def test_the_analysis_comes_first_and_is_unchanged(self):
        body = hand_back.build_comment(ANALYSIS, PENDING)
        self.assertTrue(body.startswith(ANALYSIS.rstrip()))

    def test_the_footer_is_appended(self):
        body = hand_back.build_comment(ANALYSIS, PENDING)
        self.assertIn("/approve", body)

    def test_the_result_still_reads_as_an_analysis(self):
        """The footer must not break the recognizer that repair depends on."""
        import triage_repair  # noqa: E402

        for analysis, state in ((ANALYSIS, PENDING), (QUESTION, CLARIFY)):
            body = hand_back.build_comment(analysis, state)
            self.assertTrue(triage_repair.has_analysis_signature(body))

    def test_an_analysis_with_no_signature_is_refused(self):
        """A hand-back with no plan is the silent bad state — refuse to write it."""
        with self.assertRaises(ValueError):
            hand_back.build_comment("Looks tricky, will think about it.", PENDING)


class ApplyTests(unittest.TestCase):
    """`apply` — comment first, then the label. Always that order."""

    def test_it_posts_the_comment_before_setting_labels(self):
        api = FakeApi()
        hand_back.apply(api, 47, ANALYSIS, ["ai-triage", "area:ai"], PENDING)
        self.assertEqual(api.methods, ["POST", "PUT"])

    def test_the_comment_carries_the_footer(self):
        api = FakeApi()
        hand_back.apply(api, 47, ANALYSIS, ["ai-triage"], PENDING)
        _, path, body = api.calls[0]
        self.assertIn("/comments", path)
        self.assertIn("/approve", body["body"])

    def test_the_label_write_sends_the_planned_set(self):
        api = FakeApi()
        hand_back.apply(api, 47, ANALYSIS, ["ai-triage", "area:ai"], PENDING)
        _, path, body = api.calls[1]
        self.assertIn("/labels", path)
        self.assertEqual(sorted(body["labels"]), ["area:ai", PENDING])

    def test_a_failed_label_write_still_leaves_the_comment_posted(self):
        """The recoverable half: a plan on `ai-triage` is what the next run redoes."""
        api = FakeApi(fail_on="/labels")
        with self.assertRaises(RuntimeError):
            hand_back.apply(api, 47, ANALYSIS, ["ai-triage"], PENDING)
        self.assertEqual(api.methods, ["POST", "PUT"])

    def test_a_refused_state_writes_nothing_at_all(self):
        api = FakeApi()
        with self.assertRaises(ValueError):
            hand_back.apply(api, 47, ANALYSIS, ["ai-triage"], "ready-for-work")
        self.assertEqual(api.calls, [])

    def test_a_refused_analysis_writes_nothing_at_all(self):
        api = FakeApi()
        with self.assertRaises(ValueError):
            hand_back.apply(api, 47, "no plan here", ["ai-triage"], PENDING)
        self.assertEqual(api.calls, [])


if __name__ == "__main__":
    unittest.main()
