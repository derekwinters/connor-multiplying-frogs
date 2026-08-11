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
| `pipeline-tests` | PR/push touching the pipeline skills | fails the PR | the pipeline scripts' own unit tests |
| `pipeline-*` (others) | schedule, issue comments | no | the issue pipeline itself |

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

### The check that enforces it

`check_action_pins.py` scans every file under `.github/workflows/` and **fails
the PR** on any `uses:` whose ref is not a full 40-character hex SHA. Tags,
branches, short SHAs, and a bare `owner/repo` with no ref are all rejected.

It runs inside the `Gate tests` job rather than as its own workflow: that job
already runs on every PR, so the check costs no extra run — and it is
deliberately **not** path-filtered. A path filter would miss precisely the case
this exists for, which is a tag slipping into a workflow that nothing happens
to trigger. The platform's own policy would eventually reject it, mid-run,
months later.

Two things it deliberately does not flag:

- **`./…`** — a local composite action or reusable workflow, versioned with
  this repository. Pinning it to a SHA would pin the repo to itself.
- **`docker://…`** — an image reference, governed by the registry rather than
  by Actions.

A missing `# vX.Y.Z` comment is a **warning, not a failure**. The pin is
correct and the build is safe; what is missing is the human-readable part.
Failing a PR over a comment teaches people that this check is pedantic, and a
check people resent is one they route around.

### Keeping pins fresh

A pin that never moves is a pin that misses security fixes — the failure mode
this convention trades *into*. `.github/dependabot.yml` configures Dependabot
to watch `github-actions` and open a PR per action when a new version appears,
updating the SHA **and** the trailing comment together.

That last part is why the comment convention is worth keeping tidy: because
Dependabot rewrites both, its PRs *satisfy* the pin check rather than tripping
it.

| Setting | Value | Why |
| --- | --- | --- |
| `directory` | `/` | The ecosystem knows where workflows live; this is not a path to them. |
| `schedule` | weekly, Monday 09:00 Central | A week's updates arrive together, as a task rather than an interruption. |
| `open-pull-requests-limit` | 3 | See below. |
| `commit-message.prefix` | `chore` + scope | Produces `chore(deps): …` — a valid Conventional Commit. |
| `labels` | `area:build`, `skip-docs` | Its PRs touch no docs, so the gate needs the escape hatch. |

**The PR limit is low on purpose.** The failure this guards against is not
missing an update; it is a wall of dependency PRs that nobody reads and
everybody merges, which is strictly worse than not pinning because it looks
like diligence. Three at a time stays reviewable.

**`skip-docs` is applied up front** rather than left for a human. The docs
reconciliation gate fails a code-only PR, and every Dependabot PR is code-only
by construction — without the label each one arrives red and needs unsticking
before it can merge.

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

#### How a value reaches the Unity process {#build-inputs}

**On Unity's command line, via the action's `customParameters` input.** Never in
the step's `env:`.

`game-ci/unity-builder` does not run Unity on the runner. It runs it inside a
container, and it forwards a **fixed allow-list** of environment variables into
that container — `UNITY_*`, `BUILD_*`, `ANDROID_*`, `CUSTOM_PARAMETERS`, a
handful of `GITHUB_*`. Nothing else. A `FROGS_ANDROID_PROFILE` set in a step's
`env:` sits on the runner, outside the container, where the editor never looks.

