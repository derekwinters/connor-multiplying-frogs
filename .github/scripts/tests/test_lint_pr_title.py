"""Unit tests for the PR-title lint."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import lint_pr_title  # noqa: E402


class ValidTitleTests(unittest.TestCase):
    def assertValid(self, title):
        problems = lint_pr_title.validate(title)
        self.assertEqual([], problems, f"{title!r} should be valid")

    def test_plain_type_and_subject(self):
        self.assertValid("feat: add the pond")

    def test_with_a_scope(self):
        self.assertValid("feat(frogs): add the pond")

    def test_a_scope_with_hyphens_and_digits(self):
        self.assertValid("ci(pr-build): cache the 2d atlas")

    def test_a_breaking_change_marker(self):
        self.assertValid("feat!: change how splitting works")

    def test_a_breaking_change_marker_with_a_scope(self):
        self.assertValid("feat(frogs)!: change how splitting works")

    def test_every_allowed_type(self):
        for kind in lint_pr_title.ALLOWED_TYPES:
            self.assertValid(f"{kind}: something happened")

    def test_the_release_please_title(self):
        # If this one ever fails to lint, releases stop merging.
        self.assertValid("chore(main): release 0.1.0")

    def test_a_subject_containing_a_colon(self):
        self.assertValid("docs: explain why /VERSION is the source of truth")


class InvalidTitleTests(unittest.TestCase):
    def assertProblem(self, title, fragment):
        problems = lint_pr_title.validate(title)
        self.assertTrue(problems, f"{title!r} should be rejected")
        self.assertTrue(
            any(fragment in problem for problem in problems),
            f"expected a problem mentioning {fragment!r}, got {problems}",
        )

    def test_no_type_at_all(self):
        self.assertProblem("add the pond", "not a Conventional Commit")

    def test_an_unknown_type(self):
        self.assertProblem("feet: add the pond", "not an allowed type")

    def test_an_uppercase_type_is_rejected_with_a_hint(self):
        self.assertProblem("Feat: add the pond", "Did you mean 'feat'?")

    def test_a_missing_colon(self):
        self.assertProblem("feat add the pond", "not a Conventional Commit")

    def test_a_missing_space_after_the_colon(self):
        self.assertProblem("feat:add the pond", "not a Conventional Commit")

    def test_two_spaces_after_the_colon(self):
        self.assertProblem("feat:  add the pond", "more than one space")

    def test_an_empty_subject(self):
        self.assertProblem("feat:", "not a Conventional Commit")

    def test_a_whitespace_only_subject(self):
        self.assertProblem("feat:   ", "no subject")

    def test_an_empty_scope(self):
        self.assertProblem("feat(): add the pond", "scope is empty")

    def test_an_uppercase_scope(self):
        self.assertProblem("feat(Frogs): add the pond", "must be lowercase")

    def test_a_scope_with_a_space(self):
        self.assertProblem("feat(the frogs): add the pond", "must be lowercase")

    def test_a_trailing_full_stop(self):
        self.assertProblem("feat: add the pond.", "full stop")

    def test_a_title_that_is_too_long(self):
        title = "feat: " + ("a" * lint_pr_title.MAX_LENGTH)
        self.assertProblem(title, f"keep it to {lint_pr_title.MAX_LENGTH}")

    def test_leading_whitespace(self):
        self.assertProblem("  feat: add the pond", "leading or trailing whitespace")

    def test_an_empty_title(self):
        self.assertProblem("", "empty")

    def test_reports_several_problems_at_once(self):
        problems = lint_pr_title.validate("Feat(Frogs): add the pond.")

        self.assertEqual(3, len(problems), problems)


class LengthTests(unittest.TestCase):
    def test_real_merged_titles_from_this_repo_are_accepted(self):
        # The limit was calibrated against these. A limit that rejects the
        # titles the project actually writes is a limit people route around.
        for title in (
            "test(core): guard against /VERSION and the release-please manifest drifting",
            "docs(ci): adopt and document the SHA-pinning convention for GitHub Actions",
            "docs: author the root CLAUDE.md with the non-negotiable engineering rules",
            "feat(build): stamp the version into PlayerSettings at build time",
        ):
            self.assertEqual([], lint_pr_title.validate(title), title)


class BoundaryTests(unittest.TestCase):
    def test_a_title_of_exactly_the_maximum_length_is_allowed(self):
        title = "feat: " + "a" * (lint_pr_title.MAX_LENGTH - len("feat: "))

        self.assertEqual(lint_pr_title.MAX_LENGTH, len(title))
        self.assertEqual([], lint_pr_title.validate(title))


class CommandLineTests(unittest.TestCase):
    def test_a_conforming_title_exits_zero(self):
        self.assertEqual(0, lint_pr_title.main(["--title", "feat: add the pond"]))

    def test_a_non_conforming_title_exits_one(self):
        self.assertEqual(1, lint_pr_title.main(["--title", "add the pond"]))


if __name__ == "__main__":
    unittest.main()
