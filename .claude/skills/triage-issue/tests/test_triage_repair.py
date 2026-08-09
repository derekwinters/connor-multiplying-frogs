"""Tests for re-fire repair detection."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import triage_repair as repair  # noqa: E402

BOT = "github-actions[bot]"

CHECKLIST_BODY = """\
Frogs keep multiplying past the limit when you tap them quickly.

## Build checklist

- [ ] Check the cap at the moment the frog is added
"""

QUESTION_BODY = """\
This one needs a decision before it can be planned.

❓ **Needs from Derek/Connor:** should a tap on a full pond do nothing, or
should the frog wiggle?
"""


def comment(body, author=BOT, created_at="2026-08-08T10:00:00Z"):
    return {"body": body, "author": author, "created_at": created_at}


class SignatureTests(unittest.TestCase):
    """`has_analysis_signature` — what counts as a triage analysis comment."""

    def test_a_build_checklist_heading_is_a_signature(self):
        self.assertTrue(repair.has_analysis_signature(CHECKLIST_BODY))

    def test_the_needs_from_marker_is_a_signature(self):
        self.assertTrue(repair.has_analysis_signature(QUESTION_BODY))

    def test_the_marker_matches_without_emphasis(self):
        body = "❓ Needs from Derek/Connor: which colour should the frog be?"
        self.assertTrue(repair.has_analysis_signature(body))

    def test_the_marker_matches_with_underscore_emphasis(self):
        body = "❓ __Needs from Derek/Connor:__ which colour?"
        self.assertTrue(repair.has_analysis_signature(body))

    def test_the_marker_matches_with_emphasis_inside_the_colon(self):
        body = "❓ *Needs from Derek/Connor*: which colour?"
        self.assertTrue(repair.has_analysis_signature(body))

    def test_a_prose_mention_of_the_checklist_is_not_a_signature(self):
        body = "I will add a ## Build checklist heading once the design is agreed."
        self.assertFalse(repair.has_analysis_signature(body))

    def test_a_prose_mention_of_the_question_marker_is_not_a_signature(self):
        body = "The plan says we still need from Derek/Connor: a colour decision."
        self.assertFalse(repair.has_analysis_signature(body))

    def test_an_ordinary_comment_is_not_a_signature(self):
        self.assertFalse(repair.has_analysis_signature("Looks good to me."))

    def test_an_empty_or_missing_body_is_not_a_signature(self):
        self.assertFalse(repair.has_analysis_signature(""))
        self.assertFalse(repair.has_analysis_signature(None))

    def test_the_checklist_heading_is_recognized_at_any_depth(self):
        self.assertTrue(repair.has_analysis_signature("### Build checklist\n\n- [ ] x"))

    def test_a_checklist_heading_must_start_its_line(self):
        self.assertFalse(repair.has_analysis_signature("see the ## Build checklist below"))


class AnalysisCommentTimesTests(unittest.TestCase):
    """`analysis_comment_times` — when did triage last analyze this issue?"""

    def test_no_comments_is_no_times(self):
        self.assertEqual(repair.analysis_comment_times([]), [])

    def test_an_analysis_comment_contributes_its_timestamp(self):
        comments = [comment(CHECKLIST_BODY, created_at="2026-08-01T09:00:00Z")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-01T09:00:00Z"])

    def test_ordinary_comments_are_ignored(self):
        comments = [comment("thanks!"), comment(CHECKLIST_BODY, created_at="2026-08-02T09:00:00Z")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-02T09:00:00Z"])

    def test_the_owners_own_login_counts(self):
        """Triage runs under Derek's account too, so his login is a triage author.

        The cost is deliberate and accepted: a checklist he writes by hand now
        reads as a triage analysis. See the note on `TRIAGE_AUTHORS`.
        """
        comments = [comment(CHECKLIST_BODY, author="derekwinters")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-08T10:00:00Z"])

    def test_any_other_human_is_not_triage(self):
        comments = [comment(CHECKLIST_BODY, author="a-passing-contributor")]
        self.assertEqual(repair.analysis_comment_times(comments), [])

    def test_the_claude_app_is_a_triage_author(self):
        """The App posts triage's comments — this is the login that actually writes them."""
        comments = [comment(CHECKLIST_BODY, author="claude[bot]")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-08T10:00:00Z"])

    def test_the_actions_bot_is_still_a_triage_author(self):
        """A workflow-run triage posts under the Actions token, and older comments exist."""
        comments = [comment(CHECKLIST_BODY, author="github-actions[bot]")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-08T10:00:00Z"])

    def test_the_author_match_ignores_case(self):
        comments = [comment(CHECKLIST_BODY, author="Claude[Bot]")]
        self.assertEqual(repair.analysis_comment_times(comments), ["2026-08-08T10:00:00Z"])


class TriageAuthorTests(unittest.TestCase):
    """`is_triage_author` — the one definition of "triage wrote this"."""

    def test_the_claude_app_is_recognized(self):
        self.assertTrue(repair.is_triage_author("claude[bot]"))

    def test_the_actions_bot_is_recognized(self):
        self.assertTrue(repair.is_triage_author("github-actions[bot]"))

    def test_the_owner_is_recognized(self):
        """Triage sessions run under Derek's own account, not only as a bot."""
        self.assertTrue(repair.is_triage_author("derekwinters"))

    def test_any_other_human_is_not(self):
        self.assertFalse(repair.is_triage_author("a-passing-contributor"))

    def test_the_accepted_set_is_exactly_three(self):
        """Widening this set is a decision, not a convenience — pin the size."""
        self.assertEqual(len(repair.TRIAGE_AUTHORS), 3)

    def test_another_app_is_not(self):
        self.assertFalse(repair.is_triage_author("dependabot[bot]"))

    def test_an_empty_or_missing_login_is_not(self):
        self.assertFalse(repair.is_triage_author(""))
        self.assertFalse(repair.is_triage_author(None))

    def test_times_come_back_oldest_first(self):
        comments = [
            comment(QUESTION_BODY, created_at="2026-08-05T09:00:00Z"),
            comment(CHECKLIST_BODY, created_at="2026-08-01T09:00:00Z"),
        ]
        self.assertEqual(
            repair.analysis_comment_times(comments),
            ["2026-08-01T09:00:00Z", "2026-08-05T09:00:00Z"],
        )


class RepairTests(unittest.TestCase):
    """`plan_repair` — what a re-fire should actually do."""

    def test_a_prior_analysis_with_no_state_label_repairs_the_label_only(self):
        plan = repair.plan_repair(
            labels=["ai-triage", "type:bug"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="pending-approval",
        )

        self.assertTrue(plan.repair_only)
        self.assertEqual(plan.add_labels, ["pending-approval"])
        self.assertEqual(plan.remove_labels, ["ai-triage"])
        self.assertFalse(plan.reanalyze)

    def test_the_repair_posts_no_comment(self):
        plan = repair.plan_repair(
            labels=["ai-triage"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="pending-approval",
        )
        self.assertEqual(plan.comment, "")

    def test_no_prior_analysis_means_a_full_triage(self):
        plan = repair.plan_repair(
            labels=["ai-triage"],
            comments=[comment("just a normal comment")],
            intended_state="pending-approval",
        )

        self.assertFalse(plan.repair_only)
        self.assertTrue(plan.reanalyze)

    def test_an_already_complete_hand_back_needs_nothing(self):
        plan = repair.plan_repair(
            labels=["pending-approval", "type:bug"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="pending-approval",
        )

        self.assertTrue(plan.repair_only)
        self.assertEqual(plan.add_labels, [])
        self.assertEqual(plan.remove_labels, [])
        self.assertFalse(plan.changes_anything)

    def test_a_revise_forces_reanalysis_despite_a_prior_analysis(self):
        """`/revise` is an objection to the plan — repairing the label ignores it."""
        plan = repair.plan_repair(
            labels=["ai-triage"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="pending-approval",
            note={"command": "revise", "text": "the cap should be 64"},
        )

        self.assertTrue(plan.reanalyze)
        self.assertFalse(plan.repair_only)

    def test_the_state_swap_leaves_other_labels_alone(self):
        plan = repair.plan_repair(
            labels=["ai-triage", "area:gameplay", "type:bug"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="needs-clarification",
        )

        self.assertEqual(plan.remove_labels, ["ai-triage"])
        self.assertEqual(plan.add_labels, ["needs-clarification"])

    def test_a_wrong_state_label_is_replaced_not_stacked(self):
        plan = repair.plan_repair(
            labels=["needs-clarification"],
            comments=[comment(CHECKLIST_BODY)],
            intended_state="pending-approval",
        )

        self.assertEqual(sorted(plan.remove_labels), ["needs-clarification"])
        self.assertEqual(plan.add_labels, ["pending-approval"])


if __name__ == "__main__":
    unittest.main()
