#!/usr/bin/env python3
"""The two release APKs must be the two builds the docs promise.

`release-build` runs Unity twice — ARM64/IL2CPP for the tablet, x86_64/IL2CPP
for a desktop emulator (docs/engineering/tech-stack.md). Running it twice is not
the same as getting two builds: for `v0.1.0` the profile never reached Unity,
both invocations built the same thing, and the release went out with two
byte-identical assets under different names (issue #218).

Nothing noticed, because everything that could have noticed was upstream of the
APK. Both builds went green, both files existed, both were the right size for
an APK. The only place the truth was visible was inside the archive.

So this looks inside. An APK's native libraries sit under `lib/<abi>/`, and
those ABI names are Android's, not Unity's — `arm64-v8a` and `x86_64` mean the
same thing in every APK ever built. IL2CPP additionally ships `libil2cpp.so`,
and a Mono build does not, so the scripting backend is readable from the same
listing.

Both profiles are IL2CPP: Unity has no Mono for 64-bit Android, so a build that
comes back without `libil2cpp.so` was built for a pairing Unity silently
reduces to no architecture at all (issue #282). The ABIs are what tell the two
profiles apart.

Usage:
    python3 .github/scripts/check_release_apks.py \\
        --device build/device/Android/multiplying-frogs-0.2.0.apk \\
        --emulator build/emulator/Android/multiplying-frogs-0.2.0-emulator.apk

Exits non-zero, naming every problem, if the pair is not what it should be.
Failing here is the point: an emulator APK that will not install is a failure
that otherwise happens on someone else's machine, days later, with no build log
anywhere near it.

Stdlib only — the pipeline scripts run on the Python already on the runner.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
import zipfile
from collections import namedtuple
from pathlib import Path

# Android's own directory names, and Unity's own library name. Neither is
# something this project chooses, which is what makes them safe to assert on.
DEVICE_ABI = "arm64-v8a"
EMULATOR_ABI = "x86_64"
IL2CPP_LIBRARY = "libil2cpp.so"

Apk = namedtuple("Apk", "name abis il2cpp digest")


def inspect_apk(path):
    """What an APK is, as far as the profile is concerned.

    An APK is a zip, and the entry names carry everything needed here — no
    need for `aapt`, which is not on the runner.
    """
    path = Path(path)

    with zipfile.ZipFile(path) as archive:
        entries = archive.namelist()

    abis = {entry.split("/")[1] for entry in entries
            if entry.startswith("lib/") and entry.count("/") >= 2}
    il2cpp = any(entry.endswith(f"/{IL2CPP_LIBRARY}") for entry in entries)

    return Apk(path.name, abis, il2cpp, sha256(path))


def sha256(path):
    digest = hashlib.sha256()

    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)

    return digest.hexdigest()


def problems(device, emulator):
    """Everything wrong with this pair, in plain sentences. Empty is a pass."""
    found = []

    # First, because it explains every other line that follows it. Two profiles
    # cannot produce one file, so if they did, neither profile was applied.
    if device.digest == emulator.digest:
        found.append(
            f"{device.name} and {emulator.name} are byte-for-byte identical "
            f"(sha256 {device.digest[:16]}…). Two build profiles cannot "
            f"produce one file, so neither profile reached Unity.")

    found.extend(profile_problems(device, "device", DEVICE_ABI))
    found.extend(profile_problems(emulator, "emulator", EMULATOR_ABI))

    return found


def profile_problems(apk, profile, abi):
    """Whether one APK is the build its profile asked for.

    Every profile is IL2CPP, so the backend check is the same for both: 64-bit
    Android has no Mono, and an APK without `libil2cpp.so` was built for a
    pairing Unity cannot build (#282).
    """
    found = []

    if apk.abis != {abi}:
        listed = ", ".join(sorted(apk.abis)) if apk.abis else "no native libraries at all"
        found.append(
            f"{apk.name} is the '{profile}' profile, so it must contain {abi} "
            f"and nothing else. It has {listed}.")

    if not apk.il2cpp:
        found.append(
            f"{apk.name} is the '{profile}' profile, which is IL2CPP, but it "
            f"has no {IL2CPP_LIBRARY} — it was built with Mono, which Unity "
            f"cannot build for 64-bit Android.")

    return found


def describe(apk):
    backend = "IL2CPP" if apk.il2cpp else "Mono"
    abis = ", ".join(sorted(apk.abis)) or "none"
    return f"{apk.name}: {abis}, {backend}, sha256 {apk.digest[:16]}…"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--device", required=True,
                        help="the ARM64/IL2CPP APK, for the tablet")
    parser.add_argument("--emulator", required=True,
                        help="the x86_64/IL2CPP APK, for a desktop emulator")
    arguments = parser.parse_args(argv)

    for path in (arguments.device, arguments.emulator):
        if not Path(path).is_file():
            print(f"::error::No APK at {path}. The build that should have "
                  f"written it did not.")
            return 1

    device = inspect_apk(arguments.device)
    emulator = inspect_apk(arguments.emulator)

    print(describe(device))
    print(describe(emulator))

    found = problems(device, emulator)

    if not found:
        print("Both profiles produced the build they asked for.")
        return 0

    for problem in found:
        print(f"::error::{problem}")

    print("\nThe device and emulator APKs are not the two builds "
          "docs/engineering/tech-stack.md specifies. Refusing to attach them: "
          "an emulator APK that will not install is a failure that otherwise "
          "surfaces on someone else's machine. See issue #218.")

    return 1


if __name__ == "__main__":
    sys.exit(main())
