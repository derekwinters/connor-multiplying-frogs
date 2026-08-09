#!/usr/bin/env python3
"""Compare what the labels claim against what GitHub actually shows.

Reality drifts. A PR merges without its closing keyword, someone hand-edits a
label, a run dies between two API calls. This sweep finds the gaps and sorts
each one into **auto-fix** or **flag**.

The split is the whole design. Auto-fix is for drift with a single correct
answer and no judgement in it; everything else is surfaced for a human. A
reconciler that guesses is a reconciler you have to audit, and one you have to
audit is one you turn off — at which point it is worse than not having it,
because the dashboard still says it ran.

    echo '{"issues": [...], "focus": "v0.0.1"}' | python3 reconcile.py

Stdlib only, no network: fetching is the caller's job.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

# One recognizer for "triage wrote this", shared with the skill that writes the
# format. A second copy here would drift, and the drift is self-sustaining:
# this sweep sees no analysis and requeues to `ai-triage`, triage sees the
# analysis it wrote and only repairs the label, and the issue cycles forever
# with neither side reporting an error. See #65.
_TRIAGE_SKILL = Path(__file__).resolve().parents[1] / "triage-issue"
if str(_TRIAGE_SKILL) not in sys.path:
    sys.path.insert(0, str(_TRIAGE_SKILL))

from triage_repair import has_analysis_signature, is_triage_author  # noqa: E402

# Same rule, same reason, for `Blocked by #N`: the skill that documents the
# format owns the recognizer. A copy here that drifted would report an issue as
# prose-only, or flag a cycle, on a reading nobody else in the pipeline shares.
# See #147.
_BLOCKERS_SKILL = Path(__file__).resolve().parents[1] / "issue-blockers"
if str(_BLOCKERS_SKILL) not in sys.path:
    sys.path.insert(0, str(_BLOCKERS_SKILL))

from blocker_refs import blockers_of, text_blockers  # noqa: E402

TRIAGE_LABEL = "ai-triage"
READY_LABEL = "ready-for-work"
IN_PROGRESS_LABEL = "in-progress"
PARKED_LABEL = "parked"
DASHBOARD_LABEL = "dashboard"

STATE_LABELS = {
    TRIAGE_LABEL,
    "pending-approval",
    "needs-clarification",
    READY_LABEL,
    IN_PROGRESS_LABEL,
    PARKED_LABEL,
}

# States that should carry a triage analysis. `ai-triage` has not been analyzed
# yet by definition, and `parked` is the owner's decision — requeuing it would
# overrule a human.
ANALYZED_STATES = {"pending-approval", "needs-clarification", READY_LABEL,
                   IN_PROGRESS_LABEL}

# Done-ness comes from a closing keyword in a commit **body**. Never a title:
# a squash merge puts `(#148)` in the subject and that is a PR number, not a
# statement that the issue is finished. Never a bare `#10` or `Refs #10`
# either — those are references, and treating them as completion would mark
# work done because somebody mentioned it.
CLOSING_KEYWORD = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(\d+)\b",
    re.IGNORECASE,
)


def _finding(kind: str, action: str, issue_number=None, **extra) -> dict:
    found = {"kind": kind, "action": action}
    if issue_number is not None:
        found["issue"] = issue_number
    found.update(extra)
    return found


def _labels(issue) -> set:
    return set(issue.get("labels") or [])


def _text_blockers(issue) -> list:
    return sorted(text_blockers(issue.get("body")))


def _closed_by_body(body: str) -> set:
    """Issue numbers a commit body closes — the body only, never line one."""
    lines = (body or "").splitlines()
    return {int(n) for n in CLOSING_KEYWORD.findall("\n".join(lines[1:]))}


def landed_on_main(issue_number: int, merged_commits) -> bool:
    return any(
        issue_number in _closed_by_body(commit.get("body"))
        for commit in merged_commits or []
    )


def has_open_pr(issue_number: int, pulls) -> bool:
    return any(
        pull.get("state", "open") == "open"
        and issue_number in _closed_by_body("x\n" + (pull.get("body") or ""))
        for pull in pulls or []
    )


def has_analysis(issue) -> bool:
    """Did triage analyze this issue — by triage's own hand?"""
    return any(
        is_triage_author(comment.get("author"))
        and has_analysis_signature(comment.get("body"))
        for comment in issue.get("comments") or []
    )


