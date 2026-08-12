#!/usr/bin/env python3
"""What the nightly builder builds tonight, and in what order.

Eligibility and ordering are decided here, deterministically, rather than by
the model. Two reasons. A queue you can reproduce is a queue you can argue with
— "why did it not pick #47" has an answer you can read. And a model asked to
pick work will pick the interesting work, which is not the same as the work
that unblocks the most other work.

    echo '{"issues": [...], "pulls": [...], "focus": "v0.0.1"}' | python3 select_queue.py

Stdlib only, no network: fetching is the caller's job.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

# One recognizer for `Blocked by #N` and one for `Depends on: #N`, and one
# union of the first with the native edges, shared with the skill that
# documents both formats. A copy here that drifted from the sweep's would
# refuse to build an issue nothing would ever wake, with nothing reporting a
# reason. See #147.
_BLOCKERS_SKILL = Path(__file__).resolve().parents[1] / "issue-blockers"
if str(_BLOCKERS_SKILL) not in sys.path:
    sys.path.insert(0, str(_BLOCKERS_SKILL))

from blocker_refs import blockers_of, text_depends_on  # noqa: E402

READY_LABEL = "ready-for-work"
PARKED_LABEL = "parked"
EPIC_LABEL = "type:epic"

# What the builder takes on in one night when nobody has said otherwise. Small
# on purpose: three issues that land beat six that half-land, and the cap is
# the only thing standing between a quiet night and thirty open PRs.
DEFAULT_CAP = 3

# GitHub's closing keywords. An issue with an open PR that *closes* it is
# already being worked; one merely referenced by a PR is not.
CLOSING_KEYWORD = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(\d+)\b",
    re.IGNORECASE,
)


def _numbers(pattern, body) -> set:
    return {int(number) for number in pattern.findall(body or "")}


def depends_on(issue: dict) -> list:
    """Soft ordering hints. Never gates — see `docs/engineering/issue-pipeline.md`."""
    return sorted(text_depends_on(issue.get("body")))


def closed_by_open_pr(issue_number: int, pulls) -> bool:
    """Is an open PR already claiming to close this issue?"""
    return any(
        pull.get("state", "open") == "open"
        and issue_number in _numbers(CLOSING_KEYWORD, pull.get("body"))
        for pull in pulls or []
    )


def _blocker_resolved(blocker) -> bool:
    """A blocker stops blocking once it is closed or merged."""
    if blocker is None:
        # Unknown is not resolved. Not knowing whether the thing you depend on
        # is done is exactly the case where starting is expensive.
        return False

    return blocker.get("state") == "closed" or bool(blocker.get("merged"))


def is_eligible(issue: dict, focus, pulls, snapshot) -> bool:
    labels = set(issue.get("labels") or [])

    if issue.get("state", "open") != "open":
        return False

    if READY_LABEL not in labels or PARKED_LABEL in labels:
        return False

    # An epic is a container. Its children are the work, and they carry their
    # own labels; building the epic itself would mean building all of them at
    # once in a single PR.
    if EPIC_LABEL in labels:
        return False

    # No milestone is not the focus milestone. An issue marked ready with no
    # milestone is a broken invariant, and reconcile flags it rather than the
    # builder guessing which milestone was meant.
    if issue.get("milestone") != focus:
        return False

    if closed_by_open_pr(issue["number"], pulls):
        return False

    return all(
        _blocker_resolved(snapshot.get(number)) for number in blockers_of(issue)
    )


def order(issues) -> list:
    """Dependencies first, then issue number.

    A stable topological sort: at each step take the lowest-numbered issue
    whose in-queue dependencies have already been placed. Both hard blockers
    and soft `Depends on:` lines order — a blocker that is still open would
    have failed eligibility, but one closed this evening still says which of
    two ready issues was meant to come first.
    """
    remaining = {issue["number"]: issue for issue in issues}

    edges = {
        number: {
            other for other in blockers_of(issue) + depends_on(issue)
            if other in remaining
        }
        for number, issue in remaining.items()
    }

    ordered = []
    placed = set()

    while remaining:
        ready = sorted(
            number for number, needs in edges.items()
            if number in remaining and needs <= placed
        )

        if not ready:
            # A cycle. Reconcile flags those; the builder must not hang or
            # silently drop the issues, so it falls back to number order.
            ready = sorted(remaining)

        # One at a time, re-checking after each. Placing the whole ready batch
        # would scatter a dependent away from the thing it depends on — with a
        # cap of 2, `#11, #12` instead of `#11, #10` — and the cap is far more
        # useful when it cuts a coherent chain than an arbitrary slice.
        number = ready[0]
        ordered.append(remaining.pop(number))
        placed.add(number)

    return ordered


def process(data: dict) -> dict:
    focus = data.get("focus")
    pulls = data.get("pulls") or []
    cap = data.get("cap")
    cap = DEFAULT_CAP if cap is None else int(cap)

    # Snapshot keys arrive as ints in-process and as strings through JSON.
    raw_snapshot = data.get("snapshot") or {}
    snapshot = {int(number): value for number, value in raw_snapshot.items()}

    eligible = [
        issue for issue in data.get("issues") or []
        if is_eligible(issue, focus, pulls, snapshot)
    ]

    # Order first, then cap. Capping first would take the three lowest-numbered
    # issues and could leave a dependent in without the thing it depends on.
    chosen = order(eligible)[:cap]

    queue = [
        {"number": issue["number"], "milestone": issue.get("milestone")}
        for issue in chosen
    ]

    return {"queue": queue, "count": len(queue)}


def main(argv=None) -> int:
    json.dump(process(json.load(sys.stdin)), sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
