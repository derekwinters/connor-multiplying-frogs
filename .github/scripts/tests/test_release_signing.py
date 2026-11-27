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
- `pr-build` and `rc-build` do not mention the release keystore's secrets at
  all, and none of their keystore inputs comes from a secret. They build
  pull-request branches, and a PR branch must never be able to reach the
  release key. They stay debug-signed, with their `.debug` suffix;
- every Unity build in `pr-build` and `rc-build` is handed the one committed
  debug keystore, `.github/signing/debug.keystore`, so successive debug and RC
  APKs share a certificate and install over each other (#532). Without it
  Unity signs with a debug key generated fresh in each CI container, and no
  debug build installs over the last.

Regex rather than a YAML parse, and stdlib only, for the same reason as
`test_build_output_paths.py`: the pipeline suites run on nothing but the Python
already on the runner.

See docs/engineering/ci-cd.md and issues #442 and #532.
"""

import hashlib
import re
import struct
import unittest
from pathlib import Path

from .test_build_inputs_reach_unity import unity_build_steps
from .test_release_partial_attach import scalar, steps, uncommented

REPO = Path(__file__).resolve().parents[3]
WORKFLOWS = REPO / ".github" / "workflows"
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

# Any of the five release-key secrets.
RELEASE_SECRET = re.compile(r"ANDROID_KEY[A-Z0-9_]*")

# The committed debug keystore every pr-build and rc-build APK is signed with,
# and the values the builder is handed for it. Not secrets: it is a debug key,
# and it only ever signs the `.debug` application ID.
DEBUG_KEYSTORE = REPO / ".github" / "signing" / "debug.keystore"
DEBUG_KEYSTORE_PATH = ".github/signing/debug.keystore"
DEBUG_KEYSTORE_README = DEBUG_KEYSTORE.parent / "README.md"
DEBUG_LITERALS = {
    "androidKeystorePass": "android",
    "androidKeyaliasName": "androiddebugkey",
    "androidKeyaliasPass": "android",
}
STEP_OUTPUT = re.compile(r"^\$\{\{\s*steps\.([A-Za-z0-9_-]+)\.outputs\.([A-Za-z0-9_-]+)\s*\}\}$")

DEVICE_APKS = "build/device/Android"
EMULATOR_APKS = "build/emulator/Android"


def secret_expression(secret):
    return re.compile(rf"^\$\{{\{{\s*secrets\.{secret}\s*\}}\}}$")


def jks_certificates(data):
    """Every certificate in a JKS keystore, as DER bytes, in file order.

    The format is Sun's: a 0xFEEDFEED magic, a version, an entry count, then
    per entry a tag, an alias and a timestamp; a private-key entry (tag 1)
    carries its encrypted key and then its certificate chain, a trusted
    certificate (tag 2) one certificate. Raises ValueError on anything else.
    """
    def take(count):
        nonlocal offset
        if offset + count > len(data):
            raise ValueError("truncated keystore")
        chunk = data[offset:offset + count]
        offset += count
        return chunk

    def u16(): return struct.unpack(">H", take(2))[0]
    def u32(): return struct.unpack(">I", take(4))[0]
    def utf(): return take(u16())

    def certificate():
        utf()  # the certificate type, "X.509"
        return take(u32())

    offset = 0
    if u32() != 0xFEEDFEED:
        raise ValueError("not a JKS keystore")
    if u32() not in (1, 2):
        raise ValueError("unknown JKS version")

    found = []
    for _ in range(u32()):
        tag = u32()
        utf()
        take(8)
        if tag == 1:
            take(u32())
            found.extend(certificate() for _ in range(u32()))
        elif tag == 2:
            found.append(certificate())
        else:
            raise ValueError(f"unknown JKS entry tag {tag}")
    return found


def keytool_fingerprint(der):
    """A certificate's SHA-256 as keytool prints it: `7F:DC:…`."""
    digest = hashlib.sha256(der).hexdigest().upper()
    return ":".join(digest[index:index + 2] for index in range(0, len(digest), 2))


class DebugBuildTests(unittest.TestCase):
    def test_debug_builds_never_reference_the_release_key(self):
        """A PR branch must not be able to reach the release keystore."""
        for path in DEBUG_BUILDS:
            with self.subTest(workflow=path.name):
                found = sorted(set(RELEASE_SECRET.findall(path.read_text())))
                self.assertEqual(
                    found, [],
                    f"{path.name} mentions {found}. It builds branches anyone "
                    f"can push to a PR, so it must stay debug-signed and never "
                    f"name the release keystore's secrets.")

    def test_the_reference_detector_sees_every_release_secret(self):
        """Guards the test above against passing on a pattern that matches nothing."""
        self.assertEqual(
            RELEASE_SECRET.findall(" ".join(KEYSTORE_INPUTS.values()) + " " + FINGERPRINT_SECRET),
            list(KEYSTORE_INPUTS.values()) + [FINGERPRINT_SECRET])

    def test_no_debug_build_takes_a_keystore_input_from_a_secret(self):
        for path in DEBUG_BUILDS:
            for name, block in unity_build_steps(path.read_text()):
                for field in [KEYSTORE_NAME_INPUT, *KEYSTORE_INPUTS]:
                    with self.subTest(workflow=path.name, step=name, input=field):
                        self.assertNotIn("secrets.", scalar(block, field) or "")

    def test_every_debug_build_is_given_the_committed_debug_keystore(self):
        """A build that drops it is signed with a throwaway key, and fails on the tablet."""
        for path in DEBUG_BUILDS:
            text = path.read_text()
            builds = unity_build_steps(text)
            by_id = {scalar(block, "id"): block for block in steps(text).values()}

            with self.subTest(workflow=path.name):
                self.assertTrue(builds, f"No Unity build found in {path.name}.")

            for name, block in builds:
                with self.subTest(workflow=path.name, step=name):
                    self.assertTrue(scalar(block, KEYSTORE_NAME_INPUT),
                                    f"{name!r} names no keystore file for the "
                                    f"builder to decode the keystore into.")

                    for field, value in DEBUG_LITERALS.items():
                        self.assertEqual(scalar(block, field), value,
                                         f"{name!r}: {field} is not the debug "
                                         f"keystore's {value!r}.")

                    encoded = scalar(block, "androidKeystoreBase64")
                    self.assertIsNotNone(
                        encoded, f"{name!r} passes no androidKeystoreBase64, so "
                                 f"Unity signs it with a key made fresh in this "
                                 f"container and it cannot install over the last build.")
                    reference = STEP_OUTPUT.match(encoded)
                    self.assertIsNotNone(
                        reference, f"{name!r}: androidKeystoreBase64 must be a "
                                   f"step output, not {encoded!r}.")

                    source = by_id.get(reference.group(1))
                    self.assertIsNotNone(source, f"No step has id {reference.group(1)!r}.")
                    script = uncommented(source)
                    self.assertIn("base64", script)
                    self.assertIn(DEBUG_KEYSTORE_PATH, script,
                                  "The keystore must come from the committed file.")
                    self.assertIn(reference.group(2), script)


class DebugKeystoreTests(unittest.TestCase):
    def test_the_debug_keystore_is_committed_and_parses(self):
        self.assertTrue(DEBUG_KEYSTORE.is_file(), f"No keystore at {DEBUG_KEYSTORE_PATH}.")
        self.assertEqual(len(jks_certificates(DEBUG_KEYSTORE.read_bytes())), 1)

    def test_its_fingerprint_is_the_one_documented_beside_it(self):
        """So the documented fingerprint cannot drift from the key that signs."""
        der, = jks_certificates(DEBUG_KEYSTORE.read_bytes())
        self.assertIn(keytool_fingerprint(der), DEBUG_KEYSTORE_README.read_text())

    def test_the_parser_rejects_a_file_that_is_not_a_keystore(self):
        """Guards the tests above against a parser that accepts anything."""
        with self.assertRaises(ValueError):
            jks_certificates(b"\x00" * 16)


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
