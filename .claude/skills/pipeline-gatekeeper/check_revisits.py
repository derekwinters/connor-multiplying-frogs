#!/usr/bin/env python3
"""Wake issues whose blockers have cleared.

A **state-derived transition, not a command.** Nothing else would ever wake an
issue set aside only because it was blocked: analysis acts on `ai-triage`, and
the gatekeeper otherwise acts only on comments. Without this, the issue waits
for a human to remember it — which is the same as waiting forever.

Pure: a snapshot in, a list of revisits out. The sweep does the I/O.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import re

# The state a blocked issue is parked in by triage.
BLOCKED_LABEL = "needs-clarification"
TRIAGE_LABEL = "ai-triage"

WIREFRAME_LABEL = "type:wireframe"

# An ordinary blocker is resolved once it is closed OR scheduled: it is going to
# be built, and holding the dependent back until it closes costs a night for
# nothing.
SCHEDULED_LABELS = {"ready-for-work", "in-progress"}

# Structured only. "This is similar to #42" is not a dependency, and treating it
# as one would wake issues at random.
TEXT_BLOCKER = re.compile(r"^\s*blocked\s+by\s*:?\s*#(\d+)\s*$", re.IGNORECASE | re.MULTILINE)


class Revisit:
    def __init__(self, issue_number: int, cleared: list[int]) -> None:
        self.issue_number = issue_number
        self.cleared = cleared
        self.add_labels = [TRIAGE_LABEL]
        self.remove_labels = [BLOCKED_LABEL]

    @property
    def comment(self) -> str:
        named = ", ".join(f"#{number}" for number in self.cleared)
        return (
            f"Everything this was waiting on has cleared ({named}), so it is back in "
            f"the triage queue.\n\n"
            f"_Automatic — nothing was decided here beyond that the blocker is done._"
        )

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Revisit(#{self.issue_number}, cleared={self.cleared})"


def blockers_of(issue: dict) -> list[int]:
    """Every issue this one waits on — text lines **unioned** with native edges.

    Union rather than either-or: an issue can have one recorded natively and
    another still written in prose, and waking it when only half have cleared
    is worse than not waking it at all.
    """
    from_text = {int(number) for number in TEXT_BLOCKER.findall(issue.get("body") or "")}
    from_native = {int(number) for number in (issue.get("native_blockers") or [])}
    return sorted(from_text | from_native)


def is_resolved(blocker: dict) -> bool:
    """Has this blocker stopped blocking?"""
    if blocker.get("state") == "closed" or blocker.get("merged"):
        return True

    labels = set(blocker.get("labels") or [])

    # The carve-out. A wireframe resolves ONLY when closed, because closing it
    # is what "agreed" means — a wireframe marked ready-for-work is still a
    # picture nobody has said yes to. Without this the sweep wakes the dependent
    # every run and triage sets it aside again every run, forever.
    if WIREFRAME_LABEL in labels:
        return False

    return bool(labels & SCHEDULED_LABELS)


def find_revisits(issues: list[dict], snapshot: dict) -> list[Revisit]:
    """Issues eligible to go back to triage, given the blockers' current state.

    `snapshot` maps issue number → that issue's state. A blocker missing from it
    is **not** treated as resolved: not knowing is not the same as knowing it is
    done.
    """
    found: list[Revisit] = []

    for issue in issues:
        labels = set(issue.get("labels") or [])

        # Only issues triage parked as blocked. A `parked` issue was set aside
        # by a decision the owner made, and only the owner un-makes it.
        if BLOCKED_LABEL not in labels:
            continue

        blockers = blockers_of(issue)
        if not blockers:
            continue

        known = [snapshot.get(number) for number in blockers]
        if any(blocker is None for blocker in known):
            continue

        if all(is_resolved(blocker) for blocker in known):
            found.append(Revisit(issue["number"], blockers))

    return found
