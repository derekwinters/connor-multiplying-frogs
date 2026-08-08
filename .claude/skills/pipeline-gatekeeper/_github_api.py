#!/usr/bin/env python3
"""REST helpers for the gatekeeper, and the shared dashboard re-render.

Thin on purpose. Everything with a decision in it lives in the pure modules —
`parse_commands`, `gates`, `apply_actions`, `check_revisits` — which are
testable without a network. This file is the part that cannot be, so the less
of it there is, the better.

**`GITHUB_TOKEN` only, never a PAT.** The workflow token is scoped to this
repository and expires with the job. A personal access token would carry
whatever the person who made it can do, into a job nobody is watching.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.request
from pathlib import Path

API_ROOT = "https://api.github.com"

_DASHBOARD_SKILL = Path(__file__).resolve().parents[1] / "pipeline-dashboard"
if str(_DASHBOARD_SKILL) not in sys.path:
    sys.path.insert(0, str(_DASHBOARD_SKILL))


def github_api(token=None, repository=None):
    """A callable `api(method, path, body=None)` bound to this repository.

    Returned as a closure so every pure module can take `api` as an argument
    and be tested with a fake.
    """
    token = token or os.environ["GITHUB_TOKEN"]
    repository = repository or os.environ["GITHUB_REPOSITORY"]

    def api(method: str, path: str, body=None):
        url = f"{API_ROOT}/repos/{repository}{path}"
        data = json.dumps(body).encode() if body is not None else None

        request = urllib.request.Request(url, data=data, method=method)
        request.add_header("Authorization", f"Bearer {token}")
        request.add_header("Accept", "application/vnd.github+json")
        if data:
            request.add_header("Content-Type", "application/json")

        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read().decode() or "null"
            return json.loads(raw)

    return api


PER_PAGE = 100

# A backstop, not a limit anyone should reach: 100 pages is 10,000 items. It
# exists so a bug in the stop condition cannot spin forever against the API.
MAX_PAGES = 100


def collect_pages(fetch_page, per_page: int = PER_PAGE, max_pages: int = MAX_PAGES) -> list:
    """Every item across every page, given a `fetch_page(n)` returning one page.

    **Every list endpoint in this pipeline must go through this.** GitHub caps
    a page at 100 items and says nothing when it truncates — the response is a
    well-formed list that happens to be missing everything after the hundredth
    item.

    On this repository that is not hypothetical. `/issues?state=all` returns
    100 items of which most are pull requests, so a single-page read saw 28
    issues out of far more. A board rendered from that is quietly, plausibly
    wrong, and the pie still adds up — against the wrong total.

    Stops on the first short page. Page numbers rather than the `Link` header's
    `next` URL, because that URL uses an opaque `/repositories/{id}/` form and
    keeping every request on `/repos/{owner}/{repo}/` is both simpler to reason
    about and easier to fake in a test.
    """
    items: list = []

    for page in range(1, max_pages + 1):
        batch = fetch_page(page) or []
        items.extend(batch)

        if len(batch) < per_page:
            return items

    return items


def paged(token=None, repository=None):
    """A `get_all(path)` that reads every page of a list endpoint."""
    token = token or os.environ["GITHUB_TOKEN"]
    repository = repository or os.environ["GITHUB_REPOSITORY"]

    def get_all(path: str) -> list:
        separator = "&" if "?" in path else "?"

        def fetch_page(page: int):
            url = f"{API_ROOT}/repos/{repository}{path}{separator}page={page}"
            request = urllib.request.Request(url, method="GET")
            request.add_header("Authorization", f"Bearer {token}")
            request.add_header("Accept", "application/vnd.github+json")

            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode() or "[]")

        return collect_pages(fetch_page)

    return get_all


def set_labels(api, issue_number: int, labels) -> None:
    api("PUT", f"/issues/{issue_number}/labels", {"labels": sorted(set(labels))})


def set_milestone(api, issue_number: int, milestone_number) -> None:
    api("PATCH", f"/issues/{issue_number}", {"milestone": milestone_number})


def comment(api, issue_number: int, body: str) -> None:
    api("POST", f"/issues/{issue_number}/comments", {"body": body})


def watermark(api, comment_id: int) -> None:
    """Leave the 👀 so this comment is never reconsidered."""
    api("POST", f"/issues/comments/{comment_id}/reactions", {"content": "eyes"})


def rerender_dashboard(api, focus_override=None, cap_override=None) -> None:
    """Re-render the dashboard, optionally with a new focus or cap.

    **This is how `/focus` and `/cap` persist.** The markers live in the
    dashboard body, and the body is regenerated wholesale on every render — so
    the only way to change a marker and have it survive is to render with the
    override. Hand-editing the body would work exactly once, until the next
    render overwrote it.

    Called **once**, after all label writes. Rendering between them would
    produce a board describing a half-applied command.
    """
    import render_dashboard  # noqa: PLC0415 - path is set up above

    state = _fetch_state(api)
    body = render_dashboard.render(
        state, focus_override=focus_override, cap_override=cap_override)

    api("PATCH", f"/issues/{state['dashboard_issue']['number']}", {"body": body})


def _fetch_state(api, get_all=None) -> dict:  # pragma: no cover - network shape
    """Everything the renderer needs, in the shape it expects.

    Paginated. A single-page read of `/issues` on this repository returns 100
    items of which most are pull requests, so the board would silently be
    missing everything past the first page.
    """
    get_all = get_all or paged()
    raw_issues = get_all("/issues?state=all&per_page=100")
    milestones = get_all("/milestones?state=open&per_page=100")

    issues = [
        {
            "number": issue["number"],
            "title": issue.get("title", ""),
            "state": issue.get("state", "open"),
            "labels": [label["name"] for label in issue.get("labels") or []],
            "milestone": (issue.get("milestone") or {}).get("title"),
            "body": issue.get("body") or "",
            "native_blockers": [],
        }
        for issue in raw_issues
        if "pull_request" not in issue
    ]

    dashboard = next(
        (i for i in issues if "dashboard" in i["labels"] and i["state"] == "open"), None)

    return {
        "issues": issues,
        "milestones": [{"title": m["title"], "description": m.get("description", "")}
                       for m in milestones],
        "dashboard_issue": {
            "number": dashboard["number"] if dashboard else None,
            "body": _issue_body(api, dashboard["number"]) if dashboard else "",
        },
        "reconcile_findings": [],
    }


def _issue_body(api, number) -> str:  # pragma: no cover - network shape
    return (api("GET", f"/issues/{number}") or {}).get("body") or ""
