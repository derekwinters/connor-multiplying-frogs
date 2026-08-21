# Game board

The pond. Every frog's lane, whose turn it is, and the button that starts a
turn. This is the screen the game lives on — everything else is something that
opens on top of it.

## Invariants

**Invariant:** every frog in the game is visible at once, on its own lane,
without scrolling. Four lanes is the maximum and the layout is designed for
four; it never pages, pans, or zooms.
**Invariant:** frogs never share a lane and never interact. A player's entire
state is how far up their own lane they are — see
[the reference material](../reference/index.md#frogs-are-independent).
**Invariant:** the Start log and the End log are **one drawing over separate
positions**. There is one of each on the board however many frogs are playing,
and every frog's position 0 and position 8 are on them. Two frogs on the Start
log are not sharing a space: each is on position 0 *of its own lane*, drawn on
its own lane's centre line, and neither can be moved, blocked, or read off by
the other. The shared log is the only thing on this screen a player could
mistake for frogs sharing something, which is why it is written down here.
**Invariant:** the board never moves on its own. A frog moves only as the direct
result of the answer the player just gave.
**Invariant:** whose turn it is is stated in words in the header, not only shown
by a highlight. Since
[#326](https://github.com/derekwinters/connor-multiplying-frogs/issues/326) the
words are the *only* thing in the header that says it, so this invariant is no
longer a floor the layout comfortably clears — it is the layout. Nothing may
remove those words in favour of a colour, a ring, or an arrow.
**Invariant:** `Roll` is the only way to start a turn, and it is disabled the
moment it is pressed until the turn resolves. A double-tap cannot roll twice.
**Invariant:** nothing on this screen is destructive. Ending the game lives
behind the settings button and a confirm — see
[settings dialog](settings-dialog.md).

## Regions

| Region | Job |
| --- | --- |
| `header` | Whose turn it is, and the settings button |
| `pond` | The lanes — one per frog in the game — and the two logs they share |
| `controls` | The `Roll` button |

The pond, in turn, is:

| Part | Job |
| --- | --- |
| `start-log` | **One** log down the left of the pond, spanning the whole pond band. It is position 0 of every lane |
| `lane` × 2–4 | One per frog in the game, stacked |
| `end-log` | **One** log down the right of the pond, spanning the whole pond band. It is position 8 of every lane |

A lane, in turn, is:

| Part | Job |
| --- | --- |
| `chip` | Which frog this lane belongs to, and how far it has got |
| `track` | Seven lily pads — this lane's positions 1–7 |
| `piece` | The frog itself, sitting on its current position — one of its own lily pads, or the shared log at either end |

The logs are parts of the **pond**, not parts of a lane. That is the whole of
this layout change: a four-frog game draws two logs, not eight.

## Anchors

The three regions are horizontal bands that together fill the full 1200 px
height, in this order and with no gaps:

- `header` — pinned to the top, `BoardHeaderHeight` tall, full width.
- `pond` — everything between the other two bands. Lanes are stacked and
  **vertically centred** within it, so a two-frog game is centred rather than
  clinging to the top.
- `controls` — pinned to the bottom, `BoardControlsHeight` tall, full width.

"Full width" and "the top" mean the screen's, not the reference canvas's, on a
device that is not 16:10 — a band reaches the edges the way the water does.

What the bands *contain* divides in two, and this is the division
[#325](https://github.com/derekwinters/connor-multiplying-frogs/issues/325)
introduced:

- **`header` and `controls` are measured in the reference canvas.** The turn
  banner, the gear and `Roll` are anchored controls, and a wider screen moves
  the two that are anchored to an edge without resizing any of them.
- **The `pond` is measured in the screen.** Its row is a track whose job is to
  show how far along a lane a frog has got, so it spreads to whatever width it
  is given — see [across the pond](#anchors) below.

Every number in the tables below is still in reference-canvas units, and every
one of them still means exactly what it says. `LanePositionGap` is the single
exception, and it is derived rather than typed.

The whole of that rule, and the one control it moves, is
[the bands reach the edges too](#the-bands-reach-the-edges-too).

Across the pond, the nine positions occupy the same nine columns in every lane,
and **the row spans the whole screen** — the chips and the Start log against the
real left safe margin, the End log against the real right one, and the seven
lily pads spread evenly in whatever is left between them:

- `chip` — pinned to the **left** safe margin, `LaneGutterWidth` wide. It does
  not stretch; see [why the gutter does not
  stretch](#why-the-gutter-does-not-stretch).
- `start-log` — `LaneGutterGap` to the right of the chips. Everything to the
  left of its right-hand edge is fixed, so the Start log sits at the same x on
  every screen.
- `track` — this lane's seven lily pads, `LanePositionGap` from the Start log,
  from each other, and from the End log. That gap is **derived from the screen's
  width**, not typed into the table.
- `end-log` — pinned to the **right** edge of the safe area.

`LanePositionGap` is the one number on this page that the screen decides:

```text
LanePositionGap = max( LanePositionGapMin, (screen width − LaneFixedWidth) ÷ LanePositionGapCount )
```

`LaneFixedWidth` is everything on the row that does not stretch, and
`LanePositionGapCount` is the number of gaps the slack is divided between —
one either side of the pads, six among them. Both are in
[the constants table](#named-constants), and the arithmetic that produces them
is [worked through there](#the-horizontal-arithmetic).

**At exactly 1920 px the formula gives exactly 48 px**, which was
`LanePositionGap`'s typed value before this change. That is not a coincidence to
be grateful for — it is the condition this rule was chosen to satisfy, so that
the reference canvas keeps being a picture of the game. It is checked rather
than assumed: [the mockup renders byte-identical](#mockup) at 1920 before and
after.

The floor is what a *narrower* screen gets. Below 1920 px the formula would
start closing the pads up, so it stops: the board keeps its reference width and
is centred, exactly as it always was. **The pond spreads on screens wider than
16:10 and is unchanged on everything else.**

Both logs are `SharedLogHeight` tall, which is the height of the `pond` band on
the reference canvas: they fill it, edge to edge with the two hairlines, whether
two frogs are playing or four. Every lane's centre line therefore crosses both
of them, and a frog on a log still sits on its own lane's line. A shorter screen
loses height from `pond` and nothing else — and the logs lose it with the band,
because they are the band's height rather than a number typed in beside it. The
header and the controls do not shrink, because a smaller `Roll` button is the
wrong thing to trade away.

A *taller* screen is the other side of that, and it is not symmetric: the extra
height is all `pond`, and it is all water. The logs and the lane stack stay
their reference size, centred in the band, because they are the board rather
than the backdrop — see
[the bands reach the edges too](#the-bands-reach-the-edges-too).

## Why the lanes run across

The classroom board's lanes run bottom to top. This one's run left to right, and
that is a deliberate rotation rather than an oversight.

Both were drawn and compared:

| | **Lanes across** (built) | **Lanes up** (rejected) |
| --- | --- | --- |
| Lane runs | Left to right | Bottom to top |
| Lily pad diameter | **112 px** | **64 px** |
| Frog piece | 88 px | 52 px |
| Looks like the cardboard board | No — it is rotated | Yes |
| Reads as progress | Yes — left to right, like a race | Less so on a wide screen |
| Space used | Fills a 16:10 screen | Four tall columns with wide margins |

The deciding number is the pad size. Nine positions have to fit along the lane;
laid out across a 1920-wide screen that gives a 112 px lily pad, and stood up
against a 1200-tall one it gives 64 px. The lily pad is the thing a child looks
at to see how they are doing, and standing the board up costs nearly half of it.

Under [ADR-0001](../../adr/0001-rules-sacred-presentation-ours.md) the board's
orientation is presentation, not a rule, so it is ours to change. **Derek chose
lanes-across** after seeing both drawn.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin | `SafeMargin` | 48 px |
| Header band height | `BoardHeaderHeight` | 128 px |
| Controls band height | `BoardControlsHeight` | 176 px |
| Hairline under `header`, over `controls` | `BoardBandOutline` | 3 px |
| Turn banner text size | `TurnBannerSize` | 52 px |
| Settings button, square | `SettingsButtonSize` | 96 px |
| Settings gear glyph size | `SettingsGlyphSize` | 44 px |
| Settings button outline | `SettingsButtonOutline` | 4 px |
| `Roll` button width | `RollButtonWidth` | 480 px |
| `Roll` button height | `RollButtonHeight` | 144 px |
| `Roll` label size | `RollButtonLabelSize` | 56 px |
| Frog hop duration | `FrogHopDuration` | 0.4 s |
| Lanes in play | `LaneCount` | 2–4 — one per frog in the game |
| Positions in a lane | `LanePositionCount` | 9 |
| Correct answers needed to win | `LaneWinningPosition` | 8 |

`LanePositionCount` is 9 because a lane is the Start log, seven lily pads, and
the End log, and **the End log is the winning space** — a frog has to land on
it. Confirmed by Derek in
[issue #185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185);
recorded in
[the reference material](../reference/index.md#the-end-log-is-the-winning-space).
That is what the `of 8` in every chip's pad count refers to. Drawing the two
logs once for the whole pond does not change that count: a lane is still nine
positions, and two of them are now drawn on something the lane shares.

`LaneCount` is the only quantity on this page that is not fixed at design time.
It sizes the lane stack — `LaneCount × LaneHeight`, centred in the pond — and
nothing else. In particular it does **not** size the logs: they fill the pond
band whatever it is.

The lane:

| Element | Constant | Value |
| --- | --- | --- |
| Lane band height | `LaneHeight` | 184 px |
| Lily pad diameter | `LilyPadDiameter` | 112 px |
| Notch depth, as a fraction of the radius | `LilyPadNotchDepth` | 0.15 |
| Notch widths in the variation table | `LilyPadNotchAngles` | 10°, 15°, 20°, 25° |
| Veins per pad | `LilyPadVeinCount` | 5 |
| Vein gap at the centre, fraction of the radius | `LilyPadVeinInset` | 0.20 |
| Vein gap at the rim, fraction of the radius | `LilyPadVeinOutset` | 0.12 |
| Vein stroke | `LilyPadVeinWidth` | 2.5 px |
| Vein opacity, drawn in `LilyPadEdge` | `LilyPadVeinOpacity` | 0.5 |
| Frog piece diameter | `FrogPieceDiameter` | 88 px |
| Frog piece outline | `FrogPieceOutline` | 4 px |
| Lily pad and log outline | `TrackOutline` | 3 px |
| Gap between positions | `LanePositionGap` | derived — see [Anchors](#anchors) |
| Smallest that gap may be | `LanePositionGapMin` | 48 px |
| Everything on the row that does not stretch | `LaneFixedWidth` | 1536 px |
| Gaps the leftover width is divided between | `LanePositionGapCount` | 8 |
| Chip gutter width | `LaneGutterWidth` | 256 px |
| Gap between chip and track | `LaneGutterGap` | 48 px |

### The lily pad is notched, veined, and varies per pad

A pad is a circle with a wedge cut from it and five veins across it. Both the
notch's **width** and the direction it **points** vary from pad to pad, so a
four-frog board draws 28 pads that are not 28 identical discs.

**The variation is a pure function of where the pad is.** A twelve-entry table,
indexed by the pad's own coordinates:

```text
index = (lane × 5 + position) mod 12
```

| # | Notch | Points at | | # | Notch | Points at |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 20° | 14° | | 6 | 10° | 260° |
| 1 | 10° | 212° | | 7 | 15° | 131° |
| 2 | 25° | 96° | | 8 | 25° | 341° |
| 3 | 15° | 308° | | 9 | 15° | 78° |
| 4 | 20° | 175° | | 10 | 20° | 238° |
| 5 | 25° | 47° | | 11 | 10° | 158° |

Angles are measured the way the mockup's SVG measures them: 0° points right
along the lane, 90° points down. The `× 5` offset exists so the four lanes do
not line up into visible columns; the rotations span the whole circle rather
than clustering, because an earlier draft confined them to the bottom
semicircle and read as "all the notches point down at slightly different
angles", which is not variation.

**Being derived rather than random is the whole design, and it buys four
things:**

- **Nothing is stored and nothing is saved.** A random-per-game variation would
  have to persist, or a resumed game would come back with differently shaped
  pads — and the save format is Core's under
  [ADR-0004](../../adr/0004-core-owns-the-save-format.md), so that would be a
  schema change and a migration for a cosmetic detail.
- **A pad never changes shape** when the board redraws, when a frog hops, or
  between devices and runs.
- **Rotation is free at runtime** — you rotate the transform. So the
  implementation needs **four sprites, one per entry in `LilyPadNotchAngles`**,
  not twelve. The same holds when real art replaces these shapes.
- It is **presentation, not game logic**, so it belongs in the Unity layer
  rather than Core — but it is a static pure function and is testable as one.

**The veins radiate from the circle's geometric centre**, not from the notch's
apex. The notch stopping short of the diameter is the notch's business; the
veins ignore it. Drawn from the apex the fan sits off-centre and leans
differently on every pad, which is what made an earlier draft look uneven.

The five are **symmetric about the notch's own axis**, so one vein runs straight
out opposite the notch and two pairs sit either side of it. An even count
straddles that line and reads less like a leaf. Each vein stops short at both
ends — `LilyPadVeinInset` at the centre so the five do not converge into a dark
hub, and `LilyPadVeinOutset` at the rim.

**None of this changes `LilyPadDiameter`.** 112 px is the number that
[decided lanes-across over lanes-up](#why-the-lanes-run-across); only the
outline and the surface moved. And note that a frog covers all but a 12 px ring
of the pad it sits on, so the veins and most of the notch are visible on
**empty** pads — which is most of the board, but never the pad being looked at.

The two shared logs. They belong to the pond, so they are their own table:

| Element | Constant | Value |
| --- | --- | --- |
| Log width | `LogWidth` | 176 px |
| Log height | `SharedLogHeight` | 896 px — `1200 − BoardHeaderHeight − BoardControlsHeight` |
| Log corner radius | `LogRadius` | 24 px |

`LogHeight` (120 px) is **gone**, not renamed to something with the same
meaning. It was the height of a log sized to sit inside one 184 px lane, and
there is no such thing on this board any more.

`SharedLogHeight` is **one number, not an expression**: the log spans the full
pond, so it is 896 px at two frogs, at three, and at four. That is Derek's
answer, on
[issue #296](https://github.com/derekwinters/connor-multiplying-frogs/issues/296),
to the question this page used to leave open — "log spans full lane space
regardless of player count" — and it is the option the mockup did *not*
originally draw. A two-frog game gets a full-height log with open water above
and below its two lanes, and the board's ends do not change size when a player
joins or leaves.

It is written as the band's own arithmetic rather than as the bare 896 because
the log **is** the band: if `BoardHeaderHeight` or `BoardControlsHeight` ever
moves, the logs move with it and nobody has to remember them.

### The horizontal arithmetic

The row used to sum to 1920 px exactly, and the page said so. It still does at
1920 — but it now sums to the screen's width at every width, because one term in
it is the term that absorbs the difference.

Split the row into what is fixed and what stretches:

```text
LaneFixedWidth  =  48 margin + 256 chip gutter + 48 gap
                     + 176 Start log + 7 × 112 pads + 176 End log
                     + 48 margin
                =  1536 px

LanePositionGapCount  =  8      one gap either side of the pads, six among them

LanePositionGap  =  max( 48, (screen width − 1536) ÷ 8 )
```

Checked at the two widths that are drawn:

| Screen | Slack over `LaneFixedWidth` | `LanePositionGap` | Row sums to |
| --- | --- | --- | --- |
| 1920 px — the reference canvas | 384 | **48 px** | 1536 + 8 × 48 = 1920 |
| 2560 px — the wide drawing | 1024 | **128 px** | 1536 + 8 × 128 = 2560 |

`LaneFixedWidth` is written as its own constant rather than left as a sum
nobody can name, because it is now load-bearing: it is the term the elastic gap
is computed against, so a change to `LaneGutterWidth`, `LogWidth`,
`LilyPadDiameter`, `LaneGutterGap` or `SafeMargin` is a change to it. **If a
constant here changes, `LaneFixedWidth` moves with it** — and unlike before,
nothing else has to, because the gap absorbs the difference by construction.
That is the one genuine simplification in this change: the row used to have to
be rebalanced by hand.

Sharing the logs did not disturb this arithmetic and neither does spreading it:
a shared log still stands in the same column its per-lane predecessor did, and
position 0 is still on the Start log at every width.

### Why the gutter does not stretch

`LaneGutterWidth` is 256 px on every screen. Widening it on a wider tablet was
considered — frogs have typed names now, so more room for a long one is a real
benefit — and rejected, because it would make **how much of a name fits depend
on the device**. A name that shows in full on the tablet and truncates on a
narrower screen is a bug that only appears on one person's hardware, which is
the worst kind to be told about.

If 256 px turns out to be too tight for the names Connor types, the fix is to
make it a bigger fixed number, decided once and drawn — not to make it elastic.
That is a separate change and it wants its own issue.

Vertically, the pond band is `1200 − BoardHeaderHeight − BoardControlsHeight` =
896 px, which is `SharedLogHeight` — the logs fill it exactly. Four lanes at
184 px is 736 px, centred with 80 px of water above and below, so at four frogs
the logs stand 80 px proud of the lane stack at each end; at two frogs, 264 px.

Every outline is drawn **inside** the element's own bounds, so `TrackOutline`,
`FrogPieceOutline`, `SettingsButtonOutline` and `BoardBandOutline` cost the
layout nothing and the 1920 px sum above is unaffected by any of them.

`LogRadius` is the log's own corner and `SettingsGlyphSize` the gear's own
glyph. Both happen to equal a constant on
[shared components](shared-components.md) today — `ButtonRadius` is also 24 px
and `ButtonLabelSize` also 44 px — and neither is that constant. The Button's
corner and label are the Button's to restyle; the pond's logs and its gear are
not, and they must not move when it does.

## Colours

The board is a pond. **Blue water, brown logs, green lily pads** — Derek's
decision, in his words, on
[issue #291](https://github.com/derekwinters/connor-multiplying-frogs/issues/291).

| Element | Constant | Value |
| --- | --- | --- |
| The water — the pond, and the whole screen behind it | `PondWater` | `#9FD8F2` |
| Lily pad | `LilyPadGreen` | `#B2E67F` |
| Lily pad rim | `LilyPadEdge` | `#6E9E4A` |
| Log | `LogBrown` | `#4A2E1A` |
| `Start` / `End` on a log | `LogLabelInk` | `#C6B49C` |
| Header and controls bands | `BandFill` | `#E2E8E5` |
| Band hairline | `BandEdge` | `#B9C0BD` |
| The board's words | `BoardInk` | `#1E2422` |
| Frog piece outline | `PieceEdge` | black at 35% |

**`LogEdge` is gone.** The log has no rim. On the old tan log the rim did real
work — it was 2.33 : 1 against the water and it was what made a log read as
floating *on* the pond rather than as a hole in it. Against dark chocolate the
log clears the water by 8 : 1 unaided, and a rim darker than the fill measured
1.4 : 1 against it: invisible, and doing nothing that needed doing.

**`LogLabelInk` is new**, and it exists because the log moved. `Start` and `End`
used to be drawn in a mid-brown chosen against a pale tan; on the new log that
is unreadable. They now sit at the **top** of each log rather than its middle,
in a light ink measuring 6.1 : 1 against the fill.

The four frog colours are not here. They are one table for the whole game, on
[shared components](shared-components.md#frog-colours), because a frog is the
same frog on every screen.

`TrackOutline` (3 px), `BoardBandOutline` (3 px) and `FrogPieceOutline` (4 px)
in the tables above are **widths**; the `…Edge` rows here are the colours drawn
at those widths.

### Placeholder, or settled?

**Settled:** the water is blue, the logs are brown, the lily pads are green.
That is a decision, not a proposal, and nothing should quietly walk it back.

**Not settled:** which blue, which brown, which green. The exact hues are a
taste call and they are Connor's — see
[the open questions](#open-questions). The values in the table are the proposal
drawn in the mockups, and they are what the code carries until he has looked at
the board on the tablet. They are honest placeholders in exactly the sense the
[frog colours](shared-components.md#frog-colours) are, and for the same reason.

### The water is the whole screen

`PondWater` is what this screen paints to all four edges of the device, on any
aspect ratio. It is the `pond` band's own fill **and** the paint behind
everything else — the header and controls bands are drawn on top of it, and so
is every lane, log and frog.

Both, and written down as both, because it used to be only the second one: the
`pond` band painted nothing at all and what read as the pond was the screen-wide
paint showing through where the band was not. That is invisible while the two
agree about where the band is, and it is why nobody noticed the band had the
same bug the header and the controls had — see
[the bands reach the edges too](#the-bands-reach-the-edges-too).

That makes the board the one screen that does not paint the app's own
background. Every other screen paints `#EDF1EF`, and so does the scene camera,
which is what shows for the frame before any screen has painted at all. The
rule from
[the canvas every component is measured in](shared-components.md#what-fills-a-screen-that-is-not-1610)
is unchanged and still holds here: nothing behind the canvas is ever visible,
because this screen's own paint reaches the edges.

### The bands reach the edges too

**Invariant:** every one of the three bands is as wide as the screen, `header`'s
top edge is the top of the screen, and `controls`' bottom edge is the bottom of
it — on every aspect ratio, exactly as the water is.

A band is the top or the bottom **of the screen**, not a panel laid on the pond.
A band that stopped at 1920 px on a wider device would show a strip of water past
each of its ends, and two grey bars floating on a pond is a rendering fault
rather than a design. That is what the tablet was doing until this was written
down.

**Invariant:** the `pond`'s row **grows with the band**. The chips and the Start
log sit against the real left safe margin, the End log against the real right
one, and `LanePositionGap` takes up the difference. Position 0 is still on the
Start log and position 8 still on the End log, at every width.

**Invariant:** what `header` and `controls` contain is still laid out in the
reference canvas. The turn banner, the gear and `Roll` are anchored controls,
not a track, and there is nothing about them that a wider screen should stretch.

The second of those is what the first used to say about all three bands — see
[the invariant this page used to carry](#the-invariant-this-page-used-to-carry).
The two are not in tension: a band's *contents* stretch when they are a track
whose whole job is to show distance travelled, and do not when they are a
button.

**A control anchored to a band's edge follows the screen's edge.** The turn
banner and the settings gear sit `SafeMargin` in from the real left and right
edges of the screen, because `SafeMargin` is a margin from the screen and not
from a rectangle nobody can see. This used to say "the turn chip", and the chip
is gone — see
[#326](https://github.com/derekwinters/connor-multiplying-frogs/issues/326);
what is anchored there now is the words themselves. `Roll` is centred and does not care either way.

**Extra height goes to `pond` and nowhere else** — the same answer
[Anchors](#anchors) already gives for a *shorter* screen, in the other
direction. The two bands keep their heights, because a smaller `Roll` is the
wrong thing to trade away, and what a taller screen shows more of is water: the
logs and the lane stack keep their reference size, centred in the band.

At exactly 1920 × 1200 all of this is pixel-identical to the reference mockup —
verified, not assumed: `game-board.html` renders byte-for-byte the same before
and after the pond became elastic. What is *not* pixel-identical at any other
width is now the point, which is why this change comes with
[a second drawing](#mockup) rather than a rule alone.

### The constant this page used to carry

| Was | Is now | Value | Note |
| --- | --- | --- | --- |
| `TurnBannerGap` | *(gone)* | was 24 px | It measured the gap between the banner's chip and its words. There is no chip |

`TurnBannerGap` is **removed, not redefined.** Under the option this page did
not take — a small colour swatch in front of the words — it would have survived
with the same 24 px value and a new meaning, which is the kind of survival that
leaves a constant meaning two things a year apart. Words alone need no gap,
because there is nothing to gap.

`PlayerChipWidth` is untouched: the lane chips still use it, and so do
[roll and card](roll-and-card.md) and the
[working-out grid](working-out-grid.md). Only this screen's header stopped
drawing one.

### The invariant this page used to carry

Until [#325](https://github.com/derekwinters/connor-multiplying-frogs/issues/325)
this section carried one invariant covering all three bands, and it ended with a
sentence that was quoted elsewhere on this page:

> **Invariant:** what a band *contains* is still laid out in the reference
> canvas, centred. The lanes are the reference canvas's safe area wide and the
> two shared logs stand in the columns those lanes' tracks start and end in, on
> every screen, so every constant in the table below keeps meaning exactly what
> it says and position 0 is still on the Start log. **The band grows; the board
> inside it does not.**

That is no longer true of the `pond`, and it is exactly what Derek asked to
change after seeing the built board: the board was a 1920 px picture centred in
a wider band, so the chips floated some 300 px in from the edge of a screen they
were supposed to be pinned to.

It remains true of `header` and `controls`, in the invariant above. The old
wording is kept here rather than deleted, the way
[`working-out-grid.md` keeps the invariants it used to
carry](working-out-grid.md#the-invariants-this-page-used-to-carry): a reader who
remembers the old rule needs to find out that it moved, not to find no trace of
it and assume they misremembered.

What did **not** change with it: every constant in the table still means exactly
what it says, and `LanePositionGap` is the single exception — it is the one
number a screen is now allowed to decide.

### Keeping the frogs visible

A blue pond behind a blue frog, and a green lily pad under a green frog, is the
thing this change could have broken. The frog pieces are drawn **on top of**
these fills, and two of the four are `FrogGreen` and `FrogBlue`.

So the fills are chosen against a bar, and the bar is part of the spec:

> **Every frog colour stays clearly separable from every surface it can sit
> on** — the water, a lily pad, and a log. Separable means a luminance contrast
> of at least **1.9 : 1** *and* a CIE L\*a\*b\* distance (ΔE\*ab) of at least
> **30**.

Two measures, because either alone can be fooled. Contrast catches two colours
of different hue and identical brightness, which is all a colour-blind player —
or anyone holding the tablet in sunlight — has left. ΔE catches two colours a
contrast ratio calls fine and nobody could name apart. The 4 px `PieceEdge`
outline is separation on top of this, never instead of it.

What the table's values measure, as *contrast : 1 / ΔE*:

| | The water | A lily pad | A log |
| --- | --- | --- | --- |
| `FrogGreen` `#3E933E` | 2.49 / 70.1 | 2.66 / 33.9 | 3.21 / 65.6 |
| `FrogBlue` `#37609A` | 4.11 / 49.3 | 4.40 / 100.3 | 1.95 / 57.3 |
| `FrogOrange` `#D38231` | 1.94 / 84.7 | 2.07 / 64.3 | 4.13 / 55.8 |
| `FrogPink` `#D41C78` | 3.20 / 92.3 | 3.43 / 122.1 | 2.50 / 69.5 |

Every pair clears the bar, and this set was **derived against the bar rather
than picked and then checked** — see [how the pond's colours are
constrained](#how-the-ponds-colours-are-constrained) below, which is the part
worth reading before changing any of these six values.

### The rule this page used to carry

Until [#301](https://github.com/derekwinters/connor-multiplying-frogs/issues/301)
this section ended:

> If a future repaint cannot clear the bar, **the surface moves, not the frog**
> — the frog colours are an `area:art` decision that this page does not own.

**Derek reversed that**, in his words: *"we can change frog colors too."* It was
the right call and it was necessary — with the surfaces he picked, no set of
four frog colours existed, so a rule that only ever moved the surface had no
move left to make. The frog colours are still an `area:art` decision and this
page still does not own them; what has changed is that they are now *in scope*
when the pond is repainted, instead of being fixed points the surfaces have to
work around.

### How the pond's colours are constrained

This is the working, kept because the six values above look arbitrary without it
and because the next person to repaint the pond will otherwise rediscover it the
slow way.

Every frog must clear 1.9 : 1 against all three surfaces. That pins them into a
band, and the band is narrow:

- **The water sets a ceiling.** A frog lighter than the water would need a
  relative luminance of 1.24, and pure white is 1.0. So every frog must be
  *darker* than the water, at **L ≤ 0.307**.
- **The log sets a floor.** At `LogBrown`'s L = 0.035, a frog must be
  **L ≥ 0.111**.
- So every frog lives in a band spanning **2.22 : 1**, total, whatever its hue.

The pad then has to sit 1.9 : 1 clear of that *whole* band, which leaves it two
regions and no middle:

| Pad must be | Meaning |
| --- | --- |
| **L ≥ 0.629** | about as light as the water |
| **L ≤ 0.035** | about as dark as the log |

`LilyPadGreen` is L = 0.676, in the light region. A mid-green at L = 0.379 was
drawn and is in neither: three of the four frogs failed against it, `FrogGreen`
on both measures at ΔE 21. A near-black pad at L = 0.033 was also drawn and does
comply, but it puts the pad at the log's exact luminance — the two surfaces then
differ by hue alone, which is the failure mode the two-measure bar exists to
catch, and it renders the pad's veins invisible.

**The threshold is a cliff, not a slope.** `#9CE45C` at L = 0.633 passes;
`#93E04F` at L = 0.601 fails. `LilyPadGreen` is the darkest natural leaf green
available, not a preference.

### The invariant the frog colours still do not satisfy

[Shared components](shared-components.md#frog-colours) requires that *"the four
are distinguishable to a colour-blind player by lightness alone."* Measured, the
four above step **1.28 : 1** between neighbours at worst.

That is better than the placeholders they replace, which stepped **1.11 : 1**
and were four mid-tones — the exact thing that invariant says the set is not.
But 1.28 : 1 is not a comfortable margin, and it is the most the 2.22 : 1 band
allows once four colours have to share it. **The invariant is not currently
satisfied by any set that also clears the separability bar**, and that is worth
knowing rather than discovering later: it is a genuine tension between two rules
this project holds, not an oversight in the values.

One number in that palette is deliberately low and is not about frogs: the log
and the water are almost the same brightness (1.05 : 1) and a long way apart in
hue (ΔE 46.3). What makes a log read as a log floating on water is its
`LogEdge` rim, which is 2.33 : 1 against the water. That is what `TrackOutline`
is for.

### The bands are unchanged, deliberately

`BandFill` and `BandEdge` are the pale grey-green they have always been. They
were chosen to sit against a pale board and they now sit against blue water,
where they are a soft frame rather than a crisp one — ΔE 23 from the water.

They were left alone on purpose rather than overlooked: Derek's words were
about the pond, and repainting the chrome on the way past would have made the
change harder to judge. Whether they now read as a frame or as leftovers is
[the second open question](#open-questions), and it is a look-at-it call.

## Elements

- **Turn banner** — `Green's turn` at `TurnBannerSize`, starting at
  `SafeMargin` from the real left edge of the header band and vertically centred
  in it. **Words alone: no chip, no swatch, no colour** — settled,
  [open question](#open-questions). The
  wording is the frog's **name and nothing else** — `Green's turn` for a frog
  still on its default, `Connor's turn` for one that has been renamed on
  [game setup](game-setup.md).

    The [player chip](shared-components.md#player-chip) that used to sit in
    front of these words is gone. Derek's reason is the whole argument: *"it's
    already selected in the lanes section too"* — the active frog's own lane
    chip, directly below in the pond, carries the same name, the same colour and
    the same Active ring. The header chip was the third statement of a thing the
    screen said twice already.

    It used to read `Green frog's turn`, and the reason given was that frogs
    have no other name. They do now, so the justification went with the word:
    `Connor frog's turn` is not a sentence anybody would write. Rather than
    compose the banner one way for a default name and another way for a typed
    one — a rule with an edge case in it, living in a format string — nothing
    appends anything to a name, ever. The cost is that an un-renamed frog's
    banner loses a word, which is
    [#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310)
    question 4, settled by Derek.
- **Settings button** — top right, `SettingsButtonSize` square, a gear. Opens
  the [settings dialog](settings-dialog.md). Available on any turn, at any
  time, including while it is not your turn — it is the way out of a game and
  hiding it would be worse.
- **`Roll`** — primary [button](shared-components.md#button), oversized. Opens
  [roll and card](roll-and-card.md). Disabled from the moment it is pressed
  until the turn resolves.
- **Lane × 2–4** — one per frog, in turn order, top to bottom (A) or left to
  right (B).
- **Lily pad × 7 per lane** — the spaces, and the only part of the track a lane
  has to itself. The pad a frog is on is drawn no differently from the others;
  the frog on it is the marker.
- **Start log × 1, End log × 1** — one of each for the whole board, however many
  frogs are playing, each filling the pond band top to bottom. They are the ends
  of every lane at once. The Start log is a real position a frog occupies, and a
  wrong answer there leaves the frog where it is; see
  [the Start log is a floor](../reference/index.md#the-start-log-is-a-floor-not-a-special-space).
  Two to four frogs sit on the Start log at the beginning of every game and
  gather on the End log as they finish — that is a shared drawing, not a shared
  position, per the invariant above.
    Each pad is a notched circle with five veins, and its notch's width and
    direction come from
    [the variation table](#the-lily-pad-is-notched-veined-and-varies-per-pad)
    rather than being the same on every pad.

- **Frog piece** — one per lane, on its current position, in the frog's colour.
  On a shared log it sits on its own lane's centre line, which is what keeps
  "frogs never interact" true in the picture as well as in the state.
- **Pad count** — on the chip: `3 of 8`, so progress is readable in words as
  well as by looking.

## Behaviour

- Entering from [game setup](game-setup.md): every frog on its Start log, frog 1
  active, `Roll` enabled.
- `Roll` → [roll and card](roll-and-card.md) →
  [working-out grid](working-out-grid.md) →
  [answer result](answer-result.md) → back here with the frog moved and the turn
  passed to the next frog in order.
- **The move is animated on this screen, after the result dialog closes**, over
  `FrogHopDuration` (0.4 s), one pad's distance. It is the only motion on the
  board, which is what makes it worth watching.
- A frog that reaches the End log stays there and its chip switches to the
  `Home` state. **Play continues** — the other frogs keep taking turns. That is
  Derek's provisional call recorded in
  [the reference material](../reference/index.md#where-v1-fills-a-gap-the-board-leaves-open),
  not something the classroom board says.
- A frog that is home is skipped in turn order.
- **When the last frog gets home, the game ends itself.** The hop finishes, and
  [game over](game-over.md) follows with no input from anybody — see
  [how a game ends](../reference/index.md#where-v1-fills-a-gap-the-board-leaves-open).
  A finished game never sits on this screen waiting to be dismissed.
- Hardware back opens the [settings dialog](settings-dialog.md). It does not
  quit, and it never quits without the confirm.

## Mockup

[`mockups/game-board.html`](mockups/game-board.html) — the reference canvas, and
the one to build from. Beside it,
[`mockups/game-board-wide.html`](mockups/game-board-wide.html) draws the same
board at 2560 × 1200.

**Two files, and no live comparisons.** Both questions this screen was carrying
are answered, and both losing drawings are deleted.

`game-board-paler-water.html` is deleted: Derek picked `#9FD8F2` over the paler
`#B5E3F7` on
[#301](https://github.com/derekwinters/connor-multiplying-frogs/issues/301).
`game-board-banner-swatch.html` is deleted too: he settled the banner question
in the same session — no swatch. Both go the way
`title-screen-resume-primary.html` did, because a mockup nobody can build to is
one that confuses the next reader.

**The wide file is deliberately not at the reference canvas**, which every other
mockup in this project is. It has to be: the whole of
[the elastic pond](#anchors) is what happens at a width that is not 1920, and a
drawing at one width cannot show it. 2560 × 1200 is 2.13 : 1, within a hair of
the roughly 2.17 : 1 the tablet actually reports, and it is the width at which
the derived gap lands on a whole 128 px that can be checked against the
constants table by eye.

**At 1920 the two board drawings are the same picture as before this change** —
byte-for-byte the same rendering, not merely "looks the same". That is the
check that the reference canvas is still a picture of the game, and it is the
reason `game-board.html`'s diff in this change is a comment and two CSS
variables rather than a redraw.

Drawn in the state that exercises every case at once: four frogs, one home on
the End log, one still on its Start log, two mid-lane, Green to roll — and
**exactly two logs on the whole screen**, which is the thing to look at.

Every one of the four frogs is drawn on something it has to be legible
against — Green and Pink on lily pads, Orange on the Start log, Blue home on
the End log — so [the contrast question](#keeping-the-frogs-visible) is
visible in the picture rather than only asserted in a table.

It draws the three questions #289 left open as Derek answered them on #296: the
logs fill the pond band top to bottom rather than stopping at the lanes in play,
each frog sits on its own lane's centre line, and the corner and outline are the
`LogRadius` and `TrackOutline` they always were. Only the first of the three
moved — the mockup originally drew a 736 px log spanning the four lanes, and was
redrawn to 896 px when the answer arrived.

The rejected lanes-up variant is not committed. It was drawn, compared, and
decided against; keeping a mockup of a layout nobody is building is how a
`mockups/` folder stops being trustworthy.

## Open questions

- **Which blue is the water? Settled: `#9FD8F2`.** Derek's call on
  [#301](https://github.com/derekwinters/connor-multiplying-frogs/issues/301),
  over the paler `#B5E3F7`. The losing mockup is deleted. `LogBrown` and
  `LilyPadGreen` were settled in the same session and are no longer proposals
  either — but note that neither was a free choice: see
  [how the pond's colours are constrained](#how-the-ponds-colours-are-constrained).
- **Should the turn banner show the frog's colour at all? Settled: no.** Derek's
  call — the banner is words alone, and `game-board-banner-swatch.html` is
  deleted. The reasoning that was proposed for it holds: the lane chip below
  already carries that colour in the same Active state on the same screen; a
  bare swatch outside a chip would have been a new element needing a constant of
  its own for a lone circle belonging to nothing; and the player chip's
  accessibility invariant — *"a word, always, never colour alone"* — forbids
  colour without a word and permits the inverse.

    The cost accepted with it, stated when it was proposed and still true: the
  header is the only place on the board that names the active player without
  showing their colour.
- **Does the gap look right when it is wider than the lily pad?** At the
  reference canvas `LanePositionGap` is 48 px against a 112 px
  `LilyPadDiameter`, and the pads plainly read as a track. On the wide drawing
  the gap is **128 px — wider than the pad itself**, and seven pads further
  apart than they are big could read as scattered stepping stones rather than as
  something to hop along. [The wide mockup](mockups/game-board-wide.html) exists
  to answer this, and it is a look-at-it call on the tablet, not an argument.

    If it does read wrong, the fix is a **cap** — a `LanePositionGapMax` beside
    the existing floor, with the leftover width going to water at the pond's
    right-hand end rather than into the gaps — and *not* a return to the centred
    board, which is the thing being fixed. No cap is proposed here, because
    proposing a number for a problem nobody has seen yet is how a constants
    table fills up with values nobody can justify.

    The neighbouring option, growing the pads to fill the space, is a different
    and much bigger change: `LilyPadDiameter` is 112 px because
    [that is what decided lanes-across over lanes-up](#why-the-lanes-run-across),
    and moving it reopens that decision. It wants its own issue if it is wanted.
- **Do the header and controls bands change?** They are `BandFill`, the pale
  grey-green chosen to sit against a pale board, and this change
  [deliberately left them alone](#the-bands-are-unchanged-deliberately). Against
  blue water they either read as a clean frame or as leftovers from the old
  look, and that is a thing to see rather than to argue about. If they should
  change, this is where it gets decided.
- **Do the unused lanes show?** The classroom board always has eight lanes,
  whoever is playing. The mockups draw only the lanes in play, because empty
  lanes on a screen this size cost the ones in play their height. Presentation,
  so ours — but say if it looks wrong.
- **Does the board show what the last player rolled?** Currently no: the die
  appears in [roll and card](roll-and-card.md) and is gone. A persistent
  "last roll" readout is an element and would need adding here.
The three questions #289 left open about the shared logs are **settled**, by
Derek on
[issue #296](https://github.com/derekwinters/connor-multiplying-frogs/issues/296),
and are recorded above rather than here: the log spans the full pond whatever
the player count, a frog on a log sits on its own lane's centre line, and the
log keeps `LogRadius` and `TrackOutline` as they are.
