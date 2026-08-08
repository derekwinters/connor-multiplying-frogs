#!/usr/bin/env python3
"""Fire the triage Routine the moment an issue enters `ai-triage`.

Waiting until 02:00 to triage an issue filed at 09:00 is a bad experience when
the person who filed it is sitting right there. So the gatekeeper job — which is
already running — makes an **outbound POST** to a poke-only Routine.

Outbound, specifically. A workflow triggered by the token's own label change
would never run: GitHub suppresses workflow runs from events initiated by
`GITHUB_TOKEN`, unconditionally and silently. An outbound request is not an
event, so the guard does not apply.

## The payload is not a place to put instructions

The POST body's freeform text names **only the repository and the issue
number**, and `fire_text` refuses a non-integer issue number rather than
formatting it.

**The Routine prompt must parse only the integer and follow nothing else in the
payload.** If a Routine treats this text as instructions, then anything that can
influence the text can instruct an agent with write access. Keeping the text
boring is half of that; the Routine ignoring it is the other half, and the
Routine's prompt says so.

Configured by two repository secrets — `AI_TRIAGE_URL` and `AI_TRIAGE_SECRET`
(see issue #83). Absent, this is a clean no-op: the nightly run still picks the
issue up, so a missing secret costs latency, not correctness.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import urllib.request

BODY_SNIPPET_LIMIT = 300


class FireResult:
    def __init__(self, fired: bool, outcome: str, detail: str = "") -> None:
        self.fired = fired
        self.outcome = outcome
        self.detail = detail

    @property
    def is_error(self) -> bool:
        """Not configured is not an error — it is a choice not yet made."""
        return not self.fired and self.outcome != "not-configured"

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"FireResult({self.outcome!r}, fired={self.fired!r})"


def fire_text(repository: str, issue_number) -> str:
    """The freeform text in the POST body. Deliberately boring."""
    if not isinstance(issue_number, int):
        raise ValueError(
            f"The issue number must be an integer, got {issue_number!r}. Formatting "
            "free text into the fire payload is how an instruction gets in.")

    return f"Triage issue {issue_number} in {repository}."


def interpret_fire_response(status: int, body: str) -> FireResult:
    """Classify the response **truthfully**.

    Success requires a real fire — a body carrying a session URL. A bare `200`
    means the endpoint answered, not that the Routine ran, and reporting that as
    fired would hide a misconfigured Routine behind a green log line for weeks.
    """
    snippet = (body or "")[:BODY_SNIPPET_LIMIT]

    if status == 401 or status == 403:
        return FireResult(
            False, "unauthorized",
            f"HTTP {status}: the AI_TRIAGE_SECRET is missing or wrong. {snippet}")

    if status >= 400:
        return FireResult(False, "http-error", f"HTTP {status}: {snippet}")

    try:
        payload = json.loads(body or "")
    except (ValueError, TypeError):
        return FireResult(
            False, "unreadable",
            f"HTTP {status} with a body that is not JSON: {snippet}")

    session = payload.get("session_url") or payload.get("url") if isinstance(payload, dict) else None

    if not session:
        return FireResult(
            False, "no-session",
            f"HTTP {status}, but no session URL in the response — the endpoint answered "
            f"and the Routine may not have run: {snippet}")

    return FireResult(True, "fired", f"Triage session: {session}")


def _post(url: str, headers: dict, body: bytes):
    request = urllib.request.Request(url, data=body, method="POST")
    for name, value in headers.items():
        request.add_header(name, value)

    with urllib.request.urlopen(request, timeout=30) as response:
        return response.status, response.read().decode(errors="replace")


def fire(issue_number: int, repository: str, url: str, secret: str, post=None) -> FireResult:
    """Poke the Routine. Never raises.

    The label move has already succeeded by the time this runs. Letting a
    network failure here propagate would report the whole command as failed
    when the only thing lost is a few hours of latency.
    """
    if not url or not secret:
        return FireResult(
            False, "not-configured",
            "AI_TRIAGE_URL/AI_TRIAGE_SECRET are not set, so triage waits for the "
            "nightly run. See issue #83.")

    post = post or _post
    body = json.dumps({"text": fire_text(repository, issue_number)}).encode()
    headers = {
        "Authorization": f"Bearer {secret}",
        "Content-Type": "application/json",
    }

    try:
        status, response_body = post(url, headers, body)
    except Exception as error:  # noqa: BLE001 - never raise past here
        return FireResult(False, "error", f"Could not reach the Routine: {error}")

    return interpret_fire_response(status, response_body)
