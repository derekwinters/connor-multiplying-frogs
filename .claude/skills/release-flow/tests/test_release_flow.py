"""Unit tests for the release-flow checks.

    python3 -m unittest discover -s .claude/skills -p 'test_*.py'
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import release_flow  # noqa: E402


class FindReleasePrTests(unittest.TestCase):
    def test_matches_on_the_release_branch_not_the_title(self):
        pulls = [
            {"number": 1, "title": "chore(main): release 0.1.0", "head": {"ref": "someones-branch"}},
            {"number": 2, "title": "anything at all", "head": {"ref": "release-please--branches--main"}},
        ]

        self.assertEqual(2, release_flow.find_release_pr(pulls)["number"])

    def test_returns_none_when_there_is_no_release_pr(self):
        self.assertIsNone(release_flow.find_release_pr([{"head": {"ref": "main"}}]))

    def test_tolerates_a_pull_request_with_no_head(self):
        self.assertIsNone(release_flow.find_release_pr([{"number": 1}]))


class RegeneratedTests(unittest.TestCase):
    def snapshot(self, base_sha, main_sha):
        return {"main_sha": main_sha, "pull_request": {"base": {"sha": base_sha}}}

    def test_passes_when_the_pr_is_based_on_the_current_main(self):
        verdict = release_flow.check_regenerated(self.snapshot("abc1234", "abc1234"))

        self.assertTrue(verdict.ok)

    def test_fails_when_main_has_moved_on(self):
        verdict = release_flow.check_regenerated(self.snapshot("abc1234", "def5678"))

        self.assertFalse(verdict.ok)
        self.assertIn("abc1234", verdict.reason)
        self.assertIn("def5678", verdict.reason)

    def test_fails_when_there_is_no_release_pr(self):
        verdict = release_flow.check_regenerated({"main_sha": "abc1234"})

        self.assertFalse(verdict.ok)
        self.assertIn("No open release PR", verdict.reason)

    def test_fails_rather_than_passing_when_the_snapshot_is_incomplete(self):
        # An absent main_sha must not read as "nothing has changed".
        verdict = release_flow.check_regenerated({"pull_request": {"base": {"sha": "abc1234"}}})

        self.assertFalse(verdict.ok)


class ParkedRunTests(unittest.TestCase):
    def test_passes_when_every_run_has_finished(self):
        snapshot = {
            "check_runs": [
                {"name": "ci-tests", "status": "completed", "conclusion": "success"},
                {"name": "docs-test", "status": "completed", "conclusion": "success"},
            ]
        }

        self.assertTrue(release_flow.check_parked_runs(snapshot).ok)

    def test_passes_when_there_are_no_runs_at_all(self):
        self.assertTrue(release_flow.check_parked_runs({}).ok)

    def test_fails_on_a_run_awaiting_approval(self):
        snapshot = {"check_runs": [{"name": "pr-build", "status": "action_required"}]}

        verdict = release_flow.check_parked_runs(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("pr-build", verdict.reason)

    def test_fails_when_the_parked_state_is_in_the_conclusion(self):
        snapshot = {
            "check_runs": [{"name": "pr-build", "status": "completed", "conclusion": "action_required"}]
        }

        self.assertFalse(release_flow.check_parked_runs(snapshot).ok)

    def test_names_every_parked_run_so_none_is_a_surprise(self):
        snapshot = {
            "check_runs": [
                {"name": "pr-build", "status": "waiting"},
                {"name": "ci-tests", "status": "action_required"},
                {"name": "docs-test", "status": "completed", "conclusion": "success"},
            ]
        }

        verdict = release_flow.check_parked_runs(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("ci-tests", verdict.reason)
        self.assertIn("pr-build", verdict.reason)
        self.assertNotIn("docs-test", verdict.reason)

    def test_a_failing_run_is_not_a_parked_run(self):
        # Failures are CI's problem to report; this check is only about runs
        # that will never finish on their own.
        snapshot = {"check_runs": [{"name": "ci-tests", "status": "completed", "conclusion": "failure"}]}

        self.assertTrue(release_flow.check_parked_runs(snapshot).ok)


class SquashTitleTests(unittest.TestCase):
    def test_uses_the_conventional_release_subject(self):
        self.assertEqual("chore(main): release 0.1.0", release_flow.squash_title("0.1.0"))


class ReleasedTests(unittest.TestCase):
    def complete(self):
        return {
            "version": "0.1.0",
            "tags": ["v0.0.1", "v0.1.0"],
            "releases": [{"tag_name": "v0.1.0", "draft": False}],
            "pull_request_labels": ["autorelease: tagged"],
        }

    def test_passes_when_tag_release_and_label_all_exist(self):
        verdict = release_flow.check_released(self.complete())

        self.assertTrue(verdict.ok, verdict.reason)

    def test_fails_when_the_tag_is_missing(self):
        snapshot = self.complete()
        snapshot["tags"] = ["v0.0.1"]

        verdict = release_flow.check_released(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("v0.1.0 tag", verdict.reason)

    def test_fails_when_the_release_is_missing(self):
        snapshot = self.complete()
        snapshot["releases"] = []

        verdict = release_flow.check_released(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("published GitHub Release", verdict.reason)

    def test_a_draft_release_does_not_count_and_says_so(self):
        snapshot = self.complete()
        snapshot["releases"] = [{"tag_name": "v0.1.0", "draft": True}]

        verdict = release_flow.check_released(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("found a draft", verdict.reason)

    def test_fails_when_the_tagged_label_is_missing(self):
        snapshot = self.complete()
        snapshot["pull_request_labels"] = ["autorelease: pending"]

        verdict = release_flow.check_released(snapshot)

        self.assertFalse(verdict.ok)
        self.assertIn("autorelease: tagged", verdict.reason)

    def test_reports_every_missing_piece_at_once(self):
        verdict = release_flow.check_released({"version": "0.1.0"})

        self.assertFalse(verdict.ok)
        self.assertIn("v0.1.0 tag", verdict.reason)
        self.assertIn("published GitHub Release", verdict.reason)
        self.assertIn("autorelease: tagged", verdict.reason)


class CommandLineTests(unittest.TestCase):
    def test_title_subcommand_exits_zero(self):
        self.assertEqual(0, release_flow.main(["title", "--version", "0.2.0"]))

    def test_a_failing_check_exits_one(self):
        import io
        import json
        import unittest.mock

        snapshot = json.dumps({"main_sha": "aaaaaaa", "pull_request": {"base": {"sha": "bbbbbbb"}}})

        with unittest.mock.patch("sys.stdin", io.StringIO(snapshot)):
            self.assertEqual(1, release_flow.main(["regenerated", "--snapshot", "-"]))


if __name__ == "__main__":
    unittest.main()
