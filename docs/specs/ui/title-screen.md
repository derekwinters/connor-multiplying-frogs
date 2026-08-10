# Title screen

The first thing you see. It says what the game is and gives you the way into a
game: carry on with the one you left, or start a new one.

## Invariants

**Invariant:** the only interactive elements on this screen are `RESUME` and
`NEW`. There is never a third. How many of the two are on screen when there is
no saved game is [open question 2](#open-questions); nothing is ever *added*
beyond these two.
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
  no-save case fall out for free: if [open question 2](#open-questions) is
  answered `Hidden`, the row contains one button and centres it, which is
  exactly where `Play` used to sit.
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

`TitleButtonHeight` is larger than the shared `ButtonHeight` of 112 px, and
`TitleButtonWidth` larger than the shared `ButtonMinWidth` of 320 px. These are
the first buttons a child touches and there is nothing else on the screen
competing with them for space; they are allowed to be the biggest buttons in the
game. Both buttons take the same width and the same height, deliberately: they
are two peer choices, and a size difference would be a second, accidental way of
saying which one matters — which is what
[open question 1](#open-questions) is for the review to answer on purpose.

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

- **`RESUME`** — goes back into the saved game, on the
  [game board](game-board.md). It restores a roster; it never chooses one. What
  state within a turn a resumed game re-enters is the save format's business
  ([ADR-0004](../../adr/0004-core-owns-the-save-format.md)) and is not settled
  here.
  - **Its button kind is not assigned yet.** Whether it is the primary or the
    secondary of the pair is [open question 1](#open-questions).
  - **What it does when there is no game to resume is not settled either.** It
    is [open question 2](#open-questions), and this page does not assume an
    answer. The mockups draw the state where a save exists.
  - Nothing on this screen writes, reads, or deletes a save. The save
    round-trip does not exist yet — it is out of the shape-only proof of
    concept ([#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198))
    — and this wireframe draws the button without implying the mechanism behind
    it.
- **`NEW`** — renamed from `Play`, and it goes exactly where `Play` went:
  [game setup](game-setup.md). Never disabled; there is no state of this screen
  that would justify greying it out. Its button kind is the other half of
  [open question 1](#open-questions).
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

Two mockups, drawn as a pair, differing in **exactly one thing**: which of the
two buttons is the primary. Everything else — position, size, gap, wording — is
identical between them, so the review is answering
[open question 1](#open-questions) and nothing else.

- **`NEW` primary:** [`mockups/title-screen.html`](mockups/title-screen.html)
- **`RESUME` primary:** [`mockups/title-screen-resume-primary.html`](mockups/title-screen-resume-primary.html)

**Neither is the agreed picture yet.** The pair is the question. Once review
picks one, it becomes `title-screen.html` and the other file goes.

Both draw the state where **a save exists**, so both buttons are live. That is
the state the layout question is about; the no-save layout falls out of
whichever answer [open question 2](#open-questions) gets, and is drawn when that
is answered.

The splash illustration attached to
[issue #168](https://github.com/derekwinters/connor-multiplying-frogs/issues/168)
is the art this screen is built around. The mockups draw its region as a
placeholder block rather than embedding the image, because a mockup that needs
a network fetch is a mockup that does not open on the sofa — and because the
wireframe is deciding *where the art goes*, not what it is.

## Open questions

- **1. Which of `RESUME` and `NEW` is the primary button?** Not decided. The
  shared [Button](shared-components.md#button) invariant is *"exactly one
  primary button is visible at a time"*, so one of the two is primary and the
  other is secondary — this is not a question the page can leave unanswered
  forever, only one it must not answer by accident. It reads either way: `NEW`
  inherits `Play`'s emphasis and is the button that is always there, while a
  game sitting half-finished is arguably the likelier reason somebody opened the
  app at all. The two mockups above are the two answers, side by side.
- **2. What does `RESUME` do when there is no game to resume?** Not decided —
  a fresh install, or after a game has finished and no save remains. Three
  options, all buildable from the existing
  [Button](shared-components.md#button) states:
  1. **Hidden** — not laid out at all. The component's `Hidden` state is
     already *"not laid out at all — buttons do not leave gaps behind"*, and
     because the `action` row is centred as a row, the screen then shows a
     single centred `NEW` exactly where `Play` used to be.
  2. **Disabled** — always laid out, at 40 % opacity and unpressable. The
     component already has this state; it needs only a condition saying when it
     applies.
  3. **Absent** — `RESUME` is not built yet and the screen ships with `NEW`
     alone until save/resume exists. This one differs from the other two in kind:
     it makes the two-button mockup a forward record rather than a build target.
- **Is there a `How to play` button here?** Proposed: no. The rules are one
  sentence long, Connor already knows them, and
  [an in-app tutorial is parked](../future-ideas.md). If one is wanted it goes
  beneath the `RESUME`/`NEW` row as a secondary button — and it needs its own
  wireframe for the screen it opens. Note that it would make three interactive
  elements, so it is a change to this page's first invariant as well as to its
  layout.
- **Does the title screen come back after a game, or does `Play again` restart
  directly?** Both exist on [game over](game-over.md); this screen does not care
  which is used.
