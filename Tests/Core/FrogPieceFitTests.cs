using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="FrogPieceFit"/> — docs/specs/ui/game-board.md § The frog
    /// piece, FP-2 (#474): the frog's one scale is chosen from its Sit pose's
    /// box, so a sitting frog, outline included, is as big as fits the
    /// <c>FrogPieceDiameter</c> square and is centred on it.
    ///
    /// The box is in the model's own units, as the board's view sees it; the
    /// Unity side measures it off the skinned mesh and hands it here.
    /// </summary>
    public sealed class FrogPieceFitTests
    {
        const float Tolerance = 0.0001f;

        // game-board.md's FrogPieceDiameter and FrogPieceOutline.
        const float Footprint = 88f;
        const float Outline = 4f;

        // What a frog outline-and-all may span: the square less an outline
        // either side.
        const float Inside = Footprint - (2f * Outline);

        [Test]
        public void AWideBox_IsScaledSoItsWidthFillsTheSquare_LessTheOutline()
        {
            var fit = FrogPieceFit.Fit(-1f, 0f, 1f, 1f, Footprint, Outline);

            Assert.That(fit.Scale, Is.EqualTo(Inside / 2f).Within(Tolerance));
        }

        [Test]
        public void ATallBox_IsScaledSoItsHeightFillsTheSquare_LessTheOutline()
        {
            var fit = FrogPieceFit.Fit(0f, -0.25f, 1.1f, 1.75f, Footprint, Outline);

            Assert.That(fit.Scale, Is.EqualTo(Inside / 2f).Within(Tolerance));
        }

        [Test]
        public void TheBoxsCentre_IsDrawnAtThePiecesCentre()
        {
            var fit = FrogPieceFit.Fit(-0.5f, -0.2f, 0.6f, 0.9f, Footprint, Outline);

            Assert.That(fit.CentreX, Is.EqualTo(0.05f).Within(Tolerance));
            Assert.That(fit.CentreY, Is.EqualTo(0.35f).Within(Tolerance));
            Assert.That(fit.ToPieceX(0.05f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fit.ToPieceY(0.35f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TheFittedBox_PlusItsOutline_IsInsideTheSquare_AndTouchesItsEdgesAlongItsLongerSide()
        {
            // The approved frog's Sit pose, roughly: a shade taller than wide.
            var fit = FrogPieceFit.Fit(-0.52f, -0.21f, 0.58f, 0.90f, Footprint, Outline);
            var half = Footprint / 2f;

            var left = fit.ToPieceX(-0.52f) - Outline;
            var right = fit.ToPieceX(0.58f) + Outline;
            var bottom = fit.ToPieceY(-0.21f) - Outline;
            var top = fit.ToPieceY(0.90f) + Outline;

            Assert.That(bottom, Is.EqualTo(-half).Within(Tolerance), "the taller side reaches the bottom edge");
            Assert.That(top, Is.EqualTo(half).Within(Tolerance), "and the top edge");
            Assert.That(left, Is.GreaterThanOrEqualTo(-half - Tolerance));
            Assert.That(right, Is.LessThanOrEqualTo(half + Tolerance));
            Assert.That(left + right, Is.EqualTo(0f).Within(Tolerance), "centred across");
        }

        [Test]
        public void APointOutsideTheBox_IsDrawnOutsideTheSquare_AtTheSameScale()
        {
            // The hop overshoots (FP-2): a point the Sit box does not contain
            // is not pulled in, it is drawn at the one scale.
            var fit = FrogPieceFit.Fit(-1f, -1f, 1f, 1f, Footprint, Outline);

            Assert.That(fit.ToPieceY(3f), Is.EqualTo(3f * fit.Scale).Within(Tolerance));
            Assert.That(fit.ToPieceY(3f), Is.GreaterThan(Footprint / 2f));
        }

        [Test]
        public void ASquareNoBiggerThanItsOutline_DrawsTheFrogAtNoSize()
        {
            var fit = FrogPieceFit.Fit(-1f, -1f, 1f, 1f, 2f * Outline, Outline);

            Assert.That(fit.Scale, Is.EqualTo(0f));
        }

        [TestCase(1f, 0f, -1f, 1f)]
        [TestCase(0f, 1f, 1f, -1f)]
        [TestCase(0f, 0f, 0f, 1f)]
        [TestCase(float.NaN, 0f, 1f, 1f)]
        public void ABoxWithNoSize_IsRefused(float minX, float minY, float maxX, float maxY)
        {
            Assert.Throws<ArgumentException>(() => FrogPieceFit.Fit(minX, minY, maxX, maxY, Footprint, Outline));
        }
    }
}
