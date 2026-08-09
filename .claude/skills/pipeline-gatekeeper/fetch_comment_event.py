#!/usr/bin/env python3
"""Turn an `issue_comment` webhook payload into the snapshot the parser reads.

All the I/O and all the payload-shape guesswork lives here, so
`parse_commands.py` stays pure and testable. The parser should never have to
know what GitHub's JSON looks like.

The snapshot:

    {
      "issue":   {number, labels, body, milestone, blockers, is_dashboard},
      "comment": {id, body, author, watermarked}
    }

Returns **None** when the event should not be acted on at all.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import sys
from pathlib import Path

# One recognizer for `Blocked by #N` and one union with the native edges,
# shared with the skill that documents the format. See #147. This is the one
# caller that fetches its natives live rather than reading them off a
# pre-built snapshot, which is why it calls `union_blockers` directly.
_BLOCKERS_SKILL = Path(__file__).resolve().parents[1] / "issue-blockers"
if str(_BLOCKERS_SKILL) not in sys.path:
    sys.path.insert(0, str(_BLOCKERS_SKILL))

from blocker_refs import union_blockers  # noqa: E402

DASHBOARD_LABEL = "dashboard"
WATERMARK = "eyes"


def _labels(issue: dict) -> list[str]:
    return [label.get("name", "") for label in (issue.get("labels") or [])]


def _native_blockers(api, issue_number: int) -> set[int]:
    """Native blocked-by edges, or nothing if the lookup fails.

    A failed dependency lookup must not lose the whole event: the command is
    probably `/park`, which does not care. The gates that *do* care see the
    smaller list and refuse, which is the safe direction.
    """
    try:
        edges = api("GET", f"/issues/{issue_number}/dependencies/blocked_by") or []
    except Exception:  # noqa: BLE001 - any failure means "unknown"
        return set()

    return {edge["number"] for edge in edges if isinstance(edge.get("number"), int)}


def _is_watermarked(api, comment_id: int, bot_login: str) -> bool:
    """Has the gatekeeper already claimed this comment?

    Only its **own** 👀 counts. A human reacting out of interest must not
    silence a command.
    """
    try:
        reactions = api("GET", f"/issues/comments/{comment_id}/reactions") or []
    except Exception:  # noqa: BLE001
        # Unknown is not "unclaimed": re-applying a command is worse than
        # skipping one, and the sweep will pick it up next time.
        return True

    return any(
        reaction.get("content") == WATERMARK
        and (reaction.get("user") or {}).get("login", "").lower() == bot_login.lower()
        for reaction in reactions
    )


def build(event: dict, api, owner: str, bot_login: str = "github-actions[bot]") -> dict | None:
    """The snapshot for one comment event, or None if it should be ignored."""
    issue = (event or {}).get("issue") or {}
    comment = (event or {}).get("comment") or {}

    if not comment or not issue.get("number"):
        return None

    # Defence in depth. The workflow filters these too, but a snapshot the
    # parser *could* act on is one it eventually will.
    if issue.get("pull_request"):
        return None

    user = comment.get("user") or {}
    if user.get("type") == "Bot":
        return None

    labels = _labels(issue)

    return {
        "issue": {
            "number": issue["number"],
            "labels": labels,
            "body": issue.get("body") or "",
            "milestone": (issue.get("milestone") or {}).get("title"),
            "blockers": union_blockers(
                issue.get("body"), _native_blockers(api, issue["number"])),
            "is_dashboard": DASHBOARD_LABEL in labels,
        },
        "comment": {
            "id": comment.get("id"),
            "body": comment.get("body") or "",
            "author": user.get("login", ""),
            "watermarked": _is_watermarked(api, comment.get("id"), bot_login),
        },
        "owner": owner,
    }
