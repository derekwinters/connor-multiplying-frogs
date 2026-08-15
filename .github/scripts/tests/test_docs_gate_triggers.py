"""Adding `skip-docs` must actually re-run the docs reconciliation gate.

`CLAUDE.md` rule 9, `docs/engineering/ci-cd.md`, and the gate's own failure
message all tell a blocked human the same thing: add the label and the check
re-runs, nothing needs pushing. That is the sentence someone reads at the
moment they are stuck, so it is the worst one to have be false.

It was false. The workflow carrying the gate triggered on a bare
`pull_request:`, which is GitHub's default set — `opened`, `synchronize`,
`reopened` — and a `labeled` event reaches none of those. Once the gate had
failed, the label did nothing at all (issue #176).

The invariant, stated once so it cannot rot back:

    Every event that can change the gate's verdict must re-run the gate.

The verdict has exactly two inputs — the PR's changed files and the PR's
labels. `synchronize` covers the first. `labeled` covers the second, and it is
the one that has to be asked for by name.

Stdlib only, and regex rather than a YAML parse, for the same reason as
`check_action_pins.py`: the `gate-tests` job runs `run_python_tests.py scripts`
on nothing but the Python already on the runner, with no pip install to import
`pyyaml` from. The shapes needed here are a nested mapping key and a short list.

The gate itself now lives in ai-sdlc and is invoked by a caller workflow
(#343). This file deliberately still lives here: the trigger is the half that
cannot be centralised, so it is the half that needs guarding here.

See docs/engineering/ci-cd.md and issues #176, #343.
"""

import re
import unittest
from pathlib import Path

WORKFLOWS = Path(__file__).resolve().parents[3] / ".github" / "workflows"

# The gate is wherever it is *called* from, rather than a hard-coded workflow
# filename: moving it must move this check with it, not quietly stop checking
# anything.
#
# It used to be our own `docs_reconciliation_gate.py`. It is now a caller of
# ai-sdlc's shared workflow (#343) — the marker changed, the invariant did not,
# and the `labeled` trigger is just as load-bearing on a caller as it was on a
# step.
GATE_SCRIPT = "reusable-docs-gate.yml"

# GitHub's implicit `types:` for a `pull_request` trigger, used whenever the key
# is absent. `labeled` is deliberately not among them, which is the whole bug.
DEFAULT_TYPES = ("opened", "synchronize", "reopened")

# The label event is additional, and asking for it means spelling out the
# defaults too — naming `types:` at all replaces them rather than adding to it.
REQUIRED_TYPES = DEFAULT_TYPES + ("labeled",)

BLANK_OR_COMMENT = re.compile(r"^\s*(#.*)?$")

# `on:`, `"on":`, `'on':` — YAML 1.1 readers take a bare `on` for a boolean, so
# either spelling is legitimate and both mean the trigger block.
ON_KEY = re.compile(r"^(?:on|[\"']on[\"']):\s*(.*)$")

LIST_ITEM = re.compile(r"^\s*-\s*(.+)$")


def indent_of(line: str) -> int:
    return len(line) - len(line.lstrip())


def strip_inline_comment(value: str) -> str:
    """Drop a trailing `# ...`, which YAML requires be preceded by whitespace."""
    return re.split(r"\s+#", value, maxsplit=1)[0].strip()


def block_under(lines: list[str], index: int) -> list[str]:
    """The lines strictly inside the mapping opened at `lines[index]`."""
    parent = indent_of(lines[index])
    body: list[str] = []

    for line in lines[index + 1:]:
        if BLANK_OR_COMMENT.match(line):
            continue
        if indent_of(line) <= parent:
            break
        body.append(line)

    return body


def find_key(lines: list[str], key: str):
    """`(index, inline value)` for `key:` in these lines, or None."""
    pattern = re.compile(rf"^\s*{re.escape(key)}:\s*(.*)$")

    for index, line in enumerate(lines):
        if BLANK_OR_COMMENT.match(line):
            continue
        match = pattern.match(line)
        if match:
            return index, strip_inline_comment(match.group(1))

    return None


def parse_list(lines: list[str], index: int, inline: str) -> list[str]:
    """A YAML list written either inline (`[a, b]`) or as `- ` items."""
    if inline.startswith("["):
        items = inline.strip("[]").split(",")
        return [item.strip().strip("\"'") for item in items if item.strip()]

    values = []
    for line in block_under(lines, index):
        match = LIST_ITEM.match(line)
        if match:
            values.append(strip_inline_comment(match.group(1)).strip("\"'"))

    return values


