---
name: issue-blockers
description: Read and write native GitHub blocked-by relationships between issues. Use when an issue cannot start until another is done, when a prose "Blocked by #N" needs converting, or when checking what is blocking an issue.
---

# issue-blockers

```bash
export GITHUB_REPOSITORY=derekwinters/connor-multiplying-frogs

python3 .claude/skills/issue-blockers/set_blocker.py list   --issue 28
python3 .claude/skills/issue-blockers/set_blocker.py add    --issue 28 --blocked-by 82
python3 .claude/skills/issue-blockers/set_blocker.py remove --issue 28 --blocked-by 82
python3 .claude/skills/issue-blockers/set_blocker.py audit  --issue 28
```

## Prose blockers are not allowed

**`Blocked by #42` written in an issue body is not a blocker.** It is a sentence.

Everything that acts on blockers reads the *native* dependency graph: the
nightly builder computing its ready queue, the dashboard's blocked section, and
the sweep that wakes an issue when its blocker closes. None of them read prose.
An issue "blocked" in a sentence is an issue the builder walks straight into and
starts.

So when you discover a dependency — even on an issue that is not yours — record
it natively, there and then. `audit` finds the ones already written as prose:

```text
#42 is named as a blocker in the body but has no native relationship.
Run: set_blocker.py add --issue 28 --blocked-by 42
```

### `Depends on:` is different, and stays prose

`Depends on: #42` is **soft ordering** — "this will go better afterwards" — and
it has no native form on purpose.

Converting one into a native blocker turns a preference into a hard gate the
builder refuses to pass, which is how a queue deadlocks on an issue that could
have been worked at any time. `audit` deliberately ignores `Depends on:` lines,
and there is a test pinning that.

| In the body | Means | Native? |
| --- | --- | --- |
| `Blocked by #42` | cannot start until #42 is done | **yes** — convert it |
| `Depends on: #42` | easier after #42, but not blocked | no — leave it |

## The trap: the write API takes an id, not a number

`GET .../dependencies/blocked_by` returns issues with their `number`. The
**write** endpoints take `issue_id` — the internal ten-digit identifier, not the
number everyone reads and writes:

```http
POST /repos/:owner/:repo/issues/28/dependencies/blocked_by
{"issue_id": 5098389812}      ✅   the internal id of issue #82
{"issue_id": 82}              ❌   either a 422, or a link to a random issue
```

`resolve_issue_id` does the lookup, and **refuses to fall back to the issue
number** if it cannot — a wrong id here does not fail, it creates a
relationship pointing at whatever issue happens to have that internal id.

## Verified

Against this repo: `list --issue 28` reports `#82 [open] Derek: add the
UNITY_LICENSE repository secret`, which is the real relationship on that issue,
and `list --issue 53` reports no blockers.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py issue-blockers
```

13 tests. The API is injected as a callable, so every path — including that the
issue *number* is never sent as the id — is asserted without a network call.
