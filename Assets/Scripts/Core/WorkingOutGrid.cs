using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>
    /// The kind of row in a <see cref="WorkingOutGrid"/>, top to bottom.
    /// docs/specs/ui/working-out-grid.md#how-many-columns-and-rows: every
    /// card is dealt the same row *kinds* — a carry strip, the multiplicand,
    /// the multiplier, the addition section, another carry strip, then the
    /// answer row. There is no <c>Rule</c> entry: a rule line is a drawn
    /// separator between two adjacent rows, not a row of its own
    /// (docs/adr/0002-structured-working-out-grid.md's amendment note). There
    /// is no <c>SumRow</c> entry either — the answer row *is* the sum.
    /// </summary>
    public enum GridRowKind
    {
        /// <summary>
        /// A strip of dashed carry boxes, one per digit column. Two of these
        /// exist on every grid — see <see cref="WorkingOutGrid.CarryStripCount"/> —
        /// and neither is graded.
        /// </summary>
        CarryStrip,

        /// <summary>The card's first operand, printed and not editable.</summary>
        Multiplicand,

        /// <summary>The card's second operand, printed and not editable.</summary>
        Multiplier,

        /// <summary>
        /// One row of the growable addition section — CONTEXT.md and Derek
        /// call it the <c>"+ "</c> section. Ordinary editable cells; nothing
        /// here is graded. How many of these a grid has is the one row count
        /// this type does not fix — see <see cref="WorkingOutGrid.For"/>.
        /// </summary>
        AdditionRow,

        /// <summary>
        /// The only row that is graded. Editable, and always exactly one.
        /// </summary>
        AnswerRow
    }

    /// <summary>
    /// The kind of a single cell within a <see cref="GridRow"/>.
    /// docs/specs/ui/working-out-grid.md#elements and #204's cell-kind list.
    /// </summary>
    public enum GridCellKind
    {
        /// <summary>
        /// A digit column with nothing in it and nothing fillable — the
        /// leading positions of a printed row — or the operator column, which
        /// is never fillable and never carries a printed digit. Which glyph
        /// (if any) the shell draws over an operator-column cell is derived
        /// from the row kind and is not this type's concern
        /// (#204 "The operator column").
        /// </summary>
        Blank,

        /// <summary>A digit off the card, right-aligned. Never fillable.</summary>
        Printed,

        /// <summary>An empty cell the player may fill.</summary>
        Editable,

        /// <summary>The dashed carry box on a carry strip. Optional, never graded.</summary>
        CarryBox
    }

    /// <summary>
    /// One cell of a <see cref="GridRow"/>. <see cref="Digit"/> is only
    /// meaningful when <see cref="Kind"/> is <see cref="GridCellKind.Printed"/>
    /// — every other kind carries no content, because this type reports the
    /// grid's *shape*, not anything a player has typed into it.
    /// </summary>
    public readonly struct GridCell
    {
        internal GridCell(GridCellKind kind, int? digit = null)
        {
            Kind = kind;
            Digit = digit;
        }

        public GridCellKind Kind { get; }

        public int? Digit { get; }
    }

    /// <summary>
    /// One row of a <see cref="WorkingOutGrid"/>: a <see cref="GridRowKind"/>
    /// and one <see cref="GridCell"/> per column, left to right, column zero
    /// being the operator column.
    /// </summary>
    public sealed class GridRow
    {
        internal GridRow(GridRowKind kind, IReadOnlyList<GridCell> cells)
        {
            Kind = kind;
            Cells = cells;
        }

        public GridRowKind Kind { get; }

        public IReadOnlyList<GridCell> Cells { get; }
    }

    /// <summary>
    /// The working-out grid's shape for a card, before any of it is drawn —
    /// ADR-0002: "Which cells exist for a given problem is game logic and
    /// belongs in `Core`; only drawing them belongs in the Unity shell."
    ///
    /// Since the addition section became growable
    /// (<see href="https://github.com/derekwinters/connor-multiplying-frogs/issues/234">#234</see>),
    /// the grid's shape is no longer a pure function of the card alone —
    /// docs/specs/ui/working-out-grid.md#what-core-decides-and-what-the-player-decides.
    /// <see cref="For"/> takes the card **and** the addition row count as of
    /// this moment, and reports the grid for that snapshot. It has no opinion
    /// on when the count should grow or shrink — that policy belongs to
    /// whichever issue owns turn interaction (#210 and the UI issues); this
    /// type only ever answers "given these inputs, what does the grid look
    /// like right now."
    ///
    /// This type knows nothing about a roll, a pile, a turn, or a correct
    /// answer, and nothing in the grid it reports is marked
    /// (docs/specs/ui/working-out-grid.md#invariants).
    /// </summary>
    public sealed class WorkingOutGrid
    {
        /// <summary>
        /// Addition rows a card is dealt. docs/specs/ui/working-out-grid.md's
        /// named-constants table — same name, same value, so a change to one
        /// cannot drift from the other.
        /// </summary>
        public const int GridAdditionRowsAtStart = 2;

        /// <summary>
        /// Most addition rows the section can hold.
        /// docs/specs/ui/working-out-grid.md's named-constants table.
        /// </summary>
        public const int GridAdditionRowsMax = 6;

        /// <summary>
        /// Carry strips on every grid — one above the multiplication and one
        /// above the addition section, reused rather than one per addition
        /// row. Settled by Derek on #255: "shared strip — matches today's
        /// mockups." Fixed at two regardless of the addition row count, which
        /// is exactly what makes it safe to hard-code here rather than derive
        /// it from <see cref="GridAdditionRowsAtStart"/> or
        /// <see cref="GridAdditionRowsMax"/>.
        /// </summary>
        public const int CarryStripCount = 2;

        // The operator column: column zero, present in every row, never
        // fillable, never carrying a digit. One of these and only one, on
        // every row — see #204 "The operator column".
        const int OperatorColumnCount = 1;

        WorkingOutGrid(int columnCount, IReadOnlyList<GridRow> rows)
        {
            ColumnCount = columnCount;
            Rows = rows;
        }

        /// <summary>
        /// The operator column plus one column per digit of the largest
        /// possible product for the card's *shape* — never the card's actual
        /// product. Purely a function of the card; unaffected by the addition
        /// row count.
        /// </summary>
        public int ColumnCount { get; }

        /// <summary>Every row of the grid, top to bottom.</summary>
        public IReadOnlyList<GridRow> Rows { get; }

        /// <summary>
        /// The grid for <paramref name="card"/> with its addition section
        /// currently holding <paramref name="additionRowCount"/> rows.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="card"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="additionRowCount"/> is outside
        /// <see cref="GridAdditionRowsAtStart"/> to <see cref="GridAdditionRowsMax"/> —
        /// every card is dealt with at least the starting count and the
        /// section never holds more than the cap
        /// (docs/specs/ui/working-out-grid.md#invariants).
        /// </exception>
        public static WorkingOutGrid For(Card card, int additionRowCount)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (additionRowCount < GridAdditionRowsAtStart || additionRowCount > GridAdditionRowsMax)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(additionRowCount),
                    additionRowCount,
                    $"the addition section holds {GridAdditionRowsAtStart} to "
                    + $"{GridAdditionRowsMax} rows; {additionRowCount} is neither.");
            }

            var columnCount = ColumnCountFor(card);

            var rows = new List<GridRow>
            {
                CarryStripRow(columnCount),
                PrintedRow(GridRowKind.Multiplicand, card.Multiplicand, columnCount),
                PrintedRow(GridRowKind.Multiplier, card.Multiplier, columnCount),
            };

            for (var i = 0; i < additionRowCount; i++)
            {
                rows.Add(EditableRow(GridRowKind.AdditionRow, columnCount));
            }

            rows.Add(CarryStripRow(columnCount));
            rows.Add(EditableRow(GridRowKind.AnswerRow, columnCount));

            return new WorkingOutGrid(columnCount, rows);
        }

        // The card decides the columns — docs/specs/ui/working-out-grid.md:
        // "the number of digits in the largest possible product for that
        // card's shape, plus one operator column." The shape is read off the
        // card's own operands' digit counts, not off the pile it came from —
        // this type has no notion of a pile — and the *largest possible*
        // product for that shape, never the card's actual product, which is
        // what keeps the grid sized to the shape and not to the answer.
        static int ColumnCountFor(Card card)
        {
            var largestPossibleProduct =
                LargestValueOfSameDigitCount(card.Multiplicand)
                * LargestValueOfSameDigitCount(card.Multiplier);

            return DigitCount(largestPossibleProduct) + OperatorColumnCount;
        }

        // The largest operand sharing `operand`'s digit count — e.g. 68 and
        // 11 both have two digits, so both map to 99. The three digit-count
        // bands are Card's own named bounds, not new literals standing in for
        // "one digit" or "two digits".
        static int LargestValueOfSameDigitCount(int operand)
        {
            if (operand <= Card.OneDigitMaximum)
            {
                return Card.OneDigitMaximum;
            }

            if (operand <= Card.TwoDigitMaximum)
            {
                return Card.TwoDigitMaximum;
            }

            return Card.ThreeDigitMaximum;
        }

        static int DigitCount(int value)
        {
            return value.ToString().Length;
        }

        static GridRow CarryStripRow(int columnCount)
        {
            var cells = new GridCell[columnCount];
            cells[0] = new GridCell(GridCellKind.Blank);

            for (var column = OperatorColumnCount; column < columnCount; column++)
            {
                cells[column] = new GridCell(GridCellKind.CarryBox);
            }

            return new GridRow(GridRowKind.CarryStrip, cells);
        }

        // A printed row: the operand's digits, right-aligned, with blank
        // digit cells filling whatever leading columns the operand doesn't
        // reach — e.g. `331 × 41`'s multiplier row is one blank operator
        // cell, three blank digit cells, then printed `4`, `1`.
        static GridRow PrintedRow(GridRowKind kind, int operand, int columnCount)
        {
            var digits = DigitsOf(operand);
            var digitColumnCount = columnCount - OperatorColumnCount;
            var leadingBlankCount = digitColumnCount - digits.Length;

            var cells = new GridCell[columnCount];
            cells[0] = new GridCell(GridCellKind.Blank);

            for (var i = 0; i < leadingBlankCount; i++)
            {
                cells[OperatorColumnCount + i] = new GridCell(GridCellKind.Blank);
            }

            for (var i = 0; i < digits.Length; i++)
            {
                cells[OperatorColumnCount + leadingBlankCount + i] =
                    new GridCell(GridCellKind.Printed, digits[i]);
            }

            return new GridRow(kind, cells);
        }

        static GridRow EditableRow(GridRowKind kind, int columnCount)
        {
            var cells = new GridCell[columnCount];
            cells[0] = new GridCell(GridCellKind.Blank);

            for (var column = OperatorColumnCount; column < columnCount; column++)
            {
                cells[column] = new GridCell(GridCellKind.Editable);
            }

            return new GridRow(kind, cells);
        }

        // Most-significant digit first, so `DigitsOf(68)` is `[6, 8]`.
        static int[] DigitsOf(int value)
        {
            var text = value.ToString();
            var digits = new int[text.Length];

            for (var i = 0; i < text.Length; i++)
            {
                digits[i] = text[i] - '0';
            }

            return digits;
        }
    }
}
