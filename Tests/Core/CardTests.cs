using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Card"/> is a multiplication problem shaped to a pile, plus
    /// its true product — generated from the game's seeded <see cref="Rng"/>
    /// (#200) and from nothing else. docs/adr/0002-structured-working-out-grid.md
    /// pins the three shapes; docs/specs/rules.md — "a card is one
    /// multiplication problem, and the player answers it... equals the
    /// product on the card". Only the digit-count bounds are asserted here —
    /// per #203, nothing about which values within those bounds come up, and
    /// nothing about whether a single-digit multiplier may be zero.
    /// </summary>
    public sealed class CardTests
    {
        const ulong Seed = 24680UL;

        // Enough draws that a single lucky (or unlucky) call could not have
        // produced the shape tests' pass by chance — this is the test that
        // would catch an off-by-one at one end of a range that a single draw
        // missed.
        const int DrawCount = 200;

        [Test]
        public void Draw_ForTheEasyPile_ProducesA2DigitBy1DigitShape()
        {
            var rng = Rng.FromSeed(Seed);

            var card = Card.Draw(Pile.Easy, rng);

            Assert.That(card.Multiplicand, Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum));

            // "Below 10" only — deliberately not a lower-bound check. Whether
            // a single-digit multiplier can be 0 is an open question #203
            // does not settle; see Card.OneDigitMaximum.
            Assert.That(card.Multiplier, Is.LessThanOrEqualTo(Card.OneDigitMaximum));
        }

        [Test]
        public void Draw_ForTheMediumPile_ProducesA2DigitBy2DigitShape()
        {
            var rng = Rng.FromSeed(Seed);

            var card = Card.Draw(Pile.Medium, rng);

            Assert.That(card.Multiplicand, Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum));
            Assert.That(card.Multiplier, Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum));
        }

        [Test]
        public void Draw_ForTheHardPile_ProducesA3DigitBy2DigitShape()
        {
            var rng = Rng.FromSeed(Seed);

            var card = Card.Draw(Pile.Hard, rng);

            Assert.That(card.Multiplicand, Is.InRange(Card.ThreeDigitMinimum, Card.ThreeDigitMaximum));
            Assert.That(card.Multiplier, Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum));
        }

        // Nothing beyond the digit bounds is asserted here — no claim about
        // which values within a bound appear, or how often (#203: "do not
        // write a test that asserts a distribution").
        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void Draw_AcrossRepeatedGeneration_EveryOperandStaysWithinThePilesDigitBounds(Pile pile)
        {
            var rng = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                var card = Card.Draw(pile, rng);

                AssertOperandsAreWithinDigitBounds(pile, card, draw);
            }
        }

        static void AssertOperandsAreWithinDigitBounds(Pile pile, Card card, int draw)
        {
            switch (pile)
            {
                case Pile.Easy:
                    Assert.That(card.Multiplicand,
                        Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum), $"draw {draw}");
                    Assert.That(card.Multiplier,
                        Is.LessThanOrEqualTo(Card.OneDigitMaximum), $"draw {draw}");
                    break;

                case Pile.Medium:
                    Assert.That(card.Multiplicand,
                        Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum), $"draw {draw}");
                    Assert.That(card.Multiplier,
                        Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum), $"draw {draw}");
                    break;

                case Pile.Hard:
                    Assert.That(card.Multiplicand,
                        Is.InRange(Card.ThreeDigitMinimum, Card.ThreeDigitMaximum), $"draw {draw}");
                    Assert.That(card.Multiplier,
                        Is.InRange(Card.TwoDigitMinimum, Card.TwoDigitMaximum), $"draw {draw}");
                    break;
            }
        }

        // ADR-0002: only the answer is graded — nothing about how a player
        // works the problem out. docs/specs/rules.md — "the answer... equals
        // the product on the card". So the card's Product has to be exactly
        // its operands' true product, not a value drawn independently of them.
        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void Draw_TheAnswerIsTheTrueProductOfItsOperands(Pile pile)
        {
            var rng = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                var card = Card.Draw(pile, rng);

                Assert.That(card.Product, Is.EqualTo(card.Multiplicand * card.Multiplier), $"draw {draw}");
            }
        }

        // Determinism is what makes a whole game replayable move for move in
        // the two-second suite (#203), and what a save has to preserve —
        // docs/adr/0004-core-owns-the-save-format.md.
        [TestCase(Pile.Easy)]
        [TestCase(Pile.Medium)]
        [TestCase(Pile.Hard)]
        public void Draw_FromRngsOfTheSameSeed_ProducesTheIdenticalSequenceOfCards(Pile pile)
        {
            var first = Rng.FromSeed(Seed);
            var second = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                var expected = Card.Draw(pile, first);
                var actual = Card.Draw(pile, second);

                Assert.That(actual.Multiplicand, Is.EqualTo(expected.Multiplicand), $"draw {draw}");
                Assert.That(actual.Multiplier, Is.EqualTo(expected.Multiplier), $"draw {draw}");
            }
        }
    }
}
