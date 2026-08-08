# Conventions

How this repo names, labels, and organises things. The AI issue pipeline reads
these conventions as rules, not suggestions — an issue with the wrong labels is
an issue the pipeline will route wrongly.

## Milestones

Milestones are how work is *planned*. Labels say what an issue is; the
milestone says when we intend to do it.

### Titles are versions, descriptions are scope

A version milestone is titled with a bare version — `v0.0.1`, `v0.1`, `v1.0` —
and nothing else. No dates, no theme in the title. The **description** carries
the scope: a short heading plus a paragraph or two saying what shipping that
version means and, just as usefully, what it does not include.

Triage reads those descriptions to decide where a new issue belongs, so a
milestone with an empty description is a milestone nothing can be routed into.
Write the scope when you create the milestone, not later.

### Milestones are planning labels, not the shipped version

A milestone is decoupled from `/VERSION`. `/VERSION` is what the built app
actually reports and is moved only by release-please, from conventional commits.
A milestone titled `v0.1` is a statement of intent about a batch of work; it can
be renamed, re-scoped, or have issues pulled out of it right up until it closes.
Nothing in the build reads a milestone title. Don't try to keep the two in
lockstep — they answer different questions.

### Read milestones live, never from a list in the docs

**This page deliberately does not list the current milestones.** Any list
written here is a list that goes stale the first time someone adds a milestone,
and a pipeline that trusts a stale list routes issues into milestones that no
longer exist. Every skill, workflow, and script that needs to know the
milestones queries the API for them:

```bash
gh api repos/:owner/:repo/milestones --paginate --jq '.[] | "\(.title): \(.description)"'
```

The live set is the truth. This page describes the *shape* of a milestone; the
API says which ones there are.

### Freezing a milestone to new intake

When a version's scope is settled and you want triage to stop adding to it,
freeze it — don't close it, because it still has open work:

1. Prefix the milestone description with `**FROZEN — no new intake.**`
2. Optionally set a due date, which signals the same thing to humans.

Triage treats a description starting with `FROZEN` as ineligible when choosing a
milestone for a new issue, and routes to the next open version instead. Existing
issues in a frozen milestone keep moving through the pipeline normally.

### A shipped version is a tag, not a lingering milestone

When the last issue in a version milestone closes and the release goes out, the
milestone **closes**. What survives the release is the git tag and the GitHub
release that release-please created — those are the permanent record of what
`v0.1` contained. A closed milestone is a historical planning artifact; a tag is
the artifact you can check out and build.

Never reopen a shipped milestone to hold follow-up work. Follow-up work is new
work: it goes in the next open version milestone, or in
`Direct Involvement Needed` if a human has to do it.

### The non-version milestone

`Direct Involvement Needed` is the one milestone with no version and no ship
date. It holds work no agent can finish alone — anything needing repo-admin
rights, a secret, an external account, a purchase, a device in hand, a Unity
Editor session, or a taste call that is Connor's to make.

It never closes. Issues leave it by a human doing them, or by being re-scoped
into a version milestone once the human-only part is unblocked.

## Labels

Labels are the pipeline's state machine. The list below is mirrored by
[`.github/labels.yml`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/.github/labels.yml),
which is the machine-readable source of truth; the `labels-sync` workflow
applies that file to the repo whenever it changes. **Edit `labels.yml` and this
page together** — never the GitHub label UI, whose edits the next sync undoes.

### `area:*` — what part of the game

Exactly one per issue.

| Label | Colour | Meaning |
| --- | --- | --- |
| `area:gameplay` | `#1D76DB` | Frog behaviour, multiplying rules, scoring, and game feel |
| `area:art` | `#B392F0` | Sprites, animation, palettes, and visual assets |
| `area:audio` | `#8E44AD` | Music, sound effects, and audio mixing |
| `area:ui` | `#5319E7` | Menus, HUD, layout, and on-screen controls |
| `area:story` | `#D93F0B` | Narrative, level themes, and world-building |
| `area:build` | `#546E7A` | Unity project config, CI, packaging, and releases |
| `area:ai` | `#006B75` | Claude skills, agents, and the issue pipeline itself |

### `type:*` — what kind of work

Exactly one per issue.

| Label | Colour | Meaning |
| --- | --- | --- |
| `type:epic` | `#3E4B9E` | A milestone-sized parent that gets split into tasks |
| `type:task` | `#C2E0C6` | A single self-contained unit of buildable work |
| `type:question` | `#CC317C` | A decision to make before work can be specified |
| `type:bug` | `#D73A4A` | Something built already behaves incorrectly |
| `type:wireframe` | `#F9D0C4` | A UI layout to agree on before any UI code is written |

### Pipeline state

Exactly one per open issue. The pipeline moves an issue between these; humans
change them only to override it.

| Label | Colour | Meaning |
| --- | --- | --- |
| `ai-triage` | `#FBCA04` | Queued for automated triage; the pipeline owns it next |
| `pending-approval` | `#E99695` | Triaged and waiting on a human `/approve` comment |
| `needs-clarification` | `#F29513` | Blocked on an answer from a human before it can proceed |
| `ready-for-work` | `#0E8A16` | Approved and eligible for the nightly builder to pick up |
| `in-progress` | `#2188FF` | An agent is actively building this issue right now |
| `parked` | `#BFBFBF` | Deliberately set aside; the pipeline will not pick it up |
| `dashboard` | `#FEF2C0` | The single live pipeline dashboard issue |

The normal path is `ai-triage` → `pending-approval` → `ready-for-work` →
`in-progress` → closed. `needs-clarification` and `parked` are the two ways out
of that path, and both need a human to get back on it.

### CI escape hatches

| Label | Colour | Meaning |
| --- | --- | --- |
| `skip-docs` | `#EDEDED` | Exempt this PR from the docs reconciliation gate |

### GitHub's stock labels

Deliberate decision, not an oversight:

- **Deleted** — `bug`, `documentation`, `enhancement`, `good first issue`,
  `help wanted`, and `question`. Each duplicates a `type:*` or `area:*` label,
  and a label that means the same thing twice is a label the pipeline can
  disagree with itself about.
- **Kept** — `duplicate`, `invalid`, and `wontfix`. These describe why an issue
  was closed rather than what it is, so they collide with nothing above.

## Applying the taxonomy

```bash
# Preview: validates labels.yml (distinct names, distinct colours, every label
# described) without touching the repo.
python .github/scripts/sync_labels.py --dry-run

# Apply, if you have a token with `issues: write`.
GITHUB_TOKEN=... GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs \
  python .github/scripts/sync_labels.py
```
