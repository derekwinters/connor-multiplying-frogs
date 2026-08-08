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
version.Major;                // 0
version.AndroidVersionCode;   // 1
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

| Commit prefix | Effect pre-1.0 | Effect post-1.0 |
| --- | --- | --- |
| `fix:` | patch — `0.1.0` → `0.1.1` | patch |
| `feat:` | patch — `0.1.0` → `0.1.1` | minor |
| `feat!:` or `BREAKING CHANGE:` | minor — `0.1.0` → `0.2.0` | major |
| `docs:` `chore:` `ci:` `test:` `refactor:` `build:` | none | none |

The pre-1.0 column is deliberate, configured by two release-please flags:

- **`bump-minor-pre-major: true`** — a breaking change bumps the minor version
  rather than taking us to `1.0.0`. Reaching 1.0 should be a decision, not an
  accident of a commit message.
- **`bump-patch-for-minor-pre-major: true`** — a feature bumps the patch. Before
  1.0 the game is changing constantly; if every feature bumped the minor we'd be
  at `0.40.0` by the time it was playable, and the number would mean nothing.

This is why the commit prefix is a rule and not a style preference: a `feat:`
that should have been a `fix:` ships a version number that lies about what
changed.

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
.github/release-please-config.json
.github/.release-please-manifest.json
```

Not the repo root. The root of a Unity project is already crowded with
directories Unity owns and files a human is expected to notice — `/VERSION`
earns its place there, two pieces of tool config do not. The workflow passes
both paths explicitly.

### Config summary

- **`release-type: simple`** — this is not an npm package or a Go module. There
  is no manifest with a version field for release-please to understand; there is
  a text file and a changelog. `simple` does exactly that and nothing else.
- **`extra-files`** — the entry that makes release-please update `/VERSION` via
  the generic updater and its marker. This is the line that connects the tool to
  the source of truth; without it the two drift silently.
- **`bump-minor-pre-major: true`** and **`bump-patch-for-minor-pre-major: true`**
  — the pre-1.0 behaviour described above.
- **The manifest** (`.release-please-manifest.json`) records the last released
  version per package. It is generated: release-please updates it in its own
  release PR, and a hand edit tells the tool it already released something it
  didn't.

### The guard test

A Core test asserts that `/VERSION` and `.release-please-manifest.json` agree.
It is a plain NUnit test, so it runs in seconds on every push, and it catches
the whole class of "the marker was removed", "someone hand-edited one of them",
and "a merge resolved the conflict wrongly" failures at the point they are
introduced rather than at the next release.

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

## Android `versionCode`

Android needs a monotonically increasing integer, separate from the display
version. It is derived from `/VERSION` at build time — never stored, never
hand-set:

```text
versionCode = major * 10000 + minor * 100 + patch
```

| `/VERSION` | `versionCode` |
| --- | --- |
| `0.0.1` | 1 |
| `0.1.0` | 100 |
| `0.2.3` | 203 |
| `1.0.0` | 10000 |

The constraint the formula carries: **minor and patch must each stay below
100.** Given the pre-1.0 bump flags, patch is the component that moves fastest,
so `0.1.99` → `0.1.100` is the case that would break monotonicity — at `0.1.90`,
bump the minor deliberately rather than riding it out.

Two properties this buys:

- **Reproducible.** The same `/VERSION` always yields the same `versionCode`, so
  a rebuild of a tag produces an identical artifact rather than a new number.
- **Legible.** `versionCode` 203 is `0.2.3`, readable by eye, which matters when
  someone reports a bug from a phone showing only the code.

A build counter appended to the low digits would allow multiple builds per
version, and is deliberately not used: two APKs with the same version and
different code is exactly the ambiguity this is meant to prevent. RC builds are
identified by their artifact name and the commit they were built from, not by a
different `versionCode`.
