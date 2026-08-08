#!/usr/bin/env python3
"""Run every Python unit test in the repo — the skills and the CI scripts.

`python3 -m unittest discover` cannot find them on its own: discovery only
recurses into directories whose names are valid Python identifiers, and both
`.claude` and `.github` start with a dot. So this walks the two known test
locations itself, putting each suite's own directory on sys.path first so its
scripts import by their plain module name.

    .claude/skills/<skill>/tests/
    .github/scripts/tests/

Usage:
    python3 .github/scripts/run_python_tests.py                 # everything
    python3 .github/scripts/run_python_tests.py release-flow    # named skills
    python3 .github/scripts/run_python_tests.py scripts         # the CI scripts

Exits non-zero if any test fails, or if a named suite has no tests — a suite
that silently stopped being run is worse than one that never existed.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SKILLS_ROOT = REPO_ROOT / ".claude" / "skills"
SCRIPTS_ROOT = REPO_ROOT / ".github" / "scripts"

# The name that selects the CI scripts rather than a skill.
SCRIPTS_SUITE = "scripts"


def all_suites() -> list[tuple[str, Path]]:
    """(name, directory) for every suite that has a tests/ subdirectory."""
    suites: list[tuple[str, Path]] = []

    if (SCRIPTS_ROOT / "tests").is_dir():
        suites.append((SCRIPTS_SUITE, SCRIPTS_ROOT))

    if SKILLS_ROOT.is_dir():
        for directory in sorted(path for path in SKILLS_ROOT.glob("*") if path.is_dir()):
            if (directory / "tests").is_dir():
                suites.append((directory.name, directory))

    return suites


def named_suites(names: list[str]) -> list[tuple[str, Path]]:
    resolved: list[tuple[str, Path]] = []

    for name in names:
        directory = SCRIPTS_ROOT if name == SCRIPTS_SUITE else SKILLS_ROOT / name

        if not directory.is_dir():
            sys.exit(f"No suite named {name!r} — expected a skill under .claude/skills/, "
                     f"or '{SCRIPTS_SUITE}'.")
        if not (directory / "tests").is_dir():
            sys.exit(f"{name} has no tests/ directory.")

        resolved.append((name, directory))

    return resolved


def forget_modules_from(directory: Path) -> None:
    """Drop every module imported from `directory` out of sys.modules.

    Every suite lives in a package called `tests`, so without this the second
    suite's `tests` package resolves to the first one's cached module and its
    tests silently never run — reported as one loader error among many passes,
    which is exactly the kind of green nobody looks at twice.
    """
    for name, module in list(sys.modules.items()):
        origin = getattr(module, "__file__", None)
        if origin and Path(origin).is_relative_to(directory):
            del sys.modules[name]


def main(argv: list[str]) -> int:
    suites = named_suites(argv) if argv else all_suites()

    if not suites:
        print("Nothing has a tests/ directory yet.")
        return 0

    print(f"Testing: {', '.join(name for name, _ in suites)}")
    failed: list[str] = []

    for name, directory in suites:
        print(f"\n=== {name} ===")

        # The suite's own directory first, so `import lint_pr_title` resolves to
        # the script sitting beside the tests rather than anything else.
        sys.path.insert(0, str(directory))
        try:
            discovered = unittest.defaultTestLoader.discover(
                start_dir=str(directory), top_level_dir=str(directory))
            result = unittest.TextTestRunner(verbosity=2).run(discovered)
            if not result.wasSuccessful():
                failed.append(name)
        finally:
            sys.path.remove(str(directory))
            forget_modules_from(directory)

    if failed:
        print(f"\nFAILED: {', '.join(failed)}")
        return 1

    print(f"\nAll suites passed: {', '.join(name for name, _ in suites)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
