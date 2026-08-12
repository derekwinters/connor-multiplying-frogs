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
import sys
from pathlib import Path

# One recognizer for `Blocked by #N`, and one union of it with the native
# edges, shared with the skill that documents the format. Six copies of the
# pattern used to drift silently: this sweep would stop seeing a line the queue
# selector still refused to build past, and the issue would never wake. See
# #147.
_BLOCKERS_SKILL = Path(__file__).resolve().parents[1] / "issue-blockers"
if str(_BLOCKERS_SKILL) not in sys.path:
    sys.path.insert(0, str(_BLOCKERS_SKILL))

from blocker_refs import blockers_of  # noqa: E402

# `is_triage_author` — one more shared recognizer, same reason as the one
# above: this module needs to tell its own past comments apart from anyone
# else's without keeping a second copy of that judgment.
_TRIAGE_SKILL = Path(__file__).resolve().parents[1] / "triage-issue"
if str(_TRIAGE_SKILL) not in sys.path:
    sys.path.insert(0, str(_TRIAGE_SKILL))

from triage_repair import is_triage_author  # noqa: E402

# The state a blocked issue is parked in by triage.
BLOCKED_LABEL = "needs-clarification"
TRIAGE_LABEL = "ai-triage"

WIREFRAME_LABEL = "type:wireframe"

# An ordinary blocker is resolved once it is closed OR scheduled: it is going to
# be built, and holding the dependent back until it closes costs a night for
# nothing.
SCHEDULED_LABELS = {"ready-for-work", "in-progress"}

# `Revisit.comment`'s own opening line, read back out of the issue's comment
# history so a repeat sweep can recognize its own earlier action. The blocker
# numbers are parsed out of what got POSTED, not recomputed, because the write
# that matters is the one that already happened.
_REVISIT_COMMENT = re.compile(
    r"everything this was waiting on has cleared \(([^)]*)\)", re.IGNORECASE)
_ISSUE_REF = re.compile(r"#(\d+)")


def _already_revisited_for(comments, blockers) -> bool:
    """Was this exact blocker set already actioned by a past revisit?

    A closed blocker stays resolved forever, so without this check, an issue
    that lands back on `needs-clarification` for any *other* reason — an
    open design question triage raised, say — reads as "blocker cleared,
    wake it up" again on every subsequent sweep. That is what turned #296
    into an `ai-triage` / `needs-clarification` loop, once per sweep,
    indefinitely: the wake-up fired, triage answered with a real but
    unrelated question, and the still-closed blocker fired the same wake-up
    again next time round.

    Only a triage-authored comment counts — a human typing similar words by
    hand is not the automatic wake-up repeating itself.
    """
    wanted = set(blockers)

    for comment in comments or []:
        if not is_triage_author(comment.get("author")):
            continue

        match = _REVISIT_COMMENT.search(comment.get("body") or "")
        if not match:
            continue

        named = {int(n) for n in _ISSUE_REF.findall(match.group(1))}
        if wanted <= named:
            return True

    return False


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

        if not all(is_resolved(blocker) for blocker in known):
            continue

        if _already_revisited_for(issue.get("comments"), blockers):
            continue

        found.append(Revisit(issue["number"], blockers))

    return found
