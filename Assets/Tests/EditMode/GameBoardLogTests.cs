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
using LogModel = Frogs.Unity.UI.LogModel;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The log model** — issue #468, and the section of that name on
    /// docs/specs/ui/game-board.md, LG-1 to LG-7: each shared log is the
    /// approved Blender log's three pieces, assembled to the log's height by
    /// Core's <see cref="LogAssembly"/> and drawn from straight above into the
    /// log's own rectangle, with `Start` / `End` still readable on top.
    ///
    /// Written before the model was wired in, per docs/engineering/testing.md's
    /// sanctioned flow: pushed unexecuted, with CI turning them red against
    /// the flat sprite they replace before green — there is no editor in an
    /// agent environment to watch them fail in. The assembly rule itself is
    /// Core's and is tested in the fast suite (LogAssemblyTests); these test
    /// what the board does with it.
    ///
    /// Numbers are the spec page's, written out here by hand rather than read
    /// from the code under test.
    /// </summary>
    public sealed class GameBoardLogTests
    {
        const ulong AnySeed = 20261121UL;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // game-board.md, LG-2: SharedLogHeight 896 px over LilyPadDiameter
        // 112 px is an 8 unit log — seven bodies at their own length.
        const float ReferenceLengthInUnits = 8f;

        // How far the drawing's ends may be from the rectangle's: the bark's
        // last ring is the cap's sawn edge, exactly at the log's length.
        const float EndTolerance = 1f;

        // LG-3: the bark is about 174 px across in a 176 px rectangle — about
        // 1 px of water either side — and B's stub reaches about 17 px past
        // the right-hand edge.
        const float BarkInsetMax = 3f;
        const float StubReachMin = 10f;
        const float StubReachMax = 20f;

        // game-board.md § Keeping the frogs visible — the bar the word on the
        // log is held to (LG-7).
        const float MinimumContrastRatio = 1.9f;
        const float MinimumColourDistance = 30f;

        // `LogLabelInk`, from game-board.md § Colours, copied by hand.
        static readonly Color LogLabelInk = new Color32(0xC6, 0xB4, 0x9C, 0xFF);

        // Where the word's letters are, either side of the log's centre line:
        // `Start` at 26 px is about 60 px wide.
        const float WordHalfWidth = 30f;
        const float SampleStep = 2f;

        /// <summary>
        /// LG-1 and LG-6: the model's import settings, pinned in code by
        /// <see cref="LogModelImport"/>, read back from the importers rather
        /// than assumed — and the three pieces the board assembles from are
        /// in the file under their own names.
        /// </summary>
        [Test]
        public void TheModel_IsImportedAsThreeReadableStaticPieces_WithNoMaterial()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(LogModelImport.ModelPath);

            Assert.That(importer, Is.Not.Null, LogModelImport.ModelPath + " is imported");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None), "no rig");
            Assert.That(importer.importAnimation, Is.False, "no clips");
            Assert.That(importer.isReadable, Is.True, "each piece is laid flat on the CPU to draw it into the canvas");
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import), "the model's own normals say which faces look up");
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), "no lit material, so no lit shader to go missing from a build");
            Assert.That(importer.addCollider, Is.False, "the project has no 3D physics module");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LogModelImport.ModelPath);
            var pieces = model.GetComponentsInChildren<MeshFilter>(includeInactive: true)
                .Select(filter => filter.name)
                .ToArray();

            Assert.That(pieces, Has.Member("LogBody_A"), "LG-1: the plain body");
            Assert.That(pieces, Has.Member("LogBody_B"), "LG-1: the body with the stub");
            Assert.That(pieces, Has.Member("LogCap"), "LG-1: the sawn end");

            var texture = (TextureImporter)AssetImporter.GetAtPath(LogModelImport.AlbedoPath);

            Assert.That(texture, Is.Not.Null, LogModelImport.AlbedoPath + " is imported");
            Assert.That(texture.sRGBTexture, Is.True);
            Assert.That(texture.mipmapEnabled, Is.True);
            Assert.That(texture.maxTextureSize, Is.EqualTo(1024), "its own size, and no smaller");
            Assert.That(texture.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "the bark's own colours, not a compressed approximation of them");
        }

        /// <summary>
        /// LG-2: each log is the assembly for its own height — at the
        /// reference canvas, 8 frog units: a cap, seven bodies alternating A
        /// and B at their own length, and a cap. One log per end of the pond,
        /// however many frogs are playing.
        /// </summary>
        [TestCase(2)]
        [TestCase(4)]
        public void EachLog_IsSevenBodiesBetweenTwoCaps_AtTheReferenceCanvas(int frogCount)
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, frogCount);

                foreach (var log in Logs(view))
                {
                    Assert.That(log.Value.UnitSize, Is.EqualTo(GameBoardLaneView.LilyPadDiameter), $"{log.Key}: one frog unit is one lily pad");
                    Assert.That(log.Value.LengthInUnits, Is.EqualTo(ReferenceLengthInUnits).Within(0.001f), $"{log.Key}: 896 px is 8 frog units");

                    var pieces = log.Value.Pieces;

                    Assert.That(
                        pieces.Select(piece => piece.Kind).ToArray(),
                        Is.EqualTo(new[]
                        {
                            LogPieceKind.Cap,
                            LogPieceKind.BodyA, LogPieceKind.BodyB, LogPieceKind.BodyA, LogPieceKind.BodyB,
                            LogPieceKind.BodyA, LogPieceKind.BodyB, LogPieceKind.BodyA,
                            LogPieceKind.Cap,
                        }),
                        $"{log.Key}: a cap, seven bodies, a cap");
                    Assert.That(
                        pieces.Where(piece => piece.Kind != LogPieceKind.Cap).Select(piece => piece.Stretch),
                        Has.All.EqualTo(1f).Within(0.001f),
                        $"{log.Key}: at the reference canvas no body is stretched");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// LG-3 and LG-5: what a log hands uGUI. Its triangles run its
        /// rectangle from the bottom edge to the top, the bark sits just
        /// inside its width, centred, and the stub reaches past the
        /// right-hand edge — which is also what says the picture is not
        /// mirrored. Drawn with the albedo, untinted, with uGUI's default
        /// material.
        /// </summary>
        [Test]
        public void EachLog_RunsItsRectangleTopToBottom_CentredAcrossIt_InItsOwnAlbedo()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);

            try
            {
                var view = BoardOn(canvas, 4);

                foreach (var log in Logs(view))
                {
                    var what = $"the {log.Key} log";
                    var rect = log.Value.rectTransform.rect;
                    var triangles = TrianglesOf(log.Value);

                    Assert.That(triangles.Count, Is.GreaterThan(0), $"{what} draws something");

                    var points = triangles.SelectMany(t => t.Position).ToArray();
                    var bottom = points.Min(p => p.y);
                    var top = points.Max(p => p.y);
                    var left = points.Min(p => p.x);
                    var right = points.Max(p => p.x);

                    Assert.That(bottom, Is.EqualTo(rect.yMin).Within(EndTolerance), $"{what} reaches the bottom of its rectangle");
                    Assert.That(top, Is.EqualTo(rect.yMax).Within(EndTolerance), $"{what} reaches the top of its rectangle");

                    Assert.That(left, Is.InRange(rect.xMin, rect.xMin + BarkInsetMax), $"{what}: the bark sits just inside its left edge, not stretched to it");
                    Assert.That(right - rect.xMax, Is.InRange(StubReachMin, StubReachMax), $"{what}: the stub reaches past its right edge, not its left");

                    Assert.That(log.Value.mainTexture, Is.Not.Null, $"{what} is textured");
                    Assert.That(log.Value.mainTexture.name, Is.EqualTo("log_albedo"), $"{what}: with the model's albedo");
                    Assert.That(log.Value.color, Is.EqualTo(Color.white), $"{what}: untinted — its colours are the albedo's");
                    Assert.That(log.Value.material, Is.SameAs(Graphic.defaultGraphicMaterial), $"{what}: with uGUI's default material");
                    Assert.That(log.Value.raycastTarget, Is.False, $"{what} is not something to tap");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// LG-7: `Start` and `End` are drawn after the model, over it, and the
        /// bark under the word clears the bar the word is held to. Measured on
        /// what the board actually draws: for points across where the letters
        /// are, the topmost triangle there, its albedo pixel, and that pixel
        /// against `LogLabelInk`.
        /// </summary>
        [Test]
        public void TheWordOnEachLog_IsDrawnOverTheModel_AndClearsTheBarAgainstTheBarkUnderIt()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);
            var albedo = LilyPadAlbedo.DecodePng(File.ReadAllBytes(LogModelImport.AlbedoPath), out var width, out var height);

            try
            {
                var view = BoardOn(canvas, 2);
                var labels = new Dictionary<string, Text> { { "Start", view.StartLogLabel }, { "End", view.EndLogLabel } };

                foreach (var log in Logs(view))
                {
                    var what = $"the {log.Key} log's word";
                    var label = labels[log.Key];

                    // A child of the log, so uGUI draws it after the log — on top.
                    Assert.That(label.transform.parent, Is.SameAs(log.Value.transform), $"{what} is drawn over the log");

                    var rect = log.Value.rectTransform.rect;
                    var triangles = TrianglesOf(log.Value);
                    var wordTop = rect.yMax - GameBoardScreenView.LogLabelTopPadding;
                    var wordBottom = wordTop - GameBoardScreenView.LogLabelSize;
                    var worstContrast = float.MaxValue;
                    var worstDistance = float.MaxValue;
                    var samples = 0;

                    for (var y = wordBottom; y <= wordTop; y += SampleStep)
                    {
                        for (var x = rect.center.x - WordHalfWidth; x <= rect.center.x + WordHalfWidth; x += SampleStep)
                        {
                            if (!TryTopmostUv(triangles, new Vector2(x, y), out var uv))
                            {
                                continue;
                            }

                            var column = Mathf.Clamp(Mathf.FloorToInt(uv.x * width), 0, width - 1);
                            var row = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * height), 0, height - 1);
                            var bark = (Color)albedo[(row * width) + column];

                            worstContrast = Mathf.Min(worstContrast, GameBoardColoursTests.ContrastRatio(LogLabelInk, bark));
                            worstDistance = Mathf.Min(worstDistance, GameBoardColoursTests.ColourDistance(LogLabelInk, bark));
                            samples++;
                        }
                    }

                    Assert.That(samples, Is.GreaterThan(0), $"{what} has the log under it");
                    Assert.That(worstContrast, Is.GreaterThanOrEqualTo(MinimumContrastRatio), $"{what}: contrast against the bark under it");
                    Assert.That(worstDistance, Is.GreaterThanOrEqualTo(MinimumColourDistance), $"{what}: ΔE against the bark under it");
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        /// <summary>
        /// game-board.md § Keeping the frogs visible, and #469: every frog
        /// clears the separability bar against the bark it actually sits on,
        /// not only against `LogBrown`, which the approved model's bark is
        /// lighter than. Measured the way LG-7 measures the word: on the built
        /// board, the topmost triangle at each point and its albedo pixel —
        /// across the middle `LilyPadDiameter` of the log, where a sitting
        /// frog is, from one end cap to the other.
        ///
        /// This is the test `FrogBlue` `#37609A` failed (1.49 : 1 against the
        /// lightest bark) and `#4273B8` passes (1.98 : 1); `FrogPink` is the
        /// one that only just clears it (1.91 : 1).
        /// </summary>
        [Test]
        public void EveryFrog_ClearsTheBarAgainstTheBarkWhereItSits()
        {
            var canvas = CanvasOf(CanvasWidth, CanvasHeight);
            var albedo = LilyPadAlbedo.DecodePng(File.ReadAllBytes(LogModelImport.AlbedoPath), out var width, out var height);

            try
            {
                var view = BoardOn(canvas, 4);

                foreach (var log in Logs(view))
                {
                    var rect = log.Value.rectTransform.rect;
                    var unit = log.Value.UnitSize;
                    var triangles = TrianglesOf(log.Value);
                    var bark = new List<Color>();

                    for (var y = rect.yMin + (LogAssembly.CapLength * unit) + (SampleStep / 2f); y < rect.yMax - (LogAssembly.CapLength * unit); y += SampleStep)
                    {
                        for (var x = rect.center.x - (unit / 2f) + (SampleStep / 2f); x < rect.center.x + (unit / 2f); x += SampleStep)
                        {
                            if (!TryTopmostUv(triangles, new Vector2(x, y), out var uv))
                            {
                                continue;
                            }

                            var column = Mathf.Clamp(Mathf.FloorToInt(uv.x * width), 0, width - 1);
                            var row = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * height), 0, height - 1);
                            bark.Add(albedo[(row * width) + column]);
                        }
                    }

                    Assert.That(bark.Count, Is.GreaterThan(0), $"the {log.Key} log has bark where a frog sits");

                    foreach (FrogColour frog in System.Enum.GetValues(typeof(FrogColour)))
                    {
                        var piece = Frogs.Unity.UI.FrogColours.For(frog);
                        var worstContrast = bark.Min(b => GameBoardColoursTests.ContrastRatio(piece, b));
                        var worstDistance = bark.Min(b => GameBoardColoursTests.ColourDistance(piece, b));

                        Assert.That(
                            worstContrast,
                            Is.GreaterThanOrEqualTo(MinimumContrastRatio),
                            $"the {frog} frog is too close in brightness to the {log.Key} log's bark — "
                            + "game-board.md#keeping-the-frogs-visible");
                        Assert.That(
                            worstDistance,
                            Is.GreaterThanOrEqualTo(MinimumColourDistance),
                            $"the {frog} frog is too close in colour to the {log.Key} log's bark — "
                            + "game-board.md#keeping-the-frogs-visible");
                    }
                }
            }
            finally
            {
                DestroyCanvas(canvas);
            }
        }

        // ---- helpers

        sealed class DrawnTriangle
        {
            public Vector2[] Position;
            public Vector2[] Uv;
        }

        static Dictionary<string, LogModel> Logs(GameBoardScreenView view)
        {
            return new Dictionary<string, LogModel> { { "Start", view.StartLog }, { "End", view.EndLog } };
        }

        // The triangles a log hands uGUI, in draw order, in its own rectangle.
        static List<DrawnTriangle> TrianglesOf(LogModel log)
        {
            var vh = new VertexHelper();
            var stream = new List<UIVertex>();

            log.Draw(vh);
            vh.GetUIVertexStream(stream);
            vh.Dispose();

            var triangles = new List<DrawnTriangle>();

            for (var i = 0; i + 2 < stream.Count; i += 3)
            {
                triangles.Add(new DrawnTriangle
                {
                    Position = new Vector2[] { stream[i].position, stream[i + 1].position, stream[i + 2].position },
                    Uv = new Vector2[] { stream[i].uv0, stream[i + 1].uv0, stream[i + 2].uv0 },
                });
            }

            return triangles;
        }

        // The UV at a point of the last triangle drawn over it — the one on
        // top, since uGUI paints in order.
        static bool TryTopmostUv(List<DrawnTriangle> triangles, Vector2 point, out Vector2 uv)
        {
            for (var i = triangles.Count - 1; i >= 0; i--)
            {
                var p = triangles[i].Position;
                var area = Cross(p[0], p[1], p[2]);

                if (Mathf.Approximately(area, 0f))
                {
                    continue;
                }

                var w0 = Cross(p[1], p[2], point) / area;
                var w1 = Cross(p[2], p[0], point) / area;
                var w2 = 1f - w0 - w1;

                if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                {
                    var t = triangles[i].Uv;

                    uv = (w0 * t[0]) + (w1 * t[1]) + (w2 * t[2]);
                    return true;
                }
            }

            uv = Vector2.zero;
            return false;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p)
        {
            return ((b.x - a.x) * (p.y - a.y)) - ((b.y - a.y) * (p.x - a.x));
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
