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
| Scripting backend | IL2CPP — 64-bit Android has no other option |
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

**Emulator build** — x86_64, IL2CPP, for trying the game on a desktop emulator.
Use it for "does the app launch and show the right screen", never for judging
performance or for anything shipped.

CI builds both: the device profile is what the release ships, and an
emulator-targeted asset is attached alongside it so the release can be tried on
a desktop without rebuilding. A bug that reproduces only under the emulator is a
bug in the profile until proven otherwise.

#### Both profiles are IL2CPP, and that is not a preference

This page used to specify **x86_64 with Mono** for the emulator, on the
reasoning that IL2CPP cross-compiling to x86_64 is slow and buys nothing for a
smoke test. The reasoning about speed was right. The pairing was impossible.

**Unity has no Mono for 64-bit Android.** The Mono backend compiles at runtime
with a JIT, and that JIT is not supported on 64-bit Android, so `ARM64` and
`x86_64` are both IL2CPP-only (Unity Manual: *Mono scripting backend*, *IL2CPP
Overview*). Mono remains available for the 32-bit architectures, which this
project does not build.

Asking for it anyway fails in the worst possible way: nothing objects at the
point of asking. Unity drops the architecture it cannot build, leaves
`targetArchitectures` holding nothing, and the build dies much later with
*"Target architecture not specified"* — a message that names neither the
architecture nor the backend. That is what happened to the `v0.2.0` release
(#282), the first time the emulator profile had ever actually reached a build.

The emulator is x86_64, so x86_64 is the part that could not move. A slower
emulator build is the price, and two guards keep the pairing from drifting
back:

- **`Frogs.Core.AndroidBuildSupport`** knows which architectures are 64-bit and
  refuses to pair them with anything but IL2CPP. `BuildStampPreprocessor` reads
  the settings back out of `PlayerSettings` after applying a profile and fails
  the build — loudly, naming the profile — if the architecture set came back
  empty or the pairing is one Unity cannot build. It is plain C#, so the rule is
  covered by the two-second Core suite rather than by a release.
- **`.github/scripts/check_release_apks.py`** opens both APKs before either is
  attached and requires `libil2cpp.so` in each.

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

### The app icon

**The app icon is a close crop of Connor's splash art: the big frog sitting on
the log, with no title.** Derek's call on 2026-11-24
([#483](https://github.com/derekwinters/connor-multiplying-frogs/issues/483)):
*"The app icon will be a cropped version of this image, without the title text,
zoomed in to the frog on the log in center of the image."* It supersedes the
separate 608 × 608 frog-on-a-log drawing chosen on
[#169](https://github.com/derekwinters/connor-multiplying-frogs/issues/169).
Every file Android wants is generated from that one source. Nobody edits a
generated icon by hand: change the crop, or the mode below, and regenerate.

| | |
| --- | --- |
| Source | `Assets/Art/app-icon-source.png` — 1024 × 1024, rendered from [the splash art's](#the-splash-art) vector layers |
| Made by | `art-source/splash/build_splash.py`; the window, size and hidden layers are named constants in `art-source/splash/icon.py` |
| Its tests | `.github/scripts/tests/test_app_icon_crop.py` |
| Generated into | `Assets/Art/AppIcon/` |
| Generator | `.github/scripts/generate_app_icons.py` (needs Pillow) |
| Generator's tests | `.github/scripts/tests/test_generate_app_icons.py` |

**How the source is made.** The splash art's layers are stacked without the
title and its glow, and without the three small jumping frogs and their hop
arcs (one frog and one arc would otherwise poke into the frame); the unused
splashes are not in the stack anyway. That stack is cut to `ICON_CROP`, a
640 × 640 square of the 1586 × 992 splash canvas centred left-right on the big
frog, from just above its eyes to just below the log, and rendered straight from
the vectors at `ICON_SIZE`, 1024 px. So it is a fresh render, not an upscale, and
it is over twice the largest icon Android asks for. The tests check that the
whole big frog, and a good band of the log, fall inside the circle a round
launcher cuts, and that the committed source matches the committed layer PNGs
stacked and cropped the same way.

```bash
python3 art-source/splash/build_splash.py --icon-only   # seconds: from the committed layer SVGs
python3 .github/scripts/generate_app_icons.py           # then regenerate every icon
```

A full `build_splash.py` run writes the icon source too, with the rest of the
splash art.

What it generates, at mdpi, hdpi, xhdpi, xxhdpi and xxxhdpi:

| File | Size | What it is |
| --- | --- | --- |
| `adaptive-foreground-<density>.png` | 108 dp: 108 – 432 px | The adaptive icon's foreground layer |
| `adaptive-background-<density>.png` | 108 dp: 108 – 432 px | Its background layer |
| `legacy-<density>.png` | 48 dp: 48 – 192 px | The square icon older launchers use |
| `round-<density>.png` | 48 dp: 48 – 192 px | The same, cut to a circle |
| `play-store-512.png` | 512 px | The store listing image, fully opaque |

Every one of them is smaller than the source, so nothing is upscaled.

**How the picture sits in the adaptive mask is one switch.** An adaptive icon is
a 108 dp canvas of which a launcher shows only the middle 72 dp, cut to its own
shape — a circle, a squircle, a rounded square. Only the middle 66 dp circle is
guaranteed to survive every shape. The generator's `--mode` decides what that
does to the picture:

- **`fill`** — the default, and what is committed. The picture spans the whole
  72 dp viewport. A round launcher trims the scenery in the corners — the ends of
  the log, the sky and the water; the frog in the middle stays whole. Nothing is added around it: the background layer is
  the picture too. Derek's call, 2026-10-11.
- **`shrink`** — the whole picture is scaled into the 66 dp safe circle on a
  plain cream background (`#F5F2EA`), so no launcher ever trims anything, at the
  cost of a smaller frog.

The legacy, round and store images are what the adaptive icon shows through its
viewport, so they agree with the adaptive icon in either mode. To switch, or
after changing the source:

```bash
python3 -m pip install Pillow
python3 .github/scripts/generate_app_icons.py               # fill
python3 .github/scripts/generate_app_icons.py --mode shrink
python3 .github/scripts/run_python_tests.py scripts
```

One of those tests checks that the committed icons are exactly what a fresh run
of the generator produces, so an icon edited by hand, or a source changed without
regenerating, fails it. It and the other rendering tests are skipped — by name,
in the test output — where Pillow is not installed; the geometry tests need
nothing but Python.

**The build puts the icons in, every time.** Nothing is set by hand and no
`ProjectSettings` file is involved — CI builds without one. `AppIcons`, in
`Assets/Editor/`, is called from `ProjectBootstrap.ApplyToBuild()`, which the
build pre-processor runs before every build, the same way the app's name and
orientation get there. It fills Unity's Android icon slots through
`PlayerSettings.SetPlatformIcons`:

| Slots | Filled with |
| --- | --- |
| Adaptive, mdpi – xxxhdpi | `adaptive-background-<density>.png` then `adaptive-foreground-<density>.png`, the two layers |
| Round, mdpi – xxxhdpi | `round-<density>.png` |
| Legacy, mdpi – xxxhdpi | `legacy-<density>.png` |
| ldpi, all three kinds | the mdpi files, scaled down by Unity — no ldpi file is drawn |
| The default icon | `legacy-xxxhdpi.png`, so Unity's logo has nowhere to show through |

Which file goes in which slot is decided from the slot's width by
`AppIconSet` in Core, so it is tested without an editor. A slot whose width is
no Android density fails the build rather than getting the nearest file.

**A missing icon fails the build.** So does a file Unity cannot import, or a
slot that does not keep what it was given — every slot is read back after it is
set. The alternative is an APK with Unity's icon, which is what v0.6.0 shipped.

**No committed `.meta` is needed.** The icons are loaded by path, as Unity's
reference for `PlatformIcon.SetTextures` asks, and assigned in the same session
the build runs in, so nothing refers to a GUID. A texture not yet imported on a
fresh CI `Library` is imported synchronously before it is loaded. The import
settings are code, `AppIconImport`, not a hand-written `.meta`: no scaling to a
power of two, no mipmaps, uncompressed, so the texture Unity holds is exactly the
file the generator drew. `AppIconTests` reads the slots and the import settings
back.

**The tests need the Android editor.** Unity's `UnityEditor.Android` API only
exists with Android Build Support and Android as the active target, so the
EditMode suite runs in GameCI's Android image with `-buildTarget Android` — see
[CI/CD](ci-cd.md#ci-tests-the-two-suites).

### The frog model

**The frog piece is a 3D model built in Blender**, approved by Derek for
[#461](https://github.com/derekwinters/connor-multiplying-frogs/issues/461).
What it must look like and do is
[game board § The frog piece](../specs/ui/game-board.md#the-frog-piece); this
is how it gets into the project.

| | |
| --- | --- |
| Source | `tools/art/frog_model.py` — builds, rigs, animates and exports the frog headless; nothing is edited by hand in Blender |
| Runs on | Blender as a Python module, `bpy` 5.x on Python 3.13 — `venv/bin/python tools/art/frog_model.py --out <dir>` (see the script's docstring) |
| Imported files | `Assets/Art/Frog/Resources/FrogPiece/frog.fbx` and `frog_albedo.png`, copied unchanged from the script's output |
| Import settings | `Assets/Editor/FrogModelImport.cs`, applied on every import of those two files |
| Loaded by | `FrogModel`, through `Resources.Load` — so both files are in every build, and nothing refers to them by GUID |

To change the frog, change the script, run it, and copy `frog.fbx` and
`frog_albedo.png` over the imported ones. Do not edit either file directly, and
do not change the bone or clip names without changing `FrogModel` with them.

**The import settings are code, not a hand-written `.meta`.** A
`ModelImporter` block is dozens of version-dependent keys and integer enums,
which is exactly the guess [Unity serialization](unity-serialization.md) rules
out. `FrogModelImport` is an `AssetPostprocessor`. It sets them through the
typed `ModelImporter` and `TextureImporter` API, so a wrong name is a compile
error, and `FrogPieceTests` reads every one back from the importer:

| File | Setting | Why |
| --- | --- | --- |
| `frog.fbx` | Generic rig, avatar from this model, animation imported, no keyframe reduction | The 14-bone rig and its Sit and Hop clips, exactly as approved |
| `frog.fbx` | Read/Write enabled | The frog is skinned on the CPU to draw it into the canvas, in a player build too |
| `frog.fbx` | No materials, cameras, lights, blend shapes or colliders | No lit shader enters the build; colliders need the 3D physics module, which the project does not have |
| `frog_albedo.png` | sRGB, mipmaps, trilinear, clamp, max 1024, **uncompressed** | Drawn at 88 px from 1024 px; uncompressed keeps white exactly white, so the tint is exactly the frog colour |

The two files have **no committed `.meta`** yet, like the app icons above. Unity
generates one on import with a fresh GUID, which nothing depends on because the
model is loaded by path. Pinning those GUIDs needs an Editor session, and is
part of
[#183](https://github.com/derekwinters/connor-multiplying-frogs/issues/183).

**Sampling a clip needs the animation module.** `Packages/manifest.json`
carries `com.unity.modules.animation`, Unity's built-in module for
`AnimationClip` and `Animator`. It is part of the engine, not a third-party
package, and it makes no network calls. Outside the editor,
`AnimationClip.SampleAnimation` on a non-legacy clip needs an `Animator` on
the sampled object. It needs no controller (Unity issue *"Non-Legacy animations
cannot be sampled outside the Editor without an Animator"*, fixed in 2019.1).
So `FrogModel` makes sure the rig has one, and a test checks it.

### The lily pad model

**The lily pads are a 3D model built in Blender** too, approved by Derek for
[#465](https://github.com/derekwinters/connor-multiplying-frogs/issues/465).
What they must look like and do is
[game board § The lily pad model](../specs/ui/game-board.md#the-lily-pad-model);
this is how they get into the project. It follows
[the frog model](#the-frog-model) in every respect that applies, and is
simpler: four static meshes, no rig, no clips.

| | |
| --- | --- |
| Source | `tools/art/lilypad_model.py` — builds the four pads, paints their albedo atlas and exports them headless; nothing is edited by hand in Blender |
| Runs on | Blender as a Python module, `bpy` 5.x — `venv/bin/python tools/art/lilypad_model.py --out <dir>` (see the script's docstring) |
| Imported files | `Assets/Art/LilyPad/Resources/LilyPad/lilypad.fbx` and `lilypad_albedo.png`, copied unchanged from the script's output |
| Import settings | `Assets/Editor/LilyPadModelImport.cs`, applied on every import of those two files |
| Loaded by | `LilyPadModel`, through `Resources.Load` — so both files are in every build, and nothing refers to them by GUID |

To change a pad, change the script, run it, and copy `lilypad.fbx` and
`lilypad_albedo.png` over the imported ones. Do not edit either file directly,
and do not rename the four meshes (`LilyPad_Notch10` … `LilyPad_Notch25`)
without changing `LilyPadModel` with them. The atlas's layout — a 1024 px
quadrant per notch width, in `LilyPadNotchAngles` order — is the script's, and
`GameBoardLilyPadTests` reads the veins back out of it.

**The import settings are code**, for the reason the frog's are, and
`GameBoardLilyPadTests` reads every one back:

| File | Setting | Why |
| --- | --- | --- |
| `lilypad.fbx` | No rig, no animation | Four static meshes |
| `lilypad.fbx` | Read/Write enabled | Each mesh is laid flat on the CPU to draw it into the canvas, in a player build too |
| `lilypad.fbx` | Normals imported, not recalculated | The model's own normals say which faces look up |
| `lilypad.fbx` | No materials, cameras, lights, blend shapes or colliders | No lit shader enters the build; colliders need the 3D physics module, which the project does not have |
| `lilypad_albedo.png` | sRGB, mipmaps, trilinear, clamp, **max 1024**, **uncompressed** | A pad is drawn 112 px across, so a 512 px quadrant is still four times what shows, at a quarter of the 2048 px atlas's memory; uncompressed keeps `LilyPadGreen` and `LilyPadEdge` the colours the separability bar was measured against |

**Which way a pad faces is measured, not assumed.** Blender is Z-up and the FBX
is written Y-up, and how Unity's importer carries that across (a rotated node,
a scale of 100, or baked vertices) is not something this project relies on.
`LilyPadModel` takes each mesh's vertices into the model's own space through
its transforms, and Core's `LilyPadOutline` finds up (the thin axis, the way
the rim curls) and the notch (the widest gap in the rim) from those points.

The two files have **no committed `.meta`** yet, like the frog's, for the same
reason; pinning their GUIDs is part of
[#183](https://github.com/derekwinters/connor-multiplying-frogs/issues/183).

### The log model

**The shared logs are a 3D model built in Blender** as well, approved by Derek
for [#468](https://github.com/derekwinters/connor-multiplying-frogs/issues/468).
What they must look like and do is
[game board § The log model](../specs/ui/game-board.md#the-log-model); this is
how they get into the project. It follows [the lily pad model](#the-lily-pad-model)
in every respect that applies: three static meshes, no rig, no clips — but a
log is **assembled** from them at the length it is drawn, rather than drawn as
one mesh.

| | |
| --- | --- |
| Source | `tools/art/log_model.py` — builds the three pieces, paints their albedo and exports them headless; nothing is edited by hand in Blender |
| Runs on | Blender as a Python module, `bpy` 5.x — `venv/bin/python tools/art/log_model.py --out <dir>` (see the script's docstring) |
| Imported files | `Assets/Art/Log/Resources/Log/log.fbx` and `log_albedo.png`, copied unchanged from the script's output |
| Import settings | `Assets/Editor/LogModelImport.cs`, applied on every import of those two files |
| Assembly rule | Core's `LogAssembly` — how many bodies, how far each is stretched, and where every piece goes, for a length in frog units. Plain NUnit, in the fast suite |
| Loaded and drawn by | `LogModel`, through `Resources.Load` — so both files are in every build, and nothing refers to them by GUID |

To change a log, change the script, run it, and copy `log.fbx` and
`log_albedo.png` over the imported ones. Do not edit either file directly, and
do not rename the three pieces (`LogBody_A`, `LogBody_B`, `LogCap`) without
changing `LogModel` with them. The FBX also carries the script's two prebuilt
assemblies, `SharedLog` and `PlayerLog`, whose children share the pieces'
meshes; `LogModel` finds the pieces by their own node names, so it never picks
up one of those children's offsets, turns or stretches. If the assembly rule in
the script's docstring changes, `LogAssembly` and its tests change with it.

**The import settings are code**, for the reason the frog's and the lily pad's
are, and `GameBoardLogTests` reads every one back:

| File | Setting | Why |
| --- | --- | --- |
| `log.fbx` | No rig, no animation | Three static meshes |
| `log.fbx` | Read/Write enabled | Each piece is laid flat on the CPU to draw it into the canvas, in a player build too |
| `log.fbx` | Normals imported, not recalculated | The model's own normals say which faces look up |
| `log.fbx` | No materials, cameras, lights, blend shapes or colliders | No lit shader enters the build; colliders need the 3D physics module, which the project does not have |
| `log_albedo.png` | sRGB, mipmaps, trilinear, clamp, max 1024, **uncompressed** | Its native 1024 px; a body is drawn 112 px long from about 225 texels, so nothing smaller would do. Uncompressed keeps the bark the colours `LogLabelInk` and the frogs were measured against |

**Which way a piece runs is measured, not assumed**, for the lily pad's reason.
`LogModel` takes each piece's vertices into the model's own space through its
transforms, relative to the piece's own origin (its joining face, on the
waterline), and Core's `LogAssembly.MeasureFrame` reads `LogBody_A`'s points:
along is the axis the body lies all on one side of its joining face, up is the
way its section reaches furthest from the waterline, and across is the
right-handed third, so the stub on `LogBody_B` cannot be mirrored to the other
side. The body's measured length is one frog unit, whatever units the importer
chose.

The two files have **no committed `.meta`** yet, like the frog's and the lily
pad's, for the same reason; pinning their GUIDs is part of
[#183](https://github.com/derekwinters/connor-multiplying-frogs/issues/183).

### The water tile

**The pond's water is one seamless texture tile**, approved by Derek for
[#471](https://github.com/derekwinters/connor-multiplying-frogs/issues/471).
What it must look like and do is
[game board § The water's marks](../specs/ui/game-board.md#the-waters-marks);
this is how it gets into the project. It is a flat 2D image rather than a
model, so it needs no Blender: the script is plain Python with NumPy and Pillow.

| | |
| --- | --- |
| Source | `tools/art/water_tile.py` — draws the tile from a fixed seed; nothing is edited by hand |
| Runs on | Python 3 with `numpy` and `pillow` — `python3 tools/art/water_tile.py` (see the script's docstring) |
| Imported file | `Assets/Art/Water/Resources/Water/pond_water_tile.png`, written there by the script |
| Import settings | `Assets/Editor/WaterTileImport.cs`, applied on every import of that file |
| Drift | Core's `PondWaterDrift` — how far the water has moved left, as a pure function of elapsed time, wrapped at one tile. Plain NUnit, in the fast suite |
| Loaded and drawn by | `GameBoardScreenView`, through `Resources.Load`, as a `RawImage` whose `uvRect` repeats the tile and carries the drift — uGUI's default material, no shader of its own |

The script is **deterministic**: the same seed draws the same marks, and a run
writes the committed PNG byte for byte. To change the water, change the
script's numbers, run it, and commit the PNG it writes. Do not edit the PNG
directly.

**The import settings are code**, for the reason the models' are, and
`GameBoardWaterTests` reads every one back:

| Setting | Why |
| --- | --- |
| sRGB, no alpha | It is a colour image, opaque everywhere |
| **Wrap: repeat** | The `RawImage` draws the tile many times over by asking for UVs past 1; clamp would smear the edge pixels across the screen instead |
| No mipmaps, bilinear | It is drawn larger than its 512 px (one tile is `PondWaterTileSize`, 1024 px), so a smaller mip is never wanted |
| Max 512, **uncompressed** | Its own size; uncompressed keeps the water exactly `PondWater` between the marks, so the colour checks still measure what is on screen |
| Not readable | Nothing reads its pixels at runtime |

The PNG has **no committed `.meta`**, like the models', for the same reason;
pinning its GUID is part of
[#183](https://github.com/derekwinters/connor-multiplying-frogs/issues/183).

### The splash art

**Connor's splash picture is kept as separate layers**, so the launch splash, the
app icon and the home screen can each be made from the same art without redrawing it
([#481](https://github.com/derekwinters/connor-multiplying-frogs/issues/481)). The
picture is committed once, byte-for-byte as supplied, and the layers are rebuilt from it
by one script. The [app icon](#the-app-icon) is cropped from them by the same script,
and the [launch splash](#the-launch-splash) is rendered from them; nothing else the
game builds uses them yet, and putting the art on a screen is each screen's own issue.

| | |
| --- | --- |
| Source | `Assets/Art/splash-source.png` — 1586 × 992, unmodified |
| Rebuilt into | `art-source/splash/` — outside `Assets/`, so Unity never imports it |
| Builder | `art-source/splash/build_splash.py` — Python with VTracer, OpenCV, SciPy, CairoSVG, Pillow, NumPy, fontTools and skia-pathops |
| Its tests | `.github/scripts/tests/test_splash_layers.py` |
| How it works | `art-source/splash/README.md` |

What it writes:

- **One SVG per object** — sky, title glow, each cloud, the far banks, the reeds, the
  pond, each lily pad, water lily, splash and hop arc, the grass and its tufts, the log,
  each frog, and the title — all on one canvas in the original's pixels
  (`viewBox="0 0 1586 992"`), each a group with an `id`, so any set of them stacks
  straight back together. Each is also a transparent PNG at 3965 × 2480, more than
  twice the 1920 × 1200 tablet.
- **A composite** of every layer in use, and each notable object **cropped** on its own.
  Each jumping frog is grouped with the dashed arc of its hop, so hiding the frogs hides
  the arcs. The **splashes** on the lily pads are exported as layers but left out of the
  composite — kept, not used, Derek's call in review of #481's PR.
- **Review images**: the original above the rebuild, and the scene without the frogs,
  without the frogs and the title, without the frogs and the log, and without all three.

Three rules it keeps:

- **The title is vector lettering, spelled MULTIPLYING.** The picture spells it
  "MUILTIPLYING"; Derek's call (2026-11-24) is that the rebuild corrects it. Letters
  come from an open-licensed font (Fredoka, SIL Open Font License, licence committed in
  `art-source/splash/fonts/`), placed where the drawn letters are.
- **Hiding a layer leaves no hole.** Where an object is hidden behind another, its hidden
  part is completed: the log carries on under the big frog; the water, shoreline, grass
  and tufts behind the log and under the big frog; the pads and water under the unused
  splashes; and the sky — a fitted gradient over the whole canvas — under the title. The
  tests check that nothing lets the sky through below the water line once the big frog,
  the log, the splashes or the arcs are taken away, and that the sky is opaque.
- **The build is deterministic.** A second run writes byte-identical files
  (`build_splash.py --check-rebuild`). To change the art, change the script's numbers
  and rebuild; do not edit an output by hand.

The tests that read pixels need Pillow and NumPy and are skipped, by name, where those
are not installed; the structural ones need only Python. The byte-identical rebuild
is checked by the build itself, not in CI: it needs the tracing libraries and takes
minutes.

### The launch splash

**The app opens on Connor's splash picture, with no Unity logo**
([#482](https://github.com/derekwinters/connor-multiplying-frogs/issues/482)). Unity's
own launch splash shows it while the engine starts; the title screen follows.

| | |
| --- | --- |
| Picture | `Assets/Art/Splash/splash-1920x1200.png` — the whole composite, frogs and title, at 1920 × 1200 |
| Made by | `art-source/splash/build_splash.py` (`--splash-only` for just this); the window is in `art-source/splash/launch.py` |
| Its tests | `.github/scripts/tests/test_launch_splash.py` |
| Set by | `LaunchSplash`, in `Assets/Editor/` — at build time |
| Import settings | `LaunchSplashImport` — a single sprite, kept at full size, no mipmaps |
| Its EditMode tests | `Assets/Tests/EditMode/LaunchSplashTests.cs` |

**The build sets it, every time.** `LaunchSplash` is called from
`ProjectBootstrap.ApplyToBuild()`, which the build pre-processor runs before every build,
the same way the app's name and icon get there. Through `PlayerSettings.SplashScreen` it:

- turns the splash **on** and **the Unity logo off** (Unity 6 allows that on every
  licence tier), with no other logos;
- makes the picture the **background**, landscape, with no portrait picture since the
  app never turns portrait;
- shows the picture **as drawn**: no background blur and no darkening overlay. Unity
  uses both to make logos stand out, and there are none.

Everything else — how long the splash shows and how it animates — is left at Unity's
defaults. A missing picture, or one Unity did not import as a sprite, **fails the
build** rather than shipping Unity's splash. As with the icons, the picture is loaded by
path, so no committed `.meta` is needed, and the settings are read back after they are
set.

### The title screen's art and font

**The title screen is drawn on the same picture, and letters its `START` sign in the
title's font** ([#520](https://github.com/derekwinters/connor-multiplying-frogs/issues/520);
the layout is [the title screen's spec](../specs/ui/title-screen.md)). Both are game
files, loaded at runtime by path from a `Resources` folder, so nothing refers to them by
GUID and neither has a committed `.meta`.

| | |
| --- | --- |
| Picture | `Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png` — the home build step's render, the same bytes as the mockup's `full.png`, and the same picture as the launch splash |
| Extended scene | `Assets/Art/TitleScreen/Resources/TitleScreen/title-scene-2880x1440.png` — the same layers in a window 480 px wider each side and 120 px taller top and bottom, drawn behind the fitted picture on screens that are not 16:10; the layers carry their drawing past the frame (`art-source/splash/beyond.py`), so its middle is the picture ([#533](https://github.com/derekwinters/connor-multiplying-frogs/issues/533)) |
| Font | `Assets/Fonts/Fredoka/Resources/Fredoka/Fredoka-Bold.ttf`, with its `OFL.txt` beside it — the game's first font that is not Unity's built-in one |
| Made by | `art-source/splash/build_splash.py --home-only` (`art-source/splash/home.py`) |
| Its tests | `.github/scripts/tests/test_title_screen_art.py`, and `test_splash_beyond.py` for the extended scene |
| Import settings | `TitleScreenArtImport`, in `Assets/Editor/` — the picture as the launch splash's (a single sprite, full size, no mipmaps), and the extended scene behind it the same way, with a 4096 largest size because it is 2880 wide ([#533](https://github.com/derekwinters/connor-multiplying-frogs/issues/533)); the font as a dynamic font with its data included |
| Its EditMode tests | `Assets/Tests/EditMode/TitleScreenViewTests.cs` |

**The font is a static Bold, not the variable font.** The title is set in Fredoka at
weight 700, from `art-source/splash/fonts/Fredoka-Variable.ttf` (SIL Open Font License).
Unity's legacy font importer (`TrueTypeFontImporter`) has no setting that chooses a
variable font's axes, and the legacy `Text` component's bold is a synthetic one, so the
variable file imported as it is would draw Fredoka's default instance, Light (300). The
home build step instead instances the variable font at `wght` 700, `wdth` 100 with
fontTools, keeping the source's timestamp so every run writes the same bytes, and the
tests regenerate it and compare.

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

### The `Game` scene is what the app actually boots into

`Assets/Editor/GameScene.cs` creates `Assets/Scenes/Game.unity` the same way —
`EditorSceneManager`, not hand-authored YAML — with a camera and nothing else.
No probe, no layout, and **no MonoBehaviour at all**: what puts a screen on
screen is code, not a component dropped into the scene (below).

`Game.unity` is **first** in `ProjectSettings/EditorBuildSettings.asset` —
index 0, which is the entry Unity boots into. `HelloWorld.unity` stays in the
list, enabled, one slot behind it; nothing removes it, and its own EditMode
tests still pass unchanged, because they assert presence and `enabled`, never
position. Registering `Game.unity` by appending it, the way a first pass at
this might do it, would leave the built APK opening into the blank Hello World
scene forever — the ordering is the whole point, not an incidental detail.

### What puts a screen on screen: a code-only entry point

The scene stays empty. `Frogs.Unity.AppRoot` is the composition root, and it is
reached from a `[RuntimeInitializeOnLoadMethod]` hook that does nothing but call
`AppRoot.Create()`. It builds, in this order:

1. the one `Canvas`, in screen-space overlay, with a `CanvasScaler` at the
   **1920 × 1200** reference resolution every spec page is measured against;
2. an `EventSystem` and a `StandaloneInputModule`, without which no tap reaches
   any button;
3. `ScreenRouterAdapter`, whose one root per `Screen` and per `Dialog` is a
   `RectTransform` filling the canvas;
4. one view under each of those roots, each asked to build itself immediately
   rather than when its root first becomes active.

**Why not a bootstrap component in the scene**, which is the obvious way to do
this: a component reference in a `.unity` file is a GUID, and a GUID that drifts
from its `.meta` comes back as a *Missing Script* that throws nothing and logs
nothing. Adding one would also mean regenerating and re-extracting the scene the
way #209 had to, since there is no editor in an agent environment. A code-only
entry point has no GUID to lose and leaves `Game.unity` exactly what was
committed and CI verified.

**Nothing on `AppRoot` is serialized.** It is constructed at runtime and saved
into no asset, so every field on it is deliberately a plain field rather than a
`[SerializeField]` — see [Unity serialization](unity-serialization.md).

The app opens on the [title screen](../specs/ui/title-screen.md), and from there
every screen the POC has is reachable by tapping. `AppRoot` holds one
`Frogs.Core.Game` for as long as it is being played, so the board, every dialog
and the standings all read the same game.

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
yield return new WaitForSeconds(0.5f);

// Yes.
const int LaneWinningPosition = 8;
[SerializeField] float _lanePositionGapMin = 48f;
[SerializeField] float _frogHopDuration = 0.5f;
```

None of those three names is invented here. `LaneWinningPosition`,
`LanePositionGapMin` and `FrogHopDuration` are what
[the game board spec](../specs/ui/game-board.md) already calls those numbers,
and a constant in the code keeps the name the spec page gave it so the two can
be checked against each other.

The example says `LanePositionGapMin` rather than `LanePositionGap` for a
reason worth noticing: since
[#325](https://github.com/derekwinters/connor-multiplying-frogs/issues/325) the
gap itself is **derived from the screen's width** and is not a number anybody
types. Its floor still is. A derived value does not escape this rule — it gets
a named function of named constants, not an expression inlined at the one call
site that needed it.

### Why this is a rule and not a preference

- **Tuning is the game.** Whether a wrong answer costs a lily pad or costs
  nothing is the difference between fun and not, and it is a question for
  Connor. A number with a name is a number he can be asked about; `0.5f` on
  line 118 of a method is not.
- **A named value can be found.** Changing "the gap between lily pads" means
  finding one constant, not grepping for `1.5f` and guessing which of the eleven
  matches is the gap.
- **A named value can be tested.** `LaneWinningPosition` can be asserted
  against; `== 8` buried in a branch can only be re-typed into the test, where
  it will agree with a bug just as happily as with correct code.
- **The name is the documentation.** `_frogHopDuration = 0.5f` says what 0.5
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
