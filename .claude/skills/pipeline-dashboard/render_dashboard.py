#!/usr/bin/env python3
"""Render the dashboard issue body from live repo state.

A pure function of the state, with no model in the loop. The board is what
Derek looks at to decide what to approve next, so it has to be **true** — and
the only way to guarantee that is to derive every line of it from GitHub and
regenerate the whole thing every time. Nothing on it is remembered, so nothing
on it can be stale.

    python3 render_dashboard.py < state.json          # print, write nothing
    python3 render_dashboard.py --write < state.json  # PATCH the issue body

Stdlib only. `render()` does no I/O at all; `--write` is the only path that
touches the network, and the only thing it can touch is the dashboard issue's
own body.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import os
import re
import sys
import urllib.request

DEFAULT_CAP = 3

TRIAGE = "ai-triage"
PENDING = "pending-approval"
NEEDS_CLARIFICATION = "needs-clarification"
READY = "ready-for-work"
IN_PROGRESS = "in-progress"
PARKED = "parked"

PLANNING_LABELS = {TRIAGE, PENDING, NEEDS_CLARIFICATION}
ACTIVE_LABELS = {READY, IN_PROGRESS}
STATE_LABELS = PLANNING_LABELS | ACTIVE_LABELS | {PARKED}

FOCUS_MARKER = re.compile(r"<!--\s*pipeline-focus:\s*(.+?)\s*-->")
CAP_MARKER = re.compile(r"<!--\s*pipeline-cap:\s*(.+?)\s*-->")


def _labels(issue) -> set:
    return set(issue.get("labels") or [])


def _body(data) -> str:
    return (data.get("dashboard_issue") or {}).get("body") or ""


def _milestone_titles(data) -> list:
    return [m.get("title") for m in data.get("milestones") or []]


def resolve_focus(data: dict, override=None) -> str:
    """Focus milestone: override → marker → the `focus` key.

    An override naming no live milestone is **rejected, never stored**. A
    typo'd `/focus v0.0.10` would otherwise render a board where every section
    is empty — which looks exactly like a finished milestone, and is the most
    misleading thing this script could produce.
    """
    live = _milestone_titles(data)

    if override is not None:
        if live and override not in live:
            raise ValueError(
                f"No live milestone named {override!r}. "
                f"Milestones are: {', '.join(str(t) for t in live)}.")
        return override

    found = FOCUS_MARKER.search(_body(data))
    if found:
        return found.group(1)

    return data.get("focus")


def resolve_cap(data: dict, override=None) -> int:
    """Build cap: override → marker → 3."""
    if override is not None:
        return int(override)

    found = CAP_MARKER.search(_body(data))
    if found:
        try:
            return int(found.group(1))
        except ValueError:
            # A malformed marker falls back to the default rather than failing
            # the render. A board that does not render is worse than one with
            # a default cap, and the next `/cap` fixes it.
            return DEFAULT_CAP

    return DEFAULT_CAP


def _focus_issues(data, focus) -> list:
    return [
        issue for issue in data.get("issues") or []
        if issue.get("milestone") == focus
    ]


def focus_pie(data: dict, focus=None) -> dict:
    """Four slices, and **every focus issue lands in exactly one.**

    Ordered checks rather than independent predicates, so nothing can fall
    through and nothing can be counted twice. The total is the milestone's
    issue count — which is what makes the pie trustworthy: an issue cannot
    silently vanish from the board by having an odd label combination.
    """
    focus = focus or resolve_focus(data)
    counts = {"Unplanned": 0, "In Planning": 0, "Ready": 0, "Done": 0}

    for issue in _focus_issues(data, focus):
        labels = _labels(issue)

        if issue.get("state") == "closed":
            counts["Done"] += 1
        elif PARKED in labels or not (labels & STATE_LABELS):
            # Parked and never-triaged are both "not being worked and not
            # waiting on anybody" — different reasons, same meaning for
            # planning purposes.
            counts["Unplanned"] += 1
        elif labels & ACTIVE_LABELS:
            counts["Ready"] += 1
        else:
            counts["In Planning"] += 1

    return counts


def ready_queue(data: dict, focus=None) -> list:
    """Focus-milestone issues waiting to be built, lowest number first.

    `parked` is excluded even when it also carries `ready-for-work`. The
    Parked section is a listing, not a re-admission.
    """
    focus = focus or resolve_focus(data)

    return sorted(
        (
            issue for issue in _focus_issues(data, focus)
            if issue.get("state") != "closed"
            and READY in _labels(issue)
            and PARKED not in _labels(issue)
        ),
        key=lambda issue: issue.get("number", 0),
    )


def _bar(count: int, total: int, width: int = 20) -> str:
    if not total:
        return "░" * width
    filled = round(width * count / total)
    return "█" * filled + "░" * (width - filled)


def render(data: dict, focus_override=None, cap_override=None) -> str:
    """The whole dashboard body. Byte-stable for a given input."""
    focus = resolve_focus(data, focus_override)
    cap = resolve_cap(data, cap_override)

    pie = focus_pie(data, focus)
    total = sum(pie.values())

    lines = [
        "# 🐸 Pipeline dashboard",
        "",
        "_Regenerated on every render. Edit the markers, never the sections —",
        "a hand edit to a rendered section disappears at the next render._",
        "",
        f"<!-- pipeline-focus: {focus} -->",
        f"<!-- pipeline-cap: {cap} -->",
        "",
        f"## 🎯 Focus: {focus}",
        "",
        f"{total} issue{'' if total == 1 else 's'} in this milestone.",
        "",
        "| Slice | Count | |",
        "| --- | ---: | --- |",
    ]

    for slice_name, count in pie.items():
        lines.append(f"| {slice_name} | {count} | `{_bar(count, total)}` |")

    queue = ready_queue(data, focus)

    lines += [
        "",
        f"## 🔨 Ready queue (cap {cap})",
        "",
    ]

    if queue:
        lines += [
            "| Issue | Title | Milestone |",
            "| --- | --- | --- |",
        ]
        for issue in queue:
            lines.append(
                f"| #{issue['number']} | {issue.get('title', '')} "
                f"| {issue.get('milestone') or '—'} |")
    else:
        lines.append("Nothing is ready to build.")

    lines.append("")

    return "\n".join(lines) + "\n"


def write_dashboard(data: dict, body: str, token=None, repository=None):  # pragma: no cover
    """PATCH the dashboard issue body. The only write this script can make.

    Authenticates with `GITHUB_TOKEN` — never a PAT. The token's scope is this
    repository, and the only endpoint touched is this one issue.
    """
    token = token or os.environ["GITHUB_TOKEN"]
    repository = repository or os.environ["GITHUB_REPOSITORY"]
    number = data["dashboard_issue"]["number"]

    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/issues/{number}",
        data=json.dumps({"body": body}).encode(),
        method="PATCH",
    )
    request.add_header("Authorization", f"Bearer {token}")
    request.add_header("Accept", "application/vnd.github+json")

    with urllib.request.urlopen(request, timeout=30) as response:
        return response.status


def main(argv=None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    data = json.load(sys.stdin)

    body = render(data)

    if "--write" in argv:  # pragma: no cover - network path
        write_dashboard(data, body)
        return 0

    sys.stdout.write(body)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
