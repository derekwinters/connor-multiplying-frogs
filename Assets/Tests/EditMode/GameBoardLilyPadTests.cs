using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using Image = UnityEngine.UI.Image;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The lily pad is notched, veined, and varies per pad** — issue #411,
    /// and the section of that name on docs/specs/ui/game-board.md, which is
    /// where every number below is born. Written before the change, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning them red against the plain circles they replace before green
    /// — there is no editor in an agent environment to watch them fail in.
    ///
    /// The twelve-entry table is written out **as literals** rather than read
    /// from the code under test, exactly as
    /// <see cref="GameBoardColoursTests"/> writes out the page's colours: a
    /// test that asserts a table equals itself passes whatever the table is
    /// changed to. These twelve rows are the spec page's, copied by hand, so a
    /// pad cannot change shape without the page changing with it.
    ///
    /// Two of these tests are the issue's own acceptance criteria, asserted
    /// rather than eyeballed:
    ///
    /// - <see cref="AFourFrogBoard_Draws12DistinctSilhouettes_FromFourSprites"/>
    ///   walks all 28 pads and counts what they draw. Twelve silhouettes, four
    ///   sprites — because rotation is free and only a notch *width* costs an
    ///   asset.
    /// - <see cref="APadsShape_DoesNotChange_WhenTheBoardRedrawsOrAFrogMoves"/>
    ///   redraws the board and moves a frog, and compares. The variation is a
    ///   pure function of where the pad is, so nothing is stored and nothing
    ///   is saved.
    /// </summary>
    public sealed class GameBoardLilyPadTests
    {
        const ulong AnySeed = 20260823UL;
        const ulong AnotherSeed = 19991231UL;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const float Tolerance = 0.001f;

        // A byte of slack on a colour read back out of a generated texture:
        // the pad is composited in floats and stored as Color32.
        const float ColourTolerance = 2f / 255f;

        // docs/specs/ui/game-board.md — "The lily pad is notched, veined, and
        // varies per pad", the twelve-entry table, copied by hand. Row `i` is
        // what a pad at `(lane x 5 + position) mod 12 == i` draws.
        static readonly float[] TableNotchWidths =
        {
            20f, 10f, 25f, 15f, 20f, 25f, 10f, 15f, 25f, 15f, 20f, 10f
        };

        static readonly float[] TablePointsAt =
        {
            14f, 212f, 96f, 308f, 175f, 47f, 260f, 131f, 341f, 78f, 238f, 158f
        };

        // The four notch widths of `LilyPadNotchAngles`, which are the only
        // values on that table that cost a sprite.
        static readonly float[] TableNotchAngles = { 10f, 15f, 20f, 25f };

        [Test]
        public void EveryPadsNotch_IsTheTableRowForItsOwnCoordinates()
        {
            // `index = (lane x 5 + position) mod 12`, for the 28 pads a
            // four-frog board draws: four lanes, seven pads each.
            for (var lane = 0; lane < 4; lane++)
            {
                for (var position = 1; position < Lane.LaneWinningPosition; position++)
                {
                    var index = ((lane * 5) + position) % 12;
                    var what = $"lane {lane}, position {position} (row {index})";

                    Assert.That(
                        GameBoardLaneView.LilyPadVariationFor(lane, position),
                        Is.EqualTo(index),
                        what);
                    Assert.That(
                        GameBoardLaneView.LilyPadNotchWidthFor(lane, position),
                        Is.EqualTo(TableNotchWidths[index]).Within(Tolerance),
                        $"{what}: the notch's width");
                    Assert.That(
                        GameBoardLaneView.LilyPadNotchAngleFor(lane, position),
                        Is.EqualTo(TablePointsAt[index]).Within(Tolerance),
                        $"{what}: what the notch points at");
                }
            }

            // Only four widths are ever asked for, and they are the page's own
            // `LilyPadNotchAngles`. This is what makes four sprites enough.
            Assert.That(
                GameBoardLaneView.LilyPadNotchAngles.ToArray(),
                Is.EqualTo(TableNotchAngles),
                "`LilyPadNotchAngles` is the page's 10, 15, 20, 25");
            Assert.That(
                TableNotchWidths.Distinct().OrderBy(width => width).ToArray(),
                Is.EqualTo(TableNotchAngles),
                "and the twelve rows between them use no other width");
        }

        /// <summary>
        /// The issue's first acceptance criterion, counted rather than looked
        /// at: 28 pads, 12 distinct silhouettes, and **four** sprites between
        /// them. The wireframe verified this by extracting the drawn paths;
        /// this asserts the same mapping on the built board.
        /// </summary>
        [Test]
        public void AFourFrogBoard_Draws12DistinctSilhouettes_FromFourSprites()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, FourFrogGame());

                var pads = AllPads(view);

                Assert.That(pads.Count, Is.EqualTo(28), "four lanes of seven lily pads");

                Assert.That(
                    pads.Select(pad => pad.sprite.GetInstanceID()).Distinct().Count(),
                    Is.EqualTo(GameBoardLaneView.LilyPadNotchAngles.Count),
                    "four sprites, one per notch width — the pointing angle is a rotation, not an asset");

                Assert.That(
                    pads.Select(SilhouetteOf).Distinct().Count(),
                    Is.EqualTo(12),
                    "and twelve silhouettes between them, one per row of the table");

                // Which silhouette is on which pad, against the table itself.
                for (var lane = 0; lane < view.Lanes.Count; lane++)
                {
                    for (var position = 1; position < Lane.LaneWinningPosition; position++)
                    {
                        var index = ((lane * 5) + position) % 12;
                        var pad = view.Lanes[lane].LilyPads[position - 1];
                        var what = $"lane {lane}, position {position} (row {index})";

                        Assert.That(
                            pad.sprite,
                            Is.SameAs(SpriteForWidth(pads, TableNotchWidths[index])),
                            $"{what}: draws the sprite for a {TableNotchWidths[index]} degree notch");

                        // The page measures its angles the way the mockup's
                        // SVG does — 0 points right along the lane, 90 points
                        // down — and a uGUI z-rotation is the other way about,
                        // so the pad is turned by minus the table's angle.
                        Assert.That(
                            Quaternion.Angle(
                                pad.rectTransform.localRotation,
                                Quaternion.Euler(0f, 0f, -TablePointsAt[index])),
                            Is.LessThan(0.01f),
                            $"{what}: points at {TablePointsAt[index]} degrees");
                    }
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        [Test]
        public void EveryPad_HasFiveVeins_SymmetricAboutItsOwnNotch()
        {
            foreach (var width in GameBoardLaneView.LilyPadNotchAngles)
            {
                var veins = GameBoardLaneView.LilyPadVeinAnglesFor(width);

                Assert.That(
                    veins.Count,
                    Is.EqualTo(GameBoardLaneView.LilyPadVeinCount),
                    $"a {width} degree pad has `LilyPadVeinCount` veins");
                Assert.That(veins.Count, Is.EqualTo(5), "which the page puts at five");

                // Measured from the notch's own axis, so the middle one runs
                // straight out opposite the notch and two pairs straddle it.
                Assert.That(veins[2], Is.EqualTo(180f).Within(Tolerance), "one vein opposite the notch");

                for (var side = 1; side <= 2; side++)
                {
                    Assert.That(
                        veins[2 + side] - 180f,
                        Is.EqualTo(180f - veins[2 - side]).Within(Tolerance),
                        $"a {width} degree pad's veins are symmetric about the notch's axis");
                }

                // The five veins and the notch divide the circle into six
                // equal parts — game-board.md, and what the mockup draws.
                var spacing = (360f - width) / (GameBoardLaneView.LilyPadVeinCount + 1);

                for (var vein = 1; vein < veins.Count; vein++)
                {
                    Assert.That(
                        veins[vein] - veins[vein - 1],
                        Is.EqualTo(spacing).Within(Tolerance),
                        $"a {width} degree pad's veins are {spacing} degrees apart");
                }
            }
        }

        /// <summary>
        /// The veins are on the pad, not only in the arithmetic: read back out
        /// of the pixels the sprite is made of, in `LilyPadEdge` at
        /// `LilyPadVeinOpacity` over `LilyPadGreen`, with clean green between
        /// them.
        /// </summary>
        [Test]
        public void ThePadsSprite_DrawsFiveVeins_InLilyPadEdgeAtHalfOpacity()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, FourFrogGame());

                for (var lane = 0; lane < view.Lanes.Count; lane++)
                {
                    for (var position = 1; position < Lane.LaneWinningPosition; position++)
                    {
                        var pad = view.Lanes[lane].LilyPads[position - 1];
                        var width = GameBoardLaneView.LilyPadNotchWidthFor(lane, position);
                        var veins = GameBoardLaneView.LilyPadVeinAnglesFor(width);
                        var spacing = (360f - width) / (GameBoardLaneView.LilyPadVeinCount + 1);
                        var pixels = pad.sprite.texture.GetPixels32();
                        var size = pad.sprite.texture.width;

                        // The sprite is drawn with its notch pointing at 0 —
                        // the rotation above is what aims it — so every angle
                        // here is measured from the notch's own axis.
                        var expectedVein = Color.Lerp(
                            BoardColours.LilyPadGreen,
                            BoardColours.LilyPadEdge,
                            GameBoardLaneView.LilyPadVeinOpacity);

                        foreach (var vein in veins)
                        {
                            AssertPixel(
                                pixels,
                                size,
                                vein,
                                expectedVein,
                                $"lane {lane}, position {position}: the vein at {vein} degrees");

                            AssertPixel(
                                pixels,
                                size,
                                vein + (spacing / 2f),
                                BoardColours.LilyPadGreen,
                                $"lane {lane}, position {position}: clean green between veins");
                        }
                    }
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The issue's second acceptance criterion, asserted by redrawing and
        /// hopping rather than by inspection. The variation is a pure function
        /// of the pad's coordinates: nothing is stored, so there is nothing to
        /// come back different.
        /// </summary>
        [Test]
        public void APadsShape_DoesNotChange_WhenTheBoardRedrawsOrAFrogMoves()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var game = FourFrogGame();
                var view = BoardOn(canvas, game);

                var before = AllPads(view).Select(SilhouetteOf).ToArray();

                view.Refresh();
                Assert.That(AllPads(view).Select(SilhouetteOf).ToArray(), Is.EqualTo(before), "a redraw");

                // A frog moves, and is drawn part-way through its move and
                // then at rest on the pad it landed on.
                var lane = view.Lanes[0];
                lane.PlacePiecePartWay(1, 2, 0.5f);
                Assert.That(AllPads(view).Select(SilhouetteOf).ToArray(), Is.EqualTo(before), "mid-move");

                MoveTo(game, lane.Colour, 2);
                view.Refresh();
                Assert.That(AllPads(view).Select(SilhouetteOf).ToArray(), Is.EqualTo(before), "after the move");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// And the same coordinates give the same silhouette in a different
        /// game, from a different seed, on a board built from scratch — which
        /// is what "derived, not random" means, and why none of this is in the
        /// save file (ADR-0004).
        /// </summary>
        [Test]
        public void TheSameCoordinates_GiveTheSameSilhouette_InADifferentGameEntirely()
        {
            var first = CanvasOf(CanvasWidth, CanvasHeight);
            var second = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var one = BoardOn(first, FourFrogGame());
                var another = BoardOn(second, new Game(
                    new[] { FrogColour.Pink, FrogColour.Orange, FrogColour.Blue, FrogColour.Green },
                    AnotherSeed));

                Assert.That(
                    AllPads(another).Select(SilhouetteOf).ToArray(),
                    Is.EqualTo(AllPads(one).Select(SilhouetteOf).ToArray()),
                    "a different game, in a different turn order, from a different seed — the same 28 pads");

                // Nothing about the pad's shape is serialized on the view
                // either: it is recomputed from the coordinates every time the
                // board is built.
                Assert.That(
                    typeof(GameBoardLaneView)
                        .GetFields(System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.DeclaredOnly)
                        .Where(field => field.IsDefined(typeof(SerializeField), inherit: true))
                        .Select(field => field.Name)
                        .ToArray(),
                    Is.Empty,
                    "the lane serializes nothing, so a pad's shape cannot be saved with the game");
            }
            finally
            {
                DestroyCanvas(first);
                DestroyCanvas(second);
            }
        }

        /// <summary>
        /// The pad's drawing turns; its place on the lane does not. The
        /// position rect is what the frog is parented to and what
        /// <see cref="GameBoardElasticPondTests"/> measures the whole row
        /// against, so it stays square to the board and
        /// <c>LilyPadDiameter</c> across.
        /// </summary>
        [Test]
        public void ThePadArtTurns_ButThePositionItSitsOnDoesNot()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, FourFrogGame());

                Assert.That(
                    GameBoardLaneView.LilyPadDiameter,
                    Is.EqualTo(112f).Within(Tolerance),
                    "the pad is still 112 px across — this issue moved the outline and the surface, not the size");

                foreach (var lane in view.Lanes)
                {
                    Assert.That(lane.LilyPads.Count, Is.EqualTo(Lane.LanePositionCount - 2), "seven lily pads");

                    for (var position = 1; position < Lane.LaneWinningPosition; position++)
                    {
                        var pad = lane.LilyPads[position - 1];
                        var seat = lane.PositionRects[position];

                        Assert.That(pad.rectTransform.parent, Is.SameAs(seat.transform), $"position {position}");
                        Assert.That(
                            Quaternion.Angle(seat.localRotation, Quaternion.identity),
                            Is.LessThan(Tolerance),
                            $"position {position}'s own rect is square to the board");
                        Assert.That(
                            pad.rectTransform.rect.size,
                            Is.EqualTo(new Vector2(
                                GameBoardLaneView.LilyPadDiameter,
                                GameBoardLaneView.LilyPadDiameter)),
                            $"position {position}'s art fills its pad");
                        Assert.That(pad.sprite, Is.Not.Null, $"position {position} has art at all");
                    }
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        // Every pad on the board, lane by lane and left to right.
        static List<Image> AllPads(GameBoardScreenView view)
        {
            return view.Lanes.SelectMany(lane => lane.LilyPads).ToList();
        }

        // What a pad draws: its sprite, and the angle it is turned to. Two
        // pads with the same pair are the same silhouette.
        static string SilhouetteOf(Image pad)
        {
            var turn = Mathf.Round(pad.rectTransform.localEulerAngles.z * 10f) / 10f;

            return pad.sprite.GetInstanceID().ToString(CultureInfo.InvariantCulture)
                + "@"
                + turn.ToString("0.0", CultureInfo.InvariantCulture);
        }

        static Sprite SpriteForWidth(IEnumerable<Image> pads, float width)
        {
            // The sprite the board itself drew for a pad with this notch
            // width — found on the board rather than generated a second time,
            // so this asserts what is on screen.
            var pad = pads.First(candidate => Mathf.Abs(WidthOf(candidate) - width) < Tolerance);

            return pad.sprite;
        }

        static float WidthOf(Image pad)
        {
            var lane = pad.GetComponentInParent<GameBoardLaneView>();
            var position = lane.LilyPads.ToList().IndexOf(pad) + 1;

            return GameBoardLaneView.LilyPadNotchWidthFor(lane.LaneIndex, position);
        }

        // Where on the pad the veins are sampled: far enough out to be well
        // clear of `LilyPadVeinInset`, far enough in to be well clear of the
        // rim and of `LilyPadVeinOutset`.
        const float VeinSampleRadius = 0.6f;

        // One pixel of a pad's own texture, at an angle measured from the
        // notch's axis, `VeinSampleRadius` of the way out from its centre.
        // The page measures angles the way the mockup's SVG does — 0 right,
        // 90 down — and a texture's rows run the other way, so y is flipped.
        static void AssertPixel(Color32[] pixels, int size, float degrees, Color expected, string what)
        {
            var radius = size / 2f;
            var radians = degrees * Mathf.Deg2Rad;

            var x = Mathf.FloorToInt(radius + (VeinSampleRadius * radius * Mathf.Cos(radians)));
            var y = Mathf.FloorToInt(radius - (VeinSampleRadius * radius * Mathf.Sin(radians)));

            var actual = (Color)pixels[(y * size) + x];

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColourTolerance), $"{what}: red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColourTolerance), $"{what}: green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColourTolerance), $"{what}: blue");
            Assert.That(actual.a, Is.EqualTo(1f).Within(ColourTolerance), $"{what}: opaque");
        }

        static Game FourFrogGame()
        {
            return new Game(
                new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink },
                AnySeed);
        }

        static void MoveTo(Game game, FrogColour colour, int position)
        {
            var lane = game.LaneFor(colour);

            while (lane.Position < position)
            {
                lane.MoveForward();
            }

            Assert.That(lane.Position, Is.EqualTo(position), "the fixture, not the assertion");
        }

        static GameBoardScreenView BoardOn(RectTransform canvas, Game game)
        {
            var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                .AddComponent<GameBoardScreenView>();
            view.transform.SetParent(canvas, worldPositionStays: false);
            view.Initialize(game);

            return view;
        }

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
    }
}
