"""Tests for the deterministic command parser.

This is where the bad-actor gate and idempotency live, so the tests lean on the
cases where the parser must refuse.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import parse_commands as parser  # noqa: E402

OWNER = "derekwinters"


def comment(body, author=OWNER, watermarked=False, comment_id=1):
    return {"id": comment_id, "body": body, "author": author, "watermarked": watermarked}


def context(labels=(), is_dashboard=False):
    return parser.Context(owner=OWNER, labels=list(labels), is_dashboard=is_dashboard)


def parse(body, **kwargs):
    author = kwargs.pop("author", OWNER)
    watermarked = kwargs.pop("watermarked", False)
    return parser.parse(comment(body, author, watermarked), context(**kwargs))


class VocabularyTests(unittest.TestCase):
    def test_every_command_parses(self):
        for body, expected in (
            ("/admit", "admit"),
            ("/approve", "approve"),
            ("/revise needs a smaller scope", "revise"),
            ("/redo", "redo"),
            ("/propose", "propose"),
            ("/park", "park"),
            ("/unpark", "unpark"),
            ("/milestone v0.1", "milestone"),
        ):
            result = parse(body)
            self.assertEqual([expected], [a.command for a in result.actions], body)

    def test_dashboard_commands_parse_on_the_dashboard(self):
        for body, expected in (("/focus v0.1", "focus"), ("/cap 2", "cap")):
            result = parse(body, is_dashboard=True)
            self.assertEqual([expected], [a.command for a in result.actions], body)

    def test_an_argument_is_captured(self):
        result = parse("/revise the scope is too big, split it")

        self.assertEqual("the scope is too big, split it", result.actions[0].argument)

    def test_a_command_with_no_argument_has_an_empty_one(self):
        self.assertEqual("", parse("/approve").actions[0].argument)


class ProseTests(unittest.TestCase):
    def test_prose_around_a_command_is_ignored(self):
        result = parse("Looks good to me.\n\n/approve\n\nThanks!")

        self.assertEqual(["approve"], [a.command for a in result.actions])

    def test_several_commands_apply_in_order(self):
        result = parse("/milestone v0.1\n/approve")

        self.assertEqual(["milestone", "approve"], [a.command for a in result.actions])

    def test_a_command_must_start_its_line(self):
        # "see /approve for details" is a mention, not an instruction.
        self.assertEqual([], parse("see /approve for details").actions)

    def test_a_command_inside_a_code_fence_is_ignored(self):
        # Otherwise documenting the vocabulary would execute it.
        body = "Here is how it works:\n\n```\n/approve\n```\n"

        self.assertEqual([], parse(body).actions)

    def test_a_comment_with_no_commands_yields_nothing(self):
        result = parse("This looks reasonable, I'll think about it.")

        self.assertEqual([], result.actions)
        self.assertEqual([], result.skips)


class BadActorGateTests(unittest.TestCase):
    def test_a_command_from_anyone_else_is_dropped(self):
        result = parse("/approve", author="a-stranger")

        self.assertEqual([], result.actions)
        self.assertEqual(["not-owner"], [s.reason for s in result.skips])

    def test_the_drop_is_silent(self):
        # No reply and no reaction: replying would let a stranger make the bot
        # post, which is a smaller hole of the same shape.
        result = parse("/approve", author="a-stranger")

        self.assertFalse(result.should_acknowledge)

    def test_the_check_is_the_login_not_an_association(self):
        # Write access is granted for reasons unrelated to driving a pipeline.
        result = parser.parse(
            {"id": 1, "body": "/approve", "author": "a-collaborator",
             "watermarked": False, "author_association": "COLLABORATOR"},
            context())

        self.assertEqual([], result.actions)

    def test_the_owner_check_is_case_insensitive(self):
        # GitHub logins are case-insensitive; "DerekWinters" is the same person.
        result = parse("/approve", author="DerekWinters")

        self.assertEqual(["approve"], [a.command for a in result.actions])


class IdempotencyTests(unittest.TestCase):
    def test_a_watermarked_comment_is_never_re_applied(self):
        result = parse("/approve", watermarked=True)

        self.assertEqual([], result.actions)
        self.assertEqual(["already-applied"], [s.reason for s in result.skips])

    def test_a_watermarked_comment_from_a_stranger_is_still_dropped(self):
        result = parse("/approve", author="a-stranger", watermarked=True)

        self.assertEqual([], result.actions)


class UnknownCommandTests(unittest.TestCase):
    def test_an_unknown_command_is_skipped_not_guessed(self):
        result = parse("/aprove")

        self.assertEqual([], result.actions)
        self.assertEqual(["unknown-command"], [s.reason for s in result.skips])

    def test_the_skip_names_the_closest_match(self):
        result = parse("/aprove")

        self.assertIn("/approve", result.skips[0].detail)

    def test_a_nonsense_command_still_skips_cleanly(self):
        result = parse("/xyzzy")

        self.assertEqual(["unknown-command"], [s.reason for s in result.skips])


class CapTests(unittest.TestCase):
    def test_cap_is_rejected_off_the_dashboard(self):
        result = parse("/cap 2")

        self.assertEqual([], result.actions)
        self.assertEqual(["not-dashboard"], [s.reason for s in result.skips])

    def test_focus_is_rejected_off_the_dashboard(self):
        self.assertEqual(["not-dashboard"], [s.reason for s in parse("/focus v0.1").skips])

    def test_a_non_numeric_cap_is_rejected(self):
        result = parse("/cap lots", is_dashboard=True)

        self.assertEqual(["cap-invalid"], [s.reason for s in result.skips])

    def test_a_zero_or_negative_cap_is_rejected(self):
        for value in ("0", "-1"):
            result = parse(f"/cap {value}", is_dashboard=True)
            self.assertEqual(["cap-invalid"], [s.reason for s in result.skips], value)

    def test_a_valid_cap_carries_the_number(self):
        result = parse("/cap 3", is_dashboard=True)

        self.assertEqual("3", result.actions[0].argument)


class EpicTests(unittest.TestCase):
    def test_admit_is_refused_on_an_epic(self):
        # Epics are containers. Their children are the work, and an admitted
        # epic is one the builder would try to build.
        result = parse("/admit", labels=["type:epic"])

        self.assertEqual([], result.actions)
        self.assertEqual(["epic-excluded"], [s.reason for s in result.skips])

    def test_approve_is_refused_on_an_epic(self):
        self.assertEqual(["epic-excluded"], [s.reason for s in parse("/approve", labels=["type:epic"]).skips])

    def test_park_is_allowed_on_an_epic(self):
        # Parking a whole epic is a reasonable thing to want.
        result = parse("/park", labels=["type:epic"])

        self.assertEqual(["park"], [a.command for a in result.actions])

    def test_milestone_is_allowed_on_an_epic(self):
        result = parse("/milestone v0.1", labels=["type:epic"])

        self.assertEqual(["milestone"], [a.command for a in result.actions])


class NoIoTests(unittest.TestCase):
    def test_the_module_imports_nothing_that_can_reach_the_network(self):
        # Data in, actions out. The parser is the safety boundary, and a
        # boundary that can also act is not one. Checked against the import
        # statements rather than the whole file, so prose about I/O in a
        # docstring does not trip it.
        import ast

        source = Path(parser.__file__).read_text()
        imported = set()

        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported.update(alias.name.split(".")[0] for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imported.add(node.module.split(".")[0])

        self.assertEqual(set(), imported & {"urllib", "http", "socket", "subprocess", "requests"})


if __name__ == "__main__":
    unittest.main()
