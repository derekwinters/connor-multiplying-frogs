using System;
using System.Text;

namespace Frogs.Core
{
    /// <summary>
    /// One editable row of the working-out grid — a row of boxes a player
    /// types digits into, and the number that row reads as.
    /// docs/specs/ui/working-out-grid.md#behaviour.
    ///
    /// Every editable row is one of these: the answer row, each addition row,
    /// and each carry strip. Only the answer row's <see cref="ReadLeftToRight"/>
    /// is ever graded, and this type does not know which row it is — grading
    /// is <see cref="Lane.Resolve"/>'s, and nothing here has any idea what a
    /// right answer would be.
    ///
    /// **Why this is in Core.** What a row of boxes reads as, and which box
    /// the next digit goes in, can both be right or wrong with no screen
    /// attached — and being wrong about the second made the first wrong in
    /// front of a player
    /// ([#305](https://github.com/derekwinters/connor-multiplying-frogs/issues/305)).
    /// The shell owns where the boxes are drawn and which row the caret is in;
    /// this type owns the column the caret is in within a row, so the rule
    /// that broke is one a two-second test can hold down.
    ///
    /// Columns are the grid's own, so the shell can index cells and this type
    /// with the same number: column 0 is the operator column on every row and
    /// is never typed into, and the digit columns run from
    /// <see cref="FirstDigitColumn"/> to <see cref="LastDigitColumn"/>.
    /// </summary>
    public sealed class DigitRow
    {
        readonly int?[] _digits;
        readonly int[] _stamps;

        /// <summary>
        /// A row with <paramref name="columnCount"/> columns in total, of
        /// which the ones from <paramref name="firstDigitColumn"/> onwards can
        /// hold a digit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="firstDigitColumn"/> is negative, or the row has no
        /// digit column at all.
        /// </exception>
        public DigitRow(int columnCount, int firstDigitColumn)
        {
            if (firstDigitColumn < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstDigitColumn),
                    firstDigitColumn,
                    "The first digit column cannot be negative.");
            }

            if (columnCount <= firstDigitColumn)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(columnCount),
                    columnCount,
                    "A row needs at least one digit column after the operator column.");
            }

            FirstDigitColumn = firstDigitColumn;
            _digits = new int?[columnCount];
            _stamps = new int[columnCount];
        }

        /// <summary>
        /// The leftmost column a digit can go in, and — since
        /// [#305](https://github.com/derekwinters/connor-multiplying-frogs/issues/305)
        /// — where the caret starts in a row nobody has typed in yet.
        /// </summary>
        public int FirstDigitColumn { get; }

        /// <summary>
        /// The rightmost column a digit can go in. The caret stops here rather
        /// than wrapping to another row or reaching the operator column.
        /// </summary>
        public int LastDigitColumn
        {
            get { return _digits.Length - 1; }
        }

        /// <summary>
        /// Where the caret goes after a digit lands in
        /// <paramref name="column"/>: one cell to the **right**, stopping at
        /// <see cref="LastDigitColumn"/>.
        ///
        /// This is the whole of #305. The grid used to fill right to left,
        /// "which is the direction the digits are worked out in", while
        /// <see cref="ReadLeftToRight"/> read it back the other way — so a
        /// player who typed 3, 4, 0 for 340 submitted 43. One direction now,
        /// in every row kind, and it is the reading one.
        /// </summary>
        public int NextColumnAfterTyping(int column)
        {
            if (column < FirstDigitColumn)
            {
                return FirstDigitColumn;
            }

            return column < LastDigitColumn ? column + 1 : LastDigitColumn;
        }

        /// <summary>Whether <paramref name="column"/> holds a digit.</summary>
        public bool HasDigit(int column)
        {
            return column >= 0 && column < _digits.Length && _digits[column].HasValue;
        }

        /// <summary>
        /// What <paramref name="column"/> reads as — the digit, or the empty
        /// string when the box is empty.
        /// </summary>
        public string TextAt(int column)
        {
            return HasDigit(column) ? _digits[column].Value.ToString() : string.Empty;
        }

        /// <summary>
        /// Puts <paramref name="digit"/> in <paramref name="column"/>,
        /// overwriting whatever was there. <paramref name="stamp"/> orders it
        /// against every other digit in the grid, which is what
        /// <see cref="LastEnteredColumn"/> reads. A column outside the row, or
        /// the operator column, is ignored rather than throwing: a tap that
        /// cannot land is not an error.
        /// </summary>
        public void Write(int column, int digit, int stamp)
        {
            if (column < FirstDigitColumn || column >= _digits.Length)
            {
                return;
            }

            _digits[column] = digit;
            _stamps[column] = stamp;
        }

        /// <summary>Empties <paramref name="column"/>.</summary>
        public void Erase(int column)
        {
            if (column < 0 || column >= _digits.Length)
            {
                return;
            }

            _digits[column] = null;
            _stamps[column] = 0;
        }

        /// <summary>
        /// Empties every column — what `clear` does to the row the caret is
        /// in, and to no other row.
        /// </summary>
        public void EraseAll()
        {
            for (var column = 0; column < _digits.Length; column++)
            {
                _digits[column] = null;
                _stamps[column] = 0;
            }
        }

        /// <summary>Whether the row holds no digits at all.</summary>
        public bool IsEmpty()
        {
            for (var column = 0; column < _digits.Length; column++)
            {
                if (_digits[column].HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The column of the digit typed most recently anywhere in this row,
        /// which is what backspace takes away when the caret's own cell is
        /// already empty, or -1 when the row is empty.
        /// </summary>
        public int LastEnteredColumn()
        {
            var best = -1;

            for (var column = 0; column < _digits.Length; column++)
            {
                if (!_digits[column].HasValue)
                {
                    continue;
                }

                if (best < 0 || _stamps[column] > _stamps[best])
                {
                    best = column;
                }
            }

            return best;
        }

        /// <summary>
        /// The row's digits read left to right, with empty boxes contributing
        /// nothing — the string `Check it` submits when this is the answer
        /// row.
        ///
        /// Empty boxes are skipped rather than filled with zeroes, so `36_`
        /// and `_36` both read `36`. That is deliberate and settled: ADR-0002
        /// checks the answer row's *value*, and lining a short answer up under
        /// the place-value columns is the player's job, not the grid's.
        /// </summary>
        public string ReadLeftToRight()
        {
            var builder = new StringBuilder();

            for (var column = 0; column < _digits.Length; column++)
            {
                if (_digits[column].HasValue)
                {
                    builder.Append(_digits[column].Value);
                }
            }

            return builder.ToString();
        }
    }
}
