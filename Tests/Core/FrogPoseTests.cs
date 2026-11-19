using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="FrogPose"/> — docs/specs/ui/game-board.md § The frog piece,
    /// FP-6 to FP-8: which of the approved model's two clips the frog is
    /// drawn from, and how far through it, as a function of the hop's
    /// progress and of nothing else.
    /// </summary>
    public sealed class FrogPoseTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void AtRest_TheFrogIsSitting_AtTheStartOfTheSitClip()
        {
            Assert.That(FrogPose.Rest.Clip, Is.EqualTo(FrogClip.Sit));
            Assert.That(FrogPose.Rest.Fraction, Is.EqualTo(0f));
        }

        [Test]
        public void AtTheStartOfAHop_TheFrogIsAtRest()
        {
            Assert.That(FrogPose.At(0f), Is.EqualTo(FrogPose.Rest));
        }

        [Test]
        public void AtTheEndOfAHop_TheFrogIsBackAtRest()
        {
            Assert.That(FrogPose.At(1f), Is.EqualTo(FrogPose.Rest));
        }

        [TestCase(0.1f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.8f)]
        [TestCase(0.99f)]
        public void PartWayThroughAHop_TheHopClipIsDrawn_ExactlyThatFarThrough(float progress)
        {
            // FP-6: progress 0 to 1 is the Hop clip from its first frame to its
            // last, in a straight line — so the clip and the slide that carries
            // the frog across cannot drift apart.
            var pose = FrogPose.At(progress);

            Assert.That(pose.Clip, Is.EqualTo(FrogClip.Hop));
            Assert.That(pose.Fraction, Is.EqualTo(progress).Within(Tolerance));
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ProgressOutsideTheHop_IsRest(float progress)
        {
            Assert.That(FrogPose.At(progress), Is.EqualTo(FrogPose.Rest));
        }

        [Test]
        public void AMoveThatGoesNowhere_IsNotAHop()
        {
            // FP-7: a wrong answer on the Start log leaves the frog sitting.
            Assert.That(FrogPose.ForMove(0, 0, FrogPose.TakeOffProgress), Is.EqualTo(FrogPose.Rest));
        }

        [Test]
        public void AMoveToAnotherPosition_IsTheHop_AtThatPointOfIt()
        {
            Assert.That(FrogPose.ForMove(1, 2, 0.3f), Is.EqualTo(new FrogPose(FrogClip.Hop, 0.3f)));
            Assert.That(FrogPose.ForMove(3, 2, 0.6f), Is.EqualTo(new FrogPose(FrogClip.Hop, 0.6f)), "a wrong answer's hop back is a hop too");
        }

        [Test]
        public void TheHopsMoments_AreTheSpecTablesProgressPoints()
        {
            // game-board.md § The frog through a hop — the progress at which
            // the approved Hop clip has each key pose (frog_model.py's
            // HOP_KEYS), in order.
            Assert.That(FrogPose.TakeOffProgress, Is.EqualTo(0.25f));
            Assert.That(FrogPose.InTheAirProgress, Is.EqualTo(0.5f));
            Assert.That(FrogPose.LandingProgress, Is.EqualTo(0.8f));
        }

        [Test]
        public void TheFrogsPosesThroughAHop_NeverJump()
        {
            // A thousand frames across the hop: the fraction through the clip
            // never moves more than a frame's worth between neighbours, and
            // the only change of clip is at the two ends, where Hop's first
            // and last frames are the Sit pose.
            const int frames = 1000;

            var previous = FrogPose.At(0f);

            for (var frame = 1; frame < frames; frame++)
            {
                var pose = FrogPose.At((float)frame / frames);

                Assert.That(pose.Clip, Is.EqualTo(FrogClip.Hop), $"frame {frame}");

                if (previous.Clip == FrogClip.Hop)
                {
                    Assert.That(pose.Fraction - previous.Fraction, Is.EqualTo(1f / frames).Within(Tolerance));
                }

                previous = pose;
            }
        }
    }
}
