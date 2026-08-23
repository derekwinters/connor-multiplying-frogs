"""A PR's two debug APKs must be tellable apart, in the file and in the name.

`pr-build` builds the app twice — the `device` profile for the tablet and the
`emulator` profile for a desktop emulator, so a change can be *opened* before
it merges rather than after it ships. Two builds of the same commit differ only
in architecture, which is not visible from a filename, from an artifact name,
or from a download. So the only place the difference can be guaranteed is the
workflow, and it has to be guaranteed there rather than noticed later.

It has already gone wrong twice, in both directions:

- `v0.1.0` shipped an "emulator" APK that was really the ARM64 device build,
  because the profile never reached Unity (#218, #252). Both builds ran, both
  went green, both files existed, and the wrong one installed on someone
  else's machine days later.
- `v0.1.0` also tagged a release with no APK at all, because a `buildsPath`
  and the glob reading it drifted apart (#212).

So this asserts the shape that makes both impossible in `pr-build`:

- both builds name a profile on Unity's command line, and the two profiles
  differ — a build that asks for nothing gets whatever the project is set to,
  which is the device profile by another name;
- the two write to different paths under different names, so neither the files
  nor the uploads can be picked up for each other;
- each upload reads exactly one build's output, and an upload whose name says
  a profile is fed by the build that asked for it;
- the licence gate covers both builds and both uploads, so a fork with no
  secrets still gets a green, APK-less run;
- the job summary names both APKs, because the artifacts list is the one place
  nobody looks;
- and none of it strays into release-only work. A PR build is not a release.

Regex rather than a YAML parse, and stdlib only, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the
Python already on the runner.

See docs/engineering/ci-cd.md and issues #280, #252.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"
PR_BUILD = WORKFLOWS / "pr-build.yml"

# The step that decides whether anything runs at all, and the condition every
# build and upload has to carry.
LICENCE_GATE = "steps.licence.outputs.present == 'true'"

PROFILE_FLAG = "-frogsAndroidProfile"
PROFILES = ("device", "emulator")

UNITY_BUILDER = re.compile(r"^\s*(?:-\s*)?uses:\s*game-ci/unity-builder@")
UPLOAD_ARTIFACT = re.compile(r"^\s*(?:-\s*)?uses:\s*actions/upload-artifact@")

LIST_ITEM = re.compile(r"^(\s*)-\s")
INLINE_COMMENT = re.compile(r"\s+#.*$")

# A step's `if:`, read without `scalar`'s quote-stripping: the condition itself
# ends in a quote — `… == 'true'` — and stripping it would compare a mangled
# string against the real one and pass whenever both were mangled the same way.
CONDITION = re.compile(r"^\s*(?:-\s*)?if:\s*(.+?)\s*$")

DEFAULT_BUILDS_PATH = "build"

# Work that belongs to a release and never to a PR.
RELEASE_ONLY = ("check_release_apks.py", "gh release ")


def indent_of(line):
    return len(line) - len(line.lstrip())


def step_blocks(text):
    """Every step in the workflow, as a list of blocks of lines.

    A step runs from the `- ` that opens it to the next line no more indented
    than that `- ` — the next sibling step, or the end of the list.
    """
    lines = text.splitlines()
    blocks = []

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

        blocks.append(lines[index:end])

    return blocks


def scalar(block, key):
    """The value of `key: value` in `block`, or None.

    The optional `- ` matters: a step's first key is written `- name: …`, so a
    pattern anchored to the indentation alone finds every key of a step except
    the one that names it.
    """
    pattern = re.compile(rf"^\s*(?:-\s*)?{re.escape(key)}:\s*(.+?)\s*$")
    for line in block:
        match = pattern.match(line)
        if match:
            return INLINE_COMMENT.sub("", match.group(1)).strip().strip("'\"")
    return None


def condition(block):
    """The step's `if:` condition verbatim, or None."""
    for line in block:
        match = CONDITION.match(line)
        if match:
            return match.group(1)
    return None


def uncommented(block):
    """The block's lines as one string, with whole-line comments dropped.

    A rule explained in a comment is not a rule the run obeys, and every step
    in this workflow is commented — including with the shapes that used to be
    wrong.
    """
    return "\n".join(line for line in block if not line.lstrip().startswith("#"))


def matching(text, pattern):
    return [block for block in step_blocks(text)
            if any(pattern.match(line) for line in block)]


def profile_of(block):
    """The value after `-frogsAndroidProfile` in the step's customParameters."""
    value = scalar(block, "customParameters")
    if not value:
        return None

    words = value.split()
    for index, word in enumerate(words):
        if word == PROFILE_FLAG:
            following = words[index + 1] if index + 1 < len(words) else None
            return None if following is None or following.startswith("-") else following

    return None


def output_root(block):
    """Where unity-builder writes this build — `<buildsPath>/<platform>`.

    The platform segment is the action's, not ours: it appends the
    `targetPlatform` directory whether or not `buildsPath` was set (#212).
    """
    builds_path = scalar(block, "buildsPath") or DEFAULT_BUILDS_PATH
    return f"{builds_path.rstrip('/')}/{scalar(block, 'targetPlatform')}"


