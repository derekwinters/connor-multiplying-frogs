# Title screen

The first thing you see. It says what the game is and gives you the way into a
game: carry on with the one you left, or start a new one.

## Invariants

**Invariant:** the only interactive elements on this screen are `RESUME` and
`NEW`. There is never a third. How many of the two are on screen when there is
no saved game is settled — [open question 2](#open-questions), `Hidden` — and
nothing is ever *added* beyond these two.
**Invariant:** nothing on this screen can start a game with the wrong number of
players. Choosing who is playing happens on [game setup](game-setup.md), always,
including when you have played five games in a row. **`RESUME` is not an
exception to this.** It does not choose a roster — it restores the one a saved
game already carries, and that roster was chosen on game setup when that game
was started. The one path that leaves this screen without passing through game
setup does so because the choice has already been made, not because it can be
made somewhere else.
**Invariant:** the version string is present and is read from `/VERSION`, never
typed into a layout.

### The invariant this page used to carry

Until this wireframe, the first invariant on this page read:

> **Invariant:** there is exactly one interactive element on this screen —
> `Play`.

That stopped being true when [#228](https://github.com/derekwinters/connor-multiplying-frogs/issues/228)
settled that a saved game is re-entered from the title screen. `Play` does not
exist on this screen any more: it is `NEW`, and it has a neighbour. The old
wording is quoted here so the change is visible, and it is not true anywhere
else on this page.

## Regions

| Region | Job |
| --- | --- |
| `art` | The splash illustration — the whole canvas, behind everything |
| `title` | The game's name |
| `action` | The button row: `RESUME` and `NEW`, side by side |
| `footprint` | Version string, bottom-left; small and quiet |

## Anchors

- `art` fills the canvas, cropped from the centre outwards. It is the one
  element allowed to run to the screen edge, because it is a picture and a
  margin around a picture is a border nobody asked for.
- `title` is centred horizontally, its baseline at `TitleBaselineY` from the
  top. It is not vertically centred — the art has a subject in the middle of it
  and the title sits above that.
- `action` is a **single horizontal row, centred horizontally as a row**, its
  bottom edge `TitleButtonBottomOffset` up from the bottom safe area, so both
  buttons are under the thumbs of a tablet held two-handed. Within the row,
  `RESUME` is on the left and `NEW` on the right, separated by
  `TitleButtonGap`.
- The row is centred **as a whole**, not per button. That is what makes the
  no-save case fall out for free: [open question 2](#open-questions) settled
  on `Hidden`, so the row contains one button and centres it, which is exactly
  where `Play` used to sit.
- `footprint` is pinned to the bottom-left safe area corner.
- On a screen that is not 16:10, the art crops and everything else keeps its
  distance from the **safe area**, not the screen edge.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin, every screen edge | `SafeMargin` | 48 px |
| Title baseline from the top | `TitleBaselineY` | 300 px |
| Title size | `TitleSize` | 160 px |
| Width of each button in the `action` row | `TitleButtonWidth` | 560 px |
| Height of each button in the `action` row | `TitleButtonHeight` | 160 px |
| Gap between `RESUME` and `NEW` | `TitleButtonGap` | 48 px |
| Button row up from the safe area | `TitleButtonBottomOffset` | 120 px |
| Label size on both buttons | `TitleButtonLabelSize` | 64 px |
| Version string size | `VersionLabelSize` | 28 px |
| Scrim behind the title, over the art | `TitleScrimOpacity` | 0.35 |
| Entering fade, art and title | `TitleFadeDuration` | 0.3 s |

`TitleFadeDuration` is stated in [Behaviour](#behaviour) — *"art and title fade
in over `TitleFadeDuration` (0.3 s)"* — and is added here in the same PR that
declares it in code
([#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)),
per [named constants are the origin, not an
afterthought](../../engineering/ui-design-process.md#the-named-constants-are-the-origin-not-an-afterthought):
a value the code needs but this table didn't have was a sign the row was
missed, not a number free to invent.

`TitleButtonHeight` is larger than the shared `ButtonHeight` of 112 px, and
`TitleButtonWidth` larger than the shared `ButtonMinWidth` of 320 px. These are
the first buttons a child touches and there is nothing else on the screen
competing with them for space; they are allowed to be the biggest buttons in the
game. Both buttons take the same width and the same height, deliberately: they
are two peer choices, and a size difference would have been a second,
accidental way of saying which one matters — which
[open question 1](#open-questions) settles on purpose, through `ButtonKind`
alone, instead.

`TitleButtonGap` is **not** the shared `ButtonGap` of 32 px. That value is sized
for the shared 112 px button; scaled to a 160 px one the same proportion lands
just under 48 px, and 48 px is already this page's `SafeMargin` and game
setup's `SeatGap` at a comparable scale. At 32 px two 560 px buttons read as one
split control rather than two choices.

The row is `TitleButtonWidth` × 2 + `TitleButtonGap` = 1168 px wide, centred in
the 1824 px between the safe margins, leaving 328 px either side. The row is
deliberately not full-bleed, for the same reason game setup's seat row is not.

`TitleButtonLabelSize` is 64 px rather than the shared `ButtonLabelSize` of
44 px, in the same proportion as the buttons themselves are oversized
(44 × 160 ÷ 112 ≈ 63). This is not a new decision — 64 px is what the committed
mockup has drawn since this screen was first agreed. It is given a name here
because there are now two labels of very different lengths (`RESUME` and `NEW`)
sharing one button size, and a label size that only exists inside a mockup is a
label size the next redraw gets wrong.

### What became of the `Play*` constants

[#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)
builds this screen from named constants and names three of them that no longer
exist. They are **renamed, not removed, and their values do not change**:

| Was | Is now | Value | Note |
| --- | --- | --- | --- |
| `PlayButtonWidth` | `TitleButtonWidth` | 560 px | Unchanged; now applies to both buttons |
| `PlayButtonHeight` | `TitleButtonHeight` | 160 px | Unchanged; now applies to both buttons |
| `PlayButtonBottomOffset` | `TitleButtonBottomOffset` | 120 px | Unchanged; now measured to the bottom of the row |

The names lose their `Play` prefix because `Play` is not a thing on this screen
any more, and a constant named after a button that does not exist is a constant
somebody will eventually apply to the wrong one. `TitleButtonGap` is the one
genuinely new number.

## Elements

- **`RESUME`** — secondary (settled, [open question 1](#open-questions)).
  Goes back into the saved game, on the
  [game board](game-board.md). It restores a roster; it never chooses one. What
  state within a turn a resumed game re-enters is the save format's business
  ([ADR-0004](../../adr/0004-core-owns-the-save-format.md)) and is not settled
  here.
  - **Hidden when there is no game to resume** (settled, [open question
    2](#open-questions)) — not laid out at all, and because that is the only
    state that exists in this v0.2 proof of concept, `RESUME` is hidden in
    practice on every build of this screen today.
  - Nothing on this screen writes, reads, or deletes a save. The save
    round-trip does not exist yet — it is out of the shape-only proof of
    concept ([#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198))
    — and this wireframe draws the button without implying the mechanism behind
    it.
- **`NEW`** — primary (settled, [open question 1](#open-questions)). Renamed
  from `Play`, and it goes exactly where `Play` went: [game
  setup](game-setup.md). Never disabled; there is no state of this screen that
  would justify greying it out.
- **Title** — text, not a logo image, until an `area:art` issue supplies one.
  The wireframe reserves the space a wordmark would occupy.
- **Version** — `v0.1.0`, from `/VERSION`. It exists so that a screenshot from a
  tablet says which build it came from, which is the difference between a
  reproducible bug and a story about one.

## Behaviour

- Entering: art and title fade in over `TitleFadeDuration` (0.3 s). Neither
  button animates — a button that moves is a button that gets missed.
- The hardware back button on this screen exits the app. It is the only screen
  where back exits.
- Nothing auto-advances. The title screen waits indefinitely.

## Mockup

- **The agreed picture:** [`mockups/title-screen.html`](mockups/title-screen.html) —
  `NEW` primary, `RESUME` secondary.

It used to be one of a pair, drawn to ask which of the two buttons should be
primary — see "The invariant this page used to carry" and
[open question 1, settled](#open-questions) below. The losing comparison file,
`title-screen-resume-primary.html` (`RESUME` primary), is deleted: the question
it existed to ask is answered, and a mockup nobody can build to is not worth
keeping around to confuse the next reader.

The mockup draws the state where **a save exists**, so both buttons are live.
The no-save state — `RESUME` hidden, `NEW` alone and centred — is not drawn
separately: it is the same layout with one button removed and the row
re-centred, which is exactly what [the settled open question
2](#open-questions) below says happens, and it needs no picture of its own to
be unambiguous.

The splash illustration attached to
[issue #168](https://github.com/derekwinters/connor-multiplying-frogs/issues/168)
is the art this screen is built around. The mockup draws its region as a
placeholder block rather than embedding the image, because a mockup that needs
a network fetch is a mockup that does not open on the sofa — and because the
wireframe is deciding *where the art goes*, not what it is.

## Open questions

- **1. Which of `RESUME` and `NEW` is the primary button? Settled: `NEW`.**
  Derek's call, recorded on
  [issue #216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216#issuecomment-5241023983):
  `NEW` is primary, `RESUME` is secondary. `NEW` inherits `Play`'s emphasis and
  is the button that is always there — the shared
  [Button](shared-components.md#button) invariant, *"exactly one primary button
  is visible at a time,"* is satisfied by `NEW` alone. The losing comparison
  mockup (`RESUME` primary) is deleted; see [Mockup](#mockup) above.
- **2. What does `RESUME` do when there is no game to resume? Settled: Hidden.**
  Derek's call, same comment as above: not laid out at all, the component's
  existing `Hidden` state — *"not laid out at all — buttons do not leave gaps
  behind."* Because the `action` row is centred as a row, this makes the
  screen show a single centred `NEW` exactly where `Play` used to sit, with no
  extra layout to build. In practice this is the *only* state the row is ever
  in: the save/resume system does not exist anywhere in this v0.2 shape-only
  proof of concept
  ([#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198)
  excludes it from scope), so there is never a saved game for `RESUME` to
  report. [#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)
  builds the hidden/shown check as a real query a future save-system issue can
  answer honestly, not as `RESUME` being removed outright — the `Disabled` and
  `Absent` options this question used to weigh are not chosen; `RESUME` stays
  a real button that is simply always hidden today.
- **Is there a `How to play` button here?** Proposed: still no — but for a
  different reason than before, because half of the old reason has gone.

  This question used to end *"it needs its own wireframe for the screen it
  opens"*. That screen now exists:
  [#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324)
  specified [how to play](how-to-play.md), reachable from the
  [settings dialog](settings-dialog.md). So there is somewhere for a button
  here to go, and the argument for putting one here is a real one — somebody
  who has never played reaches for the title screen, not for a gear inside a
  game they have not started.

  What is left in the way is this page's own first invariant: *"the only
  interactive elements on this screen are `RESUME` and `NEW`. There is never a
  third."* Adding the button is a change to that invariant, and changing it is
  a decision about what this screen is, not a consequence of the other screen
  existing. It should be its own issue. If it is taken, the button goes beneath
  the `RESUME`/`NEW` row as a secondary one, and it opens the same screen the
  gear does.
- **Does the title screen come back after a game, or does `Play again` restart
  directly?** Both exist on [game over](game-over.md); this screen does not care
  which is used.
