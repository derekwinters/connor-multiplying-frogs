# Versioning

How the app gets its version number: one file, owned by one tool, moved by the
commit messages you were writing anyway.

## `/VERSION` is the source of truth

A single file at the repo root holds the version the app reports. It is one
line:

```text
0.0.1 # x-release-please-version
```

Everything that needs a version reads it from here. Unity's
`PlayerSettings.bundleVersion` is set from it at build time, the Android
`versionCode` is derived from it, the release tag matches it, and anything at
runtime that wants to display it reads a value that came from it.

**Never hand-edit this file.** release-please owns it. A hand edit is a version
that disagrees with the changelog, the tag, and the git history, and the
disagreement surfaces at the worst moment — when you are trying to work out
which build a bug was in.

### Why not `ProjectSettings/ProjectSettings.asset`?

Unity already stores a `bundleVersion` in `ProjectSettings.asset`, so keeping the
version somewhere else looks like duplication. It isn't, for four reasons:

- **`ProjectSettings.asset` is a 200-line generated YAML blob.** A tool that
  edits one field in it has to parse Unity's serialization format and write it
  back byte-compatibly. release-please can replace a line in a text file; it
  cannot safely rewrite a Unity asset.
- **Every CI job would need Unity to read the version.** Working out the tag for
  a release, or the filename for an artifact, would mean booting a licensed
  editor. Reading `/VERSION` is `cat`.
- **The editor rewrites that file constantly.** Opening the project touches it.
  A version living there produces conflicts on every merge and diffs that mix
  "we shipped 0.2.0" with "someone's editor reordered a list".
- **A generated file is a bad home for a decision.** `/VERSION` exists to be
  read by humans and by five different tools; `ProjectSettings.asset` exists to
  be read by Unity.

The value is *injected into* `PlayerSettings` at build time from `/VERSION`, so
there is still exactly one source and Unity still gets what it needs. See
[CI/CD](ci-cd.md).

### The `x-release-please-version` marker

release-please's generic updater does not guess where the version is in a file.
It rewrites **only lines containing the `x-release-please-version` annotation**,
which is why the marker shares the line with the version rather than sitting
above it.

