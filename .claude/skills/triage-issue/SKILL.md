---
name: triage-issue
description: Triage exactly one admitted issue — diagnose it, plan it, and hand it back to Derek for approval. Use when an issue carries ai-triage, when a triage run needs redoing, or when asked to work out what an issue actually involves.
---

# triage-issue

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

# Triage one issue. That is the whole interface.
#   triage-issue 47
```

One issue in, one hand-back out. Runnable standalone on a single issue number —
nothing about this skill knows or cares whether a batch is running around it.

## What this skill will not do

**It never invents a game mechanic, and it never sets `ready-for-work`.**

Those are the same rule wearing two hats. Every route below ends with the issue
waiting on a human: `pending-approval` if there is a plan to agree with,
`needs-clarification` if there is a question to answer. The label that says
"build this" is only ever applied by Derek typing `/approve`.

This matters more here than anywhere else in the pipeline. Triage is the stage
that decides what the game *is* — and this is Connor's game. An agent that fills
a specification gap with its own reasonable idea has quietly taken a design
decision away from an eight-year-old, and it will do it convincingly enough that
nobody notices for weeks.

If `/docs` does not say how something behaves, that is not an ambiguity to
resolve. It is the finding.

## Reads widely, writes narrowly

Read whatever helps: `/docs`, the code, sibling issues, the milestone list,
closed issues that solved something similar.

Write to **exactly one issue — the one you were invoked for.** No comments on
other issues, no relabelling a sibling that looks mis-triaged, no tidying.

The reason is blast radius. A triage run that writes to one issue can be wrong
about one issue. A triage run that writes to whatever it reads can be wrong
about all of them, and the batch that dispatched it has no idea it happened.

The single exception is **recording a dependency you discovered** — that is a
relationship, it needs both ends, and leaving it unrecorded is how the builder
walks into a blocker nobody can see. Nothing else.

## The four routes

Read the issue, then pick exactly one. Most of the skill is knowing which.

| Route | When | Ends at |
| --- | --- | --- |
| **Bug** | Something behaves against what `/docs` says | `pending-approval` + `type:bug` |
| **Spec-covered feature** | `/docs` already says how this behaves | `pending-approval` |
| **Needs a design call** | `/docs` doesn't say, or it's UI with no wireframe | `needs-clarification` |
| **`/propose` authorized** | Derek explicitly asked for a design | `pending-approval`, marked PROPOSAL |

### Bug

Diagnose the **root cause**, not the symptom. Then: the fix approach, a
`## Build checklist`, the milestone, `type:bug`, `pending-approval`.

A bug report says "my frog went backwards off the bottom of its lane". The
diagnosis says which code path skipped the Start-log floor and why. A plan
written against the symptom fixes the one case in the report and leaves the
other three.

#### When the root cause is a missing rule

Sometimes there is no coding mistake. The code does exactly what someone
intended; nobody ever wrote down what it should do, so two parts of the game
disagree and one of them looks like a bug.

That is a **spec gap**, and patching the code alone guarantees the same class of
bug returns somewhere else. The plan proposes the missing **invariant** in
plain English, and the checklist gets an item that tests it:

> **Proposed invariant:** a frog never moves below the Start log, whatever
> moved it back — a wrong answer, or a turn that resolves twice.
>
> - [ ] Test: a wrong answer on the Start log leaves the frog on the Start log

Proposing an invariant is not inventing a mechanic: it is writing down the rule
the existing behaviour already implies, so it can be agreed or corrected. If the
rule is a genuinely new choice about how the game plays, that is the
**needs a design call** route instead — and if you cannot tell which, it is that
route. See `docs/engineering/agent-workflow.md` on specs stating rules.

### Spec-covered feature

`/docs` already answers how this behaves, so the work is implementation, not
design. Produce the implementation plan, the `## Build checklist`, the milestone
matched from the live list, and `pending-approval`.

If the plan changes what a spec *says*, state the shift in plain English — used
to say X, now says Y, and why Y is better. Not "the docs were updated". A
reviewer skimming needs to see the contract move, because that is the part they
might disagree with.

### Needs a design call or a wireframe

