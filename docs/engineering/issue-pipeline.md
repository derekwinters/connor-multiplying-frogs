# Issue pipeline

The AI issue-management pipeline: how an idea becomes a triaged issue, an
approved issue, a queued issue, and a merged PR — mostly without anyone typing
anything except `/approve`.

Everything in the `pipeline-*` skills implements what this page describes. When
the two disagree, this page is the specification and the code is the bug.

## The model

Four ideas, and the rest follows from them.

**1. Labels are the state machine.** An issue's pipeline state is a label, not a
field in a database somewhere, not a line in its body. GitHub is the store. That
means the state is visible in the issues list, filterable, editable by hand in an
emergency, and impossible to lose by dropping a file.

**2. Comments are the owner's control surface.** Derek drives the pipeline by
commenting `/approve`, `/park`, `/focus v0.1` on issues. No dashboard app, no
CLI, no separate tool to log into. The place where the conversation about an
issue already happens is the place where the decision about it is made.

**3. A gatekeeper translates one into the other.** A deterministic script — not
a model — reads comments, parses commands, and applies label changes. This is
the whole safety story: the component that can change state is a parser with a
fixed vocabulary, so the worst a confused language model can do is *suggest*
something, never *do* it.

**4. Only the owner's commands are honoured.** A command in a comment from
anyone else is ignored. See [the bad-actor gate](#the-bad-actor-gate).

## States

Exactly one state label per open issue.

| State | Meaning | Set by | Leaves when |
| --- | --- | --- | --- |
| *(none)* | brand new, not yet seen | issue creation | triage picks it up |
| `ai-triage` | queued for automated triage | analysis, `/retriage` | triage finishes |
| `pending-approval` | triaged; waiting on a human | triage | `/approve`, `/park` |
| `needs-clarification` | triage could not proceed without an answer | triage | `/retriage` after the answer |
| `ready-for-work` | approved and eligible for the builder | `/approve` | the builder picks it up |
| `in-progress` | an agent is building it right now | the builder | its PR merges, or it fails |
| `parked` | deliberately set aside | `/park` | `/unpark` |

Plus `dashboard`, which is not a state — it marks the one live dashboard issue,
which the pipeline never triages or works.

**Invariant:** `ready-for-work` ⇒ the issue has a milestone. Enforced at the
`/approve` gate and re-checked by the reconciler. See
[Conventions](../intro/conventions.md#the-invariant-ready-for-work-has-a-milestone).

**Invariant:** an issue has at most one state label. The gatekeeper *replaces*
rather than adds; the reconciler flags any issue carrying two.

### The normal path

```text
(new) → ai-triage → pending-approval → ready-for-work → in-progress → closed
                            ↓                ↑
                   needs-clarification ──────┘
                            ↓
                         parked → (unpark) → ai-triage
```

## Commands

Every command is a line in an issue comment beginning with `/`. A comment may
contain several; they apply in order. Text around them is ignored, so you can
explain yourself in the same comment.

| Command | Effect |
| --- | --- |
| `/admit` | bring an issue into the pipeline — it becomes `ai-triage`. |
| `/propose` | ask triage to produce a plan for it. |
| `/approve` | the plan is right → `ready-for-work`. Subject to both approval gates. |
| `/revise <notes>` | the plan is not right → back to `ai-triage`, with the notes. |
| `/redo` | the built work is not right → queue it again. |
| `/park` | set aside deliberately. |
| `/unpark` | bring it back. |
| `/milestone <title>` | set the issue's milestone, by title. |
| `/focus <title>` | set the pipeline's focus milestone. **Dashboard issue only.** |
| `/cap <n>` | set the max concurrent `in-progress` issues. **Dashboard issue only.** |

Deliberately absent: anything that closes an issue, edits a body, or merges a
PR. Those have perfectly good GitHub buttons, and a command vocabulary that can
do irreversible things is a vocabulary that will eventually do one by accident.

### Where each command is refused

The parser refuses rather than guessing, and every refusal carries a reason
code so the ack can say which rule applied:

| Reason | When |
| --- | --- |
| `not-owner` | the comment is from anyone but the owner — dropped **silently** |
| `already-applied` | the comment already carries the 👀 watermark |
| `unknown-command` | `/aprove` — never guessed at; the reply names the closest match |
| `not-dashboard` | `/focus` or `/cap` on an ordinary issue |
| `cap-invalid` | `/cap lots`, `/cap 0`, `/cap -1` |
| `epic-excluded` | `/admit`, `/propose`, `/approve`, `/revise`, `/redo` on a `type:epic` |

**Epics are containers**, so the five commands that would put one in the
builder's path are refused on them — their children are the work. `/park` and
`/milestone` still apply to a whole epic, because both are reasonable things to
want.

Two parsing rules worth knowing:

- **A command must start its line.** "see /approve for details" is a mention.
- **A command inside a code fence is ignored**, or writing up this table in a
  comment would execute it.

### Unknown commands

A line starting with `/` that isn't in the table gets a 😕 reaction and a reply
naming the closest match. It is never guessed at — `/aprove` does not approve.

## The bad-actor gate

**Commands are honoured only from the repository owner.** Everything else is
read, recognised, and dropped.

The pipeline can label, comment, fire scheduled routines, and queue work for an
agent that writes code. That is not authority to hand to whoever can type in a
comment box on a public repo.

Two details that matter:

- **Ignored means silent.** No reply, no reaction, no label change. Replying
  would let a stranger make the bot post — a smaller hole than acting on the
  command, but the same shape. The drop is recorded in the workflow log, which
  is where you look if you expected something to happen and it didn't.
- **Author association is not enough.** The check is against the configured
  owner login, not "is a collaborator" or "has write access". Access can be
  granted for a reason that has nothing to do with driving the pipeline.

## Idempotency: the 👀 watermark

Two things process comments — the `issue_comment` workflow and the periodic
sweep — and neither can assume the other didn't get there first. Webhooks are
also redelivered.

So before acting on a comment, the gatekeeper adds a **👀 reaction from its own
account**, and it skips any comment that already has one.

- The watermark is **on the comment**, so it survives everything: workflow
  re-runs, sweeps, redelivered webhooks, and the queue being rebuilt.
- It is added **before** the actions are applied, not after. A crash mid-apply
  leaves a claimed-but-unfinished comment, which is a stuck command someone can
  see; the alternative — reacting afterwards — risks applying twice, which is a
  state change nobody asked for. Prefer visible-and-stuck to silent-and-doubled.
- It is a **claim**, so the outcome gets its own reaction: 🚀 applied, 😕
  refused or not understood.

To make the gatekeeper reconsider a comment, remove its 👀.

## The approval gates

Both gates **refuse and explain**. Neither ever fixes the problem itself.

That posture is deliberate. Auto-correcting at an approval gate means the
pipeline quietly decides something Derek was in the middle of deciding, and the
first he hears of it is a build he didn't expect. A refusal costs one comment
and ten seconds; a wrong auto-fix costs a milestone's worth of misordered work.

### Gate 1: `/approve` requires a milestone

**Invariant:** an issue cannot become `ready-for-work` without a milestone.

Without one it is approved work no builder will ever select, because selection
runs against the focus milestone. It leaves the queue silently, which is the
worst way for work to disappear.

The gatekeeper **refuses** with an `approve-no-milestone` skip and a hand-back
asking *which milestone?*. It never picks one — not when only one is open, and
not when the issue's siblings all sit in the same milestone.

It reads **only the issue's milestone field**. Triage sets that field, and
scraping a `/milestone v0.1` out of the comment history would make the gate
depend on what was said rather than what is true; the two disagree the moment
anything is edited. An inline `/milestone` in the same comment does not feed
this gate either — that command fires independently, and if it succeeds the
next `/approve` sees the field it set.

`/milestone` is deliberately **not** subject to this gate. It is the command
that fixes a missing milestone, so gating it on having one would make the fix
impossible.

### Gate 2: milestone order

**Invariant:** work is approved in milestone order. An issue in a later
milestone cannot become `ready-for-work` while the focus milestone still has
open work.

Milestones are a plan. Approving `v0.2` work while `v0.1` still has twenty open
issues is how a project ends up with three half-finished versions.

The gatekeeper **refuses** and replies with the focus milestone, how many issues
remain open in it, and the two ways forward: finish the focus milestone, or
`/focus` the later one deliberately. It never bumps the issue's milestone, and
never moves focus on its own.

The escape hatch is `/focus`, which is a deliberate act, recorded on the
dashboard, and visible to everyone.

## Auto-revisit when a blocker clears

An issue parked *because it was blocked* should not need a human to remember it.
The sweep looks for issues whose recorded blockers have all closed, and returns
them to `ai-triage` with a comment saying which blocker cleared.

Conditions, all required:

- the issue is `parked` or `needs-clarification`;
- it has at least one native blocked-by relationship;
- every blocking issue is closed;
- it was not parked by an explicit `/park` with a reason unrelated to blocking —
  a park the owner chose is a park the owner un-chooses.

### The `type:wireframe` carve-out

**`type:wireframe` issues are never auto-revisited.**

Unblocking a wireframe doesn't make it agreeable. What a wireframe needs is a
person looking at a picture and saying yes — usually Connor. Waking one up
automatically produces an issue that says "ready" and isn't, and the pipeline
would then hand it to a builder that has no wireframe to build against.

They surface on the dashboard as waiting-on-a-human instead.

## `/focus` and `/cap` live on the dashboard issue

Both are stored as HTML-comment markers in the body of the dashboard issue:

```html
<!-- pipeline:focus=v0.0.1 -->
<!-- pipeline:cap=1 -->
```

The dashboard issue is the one carrying the `dashboard` label. There is exactly
one; the reconciler flags it if there are two or none.

### Why markers, and why re-rendering beats hand-editing

The dashboard body is **regenerated wholesale** on every render — every section
is derived from live GitHub state. That is what makes it trustworthy: nothing on
it can be stale, because nothing on it is remembered.

Configuration can't work that way, so the renderer parses the markers out of the
current body, and writes them back verbatim into the new one. The markers are
the only part of the body that survives a render.

Which gives the rule: **edit the markers, never the rendered sections.** A hand
edit to a rendered section disappears at the next render, and — worse — looks
like it worked until it does. Setting focus via `/focus` writes the marker,
which is the same thing done in the place that keeps it.

Markers rather than a config file in the repo because focus and cap are
*operational* state that changes between merges. A PR to change which milestone
is in focus is friction in exactly the wrong place.

## Fetching live milestones

Never hard-code a milestone list. Read it:

```http
GET /repos/{owner}/{repo}/milestones?state=open&per_page=100
```

### The gotcha: `milestone` takes a number

When setting a milestone on an issue, the field takes the milestone's **number**,
not its title:

```http
PATCH /repos/{owner}/{repo}/issues/{issue_number}
{"milestone": 2}          ✅
{"milestone": "v0.1"}     ❌ 422, or silently wrong
```

So anything accepting a title — `/milestone v0.1`, a triage decision — resolves
title → number against the live list first, and refuses if the title doesn't
match exactly one open milestone. Titles are compared literally: `v0.1` and
`V0.1` are different milestones, and the fix is to type the real one rather than
to normalise and hope.

## Schedule

| Runs | When | Does |
| --- | --- | --- |
| `gatekeeper-comment` workflow | on `issue_comment` created | parse and apply commands, immediately |
| `gatekeeper-sweep` workflow | every 15 minutes | catch missed comments; auto-revisit cleared blockers |
| `pipeline-analysis` routine | nightly, 02:00 UTC | triage everything untriaged |
| reactive triage | fired on demand by the gatekeeper | triage one issue, now |
| `pipeline-dev` routine | nightly, 03:00 UTC | build and work the ready queue |
| `pipeline-reconcile` routine | nightly, 01:00 UTC | detect and fix drift |
| `dashboard` workflow | hourly, and after each pipeline run | re-render the dashboard |

Reconcile runs before analysis, which runs before development: fix the state,
then triage against fixed state, then work against a triaged queue. A nightly
order that ran them the other way would spend each night acting on yesterday's
mistakes.

### Reactive triage

Waiting until 02:00 to triage an issue filed at 09:00 is a bad experience when
the person who filed it is sitting right there. So the gatekeeper can **fire the
triage routine immediately** for a single issue — on a new issue, or on
`/retriage`.

It needs a routine ID and a token with permission to fire it, both repository
secrets. Without them the gatekeeper logs that it couldn't fire and carries on:
the nightly run still picks the issue up, so a missing secret costs latency, not
correctness. Never a hard failure.

## What each stage does

### Analysis — find what needs triage

`select_triage.py` lists candidate issues: open, not the dashboard, and either
carrying no state label or carrying `ai-triage`. It returns them oldest-first,
with everything the triage stage needs already fetched, so the dispatcher can
fan out without each triage run re-querying.

The dispatcher (`pipeline-analysis`) is a thin loop over that list. It makes no
decisions itself — the decisions are in `triage-issue`, one issue at a time,
which keeps a bad triage contained to one issue instead of one batch.

### Triage — one issue

`triage-issue` reads an issue and produces:

- an **`area:*` and `type:*` label**;
- a **milestone proposal**, resolved against the live list;
- a **build checklist** — the acceptance criteria, as checkboxes;
- a **spec-pages line** naming what in `/docs` it touches;
- **dependencies**, recorded natively (below);
- a state: `pending-approval`, or `needs-clarification` with the specific
  question that blocks it.

It writes one comment containing its reasoning, so an `/approve` is a human
agreeing with something they can read rather than rubber-stamping a label.

**Re-fire repair.** Triage that runs twice on the same issue must not stack
duplicate comments, duplicate checklists, or contradictory labels. Each triage
comment carries a marker; a re-run finds its previous comment and **edits** it
rather than adding another. `triage_repair.py` also detects the mess left by an
older run that crashed mid-way — a label set without a comment, a comment
without labels — and repairs it.

### Recording dependencies

Dependencies are **native GitHub relationships**, never prose:

- *This is part of that* → a **sub-issue** of the parent epic.
- *This can't start until that's done* → a **blocked-by** relationship.

The builder computes its ready queue from that graph. A dependency written as a
sentence is a dependency it walks straight into. This is also why `/block` and
`/unblock` exist as commands: recording one has to be as cheap as mentioning it.

**`Depends on:` is not a blocker.** It is soft ordering — "this will go better
afterwards" — and it deliberately has no native form. Converting one into a hard
blocked-by turns a preference into a gate the builder refuses to pass, which is
how a queue deadlocks on work that could have been done at any time.

The `issue-blockers` skill is the write side, and its `audit` subcommand finds
`Blocked by #N` lines that were never recorded natively. One trap it exists
around: the dependencies **write** API takes the internal `issue_id`, not the
issue number — and a wrong id does not fail, it links to whatever issue happens
to have it.

### Development — the ready queue

`select_queue.py` returns the issues to work, in the order to work them:

**Eligibility** — all of:

- `ready-for-work`;
- in the focus milestone;
- not blocked by an open issue;
- not a `type:epic` (epics are containers; their children are the work);
- has no open PR already referencing it.

**Ordering** — a topological sort over the blocked-by graph, so an issue that
unblocks others is worked before one that unblocks nothing. Ties break oldest
first, so nothing starves. A cycle in the graph is reported, not resolved: a
cycle is a triage mistake, and silently picking an entry point hides it.

### Serial delegated delivery

Issues are worked **one at a time**, each delegated to its own agent session.

Serial because parallel agents on one repository fight: two branches touching
`ProjectSettings.asset`, two release-please runs, two PRs renumbering the same
things. The `cap` marker exists to raise this above 1 if that ever stops being
true, and its default is 1.

Delegated — a fresh session per issue — because context from the previous issue
is a liability. The agent that just spent an hour on the audio system will find
a way to make the next issue about audio.

Each delegation is: mark `in-progress`, hand the issue to the development agent
([Agent workflow](agent-workflow.md)), and let it open its own PR. The pipeline
does not write code, and it does not merge PRs.

### Reconciliation — drift

Reality drifts: a PR merges without its closing keyword, someone hand-edits a
label, a run crashes between two API calls. `reconcile.py` compares what the
labels claim against what GitHub shows, and sorts each finding into
**auto-fixable** or **flag-only**.

**Auto-fixed** — mechanical, single correct answer, no judgement:

- a closed issue still carrying a state label → remove it;
- `in-progress` with no open PR and no recent activity → back to
  `ready-for-work`;
- an issue with two state labels where one was clearly just applied → keep the
  newest;
- a merged PR whose issue is still open, with a closing keyword that GitHub
  missed → close the issue.

**Flagged, never touched** — anything needing judgement:

- `ready-for-work` with no milestone (the invariant is broken, but the fix is a
  decision about which milestone);
- an issue in the focus milestone blocked by one in a later milestone;
- a dependency cycle;
- two dashboard issues, or none.

The split is the design. A reconciler that guesses is a reconciler you have to
audit, and one you have to audit is one you turn off.

### Dashboard

`render_dashboard.py` rewrites the dashboard issue from live state: what is in
focus, what is ready, what is in flight, what is blocked, what is waiting on a
human, and what the reconciler flagged. Plus **unblocker stars** — the issues
that would free the most other work, which are the ones worth approving next.

Wholly regenerated every time, apart from the config markers. Nothing on it is
remembered, so nothing on it can be stale.

## Skills inventory

| Skill | Owns |
| --- | --- |
| `pipeline-gatekeeper` | comment parsing, the gates, label application, acks, reactive fire |
| `pipeline-analysis` | finding what needs triage and dispatching it |
| `triage-issue` | triaging one issue, and repairing a re-fire |
| `pipeline-dev` | the ready queue and serial delegated delivery |
| `pipeline-reconcile` | drift detection, auto-fix, and flagging |
| `pipeline-dashboard` | rendering the live dashboard issue |
| `milestone-orchestration` | driving a milestone end to end across the above |

The scripts inside them are ordinary Python with ordinary unit tests, run by the
`pipeline-tests` workflow ([CI/CD](ci-cd.md#pipeline-workflows)). The pipeline is
code, and a broken gatekeeper is a broken queue.

## Skills here are local, and deliberately not synced

**`.claude/skills/` is the source of truth in this repository.** Every skill was
hand-ported from `derekwinters/lucas-doggiehood` and then diverged to fit this
game — different release config, different milestones, a different label
taxonomy.

There is **no `skills-update` workflow**, no scheduled sync, and nothing here
reads an upstream skills repository at runtime. No `AI_SKILLS_READ_TOKEN` secret
is needed, and one should not be added.

### Why not sync

A sync would eventually revert local work. That is not a hypothetical: it is
why the upstream project disabled its own sync. Once a skill has diverged —
which it does the first time it mentions `.github/release-please/config.json`
or `type:wireframe` — an upstream copy is not an update, it is a regression
wearing an update's clothes. And it arrives on a schedule, so it lands when
nobody is looking at it.

### The manifest is a record, not a subscription

`.claude/.skills-manifest.json` says where each skill came from and whether it
has diverged, so a future reconciliation knows what was copied from where. It
carries `"sync": "disabled"` and the reason, so nobody wires a workflow to it by
assuming that a manifest implies a sync.

Nothing reads it. It is documentation that happens to be JSON.

### If a sync is ever wanted

In this order, and not any other:

1. **Reconcile the divergence first.** Diff every local skill against upstream
   and decide, per difference, which side is right. A sync installed before
   this step is a sync that silently makes those decisions by overwriting.
2. Restore a `skills-update` workflow and its read token.
3. Subscribe only the skills that genuinely have no local divergence, and record
   in the manifest which those are.

Steps 2 and 3 are an afternoon. Step 1 is the one that gets skipped, and it is
the one that matters.
