"""A backfill checks with today's pipeline scripts, not the tag's.

`release-build` is run two ways: from release-please in the run that made the
release, and by hand (`workflow_dispatch`) as the backfill for a release whose
attach failed. The backfill builds the game from the tag — it must build
exactly what was released — and it gets today's workflow YAML, because Actions
runs the workflow file at the ref the dispatch was made against.

But the YAML is not the whole pipeline. It shells out to checker scripts under
`.github/scripts/`, and until #453 it ran them from the tag's checkout: the
tag's copies. v0.6.0 was tagged with no APKs because `check_apk_signature.py`
could not read apksigner's `V2 Signer:` lines (#450); the fix landed on `main`
(#451), and a backfill of v0.6.0 would still have run v0.6.0's broken copy and
rejected the APKs all over again. A backfill exists to recover from a pipeline
bug, so it has to run the pipeline as it is now.

So the shape this asserts:

- a second checkout, of `github.workflow_sha` — the workflow's own commit —
  into its own path, pinned to the same action SHA as the tag checkout and
  placed after it (checking out into the workspace root clears the workspace,
  so the other way round would delete it);
- every `.github/scripts/` invocation in the workflow runs from that path, and
  none from the tag checkout;
- both release gates, `check_release_apks.py` and `check_apk_signature.py`,
  are still among them.

On the normal `workflow_call` path the workflow's commit is the release commit,
so nothing changes there.

Regex rather than a YAML parse, and stdlib only, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the Python
already on the runner.

See docs/engineering/ci-cd.md and issue #453.
"""

import re
import unittest
from pathlib import Path

from .test_release_partial_attach import LIST_ITEM, indent_of, scalar, uncommented

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"
RELEASE_BUILD = WORKFLOWS / "release-build.yml"

RELEASE_GATES = ["check_release_apks.py", "check_apk_signature.py"]

CHECKOUT = re.compile(r"actions/checkout@([0-9a-fA-F]{40})")
WORKFLOW_SHA = re.compile(r"^\$\{\{\s*github\.workflow_sha\s*\}\}$")
TAG_REF = re.compile(r"^\$\{\{\s*inputs\.tag\s*\}\}$")

# A path to one of the pipeline's scripts, with whatever directory precedes it.
SCRIPT_PATH = re.compile(r"(\S*?)\.github/scripts/([\w.-]+\.py)")


def top_level_steps(text):
    """Every step of the job, named or not, as a list of blocks, in order."""
    lines = text.splitlines()
    found = []
    step_indent = None

    for index, line in enumerate(lines):
        match = LIST_ITEM.match(line)
        if not match or line.lstrip().startswith("#"):
            continue
        opening = len(match.group(1))
        if step_indent is None:
            before = [l for l in lines[:index]
                      if l.strip() and not l.lstrip().startswith("#")]
            if not before or not re.match(r"^\s*steps:\s*$", before[-1]):
                continue
            step_indent = opening
        if opening != step_indent:
            continue

        end = index + 1
        while end < len(lines):
            following = lines[end]
            if following.strip() and indent_of(following) <= opening:
                break
            end += 1
        found.append(lines[index:end])

    return found


class PipelineScriptsTests(unittest.TestCase):
    def setUp(self):
        self.text = RELEASE_BUILD.read_text()
        self.steps = top_level_steps(self.text)
        self.checkouts = [
            (position, block) for position, block in enumerate(self.steps)
            if CHECKOUT.search(uncommented(block))]

    def tag_checkout(self):
        found = [(p, b) for p, b in self.checkouts
                 if TAG_REF.match(scalar(b, "ref") or "")]
        self.assertEqual(
            len(found), 1,
            "Expected exactly one checkout of `${{ inputs.tag }}` — the "
            "release is built from the tag.")
        return found[0]

    def pipeline_checkout(self):
        found = [(p, b) for p, b in self.checkouts
                 if WORKFLOW_SHA.match(scalar(b, "ref") or "")]
        self.assertEqual(
            len(found), 1,
            "No checkout of `${{ github.workflow_sha }}`. Without one, the "
            "checker scripts come from the tag, and a backfill runs the very "
            "copies whose bug it exists to recover from (#453).")
        return found[0]

    def test_the_step_parser_sees_the_steps(self):
        """Guards every test here against passing on an empty step list."""
        self.assertGreater(len(self.steps), 5)
        self.assertTrue(self.checkouts, "Found no actions/checkout step at all.")

    def test_the_game_is_still_built_from_the_tag(self):
        _, block = self.tag_checkout()
        self.assertIsNone(
            scalar(block, "path"),
            "The tag checkout must stay at the workspace root: that is where "
            "the Unity build looks for the project.")

    def test_the_pipeline_scripts_are_checked_out_from_the_workflows_commit(self):
        _, block = self.pipeline_checkout()
        path = scalar(block, "path")
        self.assertTrue(
            path and path not in (".", "./"),
            "The workflow_sha checkout needs its own `path:`. At the workspace "
            "root it would replace the tag's source, and the release would be "
            "built from the wrong commit.")

    def test_the_pipeline_checkout_comes_after_the_tag_checkout(self):
        """actions/checkout into the workspace root clears the workspace first."""
        tag_position, _ = self.tag_checkout()
        pipeline_position, _ = self.pipeline_checkout()
        self.assertGreater(
            pipeline_position, tag_position,
            "The workflow_sha checkout runs before the tag checkout, which "
            "clears the workspace and deletes it again.")

    def test_both_checkouts_pin_the_same_action(self):
        _, tag_block = self.tag_checkout()
        _, pipeline_block = self.pipeline_checkout()
        self.assertEqual(
            CHECKOUT.search(uncommented(pipeline_block)).group(1),
            CHECKOUT.search(uncommented(tag_block)).group(1),
            "The two checkouts pin different actions/checkout SHAs. Pin both "
            "to the same one, so a Dependabot bump moves them together.")

    def test_every_pipeline_script_runs_from_the_workflows_commit(self):
        _, block = self.pipeline_checkout()
        path = (scalar(block, "path") or "").rstrip("/")

        invocations = []
        for step in self.steps:
            if CHECKOUT.search(uncommented(step)):
                continue
            invocations.extend(SCRIPT_PATH.findall(uncommented(step)))

        self.assertTrue(invocations, "Nothing runs a .github/scripts/ script.")

        invoked = {script for _, script in invocations}
        for gate in RELEASE_GATES:
            with self.subTest(gate=gate):
                self.assertIn(gate, invoked, f"Nothing runs {gate} any more.")

        for prefix, script in invocations:
            with self.subTest(script=script):
                self.assertEqual(
                    prefix.strip("\"'").rstrip("/"), path,
                    f"{script} runs from {prefix or 'the workspace root'!r}, "
                    f"which is the tag's checkout — the tag's copy of the "
                    f"script. Run it from {path!r}/, the workflow's own "
                    f"commit, so a backfill checks with today's pipeline.")


if __name__ == "__main__":
    unittest.main()
