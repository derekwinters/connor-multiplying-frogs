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

### Release builds

Handled inside `release-please.yml` rather than by a separate workflow that
listens for the release — see [Versioning](versioning.md) for the release flow.
Merging the release PR creates the tag and the release, and the same run then:

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
