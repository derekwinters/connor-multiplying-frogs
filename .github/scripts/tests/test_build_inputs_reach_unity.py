"""A value the Unity build needs must reach the Unity process.

`game-ci/unity-builder` does not run Unity on the runner. It runs it inside a
container, and it forwards a **fixed allow-list** of environment variables into
that container — `UNITY_*`, `BUILD_*`, `ANDROID_*`, `CUSTOM_PARAMETERS`, a
handful of `GITHUB_*`. Nothing else. Setting `FROGS_ANDROID_PROFILE` in a
step's `env:` puts it in the runner's environment, where the Unity process
never looks.

That is silent by construction. Every reader of these variables treats "unset"
as "not asked for" and returns early, so a build with none of them arrives
looks exactly like a build that wanted none of them. It shipped two `v0.1.0`
APKs — a device build and an emulator build — that were byte-for-byte the same
file, both ARM64/IL2CPP, because the profile never reached Unity (issue #218).

So the values go on Unity's own command line, via the action's
`customParameters` input, which the container entrypoint appends verbatim to
its `unity-editor` invocation. This asserts that they still do, and that none
of them has drifted back into an `env:` block where it would do nothing.

Stdlib only, and regex rather than a YAML parse, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the Python
already on the runner.

See docs/engineering/ci-cd.md and issue #218.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"

UNITY_BUILDER = re.compile(r"^\s*(?:-\s*)?uses:\s*game-ci/unity-builder@")

LIST_ITEM = re.compile(r"^(\s*)-\s")

# Anything this project passes into a build. The prefix is the point: it is
# what marks a value as ours rather than the action's.
FROGS_ENV = re.compile(r"^\s*(FROGS_[A-Z0-9_]+)\s*:")

# The command-line spelling of the same thing: `-frogsAndroidProfile emulator`.
FROGS_FLAG = re.compile(r"^-frogs[A-Za-z0-9]*$")

# The versionCode every build has to carry. Absent, the editor falls back to
# shelling out to git inside the container, which is not what the workflow
# checked out and not something the build should depend on.
VERSION_CODE_FLAG = "-frogsVersionCode"

PROFILE_FLAG = "-frogsAndroidProfile"

INLINE_COMMENT = re.compile(r"\s+#.*$")


def indent_of(line):
    return len(line) - len(line.lstrip())


def step_block(lines, index):
    """The lines of the list item containing `lines[index]`."""
    start = index
    while start >= 0 and not LIST_ITEM.match(lines[start]):
        start -= 1
    if start < 0:
        return [lines[index]]

    opening = len(LIST_ITEM.match(lines[start]).group(1))

    end = start + 1
    while end < len(lines):
        line = lines[end]
        if line.strip() and indent_of(line) <= opening:
            break
        end += 1

    return lines[start:end]


def scalar(block, key):
    """The value of `key: value` in `block`, or None."""
    pattern = re.compile(rf"^\s*(?:-\s*)?{re.escape(key)}:\s*(.+?)\s*$")
    for line in block:
        match = pattern.match(line)
        if match:
            return INLINE_COMMENT.sub("", match.group(1)).strip().strip("'\"")
    return None


def unity_build_steps(text):
    """Every unity-builder step in one workflow, as (name, block)."""
    lines = text.splitlines()
    steps = []

    for index, line in enumerate(lines):
        if UNITY_BUILDER.match(line):
            block = step_block(lines, index)
            steps.append((scalar(block, "name") or "(unnamed step)", block))

    return steps


def frogs_environment_variables(block):
    """The `FROGS_*` keys a step sets in its `env:`, sorted."""
    return sorted({match.group(1) for match in
                   (FROGS_ENV.match(line) for line in block) if match})


def custom_parameters(block):
    """A step's `customParameters`, split into words. Empty if it has none."""
    value = scalar(block, "customParameters")
    return value.split() if value else []


def flag_values(block):
    """`customParameters` read as {flag: value}.

    A flag whose value is missing — the end of the string, or another flag —
    maps to None rather than being dropped. `-frogsBuildSha -frogsVersionCode
    412` would otherwise quietly pass the sha as "-frogsVersionCode" and lose
    the version code entirely, which is the same shape of silent failure this
    whole file exists to stop.
    """
    words = custom_parameters(block)
    found = {}

    for index, word in enumerate(words):
        if not FROGS_FLAG.match(word):
            continue
        following = words[index + 1] if index + 1 < len(words) else None
        found[word] = None if following is None or following.startswith("-") else following

    return found


def workflow_files():
    return sorted(WORKFLOWS.glob("*.yml"))


