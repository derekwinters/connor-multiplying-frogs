#!/usr/bin/env python3
"""What to do when triage fires on an issue it has already analyzed.

Triage re-fires: a sweep catches a comment late, a Routine is poked twice, a run
crashes after posting its analysis but before moving the label. The question
each time is the same — is there already an analysis here?

If there is, the answer is **repair, not repeat**: apply the missing label move
and post nothing. Re-analyzing would stack a second plan on top of the first,
and the two would not agree, because triage is not deterministic.

That decision is, though — so it lives here as a pure function rather than as a
judgment the agent makes each time.

## The recognizer is shared, deliberately

`has_analysis_signature` is the one implementation of "does this comment look
like triage wrote it". `pipeline-reconcile` imports it from here rather than
carrying its own copy.

Two copies would drift, and the drift is silent and asymmetric: reconcile
decides an issue has no analysis and sends it back to `ai-triage`, triage sees
the analysis it already wrote and repairs the label instead, reconcile sends it
back again. Neither side is obviously wrong on its own, and the issue cycles
forever. The producer of the format owns the recognizer.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import re

TRIAGE_LABEL = "ai-triage"

# The states a hand-back can rest in. Triage never sets any other.
HAND_BACK_STATES = ("pending-approval", "needs-clarification")

# Every state label, so a swap replaces rather than stacks.
STATE_LABELS = {
    TRIAGE_LABEL,
    "pending-approval",
    "needs-clarification",
    "ready-for-work",
    "in-progress",
    "parked",
}

# Comments these logins authored are triage's. A human pasting a checklist by
# hand is not a triage run, and treating it as one would let a well-meaning
# comment suppress the analysis the issue is actually waiting for.
#
# **All three, because triage has three surfaces**, confirmed against the board:
#
# | Login | When |
# | --- | --- |
# | `claude[bot]` | triage running as the Claude GitHub App — #4, #147 |
# | `derekwinters` | triage running in a session under Derek's own account — #83, #169 |
# | `github-actions[bot]` | triage running from a workflow holding `GITHUB_TOKEN` |
#
# All three posted analyses in the same round on 9 Aug. Accepting only one is
# not a stricter rule, it is a broken one: this set read `github-actions[bot]`
# alone while no analysis had ever come from it, so `has_analysis_signature` was
# never reached and the pipeline believed no issue had been analyzed at all.
#
# **The cost of including the owner, stated plainly.** A `## Build checklist`
# Derek writes by hand now reads as a triage analysis, and will suppress the
# analysis the issue is actually waiting for. That is accepted deliberately —
# triage genuinely runs under his account, so excluding him would leave a third
# of hand-backs invisible, which is the worse failure. Derek's call, taken
# knowing the trade.
TRIAGE_AUTHORS = frozenset({"claude[bot]", "derekwinters", "github-actions[bot]"})


def is_triage_author(login) -> bool:
    """Did triage write this, by its own hand?

    The one definition. `pipeline-reconcile` imports it rather than keeping a
    copy — the same rule, and the same reason, as `has_analysis_signature`
    below: two copies of "was this triage" drift, and the drift is a cycle
    neither side reports.
    """
    return (login or "").lower() in TRIAGE_AUTHORS

# An owner note that objects to the plan. Repairing the label would apply the
# state the rejected plan asked for and drop the objection on the floor.
REANALYZE_COMMANDS = ("revise", "redo", "propose")

# Anchored at the start of a line: a heading, not the phrase in a sentence.
# "I will add a ## Build checklist heading later" must not read as an analysis.
CHECKLIST_HEADING = re.compile(r"^\s{0,3}#{2,6}\s+build\s+checklist\s*$",
                               re.IGNORECASE | re.MULTILINE)

# The question marker. The ❓ is load-bearing — it is what separates the marker
# from prose that happens to contain the same words. Emphasis may wrap the text,
# and may or may not include the colon, because both get written.
QUESTION_MARKER = re.compile(
    r"❓\s*[*_]{0,2}\s*needs\s+from\s+derek/connor\s*[*_]{0,2}\s*:",
    re.IGNORECASE,
)


def has_analysis_signature(body) -> bool:
    """Does this comment body look like a triage analysis?

    True for a `## Build checklist` heading **or** the
    `❓ Needs from Derek/Connor:` marker — the two shapes a hand-back takes,
    one per route. A bare prose mention of either phrase is not a match.
    """
    if not body:
        return False

    return bool(CHECKLIST_HEADING.search(body) or QUESTION_MARKER.search(body))


def analysis_comment_times(comments) -> list:
    """Timestamps of triage's own analysis comments, oldest first."""
    times = [
        comment.get("created_at") or ""
        for comment in comments or []
        if is_triage_author(comment.get("author"))
        and has_analysis_signature(comment.get("body"))
    ]

    return sorted(times)


class RepairPlan:
    def __init__(self, reanalyze: bool, add_labels=None, remove_labels=None) -> None:
        self.reanalyze = reanalyze
        self.add_labels = add_labels or []
        self.remove_labels = remove_labels or []
        # A repair posts nothing. The analysis is already on the issue; a second
        # comment saying so is noise on something Derek is about to read.
        self.comment = ""

    @property
    def repair_only(self) -> bool:
        return not self.reanalyze

    @property
    def changes_anything(self) -> bool:
        return bool(self.add_labels or self.remove_labels)

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        kind = "reanalyze" if self.reanalyze else "repair"
        return f"RepairPlan({kind}, +{self.add_labels}, -{self.remove_labels})"


def plan_repair(labels, comments, intended_state: str, note=None) -> RepairPlan:
    """Repair the half-finished write, or re-analyze from scratch.

    `intended_state` is where the existing analysis was heading — the state its
    route ends at. On a fresh issue with no analysis, it is unused: the new run
    decides its own state.
    """
    if note and (note.get("command") or "").lower() in REANALYZE_COMMANDS:
        return RepairPlan(reanalyze=True)

    if not analysis_comment_times(comments):
        return RepairPlan(reanalyze=True)

    current = set(labels or [])

    if intended_state in current:
        # Nothing to repair — the earlier run finished after all.
        return RepairPlan(reanalyze=False)

    return RepairPlan(
        reanalyze=False,
        add_labels=[intended_state],
        remove_labels=sorted(current & STATE_LABELS),
    )
