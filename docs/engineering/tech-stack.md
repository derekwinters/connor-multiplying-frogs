# Tech stack

What we build with, what we build for, and the two structural rules that make
the rest of the engineering handbook possible.

This is the page [`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
rule #2 points at, and the page the geometry lint in CI enforces.

## Engine and language

| Thing | Choice |
| --- | --- |
| Engine | Unity |
| Unity version | **6000.0.81f1** (Unity 6 LTS) |
| Language | C# |
| Test framework | NUnit, via Unity Test Framework |
| Rendering | 2D, built-in render pipeline |

### The Unity version is pinned, and the pin is load-bearing

`6000.0.81f1` is written in `ProjectSettings/ProjectVersion.txt`, and the same
string appears in the CI workflows as the editor image tag. Those two have to
agree: CI runs a containerised editor, and an editor that is newer than the
project silently upgrades the project files, while an editor that is older
refuses to open it.

Unity 6 LTS because LTS means two years of patches without a forced upgrade
mid-project, which matters far more here than any feature in a newer stream.
This is a game built in evenings; an engine upgrade is not a thing to be doing
by accident.

### The render pipeline is built-in, for now

URP is the usual choice for a new 2D Unity project, and it will probably be the
right one here eventually. It is not the choice today, because URP needs a
renderer-data asset and a pipeline asset configured in the editor, and adding
the package without them leaves the project half-configured.

With zero art committed, switching later costs nothing — no materials to remake,
no lights to re-tune. So the pipeline is a decision deferred to the first issue
that actually needs 2D lighting, made deliberately at that point rather than
guessed at now.

**Bumping it** is its own PR, and the checklist is:

1. Confirm a [GameCI](https://game.ci) editor image exists for the exact
   version — `unityci/editor:<version>-android-<n>`. No image, no bump.
2. Update `ProjectSettings/ProjectVersion.txt`, every workflow that names the
   version, and the table above, together.
3. Open the project in the local editor once and commit whatever it migrates,
   in the same PR, so the migration is reviewed rather than discovered.

## Target platform

**Android tablets, held landscape.** Phones still run it — same OS, same
architecture, same APK — but the tablet is the device the game is designed
around, because that is what kids play on.

| | |
| --- | --- |
| Primary target | Android tablets, ARM64 (`arm64-v8a`) |
| Also runs on | Android phones |
| Scripting backend | IL2CPP for device builds |
| Minimum API level | 24 (Android 7.0) |
| Orientation | Landscape only (both ways up) |
| Reference resolution | 1920 × 1200 (16:10) |
| Output | `.apk` for direct install; `.aab` only if the game is ever published |

This page previously said *"Android phones. That is the platform, singular"*
and specified portrait, on the reasoning that the game is played one-handed on
a phone. **Derek changed it**: the game is primarily for kids' tablets, and a
tablet is held in landscape. The old rule's reasoning did not survive the
change of device, so both the device and the orientation moved together.

Portrait is not ruled out forever — but supporting it means designing a second
layout for every screen, which is the same argument the portrait-only rule used
to make in reverse. It stays off until a wireframe specifies one.

**1920 × 1200 is the resolution every layout is designed at.** It is 16:10,
which is the common kids'-tablet shape, and it is the viewport every UI mockup
is drawn at — see [the UI design process](ui-design-process.md). Devices that
are not exactly this still run the game; the number exists so that layouts are
designed against one agreed canvas instead of each being guessed at separately.

### Two build profiles

**Device build** — ARM64, IL2CPP, what actually gets installed on a tablet or a
phone. This is what the release build produces, and what an RC build has to
prove works.

**Emulator build** — x86_64, Mono. IL2CPP cross-compiling to x86_64 is slow and
buys nothing for a smoke test, so the emulator profile trades fidelity for a
build that finishes while you are still looking at it. Use it for "does the app
launch and show the right screen", never for judging performance or for anything
shipped.

CI builds both: the device profile is what the release ships, and an
emulator-targeted asset is attached alongside it so the release can be tried on
a desktop without rebuilding. A bug that reproduces only under the emulator is a
bug in the profile until proven otherwise.

### The settings above are applied by the build, not stored in the repo

`ProjectSettings/ProjectSettings.asset` is **not committed**. Unity generates a
default one in the CI container on every build, which means every value in the
tables above — product name, application id, minimum API level, orientation —
starts as whatever Unity happens to default to.

That is not theoretical. The first APK ever installed on a phone was called
**`workspace`**, after the container's working directory, and it rotated freely
in spite of this page promising a fixed orientation.

So `ProjectBootstrap.ApplyToBuild()` applies them, and
`BuildStampPreprocessor` calls it on **every** build — the same mechanism, and
the same reasoning, as the version stamp: a step that can be forgotten will be.
The order inside that pre-processor matters and is deliberate:

1. `ProjectBootstrap.ApplyToBuild()` — identity, minimum API, landscape-only.
2. The `.debug` application-id suffix, so it appends to the real id rather than
   to `com.DefaultCompany.workspace`.
3. The device/emulator profile, so it can still override architecture and
   scripting backend. Those two are deliberately *not* in `ApplyToBuild`.

Committing a reviewed `ProjectSettings.asset` is still worth doing — it would
make the settings a diffable source file rather than something reconstructed
each build — but it needs an editor session to generate honestly.

### What is explicitly not a target

iOS, desktop, console, web. Not "not yet" in a way that shapes decisions today —
if a choice is easier because Android is the only target, take the easier
choice. Adding a platform later is a real project, and pre-paying for it now
costs work we would rather spend on the game.

## Code architecture: the Core/Unity split

The single most important structural rule in this repo.

### The shape

```text
Assets/                       ← everything Unity compiles
  Scripts/
    Core/                     ← game logic. No UnityEngine. Ever.
      Frogs.Core.asmdef       ← no engine references, no auto-referenced assemblies
    Unity/                    ← the shell. MonoBehaviours, scene wiring, input.
      Frogs.Unity.asmdef      ← references Frogs.Core
  Tests/
    EditMode/                 ← EditMode tests for the shell; needs the editor
      Frogs.Unity.EditModeTests.asmdef
  Editor/                     ← editor-only tooling; never ships in a build
    Frogs.EditorTools.asmdef  ← references Frogs.Core and Frogs.Unity; Editor-only
  Scenes/                     ← HelloWorld.unity, created by editor code (below)
  Art/  Audio/  Prefabs/
Tests/
  Core/                       ← plain NUnit. Outside Assets/, so Unity ignores it
                                and `dotnet test` can run it with no editor.
ProjectSettings/              ← ProjectVersion.txt pins the editor version
Packages/manifest.json        ← only the packages actually needed
```

Two placements are worth explaining, because they look inconsistent and aren't:

- **EditMode tests are inside `Assets/`** — Unity only compiles code under
  `Assets/` and `Packages/`, so a Unity test assembly has nowhere else to live.
- **Core tests are outside `Assets/`** — deliberately. Unity ignores everything
  outside those two roots, which is exactly what lets `Tests/Core` be an
  ordinary .NET test project that `dotnet test` runs with no editor involved.
  Putting it under `Assets/` would drag it into Unity's compilation and cost it
  the property that makes it useful.

### The rule

**`Core` never references `UnityEngine`.** No `using UnityEngine`, no
`MonoBehaviour`, no `Vector3`, no `Time.deltaTime`, no `Debug.Log`, no
coroutines, no `ScriptableObject`. `Frogs.Core.asmdef` sets
`noEngineReferences: true` and has no references, so this is enforced by the
compiler rather than by remembering.

`Frogs.Core` is also `autoReferenced: false`: nothing picks it up implicitly, so
an assembly that wants Core has to say so. `Frogs.Unity` says so.

**Which means every folder of `.cs` needs an asmdef, including `Editor/`.** A
file in a folder no asmdef covers compiles into a *predefined* assembly —
`Assembly-CSharp` or `Assembly-CSharp-Editor` — and a predefined assembly has no
references list anyone can edit. It sees `autoReferenced: true` assemblies and
nothing else, so `using Frogs.Core;` there cannot compile, whatever the file
does. `Assets/Editor/` therefore has `Frogs.EditorTools.asmdef`.

That asmdef sets `includePlatforms: ["Editor"]`, and it has to. The `Editor`
folder name only makes code editor-only while the folder is part of a predefined
assembly; once it has an asmdef of its own, the folder convention no longer
applies and the platform list is what keeps build tooling out of the player.

#### The check that catches it

```bash
python .github/scripts/check_assembly_references.py
```

It reads every `using` under `Assets/`, works out which assembly the file
compiles into, and fails if that assembly cannot see the one the namespace
belongs to — the same verdict Unity gives, without an editor or a licence. CI
runs it beside `check_core_isolation.py` ([CI/CD](ci-cd.md)).

#### Two layers of enforcement

The asmdef flag is the real enforcement, but it only fires inside Unity —
minutes later, in CI, and only while the flag is still set. So there is a second
check that runs anywhere in under a second, with no editor and no .NET:

```bash
python .github/scripts/check_core_isolation.py
```

It fails if `Frogs.Core.asmdef` loses `noEngineReferences`, if it gains an engine
or editor reference, or if any `.cs` under `Assets/Scripts/Core/` imports —
or fully-qualifies a type from — `UnityEngine` or `UnityEditor`. That last case
is the one the asmdef flag and a grep for `using UnityEngine` both miss:
`UnityEngine.Debug.Log(…)` needs no using directive.

Run it before pushing. CI runs it too ([CI/CD](ci-cd.md)).

**The Unity layer is thin.** It reads input, hands it to Core, asks Core what the
world looks like now, and draws that. A `MonoBehaviour` that contains a rule —
how far a frog moves, what a wrong answer costs, what winning means — is a rule
in the wrong assembly.

### Why

Because it is the difference between a test suite that runs in two seconds and
one that runs in two minutes.

`Tests/Core` is a plain NUnit assembly. It compiles and runs without an editor,
without a licence, without a display — `dotnet test` in CI, in a few seconds,
on every push. That is what makes strict TDD (see [Testing](testing.md))
practical rather than aspirational: a red-green loop you can run mid-thought.

The moment one `Vector3` appears in Core, that assembly needs UnityEngine, which
needs an editor, which needs a licence and two minutes of container startup —
for every test run, forever. Five minutes saved once, paid back on every commit.

### When you genuinely need engine behaviour in Core

You need an **interface Core owns and the Unity layer implements**. Core
declares what it needs in its own vocabulary; the shell provides it.

```csharp
// In Core — no engine types.
public interface IClock { float SecondsSinceStart { get; } }

// In the Unity layer.
sealed class UnityClock : IClock {
    public float SecondsSinceStart => UnityEngine.Time.time;
}
```

Tests substitute a fake clock and control time exactly. That is the whole point,
and it is why "I'll just reference UnityEngine here" is never the shortcut it
looks like.

## Project settings are applied by code, not hand-edited

`Assets/Editor/ProjectBootstrap.cs` sets the product and company name, the
Android application identifier, the minimum API level, the target architecture,
the scripting backend, and the orientation — through the typed `PlayerSettings`
API.

Nothing in `ProjectSettings/` is hand-authored. Unity's YAML deserializer
silently ignores keys it doesn't recognise, so a hand-edited settings file with
one wrong key builds fine and is wrong at runtime; the same mistake made through
the C# API is a compile error. See
[Unity serialization](unity-serialization.md).

```bash
Unity -batchmode -quit -projectPath . \
      -executeMethod Frogs.EditorTools.ProjectBootstrap.Apply
```

It is idempotent, and whatever it changes under `ProjectSettings/` gets
committed. The version fields are deliberately absent from it — `/VERSION` owns
those, injected at build time ([Versioning](versioning.md)).

### So are scenes

`Assets/Editor/HelloWorldScene.cs` creates `Assets/Scenes/HelloWorld.unity` and
registers it in build settings, through `EditorSceneManager` and
`EditorBuildSettings` rather than by writing YAML.

Same reason, one step further. A `.unity` file is file IDs and GUID references
that have to be internally consistent and consistent with every `.meta` in the
project, and Unity ignores keys it doesn't recognise rather than complaining —
which is exactly what
[Unity serialization](unity-serialization.md#what-this-means-for-an-agent-with-no-editor)
forbids reasoning your way through. Asking Unity to write the file removes the
guess.

**It is run once, and the asset it produces is committed.** From the menu —
`Frogs → Create the Hello World scene` — or headlessly:

```bash
Unity -batchmode -quit -projectPath . \
      -executeMethod Frogs.EditorTools.HelloWorldScene.EnsureReadyToBuild
```

Nothing triggers it automatically and no build runs it. It is a tool for making
the asset; the asset is what ships.

One detail in it is load-bearing and was expensive to find: the scene is created
with `NewSceneMode.Single`. The obvious-looking alternative — `Additive`, plus
`SceneManager.MoveGameObjectToScene` for each object, so an open editor session
keeps its work — **produced no scene at all headlessly, and raised nothing.**
Twice. Don't swap it back without an editor open to watch what happens.

#### Four files, committed together

| File | Why |
| --- | --- |
| `Assets/Scenes/HelloWorld.unity` | the scene |
| `Assets/Scenes/HelloWorld.unity.meta` | its GUID, so references to it survive |
| `Assets/Scripts/Unity/HelloWorldProbe.cs.meta` | the scene refers to that script **by GUID**; without this the next checkout imports it under a new one and the scene loads with a silent *Missing Script* |
| `ProjectSettings/EditorBuildSettings.asset` | which scenes are in the build. A committed scene that is not in here is a scene the APK does not contain |

The tool is idempotent: it creates the scene only when there isn't one, so once
the files are committed it does nothing. `Assets/Tests/EditMode/HelloWorldSceneTests.cs`
asserts them — that the scene exists, that a build would include it, and that
its components survived serialization. That last one is the guard against the
failure this whole arrangement exists to avoid: a `.meta` that goes missing, a
GUID that changes, and a scene that loads with a *Missing Script* and says
nothing about it.

**The screen it renders is deliberately empty.** A camera and one component that
writes the version to the log, and nothing else: what a build-proof screen
*shows* is a layout, and layout goes through
[the wireframe loop](ui-design-process.md) rather than being invented in an
implementation PR.

### Naming

Use the `scaffold-core` skill to create a Core type; it applies all of this and
leaves you with a failing test, which is where a new type should start.

```bash
python3 .claude/skills/scaffold-core/scaffold.py Card --subfolder Cards
```

- Assemblies and their folders share a name: `Frogs.Core`, `Frogs.Unity`.
- A subfolder under `Assets/Scripts/Core/` becomes a namespace segment:
  `Cards/` is `Frogs.Core.Cards`, and its tests are `Frogs.Core.Tests.Cards`.
- Core types are named for the game, not the engine — `Lane`, `LilyPad`,
  `Card`. The words come from [`CONTEXT.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CONTEXT.md),
  which is the glossary for exactly this reason. If a type name only makes sense
  to someone who knows Unity, it probably belongs in the shell.
