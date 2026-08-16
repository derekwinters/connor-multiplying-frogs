# CLAUDE.md

Read this first, every session. These are the durable rules for working on
Multiplying Frogs — a game Derek is building with his son Connor. They exist so
they don't have to be re-derived (or quietly dropped) on each run.

If a rule here conflicts with something you inferred from the code, the rule
wins. If a rule here conflicts with an explicit instruction from Derek in the
issue you're working on, Derek wins — and say so in the PR.

---

## The contract

**`/docs` is the design contract.** It is the single description of what this
game is and how it is built. Code that disagrees with `/docs` is a bug in one of
them, and which one is a decision, not an assumption. Never "fix" the code to
match a doc, or a doc to match the code, without saying in the PR which one you
treated as authoritative and why.

**GitHub issues are the checklist.** `/docs` says what the game *is*; issues say
what is left to *do*. An issue's build checklist is the acceptance criteria —
if you cannot tick every box, the issue is not done, and you say which boxes are
untickable and why rather than ticking them optimistically.

**Work one issue at a time, in the focus milestone.** Pick a single issue,
finish it, open its PR, and stop. Do not bundle "while I was in there" changes
from other issues into the same PR — a PR that closes one issue is reviewable; a
PR that closes four is not. The focus milestone is the lowest-numbered open
version milestone unless the dashboard says otherwise; read milestones live from
the API rather than from any list in the docs.

See [`docs/engineering/agent-workflow.md`](docs/engineering/agent-workflow.md)
and [`docs/engineering/issue-pipeline.md`](docs/engineering/issue-pipeline.md).

---

## Tasks that need Derek or Connor

Some work cannot be finished by an agent: repo-admin settings, secrets and
credentials, external accounts, purchases, anything needing a physical device or
a Unity Editor session, and any taste call that is Connor's to make.

When you hit one of these, **do not** guess, stub it out silently, or park the
whole issue. Open **one small issue per task** in the
`Human` milestone, describing exactly what the human has to
do and what unblocks once they've done it. Then finish everything in your
current issue that doesn't depend on it, and say in the PR what you left out.

One task per issue. A single issue titled "various setup Derek needs to do" is
an issue that never gets done.

---

## The non-negotiable rules

### 1. Strict TDD — the test fails first

Write the failing test, watch it fail for the right reason, then write the
smallest code that passes it. Not "write the code and add a test after". If you
did not see red, you do not know the test tests anything.

Every behaviour change lands with a test that would have failed before it. Bug
fixes start with a test that reproduces the bug.

→ [`docs/engineering/testing.md`](docs/engineering/testing.md)

### 2. The Core/Unity split — game logic never touches UnityEngine

All game logic lives in the engine-free `Core` assembly: plain C#, no
`using UnityEngine`, no `MonoBehaviour`, no coroutines, no `Time.deltaTime`
reached for directly. Unity code is a thin shell that reads input, calls into
Core, and draws the result.

This is what makes the logic testable in a plain NUnit run in a couple of
seconds instead of a Unity EditMode run in a couple of minutes. Putting one
`Vector3` in Core to save five minutes costs that back on every CI run
afterwards.

If you genuinely need engine behaviour in Core, the answer is an interface Core
owns and the Unity layer implements — not a reference to UnityEngine.

→ [`docs/engineering/tech-stack.md`](docs/engineering/tech-stack.md)

### 3. Conventional Commits, always

