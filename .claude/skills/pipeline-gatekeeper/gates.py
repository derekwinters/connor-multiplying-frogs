#!/usr/bin/env python3
"""The gatekeeper's approval gates.

Every gate **refuses and explains**. None of them ever fixes the problem itself,
and a refusal leaves the issue completely untouched — no label change, no
milestone change, nothing.

That posture is the point. Auto-correcting at an approval gate means the
pipeline quietly decides something the owner was in the middle of deciding, and
the first he hears of it is a build he did not expect. A refusal costs one
comment and ten seconds; a wrong auto-fix costs a milestone's worth of
misordered work.

Pure: state in, verdict out. No GitHub I/O.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import re

# Which gates apply to which command.
#
# The presence gate deliberately does NOT run on /milestone: that command is
# what fixes a missing milestone, so gating it on having one would make the fix
# impossible.
GATES = {
    "approve": ["milestone-presence"],
}

VERSION_MILESTONE = re.compile(r"^v(\d+)\.(\d+)(?:\.(\d+))?$")


class Verdict:
    def __init__(self, ok: bool, reason: str, skip_reason: str = "", changes=None) -> None:
        self.ok = ok
        self.reason = reason
        self.skip_reason = skip_reason
        # Always empty on a refusal. Kept as a field so the caller cannot tell
        # the difference between "refused" and "refused and also did something".
        self.changes = list(changes or [])

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Verdict(ok={self.ok!r}, skip_reason={self.skip_reason!r})"


def gates_for(command: str) -> list[str]:
    return list(GATES.get(command, []))


def milestone_order(title: str | None):
    """A sortable key for a milestone title, or None if it is unordered.

    Only the `vMAJOR.MINOR[.PATCH]` scheme is ordered. Anything else — most
    importantly `Direct Involvement Needed` — has no position in the release
    sequence, because it never ships.
    """
    match = VERSION_MILESTONE.match(title or "")
    if not match:
        return None

    major, minor, patch = match.groups()
    return (int(major), int(minor), int(patch or 0))


def milestone_present(issue: dict, comments=None) -> Verdict:
    """`/approve` requires the issue to already have a milestone.

    A `ready-for-work` issue with no milestone is approved work no builder will
    ever select — selection runs against the focus milestone — so it leaves the
    queue silently, which is the worst way for work to disappear.

    Reads **only the milestone field**. Triage sets it; scraping a
    `/milestone v0.1` out of the comment history would make the gate depend on
    what was said rather than what is true, and the two disagree the moment
    anything is edited. The `comments` argument exists to make that explicit and
    is deliberately ignored.
    """
    if issue.get("milestone"):
        return Verdict(True, f"Milestone is {issue['milestone']}.")

    return Verdict(
        False,
        "This issue has no milestone, so approving it would queue work no builder "
        "ever picks up — selection runs against the focus milestone.\n\n"
        "**Which milestone should this be in?** Set it with `/milestone <title>` "
        "and approve again.",
        skip_reason="approve-no-milestone",
    )
