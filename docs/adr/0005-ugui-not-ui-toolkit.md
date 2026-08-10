# The game's UI is built in uGUI, not UI Toolkit

Unity ships two UI systems, and nothing in `/docs` had said which one this game
uses. `Packages/manifest.json` carries both `com.unity.ugui` and
`com.unity.modules.uielements` at once — that is the set a default Unity 6
project ships with, not a choice anybody made. The screen router
([#213](https://github.com/derekwinters/connor-multiplying-frogs/issues/213))
put four empty root `GameObject`s on screen and drew nothing of its own, so it
did not have to pick either. This issue is the first one that draws a pixel,
so it is the first one that has to decide.

## The choice: **uGUI** (`Canvas`, `RectTransform`, MonoBehaviours with
`[SerializeField]`) — not UI Toolkit (`UXML`, `USS`, `UIDocument`).

## Why

**`/docs` is already written in uGUI's terms.** Every screen and shared-component
spec page states geometry as pixels at a fixed 1920 × 1200 reference
resolution — [the canvas every component is measured
in](../specs/ui/shared-components.md#the-canvas-every-component-is-measured-in)
says outright: *"Unity's `CanvasScaler` is set to that same reference
resolution."* `CanvasScaler` is a uGUI type with no UI Toolkit equivalent that
means the same thing. Picking UI Toolkit would mean re-deriving how "1920 × 1200
pixels" maps onto `UIDocument`'s length units before drawing a single button.

**[Unity serialization](../engineering/unity-serialization.md) — the page
[`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
rule 6 sends every agent to — is written entirely
about uGUI's shape.** Its worked examples are a `MonoBehaviour`'s serialized
`[SerializeField]` fields and a prefab's component tree
(`FrogPrefab_HasItsSpriteAndScriptWiredUp`). UI Toolkit's failure modes are a
different shape — a `UXML` element name that does not match, a `USS` class that
does not apply — that this page says nothing about. Choosing UI Toolkit would
leave the one page that exists specifically to stop an agent guessing at Unity
serialization silent on the serialization format the UI actually uses.

**The existing EditMode tests already assert on this shape.**
`ScreenRouterAdapterTests` asserts on `GameObject.activeSelf` and component
wiring built through the typed `MonoBehaviour`/`GameObject` API — the same shape
this issue's Button tests use. Nothing in the repo is written in UI Toolkit's
vocabulary.

**What UI Toolkit would have bought instead:** every visual in v0.2 is a
rectangle or a circle, and USS `border-radius` draws both with no sprite
asset at all — cheaper than uGUI's `Image`, which needs *some* sprite to render
a rounded shape (see below). That is a real cost, and it is the only one; it is
outweighed by re-expressing an already-written design contract before drawing
anything.

## Consequences

- Every shared component and every screen is a `MonoBehaviour` hierarchy built
  through the typed `GameObject`/`RectTransform`/`Image`/`Text` API — the same
  pattern `HelloWorldScene.cs` and `ScreenRouterAdapter` already use — guarded
  by EditMode tests in the `FrogPrefab_HasItsSpriteAndScriptWiredUp` style.
- **A filled rounded rectangle or circle needs a sprite for uGUI's `Image` to
  render at all**, and [no external
  assets](../specs/ui/shared-components.md) rules out an imported texture.
  Each issue that draws one either uses a built-in editor UI resource
  (`Resources.GetBuiltinResource<Sprite>(…)`, shipped with every Unity install —
  not a project asset) or generates the shape procedurally at runtime, and says
  in its PR which. This issue's Button uses the built-in resource; see its PR.
- `Packages/manifest.json` keeps `com.unity.modules.uielements` for now — it is
  a default Unity 6 module, not a UI Toolkit feature this project uses, and
  removing it is a housekeeping change with no behaviour attached to it, not
  something this decision needs to force.
- If a future screen turns out to need UI Toolkit's flexbox layout badly enough
  to be worth re-opening this, that is its own ADR that supersedes this one —
  not a screen quietly mixing both systems.
