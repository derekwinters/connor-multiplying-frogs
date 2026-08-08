#!/usr/bin/env python3
"""Milestone operations, because the GitHub MCP toolset has none.

There is no milestone CRUD in the MCP tools at all — no list, no close, no
count. Anything that needs a milestone number, or needs to know how much is left
in one, comes through here.

    python3 milestone_ops.py list
    python3 milestone_ops.py number --title v0.1
    python3 milestone_ops.py count  --title v0.0.1
    python3 milestone_ops.py close  --title v0.0.1
    python3 milestone_ops.py reopen --title v0.0.1

Stdlib only. Needs GITHUB_TOKEN and GITHUB_REPOSITORY.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

# The marker that stops triage routing new issues into a settled milestone.
# See docs/intro/conventions.md.
FROZEN_MARKER = "FROZEN"


class MilestoneError(Exception):
    """Something the caller has to fix."""


def github_api(token: str, repository: str, api_url: str = "https://api.github.com"):
    def call(method: str, path: str, payload: dict | None = None):
        url = f"{api_url}/repos/{repository}{path}"
        data = json.dumps(payload).encode() if payload is not None else None
        request = urllib.request.Request(url, data=data, method=method)
        request.add_header("Authorization", f"Bearer {token}")
        request.add_header("Accept", "application/vnd.github+json")
        request.add_header("X-GitHub-Api-Version", "2022-11-28")
        if data is not None:
            request.add_header("Content-Type", "application/json")

        with urllib.request.urlopen(request) as response:
            body = response.read()

        return json.loads(body) if body else None

    return call


def fetch(api, state: str = "all") -> list[dict]:
    """Every milestone. Always read live — never from a list in the docs."""
    return api("GET", f"/milestones?state={state}&per_page=100") or []


def find(api, title: str) -> dict:
    """The milestone with exactly this title.

    Compared **exactly**. `v0.1` and `V0.1` are different milestones, and
    normalising them together is how work lands in the wrong one — silently,
    because both look right in a comment.
    """
    milestones = fetch(api)

    for candidate in milestones:
        if candidate.get("title") == title:
            return candidate

    known = ", ".join(sorted(m.get("title", "?") for m in milestones)) or "none"
    raise MilestoneError(f"No milestone titled '{title}'. There is: {known}.")


def resolve_number(api, title: str) -> int:
    """Title → number.

    This is the function everything else needs. `issue_write`'s `milestone`
    parameter — and the REST API's — takes the milestone **number**, not its
    title, and passing a title is either a 422 or silently wrong.
    """
    return find(api, title)["number"]


def list_milestones(api, state: str = "all") -> list[tuple]:
    """(number, title, state, open, closed, frozen) per milestone."""
    return [
        (
            m.get("number"),
            m.get("title"),
            m.get("state"),
            m.get("open_issues", 0),
            m.get("closed_issues", 0),
            is_frozen(m.get("description")),
        )
        for m in fetch(api, state)
    ]


def open_issue_count(api, title: str) -> int:
    return find(api, title).get("open_issues", 0)


def is_frozen(description: str | None) -> bool:
    """Has this milestone been frozen to new intake?"""
    return (description or "").lstrip("*# ").startswith(FROZEN_MARKER)


def close_milestone(api, title: str, force: bool = False):
    """Close a milestone, refusing while it still has open work.

    Closing one with open issues hides that work: it stops appearing in the
    milestone view, and nothing else is watching for it. If the work really is
    abandoned, move or close the issues — that is a decision, and it should
    look like one.
    """
    found = find(api, title)
    remaining = found.get("open_issues", 0)

    if remaining and not force:
        raise MilestoneError(
            f"'{title}' still has {remaining} open issue(s). Closing it would hide them: "
            "they leave the milestone view and nothing else is watching. Move or close "
            "them first, or pass --force if that is genuinely what you mean."
        )

    return api("PATCH", f"/milestones/{found['number']}", {"state": "closed"})


def reopen_milestone(api, title: str):
    return api("PATCH", f"/milestones/{find(api, title)['number']}", {"state": "open"})


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("list", "number", "count", "close", "reopen"))
    parser.add_argument("--title", help="the milestone's exact title")
    parser.add_argument("--state", default="all", choices=("all", "open", "closed"))
    parser.add_argument("--force", action="store_true", help="close despite open issues")
    arguments = parser.parse_args(argv)

    token = os.environ.get("GITHUB_TOKEN")
    repository = os.environ.get("GITHUB_REPOSITORY")
    if not token or not repository:
        sys.exit("GITHUB_TOKEN and GITHUB_REPOSITORY must both be set.")

    api = github_api(token, repository)

    try:
        if arguments.command == "list":
            for number, title, state, open_count, closed, frozen in list_milestones(
                    api, arguments.state):
                flag = " FROZEN" if frozen else ""
                print(f"  #{number} {title} [{state}] {open_count} open, {closed} closed{flag}")
            return 0

        if not arguments.title:
            sys.exit(f"--title is required for `{arguments.command}`.")

        if arguments.command == "number":
            print(resolve_number(api, arguments.title))
        elif arguments.command == "count":
            print(open_issue_count(api, arguments.title))
        elif arguments.command == "close":
            close_milestone(api, arguments.title, arguments.force)
            print(f"Closed '{arguments.title}'.")
        else:
            reopen_milestone(api, arguments.title)
            print(f"Reopened '{arguments.title}'.")

        return 0

    except MilestoneError as error:
        sys.exit(str(error))
    except urllib.error.HTTPError as error:
        sys.exit(f"GitHub API {error.code}: {error.read().decode(errors='replace')}")


if __name__ == "__main__":
    raise SystemExit(main())
