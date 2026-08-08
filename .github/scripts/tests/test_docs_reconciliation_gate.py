"""Decision-matrix tests for the docs reconciliation gate."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import docs_reconciliation_gate as gate  # noqa: E402

FEATURE_BRANCH = "claude/some-work"
RELEASE_BRANCH = "release-please--branches--main"


class DecisionMatrixTests(unittest.TestCase):
    def decide(self, paths, labels=(), head_ref=FEATURE_BRANCH):
        return gate.decide(list(paths), list(labels), head_ref)

    def test_a_docs_change_passes(self):
        verdict = self.decide(["Assets/Scripts/Core/Pond.cs", "docs/specs/frogs.md"])

        self.assertTrue(verdict.ok, verdict.reason)

    def test_a_docs_only_change_passes(self):
        self.assertTrue(self.decide(["docs/engineering/testing.md"]).ok)

    def test_code_only_with_skip_docs_passes(self):
        verdict = self.decide(["Assets/Scripts/Core/Pond.cs"], labels=["skip-docs"])

        self.assertTrue(verdict.ok, verdict.reason)
        self.assertIn("skip-docs", verdict.reason)

    def test_code_only_without_either_fails(self):
        verdict = self.decide(["Assets/Scripts/Core/Pond.cs"])

        self.assertFalse(verdict.ok)
        self.assertIn("skip-docs", verdict.reason)

    def test_an_unrelated_label_does_not_exempt(self):
        self.assertFalse(self.decide(["Assets/Scripts/Core/Pond.cs"], labels=["area:build"]).ok)

    def test_a_pr_that_changes_nothing_passes(self):
        self.assertTrue(self.decide([]).ok)


class WhatCountsAsDocsTests(unittest.TestCase):
    def assertCounts(self, path):
        self.assertTrue(gate.is_docs(path), f"{path} should count as docs")

    def assertDoesNotCount(self, path):
        self.assertFalse(gate.is_docs(path), f"{path} should not count as docs")

    def test_the_docs_tree_counts(self):
        self.assertCounts("docs/engineering/ci-cd.md")

    def test_the_root_rules_file_counts(self):
        # CLAUDE.md is where an agent reads the rules; a change there is a
        # documentation change by any useful definition.
        self.assertCounts("CLAUDE.md")

    def test_the_readme_counts(self):
        self.assertCounts("README.md")

    def test_the_site_config_counts(self):
        self.assertCounts("mkdocs.yml")

    def test_source_does_not_count(self):
        self.assertDoesNotCount("Assets/Scripts/Core/Pond.cs")

    def test_a_workflow_does_not_count(self):
        self.assertDoesNotCount(".github/workflows/ci-tests.yml")

    def test_a_skill_does_not_count(self):
        # A SKILL.md is documentation of a skill, but it is also the skill's
        # behaviour — changing it changes what an agent does, so it does not
        # excuse the docs.
        self.assertDoesNotCount(".claude/skills/release-flow/SKILL.md")

    def test_a_markdown_file_outside_docs_does_not_count(self):
        self.assertDoesNotCount("Assets/Scripts/Core/notes.md")


class ReleasePullRequestTests(unittest.TestCase):
    def test_the_release_branch_is_exempt(self):
        verdict = gate.decide(["VERSION", "CHANGELOG.md"], [], RELEASE_BRANCH)

        self.assertTrue(verdict.ok, verdict.reason)
        self.assertIn("release", verdict.reason.lower())

    def test_the_autorelease_label_is_exempt(self):
        # Belt and braces: if the branch naming ever changes, the label still
        # exempts it. A release PR can neither reconcile docs nor label itself.
        verdict = gate.decide(["VERSION"], ["autorelease: pending"], "some-other-branch")

        self.assertTrue(verdict.ok, verdict.reason)

    def test_a_feature_branch_named_similarly_is_not_exempt(self):
        verdict = gate.decide(["Assets/Scripts/Core/Pond.cs"], [], "my-release-please-notes")

        self.assertFalse(verdict.ok)


class GracePollTests(unittest.TestCase):
    def test_a_pass_returns_immediately_without_polling(self):
        polls = []

        verdict = gate.decide_with_grace(
            ["docs/x.md"], ["nothing"], FEATURE_BRANCH,
            fetch_labels=lambda: polls.append(1) or [],
            sleep=lambda seconds: None)

        self.assertTrue(verdict.ok)
        self.assertEqual([], polls, "a passing PR must not wait")

    def test_a_label_landing_during_the_window_is_observed(self):
        # The point of the whole mechanism: no failing run is produced when the
        # label arrives moments after the PR opens.
        responses = [[], [], ["skip-docs"]]

        verdict = gate.decide_with_grace(
            ["Assets/Scripts/Core/Pond.cs"], [], FEATURE_BRANCH,
            fetch_labels=lambda: responses.pop(0),
            sleep=lambda seconds: None)

        self.assertTrue(verdict.ok, verdict.reason)

    def test_it_gives_up_after_the_window_and_fails(self):
        slept = []

        verdict = gate.decide_with_grace(
            ["Assets/Scripts/Core/Pond.cs"], [], FEATURE_BRANCH,
            fetch_labels=lambda: [],
            sleep=slept.append)

        self.assertFalse(verdict.ok)
        self.assertEqual(gate.GRACE_POLL_ATTEMPTS, len(slept))
        self.assertTrue(all(delay == gate.GRACE_POLL_SECONDS for delay in slept))

    def test_a_fetch_failure_does_not_pass_the_gate(self):
        def explode():
            raise RuntimeError("the API is down")

        verdict = gate.decide_with_grace(
            ["Assets/Scripts/Core/Pond.cs"], [], FEATURE_BRANCH,
            fetch_labels=explode,
            sleep=lambda seconds: None)

        self.assertFalse(verdict.ok)

    def test_the_grace_window_is_a_named_constant(self):
        self.assertIsInstance(gate.GRACE_POLL_SECONDS, (int, float))
        self.assertIsInstance(gate.GRACE_POLL_ATTEMPTS, int)
        self.assertGreater(gate.GRACE_POLL_ATTEMPTS, 0)


if __name__ == "__main__":
    unittest.main()
