using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// docs/specs/ui/title-screen.md — the art is fitted inside the device's
    /// safe area, whole and never cropped (#533).
    /// </summary>
    public sealed class TitleArtFitTests
    {
        const float ArtWidth = 1920f;
        const float ArtHeight = 1200f;
        const float Tolerance = 0.001f;

        // What `ScreenMatchMode.Expand` makes of Derek's 2340 x 1080 phone:
        // the height is the reference's, the width grows to 2600.
        const float WidePhoneCanvasWidth = 2600f;

        // A 4:3 screen, 1600 x 1200: the width is the reference's, the height
        // grows to 1440.
        const float FourByThreeCanvasHeight = 1440f;

        static LayoutRect WholeCanvas(float width, float height) => new LayoutRect(0f, 0f, width, height);

        [Test]
        public void OnAWidePhone_TheWholeArtIsOnScreen_AtReferenceSize_CentredWithStripsEitherSide()
        {
            var fit = TitleArtFit.Fit(WidePhoneCanvasWidth, ArtHeight, WholeCanvas(WidePhoneCanvasWidth, ArtHeight), ArtWidth, ArtHeight);

            Assert.That(fit.Scale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(fit.Art.Width, Is.EqualTo(ArtWidth).Within(Tolerance));
            Assert.That(fit.Art.Height, Is.EqualTo(ArtHeight).Within(Tolerance));
            Assert.That(fit.Art.X, Is.EqualTo(340f).Within(Tolerance), "the 680 px left over is split evenly");
            Assert.That(fit.Art.Y, Is.EqualTo(0f).Within(Tolerance), "nothing is cropped off the top or the bottom");
        }

        [Test]
        public void WhenACutOutLeavesLessThanTheArt_ItIsScaledDownEvenlyToFit_InsideTheSafeArea()
        {
            // A 4:3 screen with a cut-out taking 120 px off its left edge: the
            // safe area is 1800 wide, so the art is drawn at 1800 / 1920.
            var safeArea = new LayoutRect(120f, 0f, 1800f, FourByThreeCanvasHeight);

            var fit = TitleArtFit.Fit(ArtWidth, FourByThreeCanvasHeight, safeArea, ArtWidth, ArtHeight);

            Assert.That(fit.Scale, Is.EqualTo(0.9375f).Within(Tolerance));
            Assert.That(fit.Art.Width, Is.EqualTo(1800f).Within(Tolerance));
            Assert.That(fit.Art.Height, Is.EqualTo(1125f).Within(Tolerance), "still 16:10, never stretched");
            Assert.That(fit.Art.X, Is.EqualTo(120f).Within(Tolerance), "clear of the cut-out");
            Assert.That(fit.Art.Y, Is.EqualTo((FourByThreeCanvasHeight - 1125f) / 2f).Within(Tolerance));
        }

        [Test]
        public void TheArtIsNeverScaledUp_EvenWhenTheSafeAreaIsBiggerThanItBothWays()
        {
            // Not a shape Expand produces, but a test canvas can be one, and
            // the art at more than its reference size would move every
            // constant laid out on it.
            var fit = TitleArtFit.Fit(2400f, 1400f, WholeCanvas(2400f, 1400f), ArtWidth, ArtHeight);

            Assert.That(fit.Scale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(fit.Art.X, Is.EqualTo(240f).Within(Tolerance));
            Assert.That(fit.Art.Y, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void ASafeAreaWithNoSize_IsTreatedAsTheWholeCanvas()
        {
            // Screen.safeArea can read empty before the first frame, or in a
            // batch-mode test run; the picture must still be on screen.
            var fit = TitleArtFit.Fit(WidePhoneCanvasWidth, ArtHeight, new LayoutRect(0f, 0f, 0f, 0f), ArtWidth, ArtHeight);

            Assert.That(fit.Scale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(fit.Art.X, Is.EqualTo(340f).Within(Tolerance));
            Assert.That(fit.Art.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ACanvasNotYetLaidOut_GetsTheArtAtReferenceSize_CentredOnIt()
        {
            var fit = TitleArtFit.Fit(0f, 0f, new LayoutRect(0f, 0f, 0f, 0f), ArtWidth, ArtHeight);

            Assert.That(fit.Scale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(fit.Art.Width, Is.EqualTo(ArtWidth).Within(Tolerance));
            Assert.That(fit.Art.X, Is.EqualTo(-ArtWidth / 2f).Within(Tolerance));
            Assert.That(fit.Art.Y, Is.EqualTo(-ArtHeight / 2f).Within(Tolerance));
        }

        // The extended scene behind the art: the same pond, wider and taller, with the
        // art's 1920 x 1200 in its middle (art-source/splash/beyond.py).
        const float SceneWidth = 2880f;
        const float SceneHeight = 1440f;

        [Test]
        public void OnAWidePhone_TheSceneIsDrawnAtTheArtsScale_CentredOnTheArt_SoTheyLineUp()
        {
            var fit = TitleArtFit.Fit(WidePhoneCanvasWidth, ArtHeight, WholeCanvas(WidePhoneCanvasWidth, ArtHeight), ArtWidth, ArtHeight);

            var scene = fit.Scene(WidePhoneCanvasWidth, ArtHeight, SceneWidth, SceneHeight);

            Assert.That(scene.Width, Is.EqualTo(SceneWidth).Within(Tolerance));
            Assert.That(scene.Height, Is.EqualTo(SceneHeight).Within(Tolerance));
            Assert.That(scene.X, Is.EqualTo(340f - 480f).Within(Tolerance), "its middle is exactly where the art is");
            Assert.That(scene.Y, Is.EqualTo(-120f).Within(Tolerance));
        }

        [Test]
        public void OnAFourByThreeScreen_TheSceneFillsTheExtraHeight_AtTheArtsScale()
        {
            var fit = TitleArtFit.Fit(ArtWidth, FourByThreeCanvasHeight, WholeCanvas(ArtWidth, FourByThreeCanvasHeight), ArtWidth, ArtHeight);

            var scene = fit.Scene(ArtWidth, FourByThreeCanvasHeight, SceneWidth, SceneHeight);

            Assert.That(scene.Width, Is.EqualTo(SceneWidth).Within(Tolerance));
            Assert.That(scene.X, Is.EqualTo(-480f).Within(Tolerance));
            Assert.That(scene.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void OnAScreenWiderThanTheScene_ItIsScaledUpAboutTheArtsCentre_JustEnoughToCover()
        {
            // 8:3, wider than the scene's 2.4:1: at the art's scale it would stop short.
            var fit = TitleArtFit.Fit(3200f, ArtHeight, WholeCanvas(3200f, ArtHeight), ArtWidth, ArtHeight);

            var scene = fit.Scene(3200f, ArtHeight, SceneWidth, SceneHeight);

            Assert.That(scene.Width, Is.EqualTo(3200f).Within(Tolerance));
            Assert.That(scene.Height, Is.EqualTo(1600f).Within(Tolerance));
            Assert.That(scene.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(scene.Y + (scene.Height / 2f), Is.EqualTo(600f).Within(Tolerance), "still centred on the art");
        }

        [Test]
        public void WhenACutOutShrinksTheArt_TheSceneStillCoversTheScreen()
        {
            var safeArea = new LayoutRect(120f, 0f, 1800f, FourByThreeCanvasHeight);
            var fit = TitleArtFit.Fit(ArtWidth, FourByThreeCanvasHeight, safeArea, ArtWidth, ArtHeight);

            var scene = fit.Scene(ArtWidth, FourByThreeCanvasHeight, SceneWidth, SceneHeight);

            Assert.That(scene.X, Is.LessThanOrEqualTo(0f));
            Assert.That(scene.Y, Is.LessThanOrEqualTo(0f));
            Assert.That(scene.X + scene.Width, Is.GreaterThanOrEqualTo(ArtWidth - Tolerance));
            Assert.That(scene.Y + scene.Height, Is.GreaterThanOrEqualTo(FourByThreeCanvasHeight - Tolerance));
            Assert.That(scene.X + (scene.Width / 2f), Is.EqualTo(fit.Art.X + (fit.Art.Width / 2f)).Within(Tolerance));
        }

        [Test]
        public void TheDevicesSafeArea_InScreenPixels_IsMeasuredInCanvasUnits()
        {
            // Derek's phone, 2340 x 1080, with a camera cut-out taking 100 px
            // off its left edge; the canvas is 2600 x 1200.
            var safe = TitleArtFit.SafeAreaInCanvas(2340f, 1080f, new LayoutRect(100f, 0f, 2240f, 1080f), WidePhoneCanvasWidth, ArtHeight);

            Assert.That(safe.X, Is.EqualTo(100f * WidePhoneCanvasWidth / 2340f).Within(Tolerance));
            Assert.That(safe.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(safe.Width, Is.EqualTo(2240f * WidePhoneCanvasWidth / 2340f).Within(Tolerance));
            Assert.That(safe.Height, Is.EqualTo(ArtHeight).Within(Tolerance));
        }

        [Test]
        public void AScreenWithNoSize_HasTheWholeCanvasAsItsSafeArea()
        {
            var safe = TitleArtFit.SafeAreaInCanvas(0f, 0f, new LayoutRect(0f, 0f, 0f, 0f), WidePhoneCanvasWidth, ArtHeight);

            Assert.That(safe.X, Is.EqualTo(0f));
            Assert.That(safe.Y, Is.EqualTo(0f));
            Assert.That(safe.Width, Is.EqualTo(WidePhoneCanvasWidth));
            Assert.That(safe.Height, Is.EqualTo(ArtHeight));
        }
    }
}
