#!/usr/bin/env python3
"""Work out what a parsed action actually writes.

The final label set, the acknowledgement, and whether to leave the 👀
watermark. Pure — keeping it so is what makes the whole gatekeeper testable
without a live repo.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

WATERMARK = "eyes"

TRIAGE = "ai-triage"
PENDING = "pending-approval"
NEEDS_CLARIFICATION = "needs-clarification"
READY = "ready-for-work"
IN_PROGRESS = "in-progress"
PARKED = "parked"

# Exactly one of these is on an open issue at a time, so a state change
# *replaces* rather than adds. Everything else — area:*, type:*, skip-docs — is
# untouched, because a state change that dropped them would lose the whole
# triage decision.
STATE_LABELS = {TRIAGE, PENDING, NEEDS_CLARIFICATION, READY, IN_PROGRESS, PARKED}

# What each command moves the issue to. Commands absent from this map change no
# labels at all.
MOVES = {
    "admit": TRIAGE,
    "propose": TRIAGE,
    "revise": TRIAGE,
    "unpark": TRIAGE,
    "approve": READY,
    "redo": READY,
    "park": PARKED,
}

# What to say after each move, in terms of what happens next rather than what
# label changed — the label is a means, and the reader wants the consequence.
NEXT = {
    TRIAGE: "the next triage run will pick it up",
    READY: "the nightly builder can pick it up next",
    PARKED: "the pipeline will leave it alone until `/unpark`",
}

# Refusals nobody is told about. Replying to a stranger would let them make the
# bot post; replying to a replay would post the same ack twice.
SILENT_SKIPS = {"not-owner", "already-applied"}


class Plan:
    def __init__(self, labels, milestone=None, focus=None, cap=None) -> None:
        self.labels = labels
        self.milestone = milestone
        self.focus = focus
        self.cap = cap


def plan(current_labels, actions) -> Plan:
    """The label set and settings after applying `actions` in order."""
    labels = list(current_labels)
    milestone = focus = cap = None

    for act in actions:
        if act.command in MOVES:
            labels = [label for label in labels if label not in STATE_LABELS]
            labels.append(MOVES[act.command])
        elif act.command == "milestone":
            milestone = act.argument
        elif act.command == "focus":
            focus = act.argument
        elif act.command == "cap":
            cap = int(act.argument)

    return Plan(labels, milestone, focus, cap)


def fires_triage(current_labels, new_labels) -> bool:
    """Should reactive triage be fired?

    **Only when `ai-triage` is newly present.** An idempotent re-add — a replay,
    or a second `/admit` on an already-admitted issue — must not fire triage
    again, or a stuck comment becomes a triage run every sweep.
    """
    return TRIAGE in set(new_labels) and TRIAGE not in set(current_labels)


def acknowledgement(actions, skips) -> str:
    """The comment to post, or "" for nothing."""
    lines: list[str] = []

    for act in actions:
        if act.command in MOVES:
            destination = MOVES[act.command]
            lines.append(f"- `/{act.command}` → **{destination}**; {NEXT[destination]}.")
        elif act.command == "milestone":
            lines.append(f"- `/{act.command}` → milestone set to **{act.argument}**.")
        elif act.command in ("focus", "cap"):
            lines.append(f"- `/{act.command}` → **{act.argument}**, recorded on the dashboard.")

    refusals = [skip for skip in skips if skip.reason not in SILENT_SKIPS]

    for skip in refusals:
        lines.append(f"- `/{skip.command}` was **not applied**. {skip.detail}")

    if not lines:
        return ""

    if refusals:
        lines.append("")
        lines.append(
            "Nothing was changed for the refused command(s) — not the labels, not the "
            "milestone. Fix the reason and comment again.")

    return "\n".join(lines)


def should_watermark(actions, skips) -> bool:
    """Leave the 👀 so this comment is never reconsidered.

    A **refused** comment is watermarked too: it was considered, and without the
    mark the sweep reconsiders it on every run, re-posting the same refusal.

    A stranger's comment is not, because it was never claimed — reacting to it
    would itself be letting them make the bot act.
    """
    if any(skip.reason == "not-owner" for skip in skips):
        return False

    return bool(actions or skips)
