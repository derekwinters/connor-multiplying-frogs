#!/usr/bin/env python3
"""A release APK must be signed with the release key, or it is not attached.

Android will not install an update whose signing certificate differs from the
installed app's. Release APKs used to be signed with a debug key that CI
generated fresh on every runner, so no release could install over the one
before it, and the uninstall that forced lost the game in progress (#442).

`release-build` now hands the release keystore to the Unity build. This checks
that it took: it reads the certificate out of the finished APK with
`apksigner verify --print-certs` and compares its SHA-256 to the expected one,
which the workflow takes from the `ANDROID_KEYSTORE_SHA256` secret.

Two spellings of the same fingerprint meet here. keytool, which is where the
secret's value came from, prints upper case with colons (`49:09:3C:…`);
apksigner prints lower case with no separators. Both are normalised before they
are compared.

An empty or malformed expected fingerprint is a failure, never a skip. An unset
secret must not be the thing that lets an unchecked APK through.

Usage:
    python3 .github/scripts/check_apk_signature.py \\
        --apk build/device/Android/multiplying-frogs-0.6.0.apk \\
        --expected "$ANDROID_KEYSTORE_SHA256"

`--apksigner` names the binary; without it, the newest one under the Android
SDK's `build-tools/` is used (`ANDROID_HOME`, then `ANDROID_SDK_ROOT`), and then
`apksigner` on the PATH.

Exits non-zero, saying why, unless every signer of the APK is the release key.

Stdlib only — the pipeline scripts run on the Python already on the runner.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

SHA256_HEX = re.compile(r"^[0-9a-f]{64}$")

# apksigner prints one of these per signer, alongside SHA-1 and MD5 lines that
# must not be read as the same thing.
SIGNER_SHA256 = re.compile(
    r"^Signer\b.*\bcertificate SHA-256 digest:\s*([0-9A-Fa-f:]+)\s*$")


def normalise_fingerprint(value):
    """A SHA-256 fingerprint as bare lower-case hex, or None if it is not one."""
    if value is None:
        return None

    bare = re.sub(r"[\s:]", "", value).lower()
    return bare if SHA256_HEX.match(bare) else None


def signer_fingerprints(output):
    """Every signer's certificate SHA-256, in the order apksigner listed them."""
    found = []

    for line in output.splitlines():
        match = SIGNER_SHA256.match(line.strip())
        if match:
            found.append(normalise_fingerprint(match.group(1)) or match.group(1))

    return found


def expected_problem(expected):
    """Why the expected fingerprint is unusable, or None if it is usable."""
    if expected is None or not expected.strip():
        return ("The expected fingerprint is empty — is the ANDROID_KEYSTORE_SHA256 "
                "secret set? Without it there is nothing to check the APK against, "
                "and an unchecked APK is not attached.")

    if normalise_fingerprint(expected) is None:
        return (f"The expected fingerprint ({len(expected.strip())} characters) is "
                f"not a SHA-256. ANDROID_KEYSTORE_SHA256 should hold the release "
                f"certificate's SHA-256 as keytool prints it, e.g. 49:09:3C:…")

    return None


def problems(expected, actual):
    """Everything wrong with an APK's signers, in plain sentences. Empty is a pass."""
    unusable = expected_problem(expected)
    if unusable:
        return [unusable]

    if not actual:
        return ["apksigner reported no signer at all, so the APK is unsigned or "
                "unreadable."]

    wanted = normalise_fingerprint(expected)
    return [f"Signer #{number} has certificate SHA-256 {digest}, which is not the "
            f"release key ({wanted}). An APK signed with any other key cannot "
            f"install over the previous release."
            for number, digest in enumerate(actual, start=1) if digest != wanted]


def version_key(directory):
    """`35.0.0` above `9.0.0`: compare as numbers, not as strings."""
    return [int(part) if part.isdigit() else -1
            for part in re.split(r"[.-]", directory.name)]


def find_apksigner(environ):
    """The newest SDK `apksigner`, or the one on the PATH, or None."""
    for variable in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        root = environ.get(variable)
        if not root:
            continue

        candidates = [directory / "apksigner"
                      for directory in Path(root, "build-tools").glob("*")
                      if (directory / "apksigner").is_file()]
        if candidates:
            return max(candidates, key=lambda path: version_key(path.parent))

    on_path = shutil.which("apksigner", path=environ.get("PATH"))
    return Path(on_path) if on_path else None


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--apk", required=True, help="the APK to check")
    parser.add_argument("--expected", required=True,
                        help="the release certificate's SHA-256, in either spelling")
    parser.add_argument("--apksigner", help="the apksigner binary to use")
    arguments = parser.parse_args(argv)

    apk = Path(arguments.apk)
    if not apk.is_file():
        print(f"::error::No APK at {apk}, so there is no signature to check.")
        return 1

    # Unusable whatever the APK holds, so say so before running anything: the
    # message is then about the secret, not the APK.
    unusable = expected_problem(arguments.expected)
    if unusable:
        print(f"::error::{apk.name}: {unusable}")
        return 1

    apksigner = (Path(arguments.apksigner) if arguments.apksigner
                 else find_apksigner(os.environ))
    if apksigner is None or not apksigner.is_file():
        print(f"::error::No apksigner found (looked for {apksigner or 'the SDK build-tools'}),"
              f" so {apk.name}'s signature cannot be checked. It is not attached.")
        return 1

    run = subprocess.run([str(apksigner), "verify", "--print-certs", str(apk)],
                         capture_output=True, text=True)
    print(run.stdout, end="")
    print(run.stderr, end="", file=sys.stderr)

    if run.returncode != 0:
        print(f"::error::{apk.name} does not verify (apksigner exited "
              f"{run.returncode}). It is not attached.")
        return 1

    found = problems(arguments.expected, signer_fingerprints(run.stdout))
    if found:
        for problem in found:
            print(f"::error::{apk.name}: {problem}")
        return 1

    print(f"{apk.name} is signed with the release key.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
