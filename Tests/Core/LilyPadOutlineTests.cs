using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="LilyPadOutline"/> — docs/specs/ui/game-board.md § The lily
    /// pad is notched, veined, and varies per pad, LP-3: which way the
    /// approved model's notch points is read off the model's own outline,
    /// not assumed from how the importer turned its axes.
    ///
    /// The outlines here are built the way tools/art/lilypad_model.py builds
    /// its pads — a circle with a wedge cut from it, the wedge's apex
    /// `LilyPadNotchDepth` of the radius out towards the notch — plus a ring
    /// of inner points, so the function has to find the rim for itself.
    /// </summary>
    public sealed class LilyPadOutlineTests
    {
        const float Tolerance = 0.5f;
        const float NotchDepth = 0.15f;
        const int RimSegments = 72;

        [TestCase(10f, 0f)]
        [TestCase(15f, 0f)]
        [TestCase(20f, 90f)]
        [TestCase(25f, 212f)]
        [TestCase(10f, 341f)]
        [TestCase(25f, 359.5f)]
        public void TheNotchBearing_IsTheMiddleOfTheWedgeCutFromTheRim(float notchWidth, float pointsAt)
        {
            var leaf = Leaf(notchWidth, pointsAt, radius: 0.82f);

            var bearing = LilyPadOutline.NotchBearing(leaf.X, leaf.Y);

            Assert.That(AngleBetween(bearing, pointsAt), Is.LessThan(Tolerance), $"a {notchWidth} degree notch pointing at {pointsAt}");
            Assert.That(bearing, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f), "a bearing in [0, 360)");
        }

        [Test]
        public void TheNotchBearing_DoesNotDependOnTheModelsScale()
        {
            // The importer may scale the model by 100 (centimetres); the notch
            // is in the same place whatever the units.
            var small = Leaf(15f, 47f, radius: 0.82f);
            var large = Leaf(15f, 47f, radius: 82f);

            Assert.That(
                AngleBetween(LilyPadOutline.NotchBearing(small.X, small.Y), LilyPadOutline.NotchBearing(large.X, large.Y)),
                Is.LessThan(Tolerance));
        }

        [Test]
        public void TheRadius_IsTheFurthestPointFromTheCentre()
        {
            var leaf = Leaf(20f, 0f, radius: 0.82f);

            Assert.That(LilyPadOutline.Radius(leaf.X, leaf.Y), Is.EqualTo(0.82f).Within(0.0001f));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(1, -1)]
        [TestCase(2, -1)]
        public void TheUpAxis_IsThePadsThinAxis_PointingTheWayItsRimCurls(int axis, int sign)
        {
            // The model's pad is thin, and its rim curls up: whichever way the
            // importer laid it down, its up is the thin axis, towards the rim.
            var leaf = Leaf(20f, 30f, radius: 0.82f);
            var points = new List<float>[] { new List<float>(), new List<float>(), new List<float>() };
            var plane = new[] { (axis + 1) % 3, (axis + 2) % 3 };

            for (var i = 0; i < leaf.X.Count; i++)
            {
                var r = Math.Sqrt((leaf.X[i] * leaf.X[i]) + (leaf.Y[i] * leaf.Y[i])) / 0.82;

                // The top face, and the underside a little below it; the rim
                // lifts towards the outside on both.
                foreach (var below in new[] { 0.0, 0.035 })
                {
                    var height = (0.03 * r * r) - below;

                    points[plane[0]].Add(leaf.X[i]);
                    points[plane[1]].Add(leaf.Y[i]);
                    points[axis].Add((float)(sign * height));
                }
            }

            Assert.That(LilyPadOutline.UpAxis(points[0], points[1], points[2]), Is.EqualTo(sign * (axis + 1)));
        }

        [Test]
        public void AnOutlineWithNoNotch_IsRefused()
        {
            // A whole circle has no gap wider than its own segments. Nothing
            // says which way such a pad points, so it is an error rather than
            // a guess.
            var x = new List<float>();
            var y = new List<float>();

            for (var i = 0; i < RimSegments; i++)
            {
                var a = i * 2.0 * Math.PI / RimSegments;
                x.Add((float)Math.Cos(a));
                y.Add((float)Math.Sin(a));
            }

            Assert.Throws<ArgumentException>(() => LilyPadOutline.NotchBearing(x, y));
        }

        [Test]
        public void MismatchedCoordinates_AreRefused()
        {
            Assert.Throws<ArgumentException>(() => LilyPadOutline.NotchBearing(new[] { 1f, 0f }, new[] { 0f }));
        }

        static (List<float> X, List<float> Y) Leaf(float notchWidth, float pointsAt, float radius)
        {
            var x = new List<float>();
            var y = new List<float>();
            var half = notchWidth / 2.0;

            // The rim, from one cut edge round to the other, and the same
            // sweep pulled in towards the apex, as the script's grid does.
            for (var ring = 1; ring <= 4; ring++)
            {
                var t = ring / 4.0;

                for (var i = 0; i <= RimSegments; i++)
                {
                    var theta = half + (i * (360.0 - notchWidth) / RimSegments);
                    var rimX = Math.Cos(Radians(theta)) * radius;
                    var rimY = Math.Sin(Radians(theta)) * radius;
                    var apexX = NotchDepth * radius;

                    var px = apexX + ((rimX - apexX) * t);
                    var py = rimY * t;

                    // Turned so the notch points at `pointsAt`.
                    var turn = Radians(pointsAt);
                    x.Add((float)((px * Math.Cos(turn)) - (py * Math.Sin(turn))));
                    y.Add((float)((px * Math.Sin(turn)) + (py * Math.Cos(turn))));
                }
            }

            return (x, y);
        }

        static double Radians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        static float AngleBetween(float a, float b)
        {
            var d = Math.Abs(a - b) % 360f;

            return d > 180f ? 360f - d : d;
        }
    }
}
