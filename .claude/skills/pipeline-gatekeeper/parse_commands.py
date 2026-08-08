#!/usr/bin/env python3
"""Turn an issue comment into a list of pipeline actions.

**Deterministic, and the only thing that decides what a comment means.** Not a
model. The gatekeeper can label issues, fire scheduled routines, and queue work
for an agent that writes code — so the component deciding whether to do any of
that is a parser with a fixed vocabulary, and the worst a confused model can do
is *suggest* something.

Two of the pipeline's safety properties live here, in the parser rather than
downstream, because a check the caller has to remember is a check that gets
forgotten:

- **The bad-actor gate.** Only the repository owner's comments are honoured.
- **Idempotency.** A comment already carrying the 👀 watermark is never
  re-applied.

Pure: data in, actions out. No GitHub I/O, no network, no subprocess — see
`test_the_module_imports_nothing_that_talks_to_github`.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import difflib
import re

# The vocabulary. Nothing here closes an issue, edits a body, or merges a PR:
# those have GitHub buttons, and a command set that can do irreversible things
# will eventually do one by accident.
COMMANDS = (
    "admit",      # bring an issue into the pipeline
    "approve",    # the triaged plan is right — queue it for work
    "revise",     # the plan is not right; re-triage with these notes
    "redo",       # the built work is not right; queue it again
    "propose",    # ask triage to produce a plan
    "park",       # set aside deliberately
    "unpark",     # bring it back
    "milestone",  # set the milestone, by title
    "focus",      # set the pipeline's focus milestone
    "cap",        # set the max concurrent in-progress issues
)

# Only meaningful on the one dashboard issue: they configure the pipeline, not
# an issue.
DASHBOARD_ONLY = {"focus", "cap"}

# Epics are containers; their children are the work. An admitted or approved
# epic is one the builder would try to build. Parking or re-milestoning a whole
# epic is still reasonable, so the exclusion is per command rather than blanket.
EPIC_EXCLUDED = {"admit", "approve", "revise", "redo", "propose"}
EPIC_LABEL = "type:epic"

COMMAND_LINE = re.compile(r"^\s{0,3}/([A-Za-z][\w-]*)\s*(.*?)\s*$")
FENCE = re.compile(r"^\s*(```|~~~)")


class Action:
    """A recognised command, with its argument."""

    def __init__(self, command: str, argument: str = "") -> None:
        self.command = command
        self.argument = argument

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Action({self.command!r}, {self.argument!r})"


class Skip:
    """A command that was recognised as a command and not acted on."""

    def __init__(self, reason: str, detail: str, command: str = "") -> None:
        self.reason = reason
        self.detail = detail
        self.command = command

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Skip({self.reason!r}, {self.detail!r})"


class Context:
    """Everything about the issue the parser needs, and nothing else."""

    def __init__(self, owner: str, labels=None, is_dashboard: bool = False) -> None:
        self.owner = owner
        self.labels = list(labels or [])
        self.is_dashboard = is_dashboard

    @property
    def is_epic(self) -> bool:
        return EPIC_LABEL in self.labels


class Outcome:
    def __init__(self, actions: list[Action], skips: list[Skip], acknowledge: bool) -> None:
        self.actions = actions
        self.skips = skips
        self._acknowledge = acknowledge

    @property
    def should_acknowledge(self) -> bool:
        """Should the gatekeeper react to or reply to this comment?

        False for a comment from anyone but the owner — even a reaction would
        let a stranger make the bot act on their comment, which is a smaller
        hole of the same shape as obeying it.
        """
        return self._acknowledge


def _command_lines(body: str):
    """Lines that look like commands, skipping fenced code blocks.

    A command inside a fence is documentation. Without this, writing up the
    vocabulary in a comment would execute it.
    """
    in_fence = False

    for line in (body or "").splitlines():
        if FENCE.match(line):
            in_fence = not in_fence
            continue
        if in_fence:
            continue

        match = COMMAND_LINE.match(line)
        if match:
            yield match.group(1), match.group(2)


def parse(comment: dict, context: Context) -> Outcome:
    """The actions a comment asks for, and the ones deliberately not taken."""
    author = (comment.get("author") or "").lower()

    # The bad-actor gate, first. Checked against the configured owner login —
    # not "is a collaborator", because access is granted for reasons that have
    # nothing to do with driving the pipeline.
    if author != (context.owner or "").lower():
        found = list(_command_lines(comment.get("body", "")))
        skips = [Skip("not-owner", f"/{name} from {comment.get('author')}", name)
                 for name, _ in found if name in COMMANDS]
        return Outcome([], skips, acknowledge=False)

    # Idempotency. The watermark is on the comment, so it survives workflow
    # re-runs, sweeps, and redelivered webhooks alike.
    if comment.get("watermarked"):
        found = [name for name, _ in _command_lines(comment.get("body", "")) if name in COMMANDS]
        skips = [Skip("already-applied", f"/{name} was applied on an earlier run", name)
                 for name in found]
        return Outcome([], skips, acknowledge=False)

    actions: list[Action] = []
    skips: list[Skip] = []

    for name, argument in _command_lines(comment.get("body", "")):
        if name not in COMMANDS:
            suggestion = difflib.get_close_matches(name, COMMANDS, n=1, cutoff=0.6)
            hint = f" Did you mean /{suggestion[0]}?" if suggestion else ""
            skips.append(Skip("unknown-command", f"/{name} is not a command.{hint}", name))
            continue

        if name in DASHBOARD_ONLY and not context.is_dashboard:
            skips.append(Skip(
                "not-dashboard",
                f"/{name} configures the pipeline, so it only applies on the dashboard issue.",
                name))
            continue

        if name in EPIC_EXCLUDED and context.is_epic:
            skips.append(Skip(
                "epic-excluded",
                f"/{name} does not apply to a `{EPIC_LABEL}`. Epics are containers — "
                "their children are the work.",
                name))
            continue

        if name == "cap" and not _is_positive_integer(argument):
            skips.append(Skip(
                "cap-invalid",
                f"/cap needs a positive whole number, got '{argument}'.",
                name))
            continue

        actions.append(Action(name, argument))

    return Outcome(actions, skips, acknowledge=bool(actions or skips))


def _is_positive_integer(value: str) -> bool:
    return value.isdigit() and int(value) > 0
