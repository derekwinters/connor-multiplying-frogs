using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="DigitProducts"/> reports the place-value-expanded products a
    /// card is made of — what `Help me` prints, and
    /// docs/specs/ui/working-out-grid.md#what-core-owns-the-product-list.
    ///
    /// These tests state the specs' own worked examples as literal cards,
    /// built through <see cref="Card.Of"/> rather than drawn from a seed: the
    /// point of `12 x 34` is that it is `12 x 34`, and a test that hunted for
    /// a seed drawing it would be a test about <see cref="Rng"/>.
    /// </summary>
    public sealed class DigitProductsTests
    {
        [Test]
        public void For_TwelveTimesThirtyFour_ReportsEveryMultiplierPartUnitsFirst_AndWithinEachEveryMultiplicandPart()
        {
            var card = Card.Of(12, 34);

            var products = DigitProducts.For(card);

            Assert.That(products, Is.EqualTo(new[]
            {
                new DigitProduct(4, 2),
                new DigitProduct(4, 10),
                new DigitProduct(30, 2),
                new DigitProduct(30, 10),
            }));
        }

        // The widest shape ADR-0002 allows, and the one the `Help me` mockup
        // at the cap is drawn from: three multiplicand parts by two multiplier
        // parts is six products, which is exactly `GridAdditionRowsMax`.
        [Test]
        public void For_ThreeHundredAndThirtyOneTimesFortyOne_ReportsSixProductsInThatSameOrder()
        {
            var card = Card.Of(331, 41);

            var products = DigitProducts.For(card);

            Assert.That(products, Is.EqualTo(new[]
            {
                new DigitProduct(1, 1),
                new DigitProduct(1, 30),
                new DigitProduct(1, 300),
                new DigitProduct(40, 1),
                new DigitProduct(40, 30),
                new DigitProduct(40, 300),
            }));
        }

        // A one-digit multiplier has exactly one part, so the whole list is
        // that part against each part of the multiplicand — and the tens
        // digit is still expanded: `6` is the part `60`.
        [Test]
        public void For_SixtyEightTimesFive_ReportsTheSingleMultiplierPartAgainstEachMultiplicandPart()
        {
            var card = Card.Of(68, 5);

            var products = DigitProducts.For(card);

            Assert.That(products, Is.EqualTo(new[]
            {
                new DigitProduct(5, 8),
                new DigitProduct(5, 60),
            }));
        }

        // Open question 11: a zero part is a row that adds nothing and a line
        // of writing that teaches nothing, so it is skipped rather than
        // printed. `102 x 40` expands to parts 2, 0, 100 and 0, 40 — six
        // combinations, of which four involve a zero.
        [Test]
        public void For_ACardWithZeroDigits_SkipsEveryProductAZeroPartWouldMake()
        {
            var card = Card.Of(102, 40);

            var products = DigitProducts.For(card);

            Assert.That(products, Is.EqualTo(new[]
            {
                new DigitProduct(40, 2),
                new DigitProduct(40, 100),
            }));
        }

        // The bound the working-out grid depends on: `Help me` grows the
        // addition section to one row per product, so a card that made more
        // products than `GridAdditionRowsMax` would be a card the grid cannot
        // hold. Asserted over *every* card each pile can deal — 90,000 of
        // them, which the fast suite gets through in well under a second —
        // rather than over one example, because the interesting card is the
        // one nobody thought to write down.
        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void For_EveryCardTheShapeCanDeal_ReportsNoMoreProductsThanTheAdditionSectionCanHold(Pile pile)
        {
            foreach (var card in EveryCardOfTheShapeFor(pile))
            {
                var products = DigitProducts.For(card);

                Assert.That(
                    products.Count,
                    Is.LessThanOrEqualTo(WorkingOutGrid.GridAdditionRowsMax),
                    $"{card.Multiplicand} x {card.Multiplier}");
            }
        }

        // A pure function: no state, no Rng, nothing to reset. `Help me` is
        // pressed in the middle of a turn and the shell is free to ask again
        // while drawing, so a second call has to answer the first one's
        // question the same way.
        [Test]
        public void For_TheSameCardTwice_ReportsTheSameProductsBothTimes()
        {
            var card = Card.Of(331, 41);

            var first = DigitProducts.For(card);
            var second = DigitProducts.For(card);

            Assert.That(second, Is.EqualTo(first));
        }

        // The same guard WorkingOutGrid.For has, for the same reason: a null
        // card is a caller's bug, and it should say so where it happened
        // rather than further down inside a loop.
        [Test]
        public void For_ANullCard_Throws()
        {
            Assert.That(() => DigitProducts.For(null), Throws.TypeOf<ArgumentNullException>());
        }

        // Every card of a pile's shape, from Card's own named digit bounds.
        // The shapes are ADR-0002's three, the same table Card.Draw switches
        // on; CardTests is what holds Draw to them, so this walks the shape
        // rather than the generator.
        static IEnumerable<Card> EveryCardOfTheShapeFor(Pile pile)
        {
            int multiplicandMinimum;
            int multiplicandMaximum;
            int multiplierMinimum;
            int multiplierMaximum;

            switch (pile)
            {
                case Pile.Easy:
                    multiplicandMinimum = Card.TwoDigitMinimum;
                    multiplicandMaximum = Card.TwoDigitMaximum;
                    multiplierMinimum = Card.OneDigitMinimum;
                    multiplierMaximum = Card.OneDigitMaximum;
                    break;

                case Pile.Medium:
                    multiplicandMinimum = Card.TwoDigitMinimum;
                    multiplicandMaximum = Card.TwoDigitMaximum;
                    multiplierMinimum = Card.TwoDigitMinimum;
                    multiplierMaximum = Card.TwoDigitMaximum;
                    break;

                case Pile.Hard:
                    multiplicandMinimum = Card.ThreeDigitMinimum;
                    multiplicandMaximum = Card.ThreeDigitMaximum;
                    multiplierMinimum = Card.TwoDigitMinimum;
                    multiplierMaximum = Card.TwoDigitMaximum;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(pile), pile, "no shape for this pile.");
            }

            for (var multiplicand = multiplicandMinimum; multiplicand <= multiplicandMaximum; multiplicand++)
            {
                for (var multiplier = multiplierMinimum; multiplier <= multiplierMaximum; multiplier++)
                {
                    yield return Card.Of(multiplicand, multiplier);
                }
            }
        }
    }
}