class PrBuildProfileTests(unittest.TestCase):
    def setUp(self):
        self.text = PR_BUILD.read_text()
        self.builds = matching(self.text, UNITY_BUILDER)
        self.uploads = matching(self.text, UPLOAD_ARTIFACT)

    def test_the_pr_build_runs_both_profiles(self):
        """Guards against every other test here passing on nothing."""
        self.assertEqual(
            len(self.builds), 2,
            f"pr-build.yml runs {len(self.builds)} Unity build(s). It builds "
            f"two: the device APK for the tablet and the emulator APK, so a "
            f"PR can be opened without a tablet in hand.")

    def test_each_build_names_its_profile_on_unitys_command_line(self):
        """A build that asks for nothing gets whatever the project is set to.

        Which is the device profile by another name — so two builds that both
        say nothing are two device builds, one of them mislabelled (#218).
        """
        asked = [profile_of(block) for block in self.builds]
        self.assertEqual(
            sorted(asked, key=str), sorted(PROFILES),
            f"pr-build.yml's builds ask for profiles {asked}. Each must pass "
            f"`{PROFILE_FLAG} <profile>` in customParameters — that is the "
            f"only thing that reaches the editor inside the build container "
            f"(#218), and the two must not ask for the same thing.")

    def test_the_two_builds_write_to_different_places(self):
        """A shared directory is how the wrong file gets picked up by a glob."""
        roots = [output_root(block) for block in self.builds]
        self.assertEqual(
            len(set(roots)), len(roots),
            f"Both builds write to {roots}. The two APKs differ only in "
            f"architecture, which no glob can see — give each build its own "
            f"buildsPath.")

    def test_the_two_builds_have_different_names(self):
        """Same name, different architecture, is an APK nobody can identify."""
        names = [scalar(block, "buildName") for block in self.builds]
        self.assertEqual(
            len(set(names)), len(names),
            f"Both builds produce {names}. Once downloaded, the filename is "
            f"all anyone has to tell the emulator APK from the device one.")

    def test_each_upload_reads_exactly_one_builds_output(self):
        """An upload that reads both roots, or neither, is an upload lying."""
        roots = [output_root(block) for block in self.builds]

        for block in self.uploads:
            path = scalar(block, "path")
            with self.subTest(artifact=scalar(block, "name")):
                under = [root for root in roots if path.startswith(f"{root}/")]
                self.assertEqual(
                    len(under), 1,
                    f"The upload reading {path!r} does not sit under exactly "
                    f"one of {roots}. unity-builder appends the targetPlatform "
                    f"directory itself, so a path missing that segment matches "
                    f"nothing (#212) — and a path matching both profiles "
                    f"uploads whichever the glob reaches first.")

    def test_every_build_is_uploaded_under_its_own_artifact_name(self):
        """Two builds, two artifacts, two names. One each."""
        names = [scalar(block, "name") for block in self.uploads]
        self.assertEqual(
            len(set(names)), len(names),
            f"Two uploads share an artifact name: {names}.")

        for build in self.builds:
            root = output_root(build)
            reading = [block for block in self.uploads
                       if (scalar(block, "path") or "").startswith(f"{root}/")]
            with self.subTest(profile=profile_of(build)):
                self.assertEqual(
                    len(reading), 1,
                    f"{len(reading)} upload(s) read {root!r}. A build nothing "
                    f"uploads is an APK built and thrown away; two uploads of "
                    f"one build is the same APK under two names.")

    def test_an_artifact_that_names_a_profile_is_that_profile(self):
        """The #252 failure, made impossible rather than merely unlikely.

        An artifact called `…-emulator` that holds the ARM64 build is worse
        than no emulator APK: it fails at install time, on someone else's
        machine, with nothing pointing at the build that produced it.
        """
        for block in self.uploads:
            name = scalar(block, "name")
            path = scalar(block, "path")

            for profile in PROFILES:
                if not name.endswith(f"-{profile}"):
                    continue

                source = [build for build in self.builds
                          if path.startswith(f"{output_root(build)}/")]

                with self.subTest(artifact=name):
                    self.assertEqual(
                        [profile_of(build) for build in source], [profile],
                        f"The artifact named {name!r} reads {path!r}, which is "
                        f"not the output of the {profile!r} build. That is "
                        f"exactly #252: an emulator-named artifact holding the "
                        f"device APK.")

    def test_the_licence_gate_covers_both_builds_and_both_uploads(self):
        """A fork with no secrets gets a green, APK-less run — still."""
        for block in self.builds + self.uploads:
            with self.subTest(step=scalar(block, "name")):
                self.assertEqual(
                    condition(block), LICENCE_GATE,
                    f"This step is not gated on `{LICENCE_GATE}`, so a fork "
                    f"with no UNITY_LICENSE fails instead of warning and "
                    f"skipping. The APK is a convenience; failing every PR "
                    f"over a missing secret trains everyone to ignore a red "
                    f"check.")

    def test_the_summary_names_both_apks(self):
        """The artifacts list is the one place nobody thinks to open."""
        summaries = [block for block in step_blocks(self.text)
                     if "GITHUB_STEP_SUMMARY" in uncommented(block)]
        self.assertTrue(summaries, "Nothing writes a job summary.")

        written = "\n".join(uncommented(block) for block in summaries)

        for build in self.builds:
            name = scalar(build, "buildName")
            with self.subTest(profile=profile_of(build)):
                self.assertIn(
                    name, written,
                    f"The job summary never names {name!r}, so the PR does not "
                    f"say what it produced without someone opening the "
                    f"artifacts list.")

    def test_the_summary_never_claims_an_apk_that_was_not_built(self):
        """A named APK that does not exist sends someone to an empty artifacts
        list, which reads as a broken download rather than a failed build."""
        summaries = [block for block in step_blocks(self.text)
                     if "GITHUB_STEP_SUMMARY" in uncommented(block)]
        written = "\n".join(uncommented(block) for block in summaries)

        for build in self.builds:
            identifier = scalar(build, "id")
            with self.subTest(profile=profile_of(build)):
                self.assertIsNotNone(
                    identifier,
                    f"The {profile_of(build)!r} build has no `id:`, so nothing "
                    f"downstream can ask whether it produced anything.")
                self.assertIn(
                    f"steps.{identifier}.outcome", written,
                    f"The job summary names the {profile_of(build)!r} APK "
                    f"without reading `steps.{identifier}.outcome`, so it says "
                    f"the APK is there whether or not the build made one.")

    def test_a_pr_build_does_no_release_work(self):
        """`pr-build` in isolation: no release check, no upload, no /VERSION."""
        body = "\n".join(uncommented(block) for block in step_blocks(self.text))

        for fragment in RELEASE_ONLY:
            with self.subTest(fragment=fragment):
                self.assertNotIn(
                    fragment, body,
                    f"pr-build.yml runs {fragment!r}. That belongs to "
                    f"release-build: a PR build is a convenience artifact, not "
                    f"an asset on a tag.")

        self.assertNotRegex(
            body, r">>?\s*\"?VERSION\b",
            "pr-build.yml redirects into /VERSION. That file is "
            "release-please's alone — see docs/engineering/versioning.md.")


