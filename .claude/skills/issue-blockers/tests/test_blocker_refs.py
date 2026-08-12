"""Tests for the one shared `Blocked by #N` recognizer.

The last test in this file is the reason the module exists: it widens the
pattern in one place and checks that every reader in the pipeline sees the
change. While that passes, the six copies cannot come back.
"""

import os
import re
import subprocess
import sys
import unittest
from pathlib import Path
from unittest import mock

SKILLS = Path(__file__).resolve().parents[2]

sys.path.insert(0, str(SKILLS / "issue-blockers"))

import blocker_refs  # noqa: E402


class TextBlockers(unittest.TestCase):
    def test_a_structured_line_is_a_blocker(self):
        self.assertEqual({42}, blocker_refs.text_blockers("Blocked by #42"))

    def test_the_colon_form_and_case_are_both_accepted(self):
        self.assertEqual({42}, blocker_refs.text_blockers("blocked by: #42"))

    def test_every_line_in_a_body_is_read(self):
        body = "Some context.\n\nBlocked by #42\nBlocked by #43\n\nMore context."
        self.assertEqual({42, 43}, blocker_refs.text_blockers(body))

    def test_depends_on_is_not_a_blocker(self):
        # Soft ordering with no native form. Reading it as a hard blocker turns
        # a preference into a gate the builder refuses to pass.
        self.assertEqual(set(), blocker_refs.text_blockers("Depends on: #42"))

    def test_a_mention_in_ordinary_prose_is_not_a_blocker(self):
        self.assertEqual(set(), blocker_refs.text_blockers("This is similar to #42."))

    def test_no_body_is_no_blockers(self):
        self.assertEqual(set(), blocker_refs.text_blockers(None))


class TextDependsOn(unittest.TestCase):
    """The soft-ordering line lives here for the same reason the hard one does.

    It has two readers now — the builder's queue order and the gatekeeper's
    milestone-order gate — and two copies of a pattern is how the two disagree
    about what the body said.
    """

    def test_a_structured_line_is_a_soft_dependency(self):
        self.assertEqual({42}, blocker_refs.text_depends_on("Depends on: #42"))

    def test_the_colonless_form_and_case_are_both_accepted(self):
        self.assertEqual({42}, blocker_refs.text_depends_on("depends on #42"))

    def test_a_blocker_is_not_a_soft_dependency(self):
        self.assertEqual(set(), blocker_refs.text_depends_on("Blocked by #42"))

    def test_a_mention_in_ordinary_prose_is_not_one(self):
        self.assertEqual(set(), blocker_refs.text_depends_on("This depends on how #42 goes."))

    def test_no_body_is_no_soft_dependencies(self):
        self.assertEqual(set(), blocker_refs.text_depends_on(None))

    def test_the_builder_reads_the_shared_pattern(self):
        """`select_queue` orders by these lines; it must not carry its own copy."""
        sys.path.insert(0, str(SKILLS / "pipeline-dev"))
        try:
            import select_queue

            wider = re.compile(r"^\s*depends[\s-]+on\s*:?\s*#(\d+)\s*$",
                               re.IGNORECASE | re.MULTILINE)
            with mock.patch.object(blocker_refs, "TEXT_DEPENDS", wider):
                self.assertEqual([42], select_queue.depends_on({"body": "Depends-on: #42"}))
        finally:
            sys.path.remove(str(SKILLS / "pipeline-dev"))


class UnionBlockers(unittest.TestCase):
    def test_text_and_native_are_unioned(self):
        self.assertEqual([20, 21], blocker_refs.union_blockers("Blocked by #20", {21}))

    def test_the_same_blocker_from_both_sources_is_counted_once(self):
        self.assertEqual([20], blocker_refs.union_blockers("Blocked by #20", {20}))

    def test_natives_that_could_not_be_fetched_leave_the_text_ones(self):
        # `fetch_comment_event.py` degrades to an empty set when the dependency
        # lookup fails, rather than losing the whole event. The union has to
        # keep working on what it does know.
        self.assertEqual([20], blocker_refs.union_blockers("Blocked by #20", set()))

    def test_no_sources_at_all_is_no_blockers(self):
        self.assertEqual([], blocker_refs.union_blockers(None, None))


