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
| `pond` | The lanes — one per frog in the game |
| `controls` | The `Roll` button |

A lane, in turn, is:

| Part | Job |
| --- | --- |
| `chip` | Which frog this lane belongs to, and how far it has got |
| `track` | Start log, seven lily pads, End log |
| `piece` | The frog itself, sitting on its current position |

## Anchors

The three regions are horizontal bands that together fill the full 1200 px
height, in this order and with no gaps:

- `header` — pinned to the top, `BoardHeaderHeight` tall, full width.
- `pond` — everything between the other two bands. Lanes are stacked and
  **vertically centred** within it, so a two-frog game is centred rather than
  clinging to the top.
- `controls` — pinned to the bottom, `BoardControlsHeight` tall, full width.

Inside a lane, the `track` is pinned to the **right** edge of the safe area and
the `chip` to the left, with the track's width fixed by its nine positions
rather than by the space available. A shorter screen loses height from `pond`
and nothing else; the header and the controls do not shrink, because a smaller
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
| Positions in a lane | `LanePositionCount` | 9 |
| Correct answers needed to win | `LaneWinningPosition` | 8 |

`LanePositionCount` is 9 because a lane is the Start log, seven lily pads, and
the End log, and **the End log is the winning space** — a frog has to land on
it. Confirmed by Derek in
[issue #185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185);
recorded in
[the reference material](../reference/index.md#the-end-log-is-the-winning-space).
That is what the `of 8` in every chip's pad count refers to.

The lane:

| Element | Constant | Value |
| --- | --- | --- |
| Lane band height | `LaneHeight` | 184 px |
| Lily pad diameter | `LilyPadDiameter` | 112 px |
| Frog piece diameter | `FrogPieceDiameter` | 88 px |
| Frog piece outline | `FrogPieceOutline` | 4 px |
| Log width | `LogWidth` | 176 px |
| Log height | `LogHeight` | 120 px |
| Log corner radius | `LogRadius` | 24 px |
| Lily pad and log outline | `TrackOutline` | 3 px |
| Gap between positions | `LanePositionGap` | 48 px |
| Chip gutter width | `LaneGutterWidth` | 256 px |
| Gap between chip and track | `LaneGutterGap` | 48 px |

That arithmetic is exact and worth keeping exact: two logs at 176, seven pads at
112 and eight gaps at 48 is 1520 px of track; plus a 256 px chip gutter, a 48 px
gap and two 48 px margins, that is 1920 px on the nose. If a constant here
changes, one of the others has to move with it.

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
- **Lily pad × 7** — the spaces. The pad a frog is on is drawn no differently
  from the others; the frog on it is the marker.
- **Start log / End log** — the ends of the lane. The Start log is a real
  position a frog occupies, and a wrong answer there leaves the frog where it
  is; see
  [the Start log is a floor](../reference/index.md#the-start-log-is-a-floor-not-a-special-space).
- **Frog piece** — one per lane, on its current position, in the frog's colour.
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
the End log, one still on its Start log, two mid-lane, Green to roll.

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
