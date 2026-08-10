---
name: core-unity-split
description: Decide which assembly a new class belongs in, and how the Core and Unity assemblies reference each other. Use at the start of a feature, when adding a class, or when something in Core seems to need an engine API.
---

# core-unity-split

The question at the start of nearly every feature: **where does this class go?**

The full reasoning is in
[tech-stack.md](../../../docs/engineering/tech-stack.md#code-architecture-the-coreunity-split).
This is the short answer.

## The decision rule

> **Could this class be right or wrong without a screen?**
>
> Yes → `Frogs.Core`. No → `Frogs.Unity`.

Where a frog lands after a wrong answer can be wrong with no screen attached. A
sprite being in the right place cannot. That one question settles almost every
case.

Two ways to check yourself:

- **If you can write a test for it that doesn't mention pixels, positions, or
  frames, it belongs in Core.** If the only test you can imagine is "it looks
  right", it belongs in the shell — or it is a decision, not code, and belongs
  in a wireframe.
- **If a `MonoBehaviour` contains a rule** — how far a frog moves, what a wrong
  answer costs, what winning means — that rule is in the wrong assembly.

## The layout

```text
Assets/Scripts/
  Core/    Frogs.Core.asmdef    no references, noEngineReferences: true
  Unity/   Frogs.Unity.asmdef   references Frogs.Core
```

**The reference direction is one-way: `Frogs.Unity` → `Frogs.Core`, never the
reverse.** Core does not know the shell exists. If you find yourself wanting
Core to call something in the Unity layer, you want an interface (below).

`Frogs.Core` is also `autoReferenced: false`, so nothing picks it up
implicitly — an assembly that wants Core says so.

## What Core may not contain

No `using UnityEngine`. No `MonoBehaviour`, `ScriptableObject`, `Vector3`,
`Time.deltaTime`, `Debug.Log`, coroutines, or `[SerializeField]`.

Two things enforce it, and neither is remembering:

```bash
python3 .github/scripts/check_core_isolation.py
```

- **`noEngineReferences: true`** makes it a compile error inside Unity.
- **The isolation check** guards the flag itself, and catches the case the flag
  and a grep both miss: `UnityEngine.Debug.Log(…)` fully-qualified, with no
  `using` directive to find.

## When a feature seems to need an engine API

It almost never does. What it needs is usually **time**, **randomness**, or
**where something is** — and all three have a shape that works in Core.

**Declare an interface Core owns, and implement it in the shell.**

```csharp
// Frogs.Core — no engine types.
public interface IClock { float SecondsSinceStart { get; } }

// Frogs.Unity.
sealed class UnityClock : IClock {
    public float SecondsSinceStart => UnityEngine.Time.time;
}
```

Core declares what it needs in the game's vocabulary; the shell provides it;
tests substitute a fake and control it exactly. That last part is the point —
`IClock` is not ceremony, it is what makes "what happens if `Roll` is tapped
while the frog's hop is still playing" a test rather than a thing you find out
on a tablet.

### Specific cases

| You think you need | Use instead |
| --- | --- |
| `Time.deltaTime` | an `IClock`, or pass elapsed seconds into the tick |
| `Random` | an interface Core owns, seeded in tests |
| `Vector3` | a Core type in the game's own vocabulary — a position in a lane, a lily pad index |
| `Debug.Log` | return the fact, or an interface the shell logs through |
| `MonoBehaviour` lifecycle | a plain method the shell calls from `Update` |

**"I'll just reference UnityEngine here" is never the shortcut it looks like.**
It costs five minutes now and pays it back on every test run afterwards: a
two-second `dotnet test` becomes a two-minute containerised editor, forever.

## Why it matters at all

`Tests/Core` runs with no editor, no licence, no display — seconds, on every
push. That is what makes strict TDD practical here rather than aspirational.
The split is not architectural taste; it is what buys the red-green loop.
