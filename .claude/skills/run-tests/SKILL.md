---
name: run-tests
description: Run the Multiplying Frogs test suites and report the result. Use during a red-green loop, before committing, before opening a PR, or whenever asked whether the tests pass.
---

# run-tests

Run the suites the same way every time, and report the result the same way
every time. Improvising a command per session is how "the tests pass" comes to
mean four different things.

## The Core suite

This is the one you run constantly.

```bash
dotnet test Tests/Core/Frogs.Core.Tests.csproj
```

No editor, no licence, no display, no container — a couple of seconds. Fast
enough to run on every save, which is the whole point: a red-green loop you can
run mid-thought is one you actually use.

One fixture, while you are in a loop:

```bash
dotnet test Tests/Core/Frogs.Core.Tests.csproj --filter FullyQualifiedName~AppVersion
```

### Reading the output

The line that matters is the last one:

```text
Passed!  - Failed: 0, Passed: 42, Skipped: 0, Total: 42, Duration: 57 ms
Failed!  - Failed: 3, Passed: 8,  Skipped: 0, Total: 11, Duration: 68 ms
```

- **`Failed: 0` and `Total > 0`** is a pass. Check both. A run that compiled
  nothing and executed nothing also says `Failed: 0`.
- **A build error is a red suite**, not a broken command. `error CS0103: The
  name 'AppVersion' does not exist` during a red phase is *exactly right* — it
  is what "the type does not exist yet" looks like.
- **Read the failure message, not just the count.** In a red phase you are
  checking that it failed for the reason you intended.

## The EditMode suite cannot run here

**Agent environments have no Unity editor and no licence.** `Assets/Tests/EditMode`
cannot be executed locally. This is a fact about the environment, not a
shortcoming of the change.

The flow:

1. Run the Core suite. This one is never blocked, so "I couldn't run the tests"
   is never true of it.
2. Write the EditMode tests the change needs, and say in the PR that they were
   written but not executed locally.
3. Push. CI runs both.
4. **Watch CI.** If EditMode goes red, that is yours, exactly as if you had seen
   it fail locally.

**Do not report this as a deviation.** It is the expected, documented flow —
see
[testing.md](../../../docs/engineering/testing.md#this-is-not-a-reportable-deviation).

## The other checks

Not tests, but they fail PRs, and they all run here in seconds:

```bash
python3 .github/scripts/check_core_isolation.py     # Core has no Unity dependency
python3 .github/scripts/check_geometry_literals.py  # no new bare tuning literals
python3 .github/scripts/run_python_tests.py         # the skills and CI scripts
mkdocs build --strict                               # the docs site
```

Run the ones the change could affect. Run all of them if unsure — the whole set
is faster than opening the CI page.

## Report format

Same shape every time, so it can be skimmed:

```text
Core:      42 passed, 0 failed (57 ms)
Isolation: pass
Geometry:  pass
Scripts:   105 passed
Docs:      built
EditMode:  not run — no editor in this environment; CI will run it
```

When something is red, add the failing test's name and what it said:

```text
Core:      39 passed, 3 failed
  Parse_RejectsAnythingThatIsNotThreeNumbers("0.2")
    Expected: <System.FormatException>
```

One failure with its message beats a hundred lines of pasted output. If several
failed for the same reason, say so once and give the count.
