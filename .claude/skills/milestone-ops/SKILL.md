---
name: milestone-ops
description: List, count, close, and reopen GitHub milestones, and resolve a milestone title to the number the API needs. Use whenever a milestone number is required, when checking how much is left in a milestone, or when closing one at release.
---

# milestone-ops

**The GitHub MCP toolset exposes no milestone CRUD at all** — no list, no close,
no count. This is the direct REST helper everything else calls.

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

python3 .claude/skills/milestone-ops/milestone_ops.py list
python3 .claude/skills/milestone-ops/milestone_ops.py number --title v0.1
python3 .claude/skills/milestone-ops/milestone_ops.py count  --title v0.0.1
python3 .claude/skills/milestone-ops/milestone_ops.py close  --title v0.0.1
python3 .claude/skills/milestone-ops/milestone_ops.py reopen --title v0.0.1
```

## `milestone` takes a number, not a title

The one thing to remember. `issue_write`'s `milestone` parameter — and the REST
API's — takes the milestone's **number**:

```json
{"milestone": 2}       ✅
{"milestone": "v0.1"}  ❌  a 422, or silently wrong
```

So anything holding a title resolves it first:

```bash
NUMBER=$(python3 .claude/skills/milestone-ops/milestone_ops.py number --title v0.1)
```

### Titles are compared exactly

`v0.1` and `V0.1` are **different milestones**. `resolve_number` will not
normalise them together, because that is how work lands in the wrong milestone
silently — both spellings look right in a comment. An unknown title is an error
that lists what does exist:

```text
No milestone titled 'V0.1'. There is: Direct Involvement Needed, v0.0.1, v0.1.
```

## Read milestones live

Never from a list in the docs. `list` queries the API every time, which is why
[conventions.md](../../../docs/intro/conventions.md) deliberately contains no
list of current milestones.

```text
  #1 v0.0.1 [open] 30 open, 44 closed
  #2 v0.1 [open] 7 open, 0 closed
  #3 Direct Involvement Needed [open] 5 open, 2 closed
```

The `FROZEN` flag appears on any milestone whose description starts with the
freeze marker — triage skips those when choosing where to route a new issue.

## `close` refuses while there is open work

Closing a milestone with open issues **hides that work**: it leaves the
milestone view, and nothing else is watching for it. So `close` refuses, and
says how many:

```text
'v0.1' still has 3 open issue(s). Closing it would hide them: they leave the
milestone view and nothing else is watching. Move or close them first, or pass
--force if that is genuinely what you mean.
```

If the work really is abandoned, move or close the issues — that is a decision,
and it should look like one rather than being a side effect of tidying up.
`--force` exists for when it genuinely is what you mean.

## Verified

Against this repo: `list` reports the three real milestones with their counts,
`number --title v0.1` returns `2`, `count --title v0.0.1` returns the live open
count, and `--title V0.1` is refused.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py milestone-ops
```

15 tests, with the API injected as a callable — no network.
