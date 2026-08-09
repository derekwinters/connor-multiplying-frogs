#!/usr/bin/env python3
"""Read and write **native** GitHub blocked-by relationships.

A blocker written as prose — `Blocked by #42` in an issue body — is a sentence,
not a relationship. The nightly builder, the dashboard's blocked section and the
revisit sweep all read the real dependency graph and **union** a structured
`Blocked by #N` line into it, so prose is honoured on a best-effort basis and
nothing more: it is invisible to GitHub's own dependency view, it cannot be
listed or removed, and one typo makes it vanish with nothing reporting it.
Record it natively; `audit` finds the ones still written as prose.

The recognizer for that line lives in `blocker_refs.py` beside this file, and
every reader in the pipeline imports it from there.

    python3 set_blocker.py list   --issue 28
    python3 set_blocker.py add    --issue 28 --blocked-by 82
    python3 set_blocker.py remove --issue 28 --blocked-by 82
    python3 set_blocker.py audit  --issue 28

Stdlib only. Needs GITHUB_TOKEN and GITHUB_REPOSITORY.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

# The one definition of the format this skill documents. Its sibling, so a
# plain import — every other reader in the pipeline inserts this directory on
# sys.path and imports the same names.
from blocker_refs import text_blockers


class BlockerError(Exception):
    """Something the caller has to fix, not a transport failure."""


def github_api(token: str, repository: str, api_url: str = "https://api.github.com"):
    """A callable(method, path, payload) -> parsed JSON."""

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


def resolve_issue_id(api, issue_number: int) -> int:
    """The internal numeric id for an issue number.

    **This is the trap the whole module exists around.** The dependencies write
    API takes `issue_id` — the internal identifier, a ten-digit number — not the
    issue number everyone reads and writes. Passing the number produces either a
    422 or, worse, a relationship pointing at whatever issue happens to have
    that internal id.
    """
    issue = api("GET", f"/issues/{issue_number}") or {}
    issue_id = issue.get("id")

    if not isinstance(issue_id, int):
        raise BlockerError(
            f"Could not read the internal id of issue #{issue_number}. Refusing to "
            "fall back to the issue number: that is a different identifier and would "
            "silently point at the wrong issue."
        )

    return issue_id


def list_blockers(api, issue_number: int) -> list[dict]:
    """The issues blocking this one, as returned by the API."""
    return api("GET", f"/issues/{issue_number}/dependencies/blocked_by") or []


def add_blocker(api, blocked: int, blocked_by: int):
    if blocked == blocked_by:
        raise BlockerError(f"Issue #{blocked} cannot block itself.")

    issue_id = resolve_issue_id(api, blocked_by)
    return api("POST", f"/issues/{blocked}/dependencies/blocked_by", {"issue_id": issue_id})


def remove_blocker(api, blocked: int, blocked_by: int):
    issue_id = resolve_issue_id(api, blocked_by)
    return api("DELETE", f"/issues/{blocked}/dependencies/blocked_by/{issue_id}")


def prose_blockers(body: str) -> list[int]:
    """Issue numbers named as blockers in prose — which is the wrong place."""
    return sorted(text_blockers(body))


def audit(api, issue_number: int) -> list[str]:
    """Prose blockers that have no matching native relationship."""
    issue = api("GET", f"/issues/{issue_number}") or {}
    native = {blocker.get("number") for blocker in list_blockers(api, issue_number)}

    return [
        f"#{number} is named as a blocker in the body but has no native relationship. "
        f"Run: set_blocker.py add --issue {issue_number} --blocked-by {number}"
        for number in prose_blockers(issue.get("body") or "")
        if number not in native
    ]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("list", "add", "remove", "audit"))
    parser.add_argument("--issue", type=int, required=True, help="the blocked issue")
    parser.add_argument("--blocked-by", type=int, help="the blocking issue")
    arguments = parser.parse_args(argv)

    token = os.environ.get("GITHUB_TOKEN")
    repository = os.environ.get("GITHUB_REPOSITORY")
    if not token or not repository:
        sys.exit("GITHUB_TOKEN and GITHUB_REPOSITORY must both be set.")

    api = github_api(token, repository)

    try:
        if arguments.command == "list":
            blockers = list_blockers(api, arguments.issue)
            if not blockers:
                print(f"#{arguments.issue} is not blocked by anything.")
            for blocker in blockers:
                print(f"  #{blocker['number']} [{blocker['state']}] {blocker['title']}")
            return 0

        if arguments.command == "audit":
            problems = audit(api, arguments.issue)
            for problem in problems:
                print(f"  {problem}", file=sys.stderr)
            if not problems:
                print(f"#{arguments.issue}: every blocker named in the body is native.")
            return 1 if problems else 0

        if not arguments.blocked_by:
            sys.exit(f"--blocked-by is required for `{arguments.command}`.")

        if arguments.command == "add":
            add_blocker(api, arguments.issue, arguments.blocked_by)
            print(f"#{arguments.issue} is now blocked by #{arguments.blocked_by}.")
        else:
            remove_blocker(api, arguments.issue, arguments.blocked_by)
            print(f"#{arguments.issue} is no longer blocked by #{arguments.blocked_by}.")

        return 0

    except BlockerError as error:
        sys.exit(str(error))
    except urllib.error.HTTPError as error:
        sys.exit(f"GitHub API {error.code}: {error.read().decode(errors='replace')}")


if __name__ == "__main__":
    raise SystemExit(main())
