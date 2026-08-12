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
by a highlight.
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

Across the pond, the nine positions occupy the same nine columns in every lane,
and their total width is fixed by the position count rather than by the space
available:

- `chip` — pinned to the **left** safe margin, `LaneGutterWidth` wide.
- `start-log` — `LaneGutterGap` to the right of the chips.
- `track` — this lane's seven lily pads, `LanePositionGap` from the Start log,
  from each other, and from the End log.
- `end-log` — pinned to the **right** edge of the safe area.

Both logs are `SharedLogHeight` tall, which is the height of the `pond` band
itself: they fill it, edge to edge with the two hairlines, whether two frogs are
playing or four. Every lane's centre line therefore crosses both of them, and a
frog on a log still sits on its own lane's line. A shorter screen loses height
from `pond` and nothing else — and the logs lose it with the band, because they
are the band's height rather than a number typed in beside it. The header and
the controls do not shrink, because a smaller `Roll` button is the wrong thing
to trade away.

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
| Gap between the banner's chip and its words | `TurnBannerGap` | 24 px |
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
| Frog piece diameter | `FrogPieceDiameter` | 88 px |
| Frog piece outline | `FrogPieceOutline` | 4 px |
| Lily pad and log outline | `TrackOutline` | 3 px |
| Gap between positions | `LanePositionGap` | 48 px |
| Chip gutter width | `LaneGutterWidth` | 256 px |
| Gap between chip and track | `LaneGutterGap` | 48 px |

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

That horizontal arithmetic is exact and worth keeping exact, and sharing the
logs does not disturb it, because a shared log stands in the same column its
per-lane predecessor did: two logs at 176, seven pads at 112 and eight gaps at
48 is 1520 px across the pond; plus a 256 px chip gutter, a 48 px gap and two
48 px margins, that is 1920 px on the nose. If a constant here changes, one of
the others has to move with it.

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
| Lily pad | `LilyPadGreen` | `#CCEAAF` |
| Lily pad rim | `LilyPadEdge` | `#7FAE5E` |
| Log | `LogBrown` | `#E2C79C` |
| Log rim | `LogEdge` | `#A97F4F` |
| Header and controls bands | `BandFill` | `#E2E8E5` |
| Band hairline | `BandEdge` | `#B9C0BD` |
| The board's words | `BoardInk` | `#1E2422` |
| Frog piece outline | `PieceEdge` | black at 35% |

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

`PondWater` is not the pond band's fill. It is what this screen paints to all
four edges of the device, on any aspect ratio — the header and controls bands
are drawn on top of it, and so is everything else.

That makes the board the one screen that does not paint the app's own
background. Every other screen paints `#EDF1EF`, and so does the scene camera,
which is what shows for the frame before any screen has painted at all. The
rule from
[the canvas every component is measured in](shared-components.md#what-fills-a-screen-that-is-not-1610)
is unchanged and still holds here: nothing behind the canvas is ever visible,
because this screen's own paint reaches the edges.

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
| `FrogGreen` | 2.61 / 60.4 | 3.07 / **40.8** | 2.48 / 50.7 |
| `FrogBlue` | 3.47 / 46.8 | 4.08 / 83.0 | 3.29 / 75.5 |
| `FrogOrange` | 2.13 / 87.9 | 2.51 / 65.8 | **2.02** / 46.0 |
| `FrogPink` | 2.91 / 73.8 | 3.42 / 89.5 | 2.76 / 67.6 |

Every pair clears the bar. The two tightest are the two the change was always
going to squeeze: the green frog on a green lily pad (ΔE 40.8, on a bar of 30)
and the orange frog on a brown log (contrast 2.02 : 1, on a bar of 1.9). If a
future repaint cannot clear the bar, **the surface moves, not the frog** — the
frog colours are an `area:art` decision that this page does not own.

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

- **Turn banner** — `Green frog's turn`, left of the header, with that frog's
  [player chip](shared-components.md#player-chip) in its active state. The
  wording is the frog's colour name, because frogs have no other name.
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

[`mockups/game-board.html`](mockups/game-board.html) — and, until Connor has
picked the water, [`mockups/game-board-paler-water.html`](mockups/game-board-paler-water.html)
beside it.

The two files differ in **one value**: `PondWater`. `game-board.html` draws the
proposal in the table above, `#9FD8F2`; the second draws `#B5E3F7`, the same
blue with more light in it. Everything else on the two canvases is identical to
the pixel. Open both on the tablet, one after the other, and say which is the
pond — comparing two pictures is a much easier conversation than critiquing
one, and the losing file is deleted when the answer arrives, the way
`title-screen-resume-primary.html` was.

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

- **Which blue is the water?** Blue is settled; this blue is not. Two are drawn
  — `#9FD8F2` in `game-board.html` and the paler `#B5E3F7` beside it — and
  Connor picks, on the tablet, at 1:1. Both clear
  [the separability bar](#keeping-the-frogs-visible), so either can be taken as
  it stands; a third blue of his own choosing has to be measured against that
  bar before it lands. The same is true of `LogBrown` and `LilyPadGreen`, which
  are drawn once each rather than twice — say if either is wrong and it gets
  the same treatment.
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