Every commit and every PR title follows
[Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`,
`docs:`, `test:`, `refactor:`, `chore:`, `ci:`, `build:`, with an optional
scope — `feat(frogs): …`.

This is not style. release-please derives the next version and the changelog
from these messages, so a mislabelled commit ships the wrong version number. A
breaking change is `feat!:` or a `BREAKING CHANGE:` footer, and on a
pre-1.0 project you should be very sure before writing either.

→ [`docs/engineering/versioning.md`](docs/engineering/versioning.md)

### 4. `/VERSION` is release-please's, not yours

`/VERSION` at the repo root is the single source of the app's version. It is
moved by release-please, from the conventional commits, and by nothing else.

Never hand-edit it. Never hard-code a version string anywhere else — the Unity
`PlayerSettings` version and the Android `versionCode` are both derived from
`/VERSION` at build time. If you need the version at runtime, read it from the
one place it lives.

→ [`docs/engineering/versioning.md`](docs/engineering/versioning.md)

### 5. Every PR has a `## Deviations and Decisions` section

If you did anything the issue didn't literally ask for, or chose between two
reasonable options, or left something out — it goes in a
`## Deviations and Decisions` section in the PR body. One bullet each: what you
did, and why.

An empty section is a fine outcome; write "None." Silently deviating is not,
because the review is where a wrong call gets caught cheaply, and a wrong call
nobody mentioned is one nobody looks for.

→ [`docs/engineering/agent-workflow.md`](docs/engineering/agent-workflow.md)

### 6. Never guess at Unity serialization

Unity's serializer has rules that are not intuitive and not inferable from the
C# — what survives a domain reload, what a `[SerializeField]` on a private
field does, why a `Dictionary` silently doesn't serialize, when
`[SerializeReference]` is required, how prefab overrides interact with defaults.

If you are unsure how a field will serialize, **look it up or ask** — do not
reason it out from first principles and ship it. Wrong guesses here produce data
loss that shows up days later as "the level forgot its frogs", and they are
miserable to debug.

→ [`docs/engineering/unity-serialization.md`](docs/engineering/unity-serialization.md)

### 7. Rebase, don't merge

Branches rebase onto `main`. Do not merge `main` into your branch to resolve
drift, and do not create merge commits on a feature branch. History on `main` is
a readable list of squashed changes, and it stays that way.

If a PR has conflicts, rebase it, re-run the tests, and force-push with
`--force-with-lease`.

→ [`docs/engineering/agent-workflow.md`](docs/engineering/agent-workflow.md)

### 8. Wireframe before UI code

No UI code is written before there is an agreed wireframe for it. UI is the part
Connor has the strongest opinions about and the part that is most expensive to
redo, so the layout gets settled as a cheap picture first.

**Every layout and every dialog gets its own wireframe.** Not only the big
screens — a confirm box, a toast, an end-of-turn panel each get one. A dialog
is the easiest thing to slip in without a wireframe, and being small, the
easiest to get wrong in a way nobody notices until Connor is holding it.

**Mockups are drawn at 1920 × 1200, landscape.** That is the target device, a
kids' Android tablet, and it is the one canvas every layout is designed
against. A mockup at some other size is a mockup of a screen nobody has.

A UI issue starts as a `type:wireframe` issue with a mockup in
`docs/specs/ui/`. Only once that is agreed does an implementation issue get
opened against it. Finding yourself writing a layout without a wireframe to
point at means you are working on the wrong issue.

→ [`docs/engineering/ui-design-process.md`](docs/engineering/ui-design-process.md)

### 9. Docs reconciliation and the `**Docs:**` line

If a change alters behaviour the docs describe, the docs change in the **same
PR**. Not a follow-up issue — the same PR. A design contract that lags the code
by three PRs is not a contract.

Every PR body carries a `**Docs:**` line stating what happened:

```
**Docs:** docs/specs/ui/game-board.md — a lane is now nine positions, not eight.
**Docs:** None — no behaviour the docs describe was changed.
```

**CI enforces the docs change, not the line.** A PR that touches code and
nothing under `docs/` fails the reconciliation gate. The `skip-docs` label is
the escape hatch for the genuinely doc-irrelevant PR — adding it re-runs the
check — and using it is a decision you justify in
`## Deviations and Decisions`.

The line is still required, because the gate can only see *that* a docs file
changed. Only you can say what changed in it.

→ [`docs/engineering/ci-cd.md`](docs/engineering/ci-cd.md)

### 10. Closing keywords in the PR body

Every PR closes its issue with a real keyword — `Closes #12`, `Fixes #12` — in
the PR **body**, not just a mention. The pipeline reads the issue's state from
GitHub, so an issue that stays open after its work merged is an issue the
nightly builder will hand to somebody again.

One issue per PR, so one closing keyword per PR.

→ [`docs/engineering/issue-pipeline.md`](docs/engineering/issue-pipeline.md)

### 11. Blockers are native GitHub blockers

When issue A cannot start until issue B is done, record it as a **native GitHub
blocked-by relationship**, not as a sentence in the body that only a human
reading the issue will notice. The nightly builder computes the ready queue from
the real dependency graph; a dependency it cannot see is a dependency it will
walk straight into.

If you discover a dependency while working, add the relationship there and then,
even if it isn't your issue.

→ [`docs/engineering/issue-pipeline.md`](docs/engineering/issue-pipeline.md)

### 12. Write the PR for the person reading it

Every PR body opens with a **plain-English lead**: two or three sentences saying
what changed and why, in language Connor could follow. No file lists, no
implementation tour.

Then, when the change touches specified behaviour, two things the reviewer
cannot get from the diff:

- **Spec invariants** — which of the rules in `/docs` this change had to keep
  true, and how you know it did.
- **How the spec is changing** — if the change moves the contract, say what the
  old rule was, what the new rule is, and why the new one is better. "The docs
  were updated" is not this.

→ [`docs/engineering/agent-workflow.md`](docs/engineering/agent-workflow.md)

---

## What not to do

This is a game an eight-year-old is helping build, played by him and by people
he shows it to. That rules some very ordinary game-development things out
entirely.

- **No monetization of any kind.** No in-app purchases, no premium currency, no
  paid unlocks, no "watch to continue".
- **No ads.** No ad SDKs, no ad-adjacent dependencies, nothing that renders a
  third-party banner.
- **No accounts, no sign-in, no player identity.** The game does not know who is
  playing and does not need to.
- **No network calls, no telemetry, no analytics.** The app is fully offline.
  There is no crash reporter, no event pipeline, no "anonymous usage stats". If
  a package wants network permission, that package is the wrong package.
- **No third-party SDKs** without an explicit decision from Derek, for the same
  reasons.
- **Don't invent game mechanics.** If `/docs` doesn't specify how something
  behaves, that is not an invitation to design it. Open a `type:question` issue
  describing the choice and what the options are, and let Connor decide. Guessing
  at a mechanic and shipping it means the game drifts away from being *his*
  game, which is the entire point of building it this way.
- **Don't add dependencies casually.** Every package is something that has to be
  understood, updated, and eventually removed. Prefer writing thirty lines.
- **Don't touch generated or vendored files by hand** — `Library/`, build output,
  the release-please manifest, `/VERSION`.

---

## The commands you need

```bash
# The Core suite. No editor, no licence, ~2 seconds. Run this constantly.
dotnet test Tests/Core/Frogs.Core.Tests.csproj

# One test, while you are in a red-green loop.
dotnet test Tests/Core/Frogs.Core.Tests.csproj --filter FullyQualifiedName~AppVersion

# Core has not gained a Unity dependency.
python .github/scripts/check_core_isolation.py

# The docs site still builds the way CI builds it.
mkdocs build --strict
```

All four run in an agent environment. The EditMode suite does not — it needs an
editor, so it runs in CI
([why](docs/engineering/testing.md#known-limitation-editmode-tests-run-in-ci-not-in-agent-environments)).

## Quick reference

| Thing | Where |
| --- | --- |
| What the game is | `docs/specs/` |
| How we build it | `docs/engineering/` |
| Naming, labels, milestones | [`docs/intro/conventions.md`](docs/intro/conventions.md) |
| Game logic | `Assets/Scripts/Core/` — no UnityEngine |
| Unity shell | `Assets/Scripts/Unity/` |
| Core tests | `Tests/Core/` — plain NUnit, fast, no editor |
| Unity tests | `Assets/Tests/EditMode/` — needs the editor, runs in CI |
| The app's version | `/VERSION` — release-please only |

@.claude/ai-sdlc/house-rules.md
