# Turn panel

The panel a turn happens on: the three piles face down, the die you roll, then
the card you drew, turned over, beside the grid you work it out on.

It replaces two dialogs. A turn used to cross [roll and card](roll-and-card.md),
which showed the die, the pile and the card behind a `Solve it` button, and then
the separate [working-out grid](working-out-grid.md). Now it is **one panel in
three phases**:

1. **Ready**: the three piles face down and the `Roll` button.
2. **Rolling**: the die tumbles through 30 faces and settles on the face that
   was rolled.
3. **Solving**: the drawn pile's card is picked up, turned over and laid across
   all three piles, showing the problem. The die goes, the working-out grid and
   keypad come in, and the card stays on screen until the answer is checked.

Wireframed on
[#402](https://github.com/derekwinters/connor-multiplying-frogs/issues/402).
The layout, the header words and the timings below were agreed by Derek in an
interactive prototype before this page was written. This page records those
decisions, with the prototype's positions worked out again against the
[pile card](shared-components.md#pile-card)'s numbers so that every sum
comes out exactly.

> **How the spec is changing (#529).** The first version of this page put a
> 405 px card beside the grid, and on a Hard card `Help me`'s printed products
> had nowhere to go: they needed about 154 px left of the grid and there were
> 95. That was
> [#529](https://github.com/derekwinters/connor-multiplying-frogs/issues/529).
> Derek chose **layout D** from four drawn options: a **smaller pile card**,
> 200 × 360 instead of 225 × 405, and **slimmer keypad keys**, 124 px instead
> of 140. Together they free 93 px across, and the products now fit beside the
> grid at full size with the grid and the card untouched. The card's numbers
> are the [pile card](shared-components.md#pile-card)'s and the keys are the
> [working-out grid](working-out-grid.md#named-constants)'s; both pages say
> what changed. The sums below are layout D's.

**This page owns the panel**: its regions, where they sit, the die, the
`Roll` button, the phases and the motion between them. It does **not** own two
things it shows:

- **The card itself.** Its back, its front, its size and its pick-up and
  turn-over are the [pile card](shared-components.md#pile-card)'s, and the
  `PileCard*` and `RevealedCard*` constants are referenced here, never
  restated with different values.
- **The grid and the keypad.** Their columns, rows, carry strips, the growable
  addition section, `Help me`, the keypad and `Check it` are still the
  [working-out grid](working-out-grid.md)'s. The grid is now the Solving phase
  of this panel instead of a dialog of its own. Nothing inside it changed.

## Invariants

**Invariant:** the roll happens in `Core` before anything of it is drawn on
screen. `Roll.Draw(Rng)` picks the face from the game's single seeded `Rng`.
The tumble is a readout of a result that already exists, never a source of
one. No view code calls a random number generator to pick a face, including
the 29 faces the tumble shows before the last one. Those come from `Core` too.
**Invariant:** the face shown at the end of the tumble is the rolled face, and
the card that is picked up and turned over is the card drawn from the pile that
face maps to: 1 or 2 is Easy, 3 or 4 is Medium, 5 or 6 is Hard.
**Invariant:** the panel cannot be dismissed, in any phase. Hardware back is
inert. Once the board's `Roll` is pressed, the turn is under way.
**Invariant:** nothing on the panel implies one pile is worth more than
another. The three piles are drawn the same size, in the same style, with no
score, star or bonus. They differ only in their pips and their name. The lime
glow marks the pile that was drawn, never the best or hardest one.
**Invariant:** the whole animation can be skipped by tapping anywhere. A tap at
any moment from the `Roll` press until the panel has settled in Solving jumps
straight to the settled Solving state. A four-player game plays this forty
times, and by the fortieth nobody wants to watch it.
**Invariant:** the tumble shows only faces that a real die could roll into
next. Two faces in a row are never the same, and never opposite each other.
Opposite faces add up to 7.
**Invariant:** the two cards that were not drawn stay exactly where they are
and are covered up. They do not fade, slide, shrink or unload. The turned-over
card lies on top of them.
**Invariant:** the drawn card stays on screen, face up, for the whole of
Solving. The player can always see the problem they are working on and the
pile it came from.
**Invariant:** the grid is never made smaller to fit beside the card. If
something does not fit at the
[working-out grid](working-out-grid.md)'s own sizes, that is reported, not
shrunk away. Everything fits: the widest card, with `Help me` pressed and all
six products printed, overlaps nothing.
**Invariant:** the grid does not move across when `Help me` is pressed. The
room the printed products need on the grid's left is kept beside it from the
moment it comes in, whether or not the button is ever pressed. (The grid does
still re-centre **down** the panel when the button grows its addition section,
which is the [working-out grid](working-out-grid.md#behaviour)'s existing
rule for a grown section.)

## Requirements

Each is one behaviour, numbered so a build issue and its test can name it.

- **TP-1: The board's `Roll` opens the panel.** <a id="tp-1"></a> Pressing
  `Roll` on the [game board](game-board.md) opens the turn panel in Ready.
  It opens the way every dialog opens ([PS-8](style-guide.md#ps-8)): a fade
  and a grow from `DialogOpenScale`, over `DialogFadeDuration`, over the
  `DeepPond` scrim. The panel covers the board.
- **TP-2: The panel's `Roll` starts the turn.** <a id="tp-2"></a> Pressing
  the panel's own `Roll` button rolls the die in `Core`, draws the card from
  the pile the face maps to, and moves the panel to Rolling. The button fades
  out over `TurnPanelRollFadeDuration` and cannot be pressed again.
- **TP-3: The tumble.** <a id="tp-3"></a> The die shows `DieTumbleFaceCount`
  faces, each held for `DieTumbleStepDuration`, in the order `Core` gives them.
  The last face is the rolled face. Every other face is a neighbour of the face
  after it: never the same face, and never its opposite. `Core` builds the
  sequence backwards from the rolled face, using the game's seeded `Rng`.
- **TP-4: The jiggle.** <a id="tp-4"></a> While it tumbles, the die is turned
  `DieTumbleJiggle` clockwise on the first face, the same anticlockwise on the
  second, and so on alternately. On the last face it is turned 0°.
- **TP-5: The match.** <a id="tp-5"></a> When the last face lands, the die
  rests for `DieSettleHold`. At the same moment a `Lime` glow fades in around
  the drawn pile's card, so the link from the die to the pile is seen before
  the card moves.
- **TP-6: The reveal.** <a id="tp-6"></a> After `DieSettleHold` the drawn card
  is picked up and turned over exactly as the
  [pile card](shared-components.md#pile-card) specifies
  ([PC-6](shared-components.md#pc-6), then [PC-7](shared-components.md#pc-7)).
  The glow goes with the card as it lifts, and fades out
  `PileGlowOffDelay` into the turn-over.
- **TP-7: The phase reveal.** <a id="tp-7"></a> When the card lands, over
  `PhaseRevealDuration`, the die fades out where it stands and the grid and
  keypad fade in where they stand. Nothing slides. At the start of that fade
  the header's words change from `Roll the die` to `Work it out`, and the
  skip hint fades out.
- **TP-8: Skip.** <a id="tp-8"></a> A tap anywhere on the screen during Rolling,
  or during the move into Solving, jumps straight to the settled Solving state:
  - the die is gone;
  - the drawn card is turned over in its final place;
  - the other two cards are where they always were;
  - the glow is off;
  - the grid and keypad are in;
  - the words say `Work it out`;
  - the hint is gone.

  The tap that skips does nothing else. It does not reach the grid or the
  keypad underneath. While the animation runs, the hint
  `Tap anywhere to skip` sits at the bottom right.
- **TP-9: Back is inert.** <a id="tp-9"></a> Hardware back does nothing in
  any phase.

## Regions

Each region keeps one name in every phase it appears in.

| Region | Job | Ready | Rolling | Solving |
| --- | --- | --- | --- | --- |
| `header` | Whose turn it is, what to do now, and `Help me` once there is a grid | Chip, `Roll the die` | Chip, `Roll the die` | Chip, `Work it out`, `Help me` |
| `piles` | The three pile cards face down, and then the drawn card turned over on top of them | Three backs | Three backs; the drawn one glows at the end | The drawn card's front, covering all three |
| `die` | The one die | Resting on 1 | Tumbling, then resting on the rolled face | Faded out |
| `roll` | The panel's `Roll` button | Shown | Faded out | — |
| `grid` | The working-out grid: [working-out grid](working-out-grid.md)'s `grid` and `help` | — | — | Faded in |
| `keypad` | The keypad and `Check it`: [working-out grid](working-out-grid.md)'s `keypad` and `submit` | — | — | Faded in |
| `hint` | `Tap anywhere to skip` | — | Shown | — |

`die` and `roll` together are the **stage**: the space to the right of
`piles` in Ready and Rolling. `grid` and `keypad` take that same space in
Solving.

## Anchors

The panel is the full-bleed [dialog](shared-components.md#dialog):
`TurnPanelWidth` × `TurnPanelHeight`, which is the canvas inset by 48 px on
every side, centred on the canvas over the scrim. **Every position on this
page is measured from the panel's outer edge, outline included**, the same way
the pile card measures its own numbers. The content runs from `DialogPadding`
(56 px) inside the left edge to `DialogPadding` inside the right edge:
1824 − 2 × 56 = **1712 px**.

- **`header`** is the [working-out grid](working-out-grid.md#anchors)'s
  header band, unchanged and in place for all three phases. The chip is at the
  content's left edge, `GridHeaderTop` (44 px) down. The words follow it,
  `GridHeaderGap` (32 px) after the chip, so they move with the length of the
  name. `Help me` is pinned to the content's right edge, in Solving only. The
  band ends `GridHeaderHeight` (140 px) below the panel's top.
- **`piles`** is pinned to the content's left edge, `TurnPanelPileTop` down.
  This is the same 216 px as `KeypadTop`, so the top of the cards lines up
  with the top of the keypad in Solving. Its footprint is the pile card's: in
  the chosen layout, a column `PileCardLongSide` wide and
  3 × `PileCardShortSide` + 2 × `PileCardGap` tall, **360 × 648**. The cards
  sit `PileCardGap` (24 px) apart, Easy at the top, Medium, then Hard.
- **The stage** runs from the right edge of `piles` to the content's right
  edge. `die` and `roll` are centred in it across, one above the other,
  `TurnPanelDieRollGap` apart. As a group they are centred down the height of
  `piles`.
- **`keypad`** is the grid page's. It is pinned to the content's right edge,
  `KeypadTop` (216 px) down, `KeypadWidth` (404 px) wide, with `Check it`
  `KeypadSubmitGap` below it. Its keys are `KeypadKeySize` (124 px), slimmer
  than the 140 px they were, for layout D.
- **`grid`** is centred in the space left over, together with the room its
  printed products need. Across, that space runs from the right edge of
  `piles` to the keypad's left edge, 948 px. What is centred in it is one
  block: `GridHelpColumnWidth` (128 px) for the `help` column, `GridHelpGap`
  (32 px), then the grid. The block is the same whether or not `Help me` has
  been pressed, so the grid's left edge is too. Down, the grid alone is centred
  from the bottom of the header band to the panel's last usable pixel,
  unchanged at 908 px.
- **`help`** is the grid page's right-aligned column, its right edge
  `GridHelpGap` left of the grid. On a Hard card its widest line, `40 × 300`,
  is 122.5 px, inside the 128 px kept for it.
- **`hint`** has its right edge at the content's right edge and its bottom
  `TurnPanelSkipHintBottom` above the panel's bottom edge. That is in the
  panel's padding, below everything else. The hint only shows while the stage
  is empty below the die.

**Where an odd pixel has to be split, it goes right, or down.** Centring an
even width in an odd space, or an odd width in an even one, leaves one pixel
over. Rather than place anything on a half pixel, the space before the thing
is rounded down and the space after it takes the remainder. The code centres
the same way, with integer division.

**On a screen that is not 1920 × 1200**, nothing here moves. The panel is
laid out in the reference canvas like every dialog
([what fills a screen that is not 16:10](shared-components.md#what-fills-a-screen-that-is-not-1610)),
and the scrim reaches the screen's edges.

### The horizontal arithmetic

Every row across the panel adds up to the panel exactly. Numbers are pixels,
from the panel's outer left edge.

**The panel:**

```
1824 = 56 (DialogPadding) + 1712 (content) + 56 (DialogPadding)
```

**Ready and Rolling**, piles beside the die:

```
1712 = 360 (piles) + 1352 (stage)
1352 = 546 + 260 (DieFaceSize) + 546                    die at 56 + 360 + 546 = 962
1352 = 466 + 420 (TurnPanelRollButtonWidth) + 466       Roll at 56 + 360 + 466 = 882
```

**Solving**, card, products, grid and keypad. The grid width is
`n × GridCellSize + (n − 1) × GridCellGap` for `n` columns, operator column
included. The 160 is `GridHelpColumnWidth` (128) + `GridHelpGap` (32), and it
is in every row whether or not `Help me` has been pressed:

```
1712 = 360 (revealed card) + 948 (grid's space) + 404 (KeypadWidth)
 948 = 62  + 160 + 664 (331 × 41, 6 columns) + 62        grid at 56 + 360 + 62 + 160 = 638
 948 = 118 + 160 + 552 (a Medium card, 5 columns) + 118  grid at 694
 948 = 174 + 160 + 440 (68 × 5, 4 columns) + 174         grid at 750
```

On `331 × 41` with `Help me` pressed, the products' right edge is at
638 − 32 = 606, and `40 × 300`, 122.5 px in the committed Fredoka at
`GridHelpItemSize`, starts at 483.5. That is 67.5 px clear of the card's right
edge at 416, and the grid ends 62 px short of the keypad.

The same row in canvas pixels, which is what the mockups are drawn in (add the
panel's 48):

| | Left | Right |
| --- | --- | --- |
| The turned-over card | 104 | 464 |
| `help`, the 128 px kept for it | 526 | 654 |
| `40 × 300`, the widest product | 531.5 | 654 |
| The grid, `331 × 41` | 686 | 1350 |
| The keypad and `Check it` | 1412 | 1816 |

The revealed card is exactly the pile column. It is 200 × 9/5 = 360 wide and
360 × 9/5 = 648 tall: the
[pile card's own check](shared-components.md#the-proportions-are-derived-not-chosen).

**The header**, Solving, with the `Green` chip:

```
1712 = chip + 32 (GridHeaderGap) + Work it out + space + 320 (GridHelpButtonWidth)
```

The chip and the words are as wide as the name and the words need, so the
space between them and `Help me` takes up the rest. With a ten-letter name it
is still over 700 px.

**Down the panel**, for the record, from its outer top edge:

```
1104 = 140 (header band) + 908 (grid's space) + 56 (DialogPadding)
piles:  216 + 648 = 864, leaving 184 above the last usable pixel at 1048
stage:  648 = 80 + 260 (die) + 100 (TurnPanelDieRollGap) + 128 (Roll) + 80
        die at 216 + 80 = 296, Roll at 296 + 260 + 100 = 656
grid:   908 = 88 + 732 (331 × 41 as dealt) + 88        grid at 140 + 88 = 228
        908 = 8 + 892 (after Help me, six rows) + 8     grid at 140 + 8 = 148
help:   first addition row at 148 + 302 = 450, then every 64 px: 450 … 770
keypad: 216 + 544 (4 keys of 124, 3 gaps of 16) + 24 + 128 (Check it) = 912
```

The grid's vertical sums are unchanged from the
[working-out grid](working-out-grid.md#mockup), up to the six-row cap's
892 px in 908. The 302 above the first addition row is the carry strip, the
multiplicand and multiplier rows, the rule and four 8 px gaps. `Roll`'s 10 px
lip ends at 794, inside the pile column's 864.

### What fits beside the card

**Everything, at full size.** The widest card's grid is 664 px, the widest
product is 122.5 px, and with `GridHelpGap` between them they need 818 of the
948 px between the card and the keypad. Nothing in the grid was shrunk, moved
inside, or changed size to make room for the card.

It did not fit at first. Beside a 405 px card, with 140 px keys, the grid had
855 px and was centred in it, leaving 95 px on its left, and the product column
needs 154. Derek chose layout D on
[#529](https://github.com/derekwinters/connor-multiplying-frogs/issues/529):
the card loses 45 px across (405 → 360) and the keypad loses 48 (452 → 404),
which gives the grid's space 93 px more, 855 → 948. He chose it from four
drawn layouts. The products, `GridHelpGap`, the grid and the card's design all
keep their sizes; only the card's scale and the keys changed.

## Named constants

Panel geometry and timing. The pile card's numbers are the
[pile card](shared-components.md#pile-card)'s. The grid's and the keypad's are
the [working-out grid](working-out-grid.md#named-constants)'s. Neither is
restated here.

### The panel

| Element | Constant | Value |
| --- | --- | --- |
| Panel width | `TurnPanelWidth` | 1824 px, which is `DialogMaxWidth` |
| Panel height | `TurnPanelHeight` | 1104 px, which is `DialogMaxHeight` |
| Padding | `DialogPadding` | 56 px (shared) |
| The piles' top, down from the panel's top edge | `TurnPanelPileTop` | 216 px, the same as `KeypadTop` |
| Gap between the three face-down cards | `PileCardGap` | 24 px. The pile card owns it; see below |
| `Roll the die` and `Work it out` | `GridPromptSize` | 44 px, `FontDisplay` medium, `InkSoft` (the grid page's) |

**The issue's `PileRowGap` is not declared.** The gap between the cards is
`PileCardGap`. That is the `G` in `L² = 3S² + 2GS`, so it cannot change
without changing the card. A second name for the same 24 px is how the two
would drift apart.

### The die

The die keeps its old constant names from [roll and card](roll-and-card.md),
with the prototype's values. It is bigger, outlined and shadowed in the
[Pond style](style-guide.md).

| Element | Constant | Value |
| --- | --- | --- |
| Die face, square | `DieFaceSize` | 260 px (was 240) |
| Corner radius | `DieCornerRadius` | 52 px (was 40) |
| Outline | `DieBorderWidth` | 7 px `Ink` (was 4, grey) |
| From the inside of the outline to the pip grid | `DiePipInset` | 36 px (was 34) |
| Pip diameter | `DiePipDiameter` | 46 px (was 40), `Ink` |
| Shadow | `ShadowDie` | 12 px straight down, `Ink` at 35 %, no blur |
| Fill | `White` | `#FFFFFF` |
| Gap between the die and `Roll` | `TurnPanelDieRollGap` | 100 px |
| The face it rests on in Ready | — | 1 |

The pip grid adds up: 260 − 2 × 7 − 2 × 36 = **174 px**, three cells of
58 px, and a 46 px pip sits centred in each with 6 px round it. The pips are
on the same 3 × 3 lattice as the pile card's die pictures, and in the same
proportion (46 / 260 ≈ 11 / 62).

### The `Roll` button

The panel's own `Roll`, a primary [button](shared-components.md#button). It is
smaller than the board's `Roll` (480 × 144), because it shares the stage with
the die.

| Element | Constant | Value |
| --- | --- | --- |
| Width | `TurnPanelRollButtonWidth` | 420 px |
| Height | `TurnPanelRollButtonHeight` | 128 px, the same as `CheckButtonHeight` |
| Label | `TurnPanelRollLabelSize` | 60 px, `FontDisplay` semibold, `White` |
| Fade when pressed | `TurnPanelRollFadeDuration` | 0.2 s |

### The glow

The glow marks which pile the die sent you to. The
[pile card](shared-components.md#pile-card) leaves it to this page.

| Element | Constant | Value |
| --- | --- | --- |
| Colour | `Lime` | `#87BD01` |
| Ring width | `PileGlowWidth` | 10 px |
| Gap between the card's edge and the ring | `PileGlowGap` | 4 px, so the ring's outer edge is 14 px out |
| Ring corner, outer edge | `PileGlowRadius` | 36 px |
| Fade in, and fade out | `PileGlowFade` | 0.15 s |
| When it goes, measured into the turn-over | `PileGlowOffDelay` | 0.245 s, 35 % of `PileCardRevealDuration` |

### The hint

| Element | Constant | Value |
| --- | --- | --- |
| Words | — | `Tap anywhere to skip` |
| Size | `TurnPanelSkipHintSize` | 28 px, `FontBody` at `WeightQuiet`, `Reed` |
| Its bottom edge above the panel's bottom edge | `TurnPanelSkipHintBottom` | 32 px |
| Fade in and out | `TurnPanelSkipHintFade` | 0.2 s |

### The timing

| Element | Constant | Value |
| --- | --- | --- |
| Faces the tumble shows, the last being the rolled face | `DieTumbleFaceCount` | 30 |
| How long each face is held | `DieTumbleStepDuration` | 0.04 s (40 ms), so 1.2 s in all |
| The die's turn on alternate faces | `DieTumbleJiggle` | 8°, clockwise and anticlockwise in turn; 0° on the last |
| The die resting on the rolled face, the pile glowing | `DieSettleHold` | 0.35 s |
| Pick-up, hold, turn-over | `PileCardLiftDuration`, `PileCardLiftHold`, `PileCardRevealDuration` | 0.3 s, 0.15 s, 0.7 s (the pile card's) |
| Die out, grid and keypad in | `PhaseRevealDuration` | 0.25 s |

**Why 40 ms.** Thirty faces at 40 ms is 1.2 s, which is the same length as the
old dialog's whole entry. At 25 faces a second each face is still seen as a
face rather than a blur, and the turn changes are fast enough to read as a
die tumbling rather than a slideshow. It is short enough to sit through on most
turns of a four-player game. The ones nobody wants to watch are what
[skip](#tp-8) is for. Derek agreed it in the prototype.

**The whole sequence, from the panel's `Roll`:**

| From | To | What happens |
| --- | --- | --- |
| 0 | 0.2 s | `Roll` fades out; the hint fades in |
| 0 | 1.2 s | 30 faces, 40 ms each, jiggling |
| 1.2 s | 1.55 s | The die rests on the rolled face; the glow fades in |
| 1.55 s | 1.85 s | The drawn card is picked up |
| 1.85 s | 2.0 s | It rests, picked up |
| 2.0 s | 2.7 s | It turns over and lands on the three piles; the glow fades out from 2.245 s |
| 2.7 s | 2.95 s | The die fades out, the grid and keypad fade in, the words change, the hint fades out |

About three seconds, end to end, or one tap.

## Elements

- **Player chip**: the active [dialog chip](shared-components.md#player-chip),
  showing the player's frog and name, in every phase. Four players share the
  tablet, and the board behind is dimmed, so the panel says whose turn it is.
- **`Roll the die` / `Work it out`**: the words beside the chip. They say what
  to do now. `Roll the die` shows in Ready and Rolling, and changes to
  `Work it out` once the card is turned over. **The old word `rolled` is gone.**
  It made the chip into a sentence about something that had already happened.
  The panel now opens before the roll, so the words tell you what to do next
  instead.
- **Pile cards × 3**: [pile cards](shared-components.md#pile-card), Easy,
  Medium and Hard, face down. They are not buttons. The roll picks the card,
  and the player never does.
- **Die**: one six-sided die, drawn with pips, never a numeral. It is not a
  button either.
- **`Roll`**: the panel's primary button. Pressing it rolls; see [TP-2](#tp-2).
  This is a second press after the board's `Roll`, on purpose: the board's
  opens the panel, and this one throws the die.
- **The revealed card**: the drawn card's front, carrying the problem and, on
  its small log, the pile's name. **The drawn pile is named in words for the
  whole of Solving.** The old grid showed it only in its readout pill.
- **Working-out grid, keypad, `Check it`, `Help me`**: exactly as the
  [working-out grid](working-out-grid.md#elements) specifies. `Check it` is
  disabled until there is a digit in the answer row, so it arrives disabled.
- **`Tap anywhere to skip`**: words, not a button. The whole screen is the
  thing to tap.

**The grid's card readout pill is gone.** It used to show
`331 × 41 · hard pile` in the header, because the grid dialog had no other way
to show the card. The card itself is now on screen beside the grid, bigger,
and named, so the pill would say the same thing twice. The prototype Derek
agreed has no pill.

## Behaviour

- **Opening.** `Roll` on the board opens the panel in Ready. The panel fades
  and grows in like every dialog. The scrim covers the board, and the board
  underneath does not move.
- **Ready.** The three piles are face down, the die rests on 1, and `Roll` is
  live. Nothing has been rolled yet, and nothing can be dismissed: back does
  nothing, and there is no close. Tapping anywhere but `Roll` does nothing.
- **Ready → Rolling.** `Roll` is pressed. `Core` rolls the die and draws the
  card, then hands the view the rolled face, the drawn card and the tumble's 30
  faces. Only then does anything move. `Roll` fades out over
  `TurnPanelRollFadeDuration`, the hint fades in, and the die starts its
  tumble where it stands. Nothing moves across the panel.
- **Rolling.** The die tumbles through `DieTumbleFaceCount` faces at
  `DieTumbleStepDuration`, jiggling, and lands unturned on the rolled face. It
  rests for `DieSettleHold` while the drawn pile's card glows.
- **Rolling → Solving.** Three things happen in order:
  1. **The card.** It is picked up (`PileCardLiftDuration`), rests
     (`PileCardLiftHold`), and turns over onto the three piles
     (`PileCardRevealDuration`), exactly as the pile card says. The glow lifts
     with it and fades out `PileGlowOffDelay` into the turn-over.
  2. **The card lands.** The other two cards have not moved; they are
     underneath it.
  3. **The phase reveal.** Over `PhaseRevealDuration` the die fades out where
     it stands, the grid and keypad fade in where they will be used, the words
     change to `Work it out`, and the hint fades out.

  Nothing is removed by sliding, and nothing moves except the drawn card.
- **Solving.** The [working-out grid](working-out-grid.md#behaviour)'s
  behaviour, unchanged, with the caret in the answer row's leftmost box. The
  card stays. `Check it` goes to [answer result](answer-result.md), and that
  is how the panel is left.
- **Skip.** See [TP-8](#tp-8). The skip goes from wherever the animation has
  got to, straight to the settled Solving state, with no animation of its own.
- **Back** does nothing, in every phase.

### How the phases line up with the turn

| Phase | On screen | `TurnPhase` |
| --- | --- | --- |
| Ready | Three face-down cards, the die on 1, `Roll` | `WaitingToRoll` |
| Rolling | The die tumbling, then resting; the glow | `RolledAndCardDrawn` |
| Solving | The card being turned over, then the grid and keypad | `Answering` |

The panel moves forward one phase at a time and never back, the way `TurnPhase`
does.

### What the corners do

The turned-over card's corner is `PileCardRadius` × 9/5 = 46.8 px, and the
undrawn cards underneath it have 26 px corners. Where the front covers a card
in a corner position, a sliver of that card's corner shows past the rounder
front, like a card lying on a pile. That is what the two radii give. The
prototype shows it too, and nothing here hides it.

## Mockup

Five, all at 1920 × 1200, static HTML and CSS with no script.

- **Ready, piles beside the die, the chosen layout:**
  [`mockups/turn-panel-ready-piles-beside.html`](mockups/turn-panel-ready-piles-beside.html)
- **Ready, piles above the die, the comparison:**
  [`mockups/turn-panel-ready-piles-above.html`](mockups/turn-panel-ready-piles-above.html)
- **Rolling, mid-tumble** (the die on a 4, turned 8°, the hint showing):
  [`mockups/turn-panel-rolling.html`](mockups/turn-panel-rolling.html)
- **Solving**, `331 × 41` turned over across the piles, with the grid and
  keypad as dealt:
  [`mockups/turn-panel-solving.html`](mockups/turn-panel-solving.html)
- **Solving, `Help me` pressed**: the same card after `Help me`, the button
  disabled, the addition section grown to six rows and all six products printed
  between the card and the grid, which has not moved across:
  [`mockups/turn-panel-solving-help.html`](mockups/turn-panel-solving-help.html)

### Beside or above: settled, beside

The issue asked for two Ready pictures because Derek had said the piles could
go "either above the roll dialog or on the side", and had not chosen. He chose
**beside** in the prototype, and this page is written to it. The two pictures
are kept so the comparison stays visible:

| | Beside, chosen | Above |
| --- | --- | --- |
| The backs | Three landscape cards, 360 × 200, in a column | Three portrait cards, 125 × 225, in a row |
| Turned over | One portrait card, 360 × 648 | One landscape card, 405 × 225 |
| Die pictures on a back | 56 px, 10 px pips | 38 px, 7 px pips |
| Where the problem goes | Stacked, at 108 px | One line, on a card a third the height |

The two pictures differ only where the piles force them to. The header, panel,
die, `Roll` and every size are the same. In **above** the row takes the top of
a 405 px left column, and the die and `Roll` move down into that column
beneath it, because the row now sits where the stage's top half would be.
**Above was not redrawn for layout D.** Its row is still the old column's
405 px, wider than the chosen column's 360, because no whole-number set of its
cards makes a 360 px row (see the
[pile card](shared-components.md#if-the-piles-sit-above-the-die)). It is kept
as the comparison it was. The
die is centred in the column (72 + 260 + 73 = 405), `TurnPanelDieRollGap`
below the row, and `Roll` narrows to the column's 405 px, so it does not stick
out past the content's edge. The row's sum is the
[pile card's](shared-components.md#if-the-piles-sit-above-the-die):
3 × 125 + 2 × 15 = 405.

Nothing about **above** is built unless it is chosen after all. If it is, its
Solving layout still has to be agreed, and so does the pile card's landscape
front.

## Open questions

None is open. The one this page raised is answered:

1. **Where do `Help me`'s printed products go on a Hard card? Answered:
   layout D.**
   ([#529](https://github.com/derekwinters/connor-multiplying-frogs/issues/529))
   Derek chose a smaller pile card and slimmer keys so the products fit beside
   the grid where the grid page always put them. See
   [what fits beside the card](#what-fits-beside-the-card).

The two questions [roll and card](roll-and-card.md#open-questions) carried are
both answered by this panel:

- **Two beats or one?** One panel. The pause it argued for, where the other
  three players see what you drew, is still there: the die rests, the pile
  glows, and the card is picked up before it turns over.
- **Should the pile you drew be visible during answering?** Yes. The revealed
  card carries the pile's name on its log for the whole of Solving.

The [working-out grid](working-out-grid.md#open-questions)'s own open
questions are unaffected by the move into this panel, and stay on that page.
