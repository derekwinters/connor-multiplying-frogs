# Conventions

How this repo names, labels, and organises things. The AI issue pipeline reads
these conventions as rules, not suggestions — an issue with the wrong labels is
an issue the pipeline will route wrongly.

## GitHub is the source of truth; this page is the summary

Decisions are made in GitHub — in an issue body, in a comment thread, in a PR
review — and that is where they live. This site is the **distillation**: the
settled shape of those decisions, written up so nobody has to reconstruct them
from a fifty-comment thread.

Two consequences worth being explicit about:

- **When this page and GitHub disagree, GitHub wins.** A comment from Derek
  three days ago beats a paragraph here from three weeks ago. If you notice a
  disagreement, the fix is to update this page — in the PR you noticed it in,
  not in a follow-up issue.
- **This page never records live state.** No lists of current milestones, no
  counts of open issues, no "we are currently working on…". State is queried
  from the API; conventions are written down. A page that mixes the two is a
  page that is always slightly wrong.

The reverse holds too: a decision that only exists in this site and was never
agreed in an issue is a decision nobody made. Write it up in an issue first.

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
python3 .claude/skills/milestone-ops/milestone_ops.py list
```

The live set is the truth. This page describes the *shape* of a milestone; the
API says which ones there are.

### Setting a milestone takes its number, not its title

Worth knowing before it costs you an afternoon: the API's `milestone` field
takes the milestone's **number**. Passing `"v0.1"` is a 422 at best, and
silently wrong at worst. The `milestone-ops` skill resolves a title to a number,
and compares titles **exactly** — `v0.1` and `V0.1` are different milestones,
and normalising them together is how work lands in the wrong one.

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

## Skill names

**A skill takes the `dw-` prefix only when its name would otherwise be confused
with a skill from outside this repo.** Most of our skills need no prefix:
`pipeline-gatekeeper`, `release-flow`, `scaffold-core` and the rest are named
unambiguously already, and a prefix on a name nobody could mistake is noise in
every directory listing.

One skill carries it today, and it earned it by colliding:

| Skill | Would be confused with |
| --- | --- |
| `dw-run-tests` | `run`, a built-in skill |

It is not an exact name clash — it is the *near* miss, which is the one that
actually bites. Asked to "run the tests", an agent picking by name alone can
reach for the wrong one, and the wrong one does not know about our Core suite.

**The prefix comes off when the collision goes away.** `triage-issue` carried it
while a vendored `triage` skill sat beside it; that skill has since been removed,
so the prefix was marking a collision that no longer existed — which is worse
than no prefix at all, because it tells a reader to go looking for a rival skill
they will not find.

**Before naming a new skill, check for collisions** — against the vendored names
in `.claude/.skills-manifest.json` and against the skills the harness already
offers. An exact match or a shared-prefix near-miss earns a `dw-`; anything else
does not. Vendored skills never take the prefix: renaming a third-party copy
breaks the cross-references inside its siblings and makes every re-vendor a
manual fixup.

Renaming an existing skill is never just a `git mv` — the name is load-bearing in
three places:

- **Workflow paths.** `dashboard.yml`, `gatekeeper-comment.yml` and
  `gatekeeper-sweep.yml` invoke scripts by path, and `pipeline-tests.yml`
  path-gates on `.claude/skills/pipeline-*/**`. A missed glob is a workflow
  that silently stops running rather than one that fails.
- **The frontmatter `name:`**, which must match the directory name.
- **The manifest**, where the record's `name` is the directory.

## Labels

Labels are the pipeline's state machine. The list below is mirrored by **two**
machine-readable manifests, and the `labels-sync` workflow applies their union
whenever either changes:

| File | Holds | Who edits it |
| --- | --- | --- |
| [`.github/labels.core.yml`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/.github/labels.core.yml) | the shared pipeline vocabulary | **nobody here** — `adopt` installs it, pinned |
| [`.github/labels.repo.yml`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/.github/labels.repo.yml) | frogs' own `area:*`, `type:*`, `dashboard` | us |

The core manifest is identical in every repository that has adopted
[ai-sdlc](../engineering/ai-sdlc.md) — the shared code reads those names, so a
repository that redefines one has a pipeline that does not work while appearing
adopted. To change a core label, change it in ai-sdlc and move the pin. A label
defined in both files is a hard error rather than a silent precedence rule.

**Edit `labels.repo.yml` and this page together** — never the GitHub label UI,
whose edits the next sync undoes.

> **How the spec is changing (#342).** This page used to describe a single
> `.github/labels.yml` maintained here and applied by our own
> `sync_labels.py`. The taxonomy is now split — shared vocabulary installed and
> pinned, local labels ours — and applied by ai-sdlc's sync. The colours of the
> nine shared labels changed to the shared values as part of that, and
> `no-closing-keyword` was added. Nothing was renamed or deleted.

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

#### The invariant: `ready-for-work` ⇒ has a milestone

**An issue labelled `ready-for-work` always has a milestone.** No exceptions.

This is the one label rule the tooling enforces rather than merely assumes. The
nightly builder picks work from the focus milestone, so a `ready-for-work` issue
with no milestone is work that has been approved and will then never be picked
up by anything — it falls out of the pipeline silently, which is the worst
failure mode a queue can have.

Two places hold the line:

- The gatekeeper refuses `/approve` on an issue with no milestone, and says so
  in its reply rather than failing quietly.
- The reconciler treats `ready-for-work` without a milestone as drift, and flags
  it on the dashboard.

If you are labelling by hand, set the milestone first.

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

Run the **labels-sync** workflow from the Actions tab, or push a change to
either manifest on `main` — it fires on both paths.

The sync is idempotent: running it twice changes nothing the second time, which
is what makes it safe on every push rather than something to run carefully. It
creates and updates by name, and deletes **only** what the `delete:` list names.
A label absent from both manifests is left alone, so there is no way to lose a
label by forgetting to mention it.

## Issues

### Every issue names the spec pages it touches

Every issue body ends with a line naming the pages in `/docs` it affects:

```
**Spec pages touched:** docs/specs/reference/index.md, docs/specs/ui/game-board.md
**Spec pages touched:** none — repo config.
```

`none` is a legitimate answer and has to be written out; a missing line is not
the same as `none`, because a missing line usually means nobody checked.

This line does real work. It is what the reviewer checks the PR's `**Docs:**`
line against, it is how you find out that two queued issues are about to edit
the same spec page, and it is the earliest point at which "this issue changes
the contract" becomes visible — which is the point at which it is cheapest to
argue about.

### Epics and sub-issues

Anything too big to finish in one PR is a `type:epic`, and it is split into
`type:task` children using **native GitHub sub-issues** — not a checklist of
links in the body.

- The epic carries the *why* and the shape of the whole thing. It does not carry
  a build checklist of its own beyond "all children done".
- Each child is one PR's worth of work, with its own build checklist, its own
  spec-pages line, and its own labels.
- The children hold the `area:*` label that fits them, which may differ from the
  epic's.
- An epic is never `ready-for-work`. Agents work children; a `ready-for-work`
  epic is a mislabelled epic.
- The epic closes when its last child closes. Closing an epic with open children
  is how work gets lost.

Sub-issues are native because the pipeline computes the ready queue from the
real graph. A dependency written in prose is a dependency the builder cannot
see. The same goes for ordering: when child B cannot start until child A is
done, that is a **blocked-by relationship**, not a sentence.

### When a question issue closes, and when it stays open

A `type:question` issue is a decision that has to be made before something can
be specified. They are the one issue type that does not follow the ordinary
"work it, close it" path, so:

**It closes when** the decision is made *and* recorded somewhere durable — the
relevant spec page is updated, or a task issue exists that carries the decision
into work. The question's own comment thread is not durable enough on its own,
because nobody reads a closed issue's thread. Close it with a comment stating
what was decided, in one sentence, so the thread has an answer at the bottom.

**It stays open when** the decision is made but nothing has been written down
yet, when it was answered partially ("a wrong answer moves you back one" — but
not what happens on the Start log), or when the answer was "not yet". A question
that was answered "not now" is a question that gets asked again later, and
reopening a closed issue loses the thread.

**It becomes a task** — closed as a duplicate, with the task linked — when the
answer turns out to be small enough to just build. Do not quietly retitle a
question into a task; the distinction is what makes the question queue
meaningful.

Questions Connor has to answer get the `Direct Involvement Needed` milestone and
no version milestone, because they are not shippable work.

## Docs versioning

The site is published with [mike](https://github.com/jimporter/mike), which
keeps one built copy of the docs per version alias in the `gh-pages` branch.

- **`latest`** is the default alias and the one the site opens on. It tracks the
  most recent release.
- Each release publishes under its own version, so the docs for a version you
  can still build are docs you can still read.
- The version selector in the header is Material's, wired to mike via
  `extra.version.provider: mike` in `mkdocs.yml`.

Two rules that follow from this:

- **Never edit `gh-pages` by hand.** It is entirely generated; mike rewrites it
  on every publish, and a hand edit disappears at the next release without
  warning.
- **Docs land with the change they describe**, in the same PR. The version alias
  a page appears under is decided by when it was *published*, so a doc update
  that trails its code by a release is a doc that is wrong in exactly the
  version someone is reading it for.
