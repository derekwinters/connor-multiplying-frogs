"""A release APK must be signed by the release key, and nothing else will do.

Android refuses to install an update whose signing certificate differs from the
installed app's. Every release used to be signed with a debug key generated
fresh on each CI runner, so moving from one release to the next meant
uninstalling — and uninstalling loses the game in progress (issue #442).

Passing the keystore to the build is half of the fix. The other half is
checking that it worked: a keystore input that is silently ignored produces an
APK signed with the debug key, which builds green, installs fine on a clean
device, and only fails when someone tries to update. So
`check_apk_signature.py` reads the certificate back out of the finished APK
with `apksigner verify --print-certs` and compares it to the fingerprint held
in `ANDROID_KEYSTORE_SHA256`.

The expected fingerprint being empty is a failure, not a skip. An unset secret
must never be the thing that lets an unchecked APK through.

See docs/engineering/ci-cd.md and issue #442.
"""

import os
import stat
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from check_apk_signature import (  # noqa: E402
    find_apksigner,
    main,
    normalise_fingerprint,
    problems,
    signer_fingerprints,
)

# keytool's spelling: upper case, colon-separated. This is what the secret holds.
RELEASE_KEYTOOL = ":".join(["49", "09", "3C", "A1"] + ["0F"] * 28)
# apksigner's spelling of the same certificate: lower case, no separators.
RELEASE_APKSIGNER = RELEASE_KEYTOOL.replace(":", "").lower()

DEBUG_APKSIGNER = "ab" * 32


def apksigner_output(*digests):
    """What `apksigner verify --print-certs` prints, one signer per digest."""
    lines = []
    for number, digest in enumerate(digests, start=1):
        lines += [
            f"Signer #{number} certificate DN: CN=Multiplying Frogs",
            f"Signer #{number} certificate SHA-256 digest: {digest}",
            f"Signer #{number} certificate SHA-1 digest: {'cd' * 20}",
            f"Signer #{number} certificate MD5 digest: {'ef' * 16}",
        ]
    return "\n".join(lines) + "\n"


class NormalisationTests(unittest.TestCase):
    def test_keytool_and_apksigner_spellings_are_the_same_fingerprint(self):
        self.assertEqual(normalise_fingerprint(RELEASE_KEYTOOL),
                         normalise_fingerprint(RELEASE_APKSIGNER))

    def test_surrounding_whitespace_is_ignored(self):
        """A secret pasted with a trailing newline is still the same secret."""
        self.assertEqual(normalise_fingerprint(f"  {RELEASE_KEYTOOL}\n"),
                         RELEASE_APKSIGNER)

    def test_a_value_that_is_not_a_sha256_is_rejected(self):
        for value in ("", "49:09:3C", "zz" * 32, RELEASE_APKSIGNER + "00"):
            with self.subTest(value=value):
                self.assertIsNone(normalise_fingerprint(value))


class ParsingTests(unittest.TestCase):
    def test_the_signers_sha256_is_read(self):
        self.assertEqual(signer_fingerprints(apksigner_output(RELEASE_APKSIGNER)),
                         [RELEASE_APKSIGNER])

    def test_every_signer_is_read(self):
        self.assertEqual(
            signer_fingerprints(apksigner_output(RELEASE_APKSIGNER, DEBUG_APKSIGNER)),
            [RELEASE_APKSIGNER, DEBUG_APKSIGNER])

    def test_the_sha1_and_md5_lines_are_not_mistaken_for_it(self):
        self.assertEqual(len(signer_fingerprints(apksigner_output(DEBUG_APKSIGNER))), 1)

    def test_output_with_no_signer_yields_nothing(self):
        self.assertEqual(signer_fingerprints("DOES NOT VERIFY\n"), [])


