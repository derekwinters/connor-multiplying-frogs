#!/usr/bin/env python3
"""Handle one `issue_comment` event, end to end.

Snapshot → parse → gate → apply → write → re-render. The interesting parts are
all somewhere else; this file's job is to call them in the right order and to
write the results down.

The order is the part that can be wrong, so it is the part with tests.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import apply_actions  # noqa: E402
import fetch_comment_event  # noqa: E402
import fire_routine  # noqa: E402
import gates  # noqa: E402
import parse_commands  # noqa: E402
from _github_api import (  # noqa: E402
    comment as post_comment,
    github_api,
    rerender_dashboard,
    set_labels,
    watermark,
)


class Result:
    def __init__(self, applied: bool, detail: str = "", labels=None) -> None:
        self.applied = applied
        self.detail = detail
        # The label set the issue ends this pass with, or **None** when the
        # pass never got as far as computing one. The sweep's replay writes it
        # back onto its in-memory snapshot, which reconcile then reads — see
        # `run_sweep.replay_comments`. `None` rather than "the labels
        # unchanged", so a caller with its own copy leaves it alone instead of
        # overwriting it with a guess.
        self.labels = list(labels) if labels is not None else None

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Result(applied={self.applied!r}, {self.detail!r})"


def run(event: dict, api, owner: str, rerender=None, fire=None) -> Result:
    """One comment, one pass. Never raises on an ordinary refusal."""
    snapshot = fetch_comment_event.build(event, api, owner)

    if snapshot is None:
        return Result(False, "not an issue comment worth acting on")

    issue = snapshot["issue"]
    comment = snapshot["comment"]

    if comment["watermarked"]:
        # Already handled. Re-applying a command is worse than skipping one.
        return Result(False, "already applied")

    # Defense in depth. The workflow filters on the author too, but a script
    # that only works because its caller filtered is one bad `if` away from
    # letting a stranger drive the pipeline.
    if (comment["author"] or "").lower() != (owner or "").lower():
        return Result(False, "not the owner")

    context = parse_commands.Context(
        owner=owner, labels=issue["labels"], is_dashboard=issue["is_dashboard"])
    outcome = parse_commands.parse(comment, context)

    actions, skips = _apply_gates(outcome, issue)

    if not actions and not skips:
        return Result(False, "no commands")

    plan = apply_actions.plan(issue["labels"], actions)
    changed_labels = sorted(set(plan.labels)) != sorted(set(issue["labels"]))

    if changed_labels:
        set_labels(api, issue["number"], plan.labels)

    # After the label write, never before: triage reads the issue's labels, and
    # firing first would hand it one that is not in `ai-triage` yet. Only when
    # `ai-triage` is NEWLY present, or a replay turns one stuck comment into a
    # triage run on every sweep.
    if apply_actions.fires_triage(issue["labels"], plan.labels):
        (fire or fire_routine.fire_from_env)(issue["number"])

    acknowledgement = apply_actions.acknowledgement(actions, skips)
    if acknowledgement:
        post_comment(api, issue["number"], acknowledgement)

    if apply_actions.should_watermark(actions, skips):
        watermark(api, comment["id"])

    # Once, after every label write. Rendering in between would publish a board
    # describing a half-applied command.
    if changed_labels or plan.focus or plan.cap is not None:
        rerender = rerender or (lambda **kwargs: rerender_dashboard(api, **kwargs))
        rerender(focus_override=plan.focus, cap_override=plan.cap)

    # `sorted(set(...))` because that is what `set_labels` actually wrote, and
    # the caller's snapshot should match the repository rather than the plan.
    return Result(bool(actions), acknowledgement, sorted(set(plan.labels)))


def _apply_gates(outcome, issue):
    """Run each action's gates; a refusal becomes a Skip, never a silent pass."""
    actions = []
    skips = list(outcome.skips)

    for action in outcome.actions:
        verdict = _check(action, issue)
        if verdict is None:
            actions.append(action)
        else:
            # `Skip` is (code, prose); `Verdict` names the same pair the other
            # way round. Swapped, the acknowledgement reads "was not applied.
            # approve-no-milestone" and — worse — `SILENT_SKIPS` gets a
            # sentence to match against codes like `not-owner`.
            skips.append(parse_commands.Skip(
                verdict.skip_reason, verdict.reason, action.command))

    return actions, skips


# `gates.GATES` names gates in the vocabulary of the docs; these are the
# functions that implement them. Mapped explicitly rather than derived from the
# name, so renaming a function cannot silently skip a gate — a missing key here
# raises, where a clever `getattr` would quietly find nothing to run.
GATE_FUNCTIONS = {
    "milestone-presence": gates.milestone_present,
    "milestone-order": gates.milestone_order_ok,
}


def _check(action, issue):
    for gate in gates.gates_for(action.command):
        verdict = GATE_FUNCTIONS[gate](issue)
        if not verdict.ok:
            return verdict
    return None


def main(argv=None) -> int:  # pragma: no cover - the workflow entry point
    event_path = os.environ.get("GITHUB_EVENT_PATH")
    event = json.loads(Path(event_path).read_text()) if event_path else json.load(sys.stdin)

    result = run(event, github_api(), owner=os.environ["PIPELINE_OWNER"])
    print(result.detail or "nothing to do")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
