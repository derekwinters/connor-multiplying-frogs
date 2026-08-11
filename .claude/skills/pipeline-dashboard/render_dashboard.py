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
from pathlib import Path

# One recognizer for `Blocked by #N`, and one union of it with the native
# edges, shared with the skill that documents the format. A copy here that
# drifted would show the board as ready while the builder refuses to build —
# the failure Derek would see first and be least able to explain. See #147.
_BLOCKERS_SKILL = Path(__file__).resolve().parents[1] / "issue-blockers"
if str(_BLOCKERS_SKILL) not in sys.path:
    sys.path.insert(0, str(_BLOCKERS_SKILL))

from blocker_refs import blockers_of  # noqa: E402

DEFAULT_CAP = 3

TRIAGE = "ai-triage"
PENDING = "pending-approval"
NEEDS_CLARIFICATION = "needs-clarification"
READY = "ready-for-work"
IN_PROGRESS = "in-progress"
PARKED = "parked"

DASHBOARD = "dashboard"
EPIC = "type:epic"

PLANNING_LABELS = {TRIAGE, PENDING, NEEDS_CLARIFICATION}
ACTIVE_LABELS = {READY, IN_PROGRESS}
STATE_LABELS = PLANNING_LABELS | ACTIVE_LABELS | {PARKED}

# What an Intake row says when nobody has `/admit`ted the issue.
UNADMITTED_FLAG = "🚪 not admitted"

FOCUS_MARKER = re.compile(r"<!--\s*pipeline-focus:\s*(.+?)\s*-->")
CAP_MARKER = re.compile(r"<!--\s*pipeline-cap:\s*(.+?)\s*-->")

