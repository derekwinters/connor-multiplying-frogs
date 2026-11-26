#!/usr/bin/env python3
"""Fail any `game-ci/unity-builder` step that would look up the game-ci CLI.

unity-builder v6 does not build by itself: it first installs the separate
`game-ci/cli` binary. Its `cliVersion` input defaults to `latest`, and turning
`latest` into a real tag is a call to the GitHub releases API — made without a
token unless `GITHUB_TOKEN` is in the step's environment. GitHub-hosted runners
share IP addresses, so that anonymous call runs into the 60-per-hour limit other
repositories use up, and the build dies before Unity starts (issue #480).

So every unity-builder step must:

- set `cliVersion` to a fixed release tag — not missing, empty, or `latest`;
- pass `GITHUB_TOKEN` in its step `env:`, so any GitHub API call the action
  makes is authenticated.

    python3 .github/scripts/check_unity_builder_cli.py

Stdlib only, and indentation rather than a YAML parse, for the same reason as
`check_action_pins.py`: it runs on nothing but the Python already on the
runner. See docs/engineering/ci-cd.md.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

WORKFLOWS = Path(".github/workflows")

UNITY_BUILDER = re.compile(r"^\s*(?:-\s*)?uses:\s*game-ci/unity-builder@")

LIST_ITEM = re.compile(r"^(\s*)-\s")

KEY_LINE = re.compile(r"^(\s*)(?:-\s+)?([A-Za-z0-9_-]+):\s*(.*?)\s*$")

INLINE_COMMENT = re.compile(r"\s+#.*$")

# `${{ github.token }}` or `${{ secrets.GITHUB_TOKEN }}` — the same token.
TOKEN_VALUE = re.compile(
    r"^\$\{\{\s*(?:github\.token|secrets\.GITHUB_TOKEN)\s*\}\}$")


class Finding:
    def __init__(self, path: str, line: int, kind: str, step: str) -> None:
        self.path = path
        self.line = line
        self.kind = kind
        self.step = step

    def __eq__(self, other) -> bool:  # pragma: no cover - test convenience
        return isinstance(other, Finding) and vars(self) == vars(other)

    def __repr__(self) -> str:
        return f"{self.path}:{self.line}: {self.kind}: {self.step}"


def _indent(line: str) -> int:
    return len(line) - len(line.lstrip())


def _step_bounds(lines, index):
    """(start, end) of the list item containing `lines[index]`."""
    start = index
    while start >= 0 and not LIST_ITEM.match(lines[start]):
        start -= 1
    if start < 0:
        return index, index + 1

    opening = len(LIST_ITEM.match(lines[start]).group(1))

    end = start + 1
    while end < len(lines):
        line = lines[end]
        if line.strip() and not line.lstrip().startswith("#") \
                and _indent(line) <= opening:
            break
        end += 1

    return start, end


def _steps(text):
    """Every unity-builder step as (uses-line number, block of lines)."""
    lines = text.splitlines()
    found = []
    for index, line in enumerate(lines):
        if line.lstrip().startswith("#"):
            continue
        if UNITY_BUILDER.match(line):
            start, end = _step_bounds(lines, index)
            found.append((index + 1, lines[start:end]))
    return found


def unity_builder_steps(text):
    """The line blocks of every unity-builder step in one workflow."""
    return [block for _, block in _steps(text)]


def _mapping(block, key):
    """The `key: value` pairs directly under `key:` in a step block."""
    values = {}
    parent = None
    for line in block:
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        match = KEY_LINE.match(line)
        if parent is None:
            if match and match.group(2) == key and not match.group(3):
                parent = _indent(line)
            continue
        if _indent(line) <= parent:
            break
        if match:
            value = INLINE_COMMENT.sub("", match.group(3)).strip()
            values[match.group(2)] = value.strip("'\"")
    return values


def with_input(block, name):
    """The value of a `with:` input in a step block, or None."""
    return _mapping(block, "with").get(name)


def check_text(path: str, text: str) -> list:
    findings = []
    for number, block in _steps(text):
        name = next((m.group(3) for m in map(KEY_LINE.match, block)
                     if m and m.group(2) == "name"), "(unnamed step)")

        version = with_input(block, "cliVersion")
        if not version:
            findings.append(Finding(path, number, "cli-version-missing", name))
        elif version == "latest":
            findings.append(Finding(path, number, "cli-version-latest", name))

        token = _mapping(block, "env").get("GITHUB_TOKEN", "")
        if not TOKEN_VALUE.match(token):
            findings.append(Finding(path, number, "token-missing", name))

    return findings


def check_paths(paths) -> list:
    findings = []
    for path in sorted(paths):
        findings.extend(check_text(str(path), Path(path).read_text()))
    return findings


def exit_code(findings) -> int:
    return 1 if findings else 0


MESSAGES = {
    "cli-version-missing": (
        "sets no `cliVersion`, so the action asks the GitHub API for the "
        "latest game-ci CLI release on every run. Set it to a fixed tag."),
    "cli-version-latest": (
        "sets `cliVersion: latest`, which is a GitHub API lookup on every "
        "run. Set it to a fixed tag."),
    "token-missing": (
        "does not pass `GITHUB_TOKEN: ${{ github.token }}` in its `env:`, so "
        "any GitHub API call the action makes is anonymous and rate-limited."),
}


def main(argv=None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    root = Path(argv[0]) if argv else WORKFLOWS

    paths = sorted(root.glob("*.yml")) + sorted(root.glob("*.yaml"))
    if not paths:
        print(f"No workflows found under {root}.", file=sys.stderr)
        return 1

    findings = check_paths(paths)
    for finding in findings:
        print(f"::error file={finding.path},line={finding.line}::"
              f"unity-builder step `{finding.step}` {MESSAGES[finding.kind]} "
              f"See docs/engineering/ci-cd.md.", file=sys.stderr)

    if not findings:
        steps = sum(len(_steps(Path(p).read_text())) for p in paths)
        print(f"All {steps} unity-builder steps pin the game-ci CLI and "
              f"pass GITHUB_TOKEN.")

    return exit_code(findings)


if __name__ == "__main__":
    raise SystemExit(main())
