---
name: release-flow
description: Drive a release-please release PR to a tagged, published release. Use when asked to cut, ship, or publish a release, when checking whether a release completed, or when the release PR looks stale or stuck.
---

# release-flow

Merging the release PR **is** the release. This skill exists because two things
go wrong every time, and both are silent.

## The two gotchas

### 1. The release PR can be stale

release-please rewrites its PR on every push to `main`, but that run takes a
minute and can fail. Merging a stale release PR ships a changelog and a version
that **omit whatever landed after it was last written**.

Nothing catches this afterwards. The release succeeds, the tag is created, the
APK is attached — and the version is simply lower than it should have been, with
commits missing from the changelog forever. Check before merging, every time.

### 2. Check runs can be parked, waiting for a human

A run in `action_required` or `waiting` is neither passing nor failing. It never
finishes on its own. Merging past it means merging with the checks **not having
run**, which from the merge button looks exactly like merging with them green.

Derek approves them in the PR's Checks tab. **This skill never approves them** —
approving a parked run on your own PR is the thing being parked is meant to
prevent.

## What this skill never does

- **It never toggles the release PR's own state.** No labels, no title edits, no
  closing and reopening, no pushing to `release-please--branches--main`.
  release-please owns that branch and that PR; it recomputes them from scratch
  on every run, so an edit is either undone or corrupts the next computation.
- **It never approves parked check runs.**
- **It never creates the tag or the release by hand.** Those are release-please's
  output. If they are missing after a merge, that is a bug to investigate, not a
  gap to fill in manually.

The only mutating step in the whole flow is the squash-merge, and that is
Derek's call.

## The flow

Each step gathers a snapshot with `gh` and hands it to `release_flow.py`, which
is pure and offline.

### 1. Find the release PR

```bash
gh pr list --state open --json number,title,headRefName,baseRefName,labels
```

It is the one whose head branch starts with `release-please--branches--`. Match
on that, not on the title — the title is configurable and has already changed
once.

If there isn't one, there is nothing to release: every commit since the last
release was a `docs:`/`chore:`/`ci:` that doesn't move the version.

### 2. Confirm it regenerated

```bash
gh api repos/:owner/:repo/pulls/$PR --jq \
  '{pull_request: {base: {sha: .base.sha}}}' > /tmp/s.json
gh api repos/:owner/:repo/commits/main --jq '.sha' \
  | xargs -I{} jq '. + {main_sha: "{}"}' /tmp/s.json > /tmp/snapshot.json

python3 .claude/skills/release-flow/release_flow.py regenerated --snapshot /tmp/snapshot.json
```

If it fails, wait for the `release-please` workflow to finish and re-check.
Don't merge, and don't "fix" the PR.

### 3. Check for parked runs

```bash
gh pr checks $PR --json name,state > /tmp/checks.json
jq '{check_runs: [.[] | {name, status: .state}]}' /tmp/checks.json > /tmp/parked.json

python3 .claude/skills/release-flow/release_flow.py parked --snapshot /tmp/parked.json
```

If it fails, **stop and tell Derek** which runs need approving. This is a halt,
not a retry: nothing changes until a human clicks.

### 4. Squash-merge, with the right title

The title becomes the commit on `main`, so it has to be a valid Conventional
Commit:

```bash
TITLE=$(python3 .claude/skills/release-flow/release_flow.py title --version 0.1.0)
gh pr merge $PR --squash --subject "$TITLE"
```

Merging is Derek's decision. Ask, unless he has already said to go ahead.

### 5. Verify all three artifacts appeared

```bash
python3 .claude/skills/release-flow/release_flow.py verify --snapshot /tmp/after.json
```

with a snapshot of:

```json
{
  "version": "0.1.0",
  "tags": ["v0.1.0"],
  "releases": [{"tag_name": "v0.1.0", "draft": false}],
  "pull_request_labels": ["autorelease: tagged"]
}
```

All three, because each can happen without the others:

- **the `v0.1.0` tag** — from `gh api repos/:owner/:repo/tags`;
- **a published (not draft) GitHub Release** — a tag with no release is a
  release nobody can download;
- **`autorelease: tagged`** on the merged PR — without it release-please thinks
  the release never happened and will try again.

Then check the release has its APK attached. If not, the manual backfill
workflow exists for exactly that.

## Running the tests

```bash
python3 .github/scripts/run_skill_tests.py release-flow
```

Every check in `release_flow.py` is a pure function over a snapshot dict, so the
tests need no network and no fixtures.