Stop. Do not write a plan, a checklist, or a milestone — a plan on an
undecided question is an answer smuggled in as paperwork, and half of it will
survive into the build because it was already written down.

Ask one concrete question:

```markdown
❓ **Needs from Derek/Connor:** when a wrong answer moves a frog back a lily
pad, should the frog hop backwards, or just appear on the lower pad?

Either is easy to build. I've not guessed because it changes how it feels to
get one wrong.
```

Concrete means answerable with a sentence. "How should this work?" hands the
whole design back; naming the two or three options Connor can choose between is
the useful version.

Then `needs-clarification`, and nothing else.

**UI with no agreed wireframe always takes this route**, whatever else the issue
says — no UI code exists before a `type:wireframe` issue is agreed. See
`docs/engineering/ui-design-process.md`.

### `/propose` authorized

`/propose` is Derek saying "go ahead and design this one". Only then does triage
draft a design — and it is drafted as a **clearly marked proposal**:

```markdown
## 🎨 PROPOSAL — not decided

This is a suggestion for Connor to react to, not a plan. Change any of it.
```

Then the checklist, the milestone, `pending-approval`. The marking is what keeps
the exception from eroding the rule: three weeks later a proposal that isn't
labelled as one reads exactly like a spec.

## The hand-back comment

**Opens with the plain-English lead — two or three skimmable sentences, before
any diagnosis or file detail.** Same rule as a PR body, same reason: a wrong
plan has to be catchable on a skim, and technical accuracy agrees with a wrong
plan just as fluently as a right one.

> Frogs keep multiplying past the limit when you tap them quickly. The counter
> is checked before the tap is processed instead of after, so two fast taps
> both see room for one more. The fix is to check at the moment the frog is
> added.

Then the diagnosis or plan, the `## Build checklist`, the spec-pages line, and
any question. The checklist **is** the acceptance criteria — the dev agent
cannot tick a box it cannot verify, so vague boxes come back as unfinished
issues.

See `docs/engineering/agent-workflow.md` for the lead's full rationale.

### The footer is added for you

`hand_back.py` appends the commands that make sense **on the route taken** —
`/approve`, `/revise`, `/park` on a plan; `/revise <answer>` and `/park` on a
question. Do not write one by hand and do not paste the whole command list: a
`needs-clarification` hand-back that offers `/approve` invites approving a plan
that was never written.

The wording comes from `render_dashboard.COMMANDS`, so there is one description
of each command rather than a third copy drifting quietly out of date.

## Write ordering: comment first, then the label

**Do not do this by hand. Call `hand_back.py`.**

```python
import hand_back
hand_back.apply(api, 47, analysis, labels, "pending-approval")
```

One call, both writes, in the right order — or neither. It appends the route's
footer, removes `ai-triage`, adds exactly one state label, and keeps every
`area:`, `type:` and `skip-docs` label untouched.

