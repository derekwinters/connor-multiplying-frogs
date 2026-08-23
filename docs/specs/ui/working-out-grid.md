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
**Invariant:** the answer box *is* the grid's bottom row. A player who can do
`68 × 5` in their head fills one row and presses `Check it`. Nothing has to be
switched on before the grid works.
**Invariant:** `Help me` does not survive the turn. It cannot be un-pressed and
it cannot be pressed twice, and every card is dealt with it unpressed — so it
never arrives at the next player in the state the last one left it.
**Invariant:** nothing `Help me` prints is graded, and nothing it prints is
entered for the player. It writes the products beside the rows; working each
one out, and adding them up, is still entirely the player's job.
**Invariant:** `Help me` prints, and does nothing else. It never fills a cell,
never moves the caret, never marks a row right or wrong, and never changes what
`Check it` checks.
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

A third was made false by `Help me`
([#327](https://github.com/derekwinters/connor-multiplying-frogs/issues/327)):

> **Invariant:** there is no mode and no toggle. The answer box *is* the grid's
> bottom row. A player who can do `68 × 5` in their head fills one row and
> presses `Check it`.

**Half of that is still true and half of it is not, and the half that is not
was reversed on purpose.** The answer box is still the grid's bottom row, and
nothing has to be switched on before the grid works — the sentence that
mattered survives intact as the first invariant above. What is gone is *"no
mode and no toggle"*: `Help me` is a toggle, it is discoverable, and pressing
it changes what the grid shows for the rest of the turn.

This is not drift. Derek asked for it in
[#327](https://github.com/derekwinters/connor-multiplying-frogs/issues/327),
the constraint it breaks is
[ADR-0002](../../adr/0002-structured-working-out-grid.md#two-constraints-that-keep-it-from-becoming-a-tutor)'s
second one, and per [CLAUDE.md](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
Derek's instruction beats the docs. The ADR is **amended to record the
reversal** rather than quietly contradicted; see
[what the ADR says now](../../adr/0002-structured-working-out-grid.md#two-constraints-that-keep-it-from-becoming-a-tutor).

The ADR's specific worry was *"every mode arrives at the next player in
whatever state the last one left it"*, and that worry is answered rather than
ignored: `Help me` resets with the card, which is what the third and fourth
invariants above are for. Its **other** constraint — nothing in the grid is
marked — is untouched, and the fifth invariant above is there to keep it that
way.

## Regions

| Region | Job |
| --- | --- |
| `header` | Whose turn, and the card being worked |
| `grid` | The working-out itself |
| `keypad` | Digits, backspace, clear |
| `submit` | `Check it` |
| `help` | The digit products `Help me` has printed, one per addition row |

`help` is drawn but empty until `Help me` is pressed, and empty for the whole
turn if it never is. It is a region rather than part of `grid` because nothing
in it is a cell: it is writing in the margin beside the rows, and the grid's
own column count, cell kinds and geometry are exactly what they were before the
button existed.

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
- `header` runs along the top, left-aligned — and now carries `Help me` at its
  **right** end, pinned to the panel's inner right edge, which is the keypad
  column's right edge. It is `GridHelpButtonHeight` tall, not the shared
  `ButtonHeight`, so that it sits exactly inside the `GridHeaderHeight` band
  alongside the `GridCardReadoutHeight` pill it shares that band with.
- `help` is a **right-aligned column**, its right edge `GridHelpGap` left of
  the grid's left edge, each item vertically centred on its own addition row.
  It moves with the grid, because an item belongs to the row beside it. The
  column is right-aligned rather than left, so the `×` in every item lines up
  and the eye reads down one edge.

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

**`help` fits in space that already existed, and this is where it comes
from.** The grid is centred in the 1257 px between `DialogPadding` and the
keypad column, and no card fills it: the widest, `331 × 41`, is 664 px, so
there are 296 px spare on each side. `help` uses the left half of that spare
and needs no new space at all. The tightest case is the widest card, because
that is the one where the grid is widest *and* the items are longest —
`40 × 300` at `GridHelpItemSize` renders 149 px into 264 px of room, so it
clears with 115 px to spare. That margin is worth knowing because it is what a
wider grid would eat.

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
| `Help me` height | `GridHelpButtonHeight` | 96 px |
| `Help me` width | `GridHelpButtonWidth` | 320 px |
| A printed product's text size | `GridHelpItemSize` | 32 px |
| Gap between the `help` column and the grid | `GridHelpGap` | 32 px |

`GridAdditionRowsAtStart` and `GridAdditionRowsMax` are counts, not pixels. They
are in this table anyway, because the number of rows the addition section starts
and stops at is exactly the kind of number that otherwise ends up as an unnamed
`2` in `Core` and an unnamed `6` in the Unity shell, and then only one of them
gets changed. They are the two rows on this table `Core` owns
([`WorkingOutGrid`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/WorkingOutGrid.cs)),
and the shell references them rather than keeping a copy.

**`GridHelpButtonHeight` is 96 px rather than the shared `ButtonHeight` of
112 px**, and it is the same 96 px as `GridCardReadoutHeight` and as the shared
`MinTouchTarget`. All three coincide on purpose: the button has to fit inside
the `GridHeaderHeight` (140 px) band that starts `GridHeaderTop` (44 px) down —
which leaves exactly 96 px — it sits directly beside the card readout pill and
would look wrong at a different height, and 96 px is the floor a touch target
may not go below. A 112 px button there would overhang the band by 16 px. This
is the same kind of screen-local override as the title screen's
[`TitleButtonHeight`](title-screen.md#named-constants), in the opposite
direction.

`GridHelpItemSize` is 32 px, not `GridSmallDigitSize` (28 px). The items are
not digits in boxes — they are labels in the margin, read at arm's length,
and they are the one thing on this screen a stuck player is looking *for*.
28 px is what a digit shrinks to in order to fit a 56 px box; nothing here has
to fit a box.

**Every value above is a number a drawing already fixed**, except three:
`GridFocusFill`, which is
[#304](https://github.com/derekwinters/connor-multiplying-frogs/issues/304)'s
and is arrived at against a bar rather than read off a picture — see
[what focus looks like](#what-focus-looks-like);
`GridAdditionRowHeight`, which is Derek's answer to
[open question 3](#open-questions), and `GridSmallDigitSize`, which is what a
digit has to shrink to in order to sit inside a 56 px-tall box. No committed
drawing shows a digit in one — no mockup fills a carry box, and the grown
drawing's rows are empty — so it is read off a proportion
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
- **`Help me`** — secondary button, in the `header` band at its right end,
  `GridHelpButtonWidth` × `GridHelpButtonHeight`. Present on **every** card,
  including `68 × 5`, and quiet: a button that appears from nowhere on the
  third card is worse than one that has always been there and never had to be
  pressed. Two states only:
  - **Default** — the ordinary secondary button. Pressing it fills `help`.
  - **Disabled** — `ButtonDisabledOpacity`, from the moment it is pressed until
    the turn ends. It is not a toggle that can be turned back off, because the
    rows it grew are still there and a button that removed them would be
    deleting the player's own scratch paper.

  It is deliberately at the opposite end of the panel from the keypad. Pressing
  it is one-way for the turn, and a one-way control under the hand that is
  typing is a control that gets pressed by accident.
- **The printed products** — the contents of `help`. One line per addition row,
  at `GridHelpItemSize`, in the same `line` grey the mockups use for
  everything that is not a digit. **Not cells, not editable, not tappable, and
  never graded.** Written `multiplier part × multiplicand part` with the place
  value expanded — `30 × 10`, not `3 × 1` — which is Derek's own form and the
  form the rows are written in on paper.

## Behaviour

- Entering from [roll and card](roll-and-card.md). The answer row's **leftmost**
  digit column is the focused one; typing fills the answer row **left to
  right**, which is the direction the answer is written and read in.
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
- After a digit lands, the caret steps one cell to the **right**, in whatever
  row it is in, and stops at the **last** digit column. There is **one
  direction everywhere** — the answer row, the addition rows and both carry
  strips fill the same way, so there is one rule to learn rather than one for
  the answer and another for the scratch paper.
- **The last digit column is where the caret stops, not where it wraps.**
  Typing more digits than the grid has columns overwrites the final box. The
  caret does not wrap to another row, leave the row it is in, or reach the
  operator column.
- **Pressing `Help me`.** In one step, and only ever once per turn:
    1. The addition section grows to hold one row per product — to
       `max(GridAdditionRowsAtStart, product count)`, so a card with fewer
       products than the section was dealt does not shrink it.
    2. Each product is printed in `help` beside its row, in order.
    3. The button goes to `Disabled` and stays there until the card is done.

  Nothing else changes. The caret does not move, no cell is filled, no cell is
  marked, and `Check it` still checks exactly the answer row.

  On a `2 × 2` or `3 × 2` card this grows the section past
  `GridAdditionRowsAtStart`, so **the whole section drops to
  `GridAdditionRowHeight` the instant the button is pressed** and the grid
  visibly re-lays-out. That is the existing rule for a grown section, not a new
  one — but it is the one thing about this button that will look like it did
  something odd, so the mockups draw it rather than describe it. On `68 × 5`
  there are two products and the section was dealt two rows, so nothing grows
  and nothing shrinks.

  **On a card with no products, the three steps still run and two of them do
  nothing.** `68 × 0` makes no products
  ([what `Core` owns](#what-core-owns-the-product-list)), so the section grows
  to `max(GridAdditionRowsAtStart, 0)` — the count it was dealt — nothing is
  printed, and the button goes to `Disabled` anyway, because step three is not
  conditional on what the first two found. That is this page's rule read
  literally rather than an answer to what the player *should* see there, which
  is a taste call nobody has made:
  [open question 13](#open-questions), and
  [#433](https://github.com/derekwinters/connor-multiplying-frogs/issues/433).
- **A row `Help me` grew cannot be backspaced away.** The ordinary shrink rule
  — *"backspacing the last digit out of a grown row removes the row"* — does
  not apply to the rows this button created, for the rest of the turn. A row
  with a product printed beside it that vanished would leave the product
  pointing at nothing, and the player did not ask for the row in the first
  place. Rows grown **by typing**, after the button was pressed, shrink the
  ordinary way; the floor is whatever count `Help me` established.
- **It resets with the card.** The next card is dealt with `Help me` live,
  `help` empty and the section back at `GridAdditionRowsAtStart`. Nothing
  about it is remembered between turns or between games — which is the direct
  answer to
  [ADR-0002](../../adr/0002-structured-working-out-grid.md#two-constraints-that-keep-it-from-becoming-a-tutor)'s
  objection that *"every mode arrives at the next player in whatever state the
  last one left it."*
- Hardware back does nothing.

### The fill direction this page used to carry

Until [#305](https://github.com/derekwinters/connor-multiplying-frogs/issues/305)
two of the bullets above — the first one, and the one about where the caret
steps — read the other way round, and the shell was built to them. The old
wording is quoted here so the change is visible rather than silently
overwritten. Neither sentence is true anywhere else on this page.

> The first empty answer cell is the focused one; typing fills the answer row
> **right to left**, which is the direction the digits are worked out in.

> After a digit lands, the caret steps one cell to the left, in whatever row it
> is in, and stops at the leftmost digit column.

**This is a change to the contract, not a correction of drifted code.** The page
and the shell agreed with each other and were wrong together in front of a
player: reading the row back left to right while filling it right to left meant
a player who typed `3`, `4`, `0` for 340 submitted **43**. The multiplication
done correctly, and the frog left where it was. The only way to enter 340 was to
tap each box before typing into it — the workaround Derek found on the tablet,
and what his report was actually describing.

**Why this direction and not a calculator's.** Two options were put to Derek:
calculator-style entry for the answer row, where digits shift left as they are
typed and the answer stays flush right; or this one, where the caret starts at
the left, steps right, and a digit stays in the box it landed in. He picked the
second:

> my version. calculator would work if it was not boxes, but boxes implies
> explicit entry to me.

That reasoning is the answer to the obvious objection — that a card whose answer
is shorter than the grid is wide, like `12 × 3` in three digit columns, ends up
as `36_` with the 6 in the tens column. **A row of boxes is a row of slots, and
a slot is something you point at.** Tapping a cell already moves the caret
there, so a player who wants their 36 under the tens and units taps the middle
box and types it there. Lining the answer up under the columns stays the
**player's** job, which for a game about learning to multiply on paper is where
it belongs. A calculator would have done it for them, silently, which is the
thing that makes it wrong for a grid drawn as boxes.

**Grading does not care about columns, and that stays true.** The answer row's
digits are read left to right with empty boxes contributing nothing, so `36_`
and `_36` both read `36` and both grade correct — see the
[Invariants](#invariants) above and
[ADR-0002](../../adr/0002-structured-working-out-grid.md), which is emphatic
that only the answer row's *value* is checked. A future change that rejects or
re-grades a correctly-typed but left-aligned answer would be a new rule about
place value being marked, and it needs its own decision.

### What `Core` owns: the product list

Working out which products a card makes is **game logic**, not drawing, so it
lives in the engine-free `Core` assembly beside
[`WorkingOutGrid`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/WorkingOutGrid.cs),
in
[`DigitProducts`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/DigitProducts.cs)
([#415](https://github.com/derekwinters/connor-multiplying-frogs/issues/415)).
The shape was named here before it was built, so the implementation could not
invent a different one:

- **`DigitProducts.For(Card card)`** — a **pure function** from a card to an
  ordered, read-only list of `DigitProduct`. Same card, same list, every time;
  no state, no `Rng`, nothing to reset.
- **`DigitProduct`** — a readonly struct of two ints, `MultiplierPart` and
  `MultiplicandPart`. Both are **place-value expanded**: the tens digit `3` of
  a multiplier `34` is the part `30`. Formatting it as `30 × 10` is the shell's
  job; `Core` returns numbers, not strings.
- **The order** is Derek's: every part of the **multiplier**, units first; and
  within each, every part of the **multiplicand**, units first. `12 × 34` gives
  `(4,2) (4,10) (30,2) (30,10)`.
- **A zero part contributes nothing and is skipped.** `102 × 40` gives
  `(40,2) (40,100)` — two products, not six — because `40 × 0` is a row that
  adds nothing and a line of writing that teaches nothing. An operand of `0`
  therefore has no parts at all, which is the same rule rather than a case of
  its own: `68 × 0` — a card the Easy pile can deal, since
  [`Card.OneDigitMinimum`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/Card.cs)
  is `0` — makes **no products**, and `Help me` on it has nothing to print.
  What the shell does with an empty list is the shell's, and as built
  ([#416](https://github.com/derekwinters/connor-multiplying-frogs/issues/416))
  it prints nothing, grows nothing and disables the button anyway — see
  [Behaviour](#behaviour) and [open question 13](#open-questions).
- **The list is never longer than `GridAdditionRowsMax`.** Three multiplicand
  digits by two multiplier digits is six, which is exactly the cap, and
  ADR-0002 bounds the card shapes at `3 × 2`. So the cap holds — and holds
  exactly, with nothing to spare. Skipping zeros only ever makes the list
  shorter, so it cannot be what breaks that; the bound is the same either way.

This is tested in the fast `Core` suite with no editor, which is the point of
putting it there —
[`Tests/Core/DigitProductsTests.cs`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Tests/Core/DigitProductsTests.cs)
holds the worked examples above, and it holds the `GridAdditionRowsMax` bound
over **every card the three piles can deal** — all 90,000 of them — rather than
over one example, because the card that would break a bound is the one nobody
thought to write down.

## Mockup

Three, all at 1920 × 1200.

- **The widest card, as dealt:** [`mockups/working-out-grid-331x41.html`](mockups/working-out-grid-331x41.html)
- **The easy pile, as dealt:** [`mockups/working-out-grid-68x5.html`](mockups/working-out-grid-68x5.html)
- **The widest card, grown to the cap:** [`mockups/working-out-grid-331x41-grown.html`](mockups/working-out-grid-331x41-grown.html)

Three more, drawn for `Help me`, all showing the state **after** it is pressed:

- **`12 × 34`, items beside the grid — the agreed picture:**
  [`mockups/working-out-grid-help-12x34.html`](mockups/working-out-grid-help-12x34.html)
- **`12 × 34`, items inside a widened operator column — the comparison:**
  [`mockups/working-out-grid-help-12x34-operator-column.html`](mockups/working-out-grid-help-12x34-operator-column.html)
- **`331 × 41`, six products at the cap:**
  [`mockups/working-out-grid-help-331x41.html`](mockups/working-out-grid-help-331x41.html)

**All three draw the focused cell**, filled with `GridFocusFill`. They always
drew one — an answer cell with the accent outline — and until
[#304](https://github.com/derekwinters/connor-multiplying-frogs/issues/304) that
outline was the whole of it, in the drawings as in the shell. The cell is in the
same place in each; what changed is that you can now see which one it is. A
picture of this screen without a visible focused cell is a picture of a state
the player never actually sees.

**All three were already drawn filling left to right**, and nothing about them
changed for
[#305](https://github.com/derekwinters/connor-multiplying-frogs/issues/305).
The two `331 × 41` drawings put `1` and `3` in the first two answer boxes with
the focused cell immediately to their right and the rest empty, and the
`68 × 5` drawing focuses the leftmost answer box on an empty row. Under the
right-to-left rule those were pictures of states the shell could not reach —
a caret to the *right* of the digits already typed is only reachable if the
digits fill towards it. The drawings were right about the direction before the
Behaviour section was, which is worth saying out loud: fill direction is not
something a static picture *states*, but it turns out to be something a picture
can quietly contradict.

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

### What the `Help me` pair established

The two `12 × 34` drawings are a pair asking
[open question 8](#open-questions): whether the products sit inside a widened
operator column, or outside the grid altogether. Both are drawn because the
issue was right that this is what a mockup is for.

The comparison did not go the way the arithmetic suggested. The issue expected
the operator column to be the expensive option because the grid *"is already
16 px from not fitting at six rows"* — but that 16 px is **vertical** and
widening a column is **horizontal**, where there is 296 px spare on each side.
Drawn, the widened column fits fine and looks tidier than expected: the
products sit inside the grid's own left column, right-aligned, and read as part
of the working rather than as marginalia.

What decided it was two things a sum does not show:

- **The bottom row reads `+  30 × 10`.** The operator column already holds the
  `+` glyph on the section's bottom row, and a product printed in the same cell
  shares it. `+ 30 × 10` looks like an instruction to add thirty tens.
- **`×`, `+` and `=` go adrift.** Doubling the column to 208 px leaves the
  three glyphs floating in twice the space they need, on every card, whether or
  not `Help me` was ever pressed. That is a permanent cost paid by a feature
  nobody has to use.

Underneath both is the structural point: the operator column is part of the
grid, and `Core`'s
[`WorkingOutGrid`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/Assets/Scripts/Core/WorkingOutGrid.cs)
reports the grid's columns and cell kinds. Widening one of them for one feature
changes the grid's geometry contract for every card. Printing outside it
changes nothing at all.

**These three are placed by this page's rule, not by hand.** The three older
grid mockups hand-place the grid — 370 where centring puts 352 — and
[Anchors](#anchors) already says the rule wins. These were generated from the
rule, so the two sets are about 18 px apart horizontally and that difference is
the older drawings', not these. They also draw the disabled button as
`ButtonDisabledOpacity`, which is what
[shared-components.md](shared-components.md#button) asks for and what the older
grid mockups' flat grey still gets wrong.

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

Questions 7 to 12 arrived with `Help me`
([#327](https://github.com/derekwinters/connor-multiplying-frogs/issues/327)).
7, 8 and 9 are settled by drawing; 10, 11 and 12 are not settled by anything
anyone has said yet.

- **7. Where does `Help me` live? Settled: the header band, at its right end.**
  The issue offered three places and the measurement removed one of them
  outright. **Under `Check it` does not fit**: the keypad runs to 827 px into
  the panel, `KeypadSubmitGap` and `CheckButtonHeight` take it to 979, and the
  last usable pixel is 1048 — 69 px, where the shared `ButtonHeight` is 112.
  There is no room in that column and there is no way to make room without
  moving the digit keys, which
  [Anchors](#anchors) forbids. *"Floating beside the grid"* was ruled out for a
  different reason: it puts a one-way control on the side
  [the dialog rule](shared-components.md#dialog) says *"is where a thumb
  rests"*. The header band is where it fits and where it cannot be pressed by
  the hand that is typing.

- **8. What do the printed items look like? Settled: outside the grid,
  right-aligned in the space the grid is centred in.** The reasoning, and what
  the losing drawing turned out to cost, are in
  [what the `Help me` pair established](#what-the-help-me-pair-established)
  above. The comparison file is kept for now rather than deleted, because
  unlike this page's earlier pairs the losing option here is genuinely
  attractive and the argument against it is worth being able to look at. If
  Derek agrees with the call, it goes the way of
  [the title screen's `RESUME`-primary drawing](title-screen.md#mockup).

- **9. What happens to the section's growth rule? Settled: it grows to the
  product count, and those rows do not shrink back.** `Help me` grows the
  section to `max(GridAdditionRowsAtStart, product count)` in one step. The
  rows it created are exempt from the backspace-removes-a-grown-row rule for
  the rest of the turn — the issue's own recommendation, and taken because a
  row vanishing out from under a printed product leaves the product pointing at
  nothing. See [Behaviour](#behaviour).

- **10. Is the product order right?** `12 × 34` gives `4 × 2`, `4 × 10`,
  `30 × 2`, `30 × 10` — units of the multiplier first, and within each, units
  of the multiplicand first, with place value expanded. That is Derek's own
  list from the issue, so it is the default and the drawings use it. It is
  still worth **asking Connor whether it is the order his class writes them
  in**, because being the order on the board is the entire point of the
  feature. If it is not, the change is one line in `DigitProducts.For` and
  nothing else on this page moves.

- **11. Should a zero part be printed at all?** `102 × 40` expands to parts
  `2`, `0`, `100` and `0`, `40`. Printing every combination gives six products,
  three of which are `40 × 0` and none of which teach anything, so
  [`DigitProducts.For`](#what-core-owns-the-product-list) **skips zero parts**
  and that card gets two. This is a decision made in order to be able to draw
  anything at all, not one anybody asked for, and it is reversible: it changes
  what is printed and nothing about the layout, because both readings are
  bounded by the same six rows. If Connor's class writes the zero rows out, the
  skip comes off.

  Worth noticing while it is open: on `331 × 41` the first product prints as
  `1 × 1`, which is correct, useless, and slightly odd-looking. That is what
  expanded place value does to a leading `1` and it is not a bug.

- **12. Is `Help me` the right words?** Derek's phrase, and the default. It is
  the button an eight-year-old presses when he is stuck, so what it should say
  is Connor's call — the same way
  [`Designed by Connor`](settings-dialog.md#open-questions) is.

Question 13 arrived with the built button
([#416](https://github.com/derekwinters/connor-multiplying-frogs/issues/416)).

- **13. What should `Help me` do on a card with no products?**
  ([#433](https://github.com/derekwinters/connor-multiplying-frogs/issues/433))
  `68 × 0` is a card the easy pile can deal and a card this button has nothing
  to print for. What is built is the three steps above read literally: nothing
  moves, nothing is printed, and the button greys out — so the press looks like
  it did nothing, which is the one thing an eight-year-old responds to by
  pressing again. The alternatives are leaving the button live on such a card,
  or printing one line of words where the products would go, which would be the
  only words on this screen that are not a number. Whichever way it goes it
  changes what the player sees, so it is Connor's or Derek's rather than an
  agent's.
