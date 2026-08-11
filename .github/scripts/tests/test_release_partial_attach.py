"""A release must get the APK that did build.

`release-build` builds two APKs, and until now one failure meant the release
got **neither**: the emulator build failed, the job stopped there, and the
attach step — three steps further down — never ran. `v0.2.0` was tagged and
published with no installable asset on it at all, while a perfectly good device
APK sat in the runner's filesystem until it was deleted (issues #282, #212).

Nothing is gained by throwing that away. A release with one of its two APKs is
worse than a release with both and better than a release with none, and which
of the three happened has to be obvious from the run rather than inferred from
a missing file.

So this asserts the shape that gets that:

- the emulator build may fail without aborting the job, and the device build
  may not — the device APK *is* the release;
- a failed emulator build still fails the run, after the attach, so a partial
  release is never a green one;
- the two-APK comparison stays strict whenever both APKs exist. It is the gate
  that caught `v0.1.0` shipping two identical files (#218), and "one of them is
  missing" is the only reason it may be skipped;
- a partial attach says so, as a warning and in the job summary.

Regex rather than a YAML parse, and stdlib only, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the Python
already on the runner.

See docs/engineering/ci-cd.md and issue #282.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"
RELEASE_BUILD = WORKFLOWS / "release-build.yml"

DEVICE_STEP = "Build the device APK"
EMULATOR_STEP = "Build the emulator APK"

CHECK_SCRIPT = "check_release_apks.py"

# The `- ` that opens a list item, capturing its indentation.
LIST_ITEM = re.compile(r"^(\s*)-\s")

INLINE_COMMENT = re.compile(r"\s+#.*$")


def indent_of(line):
    return len(line) - len(line.lstrip())


def steps(text):
    """Every step in the workflow, as {name: block-of-lines}.

    A step runs from the `- ` that opens it to the next line no more indented
    than that `- `. Only named steps are returned: a step this suite has an
    opinion about is one worth naming.
    """
    lines = text.splitlines()
    found = {}

    for index, line in enumerate(lines):
        match = LIST_ITEM.match(line)
        if not match:
            continue

        opening = len(match.group(1))
        end = index + 1
        while end < len(lines):
            following = lines[end]
            if following.strip() and indent_of(following) <= opening:
                break
            end += 1

        block = lines[index:end]
        name = scalar(block, "name")
        if name:
            found[name] = block

    return found


def scalar(block, key):
    """The value of `key: value` in `block`, or None."""
    pattern = re.compile(rf"^\s*(?:-\s*)?{re.escape(key)}:\s*(.+?)\s*$")
    for line in block:
        match = pattern.match(line)
        if match:
            return INLINE_COMMENT.sub("", match.group(1)).strip().strip("'\"")
    return None


def body(block):
    """The step's lines as one string, for asking what its script does."""
    return "\n".join(block)


def uncommented(block):
    """The same, with whole-line comments dropped.

    A rule explained in a comment is not a rule the run obeys, and every step
    in this workflow is heavily commented — including with the shapes that used
    to be wrong.
    """
    return "\n".join(line for line in block if not line.lstrip().startswith("#"))


class ReleaseBuildTests(unittest.TestCase):
    def setUp(self):
        self.text = RELEASE_BUILD.read_text()
        self.steps = steps(self.text)

    def test_both_builds_are_present_and_named(self):
        """Guards against every other test here passing on nothing."""
        self.assertIn(DEVICE_STEP, self.steps)
        self.assertIn(EMULATOR_STEP, self.steps)

    def test_a_failed_emulator_build_does_not_abort_the_job(self):
        """The bug: one failure meant the release got neither APK."""
        self.assertEqual(
            scalar(self.steps[EMULATOR_STEP], "continue-on-error"), "true",
            f"{EMULATOR_STEP!r} has no `continue-on-error: true`, so a failure "
            f"there stops the job before the attach step and the release gets "
            f"nothing — not even the device APK, which built fine. See #282.")

    def test_a_failed_device_build_still_aborts_the_job(self):
        """The device APK is the release. There is nothing to attach without it."""
        self.assertIsNone(
            scalar(self.steps[DEVICE_STEP], "continue-on-error"),
            f"{DEVICE_STEP!r} must not continue on error. The device APK is "
            f"what people install; a release without it is not a release.")

    def test_the_emulator_build_can_be_referred_to_later(self):
        """`steps.<id>.outcome` is how the run finds out it was partial."""
        self.assertIsNotNone(
            scalar(self.steps[EMULATOR_STEP], "id"),
            f"{EMULATOR_STEP!r} has no `id:`, so nothing downstream can ask "
            f"whether it failed.")

    def test_a_failed_emulator_build_still_fails_the_run(self):
        """Attaching what built must not turn a broken build green."""
        emulator_id = scalar(self.steps[EMULATOR_STEP], "id")
        outcome = f"steps.{emulator_id}.outcome"

        failing = [
            name for name, block in self.steps.items()
            if outcome in uncommented(block) and "exit 1" in uncommented(block)
        ]

        self.assertTrue(
            failing,
            f"No step reads {outcome} and fails on it. A release that got one "
            f"of its two APKs must still show up as a failed run, or the next "
            f"person to look sees a green build and a half-filled release.")

    def test_the_two_apk_comparison_is_still_run(self):
        """#218's gate. Not weakened by any of the above."""
        checking = [name for name, block in self.steps.items()
                    if CHECK_SCRIPT in uncommented(block)]

        self.assertTrue(checking, f"Nothing runs {CHECK_SCRIPT} any more.")

        for name in checking:
            with self.subTest(step=name):
                script = uncommented(self.steps[name])
                self.assertIn("--device", script)
                self.assertIn("--emulator", script)

    def test_the_comparison_is_skipped_only_when_an_apk_is_missing(self):
        """Both present is the strict case, and it must be tested for.

        A comparison that ran only when someone remembered to ask for it would
        be no comparison at all — the v0.1.0 pair looked entirely plausible
        from the outside.
        """
        checking = [block for _, block in self.steps.items()
                    if CHECK_SCRIPT in uncommented(block)]

        for block in checking:
            script = uncommented(block)
            with self.subTest(step=scalar(block, "name")):
                self.assertTrue(
                    re.search(r'\[\s+-f\s+"?\$\{?device', script)
                    and re.search(r'\[\s+-f\s+"?\$\{?emulator', script),
                    "The step running the comparison does not test for both "
                    "APKs existing. It may only be skipped when one of them "
                    "genuinely is not there.")

    def test_a_partial_attach_is_never_silent(self):
        """One APK on a release has to be visible without reading the log."""
        attaching = [block for name, block in self.steps.items()
                     if "gh release upload" in uncommented(block)]

        self.assertTrue(attaching, "Nothing attaches anything to the release.")

        for block in attaching:
            script = uncommented(block)
            with self.subTest(step=scalar(block, "name")):
                self.assertIn(
                    "::warning::", script,
                    "The attach step raises no warning, so a release that got "
                    "one APK instead of two looks exactly like one that got "
                    "both.")
                self.assertIn(
                    "GITHUB_STEP_SUMMARY", script,
                    "The attach step writes no job summary, which is where "
                    "what a release actually received is read.")


if __name__ == "__main__":
    unittest.main()
