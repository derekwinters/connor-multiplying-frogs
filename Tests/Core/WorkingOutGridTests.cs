using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="WorkingOutGrid"/> reports the working-out grid's shape for a
    /// card and a current addition-row count — see
    /// docs/specs/ui/working-out-grid.md#how-many-columns-and-rows. Since
    /// #234 the addition section is growable on every card, so the row *kind*
    /// list is identical for every shape; only the column count and the
    /// number of addition rows vary.
    ///
    /// <see cref="Card"/> only builds through <see cref="Card.Draw"/>, so
    /// these tests draw cards from a fixed-seed <see cref="Rng"/> (matching
    /// <c>CardTests</c>' own pattern) rather than asserting against literal
    /// operand values. Every assertion about printed digits is checked
    /// against the drawn card's own <see cref="Card.Multiplicand"/> and
    /// <see cref="Card.Multiplier"/>, not a hard-coded `68`/`5` pair.
    /// </summary>
    public sealed class WorkingOutGridTests
    {
        const ulong Seed = 13579UL;

        // Enough draws that a test relying on seeing two different answer
        // digit counts (see the "grid never looks at the product" test below)
        // could not pass by a single lucky draw.
        const int DrawCount = 200;

        [Test]
        public void For_ACardAtTheStartingRowCount_ReportsCarryMultiplicandMultiplierAdditionCarryAnswer()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Easy, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);

            Assert.That(RowKindsOf(grid), Is.EqualTo(new[]
            {
                GridRowKind.CarryStrip,
                GridRowKind.Multiplicand,
                GridRowKind.Multiplier,
                GridRowKind.AdditionRow,
                GridRowKind.AdditionRow,
                GridRowKind.CarryStrip,
                GridRowKind.AnswerRow,
            }));
        }

        // The row *kind* list no longer depends on the multiplier's digit
        // count (#234) — every shape gets the same list, at whatever addition
        // row count it currently holds. Only the column count is
        // shape-specific.
        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void For_EveryShapeAtTheStartingRowCount_ReportsTheSameRowKindList(Pile pile)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(pile, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);

            Assert.That(RowKindsOf(grid), Is.EqualTo(new[]
            {
                GridRowKind.CarryStrip,
                GridRowKind.Multiplicand,
                GridRowKind.Multiplier,
                GridRowKind.AdditionRow,
                GridRowKind.AdditionRow,
                GridRowKind.CarryStrip,
                GridRowKind.AnswerRow,
            }), $"pile {pile}");
        }

        [TestCase(Pile.Easy, 4)]
        [TestCase(Pile.Medium, 5)]
        [TestCase(Pile.Hard, 6)]
        public void For_EachShape_ReportsTheColumnCountTheSpecPageStates(Pile pile, int expectedColumnCount)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(pile, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);

            Assert.That(grid.ColumnCount, Is.EqualTo(expectedColumnCount));
        }

        // The test the issue calls out explicitly: two Easy-shaped cards
        // whose actual products have different digit counts must still
        // report an identical grid, because the grid is sized to the shape's
        // largest possible product, never the card's actual answer. Rather
        // than hard-code an operand pair (Card has no constructor that
        // accepts one), this draws until the fixed seed has produced two
        // Easy cards with differing answer digit counts, which it does well
        // within DrawCount — and asserts the diversity actually happened, so
        // the test cannot pass vacuously.
        [Test]
        public void For_TwoCardsOfTheSameShapeWithDifferentAnswerDigitCounts_ReportIdenticalGrids()
        {
            var rng = Rng.FromSeed(Seed);
            var columnCounts = new HashSet<int>();
            var answerDigitCounts = new HashSet<int>();
            var rowKindLists = new HashSet<string>();

            for (var draw = 0; draw < DrawCount; draw++)
            {
                var card = Card.Draw(Pile.Easy, rng);
                var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);

                columnCounts.Add(grid.ColumnCount);
                answerDigitCounts.Add(card.Product.ToString().Length);
                rowKindLists.Add(string.Join(",", RowKindsOf(grid)));
            }

            Assert.That(answerDigitCounts.Count, Is.GreaterThan(1),
                "the draws never produced two different answer digit counts — this test would pass vacuously.");
            Assert.That(columnCounts, Has.Count.EqualTo(1),
                "the column count must be identical across every draw of the same shape.");
            Assert.That(rowKindLists, Has.Count.EqualTo(1),
                "the row kind list must be identical across every draw of the same shape.");
        }

        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void For_ThePrintedMultiplicandRow_IsBlankOperatorThenBlankLeadingCellsThenTheOperandsDigits(Pile pile)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(pile, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
            var row = grid.Rows.Single(r => r.Kind == GridRowKind.Multiplicand);

            AssertIsAPrintedRow(row, card.Multiplicand, grid.ColumnCount);
        }

        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void For_ThePrintedMultiplierRow_IsBlankOperatorThenBlankLeadingCellsThenTheOperandsDigits(Pile pile)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(pile, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
            var row = grid.Rows.Single(r => r.Kind == GridRowKind.Multiplier);

            AssertIsAPrintedRow(row, card.Multiplier, grid.ColumnCount);
        }

        [Test]
        public void For_EveryCarryStrip_HasACarryBoxOverEveryDigitColumnAndNoneOverTheOperatorColumn()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
            var strips = grid.Rows.Where(r => r.Kind == GridRowKind.CarryStrip).ToList();

            Assert.That(strips, Has.Count.EqualTo(WorkingOutGrid.CarryStripCount));

            foreach (var strip in strips)
            {
                Assert.That(strip.Cells[0].Kind, Is.EqualTo(GridCellKind.Blank));

                for (var column = 1; column < grid.ColumnCount; column++)
                {
                    Assert.That(strip.Cells[column].Kind, Is.EqualTo(GridCellKind.CarryBox), $"column {column}");
                }
            }
        }

        // Settled by Derek on #255: a shared strip, not one per addition row.
        // The carry strip count must stay fixed at two no matter how many
        // addition rows the section currently holds.
        [TestCase(WorkingOutGrid.GridAdditionRowsAtStart)]
        [TestCase(WorkingOutGrid.GridAdditionRowsMax)]
        public void For_AnyAdditionRowCount_TheCarryStripCountStaysFixedAtTwo(int additionRowCount)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            var grid = WorkingOutGrid.For(card, additionRowCount);

            Assert.That(grid.Rows.Count(r => r.Kind == GridRowKind.CarryStrip),
                Is.EqualTo(WorkingOutGrid.CarryStripCount));
        }

        [Test]
        public void For_EveryAdditionRow_ReportsEveryDigitColumnAsEditableIncludingTheUnitsColumn()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
            var additionRows = grid.Rows.Where(r => r.Kind == GridRowKind.AdditionRow).ToList();

            Assert.That(additionRows, Has.Count.EqualTo(WorkingOutGrid.GridAdditionRowsAtStart));

            foreach (var row in additionRows)
            {
                Assert.That(row.Cells[0].Kind, Is.EqualTo(GridCellKind.Blank));

                for (var column = 1; column < grid.ColumnCount; column++)
                {
                    Assert.That(row.Cells[column].Kind, Is.EqualTo(GridCellKind.Editable), $"column {column}");
                    Assert.That(row.Cells[column].Digit, Is.Null, "no placeholder digit is pre-printed");
                }
            }
        }

        [Test]
        public void For_TheAnswerRow_IsEditableInEveryDigitColumn()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            var grid = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
            var answerRow = grid.Rows.Single(r => r.Kind == GridRowKind.AnswerRow);

            Assert.That(answerRow.Cells[0].Kind, Is.EqualTo(GridCellKind.Blank));

            for (var column = 1; column < grid.ColumnCount; column++)
            {
                Assert.That(answerRow.Cells[column].Kind, Is.EqualTo(GridCellKind.Editable), $"column {column}");
            }
        }

        // The addition section starts at GridAdditionRowsAtStart and can grow
        // to GridAdditionRowsMax — docs/specs/ui/working-out-grid.md's row
        // table — and a snapshot at every count in between must report
        // exactly that many AdditionRow rows.
        [TestCase(WorkingOutGrid.GridAdditionRowsAtStart)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(WorkingOutGrid.GridAdditionRowsMax)]
        public void For_AnAdditionRowCountWithinBounds_ReportsExactlyThatManyAdditionRows(int additionRowCount)
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            var grid = WorkingOutGrid.For(card, additionRowCount);

            Assert.That(grid.Rows.Count(r => r.Kind == GridRowKind.AdditionRow), Is.EqualTo(additionRowCount));
        }

        // Open question 5, settled by Derek on #204: a grown row can be
        // removed again — backspacing the last digit out of it takes it away.
        // That is exactly a lower addition-row-count snapshot, which this
        // type already reports correctly; there is no separate "remove a
        // row" operation for Core to own, only the count going down instead
        // of up. GridAdditionRowsAtStart is the floor: every card is dealt at
        // least that many, so a snapshot can never legitimately ask for
        // fewer.
        [Test]
        public void For_ARowCountBelowTheStartingCount_IsRejected()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            Assert.That(
                () => WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart - 1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void For_ARowCountAboveTheCap_IsRejected()
        {
            var rng = Rng.FromSeed(Seed);
            var card = Card.Draw(Pile.Hard, rng);

            Assert.That(
                () => WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsMax + 1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void For_ANullCard_IsRejected()
        {
            Assert.That(
                () => WorkingOutGrid.For(null, WorkingOutGrid.GridAdditionRowsAtStart),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        static void AssertIsAPrintedRow(GridRow row, int operand, int columnCount)
        {
            var digits = operand.ToString();
            var digitColumnCount = columnCount - 1;
            var leadingBlankCount = digitColumnCount - digits.Length;

            Assert.That(row.Cells[0].Kind, Is.EqualTo(GridCellKind.Blank), "operator column");

            for (var i = 0; i < leadingBlankCount; i++)
            {
                Assert.That(row.Cells[1 + i].Kind, Is.EqualTo(GridCellKind.Blank), $"leading column {i}");
            }

            for (var i = 0; i < digits.Length; i++)
            {
                var cell = row.Cells[1 + leadingBlankCount + i];
                Assert.That(cell.Kind, Is.EqualTo(GridCellKind.Printed), $"digit column {i}");
                Assert.That(cell.Digit, Is.EqualTo(digits[i] - '0'), $"digit column {i}");
            }
        }

        static IEnumerable<GridRowKind> RowKindsOf(WorkingOutGrid grid)
        {
            return grid.Rows.Select(r => r.Kind);
        }
    }
}
