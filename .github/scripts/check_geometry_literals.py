#!/usr/bin/env python3
"""Stop the named-values rule from decaying.

`docs/engineering/tech-stack.md` says every size, offset, margin, duration,
speed, and payout is a named constant or a serialized field. A rule with no
backstop decays: one literal at a time, each individually defensible.

This is the backstop, and it is **deliberately conservative**. It flags
f-suffixed float literals of magnitude 3 or more that appear on a line which
does not give them a name. That is narrower than the rule — see "What it does
not catch" below — because a check with a high false-positive rate is a check
people learn to override, and then it catches nothing at all.

It ratchets against a committed baseline: a file may keep the literals it
already has, and may not gain more. A check that demanded a clean codebase
before it could be switched on is a check that never gets switched on.

    python3 .github/scripts/check_geometry_literals.py
    python3 .github/scripts/check_geometry_literals.py --update-baseline

## What it does not catch

- Literals inside a named declaration's initialiser —
  `var spot = new Vector3(12f, 40f, 0f);` names the vector but not its
  components. Catching this needs a parser, not a regex.
- Integer literals. `32` is a loop bound far more often than it is geometry, and
  the false positives would swamp the findings.
- Magnitudes below 3, which are overwhelmingly arithmetic and halving.

Those are gaps in the *check*, not permission in the *rule*. Review is what
catches them.

Stdlib only.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "Assets" / "Scripts"
BASELINE_PATH = REPO_ROOT / ".github" / "geometry_literals_baseline.txt"

# Below this, a literal is nearly always arithmetic (0, 1), a halving (2), or a
# sentinel — not a measurement.
MINIMUM_MAGNITUDE = 3.0

# An f-suffixed float: 12f, 0.5F, 12.5f. The suffix is what makes this
# low-noise — a bare `32` is usually a count.
LITERAL = re.compile(r"(?<![\w.])(\d+(?:\.\d+)?)[fF](?![\w])")

# A line that gives the value a name: a field, a constant, or a local
# declaration. `float gap = 12f;`, `const float W = 280f;`, `var gap = 12f;`.
# Requires two space-separated identifiers before the `=`, which is what
# distinguishes a declaration from an assignment to something that already
# exists (`transform.position = …` has no type token).
DECLARATION = re.compile(
    r"""
    ^\s*
    (?:\[[^\]]*\]\s*)*                                  # attributes
    (?:(?:public|private|protected|internal|static|readonly|const|new|override
        |sealed|virtual|extern|volatile|unsafe|partial)\s+)*
    [A-Za-z_][\w\.<>,\[\]\?]*                           # the type
    \s+
    [A-Za-z_]\w*                                        # the name
    \s*=
    """,
    re.VERBOSE,
)


class Finding:
    def __init__(self, path: str, line: int, literal: str, text: str) -> None:
        self.path = path
        self.line = line
        self.literal = literal
        self.text = text

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"{self.path}:{self.line}: {self.literal}"


class Verdict:
    def __init__(self, ok: bool, reason: str) -> None:
        self.ok = ok
        self.reason = reason


def _strip_noise(line: str) -> str:
    """Remove string literals and line comments, so neither is searched."""
    without_strings = re.sub(r'"(?:\\.|[^"\\])*"', '""', line)
    without_chars = re.sub(r"'(?:\\.|[^'\\])*'", "''", without_strings)
    return without_chars.split("//")[0]


def find_literals(source: str, path: str) -> list[Finding]:
    """Every flagged literal in one file's text."""
    findings: list[Finding] = []
    in_block_comment = False

    for number, raw in enumerate(source.splitlines(), start=1):
        line = raw

        if in_block_comment:
            if "*/" not in line:
                continue
            line = line.split("*/", 1)[1]
            in_block_comment = False

        if "/*" in line:
            before, _, after = line.partition("/*")
            if "*/" in after:
                line = before + after.split("*/", 1)[1]
            else:
                line = before
                in_block_comment = True

        line = _strip_noise(line)

        if not line.strip():
            continue

        for match in LITERAL.finditer(line):
            if float(match.group(1)) < MINIMUM_MAGNITUDE:
                continue
            if _is_named(line, match.start()):
                continue
            findings.append(Finding(path, number, match.group(0), raw.strip()))

    return findings


