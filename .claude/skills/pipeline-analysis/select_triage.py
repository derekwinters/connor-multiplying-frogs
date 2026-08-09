#!/usr/bin/env python3
"""Which issues are eligible for triage, and what context each one carries.

Pure `process(data)` plus a stdin/stdout `main()`, like the other pipeline
scripts. Deciding *what to triage* is deterministic and unit-testable; deciding
*how to triage it* is the `dw-triage-issue` skill's job, and that separation is
what keeps a bad triage contained to one issue instead of one batch.

    echo '{"issues": [...], "owner": "..."}' | python3 select_triage.py

Stdlib only.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import re
import sys

TRIAGE_LABEL = "ai-triage"

# Excluded even when carrying ai-triage. An epic is a container — its children
# are the work. The dashboard is the pipeline's own furniture. And `parked` is a
# decision the owner made, which triage does not get to overrule.
EXCLUDED_LABELS = ("type:epic", "dashboard", "parked")

# The owner commands that leave triage something to read: what was wrong last
# time, or permission to design.
NOTE_COMMANDS = ("revise", "redo", "propose")

NOTE_LINE = re.compile(
    r"^\s{0,3}/(" + "|".join(NOTE_COMMANDS) + r")\b\s*(.*?)\s*$",
    re.IGNORECASE | re.MULTILINE,
)


def _latest_note(comments, owner: str):
    """The most recent `/revise`, `/redo`, or `/propose` from the owner.

    Only the latest: an issue revised twice should be triaged against the
    current feedback, not a conversation. Only the owner's, for the same reason
    the parser has a bad-actor gate — a stranger cannot steer triage either.
    """
    best = None

    for comment in comments or []:
        if (comment.get("author") or "").lower() != (owner or "").lower():
            continue

        for command, text in NOTE_LINE.findall(comment.get("body") or ""):
            candidate = {
                "command": command.lower(),
                "text": text,
                "at": comment.get("created_at") or "",
            }
            # Compared by timestamp rather than position, because the caller's
            # ordering is not guaranteed.
            if best is None or candidate["at"] >= best["at"]:
                best = candidate

    return best


def is_eligible(issue: dict) -> bool:
    labels = set(issue.get("labels") or [])

    return (
        issue.get("state", "open") == "open"
        and TRIAGE_LABEL in labels
        and not labels.intersection(EXCLUDED_LABELS)
    )


def process(data: dict) -> dict:
    owner = data.get("owner", "")
    eligible = []

    for issue in sorted(data.get("issues") or [], key=lambda i: i.get("number", 0)):
        if not is_eligible(issue):
            continue

        eligible.append({
            "number": issue["number"],
            "milestone": issue.get("milestone"),
            "note": _latest_note(issue.get("comments"), owner),
        })

    return {"eligible": eligible, "count": len(eligible)}


def main(argv=None) -> int:
    json.dump(process(json.load(sys.stdin)), sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
