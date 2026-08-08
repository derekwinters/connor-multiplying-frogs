#!/usr/bin/env python3
"""Checks for driving a release-please release to a tagged, released state.

Every function here is pure: it takes a snapshot of GitHub state and returns a
verdict. Nothing in this file talks to the network, and nothing in it mutates
anything — gathering the snapshot is the caller's job (see SKILL.md), and
mutating the release PR is release-please's.

Usage:

    python3 release_flow.py regenerated --snapshot snapshot.json
    python3 release_flow.py parked      --snapshot snapshot.json
    python3 release_flow.py verify      --snapshot snapshot.json
    python3 release_flow.py title       --version 0.1.0

`--snapshot -` reads from stdin. Each check exits 0 when it passes and 1 when
it does not, printing a one-line reason either way.
"""

from __future__ import annotations

import argparse
import json
import sys

RELEASE_BRANCH_PREFIX = "release-please--branches--"
PENDING_LABEL = "autorelease: pending"
TAGGED_LABEL = "autorelease: tagged"

# A check run that GitHub is holding until someone with write access approves it
# — the state a first-time or fork contributor's workflow run sits in.
PARKED_STATES = {"action_required", "waiting", "queued_pending_approval"}


class Verdict:
    """A pass/fail with a reason a human can act on."""

    def __init__(self, ok: bool, reason: str) -> None:
        self.ok = ok
        self.reason = reason

    def __eq__(self, other: object) -> bool:
        return (
            isinstance(other, Verdict)
            and self.ok == other.ok
            and self.reason == other.reason
        )

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Verdict(ok={self.ok!r}, reason={self.reason!r})"


def find_release_pr(pull_requests: list[dict]) -> dict | None:
    """The open release PR, or None.

    Matched on the head branch rather than the title, because the title is
    configurable and has changed once already.
    """
    for pull_request in pull_requests:
        head_ref = (pull_request.get("head") or {}).get("ref", "")
        if head_ref.startswith(RELEASE_BRANCH_PREFIX):
            return pull_request
    return None


def check_regenerated(snapshot: dict) -> Verdict:
    """Gotcha 1: is the release PR up to date with main?

    release-please rewrites its PR on every push to main, but the run takes a
    minute and can fail. Merging a stale release PR ships a changelog and a
    version that silently omit whatever landed after it was last written — and
    nothing downstream notices, because the release itself succeeds.
    """
    pull_request = snapshot.get("pull_request")
    if not pull_request:
        return Verdict(False, "No open release PR — release-please has nothing to release.")

    main_sha = snapshot.get("main_sha")
    if not main_sha:
        return Verdict(False, "Snapshot has no main_sha, so staleness cannot be checked.")

    base_sha = (pull_request.get("base") or {}).get("sha")
    if not base_sha:
        return Verdict(False, "Snapshot has no pull_request.base.sha.")

    if base_sha != main_sha:
        return Verdict(
            False,
            f"The release PR is based on {base_sha[:7]} but main is at {main_sha[:7]}. "
            "Wait for release-please to rewrite it, or re-run the workflow.",
        )

    return Verdict(True, f"The release PR is current with main at {main_sha[:7]}.")


def check_parked_runs(snapshot: dict) -> Verdict:
    """Gotcha 2: are any of the PR's checks waiting on a human?

    A parked run is not a failure and not a success — it simply never finishes.
    Merging past it means merging without the checks having run at all, which
    looks identical to merging with them green.

    The owner approves them in the GitHub UI. This never approves them itself:
    approving your own parked run defeats the point of it being parked.
    """
    parked = [
        run
        for run in snapshot.get("check_runs") or []
        if run.get("status") in PARKED_STATES or run.get("conclusion") in PARKED_STATES
    ]

    if parked:
        names = ", ".join(sorted(run.get("name", "?") for run in parked))
        return Verdict(
            False,
            f"{len(parked)} check run(s) waiting for approval: {names}. "
            "Ask Derek to approve them in the PR's Checks tab, then re-run this.",
        )

    return Verdict(True, "No check runs are waiting for approval.")


def squash_title(version: str) -> str:
    """The squash-merge subject for a release PR."""
    return f"chore(main): release {version}"


def check_released(snapshot: dict) -> Verdict:
    """After the merge: did the tag, the release, and the label all appear?

    All three, because each can happen without the others. A tag with no
    release is a release nobody can download; a release with no
    `autorelease: tagged` label is one release-please will try to make again.
    """
    version = snapshot.get("version")
    if not version:
        return Verdict(False, "Snapshot has no version to verify.")

    tag = f"v{version}"
    missing = []

    if tag not in set(snapshot.get("tags") or []):
        missing.append(f"the {tag} tag")

    releases = snapshot.get("releases") or []
    published = [
        release
        for release in releases
        if release.get("tag_name") == tag and not release.get("draft", False)
    ]
    if not published:
        drafted = any(release.get("tag_name") == tag for release in releases)
        missing.append(f"a published GitHub Release for {tag}" + (" (found a draft)" if drafted else ""))

    labels = set(snapshot.get("pull_request_labels") or [])
    if TAGGED_LABEL not in labels:
        missing.append(f"the `{TAGGED_LABEL}` label on the release PR")

    if missing:
        return Verdict(False, f"Release {tag} is incomplete — missing " + "; ".join(missing) + ".")

    return Verdict(True, f"Release {tag} is tagged, published, and labelled.")


def load_snapshot(path: str) -> dict:
    if path == "-":
        return json.load(sys.stdin)
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command", required=True)

    for name, help_text in (
        ("regenerated", "check the release PR is current with main"),
        ("parked", "check no check runs are waiting for approval"),
        ("verify", "check the tag, release, and label all exist"),
    ):
        subcommand = subcommands.add_parser(name, help=help_text)
        subcommand.add_argument("--snapshot", required=True, help="JSON file, or - for stdin")

    title = subcommands.add_parser("title", help="print the squash-merge title")
    title.add_argument("--version", required=True)

    arguments = parser.parse_args(argv)

    if arguments.command == "title":
        print(squash_title(arguments.version))
        return 0

    check = {
        "regenerated": check_regenerated,
        "parked": check_parked_runs,
        "verify": check_released,
    }[arguments.command]

    verdict = check(load_snapshot(arguments.snapshot))
    print(verdict.reason, file=sys.stdout if verdict.ok else sys.stderr)
    return 0 if verdict.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
