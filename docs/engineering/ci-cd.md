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
| `pr-build` | PR | yes | the app still compiles into an installable APK |
| `rc-build` | manual, tag | no | a release candidate someone can actually play |
| `release-please` | push to `main` | no | the version, changelog, tag, release, and release APK |
| `labels-sync` | push to `main` touching `labels.yml` | no | the label taxonomy matches the file |
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
exposure as an action — and the same rule, including for workflows in this
repository.

### The actions this project uses

| Action | Pin | Used by |
| --- | --- | --- |
| `actions/checkout` | `3d3c42e5aac5ba805825da76410c181273ba90b1` | everything |
| `actions/setup-python` | `5fda3b95a4ea91299a34e894583c3862153e4b97` | docs, pipeline, scripts |
| `actions/setup-dotnet` | `a98b56852c35b8e3190ac28c8c2271da59106c68` | the Core suite |
| `actions/upload-artifact` | `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` | APKs, test results |
| `actions/download-artifact` | `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c` | the release attach step |
| `googleapis/release-please-action` | `45996ed1f6d02564a971a2fa1b5860e934307cf7` | `release-please` |
| `game-ci/unity-builder` | `d829bfc901f2347c8fe18898f06712b66916ef42` | APK builds |
| `game-ci/unity-test-runner` | `0ff419b913a3630032cbe0de48a0099b5a9f0ed9` | the EditMode suite |
| `actions/configure-pages` | `45bfe0192ca1faeb007ade9deae92b16b8254a0d` | `docs-publish` |
| `actions/upload-pages-artifact` | `fc324d3547104276b827a68afc52ff2a11cc49c9` | `docs-publish` |
| `actions/deploy-pages` | `cd2ce8fcbc39b97be8ca5fce6e763baed58fa128` | `docs-publish` |

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

Red here almost always means a compile error the Core suite couldn't catch,
because it lives in the Unity layer. Read the build log, not the test log.

### `rc-build` — release candidates

An RC is a build of what is *about* to be released, produced so someone can play
it before the release is real.

- Triggered manually, or by a pre-release tag.
- **The `rcN` number is derived, not chosen.** It counts the existing RC
  artifacts for the current `/VERSION` and adds one, so the first RC of `0.2.0`
  is `0.2.0-rc1` and the next is `rc2`. Nobody has to remember where they got to,
  and two people cannot both produce `rc3`.
- Release-signed and built with the device profile — an RC that differs from the
  release in signing or backend is an RC that proves less than it appears to.
- Artifacts only. An RC is not a release; it gets no tag and no GitHub release.

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

The APK is attached from inside `release-please.yml` rather than by a separate
workflow that listens for the release — see [Versioning](versioning.md) for the
release flow. On the run where `release_created` is true, it also:

- Builds the release APK, signed with the release keystore, device profile
  (ARM64, IL2CPP).
- **Attaches it to the GitHub release**, so the tag and the artifact are one
  thing. A release whose APK lives in an expiring workflow artifact is a release
  you cannot re-install in six months.
- **Attaches an emulator-targeted asset** as well — x86_64, Mono — so the
  release can be run on a desktop emulator without rebuilding. Clearly named as
  the emulator asset; it is for trying the game, not for judging it.

Keeping this inside `release-please.yml` avoids the failure mode where a
separate release-triggered workflow doesn't fire (a `GITHUB_TOKEN`-created
release does not trigger `release` events) and the release sits there with no
APK. A manual backfill workflow exists for the case where the attach step
itself fails.

## Correctness workflows

### `pr-title-lint`

The PR title becomes the squash commit message, which becomes the changelog
entry, which is what release-please reads to decide the next version. So the
title has to be a valid Conventional Commit before it can merge.

Checks the type is one of the allowed set, that the scope (if present) is
lowercase, that there's a subject, and that the subject isn't so long it gets
truncated in a changelog. See [Versioning](versioning.md) for why each of those
matters.

Red here is a thirty-second fix: edit the PR title. The check re-runs on edit.

### `ci-tests` — the two suites

Runs both, always, in one workflow:

1. **Core** — plain NUnit via `dotnet test`. No editor, no licence, seconds.
2. **EditMode** — Unity Test Framework, headless, in a GameCI container with the
   Unity licence from the `UNITY_LICENSE` secret.

See [Testing](testing.md) for what belongs in each.

#### The results-XML verdict rule

**A green exit code is not a pass.** The Unity test runner has been known to
exit 0 having run zero tests — a licence problem, a missing assembly, or a
filter that matched nothing all look like success from the outside, and "0 tests
passed" is the most dangerous green there is.

So the verdict comes from the results XML, not the exit code. A dedicated script
parses it and fails the job unless:

- the results file exists and parses;
- the total number of tests run is **greater than zero**;
- there are no failures, errors, or unexpected inconclusives.

If the XML is missing, the job fails. "We couldn't tell" is a failure, not a
pass — the whole point is to not accept an absence of evidence as evidence.

### The geometry and tuning literal check

Enforces the named-values rule from [Tech stack](tech-stack.md): no bare numeric
literals for sizes, offsets, margins, durations, speeds, thresholds, or payouts
in method bodies.

**Ratcheting baseline.** A committed baseline file records how many violations
exist per file today. The check fails if a file's count goes *up*; it does not
demand that existing code be fixed before anything else can happen. When a file's
count goes down, the baseline is updated in the same PR, and the new lower number
becomes the ceiling.

The ratchet is the whole design. A check that demanded the codebase be clean
before it could be turned on is a check that never gets turned on; one that only
says "not worse than yesterday" can be turned on today and still converges.

If a literal is genuinely fine, the answer is one of the documented exemptions,
or naming it. Raising the baseline to make the check pass is not a fix and shows
up in review as exactly what it is.

## Docs workflows

### `docs-test`

Builds the site with `mkdocs build --strict`, so a broken nav entry, a dead
internal link, or a page that isn't in the nav fails the PR rather than shipping
a broken site.

### `docs-publish`

Publishes the built site to GitHub Pages with `mike`, under the version alias
for the release. See [Conventions](../intro/conventions.md#docs-versioning).
Never edit `gh-pages` by hand.

### The reconciliation gate — always on

**Every PR must state what happened to the docs.** The gate checks the PR body
for a `**Docs:**` line:

```text
**Docs:** docs/specs/frogs.md — updated the splitting rule to cap at 32.
**Docs:** None — no behaviour the docs describe was changed.
```

It is deliberately a *statement* requirement rather than a diff requirement. A
check that demanded a docs change whenever code changed would be wrong most of
the time and would train everyone to make token docs edits. A check that
requires you to say "None" makes the author spend three seconds actually
considering it, which is the behaviour we want.

The gate is **always on** — it does not skip based on which files changed,
because "this PR obviously doesn't affect the docs" is exactly the judgement it
exists to make someone write down.

#### The `skip-docs` escape hatch

The `skip-docs` label bypasses the gate, for the genuinely doc-irrelevant PR —
a dependency bump, a CI fix, a typo in a comment.

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
