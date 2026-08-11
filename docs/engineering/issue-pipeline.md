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
| *(none)* | brand new, not yet admitted | issue creation | `/admit` |
| `ai-triage` | queued for automated triage | `/admit`, `/propose`, `/revise`, `/unpark` | triage finishes |
| `pending-approval` | triaged; waiting on a human | triage | `/approve`, `/park` |
| `needs-clarification` | triage could not proceed without an answer | triage | `/revise <answer>`, `/park`, or the blocker sweep |
| `ready-for-work` | approved and eligible for the builder | `/approve` | the builder picks it up |
| `in-progress` | an agent is building it right now | the builder | its PR merges, or it fails |
| `parked` | deliberately set aside | `/park` | `/unpark` |

Plus `dashboard`, which is not a state — it marks the one live dashboard issue,
which the pipeline never triages or works.

**There is no `/retriage`.** This table named one twice, and it has never
existed: `parse_commands.COMMANDS` has no such entry, so typing it earns the
unknown-command reaction rather than a re-triage. The way back to `ai-triage`
from a question is `/revise <your answer>` — the one command that returns an
issue to triage *with a note attached*, which is exactly what an answer is. The
verb reads oddly on that route and the hand-back footer says so in plainer
words; see [the footer](#the-hand-back-footer).

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
| `/cap <n>` | set how many issues one build round takes on. **Dashboard issue only.** |

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

- **A command must start its line** — its *line*, not the comment. "see
  /approve for details" is a mention; a sentence of explanation above
  `/approve` is not, and applies normally. The workflow briefly filtered on
  `startsWith(body, '/')`, which dropped exactly that case with no run, no
  reaction and no acknowledgement — a silence indistinguishable from the
  pipeline being broken.
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
- A **refused** comment is watermarked too. It was considered, and without the
  mark the sweep reconsiders it every run and re-posts the same refusal. A
  stranger's comment is *not* watermarked — reacting to it would itself be
  letting them make the bot act.

To make the gatekeeper reconsider a comment, remove its 👀.

**Only the gatekeeper's own 👀 counts.** A human reacting out of interest must
not silence a command, so the watermark check matches on the reacting account.
And if the reactions cannot be read at all, the comment is treated as *already
claimed* — re-applying a command is worse than skipping one, and the sweep picks
it up next time.

### The snapshot

`fetch_comment_event.py` turns the raw webhook payload into what the parser
reads: the issue's number, labels, body, milestone, blockers (text ∪ native),
whether it is the dashboard, and the comment itself. All the payload-shape
guesswork lives there so the parser never has to know what GitHub's JSON looks
like.

It returns nothing at all for a comment on a pull request or from a bot —
defence in depth, since the workflow filters those too, but a snapshot the
parser *could* act on is one it eventually will.

It also tolerates a partial payload rather than throwing. A failed
dependency lookup in particular does not lose the event: the command is
probably `/park`, which does not care, and the gates that *do* care see a
smaller blocker list and refuse — which is the safe direction.

## Replaying the comments the webhook lost

`gatekeeper-comment` fires on `issue_comment: created`. When that delivery is
dropped — a webhook that never arrives, a workflow that fails to start, a run
cancelled mid-flight — the command is simply gone. Derek types `/approve`,
nothing happens, and **nothing anywhere reports that nothing happened.**

So the sweep re-reads recent comments and feeds anything unclaimed back through
the ordinary path. This is the second pass the 👀 watermark was designed for.

Four properties, and every one of them is load-bearing:

- **It reuses `run_comment_event.run`, the same function the live workflow
  calls.** Not a second implementation of parse → gate → apply. The owner
  re-check, the gates, the acknowledgement wording and above all the watermark
  rule are *inherited* rather than re-derived — and a second copy that got the
  watermark's failure direction backwards would re-apply every command in the
  window on every sweep, six times a day, forever.
- **It is cron-path only.** See below.
- **The window is bounded** — seven days, `REPLAY_WINDOW_DAYS` in
  `run_sweep.py`, gathered by one repo-wide `since`-filtered call rather than
  one request per open issue. Seven days is far more than the six-hourly cron
  needs for a single dropped webhook; the width is there for a multi-day
  Actions outage, and costs nothing because re-reading a claimed comment is a
  no-op. It is bounded rather than unlimited because an unbounded scan of the
  whole comment history is both slow and a far larger surface for a watermark
  bug to act on.
- **A replayed command updates the sweep's own snapshot** before reconcile
  reads it, and the board is re-rendered **once**, at the end of the sweep.

### Why the replay is cron-only

`gatekeeper-sweep` has two triggers, and on the event path one of them is
`issues: [labeled]`. An applied command *changes a label*. So an event-path
replay would wake this workflow up to re-apply the very comment
`gatekeeper-comment` is applying at that moment — and nothing serialises the
two, because they are in different concurrency groups.

The six-hourly cron has no such relationship to the comment workflow, which is
why the replay lives there, alongside reconcile's two cron-only fixes.

### Why the replay writes its labels back

Reconcile runs after the replay, and derives its fixes from the labels on the
sweep's in-memory snapshot. If the replay moved an issue from `in-progress` to
`parked` and reconcile then read the pre-replay list, it would see an
`in-progress` issue with no open PR, call it a stall, and requeue it to
`ready-for-work` — **silently undoing the command it was replayed to honour**,
and reporting a successful auto-fix while doing it.

So `run_comment_event.Result` carries the label set the command left behind,
and the replay writes it onto the snapshot issue. A refused command reports no
labels at all, rather than "the labels unchanged", so the snapshot is left
exactly as it was instead of being overwritten with a guess.

### One render, last override wins

`run_comment_event.run` re-renders the dashboard itself, which is right for one
webhook and wrong for a sweep: six replayed commands would publish six boards,
five of them describing a half-finished sweep. The replay hands it a collector
instead, and the sweep renders once at the end.

If two replayed comments both set a `/focus`, **the later one wins** — the
candidates are fetched oldest-first for exactly that reason. Refusing both as
ambiguous was the alternative; last-wins is what would have happened had the
webhooks arrived normally, and matching the undropped case is the less
surprising rule.

### When a replay raises

One malformed comment does not take the sweep down with it. The failure is
counted, named in the workflow log, and the sweep carries on — reconcile is the
backstop for the entire board, and losing it because a single comment blew up
trades a missed command for a missed sweep, which is the more expensive of the
two.

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

**Invariant:** for every open blocker edge A → B, `order(A) ≥ order(B)`, and B
must be scheduled.

Without it, an issue can sit in `v0.0.1` blocked by one in `v0.1`. The builder
correctly skips the dependent — but the blocker is not in the focus milestone to
be built either, so **the work silently stalls while the milestone reads
"ready"**. Nothing notices, because from every angle each issue looks fine on
its own.

Two refusals:

| Skip | When |
| --- | --- |
| `blocker-unscheduled` | the blocker has no milestone, or one with no version order |
| `blocker-inversion` | the blocker is in a *later* milestone |

Both name the offending issues, so the ack says which ones rather than that
something is wrong.

#### Milestone order comes from the title

`vMAJOR.MINOR[.PATCH]` is ordered; anything else is **unordered**. That
deliberately includes `Direct Involvement Needed` — it never ships, so a blocker
parked there is one nothing will ever build, which is exactly the
`blocker-unscheduled` case.

Closed blockers are ignored: a resolved dependency constrains nothing.

#### Soft `Depends on:` uses the same rule

It does not stop the builder selecting the issue, but the ordering problem is
identical — the thing it depends on is not scheduled — and a refusal is cheap
while a stalled milestone is not.

#### Refuse, never auto-bump

A refusal leaves the issue **completely untouched**. Moving the blocker earlier
and moving the subject later are both valid fixes, and which one is right is a
planning decision about scope. The reply says so, and says that nothing was
changed.

#### It runs on `/milestone` too

Setting a milestone is the other way to create an inversion. Gating only
`/approve` would let `/milestone v0.0.1` quietly produce one, to be discovered
at the next approval — or not at all, if the issue was already approved.

## Auto-revisit when a blocker clears

An issue parked in `needs-clarification` **because it was blocked** has nothing
to wake it. Analysis only acts on `ai-triage`, and the gatekeeper otherwise only
acts on comments — so without this the issue waits for a human to remember it,
which is the same as waiting forever.

This is a **state-derived transition, not a command**. The sweep computes it.

### What counts as a blocker

Structured text lines (`Blocked by #42`) **unioned** with native GitHub
relationships. Union rather than either-or: an issue can have one recorded
natively and another still in prose, and waking it when only half have cleared
is worse than not waking it at all.

An unstructured mention — "this is similar to #42" — is not a dependency, and
is deliberately not matched.

#### One recognizer, shared

Recognizing that line has exactly one implementation: `text_blockers` in
`.claude/skills/issue-blockers/blocker_refs.py`, and the union has exactly two —
`blockers_of` for a snapshot that already carries its native edges, and
`union_blockers` for the one caller that fetches them live. Every reader in the
pipeline imports them: the sweep, the queue selector, the reconciler, the
dashboard, the comment-event snapshot, and `audit`.

`issue-blockers` owns them because it is the skill that documents the format —
the same "the side that writes the format owns the recognizer" rule that put
`has_analysis_signature` in `triage-issue`.

The pattern used to be written out in each reader, and the drift is silent in
the worst way: the queue selector reads a line as a blocker and refuses to
build the issue, the sweep does not and so never wakes it, and the dashboard
shows it as ready the whole time. Nothing errors. The issue just stops moving,
and no log anywhere says why.

### When a blocker resolves

| Blocker state | Resolved? |
| --- | --- |
| closed, or merged | yes |
| open, `ready-for-work` or `in-progress` | yes — it is scheduled and will be built |
| open, anything else | no |
| not in the snapshot | **no** — not knowing is not knowing it is done |

An issue with several blockers is revisited **once every one** resolves.

### The `type:wireframe` carve-out

**A `type:wireframe` blocker resolves only when it is closed.** Being scheduled
is not enough.

Closing a wireframe issue is what "agreed" means, and a wireframe marked
`ready-for-work` is still a picture nobody has said yes to. Without the
carve-out the sweep wakes the dependent on every run and triage sets it aside
again on every run — forever, and noisily.

### What it never touches

- **`parked` issues.** Parking is a decision the owner made; only the owner
  un-makes it.
- **Anything not in `needs-clarification`** — an issue already
  `pending-approval` or `ready-for-work` is not waiting on a blocker.

A revisit adds `ai-triage`, removes `needs-clarification`, and posts a short
comment naming the cleared blockers — so the transition is visible in the
thread rather than being a label that changed itself overnight.

## `/focus` and `/cap` live on the dashboard issue

Both are stored as HTML-comment markers in the body of the dashboard issue:

```html
<!-- pipeline-focus: v0.0.1 -->
<!-- pipeline-cap: 3 -->
```

Each setting resolves **override → marker → default**: an explicit `/focus` or
`/cap` on this run wins, otherwise the marker currently in the body, otherwise
the default (3 for `cap`; `focus` has no default and must come from one of the
first two).

A `/focus` naming **no live milestone is rejected, not stored.** A typo'd
`v0.0.10` would otherwise render a board whose every section is empty — which
looks exactly like a finished milestone, and is the most misleading output the
renderer could produce. The refusal names the milestones that do exist.

A malformed `cap` marker falls back to 3 rather than failing the render: a
board that does not render is worse than one with a default cap, and the next
`/cap` fixes it.

The dashboard issue is the one carrying the `dashboard` label. There is exactly
one — [**issue #163**](https://github.com/derekwinters/connor-multiplying-frogs/issues/163)
— and the reconciler flags it if there are ever two or none.

Nothing hard-codes that number. Every script finds the board by its label, so
recreating it is a matter of moving the label rather than editing code; the
number here is for people, not for programs.

### Why markers, and why re-rendering beats hand-editing

The dashboard body is **regenerated wholesale** on every render — every section
is derived from live GitHub state. That is what makes it trustworthy: nothing on
it can be stale, because nothing on it is remembered.

Configuration can't work that way, so the renderer parses the markers out of the
current body, and writes them back verbatim into the new one. The markers are
the only part of the body that survives a render.

Which gives the rule: **edit the markers, never the rendered sections.** A hand
edit to a rendered section disappears at the next render, and — worse — looks
like it worked until it does.

`/focus` and `/cap` therefore persist **by re-rendering with an override**, not
by editing the body. The gatekeeper passes the new value to the renderer, which
writes it into the marker as part of the regenerated body. Hand-editing the
marker directly would work exactly once — until the next render, which reads
the marker it is about to overwrite and would find the old value if the write
had raced. Going through the renderer means there is only ever one writer of
that body.

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
| `gatekeeper-sweep` workflow | issue/PR events, and a six-hourly cron | auto-revisit cleared blockers, apply reconcile's fixes; on the cron, also replay missed comments |
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
the person who filed it is sitting right there. So the moment `ai-triage` is
**newly** added, the gatekeeper job — already running — makes an **outbound
POST** to a poke-only Routine.

Outbound specifically, because a workflow triggered by the token's own label
change would never run: GitHub suppresses workflow runs from events initiated by
`GITHUB_TOKEN`. An outbound request is not an event, so the guard does not
apply.

Configured by `AI_TRIAGE_URL` and `AI_TRIAGE_SECRET` (#83). **Absent, it is a
clean no-op** — the nightly run still picks the issue up, so a missing secret
costs latency rather than correctness, and never fails the command that caused
it. A network error is swallowed for the same reason: the label move has
already succeeded by then.

#### Both entry points fire, and each issue fires once

`run_comment_event.py` fires after its label write; `run_sweep.py` fires from
both of its paths, because a cleared blocker and a requeue each land an issue in
`ai-triage` without anyone typing a command.

The sweep fires **at most once per issue per pass**. A revisit does not update
the in-memory issue that reconcile then reads, so the same issue can look newly
triageable twice in one sweep — and two fires are two triage sessions racing on
one issue, each posting its own plan for Derek to choose between.

`fire_from_env` is the single place the environment variables are read, so both
entry points poke the same Routine the same way.

!!! warning "This was described here for weeks before it was true"

    `fires_triage` and `fire_routine.fire` were both written, both unit-tested
    — and called from nowhere. The workflow passed the two secrets to a step
    that never read them, so **reactive triage never fired once**, on any
    issue, and everything waited for the scheduled round.

    Nothing surfaced it. Tests passed, because each half was correct in
    isolation; the log was green, because nothing had failed; and the fallback
    it was designed around — the nightly round — did the work quietly, which is
    exactly what it is there for. A feature whose absence looks identical to
    its fallback needs a test that the call site exists, and both entry points
    now have one.

#### The outcome is classified truthfully

Success requires a **real fire** — a response carrying a session URL. A bare
`200` means the endpoint answered, not that the Routine ran, and reporting that
as fired would hide a misconfigured Routine behind a green log line for weeks.
Everything else logs the status and a bounded snippet of the body, and a `401`
says the secret is wrong rather than just failing.

The API names that field **`claude_code_session_url`**. The older `session_url`
and `url` are still accepted, but looking for only those made the success path
unreachable and annotated every healthy fire as an error
([#240](https://github.com/derekwinters/connor-multiplying-frogs/issues/240)) —
which is the same failure as a green line nobody believes, pointing the other
way. An annotation that fires on every good run is one people stop reading.

#### The endpoint is the Anthropic API, so `anthropic-version` is required

`AI_TRIAGE_URL` points at an Anthropic API endpoint, and that API requires an
`anthropic-version` header on **every** request. Without it the fire is refused
at header validation with a `400`, before the token or the payload is examined —
which is why a missing version reads as a malformed request rather than an auth
failure, and why a `400` here never meant the secret was wrong.

That cost real time in
[#238](https://github.com/derekwinters/connor-multiplying-frogs/issues/238):
because the URL is a secret, nobody could see it was an `api.anthropic.com`
endpoint, so nobody thought to ask what headers that API demands. If the fire
ever starts failing at header validation again, the endpoint's own message names
the header — which is the whole reason the two fixes above exist.

#### An error status is an answer, not a failure to reach

`urlopen` raises on any 4xx or 5xx rather than returning it, so an error status
has to be caught and turned back into a `(status, body)` pair. Skipping that
step costs three things at once, and
[#236](https://github.com/derekwinters/connor-multiplying-frogs/issues/236) cost
all three: the response body is discarded, which for a `400` is the only thing
that names the field the endpoint rejected; the classification above becomes
unreachable on every error path, so the "your secret is wrong" message can never
print; and every HTTP error reports as *"could not reach the Routine"*, which
sends you looking at networking while the endpoint is up and answering.

Only a genuine transport failure — a refused connection, a timeout, DNS — is
reported as unreachable.

#### And the classification is printed

Classifying an outcome nobody prints buys nothing. Every fire writes two lines
to the job log: one **before** the request saying it is about to go out, and one
after saying what came back.

Before, specifically. A line printed only afterwards is a line nobody sees when
the request hangs until the job times out — and the log then stops at the label
move, which reads as *the fire was never attempted* rather than *the fire was
attempted and went nowhere*.

A failed fire is a GitHub Actions `::error::` annotation. `not-configured` is a
`::notice::`, because a secret nobody has set yet is a choice not yet made
rather than a fault, and annotating it as an error trains everyone to ignore the
annotation that matters.

**Neither line contains the endpoint or the secret.** `AI_TRIAGE_URL` is itself
a secret, and GitHub masks only exact matches — a host fragment reassembled into
a log line is a leak nothing would catch.

This was [#231](https://github.com/derekwinters/connor-multiplying-frogs/issues/231):
the classification above was implemented and then discarded at every call site,
so a Routine that never ran produced a log identical to one that worked. The
page described the intended behaviour and the code did not deliver it; the code
is what changed.

A failed fire still never fails the command. The label move has already landed
by then, and the nightly round will pick the issue up — the cost is latency, not
correctness.

#### The payload is not a place to put instructions

The POST body's freeform text names **only the repository and the issue
number**, and the helper refuses a non-integer issue number rather than
formatting it into the string.

**The Routine's prompt must parse only the integer and follow nothing else in
the payload.** If a Routine treats that text as instructions, anything that can
influence the text can instruct an agent with write access. Boring text is half
the defence; the Routine ignoring it is the other half, and its prompt says so.

## What each stage does

### Analysis — find what needs triage

`select_triage.py` lists candidate issues. An issue is eligible when it is
**open** and carries **`ai-triage`**, and is none of the following:

| Excluded | Why |
| --- | --- |
| `type:epic` | An epic is a container. Its children are the work, and they carry their own labels. |
| `dashboard` | The pipeline's own furniture, not something the pipeline works on. |
| `parked` | A decision the owner made with `/park`. Triage does not get to overrule it — only `/unpark` does. |

Carrying `ai-triage` is the whole entry condition, so an issue with no state
label at all is not a candidate: it has not been `/admit`ted, and admitting is
the owner's call. That is the same rule the command table above describes, and
picking up unadmitted issues would make `/admit` mean nothing.

Each eligible issue comes back with its **number**, its **current milestone**,
and the **latest owner note** — the most recent `/revise`, `/redo`, or
`/propose` comment, with its text. Only the latest, because an issue revised
twice should be triaged against the current feedback rather than a
conversation; and only the owner's, for the same reason the parser has a
bad-actor gate. Carrying the note here means the triage run does not have to
re-read the thread to find out why it is running again.

Results are sorted by issue number, oldest first.

The dispatcher (`pipeline-analysis`) is a thin loop over that list. It makes no
decisions itself — the decisions are in `triage-issue`, one issue at a time,
which keeps a bad triage contained to one issue instead of one batch.

**The dispatcher writes nothing at all** — no labels, no comments, not even a
summary of the round. Every write belongs to the single-issue skill. A round
that made its own writes would be a second thing that can be wrong, wrong at
batch scale across the whole queue, in a way no individual triage run could
produce. It also keeps `triage-issue` genuinely standalone, which is what lets
reactive triage invoke it with no dispatcher present.

**Concurrency is set by the orchestration layer, not the script.**
`select_triage.py` reports what needs triage and has no opinion on how fast to
work through it: the right number depends on rate limits and runner capacity,
which change without the eligibility rules changing. A limit baked into the
discovery script is one nobody can adjust for a busy night without editing a
file with tests pinned to it.

A triage that fails is one issue's problem — it keeps `ai-triage` and the next
round picks it up. The round does not abort for it.

#### The scheduled round is the backstop, not the main path

Reactive triage is how an issue normally gets triaged: the gatekeeper fires it
the moment `ai-triage` appears, and the issue is analyzed within minutes.

The 02:00 round exists for what reactive triage missed — a failed POST, secrets
that were never configured, a label changed by hand instead of by a command, an
issue carried back by the blocker sweep. On a healthy night it finds nothing.
**An empty round is the system working**, not a wasted run.

### Triage — one issue

`triage-issue` reads an issue and produces:

- an **`area:*` and `type:*` label**;
- the **milestone**, set as a field by matching the live milestone descriptions;
- a **build checklist** — the acceptance criteria, as checkboxes;
- a **spec-pages line** naming what in `/docs` it touches;
- **dependencies**, recorded natively (below);
- a state: `pending-approval`, or `needs-clarification` with the specific
  question that blocks it.

It writes one comment containing its reasoning, so an `/approve` is a human
agreeing with something they can read rather than rubber-stamping a label. That
comment **opens with the plain-English lead** — the same two or three skimmable
sentences a PR body opens with, and for the same reason: a wrong plan has to be
catchable on a skim.

**It never sets `ready-for-work`, and it never invents a mechanic.** Every route
ends with the issue waiting on a human. The label that means "build this" is
only ever applied by `/approve`.

#### The four routes

| Route | When | Ends at |
| --- | --- | --- |
| Bug | behaviour contradicts what `/docs` says | `pending-approval` + `type:bug` |
| Spec-covered feature | `/docs` already says how this behaves | `pending-approval` |
| Needs a design call | `/docs` doesn't say, or UI with no wireframe | `needs-clarification` |
| `/propose` authorized | Derek asked for a design | `pending-approval`, marked PROPOSAL |

Two of these are worth spelling out.

**A bug whose root cause is a missing rule** gets an invariant, not just a
patch. When the code does what someone intended and the real fault is that
nobody wrote the rule down, the plan proposes the missing invariant in plain
English and the checklist gains an item that tests it. Patching the code alone
guarantees the same class of bug reappears elsewhere. If the rule is a genuinely
new choice about how the game plays, it is a design call instead — and when it
is unclear which, it is a design call.

**The design-call route writes no plan at all** — no checklist, no milestone,
just one concrete question and `needs-clarification`. A plan attached to an
undecided question is an answer smuggled in as paperwork, and half of it
survives into the build because it was already written down.

#### Write ordering: comment first, then the label

The analysis comment is posted **before** the state label is set, always.

This is what makes the bad state structurally impossible rather than merely
unlikely. A run that dies between the two writes leaves either a plan still
sitting on `ai-triage` — untriaged, which the next round simply redoes — or
`pending-approval` with nothing to approve, which is silent: the pipeline
believes the issue is handled and it waits on a human who has nothing to read.

`ai-triage` is removed in the same write that adds the new state, so a hand-back
rests in exactly one state. An issue carrying both gets triaged again by the
next analysis round while it sits waiting for Derek.

##### The write is code, not an instruction

`hand_back.py` performs both writes. This used to be prose in the skill file —
"remove `ai-triage` in the same write that adds the new state" — carried out by
a model at the end of a long analysis, and it was skipped in practice: sixteen
issues sat on `ai-triage` with finished plans on them, waiting for an approval
queue they had never reached.

Nothing failed when it was skipped, which is the whole problem. The comment was
posted, so the run looked successful from outside; only the label was missing,
and no exit code, artifact, or test reports a missing label.

So it is one call that does both writes or neither:

```python
hand_back.apply(api, 47, analysis, labels, "pending-approval")
```

It **refuses before it writes** in two cases, so a rejected hand-back leaves the
issue exactly as it was rather than half-written:

- a state triage may not set — `ready-for-work` is Derek's, via `/approve`;
- an analysis the recognizer would not match, which is the state
  `triage_repair` cannot repair and the reconciler requeues forever.

This follows the same principle as the gatekeeper: *the component that can
change state is a parser with a fixed vocabulary, not a model.* Triage was the
one stage where a model applied its own label, and it was the one stage that
silently stopped doing it.

##### The hand-back footer

The comment ends with the commands that make sense **on the route it took** —
not the whole vocabulary:

| Route | Offers |
| --- | --- |
| `pending-approval` | `/approve`, `/revise <notes>`, `/park` |
| `needs-clarification` | `/revise <answer>`, `/park` |

`/approve` is deliberately absent from the second: there is no plan on that
route, so approving would approve nothing, and an offered command that means
something other than it appears to is worse than no footer.

The glosses come from `render_dashboard.COMMANDS` rather than a second copy —
one description of what each command does. The single exception is `/revise` on
the clarification route, where the canonical gloss ("the plan is not right") is
false, because triage wrote no plan. That override is the visible edge of a real
gap in the vocabulary: there is no verb for *here is the answer, look again*,
and `/revise` is doing that job because it is the only route back to `ai-triage`
that carries a note.

#### Re-fire repair

Triage runs twice more often than you would think: a sweep catches a comment
late, a Routine is poked twice, a run crashes after posting its analysis but
before moving the label. `triage_repair.py` decides what happens, as a pure
function rather than a judgement made afresh each time.

| Situation | What happens |
| --- | --- |
| An analysis comment is already there | **repair** — apply the missing label move, post nothing |
| No analysis comment | a normal triage run |
| Analysis present, label already correct | nothing |
| The owner left `/revise`, `/redo`, or `/propose` | re-analyze anyway |

**Repair, not repeat.** Triage is not deterministic: the same issue analyzed
twice yields two different reasonable plans, and stacking the second on the
first leaves Derek deciding which one an `/approve` refers to.

The exception is the owner's note. `/revise` objects *to the plan* — repairing
the label would apply the state the rejected plan asked for and silently discard
the objection. The note travels with the issue from `select_triage.py`, so the
rewrite answers the actual complaint.

##### One recognizer, shared

"Does this comment look like triage wrote it" has exactly one implementation:
`has_analysis_signature` in `triage_repair.py`. It matches a `## Build
checklist` heading anchored at the start of a line, or the
`❓ Needs from Derek/Connor:` marker allowing Markdown emphasis around the text.
Prose mentioning either phrase does not match, and only comments authored by the
bot count — a human pasting a checklist is not a triage run.

`reconcile.py` imports it rather than carrying a second copy. Two copies drift,
and this particular drift is self-sustaining: reconcile finds no analysis and
returns the issue to `ai-triage`, triage finds the analysis it wrote and repairs
the label instead, reconcile returns it again. Neither side is wrong on its own
terms, so nothing surfaces as an error — the issue just cycles. The side that
writes the format owns the recognizer.

**The author test is shared for the same reason, and it was the one that broke.**
`is_triage_author` lives beside the recognizer and is imported the same way. Both
modules previously kept their own `TRIAGE_AUTHOR = "github-actions[bot]"`, while
every real triage comment was posted by the Claude App as `claude[bot]` — so the
author filter ran *before* the recognizer and rejected every analysis on the
board. Two copies, both stale in the same direction, so nothing looked
inconsistent.

Triage has two surfaces and both are accepted: `claude[bot]` when it runs as the
App, `github-actions[bot]` when it runs from a workflow holding `GITHUB_TOKEN`.
A human's comment still never counts, which is the point of having the gate at
all — Derek pasting a checklist is not a triage run.

### The I/O layer

Everything with a decision in it is pure and tested without a network. Three
thin modules wire those pieces to GitHub:

| Module | Does |
| --- | --- |
| `_github_api.py` | REST helpers, and the shared dashboard re-render |
| `run_comment_event.py` | one `issue_comment` event, end to end |
| `run_sweep.py` | the board-wide replay + revisit + reconcile pass |

`run_comment_event.py` is: snapshot → parse → gate → apply → write labels, ack,
and reaction → **re-render the dashboard once, after every label write.**
Rendering in between would publish a board describing a half-applied command.
`run_sweep.py` re-renders once at the end for the same reason, and takes
`--events-only` to drop the comment replay and reconcile's two cron-only fixes
on the **event path**. That flag names the trigger, not a clock: both paths run
the same script, and the cron is six-hourly.

**Authentication is `GITHUB_TOKEN`, never a PAT.** The workflow token is scoped
to this repository and expires with the job; a personal access token would
carry everything its owner can do into a job nobody is watching.

**The owner check is repeated in the script.** The workflows already filter on
the comment author, so this is redundant — deliberately. A script that is safe
only because its caller filtered is one edited `if:` away from letting a
stranger drive the pipeline, and the workflow trigger is exactly the kind of
line that gets edited for an unrelated reason.

#### Running either entry point by hand

Both read JSON on **stdin**, which is what makes replaying a missed event or
rehearsing a sweep possible without waiting for a trigger:

| Entry point | Expects on stdin |
| --- | --- |
| `run_comment_event.py` | an `issue_comment` webhook payload |
| `run_sweep.py` | a state snapshot — issues, pulls, merged commits, recent comments, focus |

`run_sweep.py --events-only` simulates the **event path** by dropping the
comment replay and reconcile's two cron-only fixes. Running it *without* that
flag outside the nightly window will requeue whatever the builder currently has
in flight, which is the one way to do real damage with these scripts by hand.

The rehearsal snapshot's `recent_comments` are raw `/issues/comments` items,
exactly as GitHub returns them — the replay hands them to the same
`fetch_comment_event.build` the live path uses, so it must be given the same
shape rather than a second one. `PIPELINE_OWNER` names the account whose
comments count; without it the replay does nothing at all, because an empty
owner would match an empty author.

### Applying an action

`apply_actions.py` computes the resulting label set, the acknowledgement, and
whether to watermark. Three rules worth knowing:

- **A state change replaces, never adds.** Exactly one of the state labels is
  on an open issue at a time. Everything else — `area:*`, `type:*`,
  `skip-docs` — is untouched, because a state change that dropped them would
  throw away the whole triage decision.
- **The ack says what happens next**, not which label changed. "the nightly
  builder can pick it up next" is the consequence; the label is a means.
- **A refusal's ack says nothing was changed** — not the labels, not the
  milestone — because "your command was refused" and "your command was partly
  applied" need very different responses.

Reactive triage fires **only when `ai-triage` is newly present**. An idempotent
re-add — a replay, or a second `/admit` on an already-admitted issue — must not
fire it again, or one stuck comment becomes a triage run every sweep.

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
- in the focus milestone (no milestone is not the focus milestone);
- every hard blocker closed or merged;
- not `parked`;
- not a `type:epic` (epics are containers; their children are the work);
- has no open PR **closing** it.

Two of those are narrower than they look.

**Hard blockers are native edges unioned with `Blocked by #N` lines**, merged by
one helper — `blockers_of` in `.claude/skills/issue-blockers/blocker_refs.py`,
the same one the sweep and the dashboard use. An issue can have one dependency
recorded natively and another still
written in prose, and reading either source alone releases work that is still
half-blocked. A blocker the caller knows nothing about counts as unresolved:
not knowing whether the thing you depend on is finished is precisely the case
where starting is expensive.

**An open PR only suppresses an issue if it *closes* it** — a closing keyword,
not a bare `#47`. PRs reference issues constantly for context, and treating a
mention as "already being worked" would silently starve an issue that nobody is
actually working. This is the same keyword rule reconcile uses to decide
done-ness.

**Ordering** — a stable topological sort. At each step the lowest-numbered
issue whose in-queue dependencies are already placed, so dependencies come
first and ties break oldest-first with nothing starving. Soft `Depends on:`
lines order here even though they never gate: a blocker closed this evening
still says which of two ready issues was meant to come first.

The sort places **one issue per step**, re-checking after each. Placing the
whole ready set at once would scatter a dependent away from the thing it
depends on — with a cap of 2, `#11, #12` rather than `#11, #10` — and a cap is
far more useful when it cuts a coherent chain than an arbitrary slice.

A cycle is a triage mistake, and reconcile flags it. The builder must not hang
or drop issues on one, so it falls back to number order for the tangled set.

### Serial delegated delivery

Issues are worked **one at a time**, each delegated to its own agent session.

Serial because parallel agents on one repository fight: two branches touching
`ProjectSettings.asset`, two release-please runs, two PRs renumbering the same
things. Nothing raises that above one at a time.

Delegated — a fresh session per issue — because context from the previous issue
is a liability. The agent that just spent an hour on the audio system will find
a way to make the next issue about audio.

Each delegation is: mark `in-progress` **first**, so a crashed run leaves a
visibly stuck issue rather than an invisible one; hand the issue to the
development agent ([Agent workflow](agent-workflow.md)); let it open its own PR.
The pipeline does not write code.

#### `cap` bounds the round, not the concurrency

The two are separate knobs and it is worth keeping them apart.

| | Means | Value |
| --- | --- | --- |
| Concurrency | issues worked at once | always **1** |
| `cap` | issues the round takes on at all | **3** by default |

`select_queue.py` returns at most `cap` issues; the builder then works through
them one after another. A night that picks up three issues and delivers them
serially is entirely compatible with "parallel agents fight" — nothing is
running in parallel.

The cap is small on purpose. Three issues that land beat six that half-land,
and it is the only thing standing between a quiet night and thirty open PRs.
The cap applies **after** ordering, never before: capping first would take the
three lowest-numbered issues and could admit a dependent without the thing it
depends on.

#### One branch, one PR, one issue

Never a combined PR, even for two small issues touching the same file.

A combined PR cannot be partly rejected, carries an ambiguous closing keyword,
and — the one that bites quietly — has to pick a single Conventional Commit
type. release-please derives the version from that type, so a PR bundling a
`feat:` with a `fix:` ships the wrong version number whichever label it takes.

#### The builder never merges and never closes

Not a green PR, not a one-line fix, not its own work.

The pipeline's job is to get work to the point where Derek can look at it. A
pipeline that also merged would make the review step optional in practice, and
an agent that can approve its own output has no gate on it at all. Closing is
the same rule from the other end: `Closes #N` in the PR body closes the issue
when Derek merges, and closing it directly would mark work done that nobody
accepted.

#### `skip-docs` is applied immediately, or not at all

A PR touching no files under `docs/` gets `skip-docs` **the moment it opens** —
before anything else in the round.

The reconciliation gate fails a code-only PR, and the label is the sanctioned
escape hatch. Applying it late means the round's first visible output is a red
X on a PR that was never wrong, which teaches everyone to skim past red. It
still needs a written justification in `## Deviations and Decisions`, because
the gate cannot distinguish "no docs needed" from "forgot the docs" — that is
the whole reason a human has to say which it was.

#### The owner-invoked alternative: `milestone-orchestration`

When Derek hands over several issues at once and wants them *delivered*, the
`milestone-orchestration` skill runs the same dev agent with one difference:
the gate between issues is **merged**, not **PR opened**.

| | Nightly builder | `milestone-orchestration` |
| --- | --- | --- |
| Gate between issues | PR opened | **PR merged** |
| Invoked by | the 03:00 schedule | Derek, explicitly |
| A failing issue | dropped, round continues | **halts the run** |

Merging between issues is what makes a multi-issue run conflict-free by
construction: every branch is cut from a `main` that already contains all
previously delivered work, so two issues in one run cannot produce conflicting
diffs. The nightly builder does not need this, because its PRs sit unmerged
until Derek reviews them and he resolves any overlap by choosing what to merge.

A failure **halts** rather than skipping, because the next issue would be built
on a `main` that is missing work the handoff assumed was there. Whether that
matters is unknowable without reading both issues, so the run stops and says
which issue failed.

**It is owner-invoked only, never scheduled.** Everything else in the pipeline
stops at "PR opened" so a human sees the work before it becomes the game. This
skill merges, and it exists because Derek sometimes applies that judgement up
front to a set he chose. Wiring it to a trigger would convert a decision about
six specific issues into a standing grant to merge anything.

#### A failing issue is dropped, and the round continues

If an issue cannot be completed — tests won't pass, the plan was wrong, a
checklist box cannot be ticked — the branch is deleted and **no PR is opened**.
No draft, no partial. A PR that does not close its issue still costs a review,
and a partial implementation hides an untickable checklist box that is really a
triage problem.

The issue keeps `in-progress`, deliberately. Reconcile finds an open
`in-progress` issue with no open PR and nothing on `main` and returns it to
`ready-for-work`, so the failure self-heals by the next round and stays visible
on the dashboard until it does.

One failure never aborts the round — the other issues are unrelated.

### Reconciliation — drift

Reality drifts: a PR merges without its closing keyword, someone hand-edits a
label, a run crashes between two API calls. `reconcile.py` compares what the
labels claim against what GitHub shows, and sorts each finding into
**auto-fixable** or **flag-only**.

**Auto-fixed** — mechanical, single correct answer, no judgement:

| Finding | Condition | Action | Runs |
| --- | --- | --- | --- |
| `strip_labels` | closed issue still carrying a pipeline-state label | remove it | every pass |
| `requeue` | open, `in-progress`, no open PR, not on `main` | → `ready-for-work` | **cron only** |
| `requeue_triage` | a state label with no triage-authored analysis | → `ai-triage` | **cron only** |

**Flagged, never touched** — anything needing judgement:

| Finding | Condition |
| --- | --- |
| `flag_merged_but_open` | the work is on `main` but the issue is still open |
| `flag_orphaned_analysis` | an analysis comment with no state label |
| `flag_orphaned_ready` | `ready-for-work` with no milestone |
| `flag_prose_dependency` | `Blocked by #N` in the body with no native edge |
| `flag_cycle` | a dependency cycle |
| `flag_dashboard_count` | two dashboard issues, or none |

The split is the design. A reconciler that guesses is a reconciler you have to
audit, and one you have to audit is one you turn off — at which point it is
worse than absent, because the dashboard still says it ran.

#### The sweep never closes an issue

Not even when the work is demonstrably merged and the issue is demonstrably
still open. That is `flag_merged_but_open`, and it stays a flag.

`Closes #N` in a PR body is what closes an issue, when Derek merges it. A sweep
that also closed issues would be deciding that work is finished and accepted on
evidence that it merely *landed* — and the two are not the same claim. Getting
it wrong closes something nobody agreed was done, silently, at 01:00.

#### Why two of the auto-fixes are cron-only

`requeue` and `requeue_triage` are correct nightly and wrong on the event path,
because **the drift they detect is indistinguishable from work in flight.**

- An issue the builder picked up seconds ago has `in-progress` and no PR yet.
  That is exactly `requeue`'s condition, and firing it would yank the issue out
  from under a running agent.
- Triage posts its analysis comment *before* setting the state label. Between
  those two writes an issue legitimately has neither; moments later it has both.
  Mid-write, `requeue_triage`'s condition holds.

By 01:00 the transient has resolved: an issue still looking stalled hours later
genuinely is. So they are **omitted entirely** on the event path rather than
softened with a time threshold — a threshold is a guess about how long an agent
takes, and it would be wrong in both directions.

`strip_labels` has no such problem and runs on every pass. It acts only on an
already-closed issue, and a closed issue is not transiently anything.

#### Done-ness is read from commit bodies

Whether work landed is decided by a **closing keyword in a merged commit's
body** — never the subject line, never a bare `#N` or `Refs #N`.

The subject line is excluded because a squash merge appends `(#148)` to it, and
that is a *PR* number. Bare references are excluded because PRs and commits
mention issues constantly for context; treating a mention as completion would
mark work done because somebody linked to it.

**`flag_merged_but_open` is checked before the stall rule**, so an issue already
on `main` never reads as a stall. Without that ordering it would be requeued,
rebuilt, and requeued again every night — a loop that produces a duplicate PR
each time.

### Dashboard

`render_dashboard.py` rewrites the dashboard issue from live state. The
sections, in order:

| Section | Shows | Scope |
| --- | --- | --- |
| 🎯 Focus | the four-slice pie for the focus milestone | focus |
| 🔨 Ready queue | `ready-for-work`, headed by the build cap | focus |
| 📥 Intake | `ai-triage` — waiting to be analyzed, plus unadmitted work | board-wide |
| ✋ Waiting for you | `pending-approval` — waiting on Derek | board-wide |
| ❓ Needs clarification | blocked on a question | board-wide |
| ⏸️ Parked | set aside, listed so it can be found | board-wide |
| ⚠️ Reconcile | the sweep's flag findings | board-wide |
| 📅 Other milestones | progress elsewhere; finished milestones omitted | every other |
| 🎮 Commands | the command reference | — |

Every issue table carries a **Milestone** column, so the milestone is visible
at every stage rather than only where an issue is being scheduled, and a
**Blocked by** column linking each hard blocker.

#### Only the pie and the ready queue are focus-scoped

Those two answer *what is being built now*, and the builder builds the focus
milestone. Listing an out-of-focus `ready-for-work` issue in the queue would
put work in front of Derek that nothing is going to pick up.

Every other section answers a different question — *what does somebody have to
look at* — and an issue does not stop needing to be looked at by sitting
outside the milestone currently being built. Scoping those to focus hides the
work that most needs surfacing:

- **Intake is the worst case.** `ai-triage` means the issue has *not* been
  triaged, and triage is what decides its milestone — so an issue waiting for
  triage usually has no milestone at all. A focus-scoped Intake is
  structurally near-empty: the section reads "Nothing waiting for triage"
  precisely because the untriaged pile is growing out of sight.
- **`Direct Involvement Needed` can never be the focus.** It carries no
  version and never ships, so a focus-scoped "Waiting for you" hides every
  issue in the one milestone that exists to say *Derek has to do this by
  hand* — which is most of them.
- **A question is not answered by being scheduled late**, and answering it
  early is often what lets the issue be scheduled at all.
- **Parked work is listed so it can be found.** An out-of-focus parked issue
  is the one most in need of that.

The **Milestone** column is what keeps a board-wide table readable: a row
reading `—` is an issue nobody has scheduled, which is the point of showing
it. If every table were focus-scoped, that column would be a constant.

#### Intake carries two piles, and flags which is which

An issue with **no pipeline-state label at all** has not been `/admit`ted, and
the nightly analysis run will never pick it up — [its entry condition is
`ai-triage` and nothing else](#analysis--find-what-needs-triage). Left off the
board entirely, such an issue is invisible until somebody happens to scroll
the issue list, which on this repo meant seven of twenty-one open issues.

So Intake lists both, and marks the difference:

| Row | Means | Who moves it |
| --- | --- | --- |
| plain | `ai-triage` — the pipeline has it | tonight's analysis run |
| `🚪 not admitted` | no state label — nothing has it | Derek, with `/admit` |

The flag is not decoration. The two piles are waiting on different things, and
an Intake row that did not say which pile it was in would make the section
mean two contradictory things — "handled" and "stalled on you" — at a glance,
which is the only way this section gets read.

**Two issues are never listed as unadmitted**: the `dashboard` issue, which is
the pipeline's own furniture, and any `type:epic`, which is a container whose
children carry the work. The epic case is exactly what
[`epic-excluded` refuses](#where-each-command-is-refused), and listing an
issue beside a command that gets refused is worse than not listing it.

Unadmitted issues stay in the pie's **Unplanned** slice, and only inside the
focus milestone — the pie's job is to add up, and widening Intake does not
move an issue between slices.

#### Unblocker stars

An issue is starred `⭐ unblocks #A, #B` when it appears in another open
issue's hard-blocker set **and is not itself blocked**, and it sorts to the top
of its table. Blocked rows carry `⛔ blocked` naming what they wait on.

Both halves of that condition matter. Appearing in someone's blocker set is
what makes an issue leverage — approving it frees work that is otherwise stuck.
Not being blocked itself is what makes it *actionable*. Starring a blocked
issue would point Derek at something nobody can start, which is worse than no
star: it costs a click to find out the suggestion was useless, and a
recommendation that is often useless stops being read.

A closed blocker is never starred — it has already done its unblocking.

#### Parked is listed, never queued

The Parked section is read-only, and `parked` issues are excluded from every
other queue and count on the board — including when an issue somehow carries
both `parked` and `ready-for-work`.

**The pie's Unplanned slice is the one deliberate exception**, because the pie
has to add up. Excluding parked work there would make the slices sum to less
than the milestone's issue count, and a total that does not reconcile is a
board you stop trusting.

Wholly regenerated every time, apart from the config markers. Nothing on it is
remembered, so nothing on it can be stale.

Rendering is **deterministic and byte-stable**: the same state produces the
same bytes. Without that, every hourly run would PATCH the issue and the
dashboard would generate a stream of meaningless edits.

#### No model renders the board

Production rendering is `dashboard.yml` running `render_dashboard.py`. Nothing
about the body is generated prose — every line comes out of a pure function of
the state.

The board is what Derek reads to decide what to approve next. A model-written
summary can be subtly, fluently wrong in a way that reads perfectly; a table
computed from labels either matches GitHub or has a bug someone can find.

#### The render's write surface is one issue body

The renderer reads the whole board and can modify **exactly one thing**: the
dashboard issue's body, via a single `PATCH`, authenticated with `GITHUB_TOKEN`
rather than a PAT. No labels, no comments, no milestones, no other issues. Run
without `--write` it does not write at all — previewing is the default, because
it is the common case and the safe option should not need a flag.

That narrow surface is what makes an incorrect render cheap. A wrong board is
replaced by the next render; if the renderer also applied labels, a bug in the
star logic would be a bug that reorganises the pipeline.

#### The focus pie always adds up

The focus milestone is summarised as four slices:

| Slice | What is in it |
| --- | --- |
| **Unplanned** | `parked`, or carrying no pipeline-state label at all |
| **In Planning** | `ai-triage`, `pending-approval`, `needs-clarification` |
| **Ready** | `ready-for-work`, `in-progress` |
| **Done** | closed, whatever its labels say |

Every issue in the milestone falls into **exactly one**, and the four counts
sum to the milestone's issue total. That is the property worth protecting: it
means an issue cannot quietly disappear from the board by ending up with an odd
combination of labels. The slices are assigned by ordered checks rather than
independent predicates, so there is no gap to fall through and no overlap to be
counted twice.

Parked and never-triaged share the Unplanned slice because they mean the same
thing for planning — not being worked, and not waiting on anybody — even though
they got there for different reasons. This is the **one** place a `parked`
issue is counted; every active-work queue excludes it, because the board's
Parked section is a listing, not a re-admission.

#### How reconcile's flags surface

The sweep's flag findings render into the board's **⚠️ Reconcile** section, and
that is the only place they appear. They are regenerated with everything else,
so a flag vanishes the moment its condition stops holding — and one still
sitting there after several days is one nobody has dealt with.

This is why a flag does not post a comment. A flag describes the board's state
*right now*; a comment would outlive the state it described and turn into a
false report of a problem that was fixed weeks ago. The dashboard can be wholly
regenerated. A comment thread cannot.

## Skills inventory

The skills the pipeline itself runs. `.claude/skills/` holds more than these —
see [Skills here are local](#skills-here-are-local-and-deliberately-not-synced).

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

**`.claude/skills/` is the source of truth in this repository.** Nothing here
subscribes to an upstream. Two kinds of skill live in the directory, and the
difference decides what a future reconciliation is allowed to do.

**Ported skills** — the pipeline skills, `release-flow`, `dw-run-tests`,
`ci-watch`, `scaffold-core`, `core-unity-split`, `issue-blockers`,
`milestone-ops` — were hand-ported from `derekwinters/lucas-doggiehood` and then
diverged to fit this game: different release config, different milestones, a
different label taxonomy.

**Vendored skills** are verbatim third-party copies pinned to an upstream
commit, with no local divergence. Today that is the 25 skills from
[`mattpocock/skills`](https://github.com/mattpocock/skills) (MIT, license kept
at `.claude/skills/LICENSE.mattpocock-skills`) — `grilling`, `wayfinder`,
`tdd`, `to-spec`, `to-tickets`, `code-review` and the rest. They are vendored
rather than installed with `/plugin install`, because a plugin is per-user
machine state: it would be present for whoever ran the command and absent for
everyone else who clones the repo, and it would not be pinned to a reviewed
commit.

Re-vendoring one is a clean diff while `diverged` stays `false` in the manifest.
It is still a manual, reviewed act — see [Why not sync](#why-not-sync), which
applies to both kinds for different reasons.

### Vendored skills do not outrank this repo

Where one of our skill names would be confused with a vendored one, ours carries a
`dw-` prefix — `dw-run-tests`, not `run-tests`. The prefix marks a collision, not
ownership, so most of our skills do not have one, and it comes off again when the
skill it collided with is removed. See
[Skill names](../intro/conventions.md#skill-names).

Several vendored skills cover ground `/docs` already specifies, and where they
disagree, this repo wins:

| Vendored skill | Defers to |
| --- | --- |
| `tdd` | CLAUDE.md rule 1 and [testing](testing.md) |
| `code-review`, `implement` | the [agent workflow](agent-workflow.md) |
| `resolving-merge-conflicts` | CLAUDE.md rule 7 — this repo rebases |
| `to-tickets`, `to-spec` | the label and milestone [conventions](../intro/conventions.md) |

A vendored `triage` skill used to sit in that table, and it was the dangerous
one: it knew nothing about our label taxonomy, the milestone rules, or the
hand-back-to-Derek step, so an agent reaching for it instead of this repo's
`triage-issue` dropped all three silently. It has been **removed** rather than
deconflicted, because a prefix only protects you if you read it, and the two
skills wrote different label vocabularies onto the same issues.

Deleting a vendored skill is the stronger fix and the one to prefer when its
whole subject is already specified here. Renaming ours was never going to stop a
tired reader picking the wrong one.

Its siblings still mention it — `ask-matt` recommends `/triage`, and
`setup-matt-pocock-skills` offers to configure its labels. Those files are left
**verbatim**, because editing a vendored copy to patch a reference sets
`diverged` and turns every future re-vendor into a manual fixup. The references
are harmless: `setup-matt-pocock-skills` already asks whether `triage` is
installed and skips its whole label section when it is not, and a session asked
to triage now resolves to `triage-issue`, which is the skill you wanted.

The prefix cannot help where the clash is between two names the repo does not
both own. Two vendored names — `grilling` and `code-review` — also exist as a
personal or built-in skill, and which one a session resolves is harness
precedence, not something the repo controls. Check the description you got before
relying on either. Renaming the vendored copy is not the fix: it would break the
`/code-review`-style cross-references inside its siblings.

There is **no `skills-update` workflow**, no scheduled sync, and nothing here
reads an upstream skills repository at runtime. No `AI_SKILLS_READ_TOKEN` secret
is needed, and one should not be added.

### Why not sync

For **ported** skills: a sync would eventually revert local work. That is not a
hypothetical: it is why the upstream project disabled its own sync. Once a skill
has diverged — which it does the first time it mentions
`.github/release-please/config.json` or `type:wireframe` — an upstream copy is
not an update, it is a regression wearing an update's clothes. And it arrives on
a schedule, so it lands when nobody is looking at it.

For **vendored** skills the risk is the other way round. There is no divergence
to lose; what a sync would do is pull unreviewed third-party instructions
straight into the agent's context on a timer. A skill file is not a library —
it is a standing instruction to an agent working on this repo, and an upstream
edit could contradict CLAUDE.md, reach for a tool this project has ruled out, or
just quietly change what `/grilling` does mid-session. That is a change someone
should read before it takes effect, which is exactly what a pinned commit and a
reviewed bump give you.

### The manifest is a record, not a subscription

`.claude/.skills-manifest.json` says where each skill came from and whether it
has diverged, so a future reconciliation knows what was copied from where. It
carries `"sync": "disabled"` and the reason, so nobody wires a workflow to it by
assuming that a manifest implies a sync.

Each ported skill carries `ported_from`; each vendored skill carries
`vendored_from` and its `source_path` in the upstream tree. The
`vendored_upstreams` list holds the part that makes a re-vendor reproducible —
repository, licence, and the exact `pinned_commit` the current copies came from.
Bumping a vendored skill means moving that pin in the same commit as the files,
so the record and the copies cannot drift apart.

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
