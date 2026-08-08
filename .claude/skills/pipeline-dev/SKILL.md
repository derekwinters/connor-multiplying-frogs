---
name: pipeline-dev
description: Run a build round — select the ready queue and deliver each issue as its own branch and PR. Use for the nightly build, or when asked to work through everything that is ready.
---

# pipeline-dev

The nightly builder. Pick the queue, then work it one issue at a time.

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

# 1. What are we building tonight, and in what order?
python3 .claude/skills/pipeline-dev/select_queue.py < state.json

# 2. For each selected issue, in order:
#      mark in-progress → delegate to the dev agent → it opens its own PR
```

## The two things this never does

**It never merges, and it never closes.** Not a green PR, not a one-line typo
fix, not its own work.

The pipeline exists to get work to the point where Derek can look at it. If it
also merged, the review step would be optional in practice — and the whole
design assumes a human reads the plan before it becomes the game. An agent that
can approve its own output has no gate on it at all.

Closing is the same rule from the other end. A PR body carries `Closes #N`, so
GitHub closes the issue when Derek merges. The pipeline closing it directly
would mark work done that nobody accepted.

## Step 1 — the queue

`select_queue.py` decides eligibility and ordering, deterministically. Ready,
in focus, unblocked, not parked, not an epic, no open PR closing it — ordered
dependencies-first and capped (3 by default).

Both decisions are in code rather than in the model on purpose: a queue you can
reproduce is a queue you can argue with, and a model asked to pick work will
pick the interesting work rather than the work that unblocks the most.

## Step 2 — one issue at a time

Serial. Not a concurrency limit that happens to be set to one — parallel agents
on one repository fight over `ProjectSettings.asset`, over release-please, over
PR numbering. Selecting three issues and delivering them one after another runs
nothing in parallel.

Each issue gets a **fresh delegated session**, because context from the last
issue is a liability: the agent that just spent an hour on the audio system will
find a way to make the next issue about audio.

For each issue, in queue order:

1. Mark it **`in-progress`** — before any work, so a crashed run is visible as
   a stuck issue rather than an invisible one.
2. Hand it to the **dev agent** (`.claude/agents/dev.md`), which works it under
   strict TDD and opens its own PR.
3. Move on. Do not wait for CI, do not review, do not merge.

## One branch, one PR, one issue

**Never a combined PR**, even for two small issues in the same file, even when
the second is a one-liner.

| | One PR per issue | One PR for the round |
| --- | --- | --- |
| Reviewable | yes | not really |
| Reject one bad change | revert one PR | unpick a diff |
| `Closes #N` | one keyword, one issue | ambiguous |
| Conventional Commit | one honest type | pick the least wrong one |

The last row is the one that bites quietly. release-please derives the version
from the commit type, so a PR combining a `feat:` and a `fix:` has to be
labelled one of them, and whichever is chosen ships the wrong version number.

The PR title is that issue's single Conventional-Commit line. The body carries
the plain-English lead, `## Deviations and Decisions`, a `**Docs:**` line, and
`Closes #N`.

## `skip-docs` goes on immediately

If the PR touches no files under `docs/`, apply **`skip-docs` the moment the PR
is open** — before marking anything, before moving to the next issue, before
anything else.

The docs reconciliation gate fails a code-only PR and the label is the
sanctioned escape hatch. Apply it late and the round's first visible result is
a red X on a PR that was never wrong, which trains everyone to ignore red.

Reaching for it should still be rare and it needs a written justification in
`## Deviations and Decisions` — the gate cannot tell "no docs needed" from
"forgot the docs", which is exactly why a human has to say which it was.

## A failing issue is dropped, and the round continues

If an issue cannot be completed — tests won't pass, the plan turns out to be
wrong, the checklist has a box that cannot be ticked — **delete the branch,
open no PR, and go to the next issue.**

No half-PR, no draft, no "most of it works". A PR that does not close its issue
still costs a review, and a checklist with an untickable box is a triage problem
that a partial implementation hides rather than surfaces.

The issue keeps `in-progress`, which is deliberate. Reconcile finds an open
`in-progress` issue with no open PR and nothing on `main`, and returns it to
`ready-for-work` — so a failure self-heals by the next round, and until then it
is visible on the dashboard rather than silently forgotten.

One failure never aborts the round. The other issues are unrelated.

## When this runs

Nightly at 03:00 UTC — after reconcile (01:00) and analysis (02:00). Fix the
state, triage against fixed state, then build against a triaged queue.

There is no reactive equivalent. `/approve` puts an issue in the queue; it does
not start a build. Approving five issues in an evening should not launch five
agents while Derek is still reading.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py pipeline-dev
```

33 tests over `select_queue.py` — eligibility, the native ∪ text blocker union,
closing-keyword PR association, topological ordering, and the cap. No network.

## See also

- `.claude/agents/dev.md` — what actually writes the code
- `docs/engineering/issue-pipeline.md` — the stage and the schedule
- `docs/engineering/agent-workflow.md` — TDD, the lead, deviations
- `docs/engineering/ci-cd.md` — the docs reconciliation gate
