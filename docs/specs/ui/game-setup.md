# Game setup

Pick which frogs are playing. Tap a frog to put it in the game, tap it again to
take it out, then start.

## Invariants

**Invariant:** a game cannot start with fewer than two frogs or more than four.
The cap of four is a recorded rule change from the classroom game's 2–8 — see
[future ideas](../future-ideas.md).
**Invariant:** no keyboard ever appears on this screen. See
[why there is no typing](shared-components.md#why-there-is-no-typing).
**Invariant:** turn order is the order the frogs are listed in, left to right,
and that order is visible before the game starts. A player should not have to
discover whose turn is next by watching.
**Invariant:** every frog seat is a `MinTouchTarget`-safe target with a very
large margin — these are the biggest tap targets in the game, because they are
tapped by four children at once, reaching across each other.

## Regions

| Region | Job |
| --- | --- |
| `header` | The question: *Who is playing?* |
| `seats` | The four frog seats, in a row |
| `hint` | One line saying what to do, and what is stopping you if `Start` is off |
| `controls` | `Back` and `Start` |

## Anchors

- `header` pinned to the top safe area, centred.
- `seats` centred both ways in the space between `header` and `controls`. Four
  seats, always four, always in the same left-to-right order — an unchosen frog
  is an empty seat, not a missing one, so the row never reflows while you are
  tapping it.
- `hint` sits `HintGap` beneath `seats`, centred.
- `controls` pinned to the bottom safe area: `Back` at the left, `Start` at the
  right, both `SafeMargin` from their edge.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin | `SafeMargin` | 48 px |
| Header size | `SetupHeaderSize` | 72 px |
| Seat width | `SeatWidth` | 360 px |
| Seat height | `SeatHeight` | 440 px |
| Gap between seats | `SeatGap` | 48 px |
| Seat corner radius | `SeatRadius` | 32 px |
| Frog swatch in a seat | `SeatSwatchDiameter` | 200 px |
| Seat colour-name size | `SeatLabelSize` | 48 px |
| Ring on a chosen seat | `SeatChosenRing` | 8 px |
| Gap between the seat row and the hint | `HintGap` | 56 px |
| Hint size | `SetupHintSize` | 36 px |
| Turn-order badge diameter | `SeatOrderBadge` | 72 px |

Four seats at 360 px with three 48 px gaps is 1584 px, centred in 1920 with
168 px either side. The row is deliberately not full-bleed: the empty space is
what makes it obvious there are four seats and no more.

## Elements

- **Frog seat ×4** — Green, Blue, Orange, Pink, in that order, using
  [frog colours](shared-components.md#frog-colours). Two states, and only two:

    | State | What it looks like | What tapping does |
    | --- | --- | --- |
    | Empty | Dashed outline, grey swatch, `Tap to play` | Adds this frog; it takes the next free turn-order number |
    | Chosen | Filled, `SeatChosenRing` ring, colour name, turn-order badge | Removes this frog; the badges after it renumber |

- **Turn-order badge** — `1`–`4` on a chosen seat, top-left corner. This is the
  only thing on the screen that says what turn order is, and it is why the
  numbers renumber immediately when a frog is removed rather than at start.
- **`Start`** — primary [button](shared-components.md#button). Disabled below
  two frogs.
- **`Back`** — secondary button. Returns to the [title screen](title-screen.md).
- **Hint** — `Pick two to four frogs` while `Start` is disabled;
  `Green goes first` once it is enabled. One line, always present, so the
  layout does not jump when it changes.

## Behaviour

- Entering: seats all empty, every time. The game does not remember the last
  line-up, because it does not remember anything between sessions in v1 and
  because the players at the table are usually different ones.
- Tapping a chosen seat removes it. There is no confirm — nothing has started
  yet, and a confirm on an action with no cost teaches children to dismiss
  confirms.
- `Start` begins the game with the chosen frogs in badge order and goes to
  [game board](game-board.md), with frog 1's turn active and nothing rolled.
- Hardware back does what `Back` does.

## Mockup

[`mockups/game-setup.html`](mockups/game-setup.html) — drawn with three frogs
chosen and one seat empty, which is the state that shows both seat appearances
and the renumbering at once.

## Open questions

- **Does a saved game change this screen?**
  [ADR-0004](../../adr/0004-core-owns-the-save-format.md) puts a save format in
  Core. If a game in progress can be resumed, the resume entry point is most
  likely here or on the [title screen](title-screen.md), and either way it is a
  new element that needs adding to a wireframe. Not decided; nothing on this
  screen assumes it.
- **Should the four colours be reorderable?** Proposed: no. Turn order is
  tap order, which is one gesture rather than two, and re-ordering is a drag
  interaction on a screen four children are all reaching at.