**Without the marker, nothing fails loudly.** release-please runs, opens its
release PR, writes the changelog, tags the release — and leaves `/VERSION` at
its old value. You get a `v0.2.0` tag on a build that reports `0.1.0`, and the
first sign of it is someone asking why the version on the title screen never
changes. That is why the marker is a build-checklist item and why
[a Core guard test](#the-guard-test) asserts the file and the manifest agree.

### Every consumer strips from `#` onward

The marker lives on the version's own line, so **the file's contents are not the
version** — anything reading `/VERSION` has to cut the comment off first, then
trim.

Do not reimplement that per caller. `Frogs.Core.AppVersion.ReadFrom(contents)`
does it once, and is covered by the fast suite:

```csharp
var version = AppVersion.ReadFrom(File.ReadAllText("VERSION"));
version.Major;      // 0
version.ToString(); // "0.0.1"
```

It throws `FormatException` on a file with only a marker and no version, and on
anything that isn't three non-negative numbers — a malformed `/VERSION` has to
fail at the point of reading rather than silently become `0.0.0` in a shipped
APK.

For shell consumers, the equivalent is:

```bash
VERSION=$(cut -d'#' -f1 VERSION | tr -d '[:space:]')
```

## Conventional Commits drive the bump

The version moves because of what the commits say. Nobody decides it.

| Commit prefix | Effect |
| --- | --- |
| `fix:` | patch — `0.1.0` → `0.1.1` |
| `feat:` | minor — `0.1.0` → `0.2.0` |
| `feat!:` or `BREAKING CHANGE:` | major — `0.1.0` → **`1.0.0`** |
| `docs:` `chore:` `ci:` `test:` `refactor:` `build:` | none |

**Plain semver, with no pre-1.0 special-casing.** Both of release-please's
pre-major flags are off:

- **`bump-minor-pre-major: false`** — a `feat:` bumps the minor version even
  before 1.0. The alternative folds features into patch bumps, which makes the
  version number stop distinguishing "we fixed something" from "there is a new
  thing in the game" during exactly the period when new things are all that
  happens.
- **`bump-patch-for-minor-pre-major: false`** — same reason, from the other
  direction.

!!! warning "`feat!:` pre-1.0 goes straight to 1.0.0"
    With `bump-minor-pre-major` off, a breaking-change marker takes the version
    to `1.0.0` — not `0.2.0`. There is no undo: the tag and the release are
    created by the same merge.

    So before writing `!` or a `BREAKING CHANGE:` footer on a pre-1.0 project,
    be sure you mean it. Almost nothing in a game nobody has installed yet is
    genuinely a breaking change; there is no API and no save format anyone
    depends on. If in doubt, it's a `feat:`.

This is why the commit prefix is a rule and not a style preference: a `feat:`
that should have been a `fix:` ships a version number that lies about what
changed, and a stray `!` ships 1.0.

## Milestone versions and the shipped version are different things

They look alike and they are not connected.

| | Milestone `v0.1` | `/VERSION` |
| --- | --- | --- |
| What it is | a plan | a fact |
| Set by | a human, when planning | release-please, from commits |
| Changes when | scope changes | a release goes out |
| Read by | triage, the dashboard | the build, the tag, the app |

A milestone titled `v0.1` says "this is the batch of work we are calling 0.1".
The shipped `/VERSION` may reach `0.1.0` before that milestone closes, or long
after, or never — if the milestone's work all lands as `fix:` commits, the
version could be at `0.0.9` when `v0.1` closes. **That is fine.** Nothing is
broken and nothing needs reconciling.

Do not add tooling to keep them in step, and do not "correct" one to match the
other. See [Conventions](../intro/conventions.md) for the milestone model.

## The release-please setup

### Config lives at non-default paths

```text
.github/release-please/config.json
.github/release-please/manifest.json
```

Not the repo root. The root of a Unity project is already crowded with
directories Unity owns and files a human is expected to notice — `/VERSION`
earns its place there, two pieces of tool config do not. Grouping them in one
directory also means the whole release configuration is one thing to find. The
workflow passes both paths explicitly:

```yaml
config-file: .github/release-please/config.json
manifest-file: .github/release-please/manifest.json
```

### Config summary

- **`release-type: simple`** — this is not an npm package or a Go module. There
  is no manifest with a version field for release-please to understand; there is
  a text file and a changelog. `simple` does exactly that and nothing else.
- **`extra-files: [{ "type": "generic", "path": "VERSION" }]`** — the entry that
  makes release-please rewrite `/VERSION` via the generic updater and its
  marker. This is the line that connects the tool to the source of truth;
  without it the two drift silently.
- **`bump-minor-pre-major: false`** and **`bump-patch-for-minor-pre-major:
  false`** — plain semver, as described above.
- **`include-component-in-tag: false`** — one package, so tags are `v0.2.0`
  rather than `multiplying-frogs-v0.2.0`.
- **The manifest** (`.github/release-please/manifest.json`) records the last
  released version per package — `{".": "0.0.1"}`. It is generated:
  release-please updates it in its own release PR, and a hand edit tells the
  tool it already released something it didn't.

### The guard test

`/VERSION` and `.github/release-please/manifest.json` hold the same number in
two places, and **nothing in release-please notices when they stop agreeing.**
A damaged marker leaves `/VERSION` behind while the manifest advances; releases
keep shipping; the first symptom is a build reporting a version from three
releases ago.

`Tests/Core/VersionDriftTests.cs` closes that hole with three assertions:

| Assertion | Catches |
| --- | --- |
| `/VERSION` parses as bare semver once `#…` is stripped | a hand-edit, a bad merge resolution |
| `/VERSION` still contains `x-release-please-version` | the marker being removed — the usual *cause* |
| `/VERSION` equals the manifest's `"."` entry | the drift itself |

The middle one earns its place by failing on the cause rather than waiting for
the symptom: the run that removes the marker fails, instead of the release two
months later.

It is a plain NUnit test in the ordinary Core suite, so it costs milliseconds
and runs on every push, with no editor and no network.

### The release flow

1. Commits land on `main`, each from one squashed PR.
2. release-please maintains a **release PR** — an open PR that accumulates the
   pending changelog and the version bump. It is rewritten on every push to
   `main`; do not review it as a change, review it as a summary.
3. Merging that PR *is* the release. It updates `/VERSION` and `CHANGELOG.md`,
   creates the tag and the GitHub release, and triggers the release build, which
   attaches the APK.

## One issue → one PR → one squash commit → one changelog entry

The chain is only as good as its narrowest link, and every link is enforced:

- **One issue per PR**, closed with a keyword in the PR body.
- **One PR per squash commit** — `main` is squash-merge only, so a PR is exactly
  one commit in the history.
- **The squash commit message is the PR title**, which is why the PR title has
  to be a valid Conventional Commit and why [CI lints it](ci-cd.md).
- **One commit → one changelog entry.**

So the changelog reads as one line per issue, each traceable to a PR, an issue,
and a version. Break any link and you get a changelog entry saying
"feat: various improvements" pointing at a merge of eleven unrelated things,
which is a changelog nobody reads and therefore a changelog nobody maintains.

This is also why work-in-progress commits on a branch don't need to be
Conventional Commits — they are squashed away. Only the PR title survives.

## What a build calls itself

Two different things, from two different sources, both applied at build time and
never stored in `ProjectSettings.asset`:

| | Source | Example |
| --- | --- | --- |
| **Version name** (`PlayerSettings.bundleVersion`) | `/VERSION`, plus the commit sha on non-release builds | `0.2.3`, `0.2.3-abc1234` |
| **`versionCode`** (Android) | `git rev-list --count main` | `147` |

`Frogs.Core.BuildStamp` composes both and enforces the rules; the editor script
`BuildStampApplier` reads the file, the environment, and git, and hands the
values over. The split is the usual one — the arithmetic is in Core where the
fast suite covers it, and the I/O is in the shell.

### The version name

`0.2.3` for a release. `0.2.3-abc1234` for anything built from a PR or as a
release candidate, because a phone with four test builds on it needs to say
which is which, and "the one from Tuesday" is not an answer.

### `versionCode` is the commit count, not the version

```text
versionCode = git rev-list --count main
```

Android orders installs by this integer and **refuses to install an APK whose
code is not greater than the installed one.**

Deriving it from the semantic version does not survive that rule. Several builds
share a version between releases — every PR build of `0.2.3` would produce the
same code, so installing today's test build over yesterday's would fail, and
the two would be indistinguishable to the system. The commit count moves with
every commit, which is exactly the granularity the constraint needs.

What it buys:

- **Monotonic between releases, not just across them.** Every commit gets a
  higher number than its parent.
- **Reproducible.** The same commit always yields the same count, for everyone.
  A rebuild of a tag produces the same code rather than a new one.
- **Independent of CI.** No run numbers, no counters in workflow state, nothing
  that resets when a workflow is renamed or a repository is migrated.

The cost is legibility: `147` doesn't tell you the version at a glance the way a
derived number would. That's the right trade — the version *name* is what a
human reads, and it's on the same screen.

### It is never hand-set

`ProjectSettings.asset`'s version fields are not edited, ever — not by a person,
not by a tool. They are overwritten at build time from the values above. A
number typed into that file is a number that disagrees with `/VERSION` on the
first release after someone forgets it.

CI passes `FROGS_VERSION_CODE` and `FROGS_BUILD_SHA` rather than letting the
editor shell out to git, because a checkout with `fetch-depth: 1` has no history
to count. Locally both fall back to git, and a failure says so.