class ProblemTests(unittest.TestCase):
    def test_the_release_certificate_passes(self):
        self.assertEqual(problems(RELEASE_KEYTOOL, [RELEASE_APKSIGNER]), [])

    def test_a_debug_signed_apk_fails(self):
        """The bug: a release that installs fresh but will not update."""
        self.assertTrue(problems(RELEASE_KEYTOOL, [DEBUG_APKSIGNER]))

    def test_an_empty_expected_fingerprint_fails(self):
        """An unset secret must not wave an unchecked APK through."""
        for expected in ("", "   ", None):
            with self.subTest(expected=expected):
                self.assertTrue(problems(expected, [RELEASE_APKSIGNER]))

    def test_an_expected_fingerprint_that_is_not_a_sha256_fails(self):
        self.assertTrue(problems("49:09:3C", [RELEASE_APKSIGNER]))

    def test_an_apk_with_no_signer_fails(self):
        self.assertTrue(problems(RELEASE_KEYTOOL, []))

    def test_every_signer_must_be_the_release_key(self):
        """A second, foreign signer is not the release key either."""
        self.assertTrue(problems(RELEASE_KEYTOOL, [RELEASE_APKSIGNER, DEBUG_APKSIGNER]))


class FakeApksigner(unittest.TestCase):
    """A stand-in `apksigner`, so `main` runs end to end without the SDK."""

    def setUp(self):
        self.directory = Path(tempfile.mkdtemp())
        self.addCleanup(lambda: __import__("shutil").rmtree(self.directory))
        self.apk = self.directory / "multiplying-frogs-0.6.0.apk"
        self.apk.write_bytes(b"PK")

    def fake(self, stdout, exit_code=0):
        path = self.directory / "apksigner"
        path.write_text(
            "#!/bin/sh\n"
            f"cat <<'EOF'\n{stdout}EOF\n"
            f"exit {exit_code}\n")
        path.chmod(path.stat().st_mode | stat.S_IEXEC)
        return path

    def run_main(self, expected, apksigner):
        return main(["--apk", str(self.apk), "--expected", expected,
                     "--apksigner", str(apksigner)])


class MainTests(FakeApksigner):
    def test_a_release_signed_apk_passes(self):
        apksigner = self.fake(apksigner_output(RELEASE_APKSIGNER))
        self.assertEqual(self.run_main(RELEASE_KEYTOOL, apksigner), 0)

    def test_a_debug_signed_apk_fails(self):
        apksigner = self.fake(apksigner_output(DEBUG_APKSIGNER))
        self.assertEqual(self.run_main(RELEASE_KEYTOOL, apksigner), 1)

    def test_an_empty_expected_fingerprint_fails_even_for_a_good_apk(self):
        apksigner = self.fake(apksigner_output(RELEASE_APKSIGNER))
        self.assertEqual(self.run_main("", apksigner), 1)

    def test_an_apk_that_does_not_verify_fails(self):
        """apksigner's own verdict counts, whatever it printed."""
        apksigner = self.fake(apksigner_output(RELEASE_APKSIGNER), exit_code=1)
        self.assertEqual(self.run_main(RELEASE_KEYTOOL, apksigner), 1)

    def test_a_missing_apk_fails(self):
        apksigner = self.fake(apksigner_output(RELEASE_APKSIGNER))
        self.apk.unlink()
        self.assertEqual(self.run_main(RELEASE_KEYTOOL, apksigner), 1)

    def test_a_missing_apksigner_fails(self):
        self.assertEqual(
            self.run_main(RELEASE_KEYTOOL, self.directory / "no-such-apksigner"), 1)


class FindApksignerTests(unittest.TestCase):
    """The runner has several build-tools versions; take the newest."""

    def setUp(self):
        self.sdk = Path(tempfile.mkdtemp())
        self.addCleanup(lambda: __import__("shutil").rmtree(self.sdk))

    def build_tools(self, version):
        directory = self.sdk / "build-tools" / version
        directory.mkdir(parents=True)
        (directory / "apksigner").write_text("#!/bin/sh\n")
        return directory / "apksigner"

    def test_the_newest_build_tools_wins_by_version_not_by_spelling(self):
        self.build_tools("9.0.0")
        newest = self.build_tools("35.0.0")
        self.build_tools("34.0.0")
        self.assertEqual(find_apksigner({"ANDROID_HOME": str(self.sdk)}), newest)

    def test_android_sdk_root_is_used_when_android_home_is_not_set(self):
        only = self.build_tools("34.0.0")
        self.assertEqual(find_apksigner({"ANDROID_SDK_ROOT": str(self.sdk)}), only)

    def test_no_sdk_finds_nothing(self):
        self.assertIsNone(find_apksigner({"PATH": os.devnull}))


if __name__ == "__main__":
    unittest.main()