It **refuses before writing anything** if you pass a state triage may not set
(`ready-for-work` is Derek's, via `/approve`) or an analysis the recognizer
would not match. Both refusals leave the issue exactly as it was.

This used to be an instruction in this file rather than code, and it was skipped
in practice — sixteen issues sat on `ai-triage` carrying finished plans, because
a missing label fails nothing and the posted comment made the run look done.

**Post the analysis comment. Then set the state label. Always that order.**

Not style — it is what makes the bad state structurally impossible. If the run
dies between the two writes:

| Order | If it dies halfway | Recoverable? |
| --- | --- | --- |
| comment → label | a plan sitting on `ai-triage` | yes — the next run redoes it |
| label → comment | `pending-approval` with **no plan** | no — it waits for approval of nothing |

The second one is silent. Derek sees an issue awaiting approval, opens it, and
there is nothing to approve — and until he does, the pipeline believes that
issue is handled. The first one just looks untriaged, which is what it is.

`triage_repair.py` cleans up the second case when it happens anyway. Ordering is
what stops it happening.

## Labels and the milestone

Set the milestone as a **field**, by matching the live milestone descriptions —
never by guessing from a title, and never from a list written down in the docs.
Milestones change; the API is the truth. `milestone-ops` resolves a title to the
number the API needs.

Remove `ai-triage` **in the same write** that adds the new state label. Exactly
one state label belongs on an open issue, and an issue carrying both is an issue
the next analysis round picks up and triages again — while it sits in
`pending-approval` waiting for Derek.

Every issue also leaves triage with one `area:*` and one `type:*` label.

## Dependencies are structural, never prose

| Relationship | How to record it |
| --- | --- |
| Cannot start until #42 is done | **native** blocked-by (`issue-blockers`) |
| Easier after #42, but not blocked | `Depends on: #42` line |
| This is part of epic #12 | **sub-issue** of #12 |

A dependency written as a sentence in the body is a dependency the nightly
builder cannot see, so it walks straight into the issue and starts it. Record it
the moment you notice it — this is the one write allowed outside the issue being
triaged.

`Depends on:` deliberately has no native form; converting one into a hard
blocker is how a queue deadlocks on work that could have been done at any time.

## Re-running on the same issue

Triage runs twice — a `/revise`, a `/redo`, a sweep catching a comment late, a
run that crashed after commenting but before labelling. `triage_repair.py`
decides what to do about it, deterministically:

```python
# .claude/skills/triage-issue/triage_repair.py — a module, not a CLI
plan = plan_repair(labels, comments, intended_state="pending-approval", note=note)
```

| Situation | Plan |
| --- | --- |
| An analysis is already here | **repair** — apply the missing label move, post nothing |
| No analysis | **re-analyze** — a normal triage run |
| Analysis, and the label is already right | nothing at all |
| `/revise`, `/redo`, or `/propose` | **re-analyze**, even though an analysis exists |

**Repair, not repeat.** Re-analyzing an issue that already has a plan stacks a
second plan on the first, and the two will not agree — triage is not
deterministic, so the same issue analyzed twice produces two different
reasonable plans, and Derek gets to work out which one `/approve` means.

The last row is the exception that matters. `/revise` is an objection *to the
plan*; repairing the label would apply exactly the state the rejected plan asked
for and drop the objection on the floor. The owner's note comes attached from
`select_triage.py`, so the rewrite answers the actual complaint.

### The recognizer is shared

`has_analysis_signature` matches a `## Build checklist` heading at the start of
a line, or the `❓ Needs from Derek/Connor:` marker with optional emphasis
around it. Prose mentioning either phrase is not a match — the heading must be a
heading, and the ❓ is what separates the marker from a sentence containing the
same words.

`pipeline-reconcile` **imports this function** rather than keeping its own copy.
Two copies drift, and the drift is silent and self-sustaining: reconcile decides
there is no analysis and sends the issue back to `ai-triage`; triage sees the
analysis it wrote and repairs the label; reconcile sends it back again. Neither
side looks wrong alone, and the issue cycles forever. The side that writes the
format owns the recognizer.

Only comments authored by the bot count. Derek pasting a checklist by hand is
not a triage run, and treating it as one would let a helpful comment suppress
the analysis the issue is actually waiting for.

`is_triage_author` is that gate, and it is shared the same way — `reconcile.py`
imports it too. Both surfaces count: `claude[bot]` when triage runs as the App,
`github-actions[bot]` when it runs from a workflow. Accepting only the second
was a real bug and a quiet one: the gate runs *before* the recognizer, so every
analysis on the board read as absent while both modules agreed with each other.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py triage-issue
```

57 tests over `triage_repair.py` and `hand_back.py`: what the recognizer matches
and — more importantly — what it refuses to match, the four repair outcomes, and
the hand-back write. No network in any of them; `apply` takes an `api` callable,
so its ordering is asserted against a fake that records calls.

Two are worth knowing about because they pin a rule rather than a behaviour:
one asserts the footer never names a command `parse_commands` would refuse (the
test that catches a `/retriage`), and one asserts a comment still reads as an
analysis *after* the footer is appended — a footer that broke the recognizer
would send every triaged issue back round the loop.

## See also

- `docs/engineering/issue-pipeline.md` — the stage this sits in
- `docs/engineering/agent-workflow.md` — the plain-English lead, spec invariants
- `docs/engineering/ui-design-process.md` — wireframe before UI code
- `issue-blockers`, `milestone-ops` — the two skills this one calls
