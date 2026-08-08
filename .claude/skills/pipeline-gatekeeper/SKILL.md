---
name: pipeline-gatekeeper
description: Parse and apply Derek's slash commands on issues, and run the board-wide sweep. Use to run the gatekeeper by hand, to work out why a command was refused, or when a comment did not take effect.
---

# pipeline-gatekeeper

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs
export PIPELINE_OWNER=derekwinters

# One comment event. Reads the webhook payload on stdin.
python3 .claude/skills/pipeline-gatekeeper/run_comment_event.py < event.json

# The board-wide sweep. Reads a state snapshot on stdin.
python3 .claude/skills/pipeline-gatekeeper/run_sweep.py               < state.json
python3 .claude/skills/pipeline-gatekeeper/run_sweep.py --events-only < state.json
```

## In production this is two workflows, with no model in the loop

`gatekeeper-comment.yml` on every `issue_comment`, and `gatekeeper-sweep.yml`
every fifteen minutes. Both run these scripts directly. **Nothing here asks a
model anything.**

That is the whole point of the design. The gatekeeper is what turns Derek
typing `/approve` into an issue the builder will pick up — it is the boundary
between "a human decided" and "the machine acted". A model interpreting that
boundary could be persuaded, could misread, could be creative on a bad day.
A parser either matches the line or does not.

This skill exists for running the same logic by hand: replaying an event that
did not take, or working out why a command was refused.

## The command vocabulary

Commands are lines beginning with `/`. A comment can carry several; they apply
in order, and prose around them is ignored, so you can explain yourself in the
same comment.

| Command | Effect |
| --- | --- |
| `/admit` | bring an issue into the pipeline — becomes `ai-triage` |
| `/propose` | ask triage to design it → `ai-triage` |
| `/approve` | the plan is right → `ready-for-work`. **Both gates apply** |
| `/revise <notes>` | the plan is wrong → `ai-triage`, with the notes |
| `/redo` | the built work is wrong → `ready-for-work` |
| `/park` | set aside deliberately |
| `/unpark` | bring it back → `ai-triage` |
| `/milestone <title>` | set the milestone, by title |
| `/focus <title>` | set the focus milestone. **Dashboard issue only** |
| `/cap <n>` | issues per build round. **Dashboard issue only** |

**Nothing here closes an issue, edits a body, or merges a PR.** Those have
perfectly good GitHub buttons, and a command vocabulary that can do
irreversible things is one that eventually does an irreversible thing by
accident.

A command inside a fenced code block is documentation, not an instruction —
otherwise writing this table in a comment would execute it.

## The owner gate, and why refusal is silent

**Only the repository owner's commands are obeyed.** Anyone can comment on a
public issue; nobody but Derek can drive the pipeline.

A stranger's command is dropped **silently** — no reply, and *no reaction*.
Replying would let an outsider make the bot post, which is a smaller hole of
exactly the same shape as obeying them. So the comment is left completely
untouched, as if it were ordinary conversation, which is what it is.

The check happens in the parser **and again** in `run_comment_event.py`. That
redundancy is deliberate: a script that is safe only because the workflow's
`if:` filtered for it is one careless edit away from not being safe at all.

## The 👀 watermark

An applied comment gets an `eyes` reaction from the bot, and a comment already
carrying one is never reconsidered.

This is what makes the fifteen-minute sweep safe. The sweep re-reads recent
comments to catch anything the event path missed — a dropped webhook, a
workflow that failed to start — and without a marker it would re-apply every
command it found, every time.

Two details matter:

- **Only the bot's own 👀 counts.** A human reacting out of interest must not
  silence a command.
- **A refused comment is watermarked too** — except a stranger's. It was
  considered and answered; without the mark the sweep would re-post the same
  refusal every fifteen minutes forever.

If a reaction lookup fails, the comment is treated as **already watermarked**.
Re-applying a command is worse than skipping one, and the next sweep retries.

## The two approval gates

`/approve` is the only command that can put work in the builder's path, so it
is the only one with gates:

| Gate | Refuses when |
| --- | --- |
| `milestone-presence` | the issue has no milestone |
| `milestone-order` | an open blocker is scheduled later than the issue |

**Both refuse; neither auto-bumps.** A gate that fixed the problem itself would
be choosing a milestone, and that is a scheduling decision about what ships
when — which is Derek's, not the pipeline's. The refusal names the specific
problem, so the fix is one `/milestone` away.

The second gate is worth understanding. An issue in `v0.0.1` blocked by one in
`v0.1` is a silent stall: the builder correctly skips the dependent, but the
blocker is not in the focus milestone to be built either. Nothing ever runs,
and from every individual angle each issue looks fine. Only the relationship is
wrong.

`/milestone` is gated on order too, since setting a milestone is the other way
to create that inversion. `/admit` deliberately is not — admitting an issue is
part of what *fixes* a missing milestone.

## A refusal changes nothing

Not the labels, not the milestone, not a partial application of a multi-command
comment. The acknowledgement says so explicitly, because "your command was
refused" and "your command was partly applied" call for very different
responses from the person reading it.

## Running by hand

Both entry points read JSON on stdin and take the API as an injected callable
internally, so every decision is reachable in a test without a network call.

- `run_comment_event.py` — an `issue_comment` webhook payload. Snapshot →
  parse → gate → apply → write labels, ack, reaction → re-render the dashboard
  **once**, after all label writes.
- `run_sweep.py` — a state snapshot. Revisits cleared blockers, then reconciles.
  `--events-only` drops reconcile's two cron-only fixes; use it when simulating
  the fifteen-minute pass.

Authentication is `GITHUB_TOKEN`, never a PAT.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py pipeline-gatekeeper
```

158 tests. The parser, the gates, the label planning, the revisit rules, and
the I/O ordering — none of them touch the network.

## See also

- `docs/engineering/issue-pipeline.md` — the state machine and every refusal code
- `pipeline-reconcile` — the sweep's second half
- `pipeline-dashboard` — what the re-render calls
