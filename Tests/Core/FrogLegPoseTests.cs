using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="FrogLegPose"/> — docs/specs/ui/game-board.md § The frog
    /// piece, FP-6: the leg pose is a function of how far through the hop the
    /// frog is, 0 to 1, and of nothing else.
    /// </summary>
    public sealed class FrogLegPoseTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void AtTheStartOfAHop_TheLegsAreAtRest()
        {
            AssertPose(FrogLegPose.At(0f), FrogLegPose.Rest);
        }

        [Test]
        public void AtTheEndOfAHop_TheLegsAreBackAtRest()
        {
            AssertPose(FrogLegPose.At(1f), FrogLegPose.Rest);
        }

        [Test]
        public void TheRestPose_IsTheSpecTablesRestRow()
        {
            // game-board.md § The legs through a hop, copied by hand.
            AssertPose(FrogLegPose.Rest, new FrogLegPose(new LegJoints(20f, 30f), new LegJoints(60f, 150f)));
        }

        [Test]
        public void EachMoment_IsReachedAtTheProgressTheSpecTableGivesIt()
        {
            AssertPose(FrogLegPose.At(0.25f), new FrogLegPose(new LegJoints(-10f, 40f), new LegJoints(-50f, 10f)));
            AssertPose(FrogLegPose.At(0.5f), new FrogLegPose(new LegJoints(-30f, 60f), new LegJoints(50f, 140f)));
            AssertPose(FrogLegPose.At(0.8f), new FrogLegPose(new LegJoints(50f, 0f), new LegJoints(40f, 120f)));
        }

        [Test]
        public void AtTakeOff_TheBackLegsKickOut()
        {
            // Swung back toward the tail and straightened, compared with rest.
            Assert.That(FrogLegPose.TakeOff.Back.Hip, Is.LessThan(FrogLegPose.Rest.Back.Hip));
            Assert.That(FrogLegPose.TakeOff.Back.Knee, Is.LessThan(FrogLegPose.Rest.Back.Knee));
        }

        [Test]
        public void InTheAir_AllFourLegsAreTucked()
        {
            Assert.That(FrogLegPose.InTheAir.Back.Knee, Is.GreaterThan(FrogLegPose.TakeOff.Back.Knee), "back legs fold up after the kick");
            Assert.That(FrogLegPose.InTheAir.Front.Knee, Is.GreaterThan(FrogLegPose.Rest.Front.Knee), "front legs fold up");
            Assert.That(FrogLegPose.InTheAir.Front.Hip, Is.LessThan(FrogLegPose.Rest.Front.Hip), "front legs drawn back under the body");
        }

        [Test]
        public void ForTheLanding_TheFrontLegsReachForward()
        {
            Assert.That(FrogLegPose.Landing.Front.Hip, Is.GreaterThan(FrogLegPose.Rest.Front.Hip), "swung forward");
            Assert.That(FrogLegPose.Landing.Front.Knee, Is.LessThan(FrogLegPose.InTheAir.Front.Knee), "straightened out");
        }

        [Test]
        public void BetweenTwoMoments_TheAnglesMoveInAStraightLine()
        {
            // Halfway from rest (0) to take-off (0.25).
            var halfway = FrogLegPose.At(0.125f);

            Assert.That(halfway.Back.Hip, Is.EqualTo((60f + -50f) / 2f).Within(Tolerance));
            Assert.That(halfway.Back.Knee, Is.EqualTo((150f + 10f) / 2f).Within(Tolerance));
            Assert.That(halfway.Front.Hip, Is.EqualTo((20f + -10f) / 2f).Within(Tolerance));
            Assert.That(halfway.Front.Knee, Is.EqualTo((30f + 40f) / 2f).Within(Tolerance));
        }

        [Test]
        public void TheLegsNeverJump_FromOneFrameToTheNext()
        {
            // A thousand frames across the hop: no joint moves more than a
            // degree between neighbours, so the motion has no snap in it.
            const int frames = 1000;
            const float largestStep = 1f;

            var previous = FrogLegPose.At(0f);

            for (var frame = 1; frame <= frames; frame++)
            {
                var pose = FrogLegPose.At((float)frame / frames);

                Assert.That(System.Math.Abs(pose.Back.Hip - previous.Back.Hip), Is.LessThan(largestStep));
                Assert.That(System.Math.Abs(pose.Back.Knee - previous.Back.Knee), Is.LessThan(largestStep));
                Assert.That(System.Math.Abs(pose.Front.Hip - previous.Front.Hip), Is.LessThan(largestStep));
                Assert.That(System.Math.Abs(pose.Front.Knee - previous.Front.Knee), Is.LessThan(largestStep));

                previous = pose;
            }
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        public void ProgressOutsideTheHop_IsRest(float progress)
        {
            AssertPose(FrogLegPose.At(progress), FrogLegPose.Rest);
        }

        static void AssertPose(FrogLegPose actual, FrogLegPose expected)
        {
            Assert.That(actual.Front.Hip, Is.EqualTo(expected.Front.Hip).Within(Tolerance), "front hip");
            Assert.That(actual.Front.Knee, Is.EqualTo(expected.Front.Knee).Within(Tolerance), "front knee");
            Assert.That(actual.Back.Hip, Is.EqualTo(expected.Back.Hip).Within(Tolerance), "back hip");
            Assert.That(actual.Back.Knee, Is.EqualTo(expected.Back.Knee).Within(Tolerance), "back knee");
        }
    }
}
