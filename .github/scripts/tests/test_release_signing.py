"""Release APKs are signed with the release key; nothing else can reach it.

Android refuses to install an update whose signing certificate differs from the
installed app's. Until #442 every release was signed with a debug key made
fresh on each CI runner, so no release could install over the one before, and
the uninstall that forced lost the game in progress.

So the shape this asserts:

- both Unity builds in `release-build` are handed the release keystore, from
  the repository secrets;
- each APK's certificate is checked against `ANDROID_KEYSTORE_SHA256` *before*
  the attach step, by `check_apk_signature.py`, and an APK that fails is not
  attached — and the run still ends red, after the attach;
- `pr-build` and `rc-build` do not mention the keystore secrets at all. They
  build pull-request branches, and a PR branch must never be able to reach the
  release key. They stay debug-signed, with their `.debug` suffix.

Regex rather than a YAML parse, and stdlib only, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the Python
already on the runner.

See docs/engineering/ci-cd.md and issue #442.
"""

import re
import unittest
from pathlib import Path

from .test_build_inputs_reach_unity import unity_build_steps
from .test_release_partial_attach import scalar, steps, uncommented

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"
RELEASE_BUILD = WORKFLOWS / "release-build.yml"
DEBUG_BUILDS = [WORKFLOWS / "pr-build.yml", WORKFLOWS / "rc-build.yml"]

# The action's input names, as game-ci/unity-builder's action.yml spells them
# at the pinned version, mapped to the secret each must come from.
KEYSTORE_INPUTS = {
    "androidKeystoreBase64": "ANDROID_KEYSTORE_BASE64",
    "androidKeystorePass": "ANDROID_KEYSTORE_PASSWORD",
    "androidKeyaliasName": "ANDROID_KEY_ALIAS",
    "androidKeyaliasPass": "ANDROID_KEY_ALIAS_PASSWORD",
}
KEYSTORE_NAME_INPUT = "androidKeystoreName"

FINGERPRINT_SECRET = "ANDROID_KEYSTORE_SHA256"
CHECK_SCRIPT = "check_apk_signature.py"

# Any of the five secrets, or any keystore input to the builder.
KEY_REFERENCE = re.compile(r"ANDROID_KEY[A-Z0-9_]*|androidKey(?:store|alias)[A-Za-z]*")

DEVICE_APKS = "build/device/Android"
EMULATOR_APKS = "build/emulator/Android"


def secret_expression(secret):
    return re.compile(rf"^\$\{{\{{\s*secrets\.{secret}\s*\}}\}}$")


class DebugBuildTests(unittest.TestCase):
    def test_debug_builds_never_reference_the_release_key(self):
        """A PR branch must not be able to reach the release keystore."""
        for path in DEBUG_BUILDS:
            with self.subTest(workflow=path.name):
                found = sorted(set(KEY_REFERENCE.findall(path.read_text())))
                self.assertEqual(
                    found, [],
                    f"{path.name} mentions {found}. It builds branches anyone "
                    f"can push to a PR, so it must stay debug-signed and never "
                    f"name the release keystore's secrets or inputs.")

    def test_the_reference_detector_sees_both_shapes(self):
        """Guards the test above against passing on a pattern that matches nothing."""
        self.assertEqual(
            KEY_REFERENCE.findall("androidKeystorePass: ${{ secrets.ANDROID_KEY_ALIAS }}"),
            ["androidKeystorePass", "ANDROID_KEY_ALIAS"])


class ReleaseBuildTests(unittest.TestCase):
    def setUp(self):
        self.text = RELEASE_BUILD.read_text()
        self.builds = unity_build_steps(self.text)
        self.steps = steps(self.text)
        self.order = list(self.steps)

    def test_both_release_builds_are_found(self):
        """Guards against every other test here passing on nothing."""
        self.assertEqual(len(self.builds), 2)

    def test_every_release_build_is_given_the_keystore_from_the_secrets(self):
        for name, block in self.builds:
            for field, secret in KEYSTORE_INPUTS.items():
                with self.subTest(step=name, input=field):
                    value = scalar(block, field)
                    self.assertIsNotNone(
                        value, f"{name!r} passes no {field}, so Unity signs it "
                               f"with a throwaway debug key.")
                    self.assertRegex(value, secret_expression(secret))

            with self.subTest(step=name, input=KEYSTORE_NAME_INPUT):
                self.assertTrue(scalar(block, KEYSTORE_NAME_INPUT),
                                f"{name!r} names no keystore file for the "
                                f"builder to decode the keystore into.")

    def checking_steps(self):
        return [name for name, block in self.steps.items()
                if CHECK_SCRIPT in uncommented(block)]

    def the_check(self):
        checking = self.checking_steps()
        self.assertEqual(len(checking), 1, f"Expected one step running {CHECK_SCRIPT}.")
        return checking[0]

    def test_each_apk_is_checked_against_the_release_fingerprint(self):
        block = self.steps[self.the_check()]
        script = uncommented(block)
        self.assertIn(f"secrets.{FINGERPRINT_SECRET}", script)
        self.assertIn(DEVICE_APKS, script)
        self.assertIn(EMULATOR_APKS, script)
        self.assertIsNone(
            scalar(block, "continue-on-error"),
            "The signature check must not be allowed to fail quietly.")

    def test_the_check_runs_after_both_builds_and_before_the_attach(self):
        attach = next(name for name, block in self.steps.items()
                      if "gh release upload" in uncommented(block))
        check = self.the_check()

        for name, _ in self.builds:
            with self.subTest(build=name):
                self.assertLess(self.order.index(name), self.order.index(check))

        self.assertLess(self.order.index(check), self.order.index(attach),
                        "An APK attached before its signature is checked is "
                        "an APK nobody checked.")

    def test_a_rejected_apk_is_moved_out_of_the_attach_globs(self):
        """Not attached, rather than attached with a warning."""
        script = uncommented(self.steps[self.the_check()])
        self.assertRegex(script, r"\bmv\b",
                         "Nothing moves a rejected APK out of the directories "
                         "the attach step globs, so it would still be attached.")

    def test_a_rejected_apk_still_fails_the_run_after_the_attach(self):
        check = self.the_check()
        check_id = scalar(self.steps[check], "id")
        self.assertIsNotNone(check_id, f"{check!r} has no `id:` to read later.")

        attach = next(name for name, block in self.steps.items()
                      if "gh release upload" in uncommented(block))

        failing = [name for name, block in self.steps.items()
                   if f"steps.{check_id}.outputs" in uncommented(block)
                   and "exit 1" in uncommented(block)]

        self.assertTrue(failing, "Nothing fails the run when an APK was rejected.")
        for name in failing:
            with self.subTest(step=name):
                self.assertGreater(self.order.index(name), self.order.index(attach))


if __name__ == "__main__":
    unittest.main()
