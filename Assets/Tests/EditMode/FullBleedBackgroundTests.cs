using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.Views;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **Nothing behind the canvas is ever visible** — issue #290, and the
    /// rule docs/specs/ui/shared-components.md now states under
    /// "The canvas every component is measured in".
    ///
    /// Every screen is laid out at the 1920 x 1200 reference, and the
    /// `CanvasScaler` is set to `Expand`, so on a device that is not 16:10 the
    /// canvas is *bigger* than that reference in one direction. What fills the
    /// difference is what these tests are about: the screen's own background,
    /// reaching the edge, and never the engine's clear.
    ///
    /// Two halves, asserted separately on purpose, because a fix that only
    /// does one of them looks right on the tablet and is wrong everywhere
    /// else:
    ///
    /// - the painted background covers the whole canvas, whatever its shape;
    /// - the laid-out content is still exactly 1920 x 1200, centred, so the
    ///   fix cannot pass by stretching the layout instead. Every constant on
    ///   every spec page means what it meant before.
    ///
    /// The canvas used here is bigger in *both* directions than the reference,
    /// which is not a shape any real device has — it is the shape that makes a
    /// background that only stretched one way fail here rather than on
    /// somebody's tablet.
    /// </summary>
    public sealed class FullBleedBackgroundTests
    {
        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1200f;

        // A canvas that is not 16:10, larger than the reference in both
        // directions — what `ScreenMatchMode.Expand` produces on a device
        // whose aspect ratio is not the one the game is drawn at.
        const float OversizedCanvasWidth = 2400f;
        const float OversizedCanvasHeight = 1400f;

        const float Tolerance = 0.001f;

        const ulong AnySeed = 20260812UL;

        [Test]
        public void GameBoard_PaintsItsBackgroundToEveryEdge_AndLeavesTheBoardAtReferenceSize()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                    .AddComponent<GameBoardScreenView>();
                view.transform.SetParent(canvas, worldPositionStays: false);
                view.Initialize(new Game(new[] { FrogColour.Green, FrogColour.Blue }, AnySeed));

                AssertCoversTheWholeCanvas(view.BackgroundImage.rectTransform, canvas, "the board's background");
                AssertIsTheReferenceCanvasCentred(view.ContentRect, canvas, "the board's content");

                Assert.That(
                    view.BackgroundImage.color,
                    Is.EqualTo(ScreenColours.Background),
                    "the background the whole screen is painted in is the shared screen "
                    + "background, which is also what the camera clears to");

                // The layout itself is untouched: the header and the controls
                // band are full width *of the reference canvas*, not of the
                // device. If either of these reads 2400 the fix stretched the
                // board instead of the background, and every constant on
                // docs/specs/ui/game-board.md has quietly stopped meaning what
                // it says.
                Assert.That(
                    view.HeaderRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "the header is still the reference canvas wide, not the device wide");
                Assert.That(
                    view.ControlsRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "the controls band is still the reference canvas wide, not the device wide");
                Assert.That(
                    view.PondRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "the pond is still the reference canvas wide, so every lane is where "
                    + "docs/specs/ui/game-board.md puts it");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        [Test]
        public void TitleScreen_PaintsItsBackgroundToEveryEdge_AndLeavesItsContentAtReferenceSize()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(TitleScreenView), typeof(RectTransform))
                    .AddComponent<TitleScreenView>();
                view.transform.SetParent(canvas, worldPositionStays: false);

                AssertCoversTheWholeCanvas(view.BackgroundImage.rectTransform, canvas, "the title screen's background");
                AssertIsTheReferenceCanvasCentred(view.ContentRect, canvas, "the title screen's content");

                // The splash art stays at reference size rather than being
                // stretched to the device. It is a flat placeholder today
                // (#168 brings the real illustration), and an illustration
                // stretched to whatever shape the device happens to be is the
                // one thing a mockup cannot describe.
                Assert.That(
                    view.ArtRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "the splash art is still the reference canvas wide, not the device wide");
                Assert.That(
                    view.ArtRect.rect.height,
                    Is.EqualTo(ReferenceHeight).Within(Tolerance),
                    "the splash art is still the reference canvas tall, not the device tall");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        [Test]
        public void GameSetup_PaintsItsBackgroundToEveryEdge_AndLeavesItsContentAtReferenceSize()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(GameSetupScreenView), typeof(RectTransform))
                    .AddComponent<GameSetupScreenView>();
                view.transform.SetParent(canvas, worldPositionStays: false);

                AssertCoversTheWholeCanvas(view.BackgroundImage.rectTransform, canvas, "game setup's background");
                AssertIsTheReferenceCanvasCentred(view.ContentRect, canvas, "game setup's content");

                Assert.That(
                    view.ControlsRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "Back and Start are still SafeMargin from the reference canvas's edges, "
                    + "not from the device's");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        [Test]
        public void GameOver_PaintsItsBackgroundToEveryEdge_AndLeavesItsContentAtReferenceSize()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(GameOverScreenView), typeof(RectTransform))
                    .AddComponent<GameOverScreenView>();
                view.transform.SetParent(canvas, worldPositionStays: false);

                AssertCoversTheWholeCanvas(view.BackgroundImage.rectTransform, canvas, "game over's background");
                AssertIsTheReferenceCanvasCentred(view.ContentRect, canvas, "game over's content");

                Assert.That(
                    view.ControlsRect.rect.width,
                    Is.EqualTo(ReferenceWidth).Within(Tolerance),
                    "the controls are still SafeMargin from the reference canvas's edges, "
                    + "not from the device's");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// A dialog paints no background of its own — what it lays over the
        /// screen is the shared scrim, "a dimmed copy of the screen
        /// underneath". That is the thing that has to reach the edges: a scrim
        /// that stopped at the reference rectangle would leave the screen
        /// underneath undimmed in a strip down each side, which reads as a
        /// rendering fault rather than as a dialog.
        /// </summary>
        [Test]
        public void SettingsDialog_DimsTheWholeCanvas_AndLeavesItsPanelWhereItWas()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(SettingsDialogView), typeof(RectTransform))
                    .AddComponent<SettingsDialogView>();
                view.transform.SetParent(canvas, worldPositionStays: false);

                AssertCoversTheWholeCanvas(view.Dialog.Scrim.rectTransform, canvas, "the settings dialog's scrim");

                AssertIsCentredOn(view.Dialog.PanelRect, canvas, "the settings dialog's panel");
                Assert.That(
                    view.Dialog.PanelRect.rect.width,
                    Is.EqualTo(SettingsDialogView.SettingsDialogWidth).Within(Tolerance),
                    "the panel is still its own specified width, not the device's");
                Assert.That(
                    view.Dialog.PanelRect.rect.height,
                    Is.EqualTo(SettingsDialogView.SettingsDialogHeight).Within(Tolerance),
                    "the panel is still its own specified height, not the device's");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        [Test]
        public void EndGameConfirm_DimsTheWholeCanvas_AndLeavesItsPanelWhereItWas()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = new GameObject(nameof(EndGameConfirmView), typeof(RectTransform))
                    .AddComponent<EndGameConfirmView>();
                view.transform.SetParent(canvas, worldPositionStays: false);

                AssertCoversTheWholeCanvas(view.Dialog.Scrim.rectTransform, canvas, "the end-game confirm's scrim");

                AssertIsCentredOn(view.Dialog.PanelRect, canvas, "the end-game confirm's panel");
                Assert.That(
                    view.Dialog.PanelRect.rect.width,
                    Is.EqualTo(EndGameConfirmView.ConfirmDialogWidth).Within(Tolerance),
                    "the panel is still its own specified width, not the device's");
                Assert.That(
                    view.Dialog.PanelRect.rect.height,
                    Is.EqualTo(EndGameConfirmView.ConfirmDialogHeight).Within(Tolerance),
                    "the panel is still its own specified height, not the device's");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        // --- helpers ---------------------------------------------------------

        // A canvas the shape `ScreenMatchMode.Expand` produces on a device
        // that is not 16:10 — bigger than the reference, with the view under
        // it exactly as AppRoot's "Screens" host holds it.
        static RectTransform OversizedCanvas()
        {
            var host = new GameObject("OversizedCanvas", typeof(RectTransform));
            var rect = (RectTransform)host.transform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(OversizedCanvasWidth, OversizedCanvasHeight);
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        static void DestroyCanvas(RectTransform canvas)
        {
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }

        // Measured in world corners rather than in sizes and anchors, so no
        // anchoring arrangement can satisfy it without actually covering the
        // canvas.
        static void AssertCoversTheWholeCanvas(RectTransform rect, RectTransform canvas, string what)
        {
            var expected = new Vector3[4];
            var actual = new Vector3[4];

            canvas.GetWorldCorners(expected);
            rect.GetWorldCorners(actual);

            for (var corner = 0; corner < expected.Length; corner++)
            {
                Assert.That(
                    actual[corner].x,
                    Is.EqualTo(expected[corner].x).Within(Tolerance),
                    $"{what} does not reach the canvas horizontally — corner {corner}. "
                    + "The strip it leaves is where the engine's own clear shows through.");
                Assert.That(
                    actual[corner].y,
                    Is.EqualTo(expected[corner].y).Within(Tolerance),
                    $"{what} does not reach the canvas vertically — corner {corner}. "
                    + "The strip it leaves is where the engine's own clear shows through.");
            }
        }

        static void AssertIsTheReferenceCanvasCentred(RectTransform rect, RectTransform canvas, string what)
        {
            Assert.That(
                rect.rect.width,
                Is.EqualTo(ReferenceWidth).Within(Tolerance),
                $"{what} is not the reference canvas wide. Everything laid out inside it is "
                + "positioned in reference pixels, so a different width moves all of it.");
            Assert.That(
                rect.rect.height,
                Is.EqualTo(ReferenceHeight).Within(Tolerance),
                $"{what} is not the reference canvas tall.");

            AssertIsCentredOn(rect, canvas, what);
        }

        static void AssertIsCentredOn(RectTransform rect, RectTransform canvas, string what)
        {
            Assert.That(
                CenterOf(rect).x,
                Is.EqualTo(CenterOf(canvas).x).Within(Tolerance),
                $"{what} is not centred horizontally on the canvas, so the extra space is "
                + "all down one side instead of split evenly.");
            Assert.That(
                CenterOf(rect).y,
                Is.EqualTo(CenterOf(canvas).y).Within(Tolerance),
                $"{what} is not centred vertically on the canvas.");
        }

        static Vector3 CenterOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return (corners[0] + corners[2]) / 2f;
        }
    }
}
