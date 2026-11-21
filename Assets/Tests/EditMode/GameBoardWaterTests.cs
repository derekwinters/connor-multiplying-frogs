using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.EditorTools;
using Frogs.Unity.Views;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The water's marks** — issue #471, and the section of that name on
    /// docs/specs/ui/game-board.md, PW-1 to PW-5 and its three invariants: the
    /// water is one seamless tile, `PondWater` with see-through white marks,
    /// repeated wherever the board paints water, drifting left behind every
    /// piece, and never taking a tap.
    ///
    /// Written before the tile was wired in, per docs/engineering/testing.md's
    /// sanctioned flow: pushed unexecuted, with CI turning them red against
    /// the flat fill they replace before green — there is no editor in an
    /// agent environment to watch them fail in. The drift's arithmetic is
    /// Core's and is tested in the fast suite (PondWaterDriftTests); these
    /// test what the board does with it.
    ///
    /// Numbers are the spec page's, written out here by hand rather than read
    /// from the code under test.
    /// </summary>
    public sealed class GameBoardWaterTests
    {
        const ulong AnySeed = 20261121UL;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // A screen wider than 16:10, to prove the water reaches past the
        // reference canvas — the same 2400 px FullBleedBackgroundTests uses.
        const float WideCanvasWidth = 2400f;

        // game-board.md's named constants, copied by hand.
        const float BoardControlsHeight = 176f;
        const float PondWaterDriftSpeed = 25f;
        const float PondWaterTileSize = 1024f;
        const int PondWaterTilePixels = 512;

        // `PondWater`, from game-board.md § Colours, copied by hand.
        static readonly Color32 PondWater = new Color32(0x9F, 0xD8, 0xF2, 0xFF);

        const float Tolerance = 0.01f;
        const float UvTolerance = 0.0001f;

        /// <summary>
        /// The tile's import settings, pinned in code by
        /// <see cref="WaterTileImport"/>, read back from the importer rather
        /// than assumed.
        /// </summary>
        [Test]
        public void TheTile_IsImportedToRepeat_Uncompressed_AtItsOwnSize()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(WaterTileImport.TilePath);

            Assert.That(importer, Is.Not.Null, WaterTileImport.TilePath + " is imported");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True, "a colour image");
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None), "opaque everywhere");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), "PW-1: the tile is drawn many times over by asking for UVs past 1");
            Assert.That(importer.mipmapEnabled, Is.False, "only ever drawn at its own size or larger");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.maxTextureSize, Is.EqualTo(PondWaterTilePixels), "its own size");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "PW-2: the water between the marks is exactly PondWater, not a compressed approximation of it");
            Assert.That(importer.isReadable, Is.False, "nothing reads its pixels at runtime");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTileImport.TilePath);

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(PondWaterTilePixels));
            Assert.That(texture.height, Is.EqualTo(PondWaterTilePixels));
        }

        /// <summary>
        /// PW-2 and the first invariant: the tile is `PondWater` with white
        /// marks on it. Read out of the committed PNG's own pixels, because
        /// the imported texture is not readable.
        /// </summary>
        [Test]
        public void TheTile_IsPondWater_WithSeeThroughWhiteMarksOnIt()
        {
            var pixels = TilePixels(out var width, out var height);

            Assert.That(width, Is.EqualTo(PondWaterTilePixels));
            Assert.That(height, Is.EqualTo(PondWaterTilePixels));

            var water = pixels.Count(pixel => Same(pixel, PondWater));

            Assert.That(
                water,
                Is.GreaterThan(pixels.Length * 9 / 10),
                "between the marks the water is exactly PondWater, and at a calm density that is most of the tile");

            var marks = 0;

            foreach (var pixel in pixels)
            {
                // The soft edge of a downsampled stroke rings a few levels
                // below PondWater; anything more would be a tint.
                Assert.That(pixel.r, Is.GreaterThanOrEqualTo(PondWater.r - 10), "nothing darkens the water");
                Assert.That(pixel.g, Is.GreaterThanOrEqualTo(PondWater.g - 10), "nothing darkens the water");
                Assert.That(pixel.b, Is.GreaterThanOrEqualTo(PondWater.b - 10), "nothing darkens the water");

                // How far each channel has gone from PondWater towards white.
                // A white mark moves all three the same fraction of the way.
                var towardsWhite = new[]
                {
                    (pixel.r - PondWater.r) / (255f - PondWater.r),
                    (pixel.g - PondWater.g) / (255f - PondWater.g),
                    (pixel.b - PondWater.b) / (255f - PondWater.b),
                };

                if (towardsWhite.Min() > 0.3f)
                {
                    marks++;

                    Assert.That(towardsWhite.Max() - towardsWhite.Min(), Is.LessThan(0.15f), "a mark is white over the water, not another colour");
                    Assert.That(towardsWhite.Max(), Is.LessThan(0.95f), "the marks are see-through: the water shows through them");
                }
            }

            Assert.That(marks, Is.GreaterThan(0), "the tile has marks on it");
        }

        /// <summary>
        /// PW-1: one seamless tile. A mark that runs off one edge comes back
        /// in on the opposite one, so a row of tiles reads as one picture.
        ///
        /// Within 2 px, and not 0, deliberately: the approved generator draws
        /// each of a mark's wrapped copies with its own 4 or 5 px stroke, so a
        /// mark crossing the left and right edges can be 1 px thicker on one
        /// side of the join than the other — see tools/art/water_tile.py.
        /// </summary>
        [Test]
        public void TheTile_IsSeamless_EveryMarkThatLeavesAnEdgeComesBackOnTheOppositeOne()
        {
            var pixels = TilePixels(out var width, out var height);
            const int Slack = 2;

            bool Marked(int x, int y)
            {
                var pixel = pixels[(y * width) + x];
                return System.Math.Abs(pixel.r - PondWater.r) + System.Math.Abs(pixel.g - PondWater.g) + System.Math.Abs(pixel.b - PondWater.b) > 24;
            }

            // Marked within Slack px of (x, y), along the edge being checked.
            bool MarkedNear(int x, int y, bool alongColumn)
            {
                for (var d = -Slack; d <= Slack; d++)
                {
                    var nx = alongColumn ? x : x + d;
                    var ny = alongColumn ? y + d : y;

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && Marked(nx, ny))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (var y = 0; y < height; y++)
            {
                if (Marked(0, y))
                {
                    Assert.That(MarkedNear(width - 1, y, alongColumn: true), Is.True, $"a mark at the left edge, row {y}, carries on from the right edge");
                }

                if (Marked(width - 1, y))
                {
                    Assert.That(MarkedNear(0, y, alongColumn: true), Is.True, $"a mark at the right edge, row {y}, carries on at the left edge");
                }
            }

            for (var x = 0; x < width; x++)
            {
                if (Marked(x, 0))
                {
                    Assert.That(MarkedNear(x, height - 1, alongColumn: false), Is.True, $"a mark at the top edge, column {x}, carries on from the bottom edge");
                }

                if (Marked(x, height - 1))
                {
                    Assert.That(MarkedNear(x, 0, alongColumn: false), Is.True, $"a mark at the bottom edge, column {x}, carries on at the top edge");
                }
            }
        }

        /// <summary>
        /// PW-1: the tile is what the board paints wherever it used to paint
        /// flat `PondWater` — the background behind everything, and the pond
        /// band's own fill — and each reaches every edge of its surface. The
        /// first invariant: it is drawn untinted, with uGUI's own material,
        /// so what is on screen is the tile's own `PondWater`.
        /// </summary>
        [Test]
        public void TheWater_IsTheTile_OnTheBackgroundAndThePond_Untinted_WithTheDefaultMaterial()
        {
            var canvas = CanvasOf(WideCanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 4);
                var tile = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTileImport.TilePath);

                foreach (var water in Waters(view))
                {
                    var what = water.Key;
                    var surface = water.Value;

                    Assert.That(surface, Is.Not.Null, $"{what} is drawn");
                    Assert.That(surface.texture, Is.SameAs(tile), $"{what} is PondWaterTile");
                    Assert.That((Color32)surface.color, Is.EqualTo(new Color32(255, 255, 255, 255)), $"{what} is not tinted, darkened or lightened");
                    Assert.That(surface.material, Is.SameAs(surface.defaultMaterial), $"{what} is drawn with uGUI's default material — no shader of its own");
                    Assert.That(surface.enabled && surface.gameObject.activeInHierarchy, Is.True, $"{what} is shown");
                }

                AssertCovers(view.BackgroundWater.rectTransform, canvas, "the background's water reaches every edge of the screen");
                AssertCovers(view.PondBandWater.rectTransform, view.PondRect, "the pond's water fills the pond band");

                // The flat fills stay underneath, exactly PondWater: they are
                // what the tile's own water is, and what the colour checks
                // in GameBoardColoursTests measure.
                Assert.That((Color32)view.BackgroundImage.color, Is.EqualTo(PondWater));
                Assert.That((Color32)view.PondRect.GetComponent<Image>().color, Is.EqualTo(PondWater));
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// PW-5: behind everything. The background's water is drawn before
        /// the three bands, and the pond's water before every log, lane, pad
        /// and frog in the pond — uGUI paints in hierarchy order.
        /// </summary>
        [Test]
        public void TheWater_IsDrawnBehindEveryBandLogLaneAndFrog()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 4);

                Assert.That(view.BackgroundWater.transform.parent, Is.SameAs(view.BackgroundImage.transform), "the background's water is part of the background");
                Assert.That(
                    view.BackgroundImage.transform.GetSiblingIndex(),
                    Is.LessThan(view.HeaderRect.GetSiblingIndex()),
                    "the background is drawn before the header");
                Assert.That(
                    view.BackgroundImage.transform.GetSiblingIndex(),
                    Is.LessThan(view.PondRect.GetSiblingIndex()),
                    "the background is drawn before the pond");
                Assert.That(
                    view.BackgroundImage.transform.GetSiblingIndex(),
                    Is.LessThan(view.ControlsRect.GetSiblingIndex()),
                    "the background is drawn before the controls");

                Assert.That(view.PondBandWater.transform.parent, Is.SameAs(view.PondRect), "the pond's water is the pond's");
                Assert.That(
                    view.PondBandWater.transform.GetSiblingIndex(),
                    Is.EqualTo(0),
                    "the pond's water is the first thing drawn in the pond, so every log, lane, pad and frog is drawn over it");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// PW-3: a slow drift left at 25 px a second, sideways only. The
        /// window onto the tile moves right by 25 px of a 1024 px tile every
        /// second, which is what moves the marks left on screen; and each
        /// surface shows as many tiles as it is big.
        /// </summary>
        [Test]
        public void TheWater_DriftsLeftAtTwentyFivePixelsASecond()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 2);

                view.ShowWaterAt(0d);
                var pondAtStart = view.PondBandWater.uvRect;
                var backgroundAtStart = view.BackgroundWater.uvRect;

                view.ShowWaterAt(1d);
                var pondASecondLater = view.PondBandWater.uvRect;

                Assert.That(
                    pondASecondLater.x - pondAtStart.x,
                    Is.EqualTo(PondWaterDriftSpeed / PondWaterTileSize).Within(UvTolerance),
                    "the marks move 25 px to the left in a second");
                Assert.That(pondASecondLater.y, Is.EqualTo(pondAtStart.y).Within(UvTolerance), "sideways only");

                Assert.That(pondAtStart.width, Is.EqualTo(CanvasWidth / PondWaterTileSize).Within(UvTolerance), "one tile is 1024 px across");
                Assert.That(pondAtStart.height, Is.EqualTo(view.PondRect.rect.height / PondWaterTileSize).Within(UvTolerance), "and 1024 px tall");
                Assert.That(backgroundAtStart.height, Is.EqualTo(CanvasHeight / PondWaterTileSize).Within(UvTolerance));

                // PW-1: the two surfaces are one picture. The pond starts
                // BoardControlsHeight up the screen, so its window starts that
                // far up the tile from the background's.
                Assert.That(pondAtStart.x, Is.EqualTo(backgroundAtStart.x).Within(UvTolerance), "no seam between the pond's water and the background's");
                Assert.That(
                    pondAtStart.y - backgroundAtStart.y,
                    Is.EqualTo(BoardControlsHeight / PondWaterTileSize).Within(UvTolerance),
                    "no seam between the pond's water and the background's");

                // A whole tile on, the picture is the one it started on.
                view.ShowWaterAt(PondWaterTileSize / PondWaterDriftSpeed);
                var aTileLater = view.PondBandWater.uvRect;

                Assert.That(
                    Mathf.Repeat(aTileLater.x - pondAtStart.x + 0.5f, 1f) - 0.5f,
                    Is.EqualTo(0f).Within(UvTolerance),
                    "it loops without a jump");
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The second invariant: the drift never moves a piece. Logs, lily
        /// pads and frogs stay exactly where the layout put them; only the
        /// texture's window changes.
        /// </summary>
        [Test]
        public void TheDrift_NeverMovesAPiece()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 4);

                view.ShowWaterAt(0d);
                var before = PiecePositions(view);

                view.ShowWaterAt(17.3d);
                var after = PiecePositions(view);

                Assert.That(after.Keys, Is.EquivalentTo(before.Keys));

                foreach (var piece in before)
                {
                    Assert.That(
                        Vector3.Distance(after[piece.Key], piece.Value),
                        Is.LessThan(Tolerance),
                        $"{piece.Key} has not moved");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// The third invariant: the water never takes input, so it never
        /// blocks a tap on anything drawn over it.
        /// </summary>
        [Test]
        public void TheWater_NeverTakesInput()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 2);

                foreach (var water in Waters(view))
                {
                    Assert.That(water.Value.raycastTarget, Is.False, $"{water.Key} is not something to tap");
                }

                Assert.That(view.BackgroundWater.GetComponent<Selectable>(), Is.Null);
                Assert.That(view.PondBandWater.GetComponent<Selectable>(), Is.Null);
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        static Dictionary<string, RawImage> Waters(GameBoardScreenView view)
        {
            return new Dictionary<string, RawImage>
            {
                { "the background's water", view.BackgroundWater },
                { "the pond's water", view.PondBandWater },
            };
        }

        static Dictionary<string, Vector3> PiecePositions(GameBoardScreenView view)
        {
            var positions = new Dictionary<string, Vector3>
            {
                { "the Start log", view.StartLog.rectTransform.position },
                { "the End log", view.EndLog.rectTransform.position },
            };

            foreach (var lane in view.Lanes)
            {
                positions.Add($"{lane.Colour}'s frog", lane.PieceRect.position);

                for (var i = 0; i < lane.LilyPads.Count; i++)
                {
                    positions.Add($"{lane.Colour}'s pad {i + 1}", lane.LilyPads[i].rectTransform.position);
                }
            }

            return positions;
        }

        static Color32[] TilePixels(out int width, out int height)
        {
            return LilyPadAlbedo.DecodePng(File.ReadAllBytes(WaterTileImport.TilePath), out width, out height);
        }

        static bool Same(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        static void AssertCovers(RectTransform inner, RectTransform outer, string message)
        {
            var a = BoundsIn(inner, outer);
            var b = outer.rect;

            Assert.That(a.xMin, Is.LessThanOrEqualTo(b.xMin + Tolerance), message + " (left)");
            Assert.That(a.xMax, Is.GreaterThanOrEqualTo(b.xMax - Tolerance), message + " (right)");
            Assert.That(a.yMin, Is.LessThanOrEqualTo(b.yMin + Tolerance), message + " (bottom)");
            Assert.That(a.yMax, Is.GreaterThanOrEqualTo(b.yMax - Tolerance), message + " (top)");
        }

        static Rect BoundsIn(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var min = space.InverseTransformPoint(corners[0]);
            var max = space.InverseTransformPoint(corners[2]);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static GameBoardScreenView BoardOn(RectTransform canvas, int frogCount)
        {
            var colours = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                .AddComponent<GameBoardScreenView>();
            view.transform.SetParent(canvas, worldPositionStays: false);
            view.Initialize(new Game(colours.Take(frogCount).ToArray(), AnySeed));

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
            Object.DestroyImmediate(canvas.gameObject);
        }
    }
}
