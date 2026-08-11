---
name: pipeline-dashboard
description: Render the pipeline dashboard issue from live repo state. Use to preview the board, to refresh it by hand, or when the dashboard looks out of date.
---

# pipeline-dashboard

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

# Preview — prints the board, writes nothing anywhere.
python3 .claude/skills/pipeline-dashboard/render_dashboard.py < state.json

# Refresh for real — PATCHes the dashboard issue body.
python3 .claude/skills/pipeline-dashboard/render_dashboard.py --write < state.json
```

## In production this is a workflow, not an agent

The dashboard is rendered by `dashboard.yml` — hourly, and after each pipeline
run. **There is no model in the loop and nothing about the body is generated
prose.** Every line comes out of `render()`, which is a pure function of the
state you hand it.

That matters because the board is what Derek reads to decide what to approve
next. A summary written by a model is a summary that can be subtly, fluently
wrong — and wrong in a way that reads perfectly. A table computed from labels
either matches GitHub or has a bug someone can find.

This skill exists for the times you want to look at the board without waiting
for the hour, or check what a render *would* produce before letting it run.

## Preview writes nothing

Without `--write`, the script prints to stdout and touches nothing. That is the
default deliberately: previewing is the common case, and the safe thing should
not need a flag.

Use it to check a `/focus` change before it lands, or to see whether a rendering
bug is in the data or the renderer.

## `--write` and what it can touch

`--write` PATCHes **one endpoint**: `/repos/:owner/:repo/issues/:number` for the
dashboard issue, with a body and nothing else.

- Authenticates with **`GITHUB_TOKEN`**, never a PAT. The workflow token is
  scoped to this repository and expires with the job.
- The issue number comes from the state passed in, so a render cannot wander
  onto a different issue.
- No labels, no comments, no milestones, no other issues.

That is the whole write surface. The renderer reads the entire board and can
modify exactly one body — which is what makes it safe to run hourly without
anybody watching it.

## Read-only everywhere else

The render **mutates nothing but the dashboard issue body.**

It reports on issues all over the repository — starred, blocked, parked,
flagged — and changes none of them. A star is a suggestion, not a state change.
A `⛔ blocked` flag is a description, not a label.

This is why an incorrect render is cheap: it produces a wrong page, which the
next render replaces. If the renderer also applied labels, a bug in the star
logic would be a bug that reorganises the board.

## Everything is regenerated

The whole body is rewritten every time, apart from the two config markers,
which are parsed out of the current body and written back verbatim:

```html
<!-- pipeline-focus: v0.0.1 -->
<!-- pipeline-cap: 3 -->
```

**So edit the markers, never the rendered sections.** A hand edit to a table
disappears at the next render — and looks like it worked until it does.

Rendering is **byte-stable**: the same state produces the same bytes. Without
that, every hourly run would PATCH the issue and the dashboard's history would
be a stream of meaningless edits.

Settings resolve **override → marker → default**. A `/focus` naming no live
milestone is rejected rather than stored, because a typo renders a board whose
pie and ready queue are empty — indistinguishable from a finished milestone,
and the most misleading output this script can produce. A malformed `cap`
marker falls back to 3, because a board that does not render is worse than one
with a default cap.

## Focus scopes the pie and the ready queue, and nothing else

Those two say *what is being built now*, and the builder builds the focus
milestone. Intake, Waiting for you, Needs clarification and Parked say
*somebody has to look at this*, which is true regardless of milestone — so
they list the whole board, and the **Milestone** column tells the rows apart.

Scoping them to focus hid the work most worth surfacing. An `ai-triage` issue
has not been triaged, and triage is what assigns the milestone, so a
focus-scoped Intake is near-empty by construction. `Direct Involvement Needed`
carries no version and never ships, so it can never be the focus — and a
focus-scoped "Waiting for you" hides the milestone that exists to say Derek
has to do something by hand.

An issue with **no pipeline-state label at all** is in no section either way.
It has not been admitted; `/admit` is what brings it in.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py pipeline-dashboard
```

66 tests. The centrepiece is a golden-snapshot test rendering a fixture board
and comparing byte-for-byte against a committed file. A dashboard is a wall of
generated Markdown, and a whole-board diff is the only review that catches a
section quietly changing shape.

**If the golden test fails and the change was intended**, regenerate the
expected file and *read the diff* before committing it. The golden file is only
as strong as the review it gets.

## See also

- `pipeline-reconcile` — produces the ⚠️ flags this renders
- `docs/engineering/issue-pipeline.md` — the sections, the pie, the markers
