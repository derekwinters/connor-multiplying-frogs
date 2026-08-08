"""Tests for build-queue selection."""

import json
import sys
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import select_queue as queue  # noqa: E402

FOCUS = "v0.0.1"


def issue(number=10, labels=("ready-for-work",), milestone=FOCUS, body="",
          native_blockers=(), state="open"):
    return {
        "number": number,
        "state": state,
        "labels": list(labels),
        "milestone": milestone,
        "body": body,
        "native_blockers": list(native_blockers),
    }


def pull(number=100, body="Closes #10", state="open"):
    return {"number": number, "body": body, "state": state}


def selected(issues, pulls=(), focus=FOCUS, cap=None, snapshot=None):
    data = {
        "issues": list(issues),
        "pulls": list(pulls),
        "focus": focus,
        "snapshot": snapshot or {},
    }
    if cap is not None:
        data["cap"] = cap
    return [entry["number"] for entry in queue.process(data)["queue"]]


class EligibilityTests(unittest.TestCase):
    def test_a_ready_issue_in_focus_is_selected(self):
        self.assertEqual(selected([issue(10)]), [10])

    def test_an_issue_without_ready_for_work_is_not(self):
        self.assertEqual(selected([issue(10, labels=("pending-approval",))]), [])

    def test_an_issue_in_another_milestone_is_not(self):
        self.assertEqual(selected([issue(10, milestone="v0.0.2")]), [])

    def test_an_issue_with_no_milestone_is_not(self):
        self.assertEqual(selected([issue(10, milestone=None)]), [])

    def test_a_parked_issue_is_not(self):
        self.assertEqual(selected([issue(10, labels=("ready-for-work", "parked"))]), [])

    def test_a_closed_issue_is_not(self):
        self.assertEqual(selected([issue(10, state="closed")]), [])

    def test_an_epic_is_not(self):
        """An epic is a container; its children are the work."""
        self.assertEqual(selected([issue(10, labels=("ready-for-work", "type:epic"))]), [])


class BlockerTests(unittest.TestCase):
    def test_an_open_native_blocker_suppresses_the_issue(self):
        snapshot = {42: {"state": "open", "labels": []}}
        self.assertEqual(
            selected([issue(10, native_blockers=(42,))], snapshot=snapshot), [])

    def test_a_closed_native_blocker_does_not(self):
        snapshot = {42: {"state": "closed", "labels": []}}
        self.assertEqual(
            selected([issue(10, native_blockers=(42,))], snapshot=snapshot), [10])

    def test_a_text_blocker_line_suppresses_the_issue_too(self):
        snapshot = {42: {"state": "open", "labels": []}}
        self.assertEqual(
            selected([issue(10, body="Blocked by #42")], snapshot=snapshot), [])

    def test_native_and_text_blockers_are_unioned(self):
        """Half-cleared is still blocked, whichever half was written where."""
        snapshot = {42: {"state": "closed"}, 43: {"state": "open"}}
        blocked = issue(10, body="Blocked by #43", native_blockers=(42,))
        self.assertEqual(selected([blocked], snapshot=snapshot), [])

    def test_a_depends_on_line_is_not_a_blocker(self):
        snapshot = {42: {"state": "open", "labels": []}}
        self.assertEqual(
            selected([issue(10, body="Depends on: #42")], snapshot=snapshot), [10])

    def test_a_prose_mention_is_not_a_blocker(self):
        body = "This is similar to #42 but simpler."
        self.assertEqual(selected([issue(10, body=body)]), [10])

    def test_an_unknown_blocker_is_not_assumed_resolved(self):
        """Not knowing is not the same as knowing it is done."""
        self.assertEqual(selected([issue(10, native_blockers=(42,))], snapshot={}), [])

    def test_one_merge_helper_backs_both_sources(self):
        merged = queue.blockers_of(issue(10, body="Blocked by #43", native_blockers=(42,)))
        self.assertEqual(merged, [42, 43])