That failure is silent by construction. Every reader of these values treats
"absent" as "this build did not ask for that" — which is the right answer for
the editor's own Build button, and the wrong one here. So a build that received
none of its inputs looks exactly like a build that wanted none of them. `v0.1.0`
shipped a device APK and an emulator APK that were the same file, byte for byte,
and every check upstream of the APK was green (#218).

`customParameters` is appended verbatim to the container's `unity-editor`
invocation, so it does arrive:

| Value | Flag |
| --- | --- |
| Android `versionCode` | `-frogsVersionCode` |
| Short commit sha, for a PR build | `-frogsBuildSha` |
| Release-candidate number | `-frogsRcNumber` |
| `applicationId` suffix | `-frogsApplicationIdSuffix` |
| Android build profile | `-frogsAndroidProfile` |

`BuildInputs` reads the command line first and the matching `FROGS_*` variable
second. The variables still work everywhere the container is not involved — a
local headless `-executeMethod`, and the EditMode tests — and mean nothing in
CI.

Two things hold this in place, and both are why the rule is written down rather
than remembered:

- **`.github/scripts/tests/test_build_inputs_reach_unity.py`** fails if any
  Unity build step sets a `FROGS_*` variable in its `env:`, if a build is not
  told its `versionCode`, if a flag is passed with no value after it, or if two
  builds in one workflow ask for the same profile.
- **`BuildArguments`** throws when a flag is present with no usable value,
  rather than reading it as absent. A build that received a broken command line
  stops while the log is still open.

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
| `multiplying-frogs-0.2.0-emulator.apk` | x86_64, IL2CPP | a desktop emulator |

They are built into `build/device/` and `build/emulator/` rather than a shared
directory. The two have the same version and differ only in architecture, which
is not visible from a filename sitting next to another filename — separate
paths make it impossible for the wrong one to be picked up by a glob.

#### The `Android/` segment is the builder's, not ours

`game-ci/unity-builder` writes to **`<buildsPath>/<targetPlatform>/`** — it
appends the platform directory itself, whether or not `buildsPath` was set. So
the APKs land in `build/device/Android/` and `build/emulator/Android/`, and
anything reading them must include that segment:

| Workflow | `buildsPath` | Where the APK actually lands |
| --- | --- | --- |
| `pr-build`, `rc-build` | default (`build`) | `build/Android/` |
| `release-build` | `build/device`, `build/emulator` | `build/device/Android/`, `build/emulator/Android/` |

Getting this wrong is quiet rather than loud. The attach step runs under
`shopt -s nullglob`, so a pattern that matches nothing simply disappears — both
Unity builds go green, the assets array comes out empty, and the step reports
that the build produced no APK when it produced two. That is what left `v0.1.0`
tagged with no APK on it (#212).

Two things now stop it recurring:

- **`.github/scripts/tests/test_build_output_paths.py`** asserts the invariant
  across every workflow that runs a Unity build: each APK path must sit under
  the `<buildsPath>/<targetPlatform>/` of a build in the same file, and each
  build path must be read by something. A `buildsPath` and the glob consuming
  it cannot drift apart, and a build profile nobody collects is a failure too.
  It runs in `Gate tests` with the other CI-script tests.
- **The attach step names what it searched**, and says for each path whether
  the directory was missing, empty, or held files that were not `*.apk` —
  because "the build produced nothing" and "nothing matched the glob" have
  completely different fixes and used to be the same message.

The profile is applied by `BuildStampPreprocessor` from `-frogsAndroidProfile`
on the Unity command line ([how a value gets there](#build-inputs)), and an
unrecognised value fails the build rather than defaulting: guessing here ships
the wrong architecture, which looks fine until someone installs it.

#### The two APKs are checked against each other before either is attached

Running Unity twice is not the same as getting two builds. For `v0.1.0` it was
not: the profile never reached the editor, both invocations built the same
thing, and the release went out with two byte-identical assets under different
names (#218). Nothing upstream of the APK could have seen it — both builds went
green, both files existed, both were a plausible size.

So `.github/scripts/check_release_apks.py` opens them. An APK's native
libraries live under `lib/<abi>/`, and those ABI names are Android's, so they
mean the same thing in every APK ever built; IL2CPP additionally ships
`libil2cpp.so` and Mono does not. That is enough to assert the whole profile
table:

| Check | Why |
| --- | --- |
| the two files are not byte-identical | two profiles cannot produce one file |
| device has `arm64-v8a` and nothing else | the tablet's architecture, alone |
| emulator has `x86_64` and nothing else | an ARM64 APK will not install on an x86_64 emulator |
| both ship `libil2cpp.so` | 64-bit Android has no Mono, so an APK without it was built for a pairing Unity cannot build ([#282](https://github.com/derekwinters/connor-multiplying-frogs/issues/282)) |

It runs **before** the attach step, so a release that failed it gets no assets
rather than the wrong ones. That is the right way round: a bad APK attached to
a tag is a failure that surfaces on someone else's machine, days later, with no
build log anywhere near it, while a release missing its APKs is a backfill this
workflow already knows how to do.

It is skipped in exactly one case: when only one of the two APKs exists, so
there is no pair to compare. Attaching a single, correctly named device APK is
not the mislabelling this gate protects against; attaching two identical files
still is, and that stays strict.

#### A failed emulator build does not cost the release its device APK

The emulator build carries `continue-on-error`, and the device build does not.
Until `v0.2.0` neither did, and the consequence was the worst available
outcome: the emulator build failed, the job stopped, the attach step three
steps below never ran, and the release was **published with nothing on it** —
while a perfectly good device APK sat on the runner until it was deleted
(#282).

So the job now finishes what it can:

| | |
| --- | --- |
| Device build fails | the job stops. The device APK *is* the release. |
| Emulator build fails | the device APK is still checked and attached |
| Only one APK attached | a `::warning::` and a line in the job summary saying the release is incomplete |
| Emulator build failed | the last step re-raises it, so the **run is still red** |

The last row is the one that stops this being a papering-over. Attaching what
built must not turn a broken build green, because a half-filled release nobody
is told about is how the next release ships the same way. The run goes red
*after* the attach, not instead of it.

`.github/scripts/tests/test_release_partial_attach.py` asserts that shape:
which build may continue on error and which may not, that something downstream
reads the emulator step's `outcome` and fails on it, that the two-APK
comparison is still invoked with both APKs, and that a partial attach warns.

#### Backfilling a release that missed its APKs

`workflow_dispatch` takes the tag as an **input**, and the checkout uses it:
the build is of the source at that tag, exactly as released. The *workflow
file* is a different matter — Actions runs the version of it that lives at the
ref the dispatch was made against. So dispatching from `main` with
`tag: v0.2.0` builds v0.2.0's source using today's workflow, which is what
makes a backfill able to rescue a release whose failure was in the workflow
rather than in the code. A fix that lives in the source at `main` does **not**
reach a backfill of an older tag; only the next release gets it.

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

The Core job also runs two static checks first. Both cost nothing and both fail
for reasons the test output would never explain:

- `check_core_isolation.py` — game logic that has grown a dependency on the
  engine.
- `check_assembly_references.py` — a `using` of a project namespace from an
  assembly that cannot see it. Unity gives the same verdict, but only inside an
  editor, minutes into a build, after a licence has been sorted out. See
  [the Core/Unity split](tech-stack.md#the-check-that-catches-it).

#### A missing licence fails, it does not skip

The EditMode job's **first** step, before any checkout or container pull,
asserts that `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` are all set —
and fails the job if any is missing.

Skipping would be the friendlier behaviour and the wrong one. A required check
that goes green because the suite never ran is worse than no check at all: it
reports "tested" on a PR nothing tested, and nobody looks twice at a green tick.

Until the secrets exist (#82), this job is red on every PR that touches the
Unity project. That is the intended behaviour, and it is visible rather than
quiet.

#### Three secrets, not one

A Unity Personal licence needs **all three** of `UNITY_LICENSE`, `UNITY_EMAIL`,
and `UNITY_PASSWORD`, in every workflow that starts a Unity container.

The name of the first one is misleading. GameCI never passes `UNITY_LICENSE`
into the container at all — it parses the serial out of the `.ulf` on the runner
and activates inside the container with
`unity-editor -serial … -username … -password …`. A licence on its own gets as
far as producing a valid serial and then dies with *"License activation strategy
could not be determined"*, several minutes and one image pull later.

So the licence gates check all three up front. The secrets are the raw contents
of `Unity_lic.ulf` — the XML, verbatim, not base64 and not flattened onto one
line — plus the Unity ID that licence belongs to.

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

**And `types: [opened, synchronize, reopened, labeled]`.** The first three are
GitHub's default set, spelled out because naming `types:` at all *replaces* the
defaults rather than extending them. `labeled` is the addition, and it is the
whole of [the escape hatch](#the-skip-docs-escape-hatch) working: see
[Every input to the verdict re-runs the gate](#every-input-to-the-verdict-re-runs-the-gate).

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

#### Every input to the verdict re-runs the gate

The verdict has exactly two inputs: the PR's **changed files** and the PR's
**labels**. Both have to be able to start a fresh run, or the tick on the PR is
answering a question nobody asked any more.

| Input changes | What re-runs the gate |
| --- | --- |
| a commit is pushed | the `synchronize` trigger |
| a label is added | the `labeled` trigger, or the grace poll if the run is still going |

`labeled` is on the workflow for that reason and no other. It is not obvious
from reading `docs-test.yml`, so it carries a comment saying so and
`.github/scripts/tests/test_docs_gate_triggers.py` asserts it — the failure mode
if it is tidied away is silent, and lands on whoever is already blocked.

Because `docs-test` is grouped per PR with `cancel-in-progress: true`, a label
applied while a run is in flight cancels that run and starts another. That is
the right way round: the surviving run is the newer one, it is the one that saw
the new label, and it is the one whose conclusion the branch ruleset reads.

Removing a label does **not** re-run the gate, so a PR that passed on
`skip-docs` keeps its green tick if the label is taken off again. That is a
known gap rather than a decision — see
[#250](https://github.com/derekwinters/connor-multiplying-frogs/issues/250).

#### The grace poll

A `skip-docs` label applied moments after the PR opens would otherwise produce a
failing run that is already wrong by the time anyone reads it — and a red tick
nobody should act on is worse than a slow one.

So a would-be failure re-reads the PR's **live** labels for up to a minute
(`GRACE_POLL_ATTEMPTS` × `GRACE_POLL_SECONDS`) before reporting. A label landing
inside that window is observed by the same run: **no failing run is produced at
all.** A passing PR never waits.

The poll is the pipeline's case, not a human's. `pipeline-dev` applies
`skip-docs` in the same breath as opening the PR, so the label is always inside
the window and a bot PR never flashes red. A person reads the failure first and
labels afterwards, minutes later — that is the `labeled` trigger's job, and the
poll is no substitute for it.

If the label API cannot be read, the gate keeps failing. Unknown is not
permission — a gate that passes when it cannot see the labels is a gate an
outage switches off.

#### The `skip-docs` escape hatch

For the genuinely doc-irrelevant PR — a dependency bump, a CI fix, a typo in a
comment. Adding the label re-runs the check, so nothing needs pushing: either
the run still in flight sees it during the grace poll, or the `labeled` trigger
starts a new one.

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
| `gatekeeper-sweep` | issue/PR events, 6-hourly cron, dispatch | replays comment commands the comment workflow missed, auto-revisits blockers that have cleared, and applies reconcile's fixes |
| `dashboard` | human label changes, daily schedules, dispatch | rewrites the live dashboard issue |
| `pipeline-tests` | PR/push touching the pipeline skills | runs the pipeline scripts' own unit tests |

### `gatekeeper-comment`

`on: issue_comment: [created]`, so a command applies seconds after it is typed
rather than waiting for a scheduled round.

**Concurrency is per issue, with `cancel-in-progress: false`.** Two commands on
one issue have to apply in the order they were written; cancelling the first
run would drop a command Derek typed and acknowledge only the second. Grouping
per issue rather than globally keeps unrelated issues from queueing behind each
other.

The workflow-level `if:` filters to a non-bot comment from the repository owner
on an issue rather than a PR, and starting with `/`. That is a cost filter —
the script re-checks the author itself, because a script that is safe only
because a workflow's `if:` filtered for it is one careless edit away from not
being safe at all.

**`GITHUB_TOKEN` only, never a PAT**, and here the usual scoping argument is
the *less* important one. GitHub suppresses workflow runs from events initiated
by `GITHUB_TOKEN`. A PAT-authored acknowledgement would look like a new comment
and re-trigger this same workflow — an unbounded loop that the platform's
no-recursion guard is precisely what prevents.

The reactive-triage secrets (`AI_TRIAGE_URL`, `AI_TRIAGE_SECRET`, see #83) are
surfaced as env vars. Absent, firing is a clean no-op: the nightly round picks
the issue up, so a missing secret costs latency rather than correctness.

### `gatekeeper-sweep`

Runs on the events that can actually change the board picture — an issue
closed or labelled, a PR closed — plus a six-hourly cron and manual dispatch.

**Two paths, and the difference is the point.** The event path applies
`strip_labels` only. The cron path applies everything: the comment replay, and
reconcile's two requeue fixes.

**The comment replay is cron-path only, and for a different reason from the
other two.** `gatekeeper-comment` can lose a command outright — a webhook that
is never delivered, a workflow that fails to start — so the cron re-reads the
last week of comments and re-applies anything not already carrying the bot's
👀. It cannot run on the event path, because this workflow's event triggers
include `issues: [labeled]` and an applied command *changes a label*: the
replay would wake up and re-apply the very comment `gatekeeper-comment` is
applying at that moment. The two are in different concurrency groups, so
nothing serialises them. See
[Issue pipeline](issue-pipeline.md#replaying-the-comments-the-webhook-lost).

The two requeue fixes are cron-only because **the drift they detect is
indistinguishable from work in flight**. An issue the builder picked up ten
seconds ago has `in-progress` and no PR yet — exactly a stall's shape — and requeuing it on the
event path would yank the issue out from under a running agent. Triage's
comment-before-label ordering creates the mirror-image window for
`requeue_triage`. By the six-hourly run those transients have resolved.

They are omitted entirely rather than softened with a time threshold: a
threshold is a guess about how long an agent takes, and it is wrong in both
directions — too short and it interrupts a slow build, too long and a stall
sits there all day. `strip_labels` has no such problem and runs on every pass,
because it only ever touches an already-closed issue.

**Concurrency is one constant group, board-wide** — not per issue like
`gatekeeper-comment`. This sweep reads and rewrites the whole board, so two
overlapping runs would each be acting on a snapshot the other has already
invalidated.

### `dashboard`

Renders the board on `issues: [labeled, unlabeled]`, on three daily schedules
offset after the nightly routines, and on dispatch.

**Why the gatekeeper also re-renders inline.** GitHub suppresses workflow runs
from events initiated by `GITHUB_TOKEN`, and the gatekeeper's label moves use
that token — so they do *not* fire the `labeled` trigger here. This workflow
would never see the very changes it most needs to reflect. That is why
`run_comment_event.py` and `run_sweep.py` call the renderer directly after
their label writes. The trigger here catches label changes a human makes in the
GitHub UI; the schedule is the backstop for everything else.

**Concurrency is one group with `cancel-in-progress: false`,** so a burst of
label changes collapses to one final render on the latest state. Cancelling
would also be safe — the render is idempotent — but queueing guarantees the
last render in a burst is the one that sees everything.

#### The timestamp, and how it avoids defeating byte-stability

The "as of" line is computed in the workflow (`TZ=America/Chicago`) and passed
in as an environment variable. **The renderer never reads a clock**, which is
what keeps `render()` a pure function of its arguments and the golden test
possible.

A timestamp makes every render textually different, which would undo what
byte-stability was for: the scheduled runs would PATCH the issue every time and
the board's history would become a wall of edits that changed nothing. So
`write_dashboard` compares the new body against the current one **with the
timestamp line excluded**, and skips the write when nothing else moved.

The board therefore says when it was last *rendered*, while its edit history
records only when it last *changed* — which are two genuinely different
questions, and both worth being able to answer.

### `pipeline-tests`

The pipeline's scripts are code, they have tests, and a broken gatekeeper is a
broken queue — so unlike the rest of the pipeline workflows, this one runs on
pull requests and fails them.

**Stdlib only. No Unity, no `pip install`, no lockfile.** These scripts decide
what gets triaged, built, and requeued, and their tests must not be capable of
breaking because the game toolchain did. The job needs nothing but the Python
already on the runner, which is also why it finishes in seconds.

It runs `run_python_tests.py` rather than `python3 -m unittest discover`,
because discovery cannot find these tests at all: it only recurses into
directories whose names are valid Python identifiers, and `.claude` starts with
a dot. The runner also executes each suite in isolation — several skills have a
package named `tests`, and in a shared interpreter the second silently resolves
to the first's cached module and never runs.

**It is path-gated, so it must not be marked required.** Same trap as
[`ci-tests`](#path-gating-and-what-it-means-for-branch-protection): a
path-gated workflow does not report at all on PRs that miss its paths, so a
required check would block every unrelated PR forever waiting for a run that
will never happen. "Fails the PRs it runs on" and "required in branch
protection" are different things, and only the first is true here.

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
