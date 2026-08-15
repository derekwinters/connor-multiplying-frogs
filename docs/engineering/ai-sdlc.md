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
| `.claude/repo-config.yml` | What *this* repository is — its capabilities, owners, dashboard, commands |
| `.claude/ai-sdlc.pin` | The version of ai-sdlc being followed |
| `.claude/ai-sdlc/house-rules.md` | The shared rules, installed here so agents read them without a network |
| `.github/workflows/*.yml` callers | A trigger must be declared in the repository it fires for |

A caller is about fifteen lines: a trigger and a `uses:`. Everything it calls is in ai-sdlc, at the
pinned version.

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

`.claude/ai-sdlc.pin` records the version **and** the commit it resolves to:

    v0.4.2 b95d6bb30481e24e4b9eb8c6cdfda1a85cdb20d3

The callers reference that commit, with the version as a trailing comment — the same form as every
other pin here, and no exception to [the SHA-pinning rule](ci-cd.md). A reusable workflow runs with
this repository's token, on `issue_comment` and `issues`, so a mutable ref there is the same
exposure as a mutable action; ai-sdlc being ours says who could move a tag, not that it cannot move.

An upgrade is still a pull request that moves one line, because `adopt apply <version>` resolves the
version and rewrites both the SHA and the comment. Nobody resolves a SHA by hand, and nobody has to
read forty characters of hexadecimal to tell how far behind they are.

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
