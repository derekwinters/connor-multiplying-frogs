#!/usr/bin/env python3
"""Watch a PR's checks until they finish, and report what happened.

**This reports. It never fixes anything.** Resolution belongs to the caller —
the development agent for its own PR, the delivery skill for one it is driving.
A watcher that also fixes is a watcher whose report you cannot trust, because it
is describing a situation it has already changed.

    python3 ci_watch.py --pr 123

Exits 0 when every check passed, 1 when any failed, 2 when they never finished
or are waiting on a human. Three exit codes because they need three different
responses.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time

PASSED = "passed"
FAILED = "failed"
PENDING = "pending"
PARKED = "parked"

# Long enough not to hammer the API, short enough that a two-minute check does
# not feel like five.
POLL_SECONDS = 15
TIMEOUT_SECONDS = 20 * 60

EXCERPT_LINES = 40

# Conclusions that are not a pass but also not a failure to fix: nothing will
# change until a person clicks approve.
PARKED_STATES = {"action_required", "waiting", "queued_pending_approval"}

# A conclusion that is not one of these is a failure. Listing the *good* ones is
# deliberate: a conclusion GitHub adds later should read as "not a pass" rather
# than being silently tolerated.
PASSING_CONCLUSIONS = {"success", "neutral", "skipped"}

# Lines worth putting in front of a human, in preference to the tail of the log.
INTERESTING = re.compile(
    r"""
    (^\s*Failed\s)            # NUnit: "  Failed SplitsAtThreshold [4 ms]"
    | (\berror\s+CS\d+)       # the C# compiler
    | (^\s*Expected:)         # an assertion's two halves
    | (^\s*But\s+was:)
    | (\b(error|Error|ERROR)\b[:\s])
    | (^\s*[#][#]\[error\])   # Actions' own marker; '#' escaped for re.VERBOSE
    | (\bTraceback\b)
    | (\bAssertionError\b)
    """,
    re.VERBOSE | re.MULTILINE,
)


class Result:
    def __init__(self, state, checks, timed_out=False):
        self.state = state
        self.checks = checks
        self.timed_out = timed_out

    @property
    def failed(self):
        return [c for c in self.checks if _is_failure(c)]

    @property
    def parked(self):
        return [c for c in self.checks if _is_parked(c)]

    @property
    def unfinished(self):
        return [c for c in self.checks if c.get("status") != "completed" and not _is_parked(c)]


def _is_parked(check: dict) -> bool:
    return check.get("status") in PARKED_STATES or check.get("conclusion") in PARKED_STATES


def _is_failure(check: dict) -> bool:
    if check.get("status") != "completed" or _is_parked(check):
        return False
    return check.get("conclusion") not in PASSING_CONCLUSIONS


def classify(checks: list[dict]) -> Result:
    """The verdict for a set of check runs."""
    if not checks:
        # Not a pass. An empty list means the checks have not registered yet,
        # and "no checks" is the worst possible answer to "is CI green".
        return Result(PENDING, checks)

    if any(_is_failure(check) for check in checks):
        # A failure outranks everything: it is the news, and it is actionable
        # now.
        return Result(FAILED, checks)

    if any(check.get("status") != "completed" and not _is_parked(check) for check in checks):
        return Result(PENDING, checks)

    if any(_is_parked(check) for check in checks):
        return Result(PARKED, checks)

    return Result(PASSED, checks)


def is_terminal(state: str) -> bool:
    """Is there any point waiting longer?

    `PARKED` is terminal: nothing changes until a human clicks, so polling
    through it just burns the timeout and then reports the same thing.
    """
    return state != PENDING


def extract_excerpt(log: str, max_lines: int = EXCERPT_LINES) -> str:
    """The part of a job log worth reading.

    Interesting lines with a little context if there are any; otherwise the
    tail, because the end of a log is where a crash usually is.
    """
    if not log.strip():
        return "(no log available — the job may have been cancelled before it started)"

    lines = log.splitlines()
    wanted: set[int] = set()

    for number, line in enumerate(lines):
        if INTERESTING.search(line):
            wanted.update(range(max(0, number - 2), min(len(lines), number + 3)))

    if not wanted:
        return "\n".join(lines[-max_lines:])

    chosen = sorted(wanted)[:max_lines]
    excerpt: list[str] = []
    previous = None

    for number in chosen:
        if previous is not None and number > previous + 1:
            excerpt.append("    …")
        excerpt.append(lines[number])
        previous = number

    return "\n".join(excerpt)


def watch(fetch, sleep=time.sleep, now=time.monotonic) -> Result:
    """Poll `fetch` until the checks reach a terminal state or time runs out."""
    started = now()

    while True:
        result = classify(fetch())

        if is_terminal(result.state):
            return result

        if now() - started >= TIMEOUT_SECONDS:
            # A timeout is not a pass and not a failure. The caller is told the
            # checks never finished, which needs a different response from them
            # having failed.
            return Result(PENDING, result.checks, timed_out=True)

        sleep(POLL_SECONDS)


def fetch_checks(pull_number: int):
    def fetch() -> list[dict]:
        completed = subprocess.run(
            ["gh", "pr", "view", str(pull_number), "--json", "statusCheckRollup"],
            check=True,
            capture_output=True,
            text=True,
        )
        payload = json.loads(completed.stdout or "{}")
        return [
            {
                "name": check.get("name") or check.get("context"),
                "status": (check.get("status") or "").lower() or "completed",
                "conclusion": (check.get("conclusion") or check.get("state") or "").lower() or None,
                "url": check.get("detailsUrl") or check.get("targetUrl"),
            }
            for check in payload.get("statusCheckRollup") or []
        ]

    return fetch


def report(result: Result) -> str:
    lines = []

    for check in sorted(result.checks, key=lambda c: c.get("name") or ""):
        if _is_failure(check):
            mark = "FAIL"
        elif _is_parked(check):
            mark = "WAIT"
        elif check.get("status") != "completed":
            mark = "...."
        else:
            mark = "ok"
        lines.append(f"  {mark:5} {check.get('name')}")

    if result.state == PASSED:
        header = f"All {len(result.checks)} checks passed."
    elif result.state == FAILED:
        header = f"{len(result.failed)} check(s) failed."
    elif result.state == PARKED:
        header = (
            f"{len(result.parked)} check(s) are waiting for approval. Nothing will "
            "change until someone approves them in the PR's Checks tab."
        )
    elif result.timed_out:
        header = (
            f"Gave up after {TIMEOUT_SECONDS // 60} minutes with "
            f"{len(result.unfinished)} check(s) still running. Not a pass, and not a "
            "failure — the checks never finished."
        )
    else:
        header = "Checks are still running."

    return header + "\n" + "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pr", type=int, required=True)
    parser.add_argument("--once", action="store_true", help="report now, do not poll")
    arguments = parser.parse_args(argv)

    fetch = fetch_checks(arguments.pr)
    result = classify(fetch()) if arguments.once else watch(fetch)

    print(report(result))

    if result.state == PASSED:
        return 0
    if result.state == FAILED:
        return 1
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
