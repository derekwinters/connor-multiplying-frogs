#!/usr/bin/env python3
"""Check a PR title is a valid Conventional Commit.

This repository squash-merges, so **the PR title becomes the commit message on
main**, which is what release-please parses to decide the next version and write
the changelog. A title it does not recognise is not an error anywhere — the
merge succeeds, and the version silently does not move.

So the title is a required check rather than a matter of discipline.

Usage:
    python3 .github/scripts/lint_pr_title.py --title "feat(frogs): add the pond"
    PR_TITLE="fix: ..." python3 .github/scripts/lint_pr_title.py

Exits 0 when the title conforms, 1 with one line per problem when it does not.
"""

from __future__ import annotations

import argparse
import os
import re
import sys

# The types from CLAUDE.md, and only those. Adding one means editing both, on
# purpose: a type nobody agreed on is a type release-please quietly ignores.
ALLOWED_TYPES = (
    "feat",
    "fix",
    "docs",
    "test",
    "refactor",
    "chore",
    "ci",
    "build",
)

# commitlint's default, and a limit calibrated against real titles in this repo
# rather than picked from a style guide: a tighter 72 rejected three merged
# titles that were simply descriptive. This is here to stop an essay in the
# subject line, not to make people abbreviate.
MAX_LENGTH = 100

HEADER = re.compile(
    r"""
    ^
    (?P<type>[a-zA-Z]+)
    (?:\((?P<scope>[^)]*)\))?
    (?P<breaking>!)?
    :[ ]
    (?P<subject>.*)
    $
    """,
    re.VERBOSE,
)

SCOPE = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def validate(title: str) -> list[str]:
    """Every problem with the title, as messages a human can act on."""
    problems: list[str] = []

    if title is None or title.strip() == "":
        return ["The PR title is empty."]

    if title != title.strip():
        problems.append("The title has leading or trailing whitespace.")

    # Only the leading whitespace is dropped before matching. Stripping the
    # trailing whitespace too would turn "feat:   " into "feat:", reported as
    # "not a Conventional Commit" when the real problem — and the useful
    # message — is that it has no subject.
    title = title.lstrip()

    match = HEADER.match(title)
    if not match:
        return problems + [
            f"'{title}' is not a Conventional Commit. Expected `type(scope): subject`, "
            f"for example `feat(frogs): add the pond`. Allowed types: "
            f"{', '.join(ALLOWED_TYPES)}."
        ]

    kind = match.group("type")
    if kind not in ALLOWED_TYPES:
        hint = ""
        if kind.lower() in ALLOWED_TYPES:
            hint = f" Did you mean '{kind.lower()}'? Types are lowercase."
        problems.append(
            f"'{kind}' is not an allowed type. Use one of: {', '.join(ALLOWED_TYPES)}.{hint}"
        )

    scope = match.group("scope")
    if scope is not None:
        if scope == "":
            problems.append("The scope is empty — write `type: subject` instead of `type(): subject`.")
        elif not SCOPE.match(scope):
            problems.append(
                f"The scope '{scope}' must be lowercase letters, digits, and hyphens."
            )

    subject = match.group("subject")
    if subject.strip() == "":
        problems.append("The title has no subject after the colon.")
    else:
        if subject != subject.lstrip():
            problems.append("There is more than one space after the colon.")
        if subject.rstrip().endswith("."):
            problems.append("The subject should not end with a full stop.")

    trimmed_length = len(title.strip())
    if trimmed_length > MAX_LENGTH:
        problems.append(
            f"The title is {trimmed_length} characters; keep it to {MAX_LENGTH}. A "
            "changelog line that runs off the edge is one nobody reads."
        )

    if match.group("breaking"):
        # Not a failure — a warning is not this script's job — but worth saying,
        # because pre-1.0 a '!' takes the version straight to 1.0.0.
        print(
            "note: '!' marks a breaking change, which releases 1.0.0 from a pre-1.0 "
            "version. See docs/engineering/versioning.md.",
            file=sys.stderr,
        )

    return problems


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--title",
        default=os.environ.get("PR_TITLE"),
        help="the PR title; defaults to $PR_TITLE",
    )
    arguments = parser.parse_args(argv)

    if arguments.title is None:
        sys.exit("No title given: pass --title or set PR_TITLE.")

    problems = validate(arguments.title)

    if problems:
        print(f"The PR title is not a valid Conventional Commit:\n", file=sys.stderr)
        for problem in problems:
            print(f"  - {problem}", file=sys.stderr)
        print(
            "\nThe title becomes the squash commit on main, which release-please reads "
            "to decide the next version. Edit the title and this check re-runs.",
            file=sys.stderr,
        )
        return 1

    print(f"'{arguments.title}' is a valid Conventional Commit.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
