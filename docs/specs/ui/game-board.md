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
| `start-log` | **One** log down the left of the pond, spanning every lane in play. It is position 0 of all of them |
| `lane` × 2–4 | One per frog in the game, stacked |
| `end-log` | **One** log down the right of the pond, spanning every lane in play. It is position 8 of all of them |

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

Both logs are `SharedLogHeight` tall and vertically centred on the stack of
lanes, so every lane's centre line crosses both of them and a frog on a log
still sits on its own lane's line. A shorter screen loses height from `pond` and
nothing else; the header and the controls do not shrink, because a smaller
`Roll` button is the wrong thing to trade away.

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
It is here because the shared logs are the first element whose size depends on
it.

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
| Log height | `SharedLogHeight` | `LaneCount × LaneHeight` |
| Log corner radius | `LogRadius` | 24 px |

`LogHeight` (120 px) is **gone**, not renamed to something with the same
meaning. It was the height of a log sized to sit inside one 184 px lane, and
there is no such thing on this board any more. `SharedLogHeight` is derived
because a log that spans the lanes in play cannot have one value:

| `LaneCount` | `SharedLogHeight` |
| --- | --- |
| 2 | 368 px |
| 3 | 552 px |
| 4 | 736 px |

There is no lane gap term in that expression because there is no lane gap:
lanes stack flush, `LaneHeight` to `LaneHeight`. If a gap between lanes is ever
introduced, `SharedLogHeight` gains a `(LaneCount − 1) × LaneGap` term with it.

**Which lanes the log spans is [open question 1](#open-questions)** — this
expression is the proposal drawn in the mockup, not a settled number.

That horizontal arithmetic is exact and worth keeping exact, and sharing the
logs does not disturb it, because a shared log stands in the same column its
per-lane predecessor did: two logs at 176, seven pads at 112 and eight gaps at
48 is 1520 px across the pond; plus a 256 px chip gutter, a 48 px gap and two
48 px margins, that is 1920 px on the nose. If a constant here changes, one of
the others has to move with it.

Vertically, the pond band is `1200 − BoardHeaderHeight − BoardControlsHeight` =
896 px. Four lanes at 184 px is 736 px, centred with 80 px of water above and
below, so the tallest `SharedLogHeight` the expression can produce fits the
pond with 160 px to spare.

Every outline is drawn **inside** the element's own bounds, so `TrackOutline`,
`FrogPieceOutline`, `SettingsButtonOutline` and `BoardBandOutline` cost the
layout nothing and the 1920 px sum above is unaffected by any of them.

`LogRadius` is the log's own corner and `SettingsGlyphSize` the gear's own
glyph. Both happen to equal a constant on
[shared components](shared-components.md) today — `ButtonRadius` is also 24 px
and `ButtonLabelSize` also 44 px — and neither is that constant. The Button's
corner and label are the Button's to restyle; the pond's logs and its gear are
not, and they must not move when it does.

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
  frogs are playing, each spanning every lane in play. They are the ends of
  every lane at once. The Start log is a real position a frog occupies, and a
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

[`mockups/game-board.html`](mockups/game-board.html)

Drawn in the state that exercises every case at once: four frogs, one home on
the End log, one still on its Start log, two mid-lane, Green to roll — and
**exactly two logs on the whole screen**, which is the thing to look at.

It draws the proposal on all three open questions below: the logs span the four
lanes in play and no further, each frog sits on its own lane's centre line, and
the corner and outline are the `LogRadius` and `TrackOutline` they always were.
Those are drawn so there is something to react to, not because they are
decided.

The rejected lanes-up variant is not committed. It was drawn, compared, and
decided against; keeping a mockup of a layout nobody is building is how a
`mockups/` folder stops being trustworthy.

## Open questions

- **Do the unused lanes show?** The classroom board always has eight lanes,
  whoever is playing. The mockups draw only the lanes in play, because empty
  lanes on a screen this size cost the ones in play their height. Presentation,
  so ours — but say if it looks wrong.
- **Does the board show what the last player rolled?** Currently no: the die
  appears in [roll and card](roll-and-card.md) and is gone. A persistent
  "last roll" readout is an element and would need adding here.
- **Does a shared log span only the lanes in play, or the full pond?** The
  mockup draws the first — `SharedLogHeight` is `LaneCount × LaneHeight`, so a
  two-frog game gets a 368 px log with open water above and below it. The
  alternative is a log of a fixed 896 px, the full height of the pond, which
  puts a lot of wood beside empty water in a two-frog game but never changes
  size. Both are drawable and neither is wrong; which reads better on the tablet
  is a look-at-it call. **This one decides the `SharedLogHeight` expression** —
  the full-pond answer replaces it with a constant 896 px.
- **Where does a frog sit on a shared log?** The mockup keeps each piece on its
  own lane's centre line, so the log is shared but the positions visibly are
  not. The alternative — clustering the pieces together on the log, the way real
  frogs would sit on a real log — looks better and reads worse, because it is
  the one drawing on this screen that could suggest frogs interact. Worth
  drawing if Connor wants it.
- **Does the log keep its corner radius and its outline?** `LogRadius` (24 px)
  and `TrackOutline` (3 px) were chosen for a 176 × 120 log. The mockup keeps
  both on a log up to six times taller, where the same corner is a much smaller
  fraction of the shape. It may look right; it may want a bigger radius.
