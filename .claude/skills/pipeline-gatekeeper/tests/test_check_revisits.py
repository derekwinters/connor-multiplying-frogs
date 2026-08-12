"""Tests for blocker auto-revisit.

A state-derived transition, not a command: nothing else would ever wake an
issue that was set aside only because it was blocked.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import check_revisits as revisits  # noqa: E402


def issue(number=10, labels=("needs-clarification",), body="", native=(), blockers_state=None,
          comments=()):
    return {
        "number": number,
        "labels": list(labels),
        "body": body,
        "native_blockers": list(native),
        "comments": list(comments),
    }


def blocker(number=20, state="open", labels=(), merged=False):
    return {"number": number, "state": state, "labels": list(labels), "merged": merged}


def revisit_comment(cleared, author="github-actions[bot]"):
    """A comment shaped like `Revisit.comment`, for testing recognition of it."""
    named = ", ".join(f"#{n}" for n in cleared)
    return {
        "author": author,
        "body": (f"Everything this was waiting on has cleared ({named}), so it is back in "
                 f"the triage queue.\n\n"
                 f"_Automatic — nothing was decided here beyond that the blocker is done._"),
    }


class NoBlockerTests(unittest.TestCase):
    def test_an_issue_with_no_blockers_is_left_alone(self):
        self.assertEqual([], revisits.find_revisits([issue()], {}))

    def test_a_prose_only_mention_is_not_a_blocker(self):
        # "similar to #42" is not a dependency.
        subject = issue(body="This is similar to #42 but simpler.")

        self.assertEqual([], revisits.find_revisits([subject], {42: blocker(42, "closed")}))


class ResolutionTests(unittest.TestCase):
    def revisit(self, blocker_state):
        subject = issue(body="Blocked by #20")
        return revisits.find_revisits([subject], {20: blocker_state})

    def test_a_closed_blocker_resolves(self):
        self.assertEqual(1, len(self.revisit(blocker(20, "closed"))))

    def test_a_merged_blocker_resolves(self):
        self.assertEqual(1, len(self.revisit(blocker(20, "closed", merged=True))))

    def test_a_blocker_that_is_ready_for_work_resolves(self):
        # It is scheduled and will be built; waiting for it to close would hold
        # the dependent back for no benefit.
        self.assertEqual(1, len(self.revisit(blocker(20, "open", ["ready-for-work"]))))

    def test_a_blocker_that_is_in_progress_resolves(self):
        self.assertEqual(1, len(self.revisit(blocker(20, "open", ["in-progress"]))))

    def test_a_still_open_untouched_blocker_does_not(self):
        self.assertEqual([], self.revisit(blocker(20, "open", ["pending-approval"])))

    def test_an_unknown_blocker_does_not_resolve(self):
        # Not in the snapshot means we cannot tell, and cannot-tell is not
        # resolved.
        subject = issue(body="Blocked by #99")

        self.assertEqual([], revisits.find_revisits([subject], {}))


class WireframeCarveOutTests(unittest.TestCase):
    def test_a_wireframe_blocker_resolves_only_when_closed(self):
        subject = issue(body="Blocked by #20")
        ready = blocker(20, "open", ["type:wireframe", "ready-for-work"])

        self.assertEqual([], revisits.find_revisits([subject], {20: ready}))

    def test_a_closed_wireframe_blocker_does_resolve(self):
        subject = issue(body="Blocked by #20")
        closed = blocker(20, "closed", ["type:wireframe"])

        self.assertEqual(1, len(revisits.find_revisits([subject], {20: closed})))

    def test_the_carve_out_stops_the_sweep_re_firing_forever(self):
        # A wireframe marked ready-for-work is still not agreed. Without this,
        # the sweep would wake the dependent every run, and triage would set it
        # aside again every run.
        subject = issue(body="Blocked by #20")
        in_progress = blocker(20, "open", ["type:wireframe", "in-progress"])

        self.assertEqual([], revisits.find_revisits([subject], {20: in_progress}))


class MultipleBlockerTests(unittest.TestCase):
    def test_all_must_resolve(self):
        subject = issue(body="Blocked by #20\nBlocked by #21")
        snapshot = {20: blocker(20, "closed"), 21: blocker(21, "open", ["pending-approval"])}

        self.assertEqual([], revisits.find_revisits([subject], snapshot))

    def test_revisiting_once_every_one_resolves(self):
        subject = issue(body="Blocked by #20\nBlocked by #21")
        snapshot = {20: blocker(20, "closed"), 21: blocker(21, "closed")}

        found = revisits.find_revisits([subject], snapshot)

        self.assertEqual(1, len(found))
        self.assertEqual([20, 21], sorted(found[0].cleared))


class SourceUnionTests(unittest.TestCase):
    def test_native_relationships_count(self):
        subject = issue(native=[20])

        self.assertEqual(1, len(revisits.find_revisits([subject], {20: blocker(20, "closed")})))

    def test_text_and_native_are_unioned_not_either_or(self):
        subject = issue(body="Blocked by #20", native=[21])
        snapshot = {20: blocker(20, "closed"), 21: blocker(21, "open", ["pending-approval"])}

        self.assertEqual([], revisits.find_revisits([subject], snapshot))

    def test_the_same_blocker_from_both_sources_counts_once(self):
        subject = issue(body="Blocked by #20", native=[20])

        found = revisits.find_revisits([subject], {20: blocker(20, "closed")})

        self.assertEqual([20], found[0].cleared)


class EligibilityTests(unittest.TestCase):
    def test_a_parked_issue_is_left_alone(self):
        # Parking is a decision the owner made. Only the owner un-makes it.
        subject = issue(labels=["parked"], body="Blocked by #20")

        self.assertEqual([], revisits.find_revisits([subject], {20: blocker(20, "closed")}))

    def test_a_pending_approval_issue_is_left_alone(self):
        subject = issue(labels=["pending-approval"], body="Blocked by #20")

        self.assertEqual([], revisits.find_revisits([subject], {20: blocker(20, "closed")}))

    def test_a_ready_issue_is_left_alone(self):
        subject = issue(labels=["ready-for-work"], body="Blocked by #20")

        self.assertEqual([], revisits.find_revisits([subject], {20: blocker(20, "closed")}))


class RevisitShapeTests(unittest.TestCase):
    def test_it_adds_ai_triage_and_removes_needs_clarification(self):
        subject = issue(body="Blocked by #20")

        found = revisits.find_revisits([subject], {20: blocker(20, "closed")})[0]

        self.assertEqual(["ai-triage"], found.add_labels)
        self.assertEqual(["needs-clarification"], found.remove_labels)

    def test_the_comment_names_the_cleared_blockers(self):
        subject = issue(body="Blocked by #20")

        found = revisits.find_revisits([subject], {20: blocker(20, "closed")})[0]

        self.assertIn("#20", found.comment)

    def test_the_comment_is_short(self):
        subject = issue(body="Blocked by #20")

        found = revisits.find_revisits([subject], {20: blocker(20, "closed")})[0]

        self.assertLessEqual(len(found.comment.splitlines()), 4)


class AlreadyRevisitedTests(unittest.TestCase):
    # #296: the wake-up fired once for #20, triage answered `needs-clarification`
    # again for an unrelated open design question, and every sweep afterward saw
    # the same resolved #20 and fired the wake-up again — forever, since a
    # closed blocker never stops being "resolved". This is what stops it: a
    # blocker set this issue was already woken for does not fire twice.

    def test_does_not_refire_for_a_blocker_set_already_actioned(self):
        subject = issue(body="Blocked by #20", comments=[revisit_comment([20])])

        self.assertEqual([], revisits.find_revisits([subject], {20: blocker(20, "closed")}))

    def test_a_hand_typed_comment_from_a_non_triage_author_does_not_count(self):
        # Only the bot's own action suppresses the wake-up. A stranger's comment
        # using similar words must not silently swallow a real one.
        comment = revisit_comment([20], author="someone-else")
        subject = issue(body="Blocked by #20", comments=[comment])

        self.assertEqual(1, len(revisits.find_revisits([subject], {20: blocker(20, "closed")})))

    def test_a_new_blocker_resolving_still_fires(self):
        # Only #20 was already actioned. #21 clearing later is a genuine new
        # wake-up, not a repeat of the old one.
        subject = issue(body="Blocked by #20\nBlocked by #21",
                         comments=[revisit_comment([20])])
        snapshot = {20: blocker(20, "closed"), 21: blocker(21, "closed")}

        self.assertEqual(1, len(revisits.find_revisits([subject], snapshot)))


if __name__ == "__main__":
    unittest.main()
