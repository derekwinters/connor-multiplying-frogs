using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="PondWaterDrift"/> — docs/specs/ui/game-board.md § The water's
    /// marks, PW-3 and PW-4: the water slides left at
    /// <c>PondWaterDriftSpeed</c>, loops without a jump, and how far it has
    /// moved is a pure function of elapsed time.
    ///
    /// The spec's numbers are written out as literals rather than read from
    /// the constants, so the constants cannot drift from the page unnoticed.
    /// </summary>
    public sealed class PondWaterDriftTests
    {
        // game-board.md's named constants, copied by hand.
        const float SpecDriftSpeed = 25f;
        const float SpecTileSize = 1024f;

        const float Tolerance = 0.001f;

        [Test]
        public void TheConstants_AreTheSpecPagesValues()
        {
            Assert.That(PondWaterDrift.PondWaterDriftSpeed, Is.EqualTo(SpecDriftSpeed), "PW-3: 25 px per second");
            Assert.That(PondWaterDrift.PondWaterTileSize, Is.EqualTo(SpecTileSize), "one tile is drawn 1024 px across");
        }

        [Test]
        public void AtTheStart_TheWaterHasNotMoved()
        {
            Assert.That(PondWaterDrift.OffsetAt(0d), Is.EqualTo(0f));
        }

        [TestCase(1d, 25f)]
        [TestCase(2.5d, 62.5f)]
        [TestCase(10d, 250f)]
        [TestCase(40d, 1000f)]
        public void WithinOneTile_TheWaterHasMovedTwentyFivePixelsEverySecond(double seconds, float expected)
        {
            Assert.That(PondWaterDrift.OffsetAt(seconds), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void AfterExactlyOneTile_TheWaterIsBackWhereItStarted()
        {
            // 1024 px at 25 px/s is 40.96 s: the pattern has come round once,
            // and the picture is the one it started on.
            Assert.That(PondWaterDrift.OffsetAt(40.96d), Is.EqualTo(0f).Within(Tolerance).Or.EqualTo(SpecTileSize).Within(Tolerance));
            Assert.That(PondWaterDrift.OffsetAt(41.96d), Is.EqualTo(25f).Within(Tolerance));
        }

        [TestCase(0d)]
        [TestCase(0.016d)]
        [TestCase(40.959d)]
        [TestCase(40.96d)]
        [TestCase(1000d)]
        [TestCase(36000d)]
        [TestCase(1000000d)]
        public void TheOffset_IsAlwaysWithinOneTile(double seconds)
        {
            var offset = PondWaterDrift.OffsetAt(seconds);

            Assert.That(offset, Is.GreaterThanOrEqualTo(0f));
            Assert.That(offset, Is.LessThan(SpecTileSize));
        }

        [TestCase(40.9d)]
        [TestCase(81.9d)]
        [TestCase(36000d)]
        public void AcrossTheWrap_EveryFrameMovesTheSameDistance_SoThereIsNoJump(double seconds)
        {
            // PW-3: it loops without a jump. Wrapping at one tile changes the
            // number, not the picture: a step across the wrap is the same
            // 25 px/s as any other step, once the whole tile it skipped is
            // put back.
            const double frame = 1d / 60d;

            for (var t = seconds; t < seconds + 0.2d; t += frame)
            {
                var step = PondWaterDrift.OffsetAt(t + frame) - PondWaterDrift.OffsetAt(t);
                if (step < 0f)
                {
                    step += SpecTileSize;
                }

                Assert.That(step, Is.EqualTo(SpecDriftSpeed * frame).Within(0.01f), $"at {t:F3} s");
            }
        }

        [Test]
        public void HowFarItHasMoved_DependsOnTimeAlone_NotOnHowManyFramesThereWere()
        {
            // PW-4: a slow tablet shows the same drift as a fast one. Ten
            // seconds is ten seconds at 20 frames a second or at 120.
            var slow = 0d;
            for (var i = 0; i < 200; i++)
            {
                slow += 1d / 20d;
            }

            var fast = 0d;
            for (var i = 0; i < 1200; i++)
            {
                fast += 1d / 120d;
            }

            Assert.That(PondWaterDrift.OffsetAt(slow), Is.EqualTo(PondWaterDrift.OffsetAt(fast)).Within(Tolerance));
            Assert.That(PondWaterDrift.OffsetAt(slow), Is.EqualTo(250f).Within(Tolerance));
        }

        [Test]
        public void TheWindow_SlidesRight_SoTheMarksOnScreenSlideLeft()
        {
            // A RawImage's uvRect is the window onto the texture. For a mark
            // to move left on screen, the window has to move right over the
            // texture by the same distance.
            var before = PondWaterDrift.WindowAt(0d, 0f, 0f, 1920f, 1200f);
            var after = PondWaterDrift.WindowAt(1d, 0f, 0f, 1920f, 1200f);

            Assert.That(after.X - before.X, Is.EqualTo(25f / SpecTileSize).Within(1e-6f), "25 px of one 1024 px tile, to the right");
            Assert.That(after.Y, Is.EqualTo(before.Y), "PW-3: sideways only");
        }

        [Test]
        public void TheWindow_IsAsManyTilesAsTheSurfaceIsBig()
        {
            // PW-1: one tile repeated, each drawn PondWaterTileSize across, so
            // a 1920 x 896 pond shows 1.875 x 0.875 tiles.
            var window = PondWaterDrift.WindowAt(0d, 0f, 176f, 1920f, 896f);

            Assert.That(window.Width, Is.EqualTo(1920f / SpecTileSize).Within(1e-6f));
            Assert.That(window.Height, Is.EqualTo(896f / SpecTileSize).Within(1e-6f));
        }

        [Test]
        public void TwoSurfacesSideBySide_ShowOneUnbrokenPicture()
        {
            // PW-1: nowhere shows a seam. The background and the pond band are
            // two surfaces; where they meet they show the same texel, because
            // the window is placed by where the surface is on the screen.
            const double seconds = 17.3d;

            var background = PondWaterDrift.WindowAt(seconds, 0f, 0f, 1920f, 1200f);
            var pond = PondWaterDrift.WindowAt(seconds, 0f, 176f, 1920f, 896f);

            Assert.That(pond.X, Is.EqualTo(background.X).Within(1e-6f));
            Assert.That(pond.Y - background.Y, Is.EqualTo(176f / SpecTileSize).Within(1e-6f));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void ElapsedTimeThatIsNotATime_IsRefused(double seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PondWaterDrift.OffsetAt(seconds));
        }
    }
}
