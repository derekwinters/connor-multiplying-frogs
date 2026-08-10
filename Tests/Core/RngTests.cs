using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// Every number here is a named constant, and every one of them comes from
    /// issue #200. The generator is deterministic given a seed, so no test on
    /// this page has any business varying between runs or between machines:
    /// nothing here reads a clock, a Guid, or any other entropy.
    /// </summary>
    public sealed class RngTests
    {
        const ulong Seed = 12345UL;
        const int DrawCount = 50;
        const ulong SeedA = 12345UL;
        const ulong SeedB = 12346UL;
        const int MinDifferingDraws = 40;
        const int DrawsBeforeSnapshot = 10;
        const int DrawsAfterSnapshot = 20;
        const int RangeMin = 5;
        const int RangeMax = 17;
        const int BoundedDrawCount = 10000;
        const int BackwardsRangeTimeoutMilliseconds = 5000;
        const ulong UniformitySeed = 12345UL;
        const int SampleSize = 120000;
        const int BucketCount = 12;
        const int TolerancePercent = 5;

        // The uniformity range is enormous on purpose, and it is the only
        // number on this page that is not the one issue #200 named.
        //
        // What can skew a bounded draw is the leftover when the generator's
        // 2^32 outputs are folded into a range that does not divide it: the
        // first few values of the range get one more output than the rest. Over
        // a die-sized range that leftover is 12 values in 4.3 billion, and no
        // sample size distinguishes it from noise — the test came out green
        // against the biased implementation, which by the issue's own rule
        // means it was testing nothing.
        //
        // Spanning three quarters of the generator's output makes the same flaw
        // enormous instead: the leftover is a third of the range, so the bottom
        // four buckets get drawn twice as often as the other eight. Same defect,
        // same fix, in a size a 120,000-draw sample can see.
        const int BiasRangeMin = -1073741824;
        const int BiasRangeMax = 2147483647;
        const long BucketWidth = 268435456L;

        [Test]
        public void TwoGeneratorsWithTheSameSeed_ProduceTheSameSequence()
        {
            var first = Rng.FromSeed(Seed);
            var second = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawCount; draw++)
            {
                Assert.That(
                    second.NextUInt(),
                    Is.EqualTo(first.NextUInt()),
                    $"the sequences parted company at draw {draw}");
            }
        }

        // Two games started a moment apart get adjacent seeds, so seeds one
        // apart are the ones that have to diverge — not seeds picked far apart
        // to flatter the generator.
        [Test]
        public void TwoGeneratorsWithDifferentSeeds_ProduceDivergingSequences()
        {
            var first = Rng.FromSeed(SeedA);
            var second = Rng.FromSeed(SeedB);

            var differing = 0;

            for (var draw = 0; draw < DrawCount; draw++)
            {
                if (first.NextUInt() != second.NextUInt())
                {
                    differing++;
                }
            }

            Assert.That(differing, Is.GreaterThanOrEqualTo(MinDifferingDraws));
        }

        // A game restored from a save has to carry on, not start the sequence
        // over: rebuilding from the seed alone replays rolls the players
        // already played, which is re-randomising while holding a seed.
        [Test]
        public void AGeneratorRebuiltFromAReportedState_ResumesTheSequence()
        {
            var original = Rng.FromSeed(Seed);

            for (var draw = 0; draw < DrawsBeforeSnapshot; draw++)
            {
                original.NextUInt();
            }

            var resumed = Rng.FromState(original.State);

            for (var draw = 0; draw < DrawsAfterSnapshot; draw++)
            {
                Assert.That(
                    resumed.NextUInt(),
                    Is.EqualTo(original.NextUInt()),
                    $"the resumed sequence parted company at draw {draw}");
            }
        }

        // Both ends of the range have to come up, which is what says the upper
        // bound is inclusive — so the next issue to draw a die face or a
        // multiplicand does not have to guess whether 17 is reachable.
        [Test]
        public void ABoundedDraw_StaysInRangeAndReachesBothEnds()
        {
            var rng = Rng.FromSeed(Seed);

            var sawMin = false;
            var sawMax = false;

            for (var draw = 0; draw < BoundedDrawCount; draw++)
            {
                var value = rng.NextInt(RangeMin, RangeMax);

                Assert.That(value, Is.InRange(RangeMin, RangeMax));

                sawMin |= value == RangeMin;
                sawMax |= value == RangeMax;
            }

            Assert.That(sawMin, Is.True, $"{RangeMin} never came up");
            Assert.That(sawMax, Is.True, $"{RangeMax} never came up");
        }

        // A range handed over backwards is a caller's bug, and without this it
        // is a spectacular one: the span underflows to something larger than
        // the generator can draw, every draw is rejected, and the game hangs
        // inside the roll instead of throwing.
        [Test]
        [Timeout(BackwardsRangeTimeoutMilliseconds)]
        public void ABoundedDraw_RejectsABackwardsRange()
        {
            var rng = Rng.FromSeed(Seed);

            Assert.That(
                () => rng.NextInt(RangeMax, RangeMin),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // Nothing in the game may be likelier than anything else it is drawn
        // beside: a die whose low faces come up half again as often is a game
        // that is quietly unfair, and nobody would see it happening.
        [Test]
        public void ABoundedDraw_IsUniformAcrossTheRange()
        {
            // The buckets only mean anything while they tile the range exactly.
            Assert.That(
                (long)BiasRangeMax - BiasRangeMin + 1,
                Is.EqualTo((long)BucketCount * BucketWidth),
                "the buckets no longer tile the range");

            var rng = Rng.FromSeed(UniformitySeed);
            var counts = new int[BucketCount];

            for (var draw = 0; draw < SampleSize; draw++)
            {
                var value = rng.NextInt(BiasRangeMin, BiasRangeMax);

                Assert.That(value, Is.InRange(BiasRangeMin, BiasRangeMax));

                counts[(int)(((long)value - BiasRangeMin) / BucketWidth)]++;
            }

            var expected = SampleSize / BucketCount;
            var tolerance = (expected * TolerancePercent) / 100;

            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                Assert.That(
                    counts[bucket],
                    Is.EqualTo(expected).Within(tolerance),
                    $"bucket {bucket} of {BucketCount}");
            }
        }
    }
}