class BlockersOf(unittest.TestCase):
    def test_an_issue_dict_unions_its_body_with_its_native_edges(self):
        issue = {"body": "Blocked by #43", "native_blockers": [42]}
        self.assertEqual([42, 43], blocker_refs.blockers_of(issue))

    def test_an_issue_with_neither_is_not_blocked(self):
        self.assertEqual([], blocker_refs.blockers_of({}))


class ImportIsInert(unittest.TestCase):
    def test_importing_the_module_reads_no_environment_and_touches_nothing(self):
        # Every suite that imports this keeps it cached in sys.modules for the
        # rest of the run, so importing it has to be free of side effects.
        result = subprocess.run(
            [sys.executable, "-c", "import blocker_refs"],
            cwd=str(SKILLS / "issue-blockers"),
            env={"PATH": os.environ.get("PATH", "")},
            capture_output=True, text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)


# A pattern one character wider than the real one: it also accepts the hyphen
# form somebody writes by hand.
WIDER = re.compile(r"^\s*blocked[\s-]+by\s*:?\s*#(\d+)\s*$", re.IGNORECASE | re.MULTILINE)
HAND_WRITTEN = "Blocked-by: #42"


READER_SKILLS = ("pipeline-dashboard", "pipeline-dev", "pipeline-gatekeeper",
                 "pipeline-reconcile")


def _every_reader() -> dict:
    """Each module that recognizes a blocker, as name -> callable(body) -> list."""
    import check_revisits
    import fetch_comment_event
    import reconcile
    import render_dashboard
    import select_queue
    import set_blocker

    def via_snapshot(body):
        event = {"issue": {"number": 10, "labels": [], "body": body},
                 "comment": {"id": 5, "body": "/approve",
                             "user": {"login": "derekwinters", "type": "User"}}}
        snapshot = fetch_comment_event.build(event, _NoApi(), owner="derekwinters")
        # The snapshot carries edges, not numbers — the gates read each one's
        # milestone. Only the recognizer is under test here.
        return [edge["number"] for edge in snapshot["issue"]["blockers"]]

    return {
        "set_blocker.prose_blockers":
            lambda body: set_blocker.prose_blockers(body),
        "check_revisits.blockers_of":
            lambda body: check_revisits.blockers_of({"body": body}),
        "select_queue.blockers_of":
            lambda body: select_queue.blockers_of({"body": body}),
        "render_dashboard.blockers_of":
            lambda body: render_dashboard.blockers_of({"body": body}),
        "reconcile._text_blockers":
            lambda body: reconcile._text_blockers({"body": body}),
        "fetch_comment_event.build":
            via_snapshot,
    }


class _NoApi:
    """Every lookup fails — the snapshot still has to be built."""

    def __call__(self, method, path, payload=None):
        raise RuntimeError("no network in a unit test")


class OneDefinitionReachesEveryReader(unittest.TestCase):
    """The regression this module exists to prevent.

    Six copies of this pattern used to sit in six files. Widening one and not
    the other five does not raise: the queue selector refuses to build an issue
    the dashboard shows as ready, and nothing logs a reason.
    """

    @classmethod
    def setUpClass(cls):
        # Reaching the other skills is the point of the test, but the paths go
        # back afterwards: a suite that leaves them behind would let the next
        # one import across skills without saying so, which is how a missing
        # path insertion passes here and fails in CI.
        cls._added = [str(SKILLS / skill) for skill in READER_SKILLS
                      if str(SKILLS / skill) not in sys.path]
        for directory in cls._added:
            sys.path.insert(0, directory)

    @classmethod
    def tearDownClass(cls):
        for directory in cls._added:
            if directory in sys.path:
                sys.path.remove(directory)

    def test_the_hand_written_form_is_not_a_blocker_today(self):
        for name, read in _every_reader().items():
            with self.subTest(reader=name):
                self.assertEqual([], read(HAND_WRITTEN))

    def test_widening_the_shared_pattern_is_visible_from_every_reader(self):
        with mock.patch.object(blocker_refs, "TEXT_BLOCKER", WIDER):
            for name, read in _every_reader().items():
                with self.subTest(reader=name):
                    self.assertEqual([42], read(HAND_WRITTEN))


if __name__ == "__main__":
    unittest.main()