- Unity-layer types that exist to display a Core type are named `<Thing>View`
  (`FrogView`), and ones that exist to feed input in are `<Thing>Input`.
- One public type per file, file named for the type.

## Geometry, layout, and tuning values are named variables

**Every size, offset, margin, spacing, duration, speed, delay, threshold, and
count is a named constant or a serialized field. Never a bare literal in a
method body.**

```csharp
// No.
transform.position = new Vector3(1.5f, 0.25f, 0f);
if (position == 8) { … }
yield return new WaitForSeconds(0.4f);

// Yes.
const int LaneWinningPosition = 8;
[SerializeField] float _lanePositionGap = 48f;
[SerializeField] float _frogHopDuration = 0.4f;
```

None of those three names is invented here. `LaneWinningPosition`,
`LanePositionGap` and `FrogHopDuration` are what
[the game board spec](../specs/ui/game-board.md) already calls those numbers,
and a constant in the code keeps the name the spec page gave it so the two can
be checked against each other.

### Why this is a rule and not a preference

- **Tuning is the game.** Whether a wrong answer costs a lily pad or costs
  nothing is the difference between fun and not, and it is a question for
  Connor. A number with a name is a number he can be asked about; `0.4f` on
  line 118 of a method is not.
- **A named value can be found.** Changing "the gap between lily pads" means
  finding one constant, not grepping for `1.5f` and guessing which of the eleven
  matches is the gap.
