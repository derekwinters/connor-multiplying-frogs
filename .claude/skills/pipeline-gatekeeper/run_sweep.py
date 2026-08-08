#!/usr/bin/env python3
"""The board-wide sweep: catch missed comments, wake cleared blockers, reconcile.

Runs every 15 minutes from the sweep workflow, and nightly from the reconcile
Routine. The difference between those two is one flag, and it matters:

    run_sweep.py                  # cron — everything
    run_sweep.py --events-only    # the 15-minute pass

**`--events-only` omits reconcile's two requeue fixes**, because the drift they
detect is indistinguishable from work in flight. An issue the builder picked up
ten seconds ago has `in-progress` and no PR — exactly a stall's shape — and
requeuing it would yank the work out from under a running agent.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

_RECONCILE_SKILL = Path(__file__).resolve().parents[1] / "pipeline-reconcile"
if str(_RECONCILE_SKILL) not in sys.path:
    sys.path.insert(0, str(_RECONCILE_SKILL))

import check_revisits  # noqa: E402
import reconcile  # noqa: E402
from _github_api import (  # noqa: E402
    comment as post_comment,
    github_api,
    rerender_dashboard,
    set_labels,
)


def apply_revisits(api, issues, snapshot) -> list:
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
        woken.append(revisit.issue_number)

    return woken


def apply_fixes(api, findings, issues) -> list:
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
        fixed.append(number)

    return fixed


def run(state: dict, api, events_only: bool = False, rerender=None) -> dict:
    """One sweep. Returns what it did, for the workflow log."""
    issues = state.get("issues") or []
    snapshot = state.get("snapshot") or {}

    woken = apply_revisits(api, issues, snapshot)

    findings = reconcile.process({
        "issues": issues,
        "pulls": state.get("pulls") or [],
        "merged_commits": state.get("merged_commits") or [],
        "focus": state.get("focus"),
        "events_only": events_only,
    })["findings"]

    fixed = apply_fixes(api, findings, issues)

    # Once, at the end. The board should describe the state the sweep left
    # behind, not one of the states it passed through.
    if woken or fixed:
        rerender = rerender or (lambda **kwargs: rerender_dashboard(api, **kwargs))
        rerender()

    return {"woken": woken, "fixed": fixed, "findings": findings}


def main(argv=None) -> int:  # pragma: no cover - the workflow entry point
    import json

    argv = sys.argv[1:] if argv is None else argv
    state = json.load(sys.stdin)

    result = run(state, github_api(), events_only="--events-only" in argv)

    print(f"woke {len(result['woken'])}, fixed {len(result['fixed'])}, "
          f"flagged {sum(1 for f in result['findings'] if f['action'] == 'flag')}")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