def pull_request_types(text: str):
    """The `on: pull_request: types:` of a workflow.

    None means the key is absent — which is not "no triggers" but GitHub's
    defaults, and the distinction is exactly what this file is about.
    """
    lines = text.splitlines()

    on_index = next(
        (index for index, line in enumerate(lines)
         if indent_of(line) == 0 and ON_KEY.match(line)),
        None,
    )
    if on_index is None:
        return None

    triggers = block_under(lines, on_index)

    found = find_key(triggers, "pull_request")
    if found is None:
        return None

    pull_request_index, _ = found
    types = find_key(block_under(triggers, pull_request_index), "types")
    if types is None:
        return None

    body = block_under(triggers, pull_request_index)
    types_index, inline = types
    return parse_list(body, types_index, inline)


def workflows_running_the_gate() -> list[Path]:
    return sorted(
        path for path in list(WORKFLOWS.glob("*.yml")) + list(WORKFLOWS.glob("*.yaml"))
        if GATE_SCRIPT in path.read_text(encoding="utf-8")
    )


class TheGateRerunsWhenTheLabelChangesTests(unittest.TestCase):
    def test_exactly_one_workflow_runs_the_gate(self):
        # Without this the rest of the file passes vacuously the day the step
        # is renamed out from under it.
        self.assertEqual(
            1, len(workflows_running_the_gate()),
            f"expected exactly one workflow to run {GATE_SCRIPT}")

    def test_a_label_event_reaches_the_gate(self):
        for path in workflows_running_the_gate():
            with self.subTest(workflow=path.name):
                types = pull_request_types(path.read_text(encoding="utf-8"))

                self.assertIsNotNone(
                    types,
                    f"{path.name} takes GitHub's default pull_request types, which "
                    "exclude `labeled` — so adding `skip-docs` re-runs nothing, and "
                    "the gate's own failure message is a lie.")
                self.assertIn(
                    "labeled", types,
                    f"{path.name} must re-run when a label changes: the gate's verdict "
                    "depends on the PR's labels.")

    def test_naming_the_types_keeps_the_defaults(self):
        # `types:` replaces GitHub's defaults rather than extending them, so
        # adding `labeled` and nothing else would stop a pushed commit ever
        # re-checking the gate — a much worse bug than the one being fixed.
        for path in workflows_running_the_gate():
            with self.subTest(workflow=path.name):
                types = pull_request_types(path.read_text(encoding="utf-8")) or []

                for expected in REQUIRED_TYPES:
                    self.assertIn(expected, types, f"{path.name} must trigger on {expected}")


class TriggerParsingTests(unittest.TestCase):
    """The parser reads YAML with regexes, so it gets its own tests."""

    def test_an_absent_types_key_is_not_an_empty_list(self):
        self.assertIsNone(pull_request_types("on:\n  pull_request:\n  push:\n    branches: [main]\n"))

    def test_an_inline_list_is_read(self):
        self.assertEqual(
            ["opened", "labeled"],
            pull_request_types("on:\n  pull_request:\n    types: [opened, labeled]\n"))

    def test_a_block_list_is_read(self):
        self.assertEqual(
            ["opened", "labeled"],
            pull_request_types("on:\n  pull_request:\n    types:\n      - opened\n      - labeled\n"))

    def test_a_trailing_comment_is_not_part_of_the_last_item(self):
        self.assertEqual(
            ["opened", "labeled"],
            pull_request_types("on:\n  pull_request:\n    types: [opened, labeled]  # why\n"))

    def test_a_quoted_on_key_is_still_the_trigger_block(self):
        self.assertEqual(
            ["labeled"],
            pull_request_types('"on":\n  pull_request:\n    types: [labeled]\n'))

    def test_another_triggers_types_is_not_mistaken_for_the_pull_requests(self):
        self.assertIsNone(pull_request_types(
            "on:\n  issues:\n    types: [labeled]\n  pull_request:\n"))

    def test_a_workflow_with_no_pull_request_trigger_has_no_types(self):
        self.assertIsNone(pull_request_types("on:\n  push:\n    branches: [main]\n"))


if __name__ == "__main__":
    unittest.main()
