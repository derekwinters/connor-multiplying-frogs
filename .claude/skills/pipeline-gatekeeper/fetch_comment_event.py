#!/usr/bin/env python3
"""Turn an `issue_comment` webhook payload into the snapshot the parser reads.

All the I/O and all the payload-shape guesswork lives here, so
`parse_commands.py` stays pure and testable. The parser should never have to
know what GitHub's JSON looks like.

The snapshot:

    {
      "issue":   {number, labels, body, milestone, blockers,
                  soft_dependencies, is_dashboard},
      "comment": {id, body, author, watermarked}
    }

`blockers` and `soft_dependencies` are **edges**, not issue numbers:

    {"number": 20, "state": "open", "milestone": "v0.1"}

The milestone-order gate reads each edge's own state and milestone to decide
whether it is scheduled before the issue that waits on it, so a list of numbers
is not something it can answer with.

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

from blocker_refs import text_depends_on, union_blockers  # noqa: E402

DASHBOARD_LABEL = "dashboard"
WATERMARK = "eyes"


def _labels(issue: dict) -> list[str]:
    return [label.get("name", "") for label in (issue.get("labels") or [])]


def _native_blockers(api, issue_number: int) -> set[int]:
    """Native blocked-by edge numbers, or nothing if the lookup fails.

    A failed dependency lookup must not lose the whole event: the command is
    probably `/park`, which does not care. The text blockers are still known,
    and an edge nobody can read still reaches the gates as unscheduled — see
    `_edges` — so the refusal, not the approval, is the degraded outcome.
    """
    try:
        edges = api("GET", f"/issues/{issue_number}/dependencies/blocked_by") or []
    except Exception:  # noqa: BLE001 - any failure means "unknown"
        return set()

    return {edge["number"] for edge in edges if isinstance(edge.get("number"), int)}


def _edge(issue: dict) -> dict:
    """One dependency edge, flattened out of GitHub's issue shape."""
    return {
        "number": issue.get("number"),
        "state": issue.get("state") or "open",
        "milestone": (issue.get("milestone") or {}).get("title"),
    }


def _unreadable(number: int) -> dict:
    """An edge we know exists but could not read: open, and unscheduled.

    Deliberately the shape that makes the milestone-order gate **refuse**.
    Approving over a dependency nobody can see is the expensive direction —
    the issue goes ready, the builder skips it every night because the blocker
    is still open, and nothing says why. A refusal costs one comment.
    """
    return {"number": number, "state": "open", "milestone": None}


def _edges(api, numbers) -> list[dict]:
    """Each issue number as an edge carrying its own state and milestone.

    One read per edge. The dependency endpoint's own payload is not trusted for
    this: a shape that happens to omit `milestone` today would read as
    unscheduled and refuse every approval, and that is not a thing to guess at.
    """
    edges = []

    for number in sorted(numbers):
        try:
            fetched = api("GET", f"/issues/{number}")
        except Exception:  # noqa: BLE001 - any failure means "unknown"
            fetched = None

        edges.append(_edge(fetched) if fetched else _unreadable(number))

    return edges


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

    blocked_by = union_blockers(issue.get("body"), _native_blockers(api, issue["number"]))
    # An issue that both blocks and is depended on is one dependency, not two.
    # It is already the stronger of the pair, so the soft reading adds nothing
    # but a second mention of it in the refusal.
    depends_on = text_depends_on(issue.get("body")) - set(blocked_by)

    return {
        "issue": {
            "number": issue["number"],
            "labels": labels,
            "body": issue.get("body") or "",
            "milestone": (issue.get("milestone") or {}).get("title"),
            "blockers": _edges(api, blocked_by),
            "soft_dependencies": _edges(api, depends_on),
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
