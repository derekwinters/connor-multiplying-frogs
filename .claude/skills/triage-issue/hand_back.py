#!/usr/bin/env python3
"""The hand-back write: post the analysis, then move the state label.

Triage's last step used to be prose in a skill file — "remove `ai-triage` in the
same write that adds the new state" — performed by a model at the end of a long
analysis. When it was skipped nothing failed: the comment was posted, so the run
looked successful from outside, and the issue sat on `ai-triage` waiting for a
round that would analyze it all over again.

So the step is code now. `apply()` either does both writes, in the right order,
or refuses and does neither.

    python3 hand_back.py --issue 47 --state pending-approval --analysis plan.md

**Comment first, then the label. Always.** If a run dies between the two writes:

| Order | Dies halfway | Recoverable? |
| --- | --- | --- |
| comment → label | a plan sitting on `ai-triage` | yes — the next run redoes it |
| label → comment | `pending-approval` with no plan | no — it waits on nothing |

The second is silent: Derek opens an issue awaiting approval and there is
nothing to approve, while the pipeline believes it is handled.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_SKILLS = Path(__file__).resolve().parents[1]
for _sibling in ("pipeline-dashboard", "pipeline-gatekeeper"):
    _path = str(_SKILLS / _sibling)
    if _path not in sys.path:
        sys.path.insert(0, _path)

from render_dashboard import COMMANDS  # noqa: E402

from triage_repair import (  # noqa: E402
    HAND_BACK_STATES,
    STATE_LABELS,
    has_analysis_signature,
)

PENDING = "pending-approval"
CLARIFY = "needs-clarification"

# What to offer on each route, in the order a reader should consider them.
#
# **Route-aware, not the whole vocabulary.** A `needs-clarification` hand-back
# has no plan on it, so `/approve` would be approving nothing — offering it
# invites a click that means something different from what it looks like.
OFFERED = {
    PENDING: ("/approve", "/revise <notes>", "/park"),
    CLARIFY: ("/revise <notes>", "/park"),
}

# How each route's footer opens — what state the issue is actually in.
LEAD = {
    PENDING: "**This is waiting on you.** Reply on this issue with:",
    CLARIFY: ("**This needs an answer before it can be planned.** Reply with the "
              "answer, then:"),
}

# The glosses come from the dashboard's list rather than a third copy of them.
# `render_dashboard.COMMANDS` pairs `/approve` with "the plan is right — build
# it"; repeating that here is how two descriptions of one command drift.
_GLOSS = dict(COMMANDS)

# The exception, and it is a real one rather than a convenience.
#
# `/revise` is glossed "the plan is not right, here is why" — true on the
# `pending-approval` route, and false on the other one, where triage wrote no
# plan at all. The command is doing double duty: it is the only way back to
# `ai-triage` that carries a note, so it is also how an answered question gets
# re-triaged. Reusing its usual gloss there would tell Derek he is rejecting
# something that does not exist.
#
# This is worth saying out loud rather than only working around: the vocabulary
# has no verb for "here is the answer, look again". `docs/engineering/
# issue-pipeline.md` twice named a `/retriage` for this, which has never
# existed — `parse_commands.COMMANDS` has no such entry and typing it earns the
# unknown-command reaction.
ROUTE_GLOSS = {
    (CLARIFY, "/revise"): "send it back to triage with your answer attached",
}


def _gloss(offer: str, state: str) -> str:
    """What this command does *on this route*, in the dashboard's words by default."""
    name = offer.split()[0]

    override = ROUTE_GLOSS.get((state, name))
    if override:
        return override

    return _GLOSS.get(offer) or _GLOSS[name]


def footer(state: str) -> str:
    """The commands that make sense on this route, and what each one does."""
    if state not in OFFERED:
        raise ValueError(
            f"no footer for {state!r} — triage only hands back to "
            f"{', '.join(HAND_BACK_STATES)}")

    # A colon rather than a dash: several glosses contain an em-dash of their
    # own, and `/approve — the plan is right — build it` is a line nobody parses.
    lines = [LEAD[state], ""]
    lines += [f"- `{offer}`: {_gloss(offer, state)}" for offer in OFFERED[state]]

    return "\n".join(lines)


def build_comment(analysis: str, state: str) -> str:
    """The analysis with its footer, ready to post.

    Refuses an analysis the recognizer would not match. A hand-back comment that
    does not read as one is the state `triage_repair` cannot repair and
    `pipeline-reconcile` requeues — better to fail here, loudly, than to write
    an issue into it.
    """
    if state not in OFFERED:
        raise ValueError(f"{state!r} is not a hand-back state")

    if not has_analysis_signature(analysis):
        raise ValueError(
            "this analysis carries neither a `## Build checklist` heading nor "
            "the `❓ Needs from Derek/Connor:` marker, so nothing downstream "
            "will recognize it as a triage analysis")

    return f"{analysis.rstrip()}\n\n---\n\n{footer(state)}\n"


def plan_labels(labels, state: str) -> list:
    """The label set after the hand-back — one state label, everything else kept.

    A *replacement*, never an addition. An issue carrying both `ai-triage` and
    `pending-approval` is one the next analysis round picks up and triages
    again, while it sits waiting for Derek.
    """
    if state not in OFFERED:
        raise ValueError(
            f"triage may not set {state!r} — only {', '.join(HAND_BACK_STATES)}. "
            "`ready-for-work` is Derek's, via `/approve`.")

    kept = [label for label in labels or [] if label not in STATE_LABELS]

    return sorted(set(kept) | {state})


def apply(api, issue_number: int, analysis: str, labels, state: str) -> str:
    """Post the hand-back comment, then move the label. Returns the body posted.

    Both refusals happen before either write, so a rejected hand-back leaves the
    issue exactly as it was rather than half-written.
    """
    body = build_comment(analysis, state)
    planned = plan_labels(labels, state)

    api("POST", f"/issues/{issue_number}/comments", {"body": body})
    api("PUT", f"/issues/{issue_number}/labels", {"labels": planned})

    return body


def main(argv=None) -> int:  # pragma: no cover - the command-line entry point
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--issue", type=int, required=True)
    parser.add_argument("--state", required=True, choices=list(HAND_BACK_STATES))
    parser.add_argument("--analysis", required=True,
                        help="path to the analysis markdown, or - for stdin")
    parser.add_argument("--dry-run", action="store_true",
                        help="print the comment and the label set, write nothing")
    args = parser.parse_args(argv)

    analysis = (sys.stdin.read() if args.analysis == "-"
                else Path(args.analysis).read_text(encoding="utf-8"))

    from _github_api import github_api  # local: the network half

    if args.dry_run:
        print(build_comment(analysis, args.state))
        print("labels:", ", ".join(plan_labels([], args.state)))
        return 0

    api = github_api()
    issue = api("GET", f"/issues/{args.issue}")
    labels = [label["name"] for label in issue.get("labels") or []]

    apply(api, args.issue, analysis, labels, args.state)
    print(f"#{args.issue} handed back: {args.state}")

    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
