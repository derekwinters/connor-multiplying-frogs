# Settings dialog

The gear on the [game board](game-board.md). It holds the rules, the way out of
a game, and — since [#322](https://github.com/derekwinters/connor-multiplying-frogs/issues/322)
— an About section saying what this game is, who designed it, and which version
you are running.

## Invariants

**Invariant:** opening this changes nothing about the game. It is a menu, not a
turn.
**Invariant:** the only destructive thing on it confirms — see
[end-game confirm](end-game-confirm.md).
**Invariant:** it is reachable on anyone's turn, not only the current player's.
It is the way out of a game, and a way out that is only available to whoever
happens to be holding the tablet is not a way out.
**Invariant:** nothing here can change a rule of the game mid-play. There is no
difficulty setting, no player count, no undo.
**Invariant:** the version string comes from `/VERSION`. Moving where it is
drawn does not change where it is read from — the About block renders
`AppVersion`, and a build stamp printed as a literal is a build stamp that will
eventually be wrong.
**Invariant:** the About block is text and only text. It carries no control, no
link, and nothing tappable. It is the one part of this dialog that is not a way
to do something.

## Regions

| Region | Job |
| --- | --- |
| `title` | `Settings` |
| `actions` | `How to play`, then `End the game` |
| `about` | The game's name, `Designed by Connor`, and the version |
| `controls` | `Back to the game` |

`about` replaces the region this page used to call `footprint`, which held the
version string alone. The name changes because a footprint is a thing tucked in
a corner and this is a section with a heading in it — see
[the constants this page used to carry](#the-constants-this-page-used-to-carry).

## Anchors

A centred [dialog](shared-components.md#dialog), `SettingsDialogWidth` by
`SettingsDialogHeight`. Actions are a single left-aligned column at full inner
width; the primary button is right-aligned below them; the `about` block is
left-aligned at the panel's inner left edge.

The primary button is described as *right-aligned below the actions* rather
than *bottom-right*, which is what this page used to say, because under one of
the two placements it is no longer flush to the bottom — see
[open question 1](#open-questions).

`End the game` is separated from everything below it by
`ButtonDestructiveGap` — 96 px, which is nearly a whole button's height of
empty space. That gap is the layout, not decoration: `Back to the game` is the
button people reach for without looking, and it must not be adjacent to the one
that ends everybody's game.

Because that gap is the load-bearing number, the actions column is positioned
from the **bottom of the panel up** — `End the game` sits `ButtonDestructiveGap`
above the button row, and `How to play` sits `SettingsActionGap` above `End the
game`. The column's top edge is whatever is left over. Laid out the other way
round, from the top down, the gap would be a consequence of arithmetic and a
longer label or a shorter panel could quietly close it.

**Where `about` sits is the one thing this page does not yet fix.** Two
placements are drawn, and [open question 1](#open-questions) is the choice
between them. Everything above is true of both.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `SettingsDialogWidth` | 900 px |
| Dialog height | `SettingsDialogHeight` | 970 px |
| Action button width | `SettingsActionWidth` | 788 px |
| Gap between `How to play` and `End the game` | `SettingsActionGap` | 96 px |
| Gap below `End the game` | `ButtonDestructiveGap` | 96 px |
| Game name size, in `about` | `SettingsAboutNameSize` | 40 px |
| Credit size, in `about` | `SettingsAboutCreditSize` | 32 px |
| Version size, in `about` | [`VersionLabelSize`](title-screen.md#named-constants) | 28 px |
| Line box, as a ratio of each line's own size | `SettingsAboutLineHeight` | 1.2 |
| Gap between the three lines of `about` | `SettingsAboutLineGap` | 12 px |
| Height of the whole `about` block | `SettingsAboutBlockHeight` | 144 px |

`SettingsAboutBlockHeight` is derived, and the arithmetic sums exactly, which is
why it is a round number rather than a rounded one:

```text
40 × 1.2  +  12  +  32 × 1.2  +  12  +  28 × 1.2
   48     +  12  +    38.4    +  12  +    33.6     =  144
```

It is named anyway rather than left to be recomputed. The block's height is what
sets `SettingsDialogHeight`, and a number that three other numbers depend on is
a number worth being able to say out loud.

The two placements each need one constant of their own, and only one of these
two rows survives [open question 1](#open-questions):

| Placement | Element | Constant | Value |
| --- | --- | --- | --- |
| In the footprint | `about` up from the panel's bottom edge | `SettingsAboutBottomOffset` | 60 px |
| Under the title | `about` down from the panel's top edge | `SettingsAboutTopOffset` | 164 px |

`SettingsAboutTopOffset` is not a free number: it is `DialogPadding` (56) + the
title's line box (68) + `DialogTitleGap` (40). It is stated as a single offset
because that is how the block is positioned, and derived here so a change to
`DialogTitleGap` is visibly a change to this too.

The footprint placement also needs a gap above the block, separating it from the
button row: `SettingsAboutGap`, 56 px. It is deliberately **not**
`ButtonDestructiveGap`. Nothing in `about` is a button, so the destructive gap
has no work to do there, and reusing it would imply a safety rule that isn't
being applied.

### The built dialog does not have these numbers yet

`SettingsDialogView` carries `SettingsDialogHeight = 760f` and
`SettingsVersionBottomOffset = 60f`, which is what this page said before this
wireframe. That divergence is deliberate and is the normal state of a screen
between its wireframe and its implementation: **this page is authoritative and
the code follows it**, per
[wireframe before UI code](../../engineering/ui-design-process.md). The
implementation issue is opened against whichever placement
[open question 1](#open-questions) settles on, and it is the PR that brings the
two back into agreement. Nothing should be built from this page until then,
because half of it is still a question.

### The constants this page used to carry

| Was | Is now | Value | Note |
| --- | --- | --- | --- |
| `SettingsVersionBottomOffset` | `SettingsAboutBottomOffset` | 60 px | Unchanged; now measures the whole block, not one line |
| `SettingsDialogHeight` | `SettingsDialogHeight` | 760 → 970 px | The About block's 144 px plus the gap it needs |

The offset is renamed and not removed, for the same reason the title screen's
[`Play*` constants were renamed](title-screen.md#what-became-of-the-play-constants):
its value has not changed and its job has, and a constant named after a version
string will eventually be applied to one.

`SettingsVersionBottomOffset` only survives at all under the footprint
placement. Under the header placement there is nothing left at the bottom of
the panel to offset, and the constant goes.

## Elements

- **`How to play`** — secondary [button](shared-components.md#button). Opens the
  rules. Still disabled: the screen it opens is being designed in
  [#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324)
  and until that lands there is nothing to open.
- **`End the game`** — destructive button. Opens
  [end-game confirm](end-game-confirm.md). Never ends anything by itself.
- **`Back to the game`** — primary button. Closes and returns to exactly the
  board state that was there.
- **`about`** — three lines, left-aligned, stacked, `SettingsAboutLineGap`
  apart. Not a button, not tappable, no state:
    1. **`Multiplying Frogs`** at `SettingsAboutNameSize`, bold. The same words
       the title screen already draws — `TitleScreenView`'s own `TitleLabel`,
       at a smaller size. This is the game saying its own name, not a second
       wordmark. Note that `SettingsDialogView` has a `TitleLabel` of its own
       and it is the string `Settings`; these two are not the same constant and
       must not be made to share one.
    2. **`Designed by Connor`** at `SettingsAboutCreditSize`. Derek's wording,
       and the default answer; see [open question 3](#open-questions), which is
       Connor's to settle because the line is about him.
    3. **The version** at `VersionLabelSize`, in the mockup's `--line` grey,
       from `/VERSION` via `AppVersion`. The same value doing the same job as
       the title screen's own version readout, under the identical name — not a
       second constant for the same number.

## Behaviour

- Opened by the gear on the board, or by the hardware back button on the board.
- Hardware back inside this dialog does what `Back to the game` does — the
  least destructive button, per the
  [dialog rule](shared-components.md#dialog).
- Closing returns to the board with nothing changed: same turn, same positions,
  same enabled `Roll`.
- Nothing in `about` responds to a touch, including a long one. There is no
  build-info easter egg behind seven taps on the version.

## Why it is this thin, and what the About block does to that argument

This page used to argue that the dialog holds exactly **the two things a player
actually needs mid-game — the rules, and the exit** — and that a settings screen
padded out with toggles that do nothing is worse than a short one. That argument
stands, and v1 still has nothing to configure: no audio to mute, no account to
sign out of, no difficulty to set, no data to clear. Every one of those is either
[parked](../future-ideas.md) or ruled out by `CLAUDE.md`.

The About block is a third thing, and it is honestly **not** one a player needs
mid-game. The argument for it anyway is that it is not a control:

- **It cannot be operated, so it cannot be operated by mistake.** The reason the
  dialog stays thin is that every extra control is another thing a child can
  press mid-turn. Text adds nothing to that count.
- **The version was already here**, doing exactly this job, and doing it as a
  lonely string in a corner. The block is mostly that string given the two lines
  of context that make it legible.
- **There is nowhere else for the credit to go.** The title screen is
  [capped at two interactive elements](title-screen.md#invariants) and its art
  fills the canvas; the board is the game. This is the only surface in v1 that
  can hold a sentence.

What has **not** changed is the rule underneath the argument: nothing lands on
this dialog that a player could press. If audio ever arrives, the mute control
belongs here, and it is still the reason this dialog is a list rather than two
buttons in a row.

## Mockup

Two, and they are a **live pair** — a real choice drawn twice, per
[the loop](../../engineering/ui-design-process.md#the-loop). They are the same
900 × 970 canvas, the same stylesheet, the same three lines of text, differing
in exactly one thing: where the block sits.

- [`mockups/settings-dialog.html`](mockups/settings-dialog.html) — **in the
  footprint**, bottom-left, in a band of its own beneath the button row.
- [`mockups/settings-dialog-about-header.html`](mockups/settings-dialog-about-header.html)
  — **under the title**, reading as a header.

Until [open question 1](#open-questions) is settled, an edit to this dialog's
layout has to be made in **both** files. That is the cost of the pair and the
reason it should not stay open long — the same cost the
[game board's water pair](mockups/index.md) carries.

### What drawing them changed

[#322](https://github.com/derekwinters/connor-multiplying-frogs/issues/322)
proposed the footprint option as the block *"sharing its row with `Back to the
game` on the right"*, and expected it to be the cheap one because the version
already lived down there. It is not drawable that way. `Back to the game`
renders 531 px wide, so of `SettingsActionWidth`'s 788 px it leaves 257 px on
the left, and `Designed by Connor` at `SettingsAboutCreditSize` needs about 300.
Drawn as proposed, the primary button sits on top of the credit line.

So the block gets a band of its own beneath the button row, and both placements
cost the panel the same 210 px of height. **Height is therefore not what picks
between them**, which is the opposite of what the issue expected, and it is the
kind of thing a sum does not tell you and a picture does.

## Open questions

- **1. Where does the About block go — the footprint, or under the title?**
  The live question, and the reason there are two mockups. Neither is cheaper
  than the other, so it is a question about what the block should read *as*:
    - **In the footprint**, it is a colophon: the name and credit are the small
      print under the things the dialog is for. The cost is that `Back to the
      game` is no longer `DialogPadding` up from the panel's bottom edge — it is
      lifted 260 px to make room for the band underneath — and
      [shared-components.md](shared-components.md#dialog) says a dialog's
      buttons "sit in a single row along the bottom of the panel". This
      placement puts something below that row.
    - **Under the title**, it is a header: the dialog announces the game before
      it offers the two actions, the button row stays exactly where the shared
      component puts it, and `Designed by Connor` is the second thing read
      rather than the second-to-last. The cost is that it prints the version
      where headers go, and a version is a footnote.

    Connor picks, on the tablet, at 1:1. Closing
    [#322](https://github.com/derekwinters/connor-multiplying-frogs/issues/322)
    is the approval, and the losing file is deleted then — not kept, because a
    mockup nobody can build to is a mockup that confuses the next reader.
- **2. Is `Settings` still the right title for this dialog?** Not proposed as a
  change, flagged as a thing to notice: a dialog titled `Settings` whose
  contents are two buttons and a credit is arguably titled wrong, and it is more
  obviously wrong under the header placement, where `Settings` and
  `Multiplying Frogs` sit four lines apart doing similar jobs. Left open
  deliberately — renaming it is a change to what the gear means, which is a
  bigger question than where a credit line goes.
- **3. Is `Designed by Connor` the right words?** Derek wrote it and it is the
  default. It is the one line in the game that is about Connor, so it is his to
  change, and it is worth actually asking him rather than shipping the default
  because nobody did.
- **4. Does the title screen change too? Proposed: no.** It shows the same name
  and its own version readout at the same `VersionLabelSize`, and after this
  change the two pages both draw a version — which is fine, because they are
  drawing the same value from the same place under the same constant, not
  disagreeing about where a version lives. The title screen is already an about
  screen in effect: it is the game's name at 160 px over its own art. Adding a
  credit line there is a change to
  [its first invariant](title-screen.md#invariants) about what is on that
  screen, and it is not what
  [#322](https://github.com/derekwinters/connor-multiplying-frogs/issues/322)
  asked for.
- **What does `How to play` open?** Being answered by
  [#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324),
  which is designing the screen. Until it lands the button stays present and
  disabled, which is the answer this page already settled: a disabled button
  that appears later is less confusing than a button that appears from nowhere.
