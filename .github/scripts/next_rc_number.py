#!/usr/bin/env python3
"""Work out which release candidate this build is.

While a release PR is open, every push to it produces an `rcN` build. The number
is **derived**, never chosen: nobody has to remember where they got to, and two
pushes cannot both claim `rc3`.

release-please's own prerelease support cannot do this — it bumps on releases,
not on pushes to an open PR — so N is the number of rc-build runs on the release
branch since that PR opened. Counting from the PR's open time is what makes a
fresh release PR restart at `rc1` rather than continuing from the last release's
numbering.

Usage:
    python3 .github/scripts/next_rc_number.py --snapshot snapshot.json

where the snapshot is:

    {
      "pr_created_at": "2026-08-08T10:00:00Z",
      "this_run_id": 102,
      "runs": [{"id": 100, "created_at": "2026-08-08T10:05:00Z"}, ...]
    }

Prints the number. Stdlib only.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime


def _timestamp(value: str | None, what: str) -> datetime:
    if not value:
        raise ValueError(
            f"The snapshot has no {what}. Guessing one would silently renumber every "
            "release candidate, so this is an error."
        )

    try:
        # GitHub sends "...Z"; fromisoformat wants an offset before 3.11.
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(f"The {what} '{value}' is not an ISO-8601 timestamp.") from error


def next_rc_number(snapshot: dict) -> int:
    """The rc number for the run described by `snapshot`."""
    opened = _timestamp(snapshot.get("pr_created_at"), "pr_created_at")
    this_run_id = snapshot.get("this_run_id")

    since_opened = []
    this_run_created: datetime | None = None

    for run in snapshot.get("runs") or []:
        created = _timestamp(run.get("created_at"), f"created_at for run {run.get('id')}")
        if created < opened:
            continue
        since_opened.append((created, run.get("id")))
        if run.get("id") == this_run_id:
            this_run_created = created

    # Only runs at or before this one count. A re-run of an older build must not
    # renumber itself above a newer sibling, which would make two artifacts
    # claim the same rc and neither be the later one.
    if this_run_created is not None:
        earlier = [entry for entry in since_opened if entry[0] <= this_run_created]
        return len(earlier)

    # This run is not in the list — the Actions API lags, and a run querying for
    # itself often cannot see itself yet. Count what is there and add ourselves.
    return len(since_opened) + 1


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", required=True, help="JSON file, or - for stdin")
    arguments = parser.parse_args(argv)

    if arguments.snapshot == "-":
        snapshot = json.load(sys.stdin)
    else:
        with open(arguments.snapshot, encoding="utf-8") as handle:
            snapshot = json.load(handle)

    print(next_rc_number(snapshot))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
