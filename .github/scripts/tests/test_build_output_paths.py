"""A workflow must read Unity's output from where Unity actually writes it.

`game-ci/unity-builder` writes to `<buildsPath>/<targetPlatform>/<buildName>`.
It appends the platform directory itself, whether or not `buildsPath` was set.
So a workflow that sets `buildsPath: build/device` and then globs
`build/device/*.apk` matches nothing — the APK is in `build/device/Android/`.

That is a silent failure rather than a loud one. The glob is consumed under
`shopt -s nullglob`, so both patterns expand to nothing, the assets array is
empty, and the step reports that the build produced no APK when in fact it
produced two. Both Unity builds go green and the release ends up with no asset
on it (issue #212, release `v0.1.0`).

The invariant this asserts is that the two halves cannot disagree: for every
workflow that runs a Unity build, every APK path anywhere in that workflow must
sit under the `<buildsPath>/<targetPlatform>/` of a build in the same file —
and every build path declared must be read by something. Set a `buildsPath` and
forget the platform segment, or add a second build profile and forget to
collect it, and this fails at review time rather than at release time.

Stdlib only, and regex rather than a YAML parse, for the same reason as
`check_action_pins.py`: the pipeline suites run on nothing but the Python
already on the runner. The shapes needed here — a `uses:`, a few scalar keys
under a `with:`, and a path ending in `.apk` — are read directly.

See docs/engineering/ci-cd.md and issue #212.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"

# The action that writes the APKs. Matched at a step's `- uses:` or a bare
# `uses:`, pinned to a SHA, so the version does not have to be tracked here.
UNITY_BUILDER = re.compile(r"^\s*(?:-\s*)?uses:\s*game-ci/unity-builder@")

# The `- ` that opens a list item, capturing its indentation.
LIST_ITEM = re.compile(r"^(\s*)-\s")

# unity-builder's own default when `buildsPath` is not set. The two build
# workflows that work rely on it.
DEFAULT_BUILDS_PATH = "build"

# A path ending in `.apk`: a glob, an artifact path, an upload argument.
APK_PATH = re.compile(r"[A-Za-z0-9_./*-]*\.apk\b")

# A YAML inline comment, which the spec requires be preceded by whitespace —
# so a `#` inside a value is left alone.
INLINE_COMMENT = re.compile(r"\s+#.*$")


def indent_of(line):
    return len(line) - len(line.lstrip())


def step_block(lines, index):
    """The lines of the list item containing `lines[index]`.

    A step runs from the `- ` that opens it to the next line that is no more
    indented than that `- ` — the next sibling step, or the end of the list.
    """
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
    """The value of `key: value` in `block`, or None.

    The optional `- ` matters: a step's first key is written `- name: …`, so a
    pattern anchored to the indentation alone finds every key of a step except
    the one that names it.

    Deliberately literal: a value that is a `${{ }}` expression comes back as
    that text and will not match any path, so an unresolvable build path fails
    the check rather than being waved through.
    """
    pattern = re.compile(rf"^\s*(?:-\s*)?{re.escape(key)}:\s*(.+?)\s*$")
    for line in block:
        match = pattern.match(line)
        if match:
            return INLINE_COMMENT.sub("", match.group(1)).strip().strip("'\"")
    return None


def unity_builds(text):
    """Every Unity build in one workflow, as (name, buildsPath, platform)."""
    lines = text.splitlines()
    builds = []

    for index, line in enumerate(lines):
        if UNITY_BUILDER.match(line):
            block = step_block(lines, index)
            builds.append((
                scalar(block, "name") or "(unnamed step)",
                scalar(block, "buildsPath") or DEFAULT_BUILDS_PATH,
                scalar(block, "targetPlatform"),
            ))

    return builds


def output_root(build):
    """Where unity-builder writes this build — `<buildsPath>/<platform>`."""
    _, builds_path, platform = build
    return f"{builds_path.rstrip('/')}/{platform}"


def apk_paths(text):
    """Every APK path the workflow actually reads, deduplicated and sorted.

    Bare filenames are skipped: only a path with a directory in it makes a
    claim about where the build output lives.

    Whole-line comments are skipped too — YAML comments and shell comments
    alike. A comment reads nothing, and the clearest way to explain this rule
    is to quote the broken path next to the fixed one, which a checker that
    scanned prose would then flag forever.
    """
    lines = [line for line in text.splitlines() if not line.lstrip().startswith("#")]
    return sorted({match for match in APK_PATH.findall("\n".join(lines))
                   if "/" in match})


def workflow_files():
    return sorted(WORKFLOWS.glob("*.yml"))


class RealWorkflowTests(unittest.TestCase):
    """The workflows as they stand in this repo."""

    def setUp(self):
        self.building = [
            (path, path.read_text()) for path in workflow_files()
            if unity_builds(path.read_text())
        ]

    def test_the_unity_build_workflows_are_found(self):
        """Guards against the rest of this suite passing on nothing at all."""
        names = sorted(path.name for path, _ in self.building)
        self.assertEqual(
            names, ["pr-build.yml", "rc-build.yml", "release-build.yml"])

    def test_every_build_declares_a_target_platform(self):
        """Without one there is no knowing what directory Unity appends."""
        for path, text in self.building:
            for build in unity_builds(text):
                with self.subTest(workflow=path.name, step=build[0]):
                    self.assertIsNotNone(build[2])

    def test_every_apk_path_is_under_a_build_output_root(self):
        """The invariant. A buildsPath and the glob that reads it must agree."""
        for path, text in self.building:
            roots = [output_root(build) for build in unity_builds(text)]

            for apk in apk_paths(text):
                with self.subTest(workflow=path.name, path=apk):
                    self.assertTrue(
                        any(apk.startswith(f"{root}/") for root in roots),
                        f"{path.name} reads {apk!r}, but unity-builder writes "
                        f"to {roots} — it appends the targetPlatform directory "
                        f"to buildsPath itself, so this glob matches nothing "
                        f"and the build looks like it produced no APK.")

    def test_every_build_output_root_is_read_by_something(self):
        """The other direction: a build nothing collects is a lost artifact."""
        for path, text in self.building:
            paths = apk_paths(text)

            for build in unity_builds(text):
                root = output_root(build)
                with self.subTest(workflow=path.name, step=build[0]):
                    self.assertTrue(
                        any(apk.startswith(f"{root}/") for apk in paths),
                        f"{path.name} builds into {root!r}, but nothing in the "
                        f"workflow reads it — that APK is built and thrown "
                        f"away. Paths read: {paths}.")


class UnityBuildDetectionTests(unittest.TestCase):
    """The detectors see the real shapes, so a clean result means something."""

    STEPS = """\