class RealWorkflowTests(unittest.TestCase):
    """The workflows as they stand in this repo."""

    def setUp(self):
        self.building = [
            (path, path.read_text()) for path in workflow_files()
            if unity_build_steps(path.read_text())
        ]

    def test_the_unity_build_workflows_are_found(self):
        """Guards against the rest of this suite passing on nothing at all."""
        names = sorted(path.name for path, _ in self.building)
        self.assertEqual(
            names, ["pr-build.yml", "rc-build.yml", "release-build.yml"])

    def test_no_build_passes_its_values_in_the_environment(self):
        """The bug. An `env:` on the step never reaches the container."""
        for path, text in self.building:
            for name, block in unity_build_steps(text):
                with self.subTest(workflow=path.name, step=name):
                    self.assertEqual(
                        frogs_environment_variables(block), [],
                        f"{path.name}, step {name!r}, sets these in `env:`. "
                        f"unity-builder forwards only its own allow-list of "
                        f"variables into the container, so Unity never sees "
                        f"them and every reader treats them as unset. Pass "
                        f"them on the command line with `customParameters` "
                        f"instead — see issue #218.")

    def test_every_build_is_told_its_version_code(self):
        """Otherwise the editor shells out to git inside the container."""
        for path, text in self.building:
            for name, block in unity_build_steps(text):
                with self.subTest(workflow=path.name, step=name):
                    self.assertIn(
                        VERSION_CODE_FLAG, flag_values(block),
                        f"{path.name}, step {name!r}, passes no "
                        f"{VERSION_CODE_FLAG}. The Android versionCode is the "
                        f"commit count, and a build that has to work it out "
                        f"for itself is a build that can disagree with the "
                        f"workflow that started it.")

    def test_every_flag_passed_has_a_value(self):
        """A flag swallowing the next flag is the failure mode, not an error."""
        for path, text in self.building:
            for name, block in unity_build_steps(text):
                for flag, value in flag_values(block).items():
                    with self.subTest(workflow=path.name, step=name, flag=flag):
                        self.assertIsNotNone(
                            value,
                            f"{path.name}, step {name!r}, passes {flag} with "
                            f"nothing after it. Unity would take the next flag "
                            f"as its value and drop that flag's own.")

    def test_two_builds_in_one_workflow_ask_for_different_profiles(self):
        """Two Unity builds in a file are the device/emulator split.

        If they cannot be told apart on the command line they cannot be told
        apart in the APK either, which is exactly what #218 was.
        """
        for path, text in self.building:
            steps = unity_build_steps(text)
            if len(steps) < 2:
                continue

            profiles = [flag_values(block).get(PROFILE_FLAG) for _, block in steps]

            with self.subTest(workflow=path.name):
                self.assertEqual(
                    len(set(profiles)), len(profiles),
                    f"{path.name} runs {len(steps)} Unity builds but asks for "
                    f"profiles {profiles}. Two builds that ask Unity for the "
                    f"same thing produce the same APK twice.")


class StepDetectionTests(unittest.TestCase):
    """The detectors see the real shapes, so a clean result means something."""

    STEPS = """\
jobs:
  build:
    steps:
      - name: Build the device APK
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          FROGS_ANDROID_PROFILE: device
        with:
          targetPlatform: Android
          customParameters: -frogsVersionCode 412 -frogsAndroidProfile device

      - name: Build the emulator APK
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        with:
          targetPlatform: Android
          customParameters: -frogsVersionCode 412 -frogsAndroidProfile emulator
"""

    def test_each_build_step_is_found(self):
        self.assertEqual(
            [name for name, _ in unity_build_steps(self.STEPS)],
            ["Build the device APK", "Build the emulator APK"])

    def test_a_frogs_variable_in_the_environment_is_seen(self):
        _, block = unity_build_steps(self.STEPS)[0]
        self.assertEqual(
            frogs_environment_variables(block), ["FROGS_ANDROID_PROFILE"])

    def test_the_licence_secrets_are_not_mistaken_for_ours(self):
        """`UNITY_LICENSE` is on the action's allow-list and belongs in `env:`."""
        _, block = unity_build_steps(self.STEPS)[0]
        self.assertNotIn("UNITY_LICENSE", frogs_environment_variables(block))

    def test_a_step_does_not_absorb_its_neighbours_parameters(self):
        first, second = (block for _, block in unity_build_steps(self.STEPS))
        self.assertEqual(flag_values(first)["-frogsAndroidProfile"], "device")
        self.assertEqual(flag_values(second)["-frogsAndroidProfile"], "emulator")

    def test_a_workflow_with_no_unity_build_yields_nothing(self):
        self.assertEqual(unity_build_steps("jobs:\n  test:\n    steps:\n"
                                           "      - run: dotnet test\n"), [])


class FlagParsingTests(unittest.TestCase):
    def block(self, parameters):
        return [f"          customParameters: {parameters}"]

    def test_a_flag_and_its_value_are_paired(self):
        self.assertEqual(
            flag_values(self.block("-frogsBuildSha abc1234")),
            {"-frogsBuildSha": "abc1234"})

    def test_several_flags_are_all_read(self):
        self.assertEqual(
            flag_values(self.block(
                "-frogsVersionCode 412 -frogsApplicationIdSuffix .debug")),
            {"-frogsVersionCode": "412", "-frogsApplicationIdSuffix": ".debug"})

    def test_a_flag_at_the_end_with_no_value_is_reported(self):
        self.assertEqual(flag_values(self.block("-frogsBuildSha")),
                         {"-frogsBuildSha": None})

    def test_a_flag_followed_by_another_flag_is_reported(self):
        self.assertEqual(
            flag_values(self.block("-frogsBuildSha -frogsVersionCode 412")),
            {"-frogsBuildSha": None, "-frogsVersionCode": "412"})

    def test_the_actions_own_parameters_are_left_alone(self):
        """Only `-frogs…` flags are ours to check."""
        self.assertEqual(
            flag_values(self.block("-quit -batchmode -frogsVersionCode 412")),
            {"-frogsVersionCode": "412"})

    def test_a_step_with_no_custom_parameters_passes_nothing(self):
        self.assertEqual(flag_values(["          targetPlatform: Android"]), {})


if __name__ == "__main__":
    unittest.main()
