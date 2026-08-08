"""Tests for the action-pin check."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import check_action_pins as pins  # noqa: E402

SHA = "3d3c42e5aac5ba805825da76410c181273ba90b1"


def findings(text, path="wf.yml"):
    return pins.check_text(path, text)


def kinds(text):
    return [finding.kind for finding in findings(text)]


class PinnedTests(unittest.TestCase):
    def test_a_full_sha_with_a_version_comment_is_clean(self):
        self.assertEqual(findings(f"      - uses: actions/checkout@{SHA} # v7.0.1"), [])

    def test_uppercase_hex_is_accepted(self):
        line = f"      - uses: actions/checkout@{SHA.upper()} # v7.0.1"
        self.assertEqual(kinds(line), [])

    def test_a_pinned_line_without_a_version_comment_warns(self):
        self.assertEqual(kinds(f"      - uses: actions/checkout@{SHA}"), ["no-version-comment"])

    def test_a_warning_is_not_a_failure(self):
        found = findings(f"      - uses: actions/checkout@{SHA}")
        self.assertFalse(found[0].fatal)

    def test_a_comment_that_is_not_a_version_still_warns(self):
        line = f"      - uses: actions/checkout@{SHA} # pinned"
        self.assertEqual(kinds(line), ["no-version-comment"])


class UnpinnedTests(unittest.TestCase):
    def test_a_tag_is_rejected(self):
        found = findings("      - uses: actions/checkout@v4")
        self.assertEqual([f.kind for f in found], ["unpinned"])
        self.assertTrue(found[0].fatal)

    def test_a_branch_is_rejected(self):
        self.assertEqual(kinds("      - uses: actions/checkout@main"), ["unpinned"])

    def test_a_short_sha_is_rejected(self):
        self.assertEqual(kinds("      - uses: actions/checkout@3d3c42e"), ["unpinned"])

    def test_a_thirty_nine_character_sha_is_rejected(self):
        self.assertEqual(kinds(f"      - uses: actions/checkout@{SHA[:-1]}"), ["unpinned"])

    def test_a_reference_with_no_version_at_all_is_rejected(self):
        self.assertEqual(kinds("      - uses: actions/checkout"), ["unpinned"])

    def test_the_finding_carries_the_line_number(self):
        text = f"jobs:\n  a:\n    steps:\n      - uses: actions/checkout@v4\n"
        self.assertEqual(findings(text)[0].line, 4)


class DeliberateFormsTests(unittest.TestCase):
    """Forms that are not third-party actions and must not be flagged."""

    def test_a_local_reusable_workflow_is_allowed(self):
        self.assertEqual(kinds("    uses: ./.github/workflows/release-build.yml"), [])

    def test_a_local_composite_action_is_allowed(self):
        self.assertEqual(kinds("      - uses: ./.github/actions/setup"), [])

    def test_a_docker_reference_is_allowed(self):
        self.assertEqual(kinds("      - uses: docker://alpine:3.20"), [])

    def test_a_remote_reusable_workflow_still_needs_a_pin(self):
        """`uses:` at job level reaches third-party code just the same."""
        line = "    uses: some-org/shared/.github/workflows/build.yml@v1"
        self.assertEqual(kinds(line), ["unpinned"])


class NotAUsesLineTests(unittest.TestCase):
    def test_a_commented_out_uses_is_ignored(self):
        self.assertEqual(kinds("      # - uses: actions/checkout@v4"), [])

    def test_the_word_uses_in_prose_is_ignored(self):
        self.assertEqual(kinds("# This workflow uses: nothing in particular"), [])

    def test_a_string_containing_uses_is_ignored(self):
        self.assertEqual(kinds('        run: echo "uses: actions/checkout@v4"'), [])


class ReportingTests(unittest.TestCase):
    def test_only_fatal_findings_fail_the_run(self):
        clean = [pins.Finding("wf.yml", 1, "no-version-comment", "x", fatal=False)]
        self.assertEqual(pins.exit_code(clean), 0)

    def test_a_fatal_finding_fails_the_run(self):
        bad = [pins.Finding("wf.yml", 1, "unpinned", "x", fatal=True)]
        self.assertEqual(pins.exit_code(bad), 1)

    def test_no_findings_passes(self):
        self.assertEqual(pins.exit_code([]), 0)


class RealWorkflowTests(unittest.TestCase):
    """The check must pass on this repository as it stands."""

    def test_every_workflow_in_this_repo_is_pinned(self):
        root = Path(__file__).resolve().parents[3] / ".github" / "workflows"
        fatal = [
            finding
            for path in sorted(root.glob("*.yml"))
            for finding in pins.check_text(str(path), path.read_text())
            if finding.fatal
        ]

        self.assertEqual(fatal, [], f"unpinned actions: {fatal}")

    def test_the_repo_has_workflows_to_check(self):
        """Guards against the suite passing because it found nothing."""
        root = Path(__file__).resolve().parents[3] / ".github" / "workflows"
        self.assertGreater(len(list(root.glob("*.yml"))), 5)


if __name__ == "__main__":
    unittest.main()
