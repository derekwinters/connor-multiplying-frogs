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
112 px per extra row, so at full-size rows the section stops fitting on the
*third* of its six allowed. What gives is
[open question 3](#open-questions), settled: the addition rows take
`GridAdditionRowHeight` once the section grows, and the grid fits at every
count up to the cap. The numbers are worked through under [Mockup](#mockup),
and the third mockup is the picture of them.

**"Centred in the space left over" is the rule, in both directions.**
Horizontally that space runs from `DialogPadding` to the keypad column's left
edge; vertically from the bottom of the header band (`GridHeaderHeight`) to the
panel's last usable pixel (`DialogMaxHeight` less `DialogPadding`) — 908 px.
The grid moves within that space as it grows and shrinks, which is what lets a
six-row section sit higher up the panel than a two-row one. The two committed
drawings hand-place the as-dealt grid at `top: 216`, about 12 px above where
centring puts it, and 2 px apart from each other horizontally; that is what
hand placement looks like, and the rule — not either drawing's origin — is what
the screen is built to.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Digit cell, square | `GridCellSize` | 104 px |
| Gap between cells and between rows | `GridCellGap` | 8 px |
| Digit cell outline | `GridCellBorderWidth` | 3 px |
| Digit cell corner radius | `GridCellRadius` | 10 px |
| Carry strip height | `GridCarryRowHeight` | 56 px |
| Carry box inside the strip | `GridCarryBoxSize` | 56 × 52 px |
| Carry box outline | `GridCarryBoxBorderWidth` | 3 px |
| Carry box corner radius | `GridCarryBoxRadius` | 8 px |
| Rule line thickness | `GridRuleThickness` | 6 px |
| Addition row height, once the section is grown | `GridAdditionRowHeight` | 56 px |
| Answer row height | `GridAnswerRowHeight` | 128 px |
| Answer cell border | `GridAnswerBorderWidth` | 6 px |
| Digit size in a cell | `GridDigitSize` | 56 px |
| Digit size in a 56 px box — a grown addition row, a carry box | `GridSmallDigitSize` | 28 px |
| `=` in the operator column | `GridEqualsSize` | 28 px |
| Header band, panel top down to the bottom of the chip | `GridHeaderHeight` | 140 px |
| Header row, down from the panel's top edge | `GridHeaderTop` | 44 px |
| Gap between the header's three items | `GridHeaderGap` | 32 px |
| `Work it out` | `GridPromptSize` | 44 px |
| Card readout pill height | `GridCardReadoutHeight` | 96 px |
| Card readout pill padding, each side | `GridCardReadoutPaddingX` | 32 px |
| Card readout pill corner radius | `GridCardReadoutRadius` | 20 px |
| Card readout pill outline | `GridCardReadoutBorderWidth` | 3 px |
| Card readout label | `GridCardReadoutLabelSize` | 40 px |
| Addition rows a card is dealt | `GridAdditionRowsAtStart` | 2 rows |
| Most addition rows the section can hold | `GridAdditionRowsMax` | 6 rows |
| Fill of the cell the caret is in | `GridFocusFill` | `#8CB89E` |
| Keypad, down from the panel's top edge | `KeypadTop` | 216 px |
| Keypad key, square | `KeypadKeySize` | 140 px |
| Gap between keys | `KeypadKeyGap` | 16 px |
| Key corner radius | `KeypadKeyRadius` | 20 px |
| Key outline | `KeypadKeyBorderWidth` | 3 px |
| Digit key label | `KeypadKeyLabelSize` | 56 px |
| Backspace glyph | `KeypadBackspaceLabelSize` | 40 px |
| `clear` label | `KeypadClearLabelSize` | 32 px |
| Keypad width (3 keys + 2 gaps) | `KeypadWidth` | 452 px |
| Gap between keypad and `Check it` | `KeypadSubmitGap` | 24 px |
| `Check it` height | `CheckButtonHeight` | 128 px |

`GridAdditionRowsAtStart` and `GridAdditionRowsMax` are counts, not pixels. They
are in this table anyway, because the number of rows the addition section starts
and stops at is exactly the kind of number that otherwise ends up as an unnamed
`2` in `Core` and an unnamed `6` in the Unity shell, and then only one of them
gets changed. They are the two rows on this table `Core` owns
([`WorkingOutGrid`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/WorkingOutGrid.cs)),
and the shell references them rather than keeping a copy.

**Every value above is a number a drawing already fixed**, except two:
`GridAdditionRowHeight`, which is Derek's answer to
[open question 3](#open-questions), and `GridSmallDigitSize`, which is what a
digit has to shrink to in order to sit inside a 56 px-tall box. No committed
drawing shows a digit in one — no mockup fills a carry box, and the grown
drawing's rows are empty — so it is the one number here read off a proportion
instead of a picture: the same share of its box's height that `GridDigitSize`
(56 px) is of `GridCellSize` (104 px), which lands on the 28 px the drawings
already use for their smallest glyph.

**A grown row has a size constant of its own**, and did not until
[#223](https://github.com/derekwinters/connor-multiplying-frogs/issues/223).
Once the section holds more rows than a card is dealt, every row in it —
including the two that were dealt — is `GridAdditionRowHeight` tall rather than
`GridCellSize`, with `GridSmallDigitSize` digits inside. Nothing else in the
grid changes size, ever: the multiplicand and multiplier rows, both rule lines,
both carry strips and the answer row are the same height at two rows as at six.
Growing the section therefore costs `GridAdditionRowHeight` + `GridCellGap` =
64 px per row instead of the 112 px it would cost at full size, and the first
grown row *shrinks* the grid rather than stretching it — see
[Mockup](#mockup) for the whole sum.

**`GridFocusFill` is the one colour on that table**, and it is there because it
is the one colour that carries a rule rather than a picture's paint. The rest of
this screen's palette is the mockups' — ink, line, faint, paper, accent — copied
into the shell and named nowhere. This one has to clear a bar, and a bar needs
somewhere to be written down.

### What focus looks like

**The cell the caret is in is filled with `GridFocusFill`**, and keeps the
accent outline it already had. Nothing else about it changes: not
`GridCellSize`, not `GridCarryBoxSize`, not `GridCellBorderWidth` or
`GridAnswerBorderWidth`. A filled box among empty boxes reads from across a
room; a box that is bigger or thicker than its neighbours is a different layout,
and layout is what the mockups are for.

Until [#304](https://github.com/derekwinters/connor-multiplying-frogs/issues/304)
this page said nothing at all about focus, and the shell filled the blank with
the quietest option available: the 3 px outline swapped from `#6C7873` grey to
`#2E7D4F` accent green, and nothing else. That is two mid-tone colours on a
hairline, and on the tablet Derek could not see it — knowing it was there. A
player who cannot tell which box the next digit lands in taps a box before every
digit, which is what he did.

The fill is the accent green the outline already uses, laid over paper at 55 %,
taken to where it clears the bar the project already sets for two colours being
**clearly separable** — a luminance contrast of at least **1.9 : 1** *and* a CIE
L\*a\*b\* distance (ΔE\*ab) of at least **30**, both measures, for the reasons
[`game-board.md`](game-board.md#keeping-the-frogs-visible) gives.

It has to clear that bar twice over, because a focused cell lands on two
different fills depending on where the caret is: the ordinary cell fill, and the
answer row's own tint. As *contrast : 1 / ΔE*:

| | Paper, `#FFFFFF` — every cell but the answer row's | The answer row's tint, `#F3F8F5` |
| --- | --- | --- |
| `GridFocusFill` | 2.22 / 36.2 | 2.07 / 32.5 |

Both clear it, the answer row being the tighter of the two, which is the pair
the treatment was always going to be decided by. Two more ratios worth recording
because they are what a darker tint would cost: a digit on the focused fill is
**7.11 : 1** (`#1E2422` ink, against a 4.5 : 1 text bar), and the accent outline
against the fill inside it is **2.27 : 1 / 30.4**, so the box still reads as a
box rather than as a blob.

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
    size, same border, no badge, nothing that says "you added this".
    The section is scratch paper and scratch paper does not annotate itself.
    The one fill a cell in the section can carry is `GridFocusFill`, and that is
    not a mark on the row: it is on whichever single cell the caret is in, dealt
    or grown, and it is gone the moment the caret leaves.
    The smaller `GridAdditionRowHeight` a grown section takes applies to *every*
    row in it, the two dealt ones included — a section where the first two rows
    were taller than the four below them would be exactly the "you added this"
    marking this bullet rules out.
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
- **Exactly one cell is focused, always, and it is the one the next digit lands
  in.** It is drawn filled with `GridFocusFill` — see
  [what focus looks like](#what-focus-looks-like) — and the fill follows the
  caret everywhere it goes: through typing, backspace, `clear`, a tap on another
  cell, and a section that grows or shrinks underneath it. It is drawn on carry
  boxes and addition cells exactly as it is in the answer row, because the caret
  goes there too. When the caret leaves a cell, the cell goes back to the fill
  it had — the tint is where the next digit goes, never a record of where one
  has been.
- Tapping any cell moves the caret there, so the grid can be filled in any
  order — a player who fills the addition rows first, then the answer, is not
  fighting the caret.
- **Growing the addition section.** Typing a single digit into the section's
  current bottom row appends another row beneath it, still above the answer row.
  That repeats each time the new bottom row is written in, until the section
  holds `GridAdditionRowsMax` rows, after which nothing more is appended. A
  single digit is enough; the row does not have to be finished first. The whole
  section takes `GridAdditionRowHeight` from the moment it holds more rows than
  the card dealt it, and keeps it until it is back to that count.
- Nothing prompts the player to grow the section and nothing rewards it. A
  player who carries with the superscript boxes never sees a third addition row,
  and a player who has never been shown the superscript boxes never has to use
  them.
- **Shrinking it again.** Backspacing the last digit out of a grown row removes
  the row ([open question 5](#open-questions), settled). Only the section's
  *bottom* row can go, and only a grown one: `GridAdditionRowsAtStart` is the
  floor, because no card is ever dealt fewer, and emptying a row further up the
  section leaves an empty row where it is. `clear` never removes a row — it is
  described as emptying a block, not as taking one away, and the settled answer
  is about backspace.
- No timer, ever. `331 × 41` on paper takes as long as it takes.
- There is no `undo`, only backspace and `clear` — and `clear` empties only the
  cell block you are in, not the whole grid. **A block is a row**: `clear`
  empties every cell of the row the caret is in and touches nothing above or
  below it. That is the narrowest reading of the sentence above, chosen in
  [#223](https://github.com/derekwinters/connor-multiplying-frogs/issues/223)
  rather than invented — the wider readings (the section, the grid) are the ones
  the sentence rules out.
- Backspace takes the digit in the caret's own cell, or — if that cell is
  already empty — the digit most recently typed anywhere in the same block, and
  moves the caret to wherever it just took one from. It never reaches outside
  the block.
- After a digit lands, the caret steps one cell to the left, in whatever row it
  is in, and stops at the leftmost digit column. That is the answer row's
  right-to-left fill applied everywhere, because the addition rows and the carry
  strips are worked out in the same direction.
- Hardware back does nothing.

## Mockup

Three, all at 1920 × 1200.

- **The widest card, as dealt:** [`mockups/working-out-grid-331x41.html`](mockups/working-out-grid-331x41.html)
- **The easy pile, as dealt:** [`mockups/working-out-grid-68x5.html`](mockups/working-out-grid-68x5.html)
- **The widest card, grown to the cap:** [`mockups/working-out-grid-331x41-grown.html`](mockups/working-out-grid-331x41-grown.html)

**All three draw the focused cell**, filled with `GridFocusFill`. They always
drew one — an answer cell with the accent outline — and until
[#304](https://github.com/derekwinters/connor-multiplying-frogs/issues/304) that
outline was the whole of it, in the drawings as in the shell. The cell is in the
same place in each; what changed is that you can now see which one it is. A
picture of this screen without a visible focused cell is a picture of a state
the player never actually sees.

The first two are the agreed pictures of the screen a card deals, and they are a
pair for the reason they always were: "the grid shrinks to fit the card" is a
claim best checked by looking at the biggest and the smallest next to each
other. The easy one is now the same height as the hard one and narrower, which
is what that claim has been reduced to.

**The third is the answer to [open question 3](#open-questions), and it used to
be the question.** It is the same screen as the first with the addition section
at `GridAdditionRowsMax` instead of `GridAdditionRowsAtStart`. Drawn at
full-size addition rows — which is how it was first committed, deliberately
overflowing, as the input to that question — it did not fit. Drawn at
`GridAdditionRowHeight`, which is what Derek settled, it does.

The space: the dialog is `DialogMaxHeight` (1104 px) tall and `DialogPadding`
(56 px) of that is not usable, so the last usable pixel is 1048 px into the
panel, and the header band takes the first `GridHeaderHeight` (140 px). That
leaves **908 px**.

Everything that is the same height at every count — two carry strips (56 each),
the multiplicand and multiplier rows (104 each), two rule lines (6 each) and the
answer row (128) — comes to **460 px**. The gaps are `GridCellGap` (8 px) between
every pair of the 7 + *n* things stacked, so 8 × (6 + *n*). The addition rows are
whatever *n* rows at whatever height the section is currently drawn at:

| Addition rows | Row height | Grid height | Against the 908 px available |
| --- | --- | --- | --- |
| 2 — as dealt | `GridCellSize`, 104 px | 460 + 208 + 64 = **732 px** | fits, 176 px spare |
| 3 | `GridAdditionRowHeight`, 56 px | 460 + 168 + 72 = **700 px** | fits, 208 px spare |
| 4 | 56 px | 460 + 224 + 80 = **764 px** | fits, 144 px spare |
| 5 | 56 px | 460 + 280 + 88 = **828 px** | fits, 80 px spare |
| 6 — the cap | 56 px | 460 + 336 + 96 = **892 px** | fits, **16 px spare** |
| *6, at full-size rows* | *104 px* | *460 + 624 + 96 = 1180 px* | *272 px too tall — the old third mockup* |

Two things worth saying out loud about that column of sums. **The margin at the
cap is 16 px**, so this fits and does not fit comfortably: anything that grows
the fixed 460 px — a taller answer row, a third carry strip, a heavier rule —
pushes the six-row grid off the bottom again, and the sum above is where to
check that. And **the grid gets shorter before it gets taller**: the third row
takes the whole section down to 56 px, so growing it the first time shrinks the
grid from 732 px to 700 px, and it only passes its as-dealt height at the fifth
row. That is the cost of one shrunk height for the whole section rather than a
different height at every count, and it is a visible jump the first time a
player grows a row.

## Open questions

Questions 2 to 5 arrived with the growable addition section
([#234](https://github.com/derekwinters/connor-multiplying-frogs/issues/234)).
Question 6 is older. Questions 1, 3 and 5 are settled; 2, 4 and 6 are not
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

- **3. What happens when the section outgrows the dialog? Settled: smaller
  cells for the addition rows only.** At full-size rows the section stopped
  fitting on its **third**, and at the cap the answer row was off the bottom of
  the tablet entirely. The options were scrolling the `grid` region, shrinking
  `GridCellSize` once the section passed some count, shrinking it for the whole
  grid up front, giving the addition section its own smaller row height, or
  lowering `GridAdditionRowsMax` to the 2 that fitted.

    Derek's call on [#223](https://github.com/derekwinters/connor-multiplying-frogs/issues/223):
    "Smaller cells for addition rows only." The section gets
    `GridAdditionRowHeight` (56 px) the moment it holds more rows than the card
    dealt it; the multiplicand and multiplier rows, both rule lines, both carry
    strips and the answer row stay exactly the size they are today, at every
    count. The problem stays full-size and legible, and the scratch paper is
    what shrinks — which is the right way round, since the scratch paper is the
    part nobody checks.

    The sum is in [Mockup](#mockup) and it was re-run rather than assumed: six
    grown rows make an **892 px** grid in **908 px** of space, so it fits with
    16 px to spare, and the third mockup now draws that instead of the overflow.
    Two consequences worth knowing: the margin at the cap is thin enough that
    any future growth in the fixed rows breaks it again, and the grid gets
    *shorter* when the third row appears before it gets taller again at the
    fifth.

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

    The interaction policy this left open — whether removing a row the player
    isn't currently at is reachable at all — was owned by whichever issue built
    turn interaction, which is
    [#223](https://github.com/derekwinters/connor-multiplying-frogs/issues/223),
    and it answered **no**. Only the section's bottom row is ever removed, and
    only by backspacing its last digit out; emptying a row further up leaves an
    empty row exactly where it is, and `clear` never removes a row at all. See
    [Behaviour](#behaviour).

- **6. Should the keypad have an `=`?** No, currently: `Check it` is the commit,
  and two ways to submit is one too many.
