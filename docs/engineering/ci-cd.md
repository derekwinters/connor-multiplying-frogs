# CI/CD

Every workflow in this repo, what it guards, and what to do when it goes red.

The rule underneath all of it: **a check exists to stop a specific bad thing
from reaching `main`.** If you cannot say what a check prevents, it should not
be blocking; if you can, the fix is to satisfy it, not to route around it.

## The workflows at a glance

| Workflow | Trigger | Blocking | Guards |
| --- | --- | --- | --- |
| `pr-title-lint` | PR opened/edited | yes | the squash commit message, and therefore the changelog |
| `ci-tests` | PR, push to `main` | yes | game logic and scene wiring |
| `docs-test` | PR, push to `main` | yes | the docs site builds, and the docs match the change |
| `pr-build` | PR | no | the app still compiles into an installable APK |
| `rc-build` | manual, tag | no | a release candidate someone can actually play |
| `release-please` | push to `main` | no | the version, changelog, tag, release, and release APK |
| `labels-sync` | push to `main` touching `labels.yml` | no | the label taxonomy matches the file |
| `geometry-lint` | PR touching `Assets/Scripts/**` | yes | the named-values rule, ratcheting |
| `pipeline-*` | schedule, issue comments | no | the issue pipeline itself |

## Every action is pinned to a commit SHA

**Repository policy: every `uses:` is a full 40-character commit SHA. Never a
tag, never a branch.** A workflow written as `actions/checkout@v4` is rejected.

```yaml
# No — a tag is a mutable pointer.
- uses: actions/checkout@v7

# Yes.
- uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

A tag is a label the action's owner can move. Pinning to one means every
workflow run fetches whatever that label points at *today*, and a compromised or
merely careless upstream re-tag runs with this repository's `GITHUB_TOKEN`. A
commit SHA cannot be moved, so what ran yesterday is what runs tomorrow.

### The trailing comment is required

`# v7.0.1` after the SHA. Not optional, and not decoration — a hex string tells
a reviewer nothing, so without it nobody can tell a two-year-old pin from
yesterday's, and "bump the actions" becomes a research project.

The comment is a claim about the SHA, so it has to be true. Re-resolve rather
than editing the version comment and hoping.

### Resolving a tag to its SHA

```bash
.github/scripts/resolve_action_pin.sh actions/checkout v7.0.1
# → actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

It prints the line to paste. Use it rather than reading a SHA off the GitHub UI,
because of one trap: an **annotated** tag's ref points at a tag object, not a
commit, so `git ls-remote refs/tags/v1.2.3` can hand you a SHA that resolves to
nothing when a workflow tries to check it out. The script asks for the peeled
ref (`refs/tags/v1.2.3^{}`) first and falls back to the plain ref for
lightweight tags.

**Re-resolve on every bump.** A SHA is never inferable from a version number —
not by incrementing, not by analogy with another action, not by remembering. If
you are changing the version comment, you are re-running the script.

### The same rule applies to reusable workflows

A `uses:` that points at a workflow file is pinned identically:

```yaml
jobs:
  build:
    uses: derekwinters/connor-multiplying-frogs/.github/workflows/thing.yml@<sha> # v1.0.0
