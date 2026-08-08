#!/usr/bin/env python3
"""Run every skill's unit tests.

`python3 -m unittest discover` cannot find them on its own: discovery only
recurses into directories whose names are valid Python identifiers, and
`.claude` starts with a dot. So this walks `.claude/skills/*/tests/` itself,
putting each skill's own directory on sys.path first so its scripts import by
their plain module name.

Usage:
    python3 .github/scripts/run_skill_tests.py            # every skill
    python3 .github/scripts/run_skill_tests.py release-flow pipeline-gatekeeper

Exits non-zero if any test fails, or if a named skill has no tests — a skill
whose tests silently stopped being run is worse than one with none.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SKILLS_ROOT = REPO_ROOT / ".claude" / "skills"


def skill_directories(names: list[str]) -> list[Path]:
    if names:
        directories = []
        for name in names:
            directory = SKILLS_ROOT / name
            if not directory.is_dir():
                sys.exit(f"No skill named {name!r} under {SKILLS_ROOT.relative_to(REPO_ROOT)}/")
            directories.append(directory)
        return directories

    return sorted(path for path in SKILLS_ROOT.glob("*") if path.is_dir())


def main(argv: list[str]) -> int:
    if not SKILLS_ROOT.is_dir():
        print("No .claude/skills/ directory — nothing to test.")
        return 0

    requested = skill_directories(argv)
    suite = unittest.TestSuite()
    tested: list[str] = []

    for directory in requested:
        if not (directory / "tests").is_dir():
            if argv:
                sys.exit(f"{directory.name} has no tests/ directory.")
            continue

        # The skill's own directory, so `import release_flow` resolves to the
        # script sitting next to the tests rather than to anything else on the
        # path.
        sys.path.insert(0, str(directory))
        suite.addTests(unittest.defaultTestLoader.discover(
            start_dir=str(directory), top_level_dir=str(directory)))
        tested.append(directory.name)

    if not tested:
        print("No skill has a tests/ directory yet.")
        return 0

    print(f"Testing: {', '.join(tested)}")
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
