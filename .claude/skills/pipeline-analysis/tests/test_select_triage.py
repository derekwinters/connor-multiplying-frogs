"""Tests for triage discovery."""

import json
import sys
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import select_triage as select  # noqa: E402


def issue(number=10, state="open", labels=("ai-triage",), milestone="v0.0.1", comments=()):
    return {
        "number": number,
        "state": state,
        "labels": list(labels),
        "milestone": milestone,
        "comments": list(comments),
    }


def comment(body, author="derekwinters", created_at="2026-08-08T10:00:00Z"):
    return {"body": body, "author": author, "created_at": created_at}


def eligible_numbers(issues, owner="derekwinters"):
    return [entry["number"] for entry in select.process({"issues": issues, "owner": owner})["eligible"]]


class EligibilityTests(unittest.TestCase):
    def test_an_open_ai_triage_issue_is_eligible(self):
        self.assertEqual([10], eligible_numbers([issue()]))

    def test_a_closed_issue_is_not(self):
        self.assertEqual([], eligible_numbers([issue(state="closed")]))

    def test_an_issue_without_ai_triage_is_not(self):
        self.assertEqual([], eligible_numbers([issue(labels=["pending-approval"])]))

    def test_an_epic_is_not(self):
        # Epics are containers; their children are the work.
        self.assertEqual([], eligible_numbers([issue(labels=["ai-triage", "type:epic"])]))

    def test_the_dashboard_issue_is_not(self):
        self.assertEqual([], eligible_numbers([issue(labels=["ai-triage", "dashboard"])]))

    def test_a_parked_issue_is_not(self):
        # Even carrying ai-triage: parking is the owner's decision.
        self.assertEqual([], eligible_numbers([issue(labels=["ai-triage", "parked"])]))

    def test_several_eligible_issues_come_back_oldest_first(self):
        found = eligible_numbers([issue(number=30), issue(number=10), issue(number=20)])

        self.assertEqual([10, 20, 30], found)


class CarriedContextTests(unittest.TestCase):
    def entry(self, comments):
        return select.process(
            {"issues": [issue(comments=comments)], "owner": "derekwinters"})["eligible"][0]

    def test_the_milestone_is_carried(self):
        self.assertEqual("v0.0.1", self.entry([])["milestone"])

    def test_no_note_when_there_are_no_commands(self):
        self.assertIsNone(self.entry([comment("looks fine")])["note"])

    def test_a_revise_note_is_carried(self):
        entry = self.entry([comment("/revise the scope is too big")])

        self.assertEqual("revise", entry["note"]["command"])
        self.assertEqual("the scope is too big", entry["note"]["text"])

    def test_redo_and_propose_are_carried_too(self):
        self.assertEqual("redo", self.entry([comment("/redo")])["note"]["command"])
        self.assertEqual("propose", self.entry([comment("/propose")])["note"]["command"])

    def test_only_the_latest_note_is_carried(self):
        entry = self.entry([
            comment("/revise first go", created_at="2026-08-01T10:00:00Z"),
            comment("/revise second go", created_at="2026-08-08T10:00:00Z"),
        ])

        self.assertEqual("second go", entry["note"]["text"])

    def test_notes_are_found_regardless_of_comment_order(self):
        entry = self.entry([
            comment("/revise second go", created_at="2026-08-08T10:00:00Z"),
            comment("/revise first go", created_at="2026-08-01T10:00:00Z"),
        ])

        self.assertEqual("second go", entry["note"]["text"])

    def test_a_note_from_anyone_but_the_owner_is_ignored(self):
        # Same gate as the parser: a stranger cannot steer triage either.
        entry = self.entry([comment("/revise do it differently", author="a-stranger")])

        self.assertIsNone(entry["note"])

    def test_an_unrelated_command_is_not_a_note(self):
        self.assertIsNone(self.entry([comment("/approve")])["note"])


class ShapeTests(unittest.TestCase):
    def test_process_is_pure_and_returns_a_count(self):
        result = select.process({"issues": [issue()], "owner": "derekwinters"})

        self.assertEqual(1, result["count"])

    def test_no_issues_at_all_is_an_empty_result_not_an_error(self):
        self.assertEqual({"eligible": [], "count": 0}, select.process({"issues": []}))

    def test_main_reads_stdin_and_writes_json(self):
        payload = json.dumps({"issues": [issue()], "owner": "derekwinters"})
        out = StringIO()

        with patch("sys.stdin", StringIO(payload)), patch("sys.stdout", out):
            self.assertEqual(0, select.main([]))

        self.assertEqual(1, json.loads(out.getvalue())["count"])


if __name__ == "__main__":
    unittest.main()
