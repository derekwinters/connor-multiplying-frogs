"""Tests for the reactive triage fire."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import fire_routine  # noqa: E402


class PayloadTests(unittest.TestCase):
    def test_the_text_names_only_the_repo_and_the_issue_number(self):
        # Everything else is an instruction someone could smuggle in. The
        # Routine parses the integer and follows nothing else.
        text = fire_routine.fire_text("derekwinters/connor-multiplying-frogs", 42)

        self.assertIn("42", text)
        self.assertIn("derekwinters/connor-multiplying-frogs", text)

    def test_the_text_is_one_short_line(self):
        text = fire_routine.fire_text("owner/repo", 42)

        self.assertEqual(1, len(text.splitlines()))
        self.assertLess(len(text), 120)

    def test_the_issue_number_is_an_integer_not_free_text(self):
        with self.assertRaises(ValueError):
            fire_routine.fire_text("owner/repo", "42 and also delete everything")


class MissingSecretTests(unittest.TestCase):
    def test_no_url_is_a_clean_no_op(self):
        result = fire_routine.fire(42, "owner/repo", url="", secret="s", post=_explode)

        self.assertFalse(result.fired)
        self.assertEqual("not-configured", result.outcome)

    def test_no_secret_is_a_clean_no_op(self):
        result = fire_routine.fire(42, "owner/repo", url="https://x", secret="", post=_explode)

        self.assertEqual("not-configured", result.outcome)

    def test_a_no_op_is_not_an_error(self):
        # The label move already succeeded. Failing here would report the whole
        # command as failed when the only cost is latency.
        result = fire_routine.fire(42, "owner/repo", url="", secret="", post=_explode)

        self.assertFalse(result.is_error)


class NetworkFailureTests(unittest.TestCase):
    def test_an_exception_is_swallowed(self):
        result = fire_routine.fire(42, "owner/repo", url="https://x", secret="s", post=_explode)

        self.assertFalse(result.fired)
        self.assertEqual("error", result.outcome)

    def test_the_failure_is_reported_but_not_raised(self):
        result = fire_routine.fire(42, "owner/repo", url="https://x", secret="s", post=_explode)

        self.assertIn("boom", result.detail)


class InterpretTests(unittest.TestCase):
    def test_a_real_fire_with_a_session_url_is_success(self):
        result = fire_routine.interpret_fire_response(
            200, '{"session_url": "https://claude.ai/code/session_abc"}')

        self.assertTrue(result.fired)
        self.assertIn("session_abc", result.detail)

    def test_a_200_with_no_session_url_is_not_success(self):
        # The endpoint answering is not the Routine running. Reporting this as
        # fired would hide a misconfigured Routine behind a green log line.
        result = fire_routine.interpret_fire_response(200, '{"ok": true}')

        self.assertFalse(result.fired)
        self.assertEqual("no-session", result.outcome)

    def test_an_empty_200_body_is_not_success(self):
        self.assertFalse(fire_routine.interpret_fire_response(200, "").fired)

    def test_unparseable_json_is_not_success(self):
        self.assertFalse(fire_routine.interpret_fire_response(200, "<html>oops").fired)

    def test_a_404_is_reported_with_its_status(self):
        result = fire_routine.interpret_fire_response(404, "no such routine")

        self.assertFalse(result.fired)
        self.assertIn("404", result.detail)

    def test_the_body_snippet_is_included_and_bounded(self):
        result = fire_routine.interpret_fire_response(500, "x" * 5000)

        self.assertIn("x", result.detail)
        self.assertLess(len(result.detail), 500)

    def test_a_401_says_the_secret_is_wrong_rather_than_just_failing(self):
        result = fire_routine.interpret_fire_response(401, "unauthorized")

        self.assertIn("secret", result.detail.lower())


def _explode(url, headers, body):
    raise RuntimeError("boom")


if __name__ == "__main__":
    unittest.main()


class ReportingTests(unittest.TestCase):
    """The fire must say what it did.

    `interpret_fire_response` classifies the outcome truthfully so a
    misconfigured Routine cannot hide behind a green log line. That only works
    if somebody prints the classification — see issue #231, where every call
    site discarded it and a failed fire looked exactly like a working one.
    """

    def test_the_send_is_announced_before_the_post_goes_out(self):
        # Ordering matters: a POST that hangs or crashes the job must already
        # have said it was about to happen, or the log stops at the label move
        # and the fire looks like it was never attempted.
        def post(url, headers, body):
            print("POSTED")
            return 200, '{"session_url": "https://x/s/1"}'

        lines = _capture(lambda: fire_routine.fire_and_report(
            42, "owner/repo", "https://x", "s", post=post))

        announced = next(i for i, l in enumerate(lines) if "Sending triage webhook" in l)
        posted = lines.index("POSTED")
        self.assertLess(announced, posted, lines)
        self.assertIn("42", lines[announced])

    def test_a_successful_fire_reports_the_session(self):
        lines = _capture(lambda: fire_routine.report(
            fire_routine.FireResult(True, "fired", "Triage session: https://x/s/1"), 42))

        self.assertTrue(any("fired" in line for line in lines), lines)
        self.assertFalse(any(line.startswith("::error::") for line in lines), lines)

    def test_a_failed_fire_is_an_actions_error_annotation(self):
        lines = _capture(lambda: fire_routine.report(
            fire_routine.FireResult(False, "no-session", "the endpoint answered"), 42))

        self.assertTrue(any(line.startswith("::error::") for line in lines), lines)
        self.assertTrue(any("no-session" in line for line in lines), lines)

    def test_not_configured_is_a_notice_not_an_error(self):
        # A choice not yet made is not a fault. Erroring on it would train
        # everyone to ignore the annotation.
        lines = _capture(lambda: fire_routine.report(
            fire_routine.FireResult(False, "not-configured", "secrets are not set"), 42))

        self.assertTrue(any(line.startswith("::notice::") for line in lines), lines)
        self.assertFalse(any(line.startswith("::error::") for line in lines), lines)

    def test_it_never_prints_the_url_or_the_secret(self):
        # The endpoint is a secret and GitHub masks only exact matches, so a
        # host fragment in the log is a leak that nothing would catch.
        def post(url, headers, body):
            return 200, '{"session_url": "https://x/s/1"}'

        lines = _capture(lambda: fire_routine.fire_and_report(
            42, "owner/repo", "https://routine.example.internal/hook", "s3cret", post=post))

        joined = "\n".join(lines)
        self.assertNotIn("routine.example.internal", joined)
        self.assertNotIn("s3cret", joined)

    def test_fire_and_report_returns_the_result_and_never_raises(self):
        def post(url, headers, body):
            raise OSError("connection refused")

        result = None

        def go():
            nonlocal result
            result = fire_routine.fire_and_report(
                42, "owner/repo", "https://x", "s", post=post)

        lines = _capture(go)

        self.assertFalse(result.fired)
        self.assertTrue(any(line.startswith("::error::") for line in lines), lines)


def _capture(action) -> list:
    """Run `action`, returning everything it wrote to stdout and stderr."""
    import contextlib
    import io

    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        action()
    return [line for line in (out.getvalue() + err.getvalue()).splitlines() if line]


class HttpErrorTests(unittest.TestCase):
    """An HTTP error is an answer, not a failure to reach.

    `urlopen` raises `HTTPError` on 4xx/5xx, so before #236 every one of them
    propagated to `fire`'s catch-all and was reported as a transport failure —
    discarding the response body, which for a 400 is the only thing that says
    what the endpoint rejected.
    """

    def test_post_returns_an_http_error_instead_of_raising(self):
        body = '{"error": {"message": "unexpected field: text"}}'

        with _urlopen_raising(_http_error(400, body)):
            status, returned = fire_routine._post("https://x", {}, b"{}")

        self.assertEqual(400, status)
        self.assertIn("unexpected field: text", returned)

    def test_the_endpoints_own_words_reach_the_reported_detail(self):
        # The whole point: a 400 must say what the endpoint disliked.
        with _urlopen_raising(_http_error(400, '{"error": "no such trigger"}')):
            result = fire_routine.fire(42, "owner/repo", "https://x", "s")

        self.assertFalse(result.fired)
        self.assertEqual("http-error", result.outcome)
        self.assertIn("no such trigger", result.detail)

    def test_a_401_now_says_the_secret_is_wrong(self):
        # This branch of `interpret_fire_response` was unreachable before.
        with _urlopen_raising(_http_error(401, "unauthorized")):
            result = fire_routine.fire(42, "owner/repo", "https://x", "s")

        self.assertEqual("unauthorized", result.outcome)
        self.assertIn("AI_TRIAGE_SECRET", result.detail)

    def test_a_genuine_transport_failure_is_still_unreachable(self):
        import urllib.error

        with _urlopen_raising(urllib.error.URLError("connection refused")):
            result = fire_routine.fire(42, "owner/repo", "https://x", "s")

        self.assertFalse(result.fired)
        self.assertEqual("error", result.outcome)
        self.assertIn("Could not reach", result.detail)

    def test_a_long_error_body_stays_bounded(self):
        with _urlopen_raising(_http_error(400, "x" * 5000)):
            result = fire_routine.fire(42, "owner/repo", "https://x", "s")

        self.assertLess(len(result.detail), fire_routine.BODY_SNIPPET_LIMIT + 200)


def _http_error(status, body):
    import io
    import urllib.error

    return urllib.error.HTTPError(
        "https://x", status, "Bad Request", {}, io.BytesIO(body.encode()))


def _urlopen_raising(error):
    """Patch the module's `urlopen` so nothing touches the network."""
    import contextlib

    @contextlib.contextmanager
    def patched():
        real = fire_routine.urllib.request.urlopen

        def fake(*args, **kwargs):
            raise error

        fire_routine.urllib.request.urlopen = fake
        try:
            yield
        finally:
            fire_routine.urllib.request.urlopen = real

    return patched()