```

A reusable workflow runs with the caller's permissions, so it is the same
exposure as an action — and the same rule.

**Workflows in this repository are referenced by local path** —
`uses: ./.github/workflows/release-build.yml` — which is pinned by
construction: it resolves to the calling workflow's own commit. That is
stricter than a SHA, because there is no pin anyone has to remember to update
and no way for the two to drift apart.

### The actions this project uses

| Action | Pin | Used by |
| --- | --- | --- |
| `actions/checkout` | `3d3c42e5aac5ba805825da76410c181273ba90b1` | everything |
| `actions/setup-python` | `5fda3b95a4ea91299a34e894583c3862153e4b97` | docs, pipeline, scripts |
| `actions/setup-dotnet` | `a98b56852c35b8e3190ac28c8c2271da59106c68` | the Core suite |
| `actions/upload-artifact` | `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` | APKs, test results |
| `actions/cache` | `55cc8345863c7cc4c66a329aec7e433d2d1c52a9` | Unity's `Library/` |
| `actions/download-artifact` | `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c` | the release attach step |
| `googleapis/release-please-action` | `45996ed1f6d02564a971a2fa1b5860e934307cf7` | `release-please` |
| `game-ci/unity-builder` | `d829bfc901f2347c8fe18898f06712b66916ef42` | APK builds |
| `game-ci/unity-test-runner` | `0ff419b913a3630032cbe0de48a0099b5a9f0ed9` | the EditMode suite |

`docs-publish` uses no Pages actions: `mike` commits the built site to
`gh-pages` itself, which is also what makes the version selector work.

**Adding an action that isn't on this list is a decision, not a detail.** Every
one is third-party code running with a token; the bar is "there is no reasonable
way to do this with `run:` and the GitHub CLI". Several things that would
normally be an action — creating a release, uploading an asset, posting a
comment — are `gh` calls here for exactly that reason.

If a new action is genuinely needed: check it is permitted by the repository's
Actions policy (a denied action fails the run with a policy message, which is at
least a loud failure), pin it, add it to this table, and say why in the PR.

### Keeping pins fresh

A pin that never moves is a pin that misses security fixes — the failure mode
this convention trades *into*. Dependabot watches `github-actions` and opens a
PR per action when a new version appears, updating the SHA **and** the trailing
comment together.

That is the intended way pins move. Treat those PRs as real changes — read the
upstream release notes, don't merge on autopilot — but do not let them sit. A
year of ignored Dependabot PRs is worse than no pinning at all, because it looks
deliberate.

## Build workflows

### `pr-build` — a debug APK for every PR

Every PR produces an installable debug APK, so "does it still build" is answered
by a build rather than by hope, and so Connor can try a change before it merges.

- **Debug signing**, with the Android debug keystore. No secrets, so the
  workflow runs on any PR without special permissions.
- **The commit SHA is in the version name** — `0.2.3-abc1234`. A phone with four
  test builds on it needs to say which is which, and "the one from Tuesday" is
  not an answer.
- **`.debug` applicationId suffix**, so a debug build installs *alongside* the
  release build instead of replacing it. Connor keeps the game he plays and the
  build being tested at the same time.
- **Artifact-only distribution.** The APK is a workflow artifact on the PR.
  Nothing is uploaded anywhere, no store, no distribution service, no link that
  outlives the PR. Artifacts expire and that is correct — a stale test build is
  worse than none.

#### A missing licence warns and skips here

The opposite of [`ci-tests`](#ci-tests-the-two-suites), deliberately.

`ci-tests` is a **correctness gate**: a green tick claiming the tests ran when
they did not is a lie, so a missing licence fails it. `pr-build` is a
**convenience**: no APK is an absent convenience, not a false claim. Failing
every PR over it would train everyone to ignore a red check, which costs more
than the missing build.

The workflow says so in a `::warning::` and in the step summary, so the absence
is visible rather than silent.

#### Two details that would be bugs if got wrong

- **`fetch-depth: 0`.** The `versionCode` is the commit count, so the whole
  history has to be present. A shallow checkout silently produces a *lower*
  number than the previous build, and Android refuses to install that — with an
  error that says nothing about depth.
- **The sha comes from `pull_request.head.sha`, not `github.sha`.** The latter
  is the ephemeral merge commit, which nobody can check out later; a version
  name pointing at it identifies nothing.

The version name, `versionCode`, and `.debug` suffix are all applied by
`BuildStampPreprocessor`, a build pre-processor rather than a step CI remembers
to call — a stamping step that can be forgotten will be, and a build with the
wrong version cannot be identified afterwards.

Red here almost always means a compile error the Core suite couldn't catch,
because it lives in the Unity layer. Read the build log, not the test log.

### `rc-build` — release candidates

An RC is a build of what is *about* to be released, produced so someone can play
it before the release is real.

- **Triggered by a push to the open release PR's branch**
  (`release-please--branches--main`), so every change to what is about to ship
  produces something playable. Also `workflow_dispatch`.
- **The version comes from `/VERSION` on that branch**, which release-please has
  already bumped to the version about to ship — exactly the version an RC should
  carry.
- **Same debug signing and `.debug` suffix as a PR build.** An RC is for trying
  the game; release signing needs a keystore secret this workflow should not be
  able to reach.
- **Artifacts only.** An RC is not a release: no tag, no GitHub release.

#### The `rcN` number is derived, not chosen

`N` is the number of `rc-build` runs on the release branch **since that PR
opened**. Nobody has to remember where they got to, and two pushes cannot both
claim `rc3`.

Counting from the PR's open time is what makes a fresh release PR restart at
`rc1` instead of continuing the last release's numbering — the release branch
itself is rewritten on every release, so there is nothing on it to count.

release-please's own prerelease support cannot do this: it bumps on *releases*,
not on pushes to an open PR.

`.github/scripts/next_rc_number.py` does the arithmetic, from a snapshot of the
workflow's runs, and it handles the two cases that would otherwise produce two
artifacts claiming the same number:

- **A re-run of an older build** counts only runs at or before itself, so it
  cannot renumber above a newer sibling.
- **A run that cannot see itself yet** — the Actions API lags — counts what is
  there and adds one, rather than reporting a number already taken.

### `release-please` — the version, the tag, and the release

Runs on every push to `main`, plus `workflow_dispatch`. Two outcomes:

- **Usually**: it rewrites the open release PR with the pending changelog and
  the next version, and stops. The PR title is `chore(main): release X.Y.Z`.
- **When that PR merges**: it applies the version bump, creates the tag and the
  GitHub release, and sets `release_created: true`.

Downstream jobs gate on the workflow's outputs:

| Output | Is |
| --- | --- |
| `release_created` | `true` only on the run that created a release |
| `tag_name` | e.g. `v0.2.0` |
| `version` | e.g. `0.2.0` |

Two details worth knowing if you touch it:

- **It reads each output twice** — `release_created` and `.--release_created`.
  release-please emits top-level outputs for a single package and path-prefixed
  ones in manifest mode, and which you get has changed across versions. Reading
  both costs nothing, and means a version bump can't silently turn every
  downstream `if:` false — which looks identical to "there was nothing to
  release".
- **`cancel-in-progress: false`.** Two concurrent runs would both rewrite the
  release branch; cancelling one mid-tag is how a tag ends up existing without
  a release.

Permissions are `contents: write` (the tag, the release, the release branch) and
`pull-requests: write` (the release PR), granted on the job rather than the
workflow. It also needs **"Allow GitHub Actions to create and approve pull
requests"** enabled on the repository, without which it fails on its first
attempt to open the PR.

### Release builds

#### Why not `on: release: published`

Because it would never run.

**GitHub deliberately suppresses workflow runs from events initiated by
`GITHUB_TOKEN`.** The rule exists to stop workflows triggering themselves
forever, and it is unconditional: it does not warn, and it does not appear
anywhere in the Actions UI. release-please creates the release with that token,
so a workflow listening for `release: published` simply never fires. Every
release would sit there with no APK, and the only symptom would be the absence
of a run nobody was watching for.

So the build is called **from inside the run that created the release**, gated
on `release_created`. The token that made the release is the token in the job,
and no event is involved.

The usual workaround — a personal access token or a GitHub App, so the event is
attributed to something other than `GITHUB_TOKEN` — is a credential to store,
rotate, and scope. Calling the workflow directly needs none of that.

#### `release-build.yml`

One reusable workflow, called two ways:

| Called by | When |
| --- | --- |
| `release-please.yml` (`workflow_call`) | in the run that created the release — the normal path |
| a human (`workflow_dispatch`, with a `tag` input) | the backfill, when the attach failed or never ran |

Both paths run the same steps, so the backfill cannot drift from the thing it
backfills. `gh release upload --clobber` means re-running it replaces the asset
rather than failing on "already exists", which is the entire point of a
backfill.

It refuses to attach anything if `/VERSION` at the tag disagrees with the tag
name — attaching an APK to a mislabelled release makes the mislabelling
permanent.

#### Two APKs, in two build paths

| Asset | Profile | For |
| --- | --- | --- |
| `multiplying-frogs-0.2.0.apk` | ARM64, IL2CPP | the phone |
| `multiplying-frogs-0.2.0-emulator.apk` | x86_64, Mono | a desktop emulator |

They are built into `build/device/` and `build/emulator/` rather than a shared
directory. The two have the same version and differ only in architecture, which
is not visible from a filename sitting next to another filename — separate
paths make it impossible for the wrong one to be picked up by a glob.

The profile is applied by `BuildStampPreprocessor` from `FROGS_ANDROID_PROFILE`,
and an unrecognised value fails the build rather than defaulting: guessing here
ships the wrong architecture, which looks fine until someone installs it.

Both are **attached to the GitHub release** rather than left as workflow
artifacts, so the tag and the thing you install are one object. A release whose
APK lives in an expiring artifact is a release you cannot re-install in six
months, which defeats the point of tagging it.

The emulator asset is for *trying* the game on a desktop, not for judging it —
see [Tech stack](tech-stack.md#two-build-profiles).

Neither build carries a `.debug` suffix or a sha in its version name. This is
the app.

## Correctness workflows

### `pr-title-lint`

The PR title becomes the squash commit message, which becomes the changelog
entry, which is what release-please reads to decide the next version. So the
title has to be a valid Conventional Commit before it can merge.

Runs on `opened`, `edited`, `reopened`, and `synchronize`, so a title fixed
after the fact re-checks itself.

`.github/scripts/lint_pr_title.py` checks:

| Rule | Why |
| --- | --- |
| type is one of `feat` `fix` `docs` `test` `refactor` `chore` `ci` `build` | anything else is a type release-please quietly ignores |
| type is lowercase | `Feat:` parses as an unknown type, not as `feat:` |
| scope, if present, is `[a-z0-9-]+` and non-empty | `feat():` is a scope that says nothing |
| there is a subject after `: ` | a bare `feat:` is a changelog entry with no content |
| no trailing full stop | changelog lines don't take one |
| 100 characters or fewer | commitlint's default; stops an essay in the subject line |

Every failure names the fix, and the output ends with *why* the title matters,
because the check's whole job is to be understood by someone who thinks it is
being pedantic.

The `!` breaking-change marker is allowed, and prints a note rather than
failing — pre-1.0 it releases `1.0.0`, which is worth seeing in the log.

Red here is a thirty-second fix: edit the title. Note that the run is
**cancel-in-progress** per PR, so only the newest title is ever reported on.

The script is plain Python with 28 unit tests, run by `pipeline-tests`
alongside the skills:

```bash
python3 .github/scripts/run_python_tests.py scripts
```

### `ci-tests` — the two suites

Two jobs, deliberately separate because they cost wildly different amounts:

| Job | Runs | Needs |
| --- | --- | --- |
| **Core (NUnit)** | `dotnet test` on `Tests/Core` | nothing — seconds |
| **EditMode (Unity)** | headless Unity via GameCI | a licence, and minutes |

See [Testing](testing.md) for what belongs in each.

The Core job also runs `check_core_isolation.py` first. It costs nothing and it
fails for a reason the test output would never explain: game logic that has
grown a dependency on the engine.

#### A missing licence fails, it does not skip

The EditMode job's **first** step, before any checkout or container pull,
asserts that `UNITY_LICENSE` is set — and fails the job if it isn't.

Skipping would be the friendlier behaviour and the wrong one. A required check
that goes green because the suite never ran is worse than no check at all: it
reports "tested" on a PR nothing tested, and nobody looks twice at a green tick.

Until the secret exists (#82), this job is red on every PR that touches the
Unity project. That is the intended behaviour, and it is visible rather than
quiet.

#### Path-gating, and what it means for branch protection

The workflow only runs when `Assets/`, `Tests/`, `ProjectSettings/`,
`Packages/`, or its own files change. Most changes here are docs, skills, and
workflows, and pulling a containerised editor to check a typo helps nobody.

!!! warning "Path-gated workflows and required checks don't mix"
    A path-gated workflow does not report *at all* on a PR that misses its
    paths — it is not skipped, it is absent. If `ci-tests` is marked **required**
    in branch protection, every docs-only PR blocks forever waiting for a check
    that will never run.

    When configuring branch protection (#80), either leave `ci-tests`
    non-required, or add an always-running guard job that reports the required
    name and the real jobs depend on. Don't mark the path-gated jobs required
    and hope.

#### Why the runner step is `continue-on-error`

Because the exit code is not the verdict — see below. The runner is allowed to
fail; the gate step decides, and the results XML is uploaded as an artifact
either way, since a red run is the one you most want the XML for.

#### The results-XML verdict rule

**The exit code is not the verdict.** It is wrong in both directions:

- **Falsely green.** The runner can exit 0 having run *zero* tests — a licence
  problem, a missing assembly, or a filter that matched nothing all look like
  success from the outside. "0 tests passed" is the most dangerous green there
  is.
- **Falsely red.** Unity's editor sometimes dies during *teardown*, after a
  fully green run has written its results. Treating that as a failure produces a
  red PR with nothing wrong in it, and the fix everyone reaches for is "re-run
  until it's green" — which trains people to re-run real failures too.

So `.github/scripts/verify_editmode_results.py` derives the verdict from the
NUnit results XML. It passes only when:

- the results file exists, parses, and is a `<test-run>` document;
- `total` is **greater than zero**;
- `failed` is zero, and nothing is inconclusive;
- every count it needs is actually present — a missing `failed` attribute is a
  failure, not a zero, because otherwise an unreadable file reads as a pass.

Then, and only then, it looks at the exit code:

| Exit code | Green results | Verdict |
| --- | --- | --- |
| `0` | yes | pass |
| `139` (SIGSEGV in teardown) | yes | pass, and says so in the log |
| anything else | yes | **fail** — something went wrong after the tests |
| any, including `139` | no | **fail** |

**Forgiveness is about teardown, never about results.** A failing test fails the
gate whatever the exit code was. Adding a code to the forgiven list needs
evidence — a run with complete, green results and a nonzero exit — and is done
with `--forgive`, so it shows up in the workflow rather than being buried in the
script.

If the XML is missing, the job fails. "We couldn't tell" is a failure, not a
pass — the whole point is to not accept an absence of evidence as evidence.

The script is stdlib-only, so it needs no setup step, and it has 18 unit tests
covering each failure mode.

### `geometry-lint` — the tuning-literal check

The backstop for the named-values rule in [Tech stack](tech-stack.md). A rule
with no backstop decays one literal at a time, each individually defensible.

**What it flags:** f-suffixed float literals of magnitude 3 or more, on a line
that does not give them a name.

```csharp
Place(12f, 40f);                    // flagged
const float PanelWidth = 280f;      // fine — named
[SerializeField] float _gap = 12f;  // fine — named, and tunable
var gap = 18f;                      // fine — named
Fade(0.5f);                         // fine — below the magnitude floor
```

**It is deliberately narrower than the rule.** It does not catch literals inside
a named declaration's initialiser (`var spot = new Vector3(12f, 40f, 0f)` names
the vector, not its components), integer literals, or magnitudes below 3. Those
need a parser, or would produce more false positives than findings — and a check
people learn to override catches nothing at all. **They are gaps in the check,
not permission in the rule**; review catches them.

**Ratcheting baseline.** `.github/geometry_literals_baseline.txt` records how
many literals each file is allowed. A count going *up* fails. A count going down
passes, with a note to run `--update-baseline` so the new lower number becomes
the ceiling — an improvement must never fail a build, or the check gets turned
off.

The ratchet is the whole design. A check demanding a clean codebase before it
could be switched on is a check that never gets switched on; one saying "not
worse than yesterday" can be switched on today and still converges.

Raising the baseline to make the check pass is not a fix, and the script says so
in its own failure output. The workflow is path-gated to include the baseline
file, so a PR that only raises it still runs the check it was trying to silence.

**The checker's tests run in the same job, first.** A gate nobody has tested is
a gate whose verdict means nothing — and a broken checker's dangerous outcome is
the *pass*.

## Docs workflows

### `docs-test`

Two jobs, on **every** pull request with no path filter.

| Job | Does |
| --- | --- |
| `gate-tests` | runs the CI scripts' own unit tests |
| `Docs` | builds the site if docs changed; runs the reconciliation gate if they didn't |

**No path filter, deliberately.** A path-gated workflow is *absent* on PRs that
miss its paths rather than skipped, so it can never report on them — and a
code-only PR is exactly what the reconciliation gate exists to catch. It is
therefore also the one workflow here that is safe to mark required in branch
protection.

When docs changed, it builds with `mkdocs build --strict`, so a broken nav
entry, a dead internal link, or a page missing from the nav fails the PR rather
than shipping a broken site. No deploy — publishing is `docs-publish`'s job.

When they didn't, it runs the gate below. `pull-requests: read` is granted for
the gate's live-label fetch, and nothing more.

`gate-tests` runs on every PR because a gate nobody has tested is a gate whose
verdict means nothing — and a broken gate's dangerous outcome is the *pass*,
which is also the quiet one.

### `docs-publish`

Publishes the site to GitHub Pages with [mike](https://github.com/jimporter/mike),
which keeps one built copy per version in the `gh-pages` branch and drives the
version selector in the header. See
[Conventions](../intro/conventions.md#docs-versioning).

- **Path-gated** to `docs/`, `mkdocs.yml`, and its own file, on pushes to
  `main`, plus any `v*` tag and `workflow_dispatch`.
- **The version comes from `/VERSION`**, marker stripped, so the docs are
  labelled with the same number the app reports rather than a separate one to
  keep in step.
- **`latest` is an alias**, moved with `--update-aliases` on every publish, and
  set as the default so the site root lands there.
- **Serialised and never cancelled.** Two mike runs would both rewrite
  `gh-pages`, and the loser leaves the published site in whatever state it got
  to.

**Never edit `gh-pages` by hand.** mike rewrites it on every publish, so a hand
edit disappears at the next one — without warning, and after looking like it
worked.

#### Between releases, the current version's docs track `main`

`/VERSION` only moves when a release goes out, so a docs change merged after
`0.1.0` shipped republishes under `0.1.0`. The docs for a released version are
therefore the *current* docs for that version, not a snapshot of what shipped
with it.

That is the right trade here: a fix to a confusing page should reach readers
without waiting for a release. When `0.2.0` ships, `0.2.0` is created fresh and
`0.1.0` stops moving — so history is preserved at the granularity of releases,
which is the granularity anyone actually asks about.

### The reconciliation gate — always on

**A PR that changes code changes the docs, in the same PR.** `/docs` is the
design contract, and a contract that lags the code by three PRs is not one.

`.github/scripts/docs_reconciliation_gate.py` decides:

| PR | Verdict |
| --- | --- |
| touches anything under `docs/`, or `CLAUDE.md`, `README.md`, `mkdocs.yml` | pass |
| code only, carrying `skip-docs` | pass |
| code only, no label | **fail** |
| the release PR | pass, always |
| changes nothing | pass |

The gate is **always on**. It does not skip based on which files changed,
because "this PR obviously doesn't affect the docs" is exactly the judgement it
exists to make someone write down.

#### What counts as documentation

Not "any markdown file". A `SKILL.md` is documentation *of* a skill, but it is
also the skill's behaviour — changing it changes what an agent does, so it does
not excuse the docs. Neither does a `.md` file sitting next to some source.

#### The release PR is exempt

It contains the version bump and the changelog release-please generated, and
nothing else. It cannot reconcile docs, and there is nobody driving it to apply
a label. Matched **both** by its head branch and by its `autorelease: pending`
label, so a change to either does not silently start failing every release.

#### The grace poll

A `skip-docs` label applied moments after the PR opens would otherwise produce a
failing run that is already wrong by the time anyone reads it — and a red tick
nobody should act on is worse than a slow one.

So a would-be failure re-reads the PR's **live** labels for up to a minute
(`GRACE_POLL_ATTEMPTS` × `GRACE_POLL_SECONDS`) before reporting. A label landing
inside that window is observed by the same run: **no failing run is produced at
all.** A passing PR never waits.

If the label API cannot be read, the gate keeps failing. Unknown is not
permission — a gate that passes when it cannot see the labels is a gate an
outage switches off.

#### The `skip-docs` escape hatch

For the genuinely doc-irrelevant PR — a dependency bump, a CI fix, a typo in a
comment. Adding the label re-runs the check, so nothing needs pushing.

Using it is a decision, so it goes in the PR's `## Deviations and Decisions`
section with a reason. A `skip-docs` label with no justification is a review
comment.

