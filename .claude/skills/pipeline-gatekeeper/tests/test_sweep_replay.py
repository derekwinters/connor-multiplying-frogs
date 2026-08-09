"""Tests for the sweep's replay of comment commands the webhook never delivered.

`gatekeeper-comment` fires on `issue_comment: created`. When that delivery is
dropped the command is lost silently. The replay is the second pass the 👀
watermark was designed for, so the tests that matter here are the ones about
*not* acting twice.
"""

import sys
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import run_sweep  # noqa: E402

OWNER = "derekwinters"
BOT = "github-actions[bot]"

# What `reconcile.has_analysis` recognises as "triage analyzed this". Present on
# every issue in these tests so `requeue_triage` — which is about un-analyzed
# issues, not about the replay — stays out of the way.
ANALYSIS = {"body": "## Build checklist\n\n- [ ] x", "author": BOT,
            "created_at": "2026-08-08T10:00:00Z"}


class RecordingApi:
    """Records every call, answers the reads the replay makes."""

    def __init__(self, reactions=None, reactions_raise=False, raise_on=None):
        self.calls = []
        self._reactions = reactions or {}
        self._reactions_raise = reactions_raise
        self._raise_on = raise_on or ()

    def __call__(self, method, path, body=None):
        self.calls.append((method, path, body))

        for method_fragment, path_fragment in self._raise_on:
            if method == method_fragment and path_fragment in path:
                raise RuntimeError("the API said no")

        if path.endswith("/reactions") and method == "GET":
            if self._reactions_raise:
                raise RuntimeError("reactions are unreadable")
            comment_id = int(path.split("/issues/comments/")[1].split("/")[0])
            return self._reactions.get(comment_id, [])
        if path.endswith("/blocked_by"):
            return []
        return {}

    @property
    def label_writes(self):
        return [(p, b) for m, p, b in self.calls if m == "PUT" and p.endswith("/labels")]

    @property
    def posted_comments(self):
        return [b for m, p, b in self.calls
                if m == "POST" and p.endswith("/comments")]

    @property
    def watermarks(self):
        return [p for m, p, _ in self.calls
                if m == "POST" and p.endswith("/reactions")]


def issue(number=10, state="open", labels=("pending-approval",), body="",
          milestone="v0.0.1", comments=(ANALYSIS,)):
    return {
        "number": number, "state": state, "labels": list(labels), "body": body,
        "milestone": milestone, "native_blockers": [], "comments": list(comments),
    }


def raw_comment(identifier=999, body="/approve", author=OWNER, on=10,
                kind="User"):
    """A comment as `/issues/comments` returns it, not as the snapshot holds it."""
    return {
        "id": identifier,
        "body": body,
        "user": {"login": author, "type": kind},
        "issue_url": f"https://api.github.com/repos/derekwinters/frogs/issues/{on}",
        "created_at": "2026-08-09T09:00:00Z",
    }


def state(issues, comments=()):
    return {"issues": list(issues), "recent_comments": list(comments),
            "focus": "v0.0.1"}


def sweep(api, issues, comments=(), **kwargs):
    kwargs.setdefault("owner", OWNER)
    kwargs.setdefault("rerender", lambda **kw: None)
    return run_sweep.run(state(issues, comments), api, **kwargs)


class ReplayTests(unittest.TestCase):
    def test_an_owner_command_in_the_window_is_applied(self):
        api = RecordingApi()
        result = sweep(api, [issue()], [raw_comment()])

        self.assertEqual(result["replayed"], [10])
        self.assertEqual(api.label_writes,
                         [("/issues/10/labels", {"labels": ["ready-for-work"]})])

    def test_the_replay_acknowledges_exactly_as_the_live_path_does(self):
        api = RecordingApi()
        sweep(api, [issue()], [raw_comment()])

        self.assertEqual(len(api.posted_comments), 1)
        self.assertIn("`/approve`", api.posted_comments[0]["body"])

    def test_a_replayed_command_leaves_the_eyes(self):
        api = RecordingApi()
        sweep(api, [issue()], [raw_comment()])
        self.assertEqual(api.watermarks, ["/issues/comments/999/reactions"])

    def test_the_event_pass_replays_nothing(self):
        """The event path fires on `issues: [labeled]`, and an applied command
        changes a label — so replaying there would race `gatekeeper-comment`
        applying the very same comment, in a different concurrency group."""
        api = RecordingApi()
        result = sweep(api, [issue()], [raw_comment()], events_only=True)

        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])
        self.assertEqual(api.posted_comments, [])


