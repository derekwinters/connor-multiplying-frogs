#!/usr/bin/env python3
"""Apply .github/labels.yml to the repository's label set.

Idempotent: labels are matched by name, created when missing, patched when the
colour or description drifts, and left alone when they already agree. Labels
listed under `delete:` are removed if they still exist; labels under `keep:`
are documentation only and are never touched.

Environment:
    GITHUB_TOKEN       token with `issues: write` on the repository
    GITHUB_REPOSITORY  owner/repo (set automatically inside Actions)
    GITHUB_API_URL     defaults to https://api.github.com

Usage:
    python .github/scripts/sync_labels.py [--dry-run]
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

import yaml

LABELS_FILE = Path(__file__).resolve().parents[1] / "labels.yml"


class GitHub:
    def __init__(self, token: str, repo: str, api_url: str) -> None:
        self._token = token
        self._repo = repo
        self._api_url = api_url.rstrip("/")

    def _request(self, method: str, path: str, payload: dict | None = None) -> object:
        url = f"{self._api_url}/repos/{self._repo}{path}"
        data = json.dumps(payload).encode() if payload is not None else None
        request = urllib.request.Request(url, data=data, method=method)
        request.add_header("Authorization", f"Bearer {self._token}")
        request.add_header("Accept", "application/vnd.github+json")
        request.add_header("X-GitHub-Api-Version", "2022-11-28")
        if data is not None:
            request.add_header("Content-Type", "application/json")
        with urllib.request.urlopen(request) as response:
            body = response.read()
        return json.loads(body) if body else None

    def list_labels(self) -> list[dict]:
        labels: list[dict] = []
        page = 1
        while True:
            batch = self._request("GET", f"/labels?per_page=100&page={page}")
            if not batch:
                return labels
            labels.extend(batch)
            page += 1

    def create_label(self, label: dict) -> None:
        self._request("POST", "/labels", label)

    def update_label(self, name: str, label: dict) -> None:
        self._request("PATCH", f"/labels/{urllib.parse.quote(name)}", label)

    def delete_label(self, name: str) -> None:
        self._request("DELETE", f"/labels/{urllib.parse.quote(name)}")


def load_spec() -> tuple[list[dict], list[str]]:
    spec = yaml.safe_load(LABELS_FILE.read_text())
    wanted = []
    for entry in spec.get("labels") or []:
        wanted.append(
            {
                "name": entry["name"],
                "color": entry["color"].lstrip("#").lower(),
                "description": entry.get("description", ""),
            }
        )
    return wanted, list(spec.get("delete") or [])


def check_spec(wanted: list[dict]) -> None:
    """Fail loudly on the two mistakes a hand-edited taxonomy invites."""
    problems = []
    for field in ("name", "color"):
        seen: dict[str, str] = {}
        for label in wanted:
            key = label[field]
            if key in seen:
                problems.append(f"duplicate {field} {key!r}: {seen[key]} and {label['name']}")
            seen[key] = label["name"]
    problems += [f"{l['name']}: missing description" for l in wanted if not l["description"]]
    if problems:
        sys.exit("labels.yml is invalid:\n  " + "\n  ".join(problems))


def main() -> int:
    dry_run = "--dry-run" in sys.argv[1:]
    wanted, doomed = load_spec()
    check_spec(wanted)

    if dry_run:
        print(f"{len(wanted)} labels declared, {len(doomed)} marked for deletion; nothing applied")
        return 0

    token = os.environ.get("GITHUB_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")
    if not token or not repo:
        sys.exit("GITHUB_TOKEN and GITHUB_REPOSITORY must both be set")

    api = GitHub(token, repo, os.environ.get("GITHUB_API_URL", "https://api.github.com"))
    existing = {label["name"]: label for label in api.list_labels()}

    for label in wanted:
        current = existing.get(label["name"])
        if current is None:
            api.create_label(label)
            print(f"created  {label['name']}")
        elif (
            current.get("color", "").lower() != label["color"]
            or (current.get("description") or "") != label["description"]
        ):
            api.update_label(label["name"], label)
            print(f"updated  {label['name']}")
        else:
            print(f"ok       {label['name']}")

    for name in doomed:
        if name in existing:
            api.delete_label(name)
            print(f"deleted  {name}")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except urllib.error.HTTPError as error:  # surface the API's own message
        sys.exit(f"GitHub API {error.code}: {error.read().decode(errors='replace')}")
