#!/usr/bin/env python3
"""The board-wide sweep: replay missed comments, wake cleared blockers, reconcile.

Two paths, and the difference between them is one flag:

    run_sweep.py                  # cron — everything
    run_sweep.py --events-only    # the issue/PR event path

**`--events-only` omits the comment replay and reconcile's two requeue fixes.**

The requeue fixes, because the drift they detect is indistinguishable from work
in flight: an issue the builder picked up ten seconds ago has `in-progress` and
no PR — exactly a stall's shape — and requeuing it would yank the work out from
under a running agent.

The replay, because the event path fires on `issues: [labeled]` and an applied
command *changes a label*. Replaying there would wake this workflow up to
re-apply the very comment `gatekeeper-comment` is applying at that moment, and
nothing serialises the two — they are in different concurrency groups.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import os
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

_RECONCILE_SKILL = Path(__file__).resolve().parents[1] / "pipeline-reconcile"
if str(_RECONCILE_SKILL) not in sys.path:
    sys.path.insert(0, str(_RECONCILE_SKILL))

import apply_actions  # noqa: E402
import check_revisits  # noqa: E402
import fire_routine  # noqa: E402
import reconcile  # noqa: E402
import run_comment_event  # noqa: E402
from _github_api import (  # noqa: E402
    comment as post_comment,
    github_api,
    rerender_dashboard,
    set_labels,
)

# How far back the replay looks. The cron is six-hourly, so anything past about
# twelve hours already covers a single dropped webhook; a week covers a
# multi-day Actions outage as well. Widening costs almost nothing — a comment
# that was applied carries the 👀 and re-reading it is a no-op — but it is
# bounded rather than unlimited, because an unbounded scan of the repository's
# whole comment history is both slow and a far larger surface for a watermark
# bug to act on.
REPLAY_WINDOW_DAYS = 7

_ISSUE_URL_NUMBER = re.compile(r"/issues/(\d+)$")


def comments_query(now=None) -> str:
    """The one call that gathers the replay's candidates.

    Repo-wide and `since`-filtered, so it is a single paged request rather than
    one per open issue. Ascending by creation date, so two `/focus` commands in
    the same window are replayed in the order they were typed and the later one
    wins — which is what would have happened had both webhooks arrived.
    """
    since = (now or datetime.now(timezone.utc)) - timedelta(days=REPLAY_WINDOW_DAYS)

    return (f"/issues/comments?since={since.strftime('%Y-%m-%dT%H:%M:%SZ')}"
            "&per_page=100&sort=created&direction=asc")


class CollectedRender:
    """Stands in for the dashboard render while comments are being replayed.

    `run_comment_event.run` re-renders after its own label writes, which is
    right for one webhook and wrong for a sweep: six replayed commands would
    publish six boards, five of them describing a half-finished sweep. So the
    replay records what each command wanted and the sweep renders once, at the
    end, with the last override to arrive.
    """

    def __init__(self) -> None:
        self.focus = None
        self.cap = None
        self.wanted = False

    def __call__(self, focus_override=None, cap_override=None) -> None:
        self.wanted = True
        if focus_override is not None:
            self.focus = focus_override
        if cap_override is not None:
            self.cap = cap_override


def _issue_number(comment: dict):
    match = _ISSUE_URL_NUMBER.search((comment.get("issue_url") or "").split("?")[0])
    return int(match.group(1)) if match else None


def _as_event(issue: dict, comment: dict) -> dict:
    """The snapshot issue plus one raw comment, in webhook shape.

    `fetch_comment_event.build` reads GitHub's JSON, so the replay hands it
    GitHub's JSON rather than teaching it a second shape to understand.
    """
    return {
        "issue": {
            "number": issue["number"],
            "labels": [{"name": name} for name in issue.get("labels") or []],
            "body": issue.get("body") or "",
            "milestone": {"title": issue["milestone"]} if issue.get("milestone") else None,
        },
        "comment": comment,
    }


def replay_comments(api, state, owner, render, fire=None) -> tuple[list, list]:
    """Re-apply comment commands the comment workflow never saw.

    `gatekeeper-comment` fires on `issue_comment: created`. A dropped delivery,
    a workflow that fails to start, a run cancelled mid-flight — and the command
    is simply lost: Derek types `/approve`, nothing happens, and nothing
    anywhere reports that nothing happened.

    **Every command goes back through `run_comment_event.run`**, the same
    function the live workflow calls, so the gates, the owner re-check, the
    acknowledgement and above all the watermark rule are inherited rather than
    re-derived. That rule — a failed reaction lookup counts as *already*
    watermarked — is what keeps the failure direction safe, and a second
    implementation that got it backwards would re-apply every command in the
    window on every sweep, six times a day, forever.

    Returns the issues whose commands applied, and the issues whose replay
    raised.
    """
    if not owner:
        # An empty owner would match an empty author, and nothing here could be
        # attributed to Derek anyway. Skip rather than guess.
        return [], []

    open_issues = {
        issue["number"]: issue
        for issue in state.get("issues") or []
        if issue.get("state", "open") == "open"
    }

    replayed: list = []
    failed: list = []

    for raw in state.get("recent_comments") or []:
        issue = open_issues.get(_issue_number(raw))
        if issue is None:
            # A closed issue, a pull request, or something the snapshot does
            # not have. `/issues/comments` is repo-wide and returns all three.
            continue

        try:
            result = run_comment_event.run(
                _as_event(issue, raw), api, owner, rerender=render, fire=fire)
        except Exception:  # noqa: BLE001 - one comment, not the whole sweep
            # Reconcile is the backstop for the entire board. Losing it because
            # one comment blew up would trade a missed command for a missed
            # sweep, which is the more expensive of the two.
            failed.append(issue["number"])
            continue

        # Back onto the snapshot, before reconcile reads it. Reconcile derives
        # its fixes from `issue["labels"]`, so a stale list here has it "fix"
        # the state the command just set — silently undoing the very command
        # the replay existed to honour, and reporting a success while doing it.
        if result.labels is not None:
            issue["labels"] = list(result.labels)

        if result.applied:
            replayed.append(issue["number"])

    return replayed, failed


def apply_revisits(api, issues, snapshot, fire=None) -> list:
    """Wake issues whose blockers have all cleared.

    A state-derived transition, not a command — nothing else would ever wake an
    issue set aside only because it was blocked.
    """
    woken = []

    for revisit in check_revisits.find_revisits(issues, snapshot):
        issue = next(i for i in issues if i["number"] == revisit.issue_number)
        labels = [
            label for label in issue.get("labels") or []
            if label not in revisit.remove_labels
        ] + revisit.add_labels

        set_labels(api, revisit.issue_number, labels)
        post_comment(api, revisit.issue_number, revisit.comment)

        # A woken issue lands back in `ai-triage` and needs analyzing. Without
        # this it waits for the next scheduled round, which is the whole
        # latency the reactive fire exists to remove.
        if apply_actions.fires_triage(issue.get("labels") or [], labels):
            (fire or fire_routine.fire_from_env)(revisit.issue_number)

        woken.append(revisit.issue_number)

    return woken


def apply_fixes(api, findings, issues, fire=None) -> list:
    """Apply the auto-fix findings. Flags are left for the dashboard."""
    fixed = []

    for finding in findings:
        if finding.get("action") != "auto-fix":
            continue

        number = finding["issue"]
        issue = next((i for i in issues if i["number"] == number), None)
        if issue is None:
            continue

        labels = set(issue.get("labels") or [])
        labels -= set(finding.get("remove_labels") or [])
        labels |= set(finding.get("add_labels") or [])

        set_labels(api, number, sorted(labels))

        if apply_actions.fires_triage(issue.get("labels") or [], sorted(labels)):
            (fire or fire_routine.fire_from_env)(number)

        fixed.append(number)

    return fixed


def run(state: dict, api, events_only: bool = False, rerender=None,
        fire=None, owner=None) -> dict:
    """One sweep. Returns what it did, for the workflow log."""
    issues = state.get("issues") or []
    snapshot = state.get("snapshot") or {}
    owner = owner if owner is not None else state.get("owner")

    # **At most one fire per issue per sweep.** A revisit wakes an issue into
    # `ai-triage` without updating the in-memory copy reconcile then reads, so
    # the same issue can look newly-triageable twice in one pass. Two fires are
    # two triage sessions racing on one issue, each posting its own plan.
    sink = fire or fire_routine.fire_from_env
    already_fired = set()

    def fire_once(number):
        if number in already_fired:
            return
        already_fired.add(number)
        sink(number)

    # First, and cron-only. First because everything after it reasons about
    # `issue["labels"]`, and a replayed command is the newest thing anyone said
    # about that issue; cron-only because of the race described at the top of
    # this file.
    collected = CollectedRender()
    replayed: list = []
    replay_errors: list = []

    if not events_only:
        replayed, replay_errors = replay_comments(
            api, state, owner, collected, fire=fire_once)

    woken = apply_revisits(api, issues, snapshot, fire=fire_once)

    findings = reconcile.process({
        "issues": issues,
        "pulls": state.get("pulls") or [],
        "merged_commits": state.get("merged_commits") or [],
        "focus": state.get("focus"),
        "events_only": events_only,
    })["findings"]

    fixed = apply_fixes(api, findings, issues, fire=fire_once)

    # Once, at the end, for the whole sweep — replay included. The board should
    # describe the state the sweep left behind, not one of the states it passed
    # through.
    if woken or fixed or collected.wanted:
        rerender = rerender or (lambda **kwargs: rerender_dashboard(api, **kwargs))
        rerender(focus_override=collected.focus, cap_override=collected.cap)

    return {"replayed": replayed, "replay_errors": replay_errors,
            "woken": woken, "fixed": fixed, "findings": findings}


def fetch_state(api, focus=None) -> dict:  # pragma: no cover - network shape
    """Build the sweep's snapshot from the API.

    Separate from the dashboard's fetch because the sweep needs four things the
    renderer does not: open PRs, recent merged commits on `main`, the state of
    every issue named as a blocker, and the recent comments to replay.
    """
    from _github_api import paged  # noqa: PLC0415

    get_all = paged()
    # Paginated: a single page is 100 items and GitHub says nothing when it
    # truncates, so an unpaginated sweep would simply not see older issues.
    raw = get_all("/issues?state=all&per_page=100")

    issues = []
    for item in raw:
        if "pull_request" in item:
            continue

        number = item["number"]
        issues.append({
            "number": number,
            "title": item.get("title", ""),
            "state": item.get("state", "open"),
            "labels": [label["name"] for label in item.get("labels") or []],
            "milestone": (item.get("milestone") or {}).get("title"),
            "body": item.get("body") or "",
            "native_blockers": _blocked_by(api, number),
            "comments": _comments(api, number),
        })

    by_number = {issue["number"]: issue for issue in issues}

    return {
        "issues": issues,
        "pulls": [
            {"number": p["number"], "state": p.get("state", "open"),
             "body": p.get("body") or ""}
            for p in get_all("/pulls?state=open&per_page=100")
        ],
        # Deliberately ONE page, unlike the lists above. This answers "did this
        # land recently", and the whole history of `main` cannot change that
        # answer — an issue whose commit is 500 merges back was reconciled long
        # ago. Paginating here would grow without bound for no new information.
        "merged_commits": [
            {"body": c.get("commit", {}).get("message", "")}
            for c in api("GET", "/commits?sha=main&per_page=100") or []
        ],
        # The replay's candidates: one repo-wide, `since`-filtered, paged call
        # rather than one request per open issue. Raw, exactly as GitHub
        # returned them, because `fetch_comment_event.build` reads that shape.
        "recent_comments": get_all(comments_query()),
        # Every issue's own record doubles as the blocker snapshot.
        "snapshot": by_number,
        "focus": focus,
    }


def _blocked_by(api, number) -> list:  # pragma: no cover - network shape
    try:
        edges = api("GET", f"/issues/{number}/dependencies/blocked_by") or []
    except Exception:  # noqa: BLE001 - unknown, not "none"
        return []
    return [edge["number"] for edge in edges if isinstance(edge.get("number"), int)]


def _comments(api, number) -> list:
    # `id` is carried because the watermark is keyed on it — dropping it here
    # left the sweep holding comments it could not have claimed.
    return [
        {"id": c.get("id"),
         "body": c.get("body") or "",
         "author": (c.get("user") or {}).get("login", ""),
         "created_at": c.get("created_at", "")}
        for c in api("GET", f"/issues/{number}/comments?per_page=100") or []
    ]


def main(argv=None) -> int:  # pragma: no cover - the workflow entry point
    import json
    import os

    argv = sys.argv[1:] if argv is None else argv
    api = github_api()

    # `--fetch` is how the workflow runs it; stdin is how you rehearse a sweep
    # against a snapshot you captured earlier.
    if "--fetch" in argv:
        state = fetch_state(api, focus=os.environ.get("PIPELINE_FOCUS"))
    else:
        state = json.load(sys.stdin)

    result = run(state, api, events_only="--events-only" in argv,
                 owner=os.environ.get("PIPELINE_OWNER", ""))

    print(f"replayed {len(result['replayed'])} "
          f"({len(result['replay_errors'])} failed), "
          f"woke {len(result['woken'])}, fixed {len(result['fixed'])}, "
          f"flagged {sum(1 for f in result['findings'] if f['action'] == 'flag')}")

    for number in result["replay_errors"]:
        print(f"  replay of a comment on #{number} raised — see the run log")

    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