class IdempotencyTests(unittest.TestCase):
    """The whole risk. A replay that mis-reads the watermark re-applies every
    command it finds, six times a day, forever."""

    def test_a_watermarked_comment_is_not_replayed(self):
        api = RecordingApi(reactions={999: [{"content": "eyes",
                                             "user": {"login": BOT}}]})
        result = sweep(api, [issue()], [raw_comment()])

        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])
        self.assertEqual(api.posted_comments, [])
        self.assertEqual(api.watermarks, [])

    def test_a_humans_eyes_do_not_count_as_the_watermark(self):
        api = RecordingApi(reactions={999: [{"content": "eyes",
                                             "user": {"login": "a-passer-by"}}]})
        self.assertEqual(sweep(api, [issue()], [raw_comment()])["replayed"], [10])

    def test_an_unreadable_reaction_lookup_counts_as_watermarked(self):
        """Unknown is not "unclaimed" — the safe direction is to skip."""
        api = RecordingApi(reactions_raise=True)
        result = sweep(api, [issue()], [raw_comment()])

        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])
        self.assertEqual(api.watermarks, [])


class ScopeTests(unittest.TestCase):
    def test_a_comment_on_a_closed_issue_is_ignored(self):
        api = RecordingApi()
        result = sweep(api, [issue(state="closed", labels=())],
                       [raw_comment()])
        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.posted_comments, [])

    def test_a_comment_on_an_issue_outside_the_snapshot_is_ignored(self):
        api = RecordingApi()
        result = sweep(api, [issue(10)], [raw_comment(on=4242)])
        self.assertEqual(result["replayed"], [])

    def test_a_strangers_command_is_refused_and_not_watermarked(self):
        """Reacting to it would itself be letting a stranger make the bot act."""
        api = RecordingApi()
        result = sweep(api, [issue()], [raw_comment(author="a-stranger")])

        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])
        self.assertEqual(api.watermarks, [])

    def test_a_bot_comment_is_ignored(self):
        api = RecordingApi()
        result = sweep(api, [issue()], [raw_comment(author=BOT, kind="Bot")])
        self.assertEqual(result["replayed"], [])

    def test_without_a_configured_owner_nothing_is_replayed(self):
        api = RecordingApi()
        result = sweep(api, [issue()], [raw_comment()], owner="")
        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])


class SnapshotWriteBackTests(unittest.TestCase):
    def test_reconcile_does_not_revert_a_just_replayed_command(self):
        """Reconcile derives its fixes from `issue["labels"]`. Read stale, it
        "fixes" the state the replayed command just set — undoing the command
        it was replayed to honour, and reporting a success while doing it."""
        api = RecordingApi()
        result = sweep(api, [issue(labels=("in-progress",))],
                       [raw_comment(body="/park")])

        self.assertEqual(result["replayed"], [10])
        self.assertEqual(api.label_writes,
                         [("/issues/10/labels", {"labels": ["parked"]})])
        self.assertEqual([f["kind"] for f in result["findings"]
                          if f["action"] == "auto-fix"], [])

    def test_a_refused_command_leaves_the_snapshot_alone(self):
        """`/focus` off the dashboard is refused, so reconcile must still see
        the labels the issue really has."""
        api = RecordingApi()
        board = state([issue(labels=("pending-approval",))],
                      [raw_comment(body="/focus v0.0.2")])

        result = run_sweep.run(board, api, owner=OWNER, rerender=lambda **kw: None)

        self.assertEqual(result["replayed"], [])
        self.assertEqual(api.label_writes, [])
        self.assertIn("not applied", api.posted_comments[0]["body"])
        self.assertEqual(board["issues"][0]["labels"], ["pending-approval"])