jobs:
  build:
    steps:
      - name: Build the device APK
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        env:
          FROGS_ANDROID_PROFILE: device
        with:
          targetPlatform: Android
          buildsPath: build/device
          buildName: multiplying-frogs

      - name: Build the emulator APK
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        with:
          targetPlatform: Android
          buildsPath: build/emulator

      - name: Attach
        run: gh release upload "$TAG" build/device/*.apk
"""

    def test_each_build_is_found_with_its_own_path(self):
        self.assertEqual(
            unity_builds(self.STEPS),
            [("Build the device APK", "build/device", "Android"),
             ("Build the emulator APK", "build/emulator", "Android")])

    def test_a_build_without_a_builds_path_uses_the_default(self):
        text = """\
      - name: Build
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        with:
          targetPlatform: Android
"""
        self.assertEqual(unity_builds(text), [("Build", "build", "Android")])

    def test_a_step_does_not_absorb_its_neighbours_keys(self):
        """The second step's buildsPath must not leak into the first."""
        first, second = unity_builds(self.STEPS)
        self.assertEqual(first[1], "build/device")
        self.assertEqual(second[1], "build/emulator")

    def test_a_workflow_with_no_unity_build_yields_nothing(self):
        self.assertEqual(unity_builds("jobs:\n  test:\n    steps:\n"
                                      "      - run: dotnet test\n"), [])

    def test_the_output_root_appends_the_platform(self):
        self.assertEqual(
            output_root(("Build", "build/device", "Android")),
            "build/device/Android")

    def test_a_trailing_slash_does_not_double_up(self):
        self.assertEqual(
            output_root(("Build", "build/device/", "Android")),
            "build/device/Android")


class ApkPathDetectionTests(unittest.TestCase):
    def test_a_glob_in_a_shell_array_is_seen(self):
        self.assertEqual(
            apk_paths("          assets=(build/device/*.apk build/emulator/*.apk)\n"),
            ["build/device/*.apk", "build/emulator/*.apk"])

    def test_an_artifact_path_is_seen(self):
        self.assertEqual(apk_paths("          path: build/Android/*.apk\n"),
                         ["build/Android/*.apk"])

    def test_a_path_named_in_a_comment_is_not_read(self):
        self.assertEqual(
            apk_paths("          # not build/device/*.apk — see #212\n"
                      "          path: build/device/Android/*.apk\n"),
            ["build/device/Android/*.apk"])

    def test_a_bare_filename_is_not_a_path(self):
        self.assertEqual(apk_paths("of multiplying-frogs.apk on the tablet\n"), [])

    def test_a_repeated_path_is_reported_once(self):
        self.assertEqual(apk_paths("build/Android/*.apk\nbuild/Android/*.apk\n"),
                         ["build/Android/*.apk"])

    def test_a_workflow_with_no_apks_yields_nothing(self):
        self.assertEqual(apk_paths("run: dotnet test\n"), [])


if __name__ == "__main__":
    unittest.main()
