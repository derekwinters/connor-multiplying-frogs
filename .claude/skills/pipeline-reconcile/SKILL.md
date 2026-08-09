---
name: pipeline-reconcile
description: Sweep the board for drift between what the labels claim and what GitHub shows, fixing the mechanical cases and flagging the rest. Use for the nightly sweep, or when the board looks wrong.
---

# pipeline-reconcile

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

# Everything, including the two cron-only fixes.
python3 .claude/skills/pipeline-reconcile/reconcile.py < state.json

# The event path — same script, cron-only fixes omitted.
jq '. + {events_only: true}' state.json | \
  python3 .claude/skills/pipeline-reconcile/reconcile.py
```

Snapshot in, findings out. `process(data)` is pure and does no I/O; fetching
the state and applying the fixes both live at the edges.

## The rules

**Auto-fix** — mechanical, one correct answer, no judgement:

| Finding | Condition | Action | Runs |
| --- | --- | --- | --- |
| `strip_labels` | closed issue still carrying a pipeline-state label | remove it | every pass |
| `requeue` | open, `in-progress`, no open PR, not on `main` | → `ready-for-work` | **cron only** |
| `requeue_triage` | a state label with no triage-authored analysis | → `ai-triage` | **cron only** |

**Flag** — surfaced, never touched:

| Finding | Condition |
| --- | --- |
| `flag_merged_but_open` | the work is on `main`, the issue is still open |
| `flag_orphaned_analysis` | an analysis comment with no state label |
| `flag_orphaned_ready` | `ready-for-work` with no milestone |
| `flag_prose_dependency` | `Blocked by #N` in the body with no native edge |
| `flag_cycle` | a dependency cycle |
| `flag_dashboard_count` | two dashboard issues, or none |

The split is the design. A reconciler that guesses is one you have to audit,
and one you have to audit is one you turn off — at which point it is worse than
absent, because the dashboard still says it ran.

Every auto-fix is a **label move**. Nothing here edits a body, posts a comment,
sets a milestone, or closes an issue.

## Why `requeue` and `requeue_triage` are cron-only

Because the drift they detect is **indistinguishable from work in flight.**

An issue the builder picked up ten seconds ago has `in-progress` and no PR yet.
That is exactly `requeue`'s condition. On the event path — which runs on every
comment, at any moment — firing it would yank an issue out from under a running
agent, which then opens a PR for an issue the board says is merely ready.

`requeue_triage` is the mirror image. Triage posts its analysis comment
*before* setting the state label, deliberately, so a crash leaves a plan on
`ai-triage` rather than an approval-pending issue with nothing to approve.
Between those two writes the issue legitimately has one and not the other —
and mid-write, the sweep's condition holds.

By 01:00 the transient has resolved. An issue that still looks stalled hours
after anything touched it genuinely is stalled.

**They are omitted entirely rather than softened with a time threshold.** A
threshold would be a guess about how long an agent takes, and it would be wrong
in both directions: too short and it interrupts a slow build, too long and a
stall sits there all night. `events_only=True` drops them from the output
completely, so there is no partially-correct middle version to reason about.

### Why `strip_labels` is safe on every pass

It acts only on an **already-closed** issue, and a closed issue is not
transiently anything. There is no in-flight state where "closed, still carrying
`in-progress`" is the correct configuration that a concurrent write is about to
resolve — the work is over. Nothing it removes can be needed a moment later,
so there is no version of it that races.

## The sweep never closes an issue

Not even when the work is demonstrably on `main` and the issue is demonstrably
open. That is `flag_merged_but_open`, and it stays a flag.

"The work landed" and "the work is accepted" are different claims, and only the
second justifies closing. What the sweep can see supports the first. If the
keyword parse is wrong, or the commit came from a revert, or the PR merged
something adjacent, then closing means marking work done that nobody agreed to
— silently, overnight, on a project whose entire design assumes a human sees
things before they become the game.

`flag_merged_but_open` is also checked **before** the stall rule, so an issue
already on `main` never reads as a stall. Without that ordering it would be
requeued, rebuilt, and requeued again — a fresh duplicate PR every night.

## Done-ness comes from commit bodies

A closing keyword in a merged commit's **body**. Never the subject line, never
a bare `#N` or `Refs #N`.

The subject is excluded because a squash merge appends `(#150)` to it, and that
is a *PR* number — reading it as a closing reference would mark an unrelated
issue done every time anything merges. Bare references are excluded because
commits and PRs mention issues constantly for context.

## Where findings go

Flags render into the dashboard's **⚠️ Reconcile** section, regenerated on every
render like everything else on the board. Nothing is remembered between runs:
a flag disappears when the condition stops holding, and a flag that persists
across several days is one nobody has dealt with.

That is also why flags do not post comments. A flag is a statement about the
board's current state, and a comment would outlive the state it described.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py pipeline-reconcile
```

37 tests. Two are worth knowing about: one asserts no finding can ever close an
issue, and one reads this module's source to assert it does not define its own
`has_analysis_signature` — that recognizer is imported from `dw-triage-issue`,
because two copies drift into a requeue↔repair loop that reports no error.

## See also

- `dw-triage-issue` — owns `has_analysis_signature`
- `pipeline-dashboard` — renders the flags
- `docs/engineering/issue-pipeline.md` — the stage and the schedule