class RenderTests(unittest.TestCase):
    def test_two_replayed_commands_render_the_board_once(self):
        """`run_comment_event` renders after its own writes, which is right for
        one webhook and wrong for a sweep — six replays would publish six
        boards, five of them describing a half-finished sweep."""
        renders = []
        api = RecordingApi()

        run_sweep.run(
            state([issue(10), issue(11)],
                  [raw_comment(999, on=10), raw_comment(1000, on=11)]),
            api, owner=OWNER, rerender=lambda **kw: renders.append(kw))

        self.assertEqual(len(renders), 1)

    def test_the_last_focus_override_wins(self):
        """What would have happened had both webhooks arrived in order."""
        renders = []
        api = RecordingApi()
        board = issue(20, labels=("dashboard",), comments=())

        run_sweep.run(
            state([board],
                  [raw_comment(1, body="/focus v0.0.1", on=20),
                   raw_comment(2, body="/focus v0.0.2", on=20)]),
            api, owner=OWNER, rerender=lambda **kw: renders.append(kw))

        self.assertEqual(len(renders), 1)
        self.assertEqual(renders[0].get("focus_override"), "v0.0.2")

    def test_a_sweep_that_replayed_nothing_still_does_not_render(self):
        renders = []
        run_sweep.run(state([issue(10, labels=("ai-triage",), comments=())], []),
                      RecordingApi(), owner=OWNER,
                      rerender=lambda **kw: renders.append(kw))
        self.assertEqual(renders, [])


class ReactiveTriageTests(unittest.TestCase):
    def test_a_replayed_admit_fires_triage(self):
        fired = []
        sweep(RecordingApi(), [issue(labels=(), comments=())],
              [raw_comment(body="/admit")], fire=fired.append)
        self.assertEqual(fired, [10])

    def test_a_replayed_approve_does_not(self):
        fired = []
        sweep(RecordingApi(), [issue()], [raw_comment()], fire=fired.append)
        self.assertEqual(fired, [])


class ResilienceTests(unittest.TestCase):
    def test_one_failing_comment_does_not_take_the_rest_of_the_sweep_with_it(self):
        """Reconcile is the backstop for the whole board. Losing it because one
        comment blew up would trade a missed command for a missed sweep."""
        api = RecordingApi(raise_on=[("PUT", "/issues/10/labels")])
        result = sweep(api, [issue(10), issue(11)],
                       [raw_comment(999, on=10), raw_comment(1000, on=11)])

        self.assertEqual(result["replay_errors"], [10])
        self.assertEqual(result["replayed"], [11])
        self.assertIn("/issues/11/labels", [p for p, _ in api.label_writes])


class WindowTests(unittest.TestCase):
    def test_the_window_is_a_named_constant(self):
        self.assertIsInstance(run_sweep.REPLAY_WINDOW_DAYS, int)
        self.assertGreater(run_sweep.REPLAY_WINDOW_DAYS, 0)

    def test_the_query_asks_only_for_comments_inside_the_window(self):
        now = datetime(2026, 8, 9, 12, 0, tzinfo=timezone.utc)
        since = (now - timedelta(days=run_sweep.REPLAY_WINDOW_DAYS))

        query = run_sweep.comments_query(now)

        self.assertTrue(query.startswith("/issues/comments?"), query)
        self.assertIn(f"since={since.strftime('%Y-%m-%dT%H:%M:%SZ')}", query)
        self.assertIn("per_page=100", query)

    def test_the_query_is_ascending_so_the_later_command_lands_later(self):
        self.assertIn("direction=asc", run_sweep.comments_query())

    def test_the_candidates_come_from_that_one_repo_wide_call(self):
        source = (Path(__file__).resolve().parents[1] / "run_sweep.py").read_text()
        self.assertIn("get_all(comments_query())", source)


class CommentIdTests(unittest.TestCase):
    def test_the_per_issue_comments_carry_the_id(self):
        """The watermark is keyed on it."""
        def api(method, path, body=None):
            return [{"id": 7, "body": "x", "user": {"login": OWNER},
                     "created_at": "2026-08-01T00:00:00Z"}]

        self.assertEqual(run_sweep._comments(api, 10)[0]["id"], 7)


if __name__ == "__main__":
    unittest.main()