def _is_named(line: str, position: int) -> bool:
    """Does a declaration introduce the value at `position`?

    Looks at the statement the literal sits in — from the last `;` or `{`
    before it — rather than at the whole line, so a declaration written inline
    (`void A() { var gap = 12f; }`) is recognised just as well as one on its own
    line.
    """
    start = max(line.rfind(";", 0, position), line.rfind("{", 0, position)) + 1
    return DECLARATION.match(line[start:position]) is not None


def scan(root: Path) -> tuple[dict[str, int], list[Finding]]:
    counts: dict[str, int] = {}
    findings: list[Finding] = []

    for source in sorted(root.rglob("*.cs")):
        relative = source.relative_to(REPO_ROOT).as_posix()
        found = find_literals(source.read_text(encoding="utf-8", errors="replace"), relative)
        if found:
            counts[relative] = len(found)
            findings.extend(found)

    return counts, findings


def read_baseline(path: Path) -> dict[str, int]:
    if not path.is_file():
        return {}

    baseline: dict[str, int] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        name, _, count = line.partition("\t")
        baseline[name.strip()] = int(count.strip())

    return baseline


def write_baseline(path: Path, counts: dict[str, int]) -> None:
    lines = [
        "# Geometry/tuning literals tolerated per file — a ratchet, not a target.",
        "# Counts may go down and the file is regenerated with --update-baseline.",
        "# They may never go up: see .github/scripts/check_geometry_literals.py.",
        "",
    ]
    lines += [f"{name}\t{count}" for name, count in sorted(counts.items())]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def compare(counts: dict[str, int], baseline: dict[str, int]) -> Verdict:
    regressions = []
    improvements = []

    for name, count in sorted(counts.items()):
        allowed = baseline.get(name, 0)
        if count > allowed:
            regressions.append(f"{name}: {count} literal(s), baseline allows {allowed}")

    for name, allowed in sorted(baseline.items()):
        if counts.get(name, 0) < allowed:
            improvements.append(name)

    if regressions:
        return Verdict(
            False,
            "New geometry/tuning literals:\n  " + "\n  ".join(regressions),
        )

    if improvements:
        return Verdict(
            True,
            f"{len(improvements)} file(s) improved. Run with --update-baseline and "
            "commit it, so the new lower count becomes the ceiling.",
        )

    return Verdict(True, "No new geometry/tuning literals.")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--update-baseline",
        action="store_true",
        help="rewrite the baseline from the current tree",
    )
    arguments = parser.parse_args(argv)

    if not SOURCE_ROOT.is_dir():
        print(f"No {SOURCE_ROOT.relative_to(REPO_ROOT)}/ — nothing to check.")
        return 0

    counts, findings = scan(SOURCE_ROOT)

    if arguments.update_baseline:
        write_baseline(BASELINE_PATH, counts)
        total = sum(counts.values())
        print(f"Baseline written: {total} literal(s) across {len(counts)} file(s).")
        return 0

    verdict = compare(counts, read_baseline(BASELINE_PATH))

    if not verdict.ok:
        print(verdict.reason, file=sys.stderr)
        print("", file=sys.stderr)
        for finding in findings:
            print(f"  {finding.path}:{finding.line}: {finding.literal} — {finding.text}",
                  file=sys.stderr)
        print(
            "\nGive each value a name: a `const`, or a `[SerializeField]` so it can be "
            "tuned without a rebuild. See docs/engineering/tech-stack.md.\n"
            "Raising the baseline to make this pass is not a fix, and reads as one in "
            "review.",
            file=sys.stderr,
        )
        return 1

    print(verdict.reason)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