## Pipeline workflows

The issue pipeline runs on schedules and events rather than on PRs, and none of
it blocks a merge. See [Issue pipeline](issue-pipeline.md) for what the pipeline
does; this is where the workflows live.

| Workflow | Trigger | Does |
| --- | --- | --- |
| `gatekeeper-comment` | issue comment created | parses commands (`/approve`, `/park`, …), applies label changes, acknowledges with a reaction |
| `gatekeeper-sweep` | schedule | catches events the comment trigger missed, and auto-revisits blockers whose blocking issue has closed |
| `dashboard` | schedule, and after pipeline runs | rewrites the live dashboard issue |
| `pipeline-tests` | PR touching `.claude/skills/**` | runs the pipeline scripts' own unit tests |

`pipeline-tests` is the one that blocks: the pipeline's scripts are code, they
have tests, and a broken gatekeeper is a broken queue.

## When something is red

1. **Read the failure.** Which job, which step, which assertion. CI failures on
   this repo are specific by design.
2. **Reproduce what you can locally.** The Core suite, `mkdocs build --strict`,
   and the geometry check all run without an editor or a licence.
3. **Fix it in the same PR.** A red PR does not merge; a red PR that gets a
   follow-up issue is a red PR that stays red.
4. **If it's red on `main` too**, say so in the PR — that's a pre-existing
   failure, not yours, and it needs its own issue.

The one thing not to do is disable or weaken a check to get a PR through. If a
check is wrong, that's a real problem and worth an issue; it is never worth a
quiet edit in an unrelated PR.
