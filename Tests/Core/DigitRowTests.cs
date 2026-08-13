using System;
using System.Text;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// One editable row of the working-out grid — issue #305, built to
    /// docs/specs/ui/working-out-grid.md#behaviour.
    ///
    /// The rule this file exists for is the one #305 changed: a digit lands in
    /// the caret's box and the caret steps one box to the **right**, stopping
    /// at the last digit column. It used to step left, while the row was read
    /// back left to right, so a player who typed 340 submitted 43.
    ///
    /// **Nothing here knows a correct answer.** A row reads as a number; what
    /// that number is worth is <see cref="Lane.Resolve"/>'s business.
    /// </summary>
    public sealed class DigitRowTests
    {
        // The grid's own column numbering: column 0 is the operator column on
        // every row and is never typed into. `68 × 5` gets three digit
        // columns and `331 × 41` gets five, plus the operator column in both.
        const int OperatorColumn = 0;
        const int FirstDigitColumn = OperatorColumn + 1;
        const int EasyColumnCount = 4;
        const int HardColumnCount = 6;

        static DigitRow EasyRow()
        {
            return new DigitRow(EasyColumnCount, FirstDigitColumn);
        }

        [Test]
        public void ARowKnowsWhichColumnsCanHoldADigit()
        {
            var row = EasyRow();

            Assert.That(row.FirstDigitColumn, Is.EqualTo(FirstDigitColumn));
            Assert.That(row.LastDigitColumn, Is.EqualTo(EasyColumnCount - 1));
            Assert.That(row.IsEmpty(), Is.True);
            Assert.That(row.ReadLeftToRight(), Is.Empty);
        }

        [Test]
        public void ARowNeedsAtLeastOneDigitColumn()
        {
            Assert.That(() => new DigitRow(1, FirstDigitColumn), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new DigitRow(EasyColumnCount, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AfterADigitLands_TheCaretStepsOneColumnToTheRight()
        {
            // #305, at the level the bug actually lives at. Typed in reading
            // order, the digits occupy the columns in reading order.
            var row = EasyRow();
            var column = row.FirstDigitColumn;

            column = Land(row, column, 3, 0);
            Assert.That(column, Is.EqualTo(FirstDigitColumn + 1), "the box to the right of the one just filled");

            column = Land(row, column, 4, 1);
            Assert.That(column, Is.EqualTo(FirstDigitColumn + 2));

            Land(row, column, 0, 2);

            Assert.That(row.TextAt(FirstDigitColumn), Is.EqualTo("3"));
            Assert.That(row.TextAt(FirstDigitColumn + 1), Is.EqualTo("4"));
            Assert.That(row.TextAt(FirstDigitColumn + 2), Is.EqualTo("0"));
            Assert.That(row.ReadLeftToRight(), Is.EqualTo("340"), "the number that was typed, not its reverse");
        }

        [Test]
        public void TheCaretStopsAtTheLastDigitColumn_AndFurtherDigitsOverwriteIt()
        {
            // The mirror of the bound the leftward caret held at the first
            // digit column: typing more digits than the grid is wide must not
            // wrap, escape the row, or reach the operator column.
            var row = EasyRow();

            Assert.That(row.NextColumnAfterTyping(row.LastDigitColumn), Is.EqualTo(row.LastDigitColumn));

            var column = row.FirstDigitColumn;

            foreach (var digit in new[] { 3, 4, 0, 7, 9 })
            {
                column = Land(row, column, digit, 0);
            }

            Assert.That(column, Is.EqualTo(row.LastDigitColumn));
            Assert.That(row.ReadLeftToRight(), Is.EqualTo("349"), "the last box took each new digit in turn");
        }

        [Test]
        public void TheCaretNeverStepsIntoTheOperatorColumn_AndNoDigitCanBeWrittenThere()
        {
            var row = EasyRow();

            Assert.That(row.NextColumnAfterTyping(OperatorColumn), Is.EqualTo(FirstDigitColumn));
            Assert.That(row.NextColumnAfterTyping(-5), Is.EqualTo(FirstDigitColumn));

            row.Write(OperatorColumn, 9, 0);
            row.Write(EasyColumnCount, 9, 1);

            Assert.That(row.IsEmpty(), Is.True, "the operator column is not a box a digit can go in");
            Assert.That(row.ReadLeftToRight(), Is.Empty);
        }

        [Test]
        public void AShortAnswerStaysInTheColumnsItWasTypedInto_AndStillReadsAsItsValue()
        {
            // Boxes are slots, and a slot is something you point at. The row
            // does not shuffle a two-digit answer flush right, and grading
            // does not care that it did not: ADR-0002 checks the answer row's
            // value only.
            var typedInTheMiddle = EasyRow();
            var column = FirstDigitColumn + 1;
            column = Land(typedInTheMiddle, column, 3, 0);
            Land(typedInTheMiddle, column, 6, 1);

            Assert.That(typedInTheMiddle.TextAt(FirstDigitColumn), Is.Empty);
            Assert.That(typedInTheMiddle.ReadLeftToRight(), Is.EqualTo("36"));

            var typedFlushLeft = EasyRow();
            var left = typedFlushLeft.FirstDigitColumn;
            left = Land(typedFlushLeft, left, 3, 0);
            Land(typedFlushLeft, left, 6, 1);

            Assert.That(typedFlushLeft.TextAt(FirstDigitColumn), Is.EqualTo("3"));
            Assert.That(
                typedFlushLeft.ReadLeftToRight(),
                Is.EqualTo(typedInTheMiddle.ReadLeftToRight()),
                "`36_` and `_36` are the same answer");
        }

        [Test]
        public void TheWidestCardFillsLeftToRightToo_BecauseThereIsOneDirectionNotTwo()
        {
            var row = new DigitRow(HardColumnCount, FirstDigitColumn);
            var column = row.FirstDigitColumn;

            foreach (var digit in new[] { 1, 3, 5, 7, 1 })
            {
                column = Land(row, column, digit, 0);
            }

            Assert.That(row.ReadLeftToRight(), Is.EqualTo("13571"));
            Assert.That(column, Is.EqualTo(HardColumnCount - 1), "come to rest on the last digit column");
        }

        [Test]
        public void BackspaceTakesTheCaretsOwnDigit_OrTheRowsMostRecentOne()
        {
            var row = EasyRow();
            var column = row.FirstDigitColumn;

            column = Land(row, column, 2, 0);
            Land(row, column, 8, 1);

            Assert.That(row.ReadLeftToRight(), Is.EqualTo("28"));

            // The caret parked on an empty box: the most recently typed digit
            // anywhere in the row goes, wherever it is.
            var empty = row.LastDigitColumn;
            Assert.That(row.HasDigit(empty), Is.False);
            Assert.That(row.LastEnteredColumn(), Is.EqualTo(FirstDigitColumn + 1), "the 8, typed second");

            row.Erase(row.LastEnteredColumn());
            Assert.That(row.ReadLeftToRight(), Is.EqualTo("2"));

            // And with a digit under the caret, that one goes instead.
            Assert.That(row.HasDigit(FirstDigitColumn), Is.True);
            row.Erase(FirstDigitColumn);

            Assert.That(row.IsEmpty(), Is.True);
            Assert.That(row.LastEnteredColumn(), Is.EqualTo(-1), "nothing left to take");
        }

        [Test]
        public void RecencyIsGridWide_SoTheOrderDigitsWereTypedInSurvivesAJumpBetweenRows()
        {
            // The stamps come from one counter shared across the whole grid,
            // so "the digit most recently typed in this row" stays right after
            // the player has been typing in another one.
            var row = EasyRow();

            row.Write(FirstDigitColumn, 1, 10);
            row.Write(FirstDigitColumn + 2, 2, 4);

            Assert.That(row.LastEnteredColumn(), Is.EqualTo(FirstDigitColumn), "the higher stamp, not the righter box");
        }

        [Test]
        public void ClearEmptiesTheWholeRow()
        {
            var row = EasyRow();
            var column = row.FirstDigitColumn;

            foreach (var digit in new[] { 1, 2, 3 })
            {
                column = Land(row, column, digit, 0);
            }

            Assert.That(row.ReadLeftToRight(), Is.EqualTo("123"));

            row.EraseAll();

            Assert.That(row.IsEmpty(), Is.True);
            Assert.That(row.ReadLeftToRight(), Is.Empty);
            Assert.That(row.LastEnteredColumn(), Is.EqualTo(-1));
        }

        [Test]
        public void ARowKnowsNothingAboutWhatWouldBeCorrect()
        {
            // The same guard the grid view carries: nothing in the working-out
            // is marked, and a type with no way to grade cannot start.
            var names = new StringBuilder();

            foreach (var member in typeof(DigitRow).GetMembers())
            {
                names.Append(member.Name).Append(' ');
            }

            foreach (var word in new[] { "Correct", "Wrong", "Grade", "Verdict", "Score", "Marked", "Valid", "Product" })
            {
                Assert.That(names.ToString(), Does.Not.Contain(word), word + " has no business in a row of scratch boxes");
            }
        }

        // Types one digit the way the shell does: write it where the caret is,
        // then move the caret on. Returns where the caret ends up.
        static int Land(DigitRow row, int column, int digit, int stamp)
        {
            row.Write(column, digit, stamp);
            return row.NextColumnAfterTyping(column);
        }
    }
}
