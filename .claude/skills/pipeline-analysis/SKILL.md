---
name: pipeline-analysis
description: Run a triage round — find every issue waiting for triage and dispatch dw-triage-issue once per issue. Use for the nightly backstop round, or when asked to triage everything untriaged.
---

# pipeline-analysis

The nightly triage round. A loop, and deliberately nothing more.

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

# 1. Who needs triage, and what does each one carry into it?
python3 .claude/skills/pipeline-analysis/select_triage.py < issues.json

# 2. One dw-triage-issue invocation per eligible issue, bounded concurrency.
#      dw-triage-issue 47
#      dw-triage-issue 51
#      ...
```

## This skill writes nothing

**Every write belongs to `dw-triage-issue`.** No labels, no comments, no milestones,
no summary comment saying what the round did. The dispatcher's entire output is
having invoked the single-issue skill the right number of times.

That is the point of splitting the two. A round that made its own writes would
be a second thing that can be wrong about an issue — and wrong at batch scale,
across every issue in the queue, in a way no single triage run could produce.
Keeping the loop empty means a bad night is a set of independently-bad issues,
each one reviewable and fixable on its own.

It also keeps `dw-triage-issue` genuinely standalone. Reactive triage invokes it
directly with no dispatcher anywhere in sight, and that only works while the
dispatcher owns nothing the single-issue skill needs.

## Step 1 — discovery

`select_triage.py` decides eligibility. Open, carrying `ai-triage`, and not an
epic, the dashboard, or `parked`.

Each eligible issue comes back with its number, its milestone, and the latest
owner `/revise`, `/redo`, or `/propose` note. **Pass that note through.** It is
why a re-triage answers the actual objection instead of producing the same plan
that was rejected the first time.

The script is pure and stdlib-only: JSON in, JSON out, no network. Fetching the
issues is the caller's job, which is what makes the eligibility rules testable
without a live repo.

## Step 2 — dispatch, with concurrency chosen above

Invoke `dw-triage-issue` once per eligible issue, several at a time.

**The concurrency limit is set by the orchestration layer, not written into the
script.** `select_triage.py` reports what needs triage; it has no opinion on how
fast to work through it. The right number depends on API rate limits and how
much the runner can take — things that change without the eligibility rules
changing at all.

A number hard-coded in the discovery script is a number nobody can adjust for
one busy night without editing a file that has tests pinned to it, and it would
be one more thing to keep in sync with the dev round's own limit. Keep it where
the run is configured.

Issues are dispatched oldest-first. A triage that fails is one issue's problem:
it keeps `ai-triage`, so the next round picks it up again. Do not abort the
round for it.

## When this runs, and when it doesn't

| | Scheduled round | Reactive triage |
| --- | --- | --- |
| Trigger | nightly, 02:00 UTC | `ai-triage` newly added |
| Scope | every eligible issue | exactly one issue |
| Runs this skill? | yes | **no** — `dw-triage-issue` directly |

**Reactive triage is the normal path.** Filing an issue and getting triage hours
later is a bad experience when the person who filed it is sitting right there,
so the gatekeeper pokes a Routine the moment `ai-triage` appears and that issue
gets triaged immediately.

**This round is the backstop.** It exists for what reactive triage missed: the
POST that failed, the secrets that were never configured, an issue whose label
was changed by hand rather than by a command, one carried back by the blocker
sweep. On a healthy night it finds nothing, and that is the expected result — an
empty round is the system working, not a wasted run.

It runs after reconcile (01:00) and before development (03:00): fix the state,
triage against fixed state, then build against a triaged queue.

Reactive fire happens **only when `ai-triage` is newly present**, never on an
idempotent re-add. Otherwise one stuck comment becomes a triage run every sweep.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py pipeline-analysis
```

18 tests over `select_triage.py` — eligibility, the carried owner note, and the
pure-`process` / stdin-stdout shape. No network in any of them.

## See also

- `dw-triage-issue` — everything this skill dispatches to
- `docs/engineering/issue-pipeline.md` — the stage, the schedule, reactive fire
