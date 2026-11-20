using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.EditorTools;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using LilyPadModel = Frogs.Unity.UI.LilyPadModel;

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
    /// Since #465 a pad is the approved 3D model (LP-1 to LP-5 on that page):
    /// one of four meshes, textured with an albedo that carries the rim and
    /// the veins. These tests were rewritten against it before it was wired
    /// in, and pushed unexecuted for the same reason. The veins are now read
    /// out of that albedo — the committed PNG — rather than out of a
    /// generated sprite.
    ///
    /// Two of these tests are #411's acceptance criteria, asserted rather than
    /// eyeballed:
    ///
    /// - <see cref="AFourFrogBoard_Draws12DistinctSilhouettes_FromFourMeshes"/>
    ///   walks all 28 pads and counts what they draw. Twelve silhouettes, four
    ///   meshes — because rotation is free and only a notch *width* costs an
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

        // How far a colour painted on the approved albedo may be from the
        // page's own: the model's fill is LilyPadGreen lightened by up to 4%
        // towards the middle and darkened by up to 6% towards the rim
        // (game-board.md, LP-4), and a vein is LilyPadEdge at half opacity
        // over that.
        const float ColourTolerance = 8f / 255f;

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
        // values on that table that cost a mesh.
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
            // `LilyPadNotchAngles`. This is what makes four meshes enough.
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
        /// #411's first acceptance criterion, counted rather than looked at:
        /// 28 pads, 12 distinct silhouettes, and **four** meshes between them
        /// (LP-1). The wireframe verified this by extracting the drawn paths;
        /// this asserts the same mapping on the built board.
        /// </summary>
        [Test]
        public void AFourFrogBoard_Draws12DistinctSilhouettes_FromFourMeshes()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, FourFrogGame());

                var pads = AllPads(view);

                Assert.That(pads.Count, Is.EqualTo(28), "four lanes of seven lily pads");

                Assert.That(
                    pads.Select(pad => pad.Mesh.GetInstanceID()).Distinct().Count(),
                    Is.EqualTo(GameBoardLaneView.LilyPadNotchAngles.Count),
                    "four meshes, one per notch width — the pointing angle is a rotation, not an asset");

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
                            pad.Mesh,
                            Is.SameAs(MeshForWidth(pads, TableNotchWidths[index])),
                            $"{what}: draws the same mesh as every {TableNotchWidths[index]} degree pad");
                        Assert.That(
                            pad.Mesh.name,
                            Is.EqualTo("LilyPad_Notch" + TableNotchWidths[index].ToString("00", CultureInfo.InvariantCulture)),
                            $"{what}: and it is the model's mesh for that width");

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
        /// of the approved model's albedo (LP-4), one quadrant per notch
        /// width, in `LilyPadEdge` at `LilyPadVeinOpacity` over the fill, with
        /// clean green between them.
        /// </summary>
        [Test]
        public void TheModelsAlbedo_DrawsFiveVeins_InLilyPadEdgeAtHalfOpacity()
        {
            var expectedVein = Color.Lerp(
                BoardColours.LilyPadGreen,
                BoardColours.LilyPadEdge,
                GameBoardLaneView.LilyPadVeinOpacity);

            foreach (var width in GameBoardLaneView.LilyPadNotchAngles)
            {
                var veins = GameBoardLaneView.LilyPadVeinAnglesFor(width);
                var spacing = (360f - width) / (GameBoardLaneView.LilyPadVeinCount + 1);

                foreach (var vein in veins)
                {
                    var what = $"a {width} degree pad: the vein at {vein} degrees from its notch";

                    // Along the vein's own ray, at three radii. That one ray is
                    // inked the whole way along is what says the fan radiates
                    // from the circle's geometric centre rather than from the
                    // notch's apex.
                    foreach (var radius in AlongTheVein)
                    {
                        AssertPixel(width, vein, radius, expectedVein, $"{what}, {radius:0.00} out");
                    }

                    // And it stops short at both ends — `LilyPadVeinInset` at
                    // the centre, `LilyPadVeinOutset` at the rim.
                    AssertTheVeinsEnds(width, vein, expectedVein, what);

                    AssertPixel(
                        width,
                        vein + (spacing / 2f),
                        VeinSampleRadius,
                        BoardColours.LilyPadGreen,
                        $"a {width} degree pad: clean green between veins");
                }

                // Inside the notch is not the leaf: the rim colour the script
                // paints everything outside the pad with, never the fill.
                AssertPixel(width, 0f, NotchSampleRadius, BoardColours.LilyPadEdge, $"a {width} degree pad: its notch points along its own axis");
            }
        }

        /// <summary>
        /// LP-6: the model's import settings, pinned in code by
        /// <see cref="LilyPadModelImport"/>, read back from the importers
        /// rather than assumed.
        /// </summary>
        [Test]
        public void TheModel_IsImportedAsFourReadableStaticMeshes_WithNoMaterial()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(LilyPadModelImport.ModelPath);

            Assert.That(importer, Is.Not.Null, LilyPadModelImport.ModelPath + " is imported");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None), "no rig");
            Assert.That(importer.importAnimation, Is.False, "no clips");
            Assert.That(importer.isReadable, Is.True, "each mesh is laid flat on the CPU to draw it into the canvas");
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), "no lit material, so no lit shader to go missing from a build");
            Assert.That(importer.addCollider, Is.False, "the project has no 3D physics module");

            var meshes = AssetDatabase.LoadAllAssetsAtPath(LilyPadModelImport.ModelPath)
                .OfType<Mesh>()
                .Select(mesh => mesh.name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                meshes,
                Is.EqualTo(new[] { "LilyPad_Notch10", "LilyPad_Notch15", "LilyPad_Notch20", "LilyPad_Notch25" }),
                "LP-1: four meshes, one per `LilyPadNotchAngles` entry");

            var texture = (TextureImporter)AssetImporter.GetAtPath(LilyPadModelImport.AlbedoPath);

            Assert.That(texture, Is.Not.Null, LilyPadModelImport.AlbedoPath + " is imported");
            Assert.That(texture.sRGBTexture, Is.True);
            Assert.That(texture.mipmapEnabled, Is.True, "a 2048 px atlas drawn 112 px a pad");
            Assert.That(texture.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "the page's colours, not a compressed approximation of them");
        }

        /// <summary>
        /// LP-2, LP-3 and LP-5: what a pad hands uGUI. Its triangles fill
        /// exactly its 112 px rectangle; the model's notch points along the
        /// rectangle's +x before the rectangle is turned, so turning it by the
        /// table's angle points the notch where the table says; and it is
        /// textured with the albedo, untinted, with uGUI's default material.
        /// </summary>
        [Test]
        public void EachPad_DrawsItsMeshFlat_FillingItsPad_WithTheNotchAlongItsOwnAxis()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, FourFrogGame());

                foreach (var pad in AllPads(view).GroupBy(pad => pad.NotchWidth).Select(group => group.First()))
                {
                    var what = $"a {pad.NotchWidth} degree pad";
                    var triangles = TrianglesOf(pad);
                    var rect = pad.rectTransform.rect;
                    var half = GameBoardLaneView.LilyPadDiameter / 2f;

                    Assert.That(triangles.Count, Is.GreaterThan(0), $"{what} draws something");

                    var furthest = triangles.SelectMany(t => t).Max(point => (point - rect.center).magnitude);

                    Assert.That(furthest, Is.EqualTo(half).Within(Tolerance + 0.01f), $"{what}: its rim reaches the edge of its pad and no further");

                    Assert.That(Covers(triangles, pad.RectPointOf(180f, VeinSampleRadius)), Is.True, $"{what}: the leaf is opposite its notch");
                    Assert.That(Covers(triangles, pad.RectPointOf(0f, NotchSampleRadius)), Is.False, $"{what}: the notch is along the rectangle's +x");
                    Assert.That(Covers(triangles, pad.RectPointOf(0f, 0.05f)), Is.True, $"{what}: the notch stops short of the middle (`LilyPadNotchDepth`)");

                    Assert.That(pad.mainTexture, Is.Not.Null, $"{what} is textured");
                    Assert.That(pad.mainTexture.name, Is.EqualTo("lilypad_albedo"), $"{what}: with the model's albedo");
                    Assert.That(pad.color, Is.EqualTo(Color.white), $"{what}: untinted — its colours are the albedo's");
                    Assert.That(pad.material, Is.SameAs(Graphic.defaultGraphicMaterial), $"{what}: with uGUI's default material");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// #411's second acceptance criterion, asserted by redrawing and
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
                        Assert.That(pad.Mesh, Is.Not.Null, $"position {position} has art at all");
                    }
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        // Every pad on the board, lane by lane and left to right.
        static List<LilyPadModel> AllPads(GameBoardScreenView view)
        {
            return view.Lanes.SelectMany(lane => lane.LilyPads).ToList();
        }

        // What a pad draws: its mesh, and the angle it is turned to. Two
        // pads with the same pair are the same silhouette.
        static string SilhouetteOf(LilyPadModel pad)
        {
            var turn = Mathf.Round(pad.rectTransform.localEulerAngles.z * 10f) / 10f;

            return pad.Mesh.GetInstanceID().ToString(CultureInfo.InvariantCulture)
                + "@"
                + turn.ToString("0.0", CultureInfo.InvariantCulture);
        }

        static Mesh MeshForWidth(IEnumerable<LilyPadModel> pads, float width)
        {
            // The mesh the board itself drew for a pad with this notch width
            // — found on the board rather than loaded a second time, so this
            // asserts what is on screen.
            var pad = pads.First(candidate => Mathf.Abs(WidthOf(candidate) - width) < Tolerance);

            return pad.Mesh;
        }

        static float WidthOf(LilyPadModel pad)
        {
            var lane = pad.GetComponentInParent<GameBoardLaneView>();
            var position = lane.LilyPads.ToList().IndexOf(pad) + 1;

            return GameBoardLaneView.LilyPadNotchWidthFor(lane.LaneIndex, position);
        }

        // The middle of a vein, and three points along one: just outside
        // `LilyPadVeinInset` (0.20), the middle, and just inside where
        // `LilyPadVeinOutset` (0.12) ends it.
        const float VeinSampleRadius = 0.6f;

        static readonly float[] AlongTheVein = { 0.25f, VeinSampleRadius, 0.85f };

        // Inside every notch: 0.6 of the radius out along its axis, even the
        // narrowest (10 degree) notch is about 0.05 of the radius wide on
        // either side — 2.6 px on the board, 21 px in the albedo.
        const float NotchSampleRadius = 0.6f;

        // How far either end of a drawn vein may be from the constant that
        // puts it there, as a fraction of the radius: a vein is stroked with
        // a round cap, which carries its end a little past the constant.
        // The same 2 px the sprite's test allowed, on a 56 px radius.
        const float VeinEndTolerance = 2f / 56f;

        /// <summary>
        /// Where the vein at this angle starts and stops, walked a pixel at a
        /// time along its own ray in the albedo, against the two constants
        /// that put its ends there. Only the first unbroken inked run counts:
        /// further out the ray crosses the rim, whose antialiased inner edge
        /// passes through the vein's colour on its way.
        /// </summary>
        static void AssertTheVeinsEnds(float notchWidth, float degrees, Color inked, string what)
        {
            var step = 1f / LilyPadAlbedo.RadiusInPixels;
            var nearest = 0f;
            var furthest = 0f;

            for (var along = step; along < 1f; along += step)
            {
                if (IsColour(LilyPadAlbedo.PixelAt(notchWidth, degrees, along), inked))
                {
                    if (nearest == 0f)
                    {
                        nearest = along;
                    }

                    furthest = along;
                    continue;
                }

                if (nearest != 0f)
                {
                    break;
                }
            }

            Assert.That(
                nearest,
                Is.EqualTo(GameBoardLaneView.LilyPadVeinInset).Within(VeinEndTolerance),
                $"{what} starts `LilyPadVeinInset` out from the centre");
            Assert.That(
                furthest,
                Is.EqualTo(1f - GameBoardLaneView.LilyPadVeinOutset).Within(VeinEndTolerance),
                $"{what} stops `LilyPadVeinOutset` short of the rim");
        }

        static bool IsColour(Color pixel, Color expected)
        {
            return Mathf.Abs(pixel.r - expected.r) <= ColourTolerance
                && Mathf.Abs(pixel.g - expected.g) <= ColourTolerance
                && Mathf.Abs(pixel.b - expected.b) <= ColourTolerance;
        }

        static void AssertPixel(float notchWidth, float degrees, float radiusFraction, Color expected, string what)
        {
            var actual = LilyPadAlbedo.PixelAt(notchWidth, degrees, radiusFraction);

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColourTolerance), $"{what}: red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColourTolerance), $"{what}: green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColourTolerance), $"{what}: blue");
        }

        // The triangles a pad hands uGUI, in its own rectangle.
        static List<Vector2[]> TrianglesOf(LilyPadModel pad)
        {
            var vh = new VertexHelper();
            var stream = new List<UIVertex>();

            pad.Draw(vh);
            vh.GetUIVertexStream(stream);
            vh.Dispose();

            var triangles = new List<Vector2[]>();

            for (var i = 0; i + 2 < stream.Count; i += 3)
            {
                triangles.Add(new Vector2[] { stream[i].position, stream[i + 1].position, stream[i + 2].position });
            }

            return triangles;
        }

        static bool Covers(IEnumerable<Vector2[]> triangles, Vector2 point)
        {
            foreach (var t in triangles)
            {
                var d1 = Cross(t[0], t[1], point);
                var d2 = Cross(t[1], t[2], point);
                var d3 = Cross(t[2], t[0], point);

                var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
                var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;

                if (!(hasNegative && hasPositive))
                {
                    return true;
                }
            }

            return false;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p)
        {
            return ((b.x - a.x) * (p.y - a.y)) - ((b.y - a.y) * (p.x - a.x));
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
