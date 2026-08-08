"""Unit tests for the ci-watch classifier and excerpt extractor."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import ci_watch  # noqa: E402


def run(name, status="completed", conclusion="success"):
    return {"name": name, "status": status, "conclusion": conclusion}


class ClassifyTests(unittest.TestCase):
    def test_all_successful_is_passed(self):
        result = ci_watch.classify([run("lint"), run("Docs")])

        self.assertEqual(ci_watch.PASSED, result.state)

    def test_one_failure_is_failed(self):
        result = ci_watch.classify([run("lint"), run("Docs", conclusion="failure")])

        self.assertEqual(ci_watch.FAILED, result.state)
        self.assertEqual(["Docs"], [c["name"] for c in result.failed])

    def test_an_unfinished_run_is_pending(self):
        result = ci_watch.classify([run("lint"), run("Docs", status="in_progress", conclusion=None)])

        self.assertEqual(ci_watch.PENDING, result.state)

    def test_a_queued_run_is_pending(self):
        result = ci_watch.classify([run("lint", status="queued", conclusion=None)])

        self.assertEqual(ci_watch.PENDING, result.state)

    def test_skipped_and_neutral_do_not_fail_the_verdict(self):
        result = ci_watch.classify(
            [run("a", conclusion="skipped"), run("b", conclusion="neutral"), run("c")])

        self.assertEqual(ci_watch.PASSED, result.state)

    def test_cancelled_is_a_failure(self):
        # A cancelled check did not pass, and treating it as one is how a
        # cancelled run becomes a green merge.
        result = ci_watch.classify([run("a", conclusion="cancelled")])

        self.assertEqual(ci_watch.FAILED, result.state)

    def test_a_timed_out_run_is_a_failure(self):
        self.assertEqual(ci_watch.FAILED, ci_watch.classify([run("a", conclusion="timed_out")]).state)

    def test_no_checks_at_all_is_pending_not_passed(self):
        # An empty list means the checks have not registered yet. Reporting it
        # as a pass is the worst possible answer to "is CI green".
        self.assertEqual(ci_watch.PENDING, ci_watch.classify([]).state)


class ParkedRunTests(unittest.TestCase):
    def test_a_run_awaiting_approval_is_reported_as_parked(self):
        result = ci_watch.classify([run("pr-build", status="waiting", conclusion=None)])

        self.assertEqual(ci_watch.PARKED, result.state)
        self.assertEqual(["pr-build"], [c["name"] for c in result.parked])

    def test_action_required_counts_as_parked(self):
        result = ci_watch.classify([run("pr-build", conclusion="action_required")])

        self.assertEqual(ci_watch.PARKED, result.state)

    def test_a_failure_outranks_a_parked_run(self):
        # If something has already failed, that is the news; the parked run is
        # a detail on top of it.
        result = ci_watch.classify(
            [run("a", conclusion="failure"), run("b", status="waiting", conclusion=None)])

        self.assertEqual(ci_watch.FAILED, result.state)

    def test_parked_is_terminal_for_polling(self):
        # Nothing will change without a human, so waiting is pointless.
        self.assertTrue(ci_watch.is_terminal(ci_watch.PARKED))
        self.assertTrue(ci_watch.is_terminal(ci_watch.PASSED))
        self.assertTrue(ci_watch.is_terminal(ci_watch.FAILED))
        self.assertFalse(ci_watch.is_terminal(ci_watch.PENDING))


class ExcerptTests(unittest.TestCase):
    def test_error_lines_are_preferred_over_the_tail(self):
        log = "\n".join(["setting up"] * 50 + ["error CS0103: nope"] + ["cleanup"] * 50)

        excerpt = ci_watch.extract_excerpt(log)

        self.assertIn("error CS0103", excerpt)

    def test_the_tail_is_used_when_nothing_matches(self):
        log = "\n".join(f"line {n}" for n in range(200))

        excerpt = ci_watch.extract_excerpt(log)

        self.assertIn("line 199", excerpt)
        self.assertNotIn("line 0", excerpt)

    def test_the_excerpt_is_bounded(self):
        log = "\n".join(["error: something"] * 500)

        excerpt = ci_watch.extract_excerpt(log)

        self.assertLessEqual(len(excerpt.splitlines()), ci_watch.EXCERPT_LINES)

    def test_a_failed_assertion_is_matched(self):
        log = "ok\n  Failed SplitsAtThreshold [4 ms]\n  Expected: 32\n  But was: 16\nok"

        excerpt = ci_watch.extract_excerpt(log)

        self.assertIn("Failed SplitsAtThreshold", excerpt)

    def test_an_empty_log_says_so_rather_than_returning_nothing(self):
        self.assertIn("no log", ci_watch.extract_excerpt("").lower())


class PollingBoundsTests(unittest.TestCase):
    def test_the_interval_and_timeout_are_named_constants(self):
        self.assertIsInstance(ci_watch.POLL_SECONDS, (int, float))
        self.assertIsInstance(ci_watch.TIMEOUT_SECONDS, (int, float))
        self.assertGreater(ci_watch.TIMEOUT_SECONDS, ci_watch.POLL_SECONDS)

    def test_polling_stops_at_a_terminal_state(self):
        responses = [
            [run("a", status="in_progress", conclusion=None)],
            [run("a", status="in_progress", conclusion=None)],
            [run("a")],
        ]
        slept = []

        result = ci_watch.watch(
            fetch=lambda: responses.pop(0), sleep=slept.append, now=_clock())

        self.assertEqual(ci_watch.PASSED, result.state)
        self.assertEqual(2, len(slept))

    def test_polling_gives_up_and_reports_pending(self):
        # A timeout is not a pass. The caller is told the checks never
        # finished, which is a different problem from them failing.
        result = ci_watch.watch(
            fetch=lambda: [run("a", status="in_progress", conclusion=None)],
            sleep=lambda seconds: None,
            now=_clock(step=ci_watch.TIMEOUT_SECONDS))

        self.assertEqual(ci_watch.PENDING, result.state)
        self.assertTrue(result.timed_out)


def _clock(step=1):
    state = {"t": 0}

    def now():
        state["t"] += step
        return state["t"]

    return now


if __name__ == "__main__":
    unittest.main()
