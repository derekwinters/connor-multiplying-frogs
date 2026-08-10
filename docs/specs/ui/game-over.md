# Game over

Who won, and where everybody else got to.

## Invariants

**Invariant:** every frog that played is listed, in finishing order, with how
far it got. Nobody disappears off the bottom of the game they just played.
**Invariant:** there is no score. The classroom game has no score — it has an
order — and inventing one would be inventing a mechanic.
**Invariant:** a frog is home when it **lands on the End log** — eight correct
answers — not when it reaches the last lily pad. See
[the End log is the winning space](../reference/index.md#the-end-log-is-the-winning-space).
**Invariant:** the winner is the frog that reached the End log first, and
finishing order is the order frogs got home. Frogs that did not finish are
ranked by how many lily pads they made.
**Invariant:** nothing on this screen is destructive, and nothing on it can
resume the game that just ended.

## Regions

| Region | Job |
| --- | --- |
| `headline` | `Blue frog wins!` |
| `standings` | One row per frog, in finishing order |
| `controls` | `Back to the title` and `Play again` |

## Anchors

A full screen, not a dialog — the game is over, so there is nothing behind it
worth dimming.

- `headline` centred, pinned below the top safe area.
- `standings` a centred column of `StandingsRowWidth`, one row per frog, with
  `StandingsRowGap` between rows. Four rows is the maximum, so the column never
  scrolls.
- `controls` on the bottom safe-area line: `Back to the title` left,
  `Play again` right.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Headline size | `GameOverHeadlineSize` | 88 px |
| Headline top offset | `GameOverHeadlineTop` | 64 px |
| Standings column width | `StandingsRowWidth` | 1200 px |
| Standings column top offset | `StandingsColumnTop` | 250 px |
| Row height | `StandingsRowHeight` | 140 px |
| Row inner horizontal padding | `StandingsRowPadding` | 40 px |
| Gap between a row's place/swatch/name/progress | `StandingsRowInnerGap` | 32 px |
| Gap between rows | `StandingsRowGap` | 24 px |
| Row corner radius | `StandingsRowRadius` | 24 px |
| Ordinary row border | `StandingsRowBorder` | 3 px |
| Winner row border | `StandingsWinnerBorder` | 6 px |
| Place number column width | `StandingsPlaceWidth` | 80 px |
| Place number size | `StandingsPlaceSize` | 56 px |
| Frog swatch in a row | `StandingsSwatchDiameter` | 88 px |
| Colour name size | `StandingsNameSize` | 52 px |
| Progress readout size | `StandingsProgressSize` | 44 px |
| Rows' entry-reveal duration (total) | `StandingsRevealDuration` | 0.4 s |

The `controls` row sits on the bottom safe-area line, `SafeMargin` (48 px) in
from each edge — the same constant [title screen](title-screen.md) and
[game setup](game-setup.md) already name, not a second margin of its own. The
progress readout's denominator is `LaneWinningPosition` (8) from
[game board](game-board.md); `LanePositionCount` (9) counts the Start log too
and is not the number this readout means.

## Elements

- **Headline** — `<Colour> frog wins!` when a frog reached the End log. If the
  game was ended before anybody got home, it reads `Game over` instead, because
  announcing a winner who did not win is worse than announcing nobody.
- **Standings row × 2–4** — place number, frog swatch, colour name, and how far
  it got: `Home — 8 of 8` for a finisher, `6 of 8` for everyone else. The winner
  row is drawn heavier; every row is otherwise identical, because second place
  and last place are the same kind of fact.
- **`Play again`** — primary [button](shared-components.md#button). Starts a new
  game with **the same frogs, in the same turn order**, straight to the
  [game board](game-board.md) with everyone back on their Start log.
- **`Back to the title`** — secondary button. Returns to the
  [title screen](title-screen.md).

`Play again` skipping [game setup](game-setup.md) is the one place the game
remembers anything. It is worth it: the overwhelmingly common case is the same
four children going again, and making them re-tap their colours every time is a
tax on the thing they most want to do. Changing who is playing is one extra tap
through the title screen.

## Behaviour

- Reached two ways, and only two:

    | Route | What the headline says |
    | --- | --- |
    | The last frog reaches its End log, and the game ends itself | `<Colour> frog wins!` |
    | `End the game` confirmed from [end-game confirm](end-game-confirm.md) | `<Colour> frog wins!` if anyone got home, otherwise `Game over` |

    The first route needs no input from anybody. When the last frog lands on its
    End log, the hop finishes, and this screen follows — see
    [how a game ends](../reference/index.md#where-v1-fills-a-gap-the-board-leaves-open).
- Entering: rows appear in place, top to bottom, over `StandingsRevealDuration`
  (0.4 s total). No suspense reveal — everyone watched it happen.
- Hardware back does what `Back to the title` does.
- The finished game is not recoverable from here, and nothing warns about that,
  because a game that has been ended deliberately is not an accident to guard
  against twice.

## Mockup

[`mockups/game-over.html`](mockups/game-over.html) — four frogs, one home, drawn
in the state where the game was ended before the others finished, which is the
one that has to look fair.

## Open questions

- **Is finishing order the right ranking for frogs that did not finish?**
  Ranking by lily pads is the only fact available, and ties are possible. Two
  frogs on the same pad currently share a place number. Say if they should be
  ordered by turn order instead.
