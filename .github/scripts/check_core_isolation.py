#!/usr/bin/env python3
"""Fail if the Core assembly has gained a dependency on Unity.

`noEngineReferences: true` in Frogs.Core.asmdef already makes `using
UnityEngine` a compile error — but only inside Unity, which means the failure
arrives minutes later in CI, and only if someone hasn't flipped the flag. This
check runs anywhere in under a second, with no editor and no .NET, and it
guards the flag itself.

Three things are asserted:

  1. Frogs.Core.asmdef still sets `noEngineReferences: true`.
  2. It references no engine or editor assembly.
  3. No .cs file under Assets/Scripts/Core/ imports UnityEngine or UnityEditor.

Usage:
    python .github/scripts/check_core_isolation.py [--verbose]

Exits 0 when Core is clean, 1 with one line per violation when it is not.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CORE_DIR = REPO_ROOT / "Assets" / "Scripts" / "Core"
CORE_ASMDEF = CORE_DIR / "Frogs.Core.asmdef"

# Assembly names that mean "this is no longer engine-free". Matched against the
# asmdef's references, which may be plain names or "GUID:..." forms — a GUID
# reference to an engine assembly is caught by the noEngineReferences check
# instead, since Unity refuses that combination outright.
FORBIDDEN_REFERENCE_PREFIXES = ("Unity.", "UnityEngine", "UnityEditor")

# `using UnityEngine;`, `using UnityEngine.UI;`, `using static UnityEngine.Mathf;`,
# `global using UnityEngine;` — and the fully-qualified `UnityEngine.Debug.Log`
# that sidesteps a using directive entirely.
IMPORT_PATTERN = re.compile(
    r"^\s*(?:global\s+)?using\s+(?:static\s+)?(UnityEngine|UnityEditor)\b", re.MULTILINE
)
QUALIFIED_PATTERN = re.compile(r"\b(UnityEngine|UnityEditor)\s*\.", re.MULTILINE)


def check_asmdef(problems: list[str]) -> None:
    if not CORE_ASMDEF.exists():
        problems.append(f"{CORE_ASMDEF.relative_to(REPO_ROOT)}: missing")
        return

    try:
        asmdef = json.loads(CORE_ASMDEF.read_text())
    except json.JSONDecodeError as error:
        problems.append(f"{CORE_ASMDEF.relative_to(REPO_ROOT)}: invalid JSON — {error}")
        return

    if asmdef.get("noEngineReferences") is not True:
        problems.append(
            f"{CORE_ASMDEF.relative_to(REPO_ROOT)}: noEngineReferences must be true — "
            "it is what makes `using UnityEngine` a compile error in Core"
        )

    for reference in asmdef.get("references") or []:
        if reference.startswith(FORBIDDEN_REFERENCE_PREFIXES):
            problems.append(
                f"{CORE_ASMDEF.relative_to(REPO_ROOT)}: references {reference!r} — "
                "Core is engine-free; put the adapter in Frogs.Unity instead"
            )


def check_sources(problems: list[str], verbose: bool) -> None:
    sources = sorted(CORE_DIR.rglob("*.cs"))
    if verbose:
        print(f"scanning {len(sources)} source file(s) under {CORE_DIR.relative_to(REPO_ROOT)}")

    for source in sources:
        text = source.read_text(encoding="utf-8", errors="replace")
        relative = source.relative_to(REPO_ROOT)
        for pattern, description in (
            (IMPORT_PATTERN, "imports"),
            (QUALIFIED_PATTERN, "fully-qualifies a type from"),
        ):
            match = pattern.search(text)
            if match:
                line = text[: match.start()].count("\n") + 1
                problems.append(
                    f"{relative}:{line}: {description} {match.group(1)} — "
                    "game logic cannot depend on the engine. If you need engine "
                    "behaviour, declare an interface here and implement it in "
                    "Frogs.Unity (docs/engineering/tech-stack.md)"
                )
                break


def main() -> int:
    verbose = "--verbose" in sys.argv[1:]
    problems: list[str] = []

    check_asmdef(problems)
    check_sources(problems, verbose)

    if problems:
        print("Core is not engine-free:", file=sys.stderr)
        for problem in problems:
            print(f"  {problem}", file=sys.stderr)
        return 1

    print("Core is engine-free.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
