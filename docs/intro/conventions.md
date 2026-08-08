# Conventions

How this repo names, labels, and organises things. The AI issue pipeline reads
these conventions as rules, not suggestions — an issue with the wrong labels is
an issue the pipeline will route wrongly.

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
