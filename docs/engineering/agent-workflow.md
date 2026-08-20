# Agent workflow

How an issue actually becomes a merged PR: who does the work, the loop they
follow, and what the PR they open has to contain.

The short version of this page is in
[`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md).
This is the version you come back to when you want to know *why* a rule is
shaped the way it is.

## Who writes the code

**A dedicated development agent** — [`.claude/agents/dev.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/.claude/agents/dev.md) —
not an ad-hoc chat session.

That distinction is the point. An agent definition is a file: it is reviewed,
versioned, and improved. When it gets something wrong twice, the fix goes in the
file and every future run inherits it. A chat session that got the same thing
right is a chat session whose good judgement disappeared when the window closed.

The agent carries the rules that are easy to drop under pressure:

- **Strict TDD** — the failing test first, and seen to fail
  ([Testing](testing.md)).
- **The Core/Unity split** — logic in the engine-free assembly
  ([Tech stack](tech-stack.md)).
- **Named values** — no bare geometry or tuning literals
  ([Tech stack](tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)).
- **Conventional Commits** — because the version and changelog derive from them
  ([Versioning](versioning.md)).

None of these are things an agent will get right by being generally competent.
They are things it gets right by being told, every session, in a file it cannot
skip.

It also carries one rule that overrides all of them, including finishing the
issue it was given: **never invent a design decision.** If the issue and the
docs do not say how something behaves, the agent opens a `type:question` issue
and works something else. A guessed mechanic that ships is a game drifting away
from being Connor's, which is the whole reason for building it this way.

### Supporting skills

The agent doesn't hold every procedure in its own definition. Repeatable
procedures live in skills under `.claude/skills/`, so they can be invoked, read,
and fixed independently:

| Skill | For |
| --- | --- |
| `dw-run-tests` | running the Core and EditMode suites, and reading the results |
| `ci-watch` | watching a PR's checks and diagnosing a red one |
| `scaffold-core` | creating a new Core type with its test, wired to the right asmdef |
| `core-unity-split` | reference: what may and may not live in Core |
| `github-api` | what may be read and written in GitHub, and what may not |
| `issue-blockers` | recording and reading native blocked-by relationships |
| `milestone-ops` | milestone queries and moves |
| `release-flow` | driving a release through release-please |

The last four come from ai-sdlc and are installed by `skills-update.yml`. Since
ai-sdlc v0.4.21 they carry no scripts: they are instructions, and the GitHub
reads and writes they describe go through `github-api` rather than through
Python that expected somebody to hand it a client.

The pipeline skills — `pipeline-gatekeeper`, `pipeline-analysis`,
`triage-issue`, `pipeline-dev`, `pipeline-reconcile`, `pipeline-dashboard` — are
a separate family. They operate the queue rather than write game code. See
[Issue pipeline](issue-pipeline.md).

Skills written **here** are isolated copies: they are not synced from anywhere,
so a fix made here stays here and a change made elsewhere does not silently
arrive. The ones named in `repo-config.yml` are the exception — `skills-update`
keeps those at ai-sdlc's pin and opens a pull request when they move.

## How an issue gets worked

1. **Pick one issue.** From the focus milestone, `ready-for-work`, not blocked.
   One. Bundling is how a reviewable change becomes an unreviewable one.
2. **Read the spec pages it names.** Every issue lists the pages in `/docs` it
   touches. Read them before the code, because they are the contract the change
   has to satisfy.
   - **Stop and flag** if the issue contradicts a spec page. Don't pick a
     winner. Comment on the issue saying what disagrees, and either wait or work
     something else.
   - **Stop and flag** if the issue asks for UI and there is no agreed wireframe
     in `docs/specs/ui/`. That's a `type:wireframe` issue that hasn't happened
     yet — see [UI design process](ui-design-process.md).
3. **Branch.** Off current `main`.
4. **Write the failing test.** Run it. Watch it fail. Check it failed for the
   reason you meant.
5. **Make it pass**, smallest thing first, then refactor with the suite green.
6. **Reconcile the docs — in this PR.** If behaviour the docs describe has
   changed, the docs change now, not in a follow-up. A contract that lags the
   code by three PRs isn't a contract.
