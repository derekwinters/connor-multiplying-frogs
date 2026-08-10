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
whether the frog moves. Carry boxes and addition rows are scratch paper — they
exist so you don't lose the 3, not to be checked.
**Invariant:** a row the player grows is exactly as ungraded as every other cell
in the grid. Growing the addition section is asking for more scratch paper, and
scratch paper is never checked
([ADR-0002](../../adr/0002-structured-working-out-grid.md#two-constraints-that-keep-it-from-becoming-a-tutor)).
**Invariant:** both superscript carry strips are always on screen, on every
card, whether or not the player uses the addition section. The two ways of
carrying are not alternatives the game chooses between — both are drawn, and the
player uses whichever one they were taught.
**Invariant:** there is no mode and no toggle. The answer box *is* the grid's
bottom row. A player who can do `68 × 5` in their head fills one row and
presses `Check it`.
**Invariant:** the grid is sized to the card in its **columns**. `68 × 5` gets
three digit columns and `331 × 41` gets five, so an easy card still does not
look like homework.
**Invariant:** the grid's **rows** are not sized to the card. Every card is
dealt the same rows, and from there the player — not the card — decides whether
there are more.
**Invariant:** the addition section never holds more than
`GridAdditionRowsMax` rows. The player decides when it grows; the cap decides
when it stops.
**Invariant:** the game never deals a *problem* outside the three shapes the
piles produce — `2×1`, `2×2`, `3×2` digits. `331 × 41` is still the confirmed
widest.
**Invariant:** `Check it` is disabled until at least one digit is in the answer
row. An empty answer is not a wrong answer.
**Invariant:** this dialog cannot be dismissed. The card is drawn and the turn
is under way.

### The invariants this page used to carry

Until this wireframe, two of the invariants above read differently. Both were
made false by the addition section becoming growable
([#234](https://github.com/derekwinters/connor-multiplying-frogs/issues/234)),
and the old wording is quoted here so the change is visible. Neither sentence is
true anywhere else on this page.

> **Invariant:** the grid is sized to the card, not to the largest possible
> problem. `68 × 5` does not get `331 × 41`'s rows.

This is now true of **columns only**, and it is no longer true of rows at all:
`68 × 5` gets exactly `331 × 41`'s rows, because every card starts with the same
ones. What survives is the intent — the easy card is visibly a smaller job,
because it is narrower.

> **Invariant:** the grid never has to render a shape outside the three the
> piles produce — `2×1`, `2×2`, `3×2` digits. `331 × 41` is the confirmed worst
> case.

Still true of the three **problem** shapes; ADR-0002's bound on the problems is
untouched, and nothing here lets the game deal a fourth shape. It is no longer
true of the **rendered row count**, which the player extends. `331 × 41` is the
widest grid but not the tallest one — the tallest is any card whose addition
section has been grown to `GridAdditionRowsMax`, and that grid is the same
height whichever card it came from.

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
because of it. Side by side, the widest card as dealt fits.

It does **not** fit with room to spare — the phrase this paragraph used to end
with, and the one ADR-0002 still ends with, both written before the addition
section could grow. As dealt, the grid ends 948 px
into a panel whose last usable pixel is 1048 px — about 100 px of slack against
112 px per extra row, so the section stops fitting on the *third* of its six
allowed rows. What to do about that is
[open question 3](#open-questions); the numbers are worked through under
[Mockup](#mockup), and the third mockup is the picture of them.

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
| Addition rows a card is dealt | `GridAdditionRowsAtStart` | 2 rows |
| Most addition rows the section can hold | `GridAdditionRowsMax` | 6 rows |
| Keypad key, square | `KeypadKeySize` | 140 px |
| Gap between keys | `KeypadKeyGap` | 16 px |
| Keypad width (3 keys + 2 gaps) | `KeypadWidth` | 452 px |
| Gap between keypad and `Check it` | `KeypadSubmitGap` | 24 px |
| `Check it` height | `CheckButtonHeight` | 128 px |

`GridAdditionRowsAtStart` and `GridAdditionRowsMax` are counts, not pixels. They
are in this table anyway, because the number of rows the addition section starts
and stops at is exactly the kind of number that otherwise ends up as an unnamed
`2` in `Core` and an unnamed `6` in the Unity shell, and then only one of them
gets changed.

**A grown row has no size constant of its own.** It is an ordinary row of
ordinary cells: `GridCellSize` tall, `GridCellGap` below the row above it, the
same cells the section already has. Growing the section costs
`GridCellSize` + `GridCellGap` = 112 px of height per row and nothing else.

### How many columns and rows

The card decides the columns. The card and the player between them decide the
rows.

- **Columns** = the number of digits in the largest possible product for that
  card's shape, plus one operator column on the left. `331 × 41` needs five
  digit columns (`13571`); `68 × 5` needs three (`340`).
- **Rows** are the same for every card, top to bottom:

        carry strip · multiplicand · multiplier · rule ·
        addition rows · rule · carry strip · answer

    The addition rows are the only part of that list whose count is not fixed,
    and what varies it is not the card: every card is dealt
    `GridAdditionRowsAtStart` of them, and the player can grow the section from
    there to `GridAdditionRowsMax`.

    The `rule` entries in that list are drawn separators, not rows: a straight
    line at `GridRuleThickness`, with no cells of its own, between two row
    kinds the shell already knows are adjacent. `Core` does not report them —
    [#204](https://github.com/derekwinters/connor-multiplying-frogs/issues/204)
    reports carry strip, multiplicand, multiplier, addition rows, carry strip,
    answer row, and nothing named `Rule`.

**"The addition section"** is this page's name for those rows — the block
between the first rule line and the second, which Derek calls the `"+ "`
section and which this page used to call the *partial-product rows*. They are
the same rows.
[#204](https://github.com/derekwinters/connor-multiplying-frogs/issues/204)
is written against the older name and means these. The section is renamed
because a one-digit card now gets it too, and on `68 × 5` its rows are not
partial products of anything — they are somewhere to add up.

#### What Derek settled, in his words

> Let's support both. […] The top superscript boxes can always exist. But if
> someone instead wants to add full numbers below the numbers, they can. If they
> enter numbers below the answer box, another row appears underneath it. Each
> time they add a row below, it would keep expanding. […] This is the `"+ "`
> section below the problem and above the answer.

> I meant below the problem above the answer
>
> Single digit
>
> Cap the rows of addition to 6, start with 2

> Single digit gets 2 rows too
>
> Add to bottom of addition rows, above answer row

Read together: the section is the one between the problem and the answer; a
**single digit** typed into it is what adds the next row; the new row is
appended to the **bottom of the section**, which stays above the answer row;
every card starts with **2** rows; and the section stops at **6**.

#### What `Core` decides, and what the player decides

The split matters because it is the thing
[#204](https://github.com/derekwinters/connor-multiplying-frogs/issues/204)
models and [#223](https://github.com/derekwinters/connor-multiplying-frogs/issues/223)
draws, and it is not the split either issue was written against.

| | Derived from the card | Player state |
| --- | --- | --- |
| Column count | ✓ | |
| Which cells are printed (the multiplicand and multiplier digits) | ✓ | |
| Every row except the addition rows | ✓ — the same for every card | |
| How many addition rows exist | starts at `GridAdditionRowsAtStart` | grows from there, to `GridAdditionRowsMax` |
| Cell contents | | ✓ |

So the grid's shape is **no longer a pure function of the card**. It is a
function of the card and of how many rows the player has grown, and the second
of those changes during a turn. It is still bounded at both ends, and by the
same two numbers for every card the piles can deal:
`GridAdditionRowsAtStart` rows when the card is dealt, `GridAdditionRowsMax` at
the most. There is no card that can be dealt more, and no player who can grow
past it.

## Elements

- **Multiplicand and multiplier rows** — printed, not editable. These two rows
  *are* the card.
- **Carry strip** — dashed boxes, one per column. Two strips, on every card: one
  above the multiplication and one above the final addition, which is the "twice
  over" ADR-0002 describes. Optional to use, never checked. `68 × 5` used to
  have only the first, because it had nothing to add up; it has both now that it
  has an addition section, subject to
  [open question 4](#open-questions). Where the second strip goes once the
  section grows is [open question 2](#open-questions).
- **Addition rows** — ordinary empty cells, including the units column of every
  row below the first. **The placeholder zero is not pre-printed.** Some classrooms teach
  writing the `0`, some teach shifting left; leaving the cell ordinary lets both
  work, and pre-printing it would pick one — which is the thing ADR-0002 says
  not to do.
  - **A grown row is indistinguishable from a dealt one.** Same cells, same
    size, same border, no tint, no badge, nothing that says "you added this".
    The section is scratch paper and scratch paper does not annotate itself.
  - The `+` glyph sits in the operator column of the **bottom row of the
    section**, wherever the bottom currently is, which is where the committed
    two-row drawing already puts it. Growing the section moves the glyph down
    with it rather than stamping a `+` on every row.
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
  order — a player who fills the addition rows first, then the answer, is not
  fighting the caret.
- **Growing the addition section.** Typing a single digit into the section's
  current bottom row appends another row beneath it, still above the answer row.
  That repeats each time the new bottom row is written in, until the section
  holds `GridAdditionRowsMax` rows, after which nothing more is appended. A
  single digit is enough; the row does not have to be finished first.
- Nothing prompts the player to grow the section and nothing rewards it. A
  player who carries with the superscript boxes never sees a third addition row,
  and a player who has never been shown the superscript boxes never has to use
  them.
- **Whether a grown row can be taken away again is not specified here.** Neither
  `backspace` nor `clear` is described as removing a row, and neither is
  described as leaving one behind. It is a question, not an omission — see
  [open question 5](#open-questions).
- No timer, ever. `331 × 41` on paper takes as long as it takes.
- There is no `undo`, only backspace and `clear` — and `clear` empties only the
  cell block you are in, not the whole grid.
- Hardware back does nothing.

## Mockup

Three, all at 1920 × 1200.

- **The widest card, as dealt:** [`mockups/working-out-grid-331x41.html`](mockups/working-out-grid-331x41.html)
- **The easy pile, as dealt:** [`mockups/working-out-grid-68x5.html`](mockups/working-out-grid-68x5.html)
- **The widest card, grown to the cap:** [`mockups/working-out-grid-331x41-grown.html`](mockups/working-out-grid-331x41-grown.html)

The first two are the agreed pictures of the screen a card deals, and they are a
pair for the reason they always were: "the grid shrinks to fit the card" is a
claim best checked by looking at the biggest and the smallest next to each
other. The easy one is now the same height as the hard one and narrower, which
is what that claim has been reduced to.

**The third is a question, not an agreed picture.** It is the same screen as the
first with the addition section at `GridAdditionRowsMax` instead of
`GridAdditionRowsAtStart`, everything else identical, at today's constants — and
at today's constants it does not fit:

| | Grid height | Ends, measured into the panel |
| --- | --- | --- |
| `GridAdditionRowsAtStart` (2 rows) | 732 px | 948 px, about 100 px of slack |
| `GridAdditionRowsMax` (6 rows) | 1180 px | 1396 px |

The dialog is `DialogMaxHeight` (1104 px) tall and `DialogPadding` (56 px) of
that is not usable, so the last usable pixel is 1048 px into the panel. The
header takes the first 140 px. That leaves **908 px for a grid that wants
1180 px** — 272 px too tall even shoved right up under the header, with the grid
free to re-centre upward under the [Anchors](#anchors) rule. The second rule
line, the second carry strip and **the answer row itself** all end up below the
bottom edge of the tablet.

That overrun is drawn rather than described because it is the input to
[open question 3](#open-questions). A mockup that visibly overflows is the
correct result here: the wireframe's job was to find out what the cap costs at
today's numbers, not to pick the fix.

## Open questions

Questions 2 to 5 arrived with the growable addition section
([#234](https://github.com/derekwinters/connor-multiplying-frogs/issues/234)).
Question 6 is older. Questions 1 and 5 are settled; 2, 3, 4 and 6 are not
answered by anything Derek has said yet.

- **1. Which carry convention does Connor's class use? Settled: shared
  strip.** The mockups put one carry strip above the multiplication and one
  above the addition, each reused across the rows it covers. The alternative
  was a strip attached to each addition row, so a carry is never reused across
  two passes.

    This was **the one open question on this page that changed the shape of
    the grid in `Core`**, which is why it is worth being exact about what
    depended on it. Depending on it: the number of carry strips a grid has,
    whether that number tracks the addition row count (and therefore grows
    during a turn), and the per-row-versus-shared structure the row list above
    states. Not depending on it: the column count, the addition section's
    starting count and cap, the growth trigger, the fact that nothing is
    graded, and the overrun measured in the third mockup.

    Derek's call on [#255](https://github.com/derekwinters/connor-multiplying-frogs/issues/255):
    "Shared strip — matches today's mockups." `Core` and the grid screen are
    built to what the mockups already drew — two strips at most, confirmed
    rather than redrawn. It was originally asked in
    [#227](https://github.com/derekwinters/connor-multiplying-frogs/issues/227),
    which was carrying two questions under one title and was closed once Derek
    answered the other one — superscript carries versus written-out addition
    rows, the change this page has already absorbed. The layout question
    itself is what #255 answered.

- **2. What happens to the second carry strip as the section grows?** Today it
  is pinned directly above the answer row. Three readings, none picked: it stays
  pinned above the answer; it follows the bottom of the growing section; or
  every addition row gets its own — which is question 1 answered the other way,
  arriving through the back door. The third mockup draws it pinned above the
  answer, because that is where the committed drawing has it.

- **3. What happens when the section outgrows the dialog?** Not decided, and by
  the measurements above this is not a distant edge case: the section stops
  fitting on its **third** row, and at the cap the answer row is off the bottom
  of the tablet entirely. Options include scrolling the `grid` region, shrinking
  `GridCellSize` once the section passes some count, shrinking it for the whole
  grid up front, giving the addition section its own smaller row height, or
  lowering `GridAdditionRowsMax` to what fits — which is 2 today. Each one costs
  something different, and picking one is a layout decision.

- **4. Does the one-digit card's addition section get the second rule line and
  the second carry strip?** `68 × 5` now gets the addition rows. It is not
  settled whether it also gets the structure that wraps them on a two-digit
  card, or whether the rows sit under the first rule line on their own. The
  redrawn `68 × 5` mockup uses the two-digit structure because that is the only
  one this project has drawn — a default in a picture, not a decision.

- **5. Can a grown row be taken away again? Settled: yes.** Derek, on
  [#204](https://github.com/derekwinters/connor-multiplying-frogs/issues/204):
  backspacing the last digit out of a grown row removes the row. This is
  Core-level state, not just drawing — the grid model needs a transition for
  the addition section shrinking by one row, not only growing by one.
  [`WorkingOutGrid`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/WorkingOutGrid.cs)
  reports the grid for a given `(card, addition row count)` snapshot, so a
  shrink is nothing more than the next snapshot's count being one lower — it
  does not need an operation of its own. `GridAdditionRowsAtStart` is the
  floor: no snapshot may ask for fewer, because no card is ever dealt fewer.
  What is not settled, and is not this page's or `Core`'s to settle: whether
  removing a row the player isn't currently at (not the section's bottom row)
  is reachable at all — that is the caret-and-keypad interaction policy, owned
  by whichever issue builds turn interaction.

- **6. Should the keypad have an `=`?** No, currently: `Check it` is the commit,
  and two ways to submit is one too many.
