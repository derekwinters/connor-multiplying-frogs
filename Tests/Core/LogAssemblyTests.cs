using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="LogAssembly"/> — docs/specs/ui/game-board.md § The log model,
    /// LG-2 and LG-5: a shared log is the approved model's three pieces put
    /// together by tools/art/log_model.py's own assembly rule, and which way a
    /// piece runs is read off its own vertices rather than assumed from how
    /// the importer turned the file's axes.
    ///
    /// The rule is copied from the script's docstring by hand, as numbers, so
    /// the code cannot drift from the art without these failing:
    ///
    ///     mid = L - 2 * CAP_LEN
    ///     n   = max(1, floor(mid / 1.0 + 0.5))
    ///     sy  = mid / n
    ///     bodies alternate A, B, A, B ... starting with A
    /// </summary>
    public sealed class LogAssemblyTests
    {
        const float Tolerance = 0.0001f;

        // game-board.md: SharedLogHeight 896 px over LilyPadDiameter 112 px.
        const float ReferenceLength = 8f;

        [Test]
        public void TheReferenceLog_IsSevenBodiesAtTheirOwnLength_BetweenTwoCaps()
        {
            var pieces = LogAssembly.For(ReferenceLength);

            Assert.That(
                pieces.Select(piece => piece.Kind).ToArray(),
                Is.EqualTo(new[]
                {
                    LogPieceKind.Cap,
                    LogPieceKind.BodyA, LogPieceKind.BodyB, LogPieceKind.BodyA, LogPieceKind.BodyB,
                    LogPieceKind.BodyA, LogPieceKind.BodyB, LogPieceKind.BodyA,
                    LogPieceKind.Cap,
                }),
                "a cap, seven bodies alternating A and B from A, and a cap");

            foreach (var body in pieces.Where(piece => piece.Kind != LogPieceKind.Cap))
            {
                Assert.That(body.Stretch, Is.EqualTo(1f).Within(Tolerance), "896 px is exactly seven bodies: none is stretched");
                Assert.That(body.Reversed, Is.False, "bodies are never turned");
            }

            Assert.That(
                pieces.Where(piece => piece.Kind != LogPieceKind.Cap).Select(piece => piece.Origin).ToArray(),
                Is.EqualTo(new[] { 0.5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f }).Within(Tolerance),
                "each body starts where the one before it ends, the first where the start cap ends");
        }

        [Test]
        public void TheStartCap_IsTheEndCapTurnedHalfATurn_SoBothEndsAreSawnFaces()
        {
            var pieces = LogAssembly.For(ReferenceLength);
            var start = pieces[0];
            var end = pieces[pieces.Count - 1];

            // The start cap's origin is its joining face, half a unit in; turned,
            // it runs back from there to the log's own start at 0.
            Assert.That(start.Kind, Is.EqualTo(LogPieceKind.Cap));
            Assert.That(start.Origin, Is.EqualTo(LogAssembly.CapLength).Within(Tolerance));
            Assert.That(start.Reversed, Is.True, "the start cap is turned half a turn");
            Assert.That(start.Stretch, Is.EqualTo(1f), "caps are never stretched");

            // The end cap's joining face is half a unit short of the far end,
            // and it runs on from there to the log's length.
            Assert.That(end.Kind, Is.EqualTo(LogPieceKind.Cap));
            Assert.That(end.Origin, Is.EqualTo(ReferenceLength - LogAssembly.CapLength).Within(Tolerance));
            Assert.That(end.Reversed, Is.False);
            Assert.That(end.Stretch, Is.EqualTo(1f));
        }

        [TestCase(4.5f, 4, 0.875f)]     // mid 3.5: 3.5 + 0.5 = 4.0 rounds to four, squashed
        [TestCase(4.4f, 3, 1.1333333f)] // mid 3.4: 3.9 floors to three, stretched
        [TestCase(2.5f, 2, 0.75f)]      // mid 1.5: half rounds up, to two
        [TestCase(1.5f, 1, 0.5f)]       // the shortest log the rule is defined for
        [TestCase(1.9f, 1, 0.9f)]
        [TestCase(20f, 19, 1f)]
        public void TheBodyCount_RoundsHalfUp_AndEveryBodyIsStretchedTheSame(float length, int bodies, float stretch)
        {
            var pieces = LogAssembly.For(length);
            var middle = pieces.Where(piece => piece.Kind != LogPieceKind.Cap).ToList();

            Assert.That(middle.Count, Is.EqualTo(bodies), $"a {length} unit log");
            Assert.That(middle.Select(piece => piece.Stretch), Has.All.EqualTo(stretch).Within(Tolerance));
        }

        [Test]
        public void ThePieces_TileTheWholeLength_WithNoGapAndNoOverlap()
        {
            for (var length = LogAssembly.ShortestLength; length <= 20f; length += 0.137f)
            {
                var pieces = LogAssembly.For(length);
                var at = 0f;

                // The start cap is turned, so it covers [origin - CapLength, origin].
                Assert.That(pieces[0].Origin - LogAssembly.CapLength, Is.EqualTo(at).Within(Tolerance), $"{length}: starts at 0");
                at = pieces[0].Origin;

                for (var i = 1; i < pieces.Count - 1; i++)
                {
                    Assert.That(pieces[i].Origin, Is.EqualTo(at).Within(Tolerance), $"{length}: body {i - 1} starts where the last piece ended");
                    Assert.That(pieces[i].Kind, Is.EqualTo(i % 2 == 1 ? LogPieceKind.BodyA : LogPieceKind.BodyB), $"{length}: A, B, A…");
                    at += pieces[i].Stretch * LogAssembly.BodyLength;
                }

                var end = pieces[pieces.Count - 1];

                Assert.That(end.Origin, Is.EqualTo(at).Within(Tolerance), $"{length}: the end cap joins the last body");
                Assert.That(end.Origin + LogAssembly.CapLength, Is.EqualTo(length).Within(Tolerance), $"{length}: and ends at the log's length");
            }
        }

        [Test]
        public void NoBody_IsStretchedPastTheScriptsOwnBounds()
        {
            // The docstring: for mid >= 0.5 a body's stretch stays within
            // [0.67, 1.5]; once mid >= 3.5, within [0.875, 1.17].
            for (var length = LogAssembly.ShortestLength; length <= 40f; length += 0.01f)
            {
                var mid = length - (2f * LogAssembly.CapLength);
                var stretch = LogAssembly.For(length)[1].Stretch;

                Assert.That(stretch, Is.InRange(0.5f, 1.5f), $"{length}");

                if (mid >= 3.5f)
                {
                    Assert.That(stretch, Is.InRange(0.875f - Tolerance, 1.17f), $"{length}");
                }
            }
        }

        [TestCase(1.49f)]
        [TestCase(1f)]
        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ALogShorterThanTheRuleCovers_IsRefused_NotGuessedAt(float length)
        {
            // Below 1.5 units the script says the caps would have to shrink
            // too, and how is not decided — so it is not drawn.
            Assert.Throws<ArgumentOutOfRangeException>(() => LogAssembly.For(length));
        }

        [Test]
        public void TheShortestLength_IsTwoCapsAndHalfABody()
        {
            Assert.That(LogAssembly.CapLength, Is.EqualTo(0.5f));
            Assert.That(LogAssembly.BodyLength, Is.EqualTo(1f));
            Assert.That(LogAssembly.ShortestLength, Is.EqualTo(1.5f));
        }

        // ---- MeasureFrame: which way a piece runs, read off its own points.

        /// <summary>
        /// Every way an importer could have laid the piece down — any axis for
        /// any direction, either sign, mirrored or not, in metres or
        /// centimetres — and the frame comes back the same: along is where the
        /// body runs from its joining face, up is out of its top, and across
        /// is the side its branch stub is on.
        /// </summary>
        [Test]
        public void TheFrame_IsReadOffThePiece_WhateverTheImporterDidToItsAxes()
        {
            foreach (var permutation in Permutations)
            {
                foreach (var signs in Signs)
                {
                    foreach (var scale in new[] { 1f, 100f, 0.01f })
                    {
                        var what = $"across->{signs[0] * (permutation[0] + 1)}, along->{signs[1] * (permutation[1] + 1)}, "
                            + $"up->{signs[2] * (permutation[2] + 1)}, scale {scale}";
                        var body = BodyB(permutation, signs, scale);

                        var frame = LogAssembly.MeasureFrame(body[0], body[1], body[2]);

                        Assert.That(frame.Across, Is.EqualTo(signs[0] * (permutation[0] + 1)), $"{what}: across");
                        Assert.That(frame.Along, Is.EqualTo(signs[1] * (permutation[1] + 1)), $"{what}: along");
                        Assert.That(frame.Up, Is.EqualTo(signs[2] * (permutation[2] + 1)), $"{what}: up");
                        Assert.That(frame.UnitLength, Is.EqualTo(scale).Within(scale * Tolerance), $"{what}: a body is one unit long");
                    }
                }
            }
        }

        [Test]
        public void TheFrame_RefusesSomethingThatIsNotOnePointList()
        {
            Assert.Throws<ArgumentException>(() => LogAssembly.MeasureFrame(new[] { 0f }, new[] { 0f, 1f }, new[] { 0f }));
            Assert.Throws<ArgumentException>(() => LogAssembly.MeasureFrame(null, new float[0], new float[0]));
            Assert.Throws<ArgumentException>(() => LogAssembly.MeasureFrame(new float[0], new float[0], new float[0]));
        }

        static readonly int[][] Permutations =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        static IEnumerable<int[]> Signs
        {
            get
            {
                foreach (var a in new[] { 1, -1 })
                {
                    foreach (var b in new[] { 1, -1 })
                    {
                        foreach (var c in new[] { 1, -1 })
                        {
                            yield return new[] { a, b, c };
                        }
                    }
                }
            }
        }

        // LogBody_B the way tools/art/log_model.py builds it, in its own
        // terms — across (X), along (Y) from its joining face at 0 to 1,
        // up (Z) from the waterline at 0 — then put on the importer's axes:
        // term t lands on axis permutation[t] with sign signs[t], scaled.
        //
        // The section is the script's: an ellipse HALF_W across and HALF_H
        // high, centred a third of the way under the waterline at ZC, cut flat
        // below at the last column. The stub pokes out on +across.
        static List<float>[] BodyB(int[] permutation, int[] signs, float scale)
        {
            const float HalfWidth = 0.7857f;
            const float HalfHeight = 0.68f;
            const float Centre = HalfHeight / 3f;
            const int Rings = 13;
            const int ColumnsEachSide = 20;
            const float LastColumn = 2.244f; // 128.6 degrees from the top, in radians

            var points = new List<float[]>();

            for (var ring = 0; ring < Rings; ring++)
            {
                var along = ring / (float)(Rings - 1);

                for (var column = -ColumnsEachSide; column <= ColumnsEachSide; column++)
                {
                    var theta = LastColumn * column / ColumnsEachSide;

                    points.Add(new[]
                    {
                        HalfWidth * (float)Math.Sin(theta),
                        along,
                        Centre + (HalfHeight * (float)Math.Cos(theta)),
                    });
                }
            }

            // The stub: a short tube out of the side, reaching about 0.15
            // past the bark, half way along and a little above the waterline.
            for (var k = 0; k < 8; k++)
            {
                var a = 2.0 * Math.PI * k / 8.0;

                points.Add(new[] { 0.93f, 0.6f + (0.1f * (float)Math.Cos(a)), 0.5f + (0.1f * (float)Math.Sin(a)) });
                points.Add(new[] { 0.70f, 0.5f + (0.13f * (float)Math.Cos(a)), 0.5f + (0.13f * (float)Math.Sin(a)) });
            }

            var axes = new[] { new List<float>(), new List<float>(), new List<float>() };

            foreach (var point in points)
            {
                var placed = new float[3];

                for (var term = 0; term < 3; term++)
                {
                    placed[permutation[term]] = signs[term] * point[term] * scale;
                }

                for (var axis = 0; axis < 3; axis++)
                {
                    axes[axis].Add(placed[axis]);
                }
            }

            return axes;
        }
    }
}
