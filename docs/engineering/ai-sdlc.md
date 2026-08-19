# ai-sdlc

Multiplying Frogs used to own every piece of its development tooling: its own gatekeeper, its own
dashboard renderer, its own label sync, its own reconcile sweep. All of it worked, and all of it
existed in near-identical form in the other repositories Derek maintains — which meant a fix to any
of it was a fix in one place and a slow divergence everywhere else.

[ai-sdlc](https://derekwinters.github.io/ai-sdlc/) is that tooling, extracted once, specified, and
tested. This repository is a consumer of it.

## What lives where now

The logic lives in ai-sdlc. This repository holds only the parts that genuinely differ:

| Here | Why it cannot be centralised |
| --- | --- |
| `.ai-sdlc/repo-config.yml` | What *this* repository is — its capabilities, owners, dashboard, commands |
| `.ai-sdlc/ai-sdlc.pin` | The version of ai-sdlc being followed |
| `.ai-sdlc/house-rules.md` | The shared rules, installed here so agents read them without a network |
| `.ai-sdlc/adoption.md` | Generated from `repo-config.yml`: what is installed here, and at what version |
| `.claude/skills/ai-sdlc/SKILL.md` | The discovery surface, at the path Claude Code requires |
| `.github/workflows/*.yml` callers | A trigger must be declared in the repository it fires for |

A caller is about fifteen lines: a trigger and a `uses:`. Everything it calls is in ai-sdlc, at the
pinned version.

Everything but the last two lives under `.ai-sdlc/`, which is ai-sdlc's own name. Until v0.4.18 it
was all under `.claude/`, and that was wrong in a way worth stating: a GitHub Actions job parsing
`capabilities` and `owners` has nothing to do with an AI coding assistant, and squatting in a
vendor namespace took a dependency on somebody else's naming for nothing. `.claude/skills/` is the
one exception, because Claude Code requires that path.

`adopt apply` did the move, in the same run as the v0.4.18 upgrade: `repo-config.yml` moved
byte-for-byte with its comments intact, the old copies went, and the import line at the bottom of
`CLAUDE.md` was repointed.

### Two kinds of caller

Since v0.4.19 the callers here come in two shapes, and the difference is worth knowing before
editing one:

| Shape | Which | What runs |
| --- | --- | --- |
| **Action** | `closing-keyword.yml`, `docs-gate.yml`, and the four gatekeeper callers | One `uses:` step. GitHub fetches the action into the runner's own directory; **nothing of ai-sdlc's enters this repository's workspace**. |
| **Reusable workflow** | `dashboard.yml`, `labels-sync.yml`, `skills-update.yml`, `docs-build.yml` | The called workflow checks ai-sdlc out into `.ai-sdlc-checkout/` and runs a script from it. |

The action shape is where the rest are going. It removes a whole class of problem rather than
avoiding it: `actions/checkout` empties the directory it writes into, so anything ai-sdlc fetches
into this workspace is one naming collision away from replacing a file we committed — which is
exactly why the checkout path had to be renamed away from `.ai-sdlc` when the configuration moved
there. A run that fetches nothing cannot collide with anything.

An action caller also carries **one** reference rather than a `uses:` and a matching `ref:`, so
there is no pair to keep in step.

**A converted caller's status check is renamed**, because GitHub names a reusable workflow's check
`<workflow> / <job>` and an action's `<job>`. That is a branch-protection change and only Derek can
make it — #383. It affects only checks that gate a pull request, so the gatekeeper callers — which
run on issues — are unaffected.

### The gatekeeper is one action, and every writer on an issue now queues

Four callers — `gatekeeper-comment`, `gatekeeper-close`, `triage` and `gatekeeper-sweep` — run the
same action and differ only in the `mode:` they ask for and the trigger that starts them.

Converting them settled something that had been wrong here since triage arrived. `triage.yml` used
to have a concurrency group of its own, so a triage fire could run *at the same time* as a
gatekeeper comment on the same issue. That is not harmless: every label write goes through
`set_labels`, which replaces the whole label set rather than patching it, so two runs on one issue
read-modify-write the same list and one silently loses — whichever label each meant to change.

The gatekeeper's own moves cannot trigger triage, because GitHub starts no run from an event its
token authored. But labelling an issue **by hand** while a `/admit` is in flight could, and
labelling by hand is precisely what the triage caller was added for (ai-sdlc#123).

All three issue-scoped callers now share `gatekeeper-<issue number>`, and none may be cancelled: a
cancelled run leaves the label set half applied. A triage fire may now wait a few seconds behind a
comment on the same issue, which is the trade.

### The two things that point at ai-sdlc

`adoption.md` and the `ai-sdlc` skill are the answer to *how does an agent working here know any of
this*. They are not alternatives:

- The `@.ai-sdlc/house-rules.md` import in `CLAUDE.md` is **always on**. It is read every session,
  which is why it has to stay short, and it is what carries the one sentence saying ai-sdlc governs
  this repository's issues, labels, milestones, triage, gates and releases.
- The `ai-sdlc` skill is loaded **on demand**, so it can afford detail. But loading it is a
  judgement the model makes from its description, and you cannot go looking for a skill about a
  thing you do not know exists — which is exactly the failure here, an agent editing a stale
  pipeline doc with no idea ai-sdlc owns it. Hence the always-on sentence as well.

`adoption.md` is **generated** and holds only resolved state: the pin, the capabilities and profiles
in force, the pipeline-state labels this repository actually uses, the callers installed, the skills
installed, and links into the specification at the exact commit this repository runs. It explains
nothing, deliberately. Every hand-maintained copy of something ai-sdlc generates has drifted here
eventually — four label colours in `conventions.md` disagreed with `labels.core.yml`, and 27
references survived a pipeline state that no longer existed. Do not edit it: change
`repo-config.yml` and run `adopt apply`.

## Capabilities arrive one at a time

ai-sdlc is six capabilities, ordered so each may depend only on those below it:

    substrate → hygiene → consistency → labels → release → pipeline

A repository takes what it wants. `repo-config.yml` lists what is **live here right now**, not what
is intended — `adopt` installs everything a declared capability owns on its next run, so declaring
`pipeline` before the old gatekeeper is gone would put two handlers on one event, racing, both
writing.

The migration is tracked in the **ai-sdlc adoption** milestone, one issue per capability. It is
deliberately not a version milestone: this is infrastructure, and a version number belongs to a
release of the game.

## The pin

`.ai-sdlc/ai-sdlc.pin` records the version **and** the commit it resolves to:

    v0.4.2 b95d6bb30481e24e4b9eb8c6cdfda1a85cdb20d3

The callers reference that commit, with the version as a trailing comment — the same form as every
other pin here, and no exception to [the SHA-pinning rule](ci-cd.md). A reusable workflow runs with
this repository's token, on `issue_comment` and `issues`, so a mutable ref there is the same
exposure as a mutable action; ai-sdlc being ours says who could move a tag, not that it cannot move.

An upgrade is still a pull request that moves one line, because `adopt apply <version>` resolves the
version and rewrites both the SHA and the comment. Nobody resolves a SHA by hand, and nobody has to
read forty characters of hexadecimal to tell how far behind they are.

### Pinning a commit, to try unreleased work

`adopt apply` also takes a **bare commit SHA**, and then the pin records that commit twice:

    86edeee56e7f976a9c1ab85f3038984eabcc8a51 86edeee56e7f976a9c1ab85f3038984eabcc8a51

This is how a change is tried here before it has a version — which is the normal case, because this
repository is where ai-sdlc gets proven and a release should not be cut for something unproven. The
trade is that the trailing comment on each caller repeats the SHA instead of naming a version, so
`check_action_pins.py` warns that nobody can tell which version it is. That warning is **correct
and expected on a commit pin**: there is no version, and that is the point.

A commit pin is temporary. When the work it is testing is released, move the pin to the version, so
the board reads `v0.5.1` rather than forty characters of hexadecimal.

## The two escape hatches

Both adopted checks can be overridden, and both do it the same way — with a **label that makes the
check pass**, never one that skips it. That distinction is the whole design: a skipped required
check stays pending forever and blocks the merge it was meant to permit, so an `if:` would turn an
escape hatch into a trap.

| Label | Overrides | Use it when |
| --- | --- | --- |
| `no-closing-keyword` | `closing-keyword` | the pull request deliberately closes no issue |
| `skip-docs` | `docs-gate` | the change genuinely needs no documentation |

Adding either label re-runs its check, because `labeled` is among the caller's triggers. Nothing
needs pushing. Using one is a decision, so it belongs in `## Deviations and Decisions`.

Verified end to end when the closing-keyword check was adopted: a pull request with no closing
keyword failed, the label made it pass on a fresh run, and removing the label failed it again.

## What the docs gate does, and does not, replace

The reconciliation gate — *a pull request that changes code must change documentation, or carry
`skip-docs`* — is now ai-sdlc's, called from `docs-gate.yml`.

`docs-test.yml` stays, because the shared profile does not cover everything frogs had:

| | Where it runs now |
| --- | --- |
| docs reconciliation | `docs-gate.yml` — ai-sdlc's shared action |
| `mkdocs build --strict` | `docs-test.yml`, still ours |
| the CI scripts' own tests, and the action-pin check | `docs-test.yml`, still ours |

ai-sdlc's mkdocs profile does have a build-and-publish workflow, but it publishes through GitHub
Pages, and frogs publishes with `mike` to `gh-pages` to get the version selector. Replacing a
working publisher with a differently-shaped one is not a migration, so `docs-publish.yml` is
untouched.

One behavioural difference worth knowing: frogs' gate re-read the pull request's live labels during
a grace window, to survive `skip-docs` being added while the check was running. The shared gate
relies on the `labeled` trigger instead — adding the label starts a fresh run. Same outcome, fewer
moving parts, and `test_docs_gate_triggers.py` still guards the trigger because that is the half
that cannot be centralised.

## What the pipeline replaced

The whole issue lifecycle now runs from ai-sdlc. What used to live here, and where it went:

| Was here | Now |
| --- | --- |
| `gatekeeper-comment.yml` + `pipeline-gatekeeper` | `gatekeeper-comment.yml`, a caller |
| — | `gatekeeper-close.yml`, a caller — frogs had no equivalent |
| `dashboard.yml` + `pipeline-dashboard` | `dashboard.yml`, a caller |
| `gatekeeper-sweep.yml` + `pipeline-reconcile` | `gatekeeper-sweep.yml`, a caller — same name, different job |
| `pipeline-analysis`, `pipeline-dev`, `triage-issue`, `ci-watch`, `milestone-ops`, `issue-blockers`, `release-flow` | named in `repo-config.yml`, installed by `skills-update.yml` — see [How the skills get here](#how-the-skills-get-here) |
| `pipeline-tests.yml` | gone with the code it tested |

### How the skills get here

The workflows arrived; the skills did not. `.claude/skills/` holds this repository's own —
`core-unity-split`, `dw-run-tests`, `grilling` and the rest — and nothing from ai-sdlc. The row
above used to say they were installed with `gh skill`, and that was a description of the intended
end state written in the present tense, which is the same failure as a specified-but-uncalled
function: it reads as covered, so nobody checks.

Nothing had ever run `gh skill install` for a consumer. The mechanism was settled in ai-sdlc's
`docs/design.md` §7 from the beginning and no job invoked it (ai-sdlc#144).

What installs them is `skills-update.yml`, an ai-sdlc caller that runs on a schedule, installs what
`skills:` in `repo-config.yml` names, and opens a **pull request** — never a direct commit, because
an installed skill is an instruction an agent reads and a timer that put unreviewed ones into its
context would be a consent problem rather than an untidiness. A skill edited here is reported and
left alone, never overwritten.

It arrived with v0.4.17, and `repo-config.yml` now names six: `triage-issue`, `pipeline-dev`,
`ci-watch`, `milestone-ops`, `issue-blockers` and `release-flow`. The list is not every skill —
`pipeline-gatekeeper`, `pipeline-dashboard`, `label-sync`, `closing-keyword` and `docs-gate` only
ever execute from ai-sdlc's own checkout inside a reusable workflow, so a copy here would be a
second version to keep at the pin that nothing reads.

`adopt` is absent despite the upgrade command below naming it: the skill imports `lib.config`,
which is not part of the skill, so an installed copy raises `ModuleNotFoundError`. Upgrades are run
from an ai-sdlc checkout until that is fixed. `issue-blockers` has the same defect and is listed
anyway, because it is genuinely one of ours and the fix belongs upstream — both are ai-sdlc#149.

One repository setting is still needed before the job can finish: Actions must be allowed to open
pull requests (#378).

### The sweep is back, for a different reason

frogs' original sweep existed because frogs **stored** blockedness as pipeline state, and stored
state goes stale. ai-sdlc derives blockedness at selection time from the live dependency graph
instead. State computed when it is used cannot drift, so that sweep was not dropped — its cause
was.

v0.4.17 brought a different one. Firing triage is a poke, and a poke can be lost: a routine that
never starts, starts and dies, or is refused leaves the issue exactly as it was, and nothing looks
at it again. Eight issues sat stranded here overnight that way (#373). `gatekeeper-sweep.yml` runs
hourly and moves an issue whose session never answered to `ai-triage-stalled`.

It **starts no sessions**. A scheduled job that can start them can spend the account's usage limits
while nobody is watching, so the sweep only writes labels — whatever it gets wrong, it gets wrong
cheaply. Deciding another session is worth spending is `/admit`.

Genuine drift — a closed issue still carrying pipeline labels — is handled by the close handler, and
anything left is **reported on the dashboard rather than silently repaired**. That is the whole
bargain: nothing fixes the board behind your back, so the board has to show you what is wrong.

## Upgrading, and checking for drift

```bash
python3 .claude/skills/adopt/main.py plan   <pin>   # read-only: what would change
python3 .claude/skills/adopt/main.py apply  <pin>   # writes, on a branch
python3 .claude/skills/adopt/main.py verify <pin>   # read-only: are we still current
```

`apply` is both the install and the upgrade path, so the upgrade cannot rot separately from the
install.

Every file `adopt` writes carries a provenance header naming the source, the ref, and a content
hash. That is how it tells a file of its own from one of ours, and a locally-edited managed file
from a merely outdated one. **An edit is a conflict, not a stale file** — it is reported and left
alone, never overwritten, because overwriting it would discard the edit silently. If you need to
change a managed file, change it in ai-sdlc and move the pin.