class HelperTests(unittest.TestCase):
    """The detectors see the real shapes, so a clean result means something."""

    STEPS = """\
jobs:
  build:
    steps:
      - name: Build the device APK
        if: steps.licence.outputs.present == 'true'
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        with:
          targetPlatform: Android
          buildsPath: build/device
          buildName: multiplying-frogs-0.2.0-abc1234
          customParameters: -frogsVersionCode 412 -frogsAndroidProfile device

      - name: Build the emulator APK
        uses: game-ci/unity-builder@d829bfc # v5.0.0
        with:
          targetPlatform: Android
          buildsPath: build/emulator
          customParameters: -frogsVersionCode 412 -frogsAndroidProfile emulator

      - name: Upload the emulator APK
        uses: actions/upload-artifact@043fb46 # v7.0.1
        with:
          name: debug-apk-0.2.0-abc1234-emulator
          path: build/emulator/Android/*.apk
"""

    def test_each_kind_of_step_is_found(self):
        self.assertEqual(
            [scalar(block, "name") for block in matching(self.STEPS, UNITY_BUILDER)],
            ["Build the device APK", "Build the emulator APK"])
        self.assertEqual(
            [scalar(block, "name") for block in matching(self.STEPS, UPLOAD_ARTIFACT)],
            ["Upload the emulator APK"])

    def test_a_step_does_not_absorb_its_neighbours_keys(self):
        first, second = matching(self.STEPS, UNITY_BUILDER)
        self.assertEqual(profile_of(first), "device")
        self.assertEqual(profile_of(second), "emulator")
        self.assertEqual(scalar(first, "buildName"), "multiplying-frogs-0.2.0-abc1234")
        self.assertIsNone(scalar(second, "buildName"))

    def test_the_output_root_appends_the_platform(self):
        first, _ = matching(self.STEPS, UNITY_BUILDER)
        self.assertEqual(output_root(first), "build/device/Android")

    def test_a_build_without_a_builds_path_uses_the_default(self):
        block = ["      - uses: game-ci/unity-builder@d829bfc",
                 "        with:",
                 "          targetPlatform: Android"]
        self.assertEqual(output_root(block), "build/Android")

    def test_a_build_with_no_profile_flag_reports_none(self):
        self.assertIsNone(profile_of(
            ["          customParameters: -frogsVersionCode 412"]))

    def test_a_profile_flag_with_no_value_reports_none(self):
        self.assertIsNone(profile_of(
            ["          customParameters: -frogsAndroidProfile -frogsVersionCode 412"]))

    def test_the_licence_condition_is_read_from_the_step(self):
        """Verbatim: the condition's own trailing quote must survive."""
        first, second = matching(self.STEPS, UNITY_BUILDER)
        self.assertEqual(condition(first), LICENCE_GATE)
        self.assertTrue(condition(first).endswith("'true'"))
        self.assertIsNone(condition(second))


if __name__ == "__main__":
    unittest.main()
