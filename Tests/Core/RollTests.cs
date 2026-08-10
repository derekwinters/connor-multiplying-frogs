using System;
using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// The roll that opens a turn: one face of a six-sided die, drawn from the
    /// game's seeded <see cref="Rng"/> (#200) and from nothing else, and the
    /// pile that face maps to. docs/specs/rules.md — "the roll selects the
    /// pile and does nothing else... it never moves a frog." Every number here
    /// is a named constant, the same convention RngTests uses.
    /// </summary>
    public sealed class RollTests
    {
        const ulong Seed = 12345UL;
        const ulong OtherSeed = 12346UL;
        const int DrawCount = 50;

        // Unlike a 32-bit NextUInt() draw, a face is one of only six values, so
        // two independent sequences agree by chance about a sixth of the time —
        // over 50 draws that is roughly eight agreements, not zero. The bar
        // here is well below that expectation, so it is divergence being
        // asserted, not the absence of every coincidental match.
        const int MinDifferingDraws = 30;

        // Rolling drives the seeded RNG from #200 rather than a fresh
        // System.Random: two rolls built from separately-constructed
        // generators of the same seed have to produce the identical sequence
        // of faces, over and over, for that to be true.
        [Test]
        public void RollsFromRngsOfTheSameSeed_ProduceTheIdenticalSequence()
        {
            var first = Rng.FromSeed(Seed);
            var second = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                Assert.That(
                    Roll.Draw(second).Face,
                    Is.EqualTo(Roll.Draw(first).Face),
                    $"the sequences parted company at draw {draw}");
            }
        }

        // The other half of the same claim: generators seeded differently have
        // to diverge, or the roll could be drawing from something that ignores
        // the seed altogether — a fresh System.Random would happen to pass the
        // same-seed test above (its own state still repeats within one run)
        // but never diverge in the way an unseeded source would either. Faces
        // are drawn from a 6-wide range, so two independent sequences agreeing
        // by chance on most of 50 draws is not a real risk.
        [Test]
        public void RollsFromRngsOfDifferentSeeds_Diverge()
        {
            var first = Rng.FromSeed(Seed);
            var second = Rng.FromSeed(OtherSeed);

            var differing = 0;

            for (var draw = 0; draw < DrawCount; draw++)
            {
                if (Roll.Draw(first).Face != Roll.Draw(second).Face)
                {
                    differing++;
                }
            }

            Assert.That(differing, Is.GreaterThanOrEqualTo(MinDifferingDraws));
        }

        // The bounds a face can come up in, asserted against the named
        // constants rather than bare literals — so the next reader of a face
        // comparison knows 1 and 6 are the die's ends, not two arbitrary
        // numbers.
        [Test]
        public void OverAFixedSeedSequence_EveryFaceIsWithinTheDiesRange()
        {
            var rng = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                var face = Roll.Draw(rng).Face;

                Assert.That(face, Is.InRange(Roll.MinimumFace, Roll.MaximumFace));
            }
        }

        // The face-to-pile table, read off the pile labels in the board
        // photograph and repeated in ADR-0002 and docs/specs/ui/roll-and-card.md:
        // 1 or 2 -> Easy, 3 or 4 -> Medium, 5 or 6 -> Hard. Asserted face by
        // face so a single row moving is visible in the diff.
        [TestCase(1, Pile.Easy)]
        [TestCase(2, Pile.Easy)]
        [TestCase(3, Pile.Medium)]
        [TestCase(4, Pile.Medium)]
        [TestCase(5, Pile.Hard)]
        [TestCase(6, Pile.Hard)]
        public void PileForFace_MapsEachFaceToItsPile(int face, Pile expected)
        {
            Assert.That(Roll.PileForFace(face), Is.EqualTo(expected));
        }

        // Two faces per pile is a counting fact over all six faces, not a
        // statistical one — so it is asserted by grouping the enumerated
        // results, not by sampling a die roll.
        [Test]
        public void EveryPile_IsReachedByExactlyTwoFaces()
        {
            var facesByPile = Enumerable
                .Range(Roll.MinimumFace, Roll.MaximumFace - Roll.MinimumFace + 1)
                .GroupBy(Roll.PileForFace);

            foreach (var group in facesByPile)
            {
                Assert.That(group.Count(), Is.EqualTo(2), $"{group.Key} pile");
            }

            Assert.That(facesByPile.Select(group => group.Key), Is.EquivalentTo(
                new[] { Pile.Easy, Pile.Medium, Pile.Hard }));
        }

        // The mapping cannot occur with a face outside 1-6 when the face comes
        // from the die, but the function still has to say what it does with
        // one rather than silently returning a pile for it.
        [TestCase(0)]
        [TestCase(7)]
        [TestCase(-1)]
        public void PileForFace_RejectsAFaceOutsideTheDiesRange(int face)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => Roll.PileForFace(face));

            Assert.That(exception.Message, Does.Contain(face.ToString()));
        }
    }
}