COMMANDS = [
    ("/admit", "bring an issue into the pipeline"),
    ("/approve", "the plan is right — build it"),
    ("/revise <notes>", "the plan is not right, here is why"),
    ("/redo", "the build is not right — build it again"),
    ("/propose", "no spec for this yet; design it and show me"),
    ("/park", "set aside, do not pick this up"),
    ("/unpark", "back into the pipeline"),
    ("/milestone <title>", "set the issue's milestone"),
    ("/focus <title>", "set the focus milestone (this issue only)"),
    ("/cap <n>", "issues per build round (this issue only)"),
]


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

    **This is the one issue table that is focus-scoped**, because it is the
    one that predicts what the nightly builder will do — and the builder
    builds the focus milestone. Listing an out-of-focus `ready-for-work` issue
    here would put work in front of Derek that nothing is going to pick up.
    """
    focus = focus or resolve_focus(data)

    return _sorted_rows(_active(data, READY, focus), data)


def _open_issues(data) -> dict:
    return {
        issue["number"]: issue for issue in data.get("issues") or []
        if issue.get("state") != "closed"
    }


def compute_unblockers(data: dict) -> dict:
    """`{issue number: [issues it unblocks]}` — the highest-leverage picks.

    An issue earns a star when it appears in another open issue's hard-blocker
    set **and is not itself blocked**. Both halves matter: the first is what
    makes it leverage, and the second is what makes it actionable. Starring a
    blocked issue would point Derek at work nobody can start, which is worse
    than no star at all — it costs a click to discover the suggestion was
    useless.

    A closed blocker is never starred: it has already done its unblocking.
    """
    issues = _open_issues(data)
    unblocks = {}

    for number, issue in issues.items():
        for blocker in blockers_of(issue):
            if blocker in issues:
                unblocks.setdefault(blocker, []).append(number)

    return {
        blocker: sorted(blocked)
        for blocker, blocked in unblocks.items()
        if not blockers_of(issues[blocker])
    }


def _is_blocked(issue, data) -> bool:
    issues = _open_issues(data)
    return any(blocker in issues for blocker in blockers_of(issue))


def _active(data, label, focus=None):
    """Open, carrying `label`, and **not** parked. Board-wide unless scoped.

    Parked is excluded from every active queue. The Parked section is a
    listing, not a re-admission — an issue set aside by `/park` must not
    reappear in a table that says "here is what to do next".

    `focus` narrows to one milestone, and **only the ready queue passes it.**
    See `intake` for why the attention sections do not.
    """
    issues = data.get("issues") or [] if focus is None else _focus_issues(data, focus)

    return [
        issue for issue in issues
        if issue.get("state") != "closed"
        and label in _labels(issue)
        and PARKED not in _labels(issue)
    ]


def _sorted_rows(issues, data):
    """Unblockers first (most unblocked first), then by number."""
    stars = compute_unblockers(data)
    return sorted(
        issues,
        key=lambda issue: (
            -len(stars.get(issue["number"], [])),
            issue.get("number", 0),
        ),
    )


def is_unadmitted(issue) -> bool:
    """Open, carrying no pipeline-state label, and admissible.

    An issue nobody has `/admit`ted. It is not a triage candidate — the
    nightly analysis run keys on `ai-triage` alone — so nothing will happen to
    it until Derek types the command.

    The two exclusions are the two things `/admit` would be refused on, and
    listing an issue beside a command that gets refused is worse than not
    listing it. The dashboard is the pipeline's own furniture, and an epic is
    a container whose children carry the work.
    """
    labels = _labels(issue)

    return (
        issue.get("state") != "closed"
        and not (labels & STATE_LABELS)
        and DASHBOARD not in labels
        and EPIC not in labels
    )


def intake(data: dict) -> list:
    """Everything waiting to be looked at, **anywhere on the board.**

    Two piles, and the `🚪 not admitted` flag is what tells them apart:

    - **`ai-triage`** — the pipeline has it. Tonight's analysis run picks it
      up and nothing is needed from Derek.
    - **unadmitted** — nothing has it. Only `/admit` moves it.

    They share a table because both answer "what has nobody looked at yet",
    and a section Derek has to remember to scroll to is a section that stops
    being read. They carry a flag because the *action* differs, and an Intake
    row that does not say which pile it is in would mean two things at once.

    Deliberately not focus-scoped, for the reason the two piles exist at all:
    neither has been triaged, and triage is what decides an issue's milestone,
    so most rows here have no milestone to filter on. Filtering by focus left
    this table permanently near-empty while the work it exists to surface
    piled up out of sight.

    The Milestone column keeps that readable — a row reading `—` is an issue
    nobody has scheduled, which is the point of putting it here.
    """
    # Disjoint by construction: `ai-triage` is itself a state label, so an
    # issue carrying it is never unadmitted.
    return _sorted_rows(
        _active(data, TRIAGE) + [i for i in data.get("issues") or [] if is_unadmitted(i)],
        data)


def pending(data: dict) -> list:
    """Everything waiting on Derek, **anywhere on the board.**

    Board-wide for a blunter reason than Intake: much of this work lives in
    `Direct Involvement Needed`, a milestone with no version that never ships
    and therefore can never be the focus. Focus-scoping the one section titled
    "Waiting for you" would hide exactly the issues that are waiting for him.
    """
    return _sorted_rows(_active(data, PENDING), data)


def needs_clarification(data: dict) -> list:
    """Everything blocked on a question, **anywhere on the board.**

    A question does not stop being unanswered because it is scheduled for a
    later milestone, and answering it early is often what lets the issue be
    scheduled at all.
    """
    return _sorted_rows(_active(data, NEEDS_CLARIFICATION), data)


def parked(data: dict) -> list:
    """Everything set aside, **anywhere on the board.**

    Parked work is listed so it can be found and unparked. An out-of-focus
    parked issue is the one most in need of that — it is two filters away from
    anybody noticing it again.
    """
    return sorted(
        (
            issue for issue in data.get("issues") or []
            if issue.get("state") != "closed" and PARKED in _labels(issue)
        ),
        key=lambda issue: issue.get("number", 0),
    )


def _bar(count: int, total: int, width: int = 20) -> str:
    if not total:
        return "░" * width
    filled = round(width * count / total)
    return "█" * filled + "░" * (width - filled)


AS_OF_LINE = re.compile(r"^_Board as of .*_$", re.MULTILINE)


def body_changed(current: str, new: str) -> bool:
    """Do these two bodies differ in anything but their timestamp?

    The "as of" line makes every render textually different, which would
    defeat byte-stability's whole purpose: the scheduled runs would PATCH the
    issue every time and the dashboard's history would be a wall of edits that
    changed nothing.

    So the timestamp is rendered, but it is not on its own a reason to write.
    """
    strip = lambda body: AS_OF_LINE.sub("", body or "")  # noqa: E731
    return strip(current) != strip(new)


def render(data: dict, focus_override=None, cap_override=None, as_of=None) -> str:
    """The whole dashboard body. Byte-stable for a given input.

    `as_of` is an input like any other — the renderer never reads a clock, so
    the same arguments always produce the same bytes.
    """
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
    ]

    if as_of:
        lines += [f"_Board as of {as_of}._", ""]

    lines += [
        f"## 🎯 Focus: {focus}",
        "",
        f"{total} issue{'' if total == 1 else 's'} in this milestone.",
        "",
        "| Slice | Count | |",
        "| --- | ---: | --- |",
    ]

    for slice_name, count in pie.items():
        lines.append(f"| {slice_name} | {count} | `{_bar(count, total)}` |")

    stars = compute_unblockers(data)

    def table(heading, issues, empty):
        lines.extend(["", heading, ""])

        if not issues:
            lines.append(empty)
            return

        lines.extend([
            "| Issue | Title | Milestone | Blocked by |",
            "| --- | --- | --- | --- |",
        ])

        for issue in issues:
            number = issue["number"]
            title = issue.get("title", "")

            # Inside the star prefix, so the highest-leverage signal still
            # reads first on a row that carries both.
            if is_unadmitted(issue):
                title = f"{UNADMITTED_FLAG} — {title}"

            if number in stars:
                unblocked = ", ".join(f"#{n}" for n in stars[number])
                title = f"⭐ unblocks {unblocked} — {title}"

            blocking = [b for b in blockers_of(issue) if b in _open_issues(data)]
            blocked = (
                "⛔ blocked: " + ", ".join(f"#{b}" for b in blocking)
                if blocking else "—"
            )

            lines.append(
                f"| #{number} | {title} | {issue.get('milestone') or '—'} | {blocked} |")

    # Only the ready queue is focus-scoped. The sections below it say
    # "somebody has to look at this", and an issue does not stop needing to be
    # looked at by sitting outside the milestone currently being built —
    # least of all one that has no milestone because nobody has triaged it.
    table(f"## 🔨 Ready queue (cap {cap})", ready_queue(data, focus),
          "Nothing is ready to build.")
    table("## 📥 Intake", intake(data), "Nothing waiting for triage.")
    table("## ✋ Waiting for you", pending(data),
          "Nothing waiting for approval.")
    table("## ❓ Needs clarification", needs_clarification(data),
          "Nothing blocked on a question.")

    # Read-only: parked work stays visible so it can be found and unparked,
    # but it is deliberately not a queue.
    lines += ["", "## ⏸️ Parked", ""]
    parked_issues = parked(data)
    if parked_issues:
        for issue in parked_issues:
            lines.append(f"- #{issue['number']} {issue.get('title', '')} — `/unpark`")
    else:
        lines.append("Nothing parked.")

    # Only flags. Auto-fixes have already been applied by the time this
    # renders, so listing them would report problems that no longer exist.
    lines += ["", "## ⚠️ Reconcile", ""]
    flags = [
        finding for finding in data.get("reconcile_findings") or []
        if finding.get("action") == "flag"
    ]
    if flags:
        lines += ["| Finding | Issue |", "| --- | --- |"]
        for finding in flags:
            target = finding.get("issue")
            lines.append(
                f"| `{finding.get('kind')}` | "
                f"{f'#{target}' if target else '—'} |")
    else:
        lines.append("Nothing flagged.")

    lines += ["", "## 📅 Other milestones", ""]
    others = _other_milestones(data, focus)
    if others:
        lines += ["| Milestone | Done | Open |", "| --- | ---: | ---: |"]
        for title, done, open_count in others:
            lines.append(f"| {title} | {done} | {open_count} |")
    else:
        lines.append("Nothing else in flight.")

    lines += ["", "## 🎮 Commands", "", "| Command | Does |", "| --- | --- |"]
    for command, description in COMMANDS:
        lines.append(f"| `{command}` | {description} |")

    lines.append("")

    return "\n".join(lines) + "\n"


def _other_milestones(data, focus) -> list:
    """Progress on every other milestone that still has open work.

    A milestone with nothing open is **omitted**. It is finished, and a row
    reading 100% is a line of the board spent saying "nothing to do here" —
    on a page whose whole job is showing what to do next.
    """
    rows = []

    for milestone in data.get("milestones") or []:
        title = milestone.get("title")
        if title == focus:
            continue

        issues = [i for i in data.get("issues") or [] if i.get("milestone") == title]
        if not issues:
            continue

        open_count = sum(1 for i in issues if i.get("state") != "closed")
        if not open_count:
            continue

        rows.append((title, len(issues) - open_count, open_count))

    return rows


def write_dashboard(data: dict, body: str, token=None, repository=None):  # pragma: no cover
    """PATCH the dashboard issue body. The only write this script can make.

    Authenticates with `GITHUB_TOKEN` — never a PAT. The token's scope is this
    repository, and the only endpoint touched is this one issue.

    Skips the write entirely when nothing but the timestamp changed, so a
    scheduled render on an unchanged board costs no edit.
    """
    token = token or os.environ["GITHUB_TOKEN"]
    repository = repository or os.environ["GITHUB_REPOSITORY"]
    number = data["dashboard_issue"]["number"]

    if not body_changed(data["dashboard_issue"].get("body") or "", body):
        return None

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

    # Passed in rather than read from a clock, so `render` stays a pure
    # function of its arguments and the golden test stays possible.
    as_of = os.environ.get("DASHBOARD_AS_OF") or None
    body = render(data, as_of=as_of)

    if "--write" in argv:  # pragma: no cover - network path
        write_dashboard(data, body)
        return 0

    sys.stdout.write(body)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
