---
name: scaffold-core
description: Create a new Core type and its failing test, following the project's namespaces, folder layout, and .meta conventions. Use before writing any Core logic, at the start of the red phase.
---

# scaffold-core

```bash
python3 .claude/skills/scaffold-core/scaffold.py Lane
python3 .claude/skills/scaffold-core/scaffold.py Card --subfolder Cards
```

## Run this before any logic exists

The point is *where it leaves you*: an empty class and a test that fails. That
is the start of the red phase, which is where a new type is supposed to begin.

Scaffolding after writing the implementation gets you nothing — the layout is
already decided by then, and the test you add afterwards is one written by
someone who already knows the answer.

## What it writes

| File | |
| --- | --- |
| `Assets/Scripts/Core/<sub>/<Name>.cs` | the type, in `Frogs.Core[.<Sub>]` |
| `Assets/Scripts/Core/<sub>/<Name>.cs.meta` | a pinned GUID |
| `Tests/Core/<sub>/<Name>Tests.cs` | a test that **fails** |

Namespaces and layout follow
[tech-stack.md](../../../docs/engineering/tech-stack.md): a subfolder becomes a
namespace segment, so `--subfolder Cards` gives `Frogs.Core.Cards` and
`Frogs.Core.Tests.Cards`.

### Three decisions worth knowing

**The class is empty.** A class arriving with a plausible implementation in it
is an invitation to skip the test that should have driven it — which is the one
thing this skill exists to prevent.

**The test calls `Assert.Fail`, not nothing.** An empty stub passes, and a
passing suite right after scaffolding tells you nothing. Running the suite
immediately after this must go red. The failure message says what to do next:

```text
Failed Lane_DoesTheThingTheIssueAskedFor
  Write the first real test for Lane.
```

**The test file gets no `.meta`.** `Tests/Core` is outside `Assets/`, so Unity
never sees it — a `.meta` there would be a file nothing reads. The *class* gets
one, with a pinned GUID, so Unity does not invent an identity on first import.

## It refuses rather than guessing

- **A lowercase or dotted name is an error**, not something to correct. A
  scaffolder that silently fixes what you asked for writes a file you then
  cannot find.
- **It never overwrites.** An existing file is a `FileExistsError` saying to
  delete it first if that is really what you want.

## After scaffolding

1. Replace `Assert.Fail` with **one real behaviour**, named for the behaviour.
2. Run the suite. **Watch it fail**, and check it failed for the reason you
   meant.
3. Write the smallest code that passes it.

```bash
dotnet test Tests/Core/Frogs.Core.Tests.csproj
```

## Running the tests

```bash
python3 .github/scripts/run_python_tests.py scaffold-core
```

24 tests. Everything that generates text is a pure function, and the writing is
tested against a temporary directory.
