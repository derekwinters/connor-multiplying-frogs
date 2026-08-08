#!/usr/bin/env python3
"""The docs reconciliation gate.

`/docs` is the design contract. A contract that lags the code by three PRs is
not a contract, so a PR that changes code changes the docs in the same PR — or
says, with the `skip-docs` label, that it deliberately did not.

The rule is only real if CI enforces it. This is the decision function:

    docs changed                        → pass
    code only, `skip-docs` label        → pass
    code only, no label                 → fail
    the release PR                      → pass, always

Usage:
    python3 .github/scripts/docs_reconciliation_gate.py \\
        --changed-files changed.txt --pr 123 --head-ref "$HEAD_REF"

Stdlib only.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import PurePosixPath

SKIP_LABEL = "skip-docs"

# The release PR is exempt. It cannot reconcile docs — it contains only the
# version bump and the changelog release-please generated — and it cannot label
# itself, because nobody is driving it. Matched two ways so a change to either
# does not silently start failing every release.
RELEASE_BRANCH_PREFIX = "release-please--branches--"
RELEASE_LABEL = "autorelease: pending"

# Paths whose change counts as documenting the change.
DOCS_DIRECTORIES = ("docs/",)
DOCS_FILES = ("CLAUDE.md", "README.md", "mkdocs.yml")

# A label applied moments after a PR opens would otherwise produce a failing run
# that is already wrong by the time anyone reads it — and a red tick nobody
# should act on is worse than a slow one. Poll the *live* labels for a short
# window before failing.
GRACE_POLL_SECONDS = 10
GRACE_POLL_ATTEMPTS = 6


class Verdict:
    def __init__(self, ok: bool, reason: str) -> None:
        self.ok = ok
        self.reason = reason

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Verdict(ok={self.ok!r}, reason={self.reason!r})"


def is_docs(path: str) -> bool:
    """Does changing this path count as documenting a change?

    Deliberately not "any markdown file". A `SKILL.md` is documentation of a
    skill, but it is also the skill's behaviour — changing it changes what an
    agent does, so it does not excuse the docs.
    """
    normalised = PurePosixPath(path).as_posix()
    return normalised in DOCS_FILES or normalised.startswith(DOCS_DIRECTORIES)


def is_release_pull_request(labels: list[str], head_ref: str) -> bool:
    return head_ref.startswith(RELEASE_BRANCH_PREFIX) or RELEASE_LABEL in labels


def decide(changed_files: list[str], labels: list[str], head_ref: str) -> Verdict:
    """The gate's verdict, from a snapshot. Pure."""
    if is_release_pull_request(labels, head_ref):
        return Verdict(True, "The release PR is exempt from the docs gate.")

    if not changed_files:
        return Verdict(True, "Nothing changed.")

    documented = [path for path in changed_files if is_docs(path)]
    if documented:
        return Verdict(True, f"Docs changed: {', '.join(sorted(documented)[:5])}.")

    if SKIP_LABEL in labels:
        return Verdict(
            True,
            f"No docs changed, but the `{SKIP_LABEL}` label says that is deliberate. "
            "Justify it in the PR's Deviations and Decisions section.",
        )

    return Verdict(
        False,
        "This PR changes code and no documentation.\n\n"
        "  /docs is the design contract, and a contract that lags the code is not one.\n"
        "  Either update the page this change affects, or add the "
        f"`{SKIP_LABEL}` label and say\n"
        "  why in the PR's `## Deviations and Decisions` section.\n\n"
        "  Adding the label re-runs this check; you do not need to push anything.",
    )


def decide_with_grace(
    changed_files: list[str],
    labels: list[str],
    head_ref: str,
    fetch_labels,
    sleep=time.sleep,
) -> Verdict:
    """`decide`, but re-reading live labels before reporting a failure.

    A passing PR never waits. Only a would-be failure polls, because the label
    that fixes it is one click away and often arrives seconds after the PR is
    opened.
    """
    verdict = decide(changed_files, labels, head_ref)
    if verdict.ok:
        return verdict

    for attempt in range(GRACE_POLL_ATTEMPTS):
        sleep(GRACE_POLL_SECONDS)

        try:
            live = fetch_labels()
        except Exception as error:  # noqa: BLE001 - any failure means "unknown"
            # Unknown is not permission. A gate that passes when it cannot read
            # the labels is a gate an outage switches off.
            print(f"Could not read the PR's labels ({error}); retrying.", file=sys.stderr)
            continue

        verdict = decide(changed_files, live, head_ref)
        if verdict.ok:
            return Verdict(
                True,
                f"{verdict.reason} (Seen {(attempt + 1) * GRACE_POLL_SECONDS}s after the "
                "run started, within the grace window.)",
            )

    return verdict


def fetch_labels_from_github(repository: str, pull_number: int):
    """Live labels for a PR, via the `gh` CLI already present on the runner."""

    def fetch() -> list[str]:
        completed = subprocess.run(
            ["gh", "api", f"repos/{repository}/pulls/{pull_number}", "--jq", "[.labels[].name]"],
            check=True,
            capture_output=True,
            text=True,
        )
        return json.loads(completed.stdout or "[]")

    return fetch


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--changed-files", required=True, help="file with one path per line")
    parser.add_argument("--pr", type=int, required=True)
    parser.add_argument("--head-ref", default="")
    parser.add_argument(
        "--repository",
        default=os.environ.get("GITHUB_REPOSITORY", ""),
        help="owner/repo; defaults to $GITHUB_REPOSITORY",
    )
    parser.add_argument(
        "--labels",
        default="",
        help="comma-separated labels from the event payload",
    )
    arguments = parser.parse_args(argv)

    with open(arguments.changed_files, encoding="utf-8") as handle:
        changed = [line.strip() for line in handle if line.strip()]

    labels = [label.strip() for label in arguments.labels.split(",") if label.strip()]

    verdict = decide_with_grace(
        changed,
        labels,
        arguments.head_ref,
        fetch_labels=fetch_labels_from_github(arguments.repository, arguments.pr),
    )

    print(verdict.reason, file=sys.stdout if verdict.ok else sys.stderr)
    return 0 if verdict.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
