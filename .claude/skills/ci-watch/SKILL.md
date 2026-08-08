---
name: ci-watch
description: Watch a pull request's checks until they finish and report pass or fail with log excerpts. Use after pushing, when asked whether CI is green, or when a PR's checks need following to completion. Reports only — it never fixes anything.
---

# ci-watch

Poll a PR's checks until they reach a terminal state, then say what happened.

```bash
python3 .claude/skills/ci-watch/ci_watch.py --pr 123     # poll to completion
python3 .claude/skills/ci-watch/ci_watch.py --pr 123 --once  # report right now
```

## It reports. It never fixes.

Not a style preference — **a watcher that also fixes is a watcher whose report
you cannot trust**, because it is describing a situation it has already changed.
"CI was red, then I changed three things, and now it is green" is not a CI
report; it is a second change nobody reviewed.

Resolution belongs to whoever called it: the development agent for its own PR,
the delivery skill for one it is driving. This skill does not push, does not
re-run jobs, does not approve parked checks, and does not edit the PR.

## The four states

| State | Exit | Means |
| --- | --- | --- |
| `passed` | 0 | every check completed, none failed |
| `failed` | 1 | at least one check failed — actionable now |
| `parked` | 2 | a check is waiting for a human to approve it |
| `pending` (timed out) | 2 | the checks never finished |

Three exit codes because they need three different responses. Two of them are
not "fix the build":

- **`parked`** means nothing will change until someone clicks approve in the
  PR's Checks tab. Polling through it just burns the timeout and reports the
  same thing, so it is treated as **terminal**. Tell the human; do not wait.
- **A timeout is not a pass and not a failure.** The report says the checks
  never finished, which is a different problem from them failing — usually a
  runner queue rather than the change.

### Two things it deliberately does not call a pass

- **No checks at all** is `pending`, not `passed`. An empty list means the
  checks have not registered yet, and "no checks ran" is the worst possible
  answer to "is CI green".
- **`cancelled` and `timed_out` are failures.** They did not pass, and treating
  them as a pass is how a cancelled run becomes a green merge. The classifier
  lists the *passing* conclusions (`success`, `neutral`, `skipped`) rather than
  the failing ones, so a conclusion GitHub adds later reads as "not a pass"
  instead of being silently tolerated.

A failure outranks a parked run: if something has already failed, that is the
news, and the parked check is a detail on top of it.

## Bounded polling

`POLL_SECONDS = 15`, `TIMEOUT_SECONDS = 1200`. Long enough not to hammer the
API, short enough that a two-minute check does not feel like five, and it always
stops.

## The report

```text
1 check(s) failed.
  ok    Core (NUnit)
  ok    Debug APK
  FAIL  EditMode (Unity)
  ok    lint
```

For a failure, add the excerpt from that job's log:

```bash
gh run view --job <job-id> --log-failed
```

`extract_excerpt` picks the lines worth reading — NUnit `Failed`/`Expected:`/
`But was:` lines, `error CS…`, Actions' own `##[error]` markers, tracebacks —
with two lines of context each, and falls back to the tail of the log when
nothing matches, because the end of a log is where a crash usually is. It is
capped at 40 lines: one failure with its message beats a hundred lines of
pasted output.

## Verified

Checked against real PRs in this repo — #130 (all green) classified `passed`,
#123 (EditMode red, everything else green) classified `failed` with only the
Unity job marked.

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py ci-watch
```

20 tests. The classifier and the excerpt extractor are pure functions, and
`watch` takes its clock and its sleep as arguments, so the polling and timeout
behaviour is tested without waiting for anything.
