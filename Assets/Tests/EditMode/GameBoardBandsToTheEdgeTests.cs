using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using Image = UnityEngine.UI.Image;
using PlayerChip = Frogs.Unity.UI.PlayerChip;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The bands are the top and the bottom of the screen, not panels
    /// floating on the pond** — issue #303, and the rule
    /// docs/specs/ui/game-board.md now states under "The bands reach the edges
    /// too".
    ///
    /// #290 gave the board a background that reaches all four edges. It left
    /// `header`, `pond` and `controls` laid out inside a rect fixed at the
    /// 1920 x 1200 reference, so on any screen that is not 16:10 the two
    /// painted bands stopped short of the real edge and the one thing that did
    /// reach it — the water — showed past their ends.
    ///
    /// What this fixture pins down is the split that fixes it, because either
    /// half alone is wrong:
    ///
    /// - the three bands' **fills** are measured against the canvas, so they
    ///   reach its edges on any aspect ratio, and an edge-anchored control
    ///   inside a band follows the real edge with it;
    /// - what `header` and `controls` **contain** is still measured in the
    ///   reference canvas, because the turn banner, the gear and `Roll` are
    ///   anchored controls rather than a track and there is nothing about them
    ///   a wider screen should stretch.
    ///
    /// The `pond`'s row is the exception, and #408 is where it became one: its
    /// row is a track whose job is to show how far along a lane a frog has
    /// got, so it spreads to whatever width it is given —
    /// <see cref="GameBoardElasticPondTests"/> holds it to the two drawings.
    ///
    /// And the test that makes the whole thing safe: at exactly 1920 x 1200
    /// nothing moved at all. This fix is invisible on the tablet it was
    /// reported from and on every mockup in the repo.
    ///
    /// The oversized canvas here is bigger than the reference in *both*
    /// directions, which is not a shape any real device has — it is the shape
    /// that makes a band that only grew one way fail here rather than on
    /// somebody's tablet.
    /// </summary>
    public sealed class GameBoardBandsToTheEdgeTests
    {
        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1200f;

        // What `ScreenMatchMode.Expand` produces on a device that is not
        // 16:10 — the same canvas FullBleedBackgroundTests uses, for the same
        // reason.
        const float OversizedCanvasWidth = 2400f;
        const float OversizedCanvasHeight = 1400f;

        const float Tolerance = 0.001f;

        const ulong AnySeed = 20260813UL;

        /// <summary>
        /// The bug as Derek reported it from the tablet: "the pond expanded
        /// but not the top and bottom bars". Before the fix the two painted
        /// bands are <see cref="ReferenceWidth"/> wide and centred, so this
        /// fails on the very first edge it checks.
        /// </summary>
        [Test]
        public void Header_And_Controls_ReachTheCanvasEdges_OnAScreenThatIsNot1610()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                var screen = BoundsOf(canvas, canvas);
                var header = BoundsOf(view.HeaderRect, canvas);
                var controls = BoundsOf(view.ControlsRect, canvas);

                Assert.That(
                    header.xMin,
                    Is.EqualTo(screen.xMin).Within(Tolerance),
                    "the header stops short of the left edge of the screen, and the strip it "
                    + "leaves is filled by the water behind it");
                Assert.That(
                    header.xMax,
                    Is.EqualTo(screen.xMax).Within(Tolerance),
                    "the header stops short of the right edge of the screen");
                Assert.That(
                    header.yMax,
                    Is.EqualTo(screen.yMax).Within(Tolerance),
                    "the header's top is not the top of the screen, so there is a band of water "
                    + "above it");

                Assert.That(
                    controls.xMin,
                    Is.EqualTo(screen.xMin).Within(Tolerance),
                    "the controls band stops short of the left edge of the screen");
                Assert.That(
                    controls.xMax,
                    Is.EqualTo(screen.xMax).Within(Tolerance),
                    "the controls band stops short of the right edge of the screen");
                Assert.That(
                    controls.yMin,
                    Is.EqualTo(screen.yMin).Within(Tolerance),
                    "the controls band's bottom is not the bottom of the screen, so there is a "
                    + "band of water below it");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The half of the bug that looks fixed and is not. `pond` has the
        /// identical anchoring fault as the other two bands; it is invisible
        /// only because the band painted nothing of its own and the background
        /// showed through the gap. So the pond gets a fill, and it is that
        /// fill — not the background behind it — that has to cover everything
        /// between the two bands.
        /// </summary>
        [Test]
        public void Pond_PaintsItsOwnWaterBetweenTheTwoBands_RatherThanShowingTheBackgroundThrough()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                var pondFill = view.PondRect.GetComponent<Image>();

                Assert.That(
                    pondFill,
                    Is.Not.Null,
                    "the pond band paints nothing, so what looks like the pond reaching the edge "
                    + "is the background showing through the pond's own gap");
                Assert.That(
                    pondFill.color,
                    Is.EqualTo(BoardColours.PondWater),
                    "the pond is painted in the water");

                var screen = BoundsOf(canvas, canvas);
                var pond = BoundsOf(view.PondRect, canvas);
                var header = BoundsOf(view.HeaderRect, canvas);
                var controls = BoundsOf(view.ControlsRect, canvas);

                Assert.That(pond.xMin, Is.EqualTo(screen.xMin).Within(Tolerance), "the pond reaches the left edge");
                Assert.That(pond.xMax, Is.EqualTo(screen.xMax).Within(Tolerance), "the pond reaches the right edge");
                Assert.That(
                    pond.yMax,
                    Is.EqualTo(header.yMin).Within(Tolerance),
                    "the pond starts where the header ends — the three bands meet with no gaps");
                Assert.That(
                    pond.yMin,
                    Is.EqualTo(controls.yMax).Within(Tolerance),
                    "the pond ends where the controls band starts");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// Where a taller screen's extra height goes, which is the same answer
        /// the code already gave for a shorter one: the pond, and nowhere
        /// else. A smaller `Roll` is the wrong thing to trade away, and a
        /// taller header is not something any mockup describes.
        /// </summary>
        [Test]
        public void TheExtraHeightOfATallerScreen_GoesToThePond_AndNowhereElse()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                Assert.That(
                    view.HeaderRect.rect.height,
                    Is.EqualTo(GameBoardScreenView.BoardHeaderHeight).Within(Tolerance),
                    "the header is its specified height on every screen");
                Assert.That(
                    view.ControlsRect.rect.height,
                    Is.EqualTo(GameBoardScreenView.BoardControlsHeight).Within(Tolerance),
                    "the controls band is its specified height on every screen");
                Assert.That(
                    view.PondRect.rect.height,
                    Is.EqualTo(OversizedCanvasHeight
                        - GameBoardScreenView.BoardHeaderHeight
                        - GameBoardScreenView.BoardControlsHeight).Within(Tolerance),
                    "every pixel of extra height belongs to the pond");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The hairline the mockup draws under `header` and over `controls`.
        /// It travels with its band: still `BoardBandOutline` tall, still
        /// inside the band's own bounds so the band's height is untouched, and
        /// now as wide as the band it edges — a hairline that stopped at 1920
        /// would be a line that ends before the band does.
        /// </summary>
        [Test]
        public void TheBandHairlines_SpanTheWidenedBand_AndStayInsideIt()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                var header = BoundsOf(view.HeaderRect, canvas);
                var controls = BoundsOf(view.ControlsRect, canvas);
                var headerHairline = BoundsOf(view.HeaderHairline.rectTransform, canvas);
                var controlsHairline = BoundsOf(view.ControlsHairline.rectTransform, canvas);

                Assert.That(
                    headerHairline.height,
                    Is.EqualTo(GameBoardScreenView.BoardBandOutline).Within(Tolerance));
                Assert.That(
                    controlsHairline.height,
                    Is.EqualTo(GameBoardScreenView.BoardBandOutline).Within(Tolerance));

                Assert.That(
                    headerHairline.xMin,
                    Is.EqualTo(header.xMin).Within(Tolerance),
                    "the hairline ends before the band it edges does");
                Assert.That(headerHairline.xMax, Is.EqualTo(header.xMax).Within(Tolerance));
                Assert.That(
                    headerHairline.yMin,
                    Is.EqualTo(header.yMin).Within(Tolerance),
                    "the hairline is drawn inside the band, along its bottom");

                Assert.That(controlsHairline.xMin, Is.EqualTo(controls.xMin).Within(Tolerance));
                Assert.That(controlsHairline.xMax, Is.EqualTo(controls.xMax).Within(Tolerance));
                Assert.That(
                    controlsHairline.yMax,
                    Is.EqualTo(controls.yMax).Within(Tolerance),
                    "the hairline is drawn inside the band, along its top");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// Derek's call on the one thing #303 left to decide: an
        /// edge-anchored control follows the **real** screen edge, not the
        /// reference one, because `SafeMargin` is a margin from the screen and
        /// not from a virtual rectangle. A gear sitting 288 px in from the
        /// right of a 2400-wide screen reads as a mistake.
        ///
        /// `Roll` is centred and does not care either way, so what is asserted
        /// of it is that it stayed centred.
        /// </summary>
        [Test]
        public void TheGearAndTheTurnChip_FollowTheRealScreenEdge_NotTheReferenceEdge()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                var screen = BoundsOf(canvas, canvas);
                var gear = BoundsOf(view.SettingsButton.RectTransform, canvas);
                var chip = BoundsOf(view.TurnBannerChip.RectTransform, canvas);
                var roll = BoundsOf(view.RollButton.RectTransform, canvas);
                var controls = BoundsOf(view.ControlsRect, canvas);

                Assert.That(
                    gear.xMax,
                    Is.EqualTo(screen.xMax - GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the gear is a safe margin in from the edge of the screen, not from the edge "
                    + "of the reference rectangle");
                Assert.That(
                    chip.xMin,
                    Is.EqualTo(screen.xMin + GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the turn chip is a safe margin in from the edge of the screen");

                Assert.That(
                    roll.center.x,
                    Is.EqualTo(screen.center.x).Within(Tolerance),
                    "`Roll` is centred on the screen");
                Assert.That(
                    roll.center.y,
                    Is.EqualTo(controls.center.y).Within(Tolerance),
                    "`Roll` is centred in its band");
                Assert.That(
                    roll.width,
                    Is.EqualTo(GameBoardScreenView.RollButtonWidth).Within(Tolerance),
                    "`Roll` is its own specified size, not the screen's");
                Assert.That(
                    roll.height,
                    Is.EqualTo(GameBoardScreenView.RollButtonHeight).Within(Tolerance));
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The other half of the split, and the half #408 turned over. A band
        /// grows, and the **pond's row grows with it**: the chips and the
        /// Start log against the real left safe margin, the End log against
        /// the real right one, and `LanePositionGap` taking up the difference.
        ///
        /// This test used to assert the opposite — that the logs and the lanes
        /// kept the reference canvas's geometry inside a widened band — which
        /// is what left the board a 1920 px picture centred in a wider one. It
        /// is the same test in the same place rather than a new one beside it,
        /// because the old rule is gone rather than joined; game-board.md
        /// keeps its wording under "the invariant this page used to carry".
        ///
        /// What is *not* elastic is asserted right below it: a log is still
        /// the pond band's reference height, and the header and the controls
        /// still contain nothing that stretches.
        /// </summary>
        [Test]
        public void TheLogsAndTheLanes_SpreadWithTheirBand_ToTheRealSafeMarginsAtBothEnds()
        {
            var canvas = OversizedCanvas();

            try
            {
                var view = BoardOn(canvas);

                var screen = BoundsOf(canvas, canvas);
                var startLog = BoundsOf(view.StartLogOutline.rectTransform, canvas);
                var endLog = BoundsOf(view.EndLogOutline.rectTransform, canvas);

                // Everything to the left of the Start log's right-hand edge is
                // fixed — a safe margin, the chip gutter and the gap after
                // it — so the Start log sits at the same x on every screen,
                // measured from the screen's own left edge.
                Assert.That(
                    startLog.xMin,
                    Is.EqualTo(screen.xMin
                        + GameBoardScreenView.SafeMargin
                        + GameBoardLaneView.LaneGutterWidth
                        + GameBoardLaneView.LaneGutterGap).Within(Tolerance),
                    "the Start log has stayed at the reference canvas's geometry instead of "
                    + "following the chips to the real left margin");
                Assert.That(
                    endLog.xMax,
                    Is.EqualTo(screen.xMax - GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the End log is pinned to the right of the real safe area, which is what "
                    + "the pads spread towards");

                Assert.That(
                    startLog.height,
                    Is.EqualTo(GameBoardScreenView.SharedLogHeight).Within(Tolerance),
                    "the logs are the pond band's reference height — a taller screen gives its "
                    + "extra height to the water, not to the logs");
                Assert.That(
                    endLog.height,
                    Is.EqualTo(GameBoardScreenView.SharedLogHeight).Within(Tolerance));

                foreach (var lane in view.Lanes)
                {
                    var bounds = BoundsOf(lane.RectTransform, canvas);

                    Assert.That(
                        bounds.xMin,
                        Is.EqualTo(screen.xMin + GameBoardScreenView.SafeMargin).Within(Tolerance),
                        "a lane's chip is against the real left safe margin, not floating in "
                        + "from an edge nobody can see");
                    Assert.That(
                        bounds.xMax,
                        Is.EqualTo(screen.xMax - GameBoardScreenView.SafeMargin).Within(Tolerance),
                        "and its track runs out to the real right one");

                    // Position 0 is still on the Start log and position 8
                    // still on the End log — at every width, which is the
                    // invariant the spreading had to keep true.
                    Assert.That(
                        BoundsOf(lane.PositionRects[0], canvas).center.x,
                        Is.EqualTo(startLog.center.x).Within(Tolerance),
                        "position 0 has come off the Start log");
                    Assert.That(
                        BoundsOf(lane.PositionRects[Lane.LaneWinningPosition], canvas).center.x,
                        Is.EqualTo(endLog.center.x).Within(Tolerance),
                        "position 8 has come off the End log");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The test that makes the rest of this fixture safe to have written:
        /// on the canvas every mockup is drawn at, nothing moved. Every band
        /// and every element inside it is exactly where
        /// docs/specs/ui/game-board.md puts it, measured from the screen's own
        /// corners rather than from anything the view computed.
        /// </summary>
        [Test]
        public void AtExactlyTheReferenceCanvas_EveryBandAndEverythingInItIsWhereItAlwaysWas()
        {
            var canvas = ReferenceCanvas();

            try
            {
                var view = BoardOn(canvas);

                var left = -ReferenceWidth / 2f;
                var right = ReferenceWidth / 2f;
                var top = ReferenceHeight / 2f;
                var bottom = -ReferenceHeight / 2f;

                var headerBottom = top - GameBoardScreenView.BoardHeaderHeight;
                var controlsTop = bottom + GameBoardScreenView.BoardControlsHeight;

                AssertBounds(
                    view.HeaderRect,
                    canvas,
                    Rect.MinMaxRect(left, headerBottom, right, top),
                    "the header");
                AssertBounds(
                    view.PondRect,
                    canvas,
                    Rect.MinMaxRect(left, controlsTop, right, headerBottom),
                    "the pond");
                AssertBounds(
                    view.ControlsRect,
                    canvas,
                    Rect.MinMaxRect(left, bottom, right, controlsTop),
                    "the controls band");

                AssertBounds(
                    view.HeaderHairline.rectTransform,
                    canvas,
                    Rect.MinMaxRect(left, headerBottom, right, headerBottom + GameBoardScreenView.BoardBandOutline),
                    "the header's hairline");
                AssertBounds(
                    view.ControlsHairline.rectTransform,
                    canvas,
                    Rect.MinMaxRect(left, controlsTop - GameBoardScreenView.BoardBandOutline, right, controlsTop),
                    "the controls band's hairline");

                // The header's contents: the chip against the left safe
                // margin, the gear against the right one, both centred on the
                // band.
                var headerCentre = (top + headerBottom) / 2f;
                var chipLeft = left + GameBoardScreenView.SafeMargin;

                AssertBounds(
                    view.TurnBannerChip.RectTransform,
                    canvas,
                    Rect.MinMaxRect(
                        chipLeft,
                        headerCentre - (PlayerChip.PlayerChipHeight / 2f),
                        chipLeft + PlayerChip.PlayerChipWidth,
                        headerCentre + (PlayerChip.PlayerChipHeight / 2f)),
                    "the turn chip");

                var gear = BoundsOf(view.SettingsButton.RectTransform, canvas);
                Assert.That(
                    gear.xMax,
                    Is.EqualTo(right - GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the gear is against the right safe margin");
                Assert.That(gear.center.y, Is.EqualTo(headerCentre).Within(Tolerance), "the gear is centred on the band");

                Assert.That(
                    BoundsOf(view.TurnBannerText.rectTransform, canvas).xMin,
                    Is.EqualTo(chipLeft + PlayerChip.PlayerChipWidth + GameBoardScreenView.TurnBannerGap).Within(Tolerance),
                    "the banner's words sit a gap past the chip beside them");

                // The controls band's contents: `Roll`, oversized and centred.
                var controlsCentre = (bottom + controlsTop) / 2f;

                AssertBounds(
                    view.RollButton.RectTransform,
                    canvas,
                    Rect.MinMaxRect(
                        -GameBoardScreenView.RollButtonWidth / 2f,
                        controlsCentre - (GameBoardScreenView.RollButtonHeight / 2f),
                        GameBoardScreenView.RollButtonWidth / 2f,
                        controlsCentre + (GameBoardScreenView.RollButtonHeight / 2f)),
                    "`Roll`");

                // The pond's contents: the two shared logs, in the columns
                // every lane's track starts and ends in.
                var pondCentre = (controlsTop + headerBottom) / 2f;
                var startLogLeft = left
                    + GameBoardScreenView.SafeMargin
                    + GameBoardLaneView.LaneGutterWidth
                    + GameBoardLaneView.LaneGutterGap;

                AssertBounds(
                    view.StartLogOutline.rectTransform,
                    canvas,
                    Rect.MinMaxRect(
                        startLogLeft,
                        pondCentre - (GameBoardScreenView.SharedLogHeight / 2f),
                        startLogLeft + GameBoardScreenView.LogWidth,
                        pondCentre + (GameBoardScreenView.SharedLogHeight / 2f)),
                    "the Start log");
                AssertBounds(
                    view.EndLogOutline.rectTransform,
                    canvas,
                    Rect.MinMaxRect(
                        right - GameBoardScreenView.SafeMargin - GameBoardScreenView.LogWidth,
                        pondCentre - (GameBoardScreenView.SharedLogHeight / 2f),
                        right - GameBoardScreenView.SafeMargin,
                        pondCentre + (GameBoardScreenView.SharedLogHeight / 2f)),
                    "the End log");

                // At the reference canvas the log fills the pond band exactly,
                // which is the sentence game-board.md writes about it.
                Assert.That(
                    GameBoardScreenView.SharedLogHeight,
                    Is.EqualTo(view.PondRect.rect.height).Within(Tolerance),
                    "at 1920 x 1200 the logs fill the pond band, edge to edge with the two "
                    + "hairlines");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        // --- helpers ---------------------------------------------------------

        static GameBoardScreenView BoardOn(RectTransform canvas)
        {
            var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                .AddComponent<GameBoardScreenView>();
            view.transform.SetParent(canvas, worldPositionStays: false);
            view.Initialize(new Game(new[] { FrogColour.Green, FrogColour.Blue }, AnySeed));

            return view;
        }

        static RectTransform ReferenceCanvas()
        {
            return CanvasOf(ReferenceWidth, ReferenceHeight);
        }

        static RectTransform OversizedCanvas()
        {
            return CanvasOf(OversizedCanvasWidth, OversizedCanvasHeight);
        }

        // The view under a canvas exactly as AppRoot's "Screens" host holds
        // it — the same fixture FullBleedBackgroundTests builds.
        static RectTransform CanvasOf(float width, float height)
        {
            var host = new GameObject("Canvas", typeof(RectTransform));
            var rect = (RectTransform)host.transform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        static void DestroyCanvas(RectTransform canvas)
        {
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }

        static void AssertBounds(RectTransform rect, RectTransform canvas, Rect expected, string what)
        {
            var actual = BoundsOf(rect, canvas);

            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(Tolerance), $"{what}: left edge");
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(Tolerance), $"{what}: right edge");
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(Tolerance), $"{what}: bottom edge");
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(Tolerance), $"{what}: top edge");
        }

        // Where something actually is on the screen, in the canvas's own
        // coordinates — measured from world corners rather than from anchors
        // and offsets, so no anchoring arrangement can satisfy these tests
        // without putting the pixels where they say.
        static Rect BoundsOf(RectTransform rect, RectTransform canvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var min = canvas.InverseTransformPoint(corners[0]);
            var max = canvas.InverseTransformPoint(corners[2]);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
