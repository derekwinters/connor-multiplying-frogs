"""Tests for the unity-builder CLI check (issue #480)."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import check_unity_builder_cli as cli  # noqa: E402

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"

SHA = "eb1b9fba120c6e62c9fb7a7a81d6c107ce004c45"


def step(env=None, cli_version="v0.1.77", name="Build the device APK"):
    """One unity-builder step inside a job, as a workflow would write it."""
    lines = [
        "jobs:",
        "  build:",
        "    steps:",
        "      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1",
        "",
        f"      - name: {name}",
        f"        uses: game-ci/unity-builder@{SHA} # v6.0.0",
    ]
    if env is not None:
        lines.append("        env:")
        lines.extend(f"          {line}" for line in env)
    lines.append("        with:")
    lines.append("          unityVersion: 6000.0.81f1")
    if cli_version is not None:
        lines.append(f"          cliVersion: {cli_version}")
    lines.append("          targetPlatform: Android")
    lines.append("")
    lines.append("      - name: Upload the device APK")
    lines.append("        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1")
    return "\n".join(lines) + "\n"


GOOD_ENV = [
    "UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}",
    "GITHUB_TOKEN: ${{ github.token }}",
]


def kinds(text):
    return [finding.kind for finding in cli.check_text("wf.yml", text)]


class CleanStepTests(unittest.TestCase):
    def test_a_pinned_cli_with_the_token_is_clean(self):
        self.assertEqual(kinds(step(env=GOOD_ENV)), [])

    def test_a_quoted_version_is_accepted(self):
        self.assertEqual(kinds(step(env=GOOD_ENV, cli_version='"v0.1.77"')), [])

    def test_a_workflow_without_unity_builder_is_clean(self):
        text = "jobs:\n  a:\n    steps:\n      - run: echo hi\n"
        self.assertEqual(kinds(text), [])


class CliVersionTests(unittest.TestCase):
    def test_a_missing_cli_version_fails(self):
        self.assertEqual(kinds(step(env=GOOD_ENV, cli_version=None)), ["cli-version-missing"])

    def test_latest_fails(self):
        self.assertEqual(kinds(step(env=GOOD_ENV, cli_version="latest")), ["cli-version-latest"])

    def test_quoted_latest_fails(self):
        self.assertEqual(kinds(step(env=GOOD_ENV, cli_version="'latest'")), ["cli-version-latest"])

    def test_an_empty_cli_version_fails(self):
        # An empty input is the action's default, which is `latest`.
        self.assertEqual(kinds(step(env=GOOD_ENV, cli_version='""')), ["cli-version-missing"])

    def test_a_cli_version_in_a_later_step_does_not_count(self):
        text = step(env=GOOD_ENV, cli_version=None) + "        with:\n          cliVersion: v0.1.77\n"
        self.assertIn("cli-version-missing", kinds(text))


class TokenTests(unittest.TestCase):
    def test_no_env_block_fails(self):
        self.assertEqual(kinds(step(env=None)), ["token-missing"])

    def test_an_env_block_without_the_token_fails(self):
        env = ["UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}"]
        self.assertEqual(kinds(step(env=env)), ["token-missing"])

    def test_the_token_under_with_does_not_count(self):
        env = ["UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}"]
        text = step(env=env).replace(
            "          unityVersion: 6000.0.81f1",
            "          unityVersion: 6000.0.81f1\n          GITHUB_TOKEN: ${{ github.token }}")
        self.assertEqual(kinds(text), ["token-missing"])

    def test_the_token_set_to_something_else_fails(self):
        env = ["GITHUB_TOKEN: ''"]
        self.assertEqual(kinds(step(env=env)), ["token-missing"])

    def test_secrets_github_token_is_accepted(self):
        env = ["GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}"]
        self.assertEqual(kinds(step(env=env)), [])

    def test_a_commented_out_token_does_not_count(self):
        env = ["# GITHUB_TOKEN: ${{ github.token }}"]
        self.assertEqual(kinds(step(env=env)), ["token-missing"])


class BothMissingTests(unittest.TestCase):
    def test_both_problems_are_reported(self):
        self.assertEqual(sorted(kinds(step(env=None, cli_version=None))),
                         ["cli-version-missing", "token-missing"])

    def test_a_finding_points_at_the_uses_line(self):
        found = cli.check_text("wf.yml", step(env=None))
        self.assertEqual(found[0].line, 7)


class MainTests(unittest.TestCase):
    def test_exit_code_is_nonzero_on_a_finding(self):
        self.assertEqual(cli.exit_code(cli.check_text("wf.yml", step(env=None))), 1)

    def test_exit_code_is_zero_when_clean(self):
        self.assertEqual(cli.exit_code(cli.check_text("wf.yml", step(env=GOOD_ENV))), 0)


class RepositoryWorkflowsTests(unittest.TestCase):
    """The real workflows, so the check failing is also a test failing."""

    def test_every_build_workflow_has_unity_builder_steps(self):
        # If this ever finds none, the check has gone blind, not clean.
        counts = {name: len(cli.unity_builder_steps((WORKFLOWS / name).read_text()))
                  for name in ("pr-build.yml", "rc-build.yml", "release-build.yml")}
        self.assertEqual(counts, {"pr-build.yml": 2, "rc-build.yml": 1, "release-build.yml": 2})

    def test_every_unity_builder_step_pins_the_cli_and_passes_the_token(self):
        paths = sorted(WORKFLOWS.glob("*.yml")) + sorted(WORKFLOWS.glob("*.yaml"))
        self.assertEqual(cli.check_paths(paths), [])

    def test_every_step_pins_the_same_cli_version(self):
        versions = set()
        for path in sorted(WORKFLOWS.glob("*.yml")):
            for block in cli.unity_builder_steps(path.read_text()):
                versions.add(cli.with_input(block, "cliVersion"))
        self.assertEqual(len(versions), 1, versions)


if __name__ == "__main__":
    unittest.main()
