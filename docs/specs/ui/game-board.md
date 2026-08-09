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

## The two variants

There is a real choice here and no way to argue it in words, so there are two
mockups.

| | **A — lanes across** | **B — lanes up** |
| --- | --- | --- |
| Lane runs | Left to right | Bottom to top |
| Lily pad diameter | **112 px** | **64 px** |
| Frog piece | 88 px | 52 px |
| Looks like the cardboard board | No — it is rotated | Yes |
| Reads as progress | Yes — left to right, like a race | Less so on a wide screen |
| Space used | Fills a 16:10 screen | Four tall columns with wide margins |

**A is the proposal.** The reason is the pad size in that table: turning a
portrait board sideways onto a landscape screen buys a lily pad nearly twice as
wide, and the lily pad is the thing a child looks at to see how they are doing.
B keeps the classroom board's shape, and the cost of that faithfulness is
paid in the size of every pad on screen.

Under [ADR-0001](../../adr/0001-rules-sacred-presentation-ours.md) this is
presentation, so it is ours to choose — but it is exactly the kind of choice
Connor should be shown two pictures of.

## Named constants

Shared by both variants:

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin | `SafeMargin` | 48 px |
| Header band height | `BoardHeaderHeight` | 128 px |
| Controls band height | `BoardControlsHeight` | 176 px |
| Turn banner text size | `TurnBannerSize` | 52 px |
| Settings button, square | `SettingsButtonSize` | 96 px |
| `Roll` button width | `RollButtonWidth` | 480 px |
| `Roll` button height | `RollButtonHeight` | 144 px |
| `Roll` label size | `RollButtonLabelSize` | 56 px |
| Positions in a lane | `LanePositionCount` | 9 |

Variant A — lanes across:

| Element | Constant | Value |
| --- | --- | --- |
| Lane band height | `LaneHeight` | 184 px |
| Lily pad diameter | `LilyPadDiameter` | 112 px |
| Frog piece diameter | `FrogPieceDiameter` | 88 px |
| Log width | `LogWidth` | 176 px |
| Log height | `LogHeight` | 120 px |
| Gap between positions | `LanePositionGap` | 48 px |
| Chip gutter width | `LaneGutterWidth` | 256 px |
| Gap between chip and track | `LaneGutterGap` | 48 px |

That arithmetic is exact and worth keeping exact: two logs at 176, seven pads at
112 and eight gaps at 48 is 1520 px of track; plus a 256 px chip gutter, a 48 px
gap and two 48 px margins, that is 1920 px on the nose. If a constant here
changes, one of the others has to move with it.

Variant B — lanes up:

| Element | Constant | Value |
| --- | --- | --- |
| Lane column width | `LaneColumnWidth` | 432 px |
| Gap between columns | `LaneColumnGap` | 32 px |
| Lily pad diameter | `LilyPadDiameterUp` | 64 px |
| Frog piece diameter | `FrogPieceDiameterUp` | 52 px |
| Log height | `LogHeightUp` | 88 px |
| Gap between positions | `LanePositionGapUp` | 20 px |
| Gap between chip and track | `LaneChipGapUp` | 16 px |

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
- Hardware back opens the [settings dialog](settings-dialog.md). It does not
  quit, and it never quits without the confirm.

## Mockup

- **A — lanes across:** [`mockups/game-board-lanes-across.html`](mockups/game-board-lanes-across.html)
- **B — lanes up:** [`mockups/game-board-lanes-up.html`](mockups/game-board-lanes-up.html)

Both are drawn with the same game state — four frogs, one home, one on the Start
log, one mid-lane, Green to roll — so the only difference between the two
pictures is the thing being decided.

## Open questions

- **Nine positions or eight?** The mockups draw the End log as a space a frog
  lands on, making a lane nine positions and a win eight correct answers.
  Whether that is what the classroom game does is
  [issue #185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185),
  and it is a rule, so Connor answers it.
- **Do the unused lanes show?** The classroom board always has eight lanes,
  whoever is playing. The mockups draw only the lanes in play, because empty
  lanes on a screen this size cost the ones in play their height. Presentation,
  so ours — but say if it looks wrong.
- **Does the board show what the last player rolled?** Currently no: the die
  appears in [roll and card](roll-and-card.md) and is gone. A persistent
  "last roll" readout is an element and would need adding here.
