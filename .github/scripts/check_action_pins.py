#!/usr/bin/env python3
"""Fail any workflow that reaches third-party code without a full SHA pin.

The platform already rejects an unpinned action at run time under this
repository's policy, but that surfaces as a confusing failure partway through a
run — and only on the workflows something happens to trigger. A tag that slips
into a rarely-fired workflow can sit there for months.

This is the review-time signal instead: cheap, offline, and it sees every
workflow whether or not anything runs it.

    python3 .github/scripts/check_action_pins.py

Stdlib only. See docs/engineering/ci-cd.md.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

WORKFLOWS = Path(".github/workflows")

# A `uses:` at the start of its content, so a step's `- uses:` and a job-level
# `uses:` both match, and the word inside prose or a shell string does not.
USES_LINE = re.compile(r"^\s*(?:-\s*)?uses:\s*(\S+)")

FULL_SHA = re.compile(r"^[0-9a-fA-F]{40}$")

# `# v7.0.1`, `# v4`, `# v1.2.3-beta`. The point is that a human can read the
# version without resolving the SHA.
VERSION_COMMENT = re.compile(r"#\s*v\d+(?:\.\d+)*\S*")

# Forms that do not reach a third-party repository at a mutable ref:
#   ./...        a local composite action or reusable workflow, versioned with
#                this repo — pinning it to a SHA would pin the repo to itself
#   docker://    an image reference, governed by the registry not by Actions
LOCAL_PREFIXES = ("./", "docker://")


class Finding:
    def __init__(self, path: str, line: int, kind: str, reference: str,
                 fatal: bool) -> None:
        self.path = path
        self.line = line
        self.kind = kind
        self.reference = reference
        self.fatal = fatal

    def __eq__(self, other) -> bool:  # pragma: no cover - test convenience
        return isinstance(other, Finding) and vars(self) == vars(other)

    def __repr__(self) -> str:
        marker = "error" if self.fatal else "warning"
        return f"{self.path}:{self.line}: {marker}: {self.kind}: {self.reference}"


def check_text(path: str, text: str) -> list:
    """Every pin problem in one workflow file."""
    findings = []

    for number, line in enumerate(text.splitlines(), start=1):
        # A commented-out `uses:` is not a `uses:`.
        if line.lstrip().startswith("#"):
            continue

        match = USES_LINE.match(line)
        if not match:
            continue

        reference = match.group(1)

        if reference.startswith(LOCAL_PREFIXES):
            continue

        _, _, version = reference.partition("@")

        if not FULL_SHA.match(version):
            findings.append(Finding(
                path, number, "unpinned", reference, fatal=True))
            continue

        if not VERSION_COMMENT.search(line):
            # A warning, not a failure. The pin is correct and the build is
            # safe; what is missing is the human-readable part, and failing a
            # PR over a comment would train people to distrust this check.
            findings.append(Finding(
                path, number, "no-version-comment", reference, fatal=False))

    return findings


def check_paths(paths) -> list:
    findings = []
    for path in sorted(paths):
        findings.extend(check_text(str(path), Path(path).read_text()))
    return findings


def exit_code(findings) -> int:
    return 1 if any(finding.fatal for finding in findings) else 0


def main(argv=None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    root = Path(argv[0]) if argv else WORKFLOWS

    paths = sorted(root.glob("*.yml")) + sorted(root.glob("*.yaml"))

    if not paths:
        print(f"No workflows found under {root}.", file=sys.stderr)
        return 1

    findings = check_paths(paths)

    for finding in findings:
        stream = sys.stderr if finding.fatal else sys.stdout
        level = "error" if finding.fatal else "warning"
        print(f"::{level} file={finding.path},line={finding.line}::"
              f"{_message(finding)}", file=stream)

    if not findings:
        print(f"All {len(paths)} workflows are pinned to full commit SHAs.")

    return exit_code(findings)


def _message(finding: Finding) -> str:
    if finding.kind == "unpinned":
        return (f"`{finding.reference}` is not pinned to a full 40-character "
                f"commit SHA. Tags and branches move; a SHA does not. "
                f"Run .github/scripts/resolve_action_pin.sh to resolve it.")

    return (f"`{finding.reference}` is pinned but has no trailing "
            f"`# vX.Y.Z` comment, so nobody can tell which version it is.")


if __name__ == "__main__":
    raise SystemExit(main())
