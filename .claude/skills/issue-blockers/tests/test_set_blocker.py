"""Unit tests for the native blocked-by helper."""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import set_blocker  # noqa: E402


class FakeApi:
    """Records calls and replays canned responses."""

    def __init__(self, responses=None):
        self.calls = []
        self.responses = responses or {}

    def __call__(self, method, path, payload=None):
        self.calls.append((method, path, payload))
        return self.responses.get((method, path), self.responses.get(path, []))


class ResolveIdTests(unittest.TestCase):
    def test_an_issue_number_resolves_to_its_numeric_id(self):
        # The write API takes the internal id, not the number anyone can see.
        # Getting this wrong is the whole trap.
        api = FakeApi({"/issues/82": {"number": 82, "id": 5098389999}})

        self.assertEqual(5098389999, set_blocker.resolve_issue_id(api, 82))

    def test_a_missing_id_is_an_error_not_a_guess(self):
        api = FakeApi({"/issues/82": {"number": 82}})

        with self.assertRaises(set_blocker.BlockerError):
            set_blocker.resolve_issue_id(api, 82)

    def test_the_number_is_never_used_as_the_id(self):
        api = FakeApi({"/issues/82": {"number": 82, "id": 5098389999}})

        set_blocker.add_blocker(api, blocked=28, blocked_by=82)

        method, path, payload = api.calls[-1]
        self.assertEqual("POST", method)
        self.assertEqual({"issue_id": 5098389999}, payload)


class ListTests(unittest.TestCase):
    def test_it_lists_the_blocking_issue_numbers(self):
        api = FakeApi({
            "/issues/28/dependencies/blocked_by": [
                {"number": 82, "title": "the secret", "state": "open", "id": 1},
                {"number": 104, "title": "the settings", "state": "closed", "id": 2},
            ]
        })

        blockers = set_blocker.list_blockers(api, 28)

        self.assertEqual([82, 104], [b["number"] for b in blockers])

    def test_no_blockers_is_an_empty_list_not_an_error(self):
        self.assertEqual([], set_blocker.list_blockers(FakeApi(), 28))


class AddTests(unittest.TestCase):
    def test_it_posts_to_the_blocked_issue(self):
        api = FakeApi({"/issues/82": {"id": 999}})

        set_blocker.add_blocker(api, blocked=28, blocked_by=82)

        method, path, _ = api.calls[-1]
        self.assertEqual("POST", method)
        self.assertEqual("/issues/28/dependencies/blocked_by", path)

    def test_an_issue_cannot_block_itself(self):
        with self.assertRaises(set_blocker.BlockerError):
            set_blocker.add_blocker(FakeApi(), blocked=28, blocked_by=28)


class RemoveTests(unittest.TestCase):
    def test_it_deletes_by_numeric_id(self):
        api = FakeApi({"/issues/82": {"id": 999}})

        set_blocker.remove_blocker(api, blocked=28, blocked_by=82)

        method, path, _ = api.calls[-1]
        self.assertEqual("DELETE", method)
        self.assertEqual("/issues/28/dependencies/blocked_by/999", path)


class ProseDetectionTests(unittest.TestCase):
    def test_a_blocked_by_line_in_a_body_is_found(self):
        body = "Some context.\n\nBlocked by #42\n\n## Build checklist"

        self.assertEqual([42], set_blocker.prose_blockers(body))

    def test_it_is_case_insensitive_and_tolerates_a_colon(self):
        self.assertEqual([42], set_blocker.prose_blockers("blocked by: #42"))

    def test_several_are_all_found(self):
        self.assertEqual([42, 43], set_blocker.prose_blockers("Blocked by #42\nBlocked by #43"))

    def test_a_soft_depends_on_line_is_not_a_blocker(self):
        # `Depends on:` is soft ordering with no native form. Converting it
        # would turn a preference into a hard gate the builder refuses to pass.
        self.assertEqual([], set_blocker.prose_blockers("Depends on: #42"))

    def test_a_mention_in_ordinary_prose_is_not_a_blocker(self):
        self.assertEqual([], set_blocker.prose_blockers("This is similar to #42 but simpler."))


if __name__ == "__main__":
    unittest.main()
