using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.Views;
using PlayerChip = Frogs.Unity.UI.PlayerChip;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The pond spreads to the full width of the board** — issue #408, and
    /// the rule docs/specs/ui/game-board.md states under
    /// [Anchors](../../docs/specs/ui/game-board.md#anchors) and "the bands
    /// reach the edges too".
    ///
    /// #303 made the three bands the screen's own top, middle and bottom. It
    /// left what the `pond` band *contains* at the reference canvas's
    /// geometry, so on the tablet the board was a 1920 px picture centred in a
    /// wider band and the chips floated some 300 px in from an edge they were
    /// supposed to be pinned to. The fix is one number: `LanePositionGap`
    /// stops being a typed 48 px and is derived from the screen instead.
    ///
    /// Two tests carry this fixture, and they are opposite ends of the same
    /// rule:
    ///
    /// - <see cref="AtExactlyTheReferenceCanvas_EveryPositionOnThePondsRowIsExactlyWhereItAlwaysWas"/>
    ///   is **the check that matters**. At 1920 x 1200 the board has to be
    ///   pixel-identical to the board before this change — that is not a
    ///   coincidence the formula happens to allow, it is the condition it was
    ///   chosen to satisfy, because the reference canvas has to keep being a
    ///   picture of the game. The wireframe proved it by rendering
    ///   `mockups/game-board.html` byte-for-byte the same before and after;
    ///   this is that comparison in geometry rather than in pixels.
    /// - <see cref="AtAWiderScreen_ThePondsRowSpreadsToTheRealSafeMarginsAtBothEnds"/>
    ///   is what the change is *for*, at the width
    ///   `mockups/game-board-wide.html` is drawn at.
    ///
    /// Both are written as **tables of where things are**, in the canvas's own
    /// coordinates, read off the two committed mockups rather than recomputed
    /// from the formula under test. A test that re-derives the layout from the
    /// same arithmetic the view uses agrees with the view by construction and
    /// proves nothing; these numbers come from the drawings, so the view has
    /// to land on the picture.
    /// </summary>
    public sealed class GameBoardElasticPondTests
    {
        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in,
        // and the size both `mockups/game-board.html` and the target tablet
        // are.
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1200f;

        // What `mockups/game-board-wide.html` is drawn at — 2.13 : 1, within a
        // hair of the ratio the tablet actually reports, and the width at
        // which the derived gap lands on a whole 128 px.
        const float WideWidth = 2560f;

        const float Tolerance = 0.001f;

        const ulong AnySeed = 20260823UL;

        // Where the pond band's own centre line sits on both canvases: the
        // band is everything between the two bands of fixed height, so on a
        // 1200 px tall screen it runs from -424 to 472 and its centre is 24 px
        // above the middle of the screen. Every log and every lane on the
        // tables below is measured from it.
        const float PondCentreY = 24f;

        // Two frogs, so two lanes, stacked and centred on the pond: 2 x 184 px
        // of lane, centred on PondCentreY.
        const float FirstLaneCentreY = PondCentreY + 92f;
        const float SecondLaneCentreY = PondCentreY - 92f;

        /// <summary>
        /// **The check that matters.** Every position on the pond's row, at
        /// the canvas every mockup is drawn at, against the numbers
        /// `mockups/game-board.html` draws them at — the chips against the
        /// left safe margin, the Start log at 352 px in, seven lily pads at a
        /// 160 px pitch, and the End log against the right safe margin.
        ///
        /// It is capable of failing on any later refactor that shifts anything
        /// by a pixel at 1920, which is the whole reason it is a table of
        /// literals rather than a re-run of the formula.
        /// </summary>
        [Test]
        public void AtExactlyTheReferenceCanvas_EveryPositionOnThePondsRowIsExactlyWhereItAlwaysWas()
        {
            var canvas = CanvasOf(ReferenceWidth, ReferenceHeight);

            try
            {
                // Green on its third lily pad, so the piece is drawn on a pad
                // rather than on a log and both cases are in the table.
                var game = TwoFrogGame();
                MoveTo(game, FrogColour.Green, 3);

                var view = BoardOn(canvas, game);

                // The two shared logs, filling the pond band top to bottom.
                // 352 px and 48 px from the mockup's own `left:` and `right:`.
                AssertBounds(
                    view.StartLog.rectTransform,
                    canvas,
                    Rect.MinMaxRect(-608f, -424f, -432f, 472f),
                    "the Start log");
                AssertBounds(
                    view.EndLog.rectTransform,
                    canvas,
                    Rect.MinMaxRect(736f, -424f, 912f, 472f),
                    "the End log");

                // Nine positions per lane: the Start log's column, seven lily
                // pads at a 160 px pitch, the End log's column.
                var centresX = new[] { -520f, -328f, -168f, -8f, 152f, 312f, 472f, 632f, 824f };

                AssertLaneRow(
                    view.LaneFor(FrogColour.Green),
                    canvas,
                    laneCentreY: FirstLaneCentreY,
                    laneLeft: -912f,
                    laneRight: 912f,
                    trackLeft: -608f,
                    centresX: centresX,
                    pieceOn: 3,
                    what: "Green's lane");

                AssertLaneRow(
                    view.LaneFor(FrogColour.Blue),
                    canvas,
                    laneCentreY: SecondLaneCentreY,
                    laneLeft: -912f,
                    laneRight: 912f,
                    trackLeft: -608f,
                    centresX: centresX,
                    pieceOn: 0,
                    what: "Blue's lane");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// What the change is for. At 2560 px the chips and the Start log are
        /// against the real left safe margin, the End log against the real
        /// right one, and the seven lily pads are spread evenly through what
        /// is left — the picture `mockups/game-board-wide.html` draws, whose
        /// own numbers these are.
        /// </summary>
        [Test]
        public void AtAWiderScreen_ThePondsRowSpreadsToTheRealSafeMarginsAtBothEnds()
        {
            var canvas = CanvasOf(WideWidth, ReferenceHeight);

            try
            {
                var game = TwoFrogGame();
                MoveTo(game, FrogColour.Green, 3);

                var view = BoardOn(canvas, game);

                // The Start log is still 352 px from the left edge of the
                // screen — everything to the left of its right-hand edge is
                // fixed — and the End log is still 48 px from the right edge
                // of the screen, which is 640 px further out than it was.
                AssertBounds(
                    view.StartLog.rectTransform,
                    canvas,
                    Rect.MinMaxRect(-928f, -424f, -752f, 472f),
                    "the Start log");
                AssertBounds(
                    view.EndLog.rectTransform,
                    canvas,
                    Rect.MinMaxRect(1056f, -424f, 1232f, 472f),
                    "the End log");

                // The wide mockup's own pad row: it starts at 656 px from the
                // left edge — `calc(224px + var(--lane-gap))` past the chip —
                // and its pitch is 112 + 128.
                var centresX = new[] { -840f, -568f, -328f, -88f, 152f, 392f, 632f, 872f, 1144f };

                AssertLaneRow(
                    view.LaneFor(FrogColour.Green),
                    canvas,
                    laneCentreY: FirstLaneCentreY,
                    laneLeft: -1232f,
                    laneRight: 1232f,
                    trackLeft: -928f,
                    centresX: centresX,
                    pieceOn: 3,
                    what: "Green's lane");

                AssertLaneRow(
                    view.LaneFor(FrogColour.Blue),
                    canvas,
                    laneCentreY: SecondLaneCentreY,
                    laneLeft: -1232f,
                    laneRight: 1232f,
                    trackLeft: -928f,
                    centresX: centresX,
                    pieceOn: 0,
                    what: "Blue's lane");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The invariant the page states in its own right: **`header` and
        /// `controls` do not stretch.** What they contain is anchored
        /// controls, not a track, so a wider screen moves the two that are
        /// anchored to an edge and resizes none of them. Without this the
        /// obvious reading of "the pond spreads" is "everything spreads", and
        /// a 640 px-wider `Roll` is nobody's design.
        /// </summary>
        [Test]
        public void AtAWiderScreen_TheHeaderAndTheControlsDoNotStretch_TheyOnlyFollowTheEdge()
        {
            var canvas = CanvasOf(WideWidth, ReferenceHeight);

            try
            {
                var view = BoardOn(canvas, TwoFrogGame());

                // Anchored to an edge: it follows the real edge, at its own
                // size.
                var gear = BoundsOf(view.SettingsButton.RectTransform, canvas);

                Assert.That(
                    gear.xMax,
                    Is.EqualTo((WideWidth / 2f) - GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the gear is a safe margin in from the real right edge");
                Assert.That(
                    gear.width,
                    Is.EqualTo(GameBoardScreenView.SettingsButtonSize).Within(Tolerance),
                    "the gear is its own square, not a share of the screen");
                Assert.That(
                    gear.height,
                    Is.EqualTo(GameBoardScreenView.SettingsButtonSize).Within(Tolerance));

                var banner = BoundsOf(view.TurnBannerText.rectTransform, canvas);

                Assert.That(
                    banner.xMin,
                    Is.EqualTo((-WideWidth / 2f) + GameBoardScreenView.SafeMargin).Within(Tolerance),
                    "the turn banner is a safe margin in from the real left edge");
                Assert.That(
                    view.TurnBannerText.fontSize,
                    Is.EqualTo((int)GameBoardScreenView.TurnBannerSize),
                    "the banner's words are their own size on every screen, not a share of it");

                // Centred, and its own size: `Roll` is the control the spec
                // says a wider screen has nothing to do to.
                var roll = BoundsOf(view.RollButton.RectTransform, canvas);

                Assert.That(roll.center.x, Is.EqualTo(0f).Within(Tolerance), "`Roll` is centred on the screen");
                Assert.That(
                    roll.width,
                    Is.EqualTo(GameBoardScreenView.RollButtonWidth).Within(Tolerance),
                    "`Roll` is its specified width, not the screen's");
                Assert.That(
                    roll.height,
                    Is.EqualTo(GameBoardScreenView.RollButtonHeight).Within(Tolerance));

                // And the bands themselves are unchanged in height, so the
                // pond is still the only band a different screen resizes.
                Assert.That(
                    view.HeaderRect.rect.height,
                    Is.EqualTo(GameBoardScreenView.BoardHeaderHeight).Within(Tolerance));
                Assert.That(
                    view.ControlsRect.rect.height,
                    Is.EqualTo(GameBoardScreenView.BoardControlsHeight).Within(Tolerance));
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The gap is the one number on game-board.md that the screen decides,
        /// and this is its formula, checked at the two widths that are drawn
        /// and at the floor below them.
        ///
        /// The first of these is the reason the change is safe: at 1920 px the
        /// slack over <c>LaneFixedWidth</c> divided between the eight gaps is
        /// exactly the 48 px the constant used to be typed as. It is asserted
        /// *before* the floor is applied, because "the floor happens to hide
        /// it" and "the arithmetic lands on it" are different facts, and only
        /// the second one keeps the reference canvas a picture of the game.
        /// </summary>
        [Test]
        public void TheDerivedGap_LandsOnTheOldTypedValueAtTheReferenceCanvas_SpreadsAboveIt_AndFloorsBelow()
        {
            Assert.That(
                (ReferenceWidth - GameBoardLaneView.LaneFixedWidth) / GameBoardLaneView.LanePositionGapCount,
                Is.EqualTo(GameBoardLaneView.LanePositionGapMin).Within(Tolerance),
                "at the reference canvas the formula has to give exactly the 48 px "
                + "LanePositionGap used to be typed as — that is the condition it was chosen "
                + "to satisfy, not a coincidence");

            Assert.That(
                GameBoardLaneView.LanePositionGapFor(ReferenceWidth),
                Is.EqualTo(48f).Within(Tolerance));
            Assert.That(
                GameBoardLaneView.LanePositionGapFor(WideWidth),
                Is.EqualTo(128f).Within(Tolerance),
                "game-board.md's own worked example for the wide drawing");

            // The floor is what a narrower screen gets: the pads stop closing
            // up and the board keeps its reference row.
            Assert.That(
                GameBoardLaneView.LanePositionGapFor(1600f),
                Is.EqualTo(GameBoardLaneView.LanePositionGapMin).Within(Tolerance),
                "below the reference width the gap stops shrinking");
            Assert.That(
                GameBoardLaneView.LanePositionGapFor(0f),
                Is.EqualTo(GameBoardLaneView.LanePositionGapMin).Within(Tolerance),
                "and it never goes negative on a screen nobody has");

            // LaneFixedWidth is everything on the row that does not stretch,
            // so the row sums to the screen at every width above the floor —
            // which is what "the pond spreads to the full width" means
            // arithmetically.
            foreach (var width in new[] { ReferenceWidth, 2400f, WideWidth })
            {
                Assert.That(
                    GameBoardLaneView.LaneFixedWidth
                        + (GameBoardLaneView.LanePositionGapCount * GameBoardLaneView.LanePositionGapFor(width)),
                    Is.EqualTo(width).Within(Tolerance),
                    $"the row sums to the screen at {width} px");
            }
        }

        // --- helpers ---------------------------------------------------------

        // One lane's whole row, against the table of where its nine positions
        // are: the chip in the gutter at the left safe margin, the track
        // pinned to the right one, and the piece on whichever position Core
        // reports.
        static void AssertLaneRow(
            GameBoardLaneView lane,
            RectTransform canvas,
            float laneCentreY,
            float laneLeft,
            float laneRight,
            float trackLeft,
            float[] centresX,
            int pieceOn,
            string what)
        {
            var laneTop = laneCentreY + (GameBoardLaneView.LaneHeight / 2f);
            var laneBottom = laneCentreY - (GameBoardLaneView.LaneHeight / 2f);

            AssertBounds(
                lane.RectTransform,
                canvas,
                Rect.MinMaxRect(laneLeft, laneBottom, laneRight, laneTop),
                what);

            AssertBounds(
                lane.Chip.RectTransform,
                canvas,
                Rect.MinMaxRect(
                    laneLeft,
                    laneCentreY - (PlayerChip.PlayerChipHeight / 2f),
                    laneLeft + GameBoardLaneView.LaneGutterWidth,
                    laneCentreY + (PlayerChip.PlayerChipHeight / 2f)),
                $"{what}: the chip");

            AssertBounds(
                lane.TrackRect,
                canvas,
                Rect.MinMaxRect(trackLeft, laneBottom, laneRight, laneTop),
                $"{what}: the track");

            Assert.That(lane.PositionRects.Count, Is.EqualTo(centresX.Length), $"{what}: nine positions");

            for (var position = 0; position < centresX.Length; position++)
            {
                var onSharedLog = position == 0 || position == Lane.LaneWinningPosition;

                var halfWidth = (onSharedLog ? GameBoardScreenView.LogWidth : GameBoardLaneView.LilyPadDiameter) / 2f;
                var halfHeight = (onSharedLog ? GameBoardLaneView.LaneHeight : GameBoardLaneView.LilyPadDiameter) / 2f;

                AssertBounds(
                    lane.PositionRects[position],
                    canvas,
                    Rect.MinMaxRect(
                        centresX[position] - halfWidth,
                        laneCentreY - halfHeight,
                        centresX[position] + halfWidth,
                        laneCentreY + halfHeight),
                    $"{what}: position {position}");
            }

            var half = GameBoardLaneView.FrogPieceDiameter / 2f;

            AssertBounds(
                lane.PieceRect,
                canvas,
                Rect.MinMaxRect(
                    centresX[pieceOn] - half,
                    laneCentreY - half,
                    centresX[pieceOn] + half,
                    laneCentreY + half),
                $"{what}: the frog, on position {pieceOn}");
        }

        static GameBoardScreenView BoardOn(RectTransform canvas, Game game)
        {
            var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                .AddComponent<GameBoardScreenView>();
            view.transform.SetParent(canvas, worldPositionStays: false);
            view.Initialize(game);

            return view;
        }

        static Game TwoFrogGame()
        {
            return new Game(new[] { FrogColour.Green, FrogColour.Blue }, AnySeed);
        }

        static void MoveTo(Game game, FrogColour colour, int position)
        {
            var lane = game.LaneFor(colour);

            for (var step = 0; step < position; step++)
            {
                lane.MoveForward();
            }

            Assert.That(lane.Position, Is.EqualTo(position), "the fixture, not the assertion");
        }

        // The view under a canvas exactly as AppRoot's "Screens" host holds
        // it — the same fixture GameBoardBandsToTheEdgeTests builds.
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
