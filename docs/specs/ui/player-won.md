# A player has won

The moment a frog gets home. One dialog, immediately after the winning hop
finishes, saying that this frog has arrived — and then handing on.

Before [#328](https://github.com/derekwinters/connor-multiplying-frogs/issues/328)
there was nothing here at all. A child who won got a dialog telling them whose
turn it was next, and then watched everybody else finish.

## Invariants

**Invariant:** getting home is marked. Every frog that reaches the End log gets
this dialog — the first one and the last one alike. It is the one screen in the
game that is about a single child's moment rather than about the state of the
game.
**Invariant:** only the **first** frog home "wins". Under the current rules the
first frog to reach the End log is the winner and the rest are ranked behind it,
so the wording changes after the first — see
[Elements](#elements). Saying "wins" four times in one game would be four lies
after the first.
**Invariant:** this dialog changes nothing about the game. It does not end it,
does not skip anyone, does not reorder anything. It is an announcement, and the
rule it announces is
[game board](game-board.md)'s: a frog that reaches the End log stays there and
**play continues**.
**Invariant:** it carries no standings. [Game over](game-over.md) lists every
frog in finishing order; a second table in different words on the screen before
it would be the same information twice.
**Invariant:** the hardware back button does **nothing** here. See
[Behaviour](#behaviour).

## Regions

| Region | Job |
| --- | --- |
| `frog` | The frog that just got home, drawn large |
| `headline` | `Blue wins!`, or `Pink is home!` |
| `controls` | The one button, which hands on |

## Anchors

A centred [dialog](shared-components.md#dialog), `WonDialogWidth` by
`WonDialogHeight`, over a dimmed copy of the board.

Measured from the panel's top down, because the frog is the thing this screen is
about and everything else follows it:

- `frog` — horizontally centred, `DialogPadding` from the panel's top edge.
- `headline` — `WonHeadlineGap` below the frog, horizontally centred, one line.
- `controls` — bottom-right at `DialogPadding`, where
  [the shared dialog](shared-components.md#dialog) puts a button row.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `WonDialogWidth` | 900 px |
| Dialog height | `WonDialogHeight` | 640 px |
| The frog, drawn large | `WonFrogDiameter` | 220 px |
| Frog outline | `WonFrogOutline` | 8 px |
| Gap between the frog and the headline | `WonHeadlineGap` | 40 px |
| Headline size | `WonHeadlineSize` | 76 px |
| Headline line box | `WonHeadlineLineBox` | 92 px |

The arithmetic down the panel, against a 634 px padding box:

```text
DialogPadding 56 + WonFrogDiameter 220        -> frog     56 .. 276
WonHeadlineGap 40 + WonHeadlineLineBox 92     -> headline 316 .. 408
ButtonHeight 112, DialogPadding 56 up         -> button   466 .. 578
```

**`WonFrogDiameter` is deliberately not the board's `FrogPieceDiameter`**
(88 px). This is the one screen in the game that draws a frog big, and that is
what it is for: the board says a frog moved, and this says a frog arrived. A
constant that happened to equal the board's would be re-used by someone later
and drag the two into step.

`WonHeadlineLineBox` is written down rather than left to a font's default for
the same reason the settings dialog's title box is: `WonHeadlineGap` is measured
from it, and a line box that varies by renderer is a gap that varies by
renderer.

## Elements

- **`frog`** — the frog that just got home, at `WonFrogDiameter` in its own
  colour, with the `PieceEdge` outline at `WonFrogOutline`. Not a
  [player chip](shared-components.md#player-chip): a chip carries a name and a
  pad count, and both are wrong here — the name is in the headline and the pad
  count is now `8 of 8` for everyone this dialog appears for.
- **`headline`** — at `WonHeadlineSize`, and its wording is the one thing on
  this page that depends on game state:

    | When | Reads |
    | --- | --- |
    | The **first** frog home | `Blue wins!` |
    | Any **later** frog home | `Pink is home!` |

    The frog's name is whatever it is called — its colour by default, or the
    name typed on [game setup](game-setup.md), so `Connor wins!` for a renamed
    frog. Same rule as the board's turn banner: the name and nothing else.

- **`controls`** — one primary [button](shared-components.md#button), named for
  what happens next, exactly as
  [answer result](answer-result.md)'s is:

    | When | Reads | Hands to |
    | --- | --- | --- |
    | There is a next player | `Green's turn` | the next player's turn on the board |
    | That frog was the **last** one home | `See the results` | [game over](game-over.md) |

    `Green` is the next player's **name**, under the same rule the headline
    uses — `Connor's turn` for a renamed frog. The two lines of this dialog
    name two different players, and a screen that called one of them by its
    colour and the other by its name would be naming the same kind of thing two
    ways.

    The second case is the one `Game.IsOver` has just become true for.

## Behaviour

- **Entry.** Opens once the winning hop has finished — the seam
  `AppRoot.HandOffFinished` already sits on, between the hop completing and the
  next player's turn starting. It opens *after* [answer result](answer-result.md)
  has closed and the frog has landed, not instead of it: the answer result
  reports the answer, this reports the arrival.
- **What decides it is `Game.FrogJustHome`**, not `Game.IsOver`. Core answers
  "which frog did the turn that just played land on its End log, if any" — one
  turn's fact, replaced by the next turn's result rather than added to, which
  is what makes one arrival announceable exactly once and never twice. It is
  deliberately a different question from whether the game is over: the two
  agree only about the very last arrival, and using the ending to decide would
  be the bug this dialog exists to fix.
- **Exit.** The button closes it. If there is a next player, the board is
  underneath with that player's turn already begun. If there is not, the game
  over screen follows.
- **Hardware back does nothing.** This makes a **fourth** dialog with an inert
  back, alongside roll and card, the working-out grid and answer result — and
  for the same reason they have one: it sits inside the turn's own chain, and a
  stray press should not fast-forward past a moment the game just stopped to
  mark. `shared-components.md`'s dialog invariant is updated to say four.
- **The game does not end here**, whatever the headline says. Play continues
  until every frog is home, which is
  [the board's rule](game-board.md#behaviour) and
  [Derek's recorded provisional call](../reference/index.md#where-v1-fills-a-gap-the-board-leaves-open),
  not a classroom-game rule. Whether it *should* end when the first frog gets
  home is [an open question](#open-questions) and is Connor's, not this
  wireframe's.

## Mockup

- [`mockups/player-won-first.html`](mockups/player-won-first.html) — the first
  frog home. `Blue wins!`, handing to the next player.
- [`mockups/player-won-later.html`](mockups/player-won-later.html) — the last
  frog home. `Pink is home!`, handing to the results.

Two files rather than one because the headline and the button each have two
states, and the two drawings cover all of both: a later frog that is **not** the
last combines the second file's headline with the first file's button, and
introduces no third thing to look at.

## Open questions

- **Should the game end when the first frog gets home?** Not proposed here, and
  deliberately not decided here. Today play continues, which is Derek's recorded
  provisional call rather than a classroom rule — and a dialog that says
  `Blue wins!` while the game carries on is exactly the moment somebody will ask
  why it does. It is a **rule** change, so it is Connor's, and it wants its own
  `type:question` issue rather than being folded into a layout.
- **What does the answer result's button say on the winning move?**
  [#287](https://github.com/derekwinters/connor-multiplying-frogs/issues/287)
  asks this, and this page changes what it means. That button used to be the
  last thing a winner read; now this dialog follows it. The placeholder
  `Game over` is worse than it was — it announces an ending that has not
  happened yet and that this dialog is about to talk over. Answering #287 should
  be done with this page in view.
- **Does the moment want more than a dialog?** A bigger chip state, an animation
  on the lane, a sound. Audio is [parked](../future-ideas.md). If the dialog
  turns out to be the wrong shape for this — if what Connor wants is the board
  celebrating rather than a panel interrupting — that is a legitimate answer and
  it is still a wireframe.