- **A named value can be tested.** `LaneWinningPosition` can be asserted
  against; `== 8` buried in a branch can only be re-typed into the test, where
  it will agree with a bug just as happily as with correct code.
- **The name is the documentation.** `_frogHopDuration = 0.4f` says what 0.4
  *is*. No comment required, and no comment to go stale.

### This includes graybox

Especially graybox. Placeholder layout is the code most likely to survive to
release, because it works and nobody goes back to it. A graybox screen full of
bare literals is a screen nobody can re-lay-out without rebuilding it.

### The exemptions

Deliberately narrow:

- `0`, `1`, and `-1` used as arithmetic or sentinel values, not as measurements.
  `count + 1` is fine; `width * 1` was never fine.
- Loop bounds derived from a collection — `for (var i = 0; i < frogs.Count; i++)`.
- Values inside a test that *are* the test's subject — a test asserting a frog
  wins on position 8 should say `8` where the reader can see it.
- Unity's own required literals in serialization or attribute arguments, where a
  constant is not permitted by the language.

Anything else needs a name.

### The check is narrower than the rule

`geometry-lint` flags **f-suffixed float literals of magnitude 3 or more** on a
line that does not name them, ratcheting against a committed baseline so
existing code need not be fixed all at once — but the count can only go down.

That is deliberately narrower than the rule above. It does not see integer
literals, magnitudes below 3, or values inside a named declaration's
initialiser. A check with a high false-positive rate is one people learn to
override, and then it catches nothing.

**So passing the check is not the same as following the rule.** The rule is what
review holds you to; the check is what stops the rule decaying while nobody is
looking. See [CI/CD](ci-cd.md#geometry-lint-the-tuning-literal-check).
