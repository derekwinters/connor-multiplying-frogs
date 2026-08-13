# Roll and card

You pressed `Roll`. This is the die landing, the pile it sends you to, and the
card you drew.

## Invariants

**Invariant:** the die face shown is the roll, and the pile shown is the pile
that roll maps to. The mapping is fixed and is the classroom board's — 1 or 2 →
easy, 3 or 4 → medium, 5 or 6 → hard. See
[the pile labels](../reference/index.md#the-pile-labels).
**Invariant:** the card's problem obeys the drawn pile's shape, and nothing on
this screen can change which pile was drawn. There is no re-roll and no
discard — that is the classroom game, and it is what makes an easy card lucky.
**Invariant:** the pile is worth the same one lily pad whichever it is. Nothing
on this screen implies otherwise — no points, no difficulty bonus.
**Invariant:** this dialog cannot be dismissed. The only way out is `Solve it`,
because the card has been drawn and the turn is now under way.

## Regions

| Region | Job |
| --- | --- |
| `whose` | The player chip of the frog taking this turn |
| `die` | The die, showing the face that was rolled |
| `pile` | Which of the three piles that face sends you to |
| `card` | The drawn card, with the problem on it |
| `controls` | `Solve it` |

## Anchors

A [dialog](shared-components.md#dialog) centred on the canvas, `RollDialogWidth`
by `RollDialogHeight`, over the dimmed board.

Inside it, a single row: `die` and `pile` on the left as one group, `card` on
the right, vertically centred against each other. `whose` sits above the row,
left-aligned with the die; `controls` below it, right-aligned with the card, per
the dialog's primary-on-the-right rule.

The left group is deliberately the smaller of the two. The die is *how* you got
the card; the card is what you now have to do.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `RollDialogWidth` | 1280 px |
| Dialog height | `RollDialogHeight` | 760 px |
| `rolled` label size | `RolledLabelSize` | 40 px |
| Gap between the player chip and `rolled` | `RolledLabelGap` | 24 px |
| Die group column width | `DieColumnWidth` | 400 px |
| Die group top offset | `DieGroupTop` | 220 px |
| Die face, square | `DieFaceSize` | 240 px |
| Die corner radius | `DieCornerRadius` | 40 px |
| Die border width | `DieBorderWidth` | 4 px |
| Inset from the die's edge to the pip grid | `DiePipInset` | 34 px |
| Die pip diameter | `DiePipDiameter` | 40 px |
| Gap between die and pile label | `DiePileGap` | 32 px |
| Pile label size | `PileLabelSize` | 40 px |
| Card top offset | `CardTop` | 180 px |
| Card width | `CardWidth` | 560 px |
| Card height | `CardHeight` | 420 px |
| Card corner radius | `CardRadius` | 24 px |
| Card border width | `CardBorderWidth` | 4 px |
| Problem text size on the card | `CardProblemSize` | 120 px |
| Gap between the problem and its rule | `CardRuleGap` | 8 px |
| Rule under the problem, thickness | `CardRuleThickness` | 8 px |
| Rule under the problem, length | `CardRuleLength` | 360 px |
| Gap between the die group and the card | `RollCardGap` | 208 px |
| Die roll animation | `DieRollDuration` | 0.8 s |
| Card deal animation | `CardDealDuration` | 0.3 s |

`CardProblemSize` at 120 px is sized so `331 × 41` — the widest problem in the
game — sits inside `CardWidth` with room to spare, written the way the classroom
cards are written: the two numbers stacked and right-aligned, `×` to the left of
the second, a rule underneath.

The three horizontal values across the panel add up exactly, and that is what
fixes `RollCardGap`: the panel is `RollDialogWidth` (1280 px) less
`DialogPadding` (56 px) on each side, so 1168 px of content, and
`DieColumnWidth` (400) + `RollCardGap` (208) + `CardWidth` (560) is that 1168.
`RollCardGap` read 96 px until issue #221 and could not: it left 208 px of
content unaccounted for, and the committed mockup — the agreed wireframe — has
always drawn 208. The mockup was treated as authoritative and the table
corrected to match it.

`DieGroupTop` and `CardTop` are measured from the inside top of the panel, the
same way [game over](game-over.md)'s `GameOverHeadlineTop` and
`StandingsColumnTop` are measured from the top of the canvas. They differ by
40 px because the two groups are different heights: the taller card sits higher
so that it and the die group are vertically centred against each other.

`DieBorderWidth` and `CardBorderWidth` hold the same number as
[shared components](shared-components.md#button)' `ButtonBorderWidth` today and
are deliberately not that constant — the button is free to restyle its outline
without redrawing the die or the card. The die's own vertical stack adds up the
same way the row does: `DiePipInset` (34) either side of a three-across pip grid
inside a `DieFaceSize` (240) square bounded by `DieBorderWidth` (4) leaves 164 px
of grid, so each of the nine cells is 54⅔ px and a `DiePipDiameter` (40) pip sits
centred in one with room around it.

## Elements

- **Player chip** — [shared](shared-components.md#player-chip), active state.
  Four players share a tablet; the dialog says whose turn this is because the
  header behind it is dimmed.
- **`rolled`** — the word beside the chip, `RolledLabelSize`, `RolledLabelGap`
  past it, in the same quiet grey as a chip's second line. It is what makes
  `whose` a sentence — *Green rolled*, or *Connor rolled* once that frog has
  been renamed on [game setup](game-setup.md) — rather than a chip sitting on
  its own, and it is why this dialog needs no title of its own. The word beside
  the chip is `rolled` and only `rolled`; nothing is appended to the name
  inside the chip to make the sentence read.
- **Die** — one six-sided die, drawn with pips rather than a numeral. There is
  exactly one die: the rules card says "Dice", and three piles × two faces
  accounts for all six faces of one — see
  [the reference material](../reference/index.md#what-is-in-the-photograph).
- **Pile label** — `Easy pile · 1 or 2`, `Medium pile · 3 or 4`,
  `Hard pile · 5 or 6`. Naming both the pile and the two faces that reach it is
  how the board itself is labelled, and it is how a child learns the mapping
  without being taught it.
- **Card** — the drawn problem. This is a *picture of a card*, not the input
  surface; nothing is typed here.
- **`Solve it`** — primary [button](shared-components.md#button). Opens the
  [working-out grid](working-out-grid.md).

## Behaviour

- Entering: the dialog fades in already showing the die *rolling*. The die
  settles over `DieRollDuration`, then the pile label appears, then the card
  deals in over `CardDealDuration`. Total about 1.2 s.
- **The whole sequence can be skipped by tapping anywhere**, which jumps
  straight to the settled state. A four-player game plays this animation forty
  times, and the fortieth time nobody wants to watch it.
- Nothing here decides anything. The roll has already happened in Core before
  this dialog opened; the animation is a readout of a result, not a source of
  one. That matters for testing: the die is not random *here*.
- Hardware back does nothing — see the no-dismiss invariant. It is inert here
  because the alternative is losing a drawn card, the same reasoning that
  makes back inert on the [working-out grid](working-out-grid.md) and
  [answer result](answer-result.md).

## Mockup

[`mockups/roll-and-card.html`](mockups/roll-and-card.html) — drawn in its
settled state at the hard pile with `331 × 41`, the widest problem the card ever
has to hold.

## Open questions

- **Two beats or one?** This is one dialog that leads to another; it could
  instead be the top of the [working-out grid](working-out-grid.md) with no
  separate step. Proposed as its own beat, because on a shared tablet the pause
  is what lets the other three players see what you drew — the moment of
  "ooh, hard pile" is most of the fun of the piles existing.
- **Should the pile you drew be visible during answering?** Currently the pile
  label is only here; the grid shows the problem but not which pile it came
  from. Say if that is a loss.
