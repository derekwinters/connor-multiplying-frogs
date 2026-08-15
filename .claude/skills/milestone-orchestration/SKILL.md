---
name: milestone-orchestration
description: Drive a set of handed-off issues through merge, one at a time, each on a branch cut from the latest main. Use when Derek hands over several issues at once and wants them delivered, not just proposed.
---

# milestone-orchestration

The owner-invoked counterpart to the nightly builder. Same dev agent, same TDD,
same one-PR-per-issue — one difference, and it is the whole point:

|  | Nightly builder | This |
| --- | --- | --- |
| Gate between issues | **PR opened** | **PR merged** |
| Invoked by | the schedule | Derek, explicitly |
| On failure | drop the issue, continue | **halt the whole run** |

**No script.** The issue set comes from the handoff — whatever Derek said to
work on — so there is nothing to compute and nothing to select.

## Merged is the gate

Each issue is finished only when its PR is **merged**, not when it is opened.
Then the next issue starts, on a branch cut fresh from the updated `main`.

That is what makes a multi-issue run conflict-free by construction. Every new
branch already contains all the previously delivered work, so two issues in the
same run cannot produce conflicting diffs — there is never a moment when two
branches are open against the same base.

The nightly builder can open three PRs at once because they sit unmerged until
Derek reviews them, and he resolves any overlap by choosing what to merge.
Here, the run *is* merging, so the sequencing has to do that job instead.

The cost is real: a run of six issues takes as long as six CI runs in series.
That is the price of ending with six merged issues and no conflicts, rather
than six open PRs that need untangling.

## The loop

For each issue, in the order Derek gave them:

1. **`git fetch origin main`** and cut a fresh branch from it. Not from the
   previous issue's branch — from `main`, after the previous merge landed.
2. Mark the issue **`in-progress`**.
3. Hand it to the **dev agent** (`.claude/agents/dev.md`): tests first, one
   issue, one PR, with a `## Deviations and Decisions` section and `Closes #N`.
4. Apply **`skip-docs`** immediately if the PR touches no `docs/` files.
5. Wait with **`ci-watch`** until every check has finished.
6. Green → **merge**. Then go to 1.
7. Not green → **stop the run.**

## A failure halts everything

Not "skip it and carry on" — that is the nightly builder's rule, and it is
wrong here.

The difference is what the next issue is built on. In this run, issue two is
built on the `main` that issue one merged into. If issue one failed and was
skipped, issue two is built on a `main` that is missing work the handoff
assumed was there — and whether that matters is unknowable without reading
both issues. Continuing means building on a base nobody intended.

There is also a plainer reason: Derek asked for these issues, and he is
around. A run that stops with "issue three failed, here is why" is one he can
act on immediately. A run that quietly delivers four of six is one he has to
audit.

**Stop at the failing issue.** Say which issue, what failed, and what the
remaining ones were. The merged work stays merged — it is finished and correct.

## Owner-invoked only

**This is never part of a scheduled routine.** Not nightly, not hourly, not
triggered by a label.

It merges. Everything else in the pipeline stops at "PR opened" so that a
human sees the work before it becomes the game — and that gate is the entire
reason the pipeline is trustworthy. This skill exists because Derek sometimes
wants to say "do these six and merge them as you go", which is him applying
that judgement up front, to a set he chose.

An automated trigger would turn a decision he made about six specific issues
into a standing grant to merge anything. If you find yourself wiring this into
a workflow, that is the mistake.

## See also

- `pipeline-dev` — the scheduled sibling that stops at "PR opened"
  (from ai-sdlc; install with `gh skill` — see issue #354)
- `ci-watch` — waits for checks and reports pass or fail
- `.claude/agents/dev.md` — what actually writes the code
- `docs/engineering/issue-pipeline.md` — how this differs from the nightly round
