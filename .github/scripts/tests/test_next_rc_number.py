"""Unit tests for deriving the next release-candidate number."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import next_rc_number as rc  # noqa: E402

PR_OPENED = "2026-08-08T10:00:00Z"


def run(created_at, run_id=1, status="completed"):
    return {"id": run_id, "created_at": created_at, "status": status}


class CountingTests(unittest.TestCase):
    def test_the_first_run_after_the_pr_opens_is_rc1(self):
        snapshot = {"pr_created_at": PR_OPENED, "runs": [run("2026-08-08T10:05:00Z", 100)],
                    "this_run_id": 100}

        self.assertEqual(1, rc.next_rc_number(snapshot))

    def test_the_third_run_is_rc3(self):
        snapshot = {
            "pr_created_at": PR_OPENED,
            "runs": [
                run("2026-08-08T10:05:00Z", 100),
                run("2026-08-08T11:00:00Z", 101),
                run("2026-08-08T12:00:00Z", 102),
            ],
            "this_run_id": 102,
        }

        self.assertEqual(3, rc.next_rc_number(snapshot))

    def test_runs_from_before_the_pr_opened_do_not_count(self):
        # A fresh release PR restarts at rc1, so runs belonging to the previous
        # release must not carry over.
        snapshot = {
            "pr_created_at": PR_OPENED,
            "runs": [
                run("2026-08-01T09:00:00Z", 1),
                run("2026-08-02T09:00:00Z", 2),
                run("2026-08-08T10:05:00Z", 100),
            ],
            "this_run_id": 100,
        }

        self.assertEqual(1, rc.next_rc_number(snapshot))

    def test_a_run_at_exactly_the_pr_open_time_counts(self):
        snapshot = {"pr_created_at": PR_OPENED, "runs": [run(PR_OPENED, 100)], "this_run_id": 100}

        self.assertEqual(1, rc.next_rc_number(snapshot))

    def test_runs_newer_than_this_one_are_ignored(self):
        # A re-run of an older build must not renumber itself above a newer one.
        snapshot = {
            "pr_created_at": PR_OPENED,
            "runs": [
                run("2026-08-08T10:05:00Z", 100),
                run("2026-08-08T11:00:00Z", 101),
                run("2026-08-08T12:00:00Z", 102),
            ],
            "this_run_id": 101,
        }

        self.assertEqual(2, rc.next_rc_number(snapshot))

    def test_out_of_order_input_is_sorted(self):
        snapshot = {
            "pr_created_at": PR_OPENED,
            "runs": [
                run("2026-08-08T12:00:00Z", 102),
                run("2026-08-08T10:05:00Z", 100),
                run("2026-08-08T11:00:00Z", 101),
            ],
            "this_run_id": 102,
        }

        self.assertEqual(3, rc.next_rc_number(snapshot))

    def test_this_run_missing_from_the_list_still_counts_itself(self):
        # The API can lag: a run querying for itself may not see itself yet.
        snapshot = {
            "pr_created_at": PR_OPENED,
            "runs": [run("2026-08-08T10:05:00Z", 100)],
            "this_run_id": 999,
        }

        self.assertEqual(2, rc.next_rc_number(snapshot))

    def test_offset_timestamps_are_understood(self):
        snapshot = {
            "pr_created_at": "2026-08-08T10:00:00+00:00",
            "runs": [run("2026-08-08T10:05:00Z", 100)],
            "this_run_id": 100,
        }

        self.assertEqual(1, rc.next_rc_number(snapshot))


class BadInputTests(unittest.TestCase):
    def test_a_missing_pr_time_is_an_error_not_an_assumption(self):
        with self.assertRaises(ValueError):
            rc.next_rc_number({"runs": [], "this_run_id": 1})

    def test_an_unparseable_timestamp_is_an_error(self):
        with self.assertRaises(ValueError):
            rc.next_rc_number(
                {"pr_created_at": "yesterday", "runs": [], "this_run_id": 1})

    def test_no_runs_at_all_still_yields_rc1(self):
        snapshot = {"pr_created_at": PR_OPENED, "runs": [], "this_run_id": 1}

        self.assertEqual(1, rc.next_rc_number(snapshot))


class CommandLineTests(unittest.TestCase):
    def test_main_prints_the_number(self):
        import io
        import json
        import unittest.mock

        snapshot = json.dumps(
            {"pr_created_at": PR_OPENED, "runs": [run("2026-08-08T10:05:00Z", 100)],
             "this_run_id": 100})

        out = io.StringIO()
        with unittest.mock.patch("sys.stdin", io.StringIO(snapshot)), \
                unittest.mock.patch("sys.stdout", out):
            self.assertEqual(0, rc.main(["--snapshot", "-"]))

        self.assertEqual("1", out.getvalue().strip())


if __name__ == "__main__":
    unittest.main()
