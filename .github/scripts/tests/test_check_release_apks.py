"""The two release APKs have to be two different builds.

`release-build` runs Unity twice, once per profile in
docs/engineering/tech-stack.md: ARM64/IL2CPP for the tablet, x86_64/Mono for a
desktop emulator. Nothing checked that the two invocations produced anything
different, and for `v0.1.0` they did not — the profile never reached Unity, so
both assets were the same ARM64 file under two names. An x86_64 emulator
refuses to install an ARM64-only APK, so the failure surfaced on someone
else's machine rather than in CI (issue #218).

This is the check that would have caught it: not "did two builds run" but "are
the two things they produced actually the two things the docs promise".

See docs/engineering/ci-cd.md and issue #218.
"""

import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from check_release_apks import inspect_apk, problems  # noqa: E402

# Enough of a Unity Android APK for the check to have an opinion about it.
DEVICE_LIBS = [
    "lib/arm64-v8a/libunity.so",
    "lib/arm64-v8a/libil2cpp.so",
    "lib/arm64-v8a/libmain.so",
]
EMULATOR_LIBS = [
    "lib/x86_64/libunity.so",
    "lib/x86_64/libmonobdwgc-2.0.so",
    "lib/x86_64/libmain.so",
]


class ApkFixture(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)

    def apk(self, name, entries, filler="x"):
        path = Path(self.directory.name) / name
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("AndroidManifest.xml", filler)
            for entry in entries:
                archive.writestr(entry, filler)
        return path

    def good_pair(self):
        return (inspect_apk(self.apk("device.apk", DEVICE_LIBS)),
                inspect_apk(self.apk("emulator.apk", EMULATOR_LIBS, filler="y")))


class InspectionTests(ApkFixture):
    def test_the_abis_present_are_read_from_the_lib_directories(self):
        apk = inspect_apk(self.apk("device.apk", DEVICE_LIBS))
        self.assertEqual(apk.abis, {"arm64-v8a"})

    def test_an_apk_with_two_abis_reports_both(self):
        apk = inspect_apk(self.apk("fat.apk", DEVICE_LIBS + EMULATOR_LIBS))
        self.assertEqual(apk.abis, {"arm64-v8a", "x86_64"})

    def test_il2cpp_is_recognised(self):
        self.assertTrue(inspect_apk(self.apk("d.apk", DEVICE_LIBS)).il2cpp)

    def test_a_mono_build_has_no_il2cpp(self):
        self.assertFalse(inspect_apk(self.apk("e.apk", EMULATOR_LIBS)).il2cpp)

    def test_two_apks_with_the_same_bytes_have_the_same_digest(self):
        first = inspect_apk(self.apk("one.apk", DEVICE_LIBS))
        second = inspect_apk(self.apk("two.apk", DEVICE_LIBS))
        self.assertEqual(first.digest, second.digest)


class ProblemTests(ApkFixture):
    def test_the_two_profiles_built_as_specified_pass(self):
        self.assertEqual(problems(*self.good_pair()), [])

    def test_byte_identical_assets_are_the_bug_itself(self):
        """v0.1.0: two names, one file. The headline failure."""
        device = inspect_apk(self.apk("device.apk", DEVICE_LIBS))
        emulator = inspect_apk(self.apk("emulator.apk", DEVICE_LIBS))

        found = problems(device, emulator)

        self.assertTrue(found)
        self.assertTrue(any("identical" in problem for problem in found),
                        f"expected the duplication to be named; got {found}")

    def test_an_emulator_apk_built_for_the_tablet_is_caught(self):
        device = inspect_apk(self.apk("device.apk", DEVICE_LIBS))
        emulator = inspect_apk(self.apk("emulator.apk", DEVICE_LIBS, filler="y"))

        found = problems(device, emulator)

        self.assertTrue(any("x86_64" in problem for problem in found),
                        f"expected the missing ABI to be named; got {found}")

    def test_a_device_apk_built_for_the_emulator_is_caught(self):
        device = inspect_apk(self.apk("device.apk", EMULATOR_LIBS))
        emulator = inspect_apk(self.apk("emulator.apk", EMULATOR_LIBS, filler="y"))

        found = problems(device, emulator)

        self.assertTrue(any("arm64-v8a" in problem for problem in found),
                        f"expected the missing ABI to be named; got {found}")

    def test_an_emulator_apk_carrying_the_tablet_abi_too_is_caught(self):
        """Not just "has x86_64" — the profile says x86_64 and nothing else."""
        device = inspect_apk(self.apk("device.apk", DEVICE_LIBS))
        emulator = inspect_apk(
            self.apk("emulator.apk", DEVICE_LIBS + EMULATOR_LIBS, filler="y"))

        self.assertTrue(problems(device, emulator))

    def test_a_device_apk_without_il2cpp_is_caught(self):
        """The profile is an architecture *and* a scripting backend."""
        device = inspect_apk(
            self.apk("device.apk", ["lib/arm64-v8a/libunity.so"]))
        emulator = inspect_apk(self.apk("emulator.apk", EMULATOR_LIBS, filler="y"))

        found = problems(device, emulator)

        self.assertTrue(any("IL2CPP" in problem for problem in found),
                        f"expected the backend to be named; got {found}")

    def test_an_emulator_apk_built_with_il2cpp_is_caught(self):
        device = inspect_apk(self.apk("device.apk", DEVICE_LIBS))
        emulator = inspect_apk(
            self.apk("emulator.apk",
                     ["lib/x86_64/libunity.so", "lib/x86_64/libil2cpp.so"],
                     filler="y"))

        found = problems(device, emulator)

        self.assertTrue(any("IL2CPP" in problem for problem in found),
                        f"expected the backend to be named; got {found}")

    def test_an_apk_with_no_native_libraries_at_all_is_caught(self):
        """A build that produced no player is not a build that passed."""
        device = inspect_apk(self.apk("device.apk", []))
        emulator = inspect_apk(self.apk("emulator.apk", EMULATOR_LIBS, filler="y"))

        self.assertTrue(problems(device, emulator))

    def test_every_problem_names_the_file_it_is_about(self):
        """Two APKs in one message, so the report has to say which."""
        device = inspect_apk(self.apk("device.apk", EMULATOR_LIBS))
        emulator = inspect_apk(self.apk("emulator.apk", DEVICE_LIBS, filler="y"))

        for problem in problems(device, emulator):
            self.assertTrue("device.apk" in problem or "emulator.apk" in problem,
                            f"{problem!r} does not say which APK it is about")


if __name__ == "__main__":
    unittest.main()
