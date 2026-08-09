# Title screen

The first thing you see. It says what the game is and gives you one thing to
press.

## Invariants

**Invariant:** there is exactly one interactive element on this screen — `Play`.
**Invariant:** nothing on this screen can start a game with the wrong number of
players. Choosing who is playing happens on [game setup](game-setup.md), always,
including when you have played five games in a row.
**Invariant:** the version string is present and is read from `/VERSION`, never
typed into a layout.

## Regions

| Region | Job |
| --- | --- |
| `art` | The splash illustration — the whole canvas, behind everything |
| `title` | The game's name |
| `action` | The `Play` button |
| `footprint` | Version string, bottom-left; small and quiet |

## Anchors

- `art` fills the canvas, cropped from the centre outwards. It is the one
  element allowed to run to the screen edge, because it is a picture and a
  margin around a picture is a border nobody asked for.
- `title` is centred horizontally, its baseline at `TitleBaselineY` from the
  top. It is not vertically centred — the art has a subject in the middle of it
  and the title sits above that.
- `action` is centred horizontally, `PlayButtonBottomOffset` up from the bottom
  safe area, so it is under the thumb of a tablet held two-handed.
- `footprint` is pinned to the bottom-left safe area corner.
- On a screen that is not 16:10, the art crops and everything else keeps its
  distance from the **safe area**, not the screen edge.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin, every screen edge | `SafeMargin` | 48 px |
| Title baseline from the top | `TitleBaselineY` | 300 px |
| Title size | `TitleSize` | 160 px |
| `Play` button width | `PlayButtonWidth` | 560 px |
| `Play` button height | `PlayButtonHeight` | 160 px |
| `Play` button up from the safe area | `PlayButtonBottomOffset` | 120 px |
| Version string size | `VersionLabelSize` | 28 px |
| Scrim behind the title, over the art | `TitleScrimOpacity` | 0.35 |

`PlayButtonHeight` is larger than the shared `ButtonHeight` of 112 px. This is
the one button on the screen and the first one a child touches; it is allowed to
be the biggest button in the game.

## Elements

- **`Play`** — primary [button](shared-components.md#button). Goes to
  [game setup](game-setup.md). Never disabled.
- **Title** — text, not a logo image, until an `area:art` issue supplies one.
  The wireframe reserves the space a wordmark would occupy.
- **Version** — `v0.1.0`, from `/VERSION`. It exists so that a screenshot from a
  tablet says which build it came from, which is the difference between a
  reproducible bug and a story about one.

## Behaviour

- Entering: art and title fade in over `TitleFadeDuration` (0.3 s). No
  animation on `Play` — a button that moves is a button that gets missed.
- The hardware back button on this screen exits the app. It is the only screen
  where back exits.
- Nothing auto-advances. The title screen waits indefinitely.

## Mockup

[`mockups/title-screen.html`](mockups/title-screen.html)

The splash illustration attached to
[issue #168](https://github.com/derekwinters/connor-multiplying-frogs/issues/168)
is the art this screen is built around. The mockup draws its region as a
placeholder block rather than embedding the image, because a mockup that needs
a network fetch is a mockup that does not open on the sofa — and because the
wireframe is deciding *where the art goes*, not what it is.

## Open questions

- **Is there a `How to play` button here?** Proposed: no. The rules are one
  sentence long, Connor already knows them, and
  [an in-app tutorial is parked](../future-ideas.md). If one is wanted it goes
  here, beneath `Play`, as a secondary button — and it needs its own wireframe
  for the screen it opens.
- **Does the title screen come back after a game, or does `Play again` restart
  directly?** Both exist on [game over](game-over.md); this screen does not care
  which is used.