7. **Run everything you can.** The Core suite, `mkdocs build --strict`, the
   geometry check. EditMode goes to CI ([Testing](testing.md#known-limitation-editmode-tests-run-in-ci-not-in-agent-environments)).
8. **Open the PR**, with:
   - a **Conventional Commit title** — it becomes the squash commit;
   - the **plain-English lead** (below);
   - a **`**Docs:**` line**, always, `None` included. CI checks that a docs
     file changed; the line is how a reader learns *what* changed in it, which
     the gate cannot see ([CI/CD](ci-cd.md#the-reconciliation-gate-always-on));
   - a **closing keyword** — `Closes #12` — in the body, not just a mention.
     The pipeline reads issue state from GitHub, so an issue left open after its
     work merged is an issue that gets handed out again;
   - **`## Deviations and Decisions`** (below);
   - **spec invariants** and **how the spec is changing**, where they apply.
9. **Watch CI.** Pushing isn't finishing. Red is yours until it's green or
   you've said in the thread why it isn't yours.

### Rebase, don't merge

Drift is resolved by rebasing onto `main` and force-pushing with
`--force-with-lease`. No merge commits on a feature branch. `main` stays a flat
list of squashed changes, one per issue.

## The plain-English lead

**Every PR body opens with two or three sentences saying what changed and why,
in language Connor could follow.** Before any technical detail. No file list, no
implementation tour, no "refactored `Lane` to extract an interface".

> A wrong answer while your frog is still on the Start log now leaves it where
> it is, instead of sliding it off the bottom of the lane. The Start log is the
> floor of a lane, so the worst a wrong answer can do down there is nothing.

### Why this and not a summary of the diff

Because **a wrong plan has to be catchable on a skim.**

The most expensive failure in this workflow isn't a bug — tests catch bugs. It's
a change that is correct, tested, well-written, and *not what anyone wanted*.
The only defence is a reviewer noticing the mismatch quickly, and a reviewer can
only notice it if the top of the PR states the intent in terms they can check
against what they wanted.

A technical summary can't do this. It describes what the code does, which is
what the diff already says, and it agrees with a wrong plan just as fluently as
with a right one. Two sentences of plain English are the only part of a PR where
"that's not what I asked for" is visible in five seconds.

It has a second effect: an agent that cannot write the lead usually doesn't
understand the issue yet. That's worth finding out before the code is written,
not after.

### Triage hand-backs open with it too

The rule is not PR-specific. **A `triage-issue` hand-back comment opens with the
same two or three sentences**, before any diagnosis, plan, or file detail.

The argument is if anything stronger there. A PR's wrong plan has already cost
the work; a triage hand-back is the moment *before* anything is built, and
`/approve` on a plan nobody skim-checked is how a whole issue gets built wrong.
The lead is what makes "that's not what I meant" visible in five seconds,
which is the only speed at which it reliably gets said.

## Specs state rules, not only outcomes

A spec page that only lists outcomes can be satisfied by code that is wrong in
every case nobody listed. So spec pages state **invariants** — things that are
always true — and PRs point at the ones they had to keep.

### The convention

Invariants are stated as their own line, marked, so they can be quoted and
found:

```markdown
**Invariant:** a frog never moves below the Start log.
**Invariant:** a turn moves one frog, by exactly one position.
```

### Worked example

An issue says: *"A wrong answer moves the frog back one lily pad."*

Outcome-only, the spec would say exactly that, and this implementation
satisfies it:

```csharp
if (!correct) position -= 1;
```

…which is wrong on the Start log, where `position` becomes `-1` and the frog
drops off the bottom of its lane. The outcome says nothing about the bottom of
a lane, so nothing catches it.

With invariants, the spec says:

> **Invariant:** a frog never moves below the Start log — a wrong answer there
> leaves it where it is.
> **Invariant:** a turn moves one frog, by exactly one position.

Now the boundary cases are specified, the tests write themselves, and the PR
can say:

> **Spec invariants kept:** a wrong answer on the Start log moves nothing
> (`WrongAnswerOnStartLogStaysPut`), and no turn moves a frog by more than one
> position (`TurnMovesExactlyOnePosition`).

Naming the test next to the invariant is what makes the claim checkable. "I kept
the invariants" is not a claim; "this test asserts it" is.

## How the spec is changing

When a change **moves the contract** — the old rule was X, the new rule is Y —
the PR body carries a blockquote note saying so:

> **How the spec is changing:** the spec used to leave the top of a lane open —
> seven lily pads, and "first one to the end wins". From this change a lane is
> nine positions and a frog wins by *landing on* the End log, because the Start
> log is already a real space a frog sits on and both ends of a lane should
> behave the same way.

Three sentences: what the old rule was, what the new one is, why the new one is
better.

### Why it isn't covered by the other three things

| | Answers | Doesn't answer |
| --- | --- | --- |
| The diff | what the text now says | what it said before, or why |
| `**Docs:**` line | which pages changed, and what changed in them | why the old rule was wrong |
| Deviations and Decisions | where you departed from the issue | a change the issue *asked* for |
| **How the spec is changing** | old rule → new rule, and why | — |

The gap is real: a PR can implement exactly what the issue asked for, update
exactly the right doc page, deviate from nothing at all, and still quietly
replace a rule the rest of the game depends on. The diff shows the new sentence.
Nothing else shows that a rule was *replaced* rather than *added*, or what the
reasoning was.

Omit the note when the change adds new specification rather than altering it.
Adding a rule about a thing that had no rule is not a spec change in this sense.

## PR reflection: `## Deviations and Decisions`

A section in every PR body. `None.` is a perfectly good entry, and the most
common correct one.

### The materiality test

An item belongs if a reviewer who knew about it might have asked for something
different. That's the whole test. Concretely:

- You did something the issue didn't ask for, or didn't do something it did.
- You chose between two reasonable approaches and the other one was defensible.
- You left something out, or stubbed it.
- You used an escape hatch — `skip-docs`, a geometry exemption, a skipped test.
- You found the issue was wrong and worked to your own interpretation.
- You opened a `Human` issue because part of it needed a
  human.

### The norm is zero to two items

If a PR routinely has six, one of two things is happening: the issues are
underspecified, or the section is being used as a changelog. Both are worth
fixing at the source.

Long lists don't just waste time — they hide. A reviewer who skims six routine
items skims the seventh, and the seventh was the one that mattered.

### What does not belong

- **"Could not run EditMode tests locally."** Expected, documented, correct
  ([Testing](testing.md#this-is-not-a-reportable-deviation)).
- **Restating what the PR does.** That's the lead and the diff.
- **Ordinary implementation choices** with no defensible alternative — a
  variable name, a private helper, which of two identical loops you wrote.
- **Anything already stated as "how the spec is changing."** Don't say it twice;
  the reader will look for a difference between the two and there isn't one.
- **Apologies, or hedging about quality.** If something isn't good enough to
  merge, don't open the PR. If it is, say what you did and stop.
