# Working-out grid

Where you actually do the multiplication. A grid with carry boxes, and a keypad
to fill it in with.

This screen is the one thing in the game the classroom game has no equivalent
of, and
[ADR-0002](../../adr/0002-structured-working-out-grid.md) is why it exists: at
the table, paper is ambient and the working-out *is* the exercise; on a tablet
there is nowhere to work.

## Invariants

**Invariant:** nothing in the grid is marked. Only the answer row decides
whether the frog moves. Carry boxes and partial-product rows are scratch paper —
they exist so you don't lose the 3, not to be checked.
**Invariant:** there is no mode and no toggle. The answer box *is* the grid's
bottom row. A player who can do `68 × 5` in their head fills one row and
presses `Check it`.
**Invariant:** the grid is sized to the card, not to the largest possible
problem. `68 × 5` does not get `331 × 41`'s rows.
**Invariant:** the grid never has to render a shape outside the three the piles
produce — `2×1`, `2×2`, `3×2` digits. `331 × 41` is the confirmed worst case.
**Invariant:** `Check it` is disabled until at least one digit is in the answer
row. An empty answer is not a wrong answer.
**Invariant:** this dialog cannot be dismissed. The card is drawn and the turn
is under way.

## Regions

| Region | Job |
| --- | --- |
| `header` | Whose turn, and the card being worked |
| `grid` | The working-out itself |
| `keypad` | Digits, backspace, clear |
| `submit` | `Check it` |

## Anchors

A full-bleed [dialog](shared-components.md#dialog) — `DialogMaxWidth` by
`DialogMaxHeight`, which is the canvas inset by `SafeMargin` on every side.

Inside it, two columns:

- `keypad` and `submit` are pinned to the **right**, `DialogPadding` from the
  panel edge. Fixed position and fixed size for every problem, so the digit keys
  are in the same place on every turn of every game. A keypad that moves is a
  keypad you have to look at.
- `grid` is centred in the space left over. It is the part that changes size
  with the problem, so it is the part that is allowed to move.
- `header` runs along the top, left-aligned.

This split is what landscape buys. In portrait the grid and the keypad have to
share a single column, and `331 × 41` was
[the project's biggest layout unknown](../../adr/0002-structured-working-out-grid.md#consequences)
because of it. Side by side, the worst case fits with room to spare.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Digit cell, square | `GridCellSize` | 104 px |
| Gap between cells and between rows | `GridCellGap` | 8 px |
| Carry strip height | `GridCarryRowHeight` | 56 px |
| Carry box inside the strip | `GridCarryBoxSize` | 56 × 52 px |
| Rule line thickness | `GridRuleThickness` | 6 px |
| Answer row height | `GridAnswerRowHeight` | 128 px |
| Answer cell border | `GridAnswerBorderWidth` | 6 px |
| Digit size in a cell | `GridDigitSize` | 56 px |
| Keypad key, square | `KeypadKeySize` | 140 px |
| Gap between keys | `KeypadKeyGap` | 16 px |
| Keypad width (3 keys + 2 gaps) | `KeypadWidth` | 452 px |
| Gap between keypad and `Check it` | `KeypadSubmitGap` | 24 px |
| `Check it` height | `CheckButtonHeight` | 128 px |

### How many columns and rows

Both are decided by the card, in `Core`, and drawn by the Unity shell:

- **Columns** = the number of digits in the largest possible product for that
  card's shape, plus one operator column on the left. `331 × 41` needs five
  digit columns (`13571`); `68 × 5` needs three (`340`).
- **Rows** depend on the multiplier:

    | Multiplier | Rows |
    | --- | --- |
    | 1 digit (`68 × 5`) | carry strip · multiplicand · multiplier · rule · answer |
    | 2 digits (`22 × 41`, `331 × 41`) | carry strip · multiplicand · multiplier · rule · partial product · partial product · rule · carry strip · answer |

A one-digit multiplier has no partial products and nothing to add up, so those
rows do not exist. That is the "sized to the card" rule doing its job: an easy
card should not look like homework.

## Elements

- **Multiplicand and multiplier rows** — printed, not editable. These two rows
  *are* the card.
- **Carry strip** — dashed boxes, one per column. Two strips at most: one above
  the multiplication and one above the final addition, which is the "twice over"
  ADR-0002 describes. Optional to use, never checked.
- **Partial-product rows** — ordinary empty cells, including the units column
  of the second row. **The placeholder zero is not pre-printed.** Some
  classrooms teach writing the `0`, some teach shifting left; leaving the cell
  ordinary lets both work, and pre-printing it would pick one — which is the
  thing ADR-0002 says not to do.
- **Answer row** — taller, heavier border, tinted. The only row that counts.
- **Keypad** — `1`–`9`, `0`, backspace, `clear`. No decimal point, no minus:
  every answer in this game is a positive whole number.
- **`Check it`** — primary [button](shared-components.md#button), full keypad
  width. Goes to [answer result](answer-result.md).

## Behaviour

- Entering from [roll and card](roll-and-card.md). The first empty answer cell
  is the focused one; typing fills the answer row **right to left**, which is
  the direction the digits are worked out in.
- Tapping any cell moves the caret there, so the grid can be filled in any
  order — a player who does the partial products first, then the answer, is not
  fighting the caret.
- No timer, ever. `331 × 41` on paper takes as long as it takes.
- There is no `undo`, only backspace and `clear` — and `clear` empties only the
  cell block you are in, not the whole grid.
- Hardware back does nothing.

## Mockup

- **The worst case:** [`mockups/working-out-grid-331x41.html`](mockups/working-out-grid-331x41.html)
- **The easy pile:** [`mockups/working-out-grid-68x5.html`](mockups/working-out-grid-68x5.html)

Two mockups, because "the grid shrinks to fit the card" is a claim best checked
by looking at the biggest and the smallest one next to each other.

## Open questions

- **Which carry convention does Connor's class use?** The mockups put one carry
  strip above the multiplication and one above the addition. The alternative is
  a strip attached to each partial-product row, which avoids reusing one strip
  across two passes. Nothing is marked either way, so this is about matching
  what he is taught — worth asking him rather than deciding here.
- **Should the keypad have an `=`?** No, currently: `Check it` is the commit,
  and two ways to submit is one too many.