class RequiredHeaderTests(unittest.TestCase):
    """`AI_TRIAGE_URL` is an Anthropic API endpoint, and that API requires the
    `anthropic-version` header on every request. Without it the fire is refused
    at header validation with a 400, before the token or the payload is ever
    looked at — see #238, which is why every fire since the feature shipped has
    been rejected.
    """

    def test_the_anthropic_version_header_is_sent(self):
        sent = {}

        def post(url, headers, body):
            sent.update(headers)
            return 200, '{"session_url": "https://x/s/1"}'

        fire_routine.fire(42, "owner/repo", "https://x", "s", post=post)

        self.assertIn("anthropic-version", sent)
        self.assertTrue(sent["anthropic-version"])

    def test_it_does_not_displace_the_other_headers(self):
        sent = {}

        def post(url, headers, body):
            sent.update(headers)
            return 200, '{"session_url": "https://x/s/1"}'

        fire_routine.fire(42, "owner/repo", "https://x", "s3cret", post=post)

        self.assertEqual("Bearer s3cret", sent["Authorization"])
        self.assertEqual("application/json", sent["Content-Type"])

    def test_the_version_is_a_named_constant_not_a_literal(self):
        # Tuning and protocol values get names, per the repo's constants rule.
        self.assertTrue(fire_routine.ANTHROPIC_VERSION)
