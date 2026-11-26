"""The EditMode suite must run in an editor that can build for Android.

Unity's Android editor API — `UnityEditor.Android.AndroidPlatformIconKind`,
which is how the app icon is put into the build (#459) — lives in the
`UnityEditor.Android.Extensions` assembly. Unity's script reference says that
namespace needs Android Build Support installed and Android as the active build
target, and to wrap such code in `#if UNITY_ANDROID`.

The EditMode job used to run in GameCI's default image, which has no Android
Build Support, with the editor's default target, which is not Android. There,
`UNITY_ANDROID` is not defined, every icon test compiles to nothing, and the
suite goes green having checked nothing about the icon.

So this asserts the EditMode step runs in GameCI's Android image for the same
editor version the APKs build with, and passes `-buildTarget Android`.
`AppIconTests` also fails outright when the target is not Android, so the two
guard each other: this one in seconds, that one in the editor.

Regex rather than a YAML parse, stdlib only, like the other workflow tests.

See docs/engineering/ci-cd.md#ci-tests-the-two-suites and issue #459.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"
CI_TESTS = WORKFLOWS / "ci-tests.yml"
PR_BUILD = WORKFLOWS / "pr-build.yml"

TEST_RUNNER = re.compile(r"^\s*(?:-\s*)?uses:\s*game-ci/unity-test-runner@")
UNITY_VERSION = re.compile(r"^\s*unityVersion:\s*(\S+)")


def indent_of(line):
    return len(line) - len(line.lstrip())


def step_containing(text, pattern):
    """The lines of the one step whose block matches `pattern`."""
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if pattern.match(line):
            start = index
            while not lines[start].lstrip().startswith("- "):
                start -= 1
            step_indent = indent_of(lines[start])
            end = start + 1
            while end < len(lines) and (
                not lines[end].strip() or indent_of(lines[end]) > step_indent
            ):
                end += 1
            return lines[start:end]
    raise AssertionError(f"no step matches {pattern.pattern}")


def input_value(step, name):
    for line in step:
        match = re.match(rf"^\s*{name}:\s*(.+?)\s*$", line)
        if match:
            return match.group(1)
    return None


class EditModeRunsAgainstAndroid(unittest.TestCase):
    def setUp(self):
        self.step = step_containing(CI_TESTS.read_text(), TEST_RUNNER)

    def test_it_uses_the_android_editor_image(self):
        image = input_value(self.step, "customImage")

        self.assertIsNotNone(
            image,
            "the EditMode step names no customImage, so GameCI picks one without "
            "Android Build Support and every Android-only test compiles away",
        )
        self.assertRegex(image, r"^unityci/editor:ubuntu-[^-]+-android-\d+$")

    def test_the_image_is_the_editor_the_apks_build_with(self):
        image = input_value(self.step, "customImage") or ""
        build_versions = {
            match.group(1)
            for line in PR_BUILD.read_text().splitlines()
            for match in [UNITY_VERSION.match(line)]
            if match
        }

        self.assertEqual(len(build_versions), 1, build_versions)
        (version,) = build_versions
        self.assertEqual(image, f"unityci/editor:ubuntu-{version}-android-3")

    def test_it_makes_android_the_active_target(self):
        parameters = input_value(self.step, "customParameters") or ""

        self.assertRegex(
            parameters,
            r"(^|\s)-buildTarget\s+Android(\s|$)",
            "without -buildTarget Android, UNITY_ANDROID is undefined in the tests",
        )

    def test_it_stays_headless(self):
        parameters = input_value(self.step, "customParameters") or ""

        self.assertRegex(parameters, r"(^|\s)-nographics(\s|$)")


if __name__ == "__main__":
    unittest.main()