def find_cycles(issues) -> list:
    """Every set of issues that transitively block each other.

    Reported, never resolved. A cycle is a triage mistake, and silently
    picking an entry point to break it hides the mistake behind working
    software.
    """
    graph = {}
    for issue in issues:
        number = issue["number"]
        graph[number] = set(blockers_of(issue))

    cycles = []
    seen = set()

    for start in sorted(graph):
        if start in seen:
            continue

        # Walk forward from `start`; anything reachable that reaches back is
        # in a cycle with it.
        reachable = set()
        stack = [start]
        while stack:
            current = stack.pop()
            for nxt in graph.get(current, ()):  # noqa: SIM118
                if nxt in graph and nxt not in reachable:
                    reachable.add(nxt)
                    stack.append(nxt)

        if start in reachable:
            members = sorted(
                {start} | {other for other in reachable if start in _reaches(graph, other)}
            )
            if not seen.intersection(members):
                cycles.append(members)
            seen.update(members)

    return cycles


def _reaches(graph, origin) -> set:
    reachable = set()
    stack = [origin]
    while stack:
        current = stack.pop()
        for nxt in graph.get(current, ()):  # noqa: SIM118
            if nxt in graph and nxt not in reachable:
                reachable.add(nxt)
                stack.append(nxt)
    return reachable


def process(data: dict) -> dict:
    """Snapshot in, findings out. No I/O — that lives at the edges."""
    issues = data.get("issues") or []
    pulls = data.get("pulls") or []
    merged_commits = data.get("merged_commits") or []

    # The event path runs on every comment. Two of the auto-fixes are wrong
    # there and correct nightly, so they are omitted entirely rather than
    # softened — see the cron-only note on each.
    events_only = bool(data.get("events_only"))

    findings = []

    for issue in sorted(issues, key=lambda i: i.get("number", 0)):
        number = issue["number"]
        labels = _labels(issue)
        state_labels = sorted(labels & STATE_LABELS)

        if issue.get("state") == "closed":
            # Safe on every pass, including the event path: it acts only on an
            # already-closed issue, and a closed issue is not transiently
            # anything. Nothing it does can be undone by work in flight.
            if state_labels:
                findings.append(_finding(
                    "strip_labels", "auto-fix", number, remove_labels=state_labels))
            continue

        landed = landed_on_main(number, merged_commits)
        open_pr = has_open_pr(number, pulls)
        analyzed = has_analysis(issue)

        if landed:
            # Flagged, never closed. GitHub closes an issue from the PR's
            # keyword when Derek merges; this sweep closing it would mark work
            # done that nobody accepted. Checked before the stall rule so an
            # issue already on `main` never reads as a stall — without that
            # guard it would be requeued, rebuilt, and requeued again.
            findings.append(_finding("flag_merged_but_open", "flag", number))

        elif IN_PROGRESS_LABEL in labels and not open_pr and not events_only:
            # Cron-only. On the event path an issue picked up seconds ago has
            # `in-progress` and no PR yet — indistinguishable from a stall, and
            # requeuing it would yank work out from under a running agent.
            findings.append(_finding(
                "requeue", "auto-fix", number,
                add_labels=[READY_LABEL], remove_labels=[IN_PROGRESS_LABEL]))

        if (labels & ANALYZED_STATES) and not analyzed and not events_only:
            # Cron-only for the mirror-image reason: triage posts its comment
            # before setting the label, so between those two writes an issue
            # legitimately has neither yet — and moments later has both.
            findings.append(_finding(
                "requeue_triage", "auto-fix", number,
                add_labels=[TRIAGE_LABEL],
                remove_labels=sorted(labels & ANALYZED_STATES)))

        if analyzed and not state_labels:
            # Flagged, not restored: the analysis says the issue was triaged
            # but not which route it took, so the intended state is genuinely
            # ambiguous and only a reader of the comment can say.
            findings.append(_finding("flag_orphaned_analysis", "flag", number))

        if READY_LABEL in labels and not issue.get("milestone"):
            # The invariant is broken, but the fix is a decision about which
            # milestone — not something to guess.
            findings.append(_finding("flag_orphaned_ready", "flag", number))

        prose_only = [
            blocker for blocker in _text_blockers(issue)
            if blocker not in set(issue.get("native_blockers") or [])
        ]
        if prose_only:
            findings.append(_finding(
                "flag_prose_dependency", "flag", number, blockers=prose_only))

    for members in find_cycles([i for i in issues if i.get("state") != "closed"]):
        findings.append(_finding("flag_cycle", "flag", issues=members))

    dashboards = [i["number"] for i in issues
                  if DASHBOARD_LABEL in _labels(i) and i.get("state") != "closed"]
    if len(dashboards) != 1:
        findings.append(_finding(
            "flag_dashboard_count", "flag", dashboards=sorted(dashboards)))

    return {
        "findings": findings,
        "count": len(findings),
        "auto_fix_count": sum(1 for f in findings if f["action"] == "auto-fix"),
        "flag_count": sum(1 for f in findings if f["action"] == "flag"),
    }


def main(argv=None) -> int:
    json.dump(process(json.load(sys.stdin)), sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