class OpenPullRequestTests(unittest.TestCase):
    def test_an_open_pr_closing_the_issue_suppresses_it(self):
        self.assertEqual(selected([issue(10)], pulls=[pull(body="Closes #10")]), [])

    def test_a_pr_that_merely_mentions_the_issue_does_not(self):
        self.assertEqual(
            selected([issue(10)], pulls=[pull(body="Related to #10")]), [10])

    def test_a_bare_reference_does_not_suppress(self):
        self.assertEqual(selected([issue(10)], pulls=[pull(body="See #10")]), [10])

    def test_a_closed_pr_does_not_suppress(self):
        self.assertEqual(
            selected([issue(10)], pulls=[pull(body="Closes #10", state="closed")]), [10])

    def test_every_closing_keyword_counts(self):
        for keyword in ("Closes", "Fixes", "Resolves", "closed", "fixed"):
            with self.subTest(keyword=keyword):
                self.assertEqual(
                    selected([issue(10)], pulls=[pull(body=f"{keyword} #10")]), [])


class OrderingTests(unittest.TestCase):
    def test_issues_come_back_in_number_order_by_default(self):
        self.assertEqual(selected([issue(12), issue(10), issue(11)]), [10, 11, 12])

    def test_a_soft_dependency_orders_before_its_dependent(self):
        """`Depends on:` does not block, but it does order."""
        issues = [issue(10, body="Depends on: #11"), issue(11)]
        self.assertEqual(selected(issues), [11, 10])

    def test_ordering_survives_a_chain(self):
        issues = [
            issue(10, body="Depends on: #11"),
            issue(11, body="Depends on: #12"),
            issue(12),
        ]
        self.assertEqual(selected(issues), [12, 11, 10])

    def test_a_dependency_outside_the_queue_is_ignored_for_ordering(self):
        self.assertEqual(selected([issue(10, body="Depends on: #99")]), [10])

    def test_a_cycle_does_not_hang_or_drop_issues(self):
        issues = [issue(10, body="Depends on: #11"), issue(11, body="Depends on: #10")]
        self.assertEqual(sorted(selected(issues)), [10, 11])


class CapTests(unittest.TestCase):
    def test_the_cap_defaults_to_three(self):
        self.assertEqual(selected([issue(n) for n in (10, 11, 12, 13, 14)]),
                         [10, 11, 12])

    def test_an_explicit_cap_is_honored(self):
        self.assertEqual(selected([issue(n) for n in (10, 11, 12, 13)], cap=2),
                         [10, 11])

    def test_a_cap_of_zero_selects_nothing(self):
        self.assertEqual(selected([issue(10)], cap=0), [])

    def test_the_cap_applies_after_ordering_not_before(self):
        issues = [issue(10, body="Depends on: #11"), issue(11), issue(12)]
        self.assertEqual(selected(issues, cap=2), [11, 10])


class ShapeTests(unittest.TestCase):
    def test_process_is_pure_and_reports_a_count(self):
        data = {"issues": [issue(10)], "pulls": [], "focus": FOCUS, "snapshot": {}}
        before = json.dumps(data, sort_keys=True)
        result = queue.process(data)

        self.assertEqual(result["count"], 1)
        self.assertEqual(json.dumps(data, sort_keys=True), before)

    def test_no_issues_is_an_empty_queue_not_an_error(self):
        self.assertEqual(queue.process({}), {"queue": [], "count": 0})

    def test_main_reads_stdin_and_writes_json(self):
        data = {"issues": [issue(10)], "pulls": [], "focus": FOCUS, "snapshot": {}}
        out = StringIO()

        with patch.object(sys, "stdin", StringIO(json.dumps(data))), \
                patch.object(sys, "stdout", out):
            self.assertEqual(queue.main([]), 0)

        self.assertEqual(json.loads(out.getvalue())["count"], 1)

    def test_the_queue_carries_the_milestone_through(self):
        data = {"issues": [issue(10)], "pulls": [], "focus": FOCUS, "snapshot": {}}
        self.assertEqual(queue.process(data)["queue"][0]["milestone"], FOCUS)


if __name__ == "__main__":
    unittest.main()
