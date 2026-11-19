using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.EditorTools;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using FrogColours = Frogs.Unity.UI.FrogColours;
using FrogModel = Frogs.Unity.UI.FrogModel;
using FrogSkin = Frogs.Unity.UI.FrogSkin;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The frog piece** — issue #461, and the section of that name on
    /// docs/specs/ui/game-board.md (FP-1 to FP-9). The piece is the approved
    /// Blender frog (tools/art/frog_model.py), imported with its 14-bone rig
    /// and its Sit and Hop clips, drawn into the board's canvas in the frog's
    /// colour; the board's hop samples the Hop clip.
    ///
    /// Written before the change and pushed unexecuted, per
    /// docs/engineering/testing.md — there is no editor in an agent
    /// environment. Which clip, and how far through it, is Core's and is
    /// tested in Tests/Core/FrogPoseTests; what is asserted here is that the
    /// Unity frog is the imported model, that its bones are exactly the clip's,
    /// and that the board drives it.
    /// </summary>
    public sealed class FrogPieceTests
    {
        const ulong AnySeed = 20261119UL;

        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // How close a bone must be to the clip's own value — the same sampler
        // on the same clip, so this is float noise, not tolerance for a
        // different pose.
        const float Degrees = 0.01f;

        // How far a bone must have turned for "it moved" to mean anything.
        const float AVisibleTurn = 10f;

        static readonly string[] s_legs = { "back", "front" };
        static readonly string[] s_sides = { "L", "R" };

        [Test]
        public void TheModel_IsImportedAsAGenericRig_WithItsClips_AndReadableForDrawing()
        {
            // FP-1, and docs/engineering/tech-stack.md § The frog model: the
            // settings FrogModelImport pins in code, read back from the
            // importer rather than assumed.
            var importer = (ModelImporter)AssetImporter.GetAtPath(FrogModelImport.ModelPath);

            Assert.That(importer, Is.Not.Null, FrogModelImport.ModelPath + " is imported");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.importAnimation, Is.True);
            Assert.That(importer.animationCompression, Is.EqualTo(ModelImporterAnimationCompression.Off), "the approved curves, not a reduction of them");
            Assert.That(importer.isReadable, Is.True, "the frog is skinned on the CPU to draw it into the canvas");
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), "no lit material, so no lit shader to go missing from a build");
            Assert.That(importer.addCollider, Is.False, "the project has no 3D physics module");

            var texture = (TextureImporter)AssetImporter.GetAtPath(FrogModelImport.AlbedoPath);

            Assert.That(texture, Is.Not.Null, FrogModelImport.AlbedoPath + " is imported");
            Assert.That(texture.sRGBTexture, Is.True);
            Assert.That(texture.mipmapEnabled, Is.True, "a 1024 px texture drawn at 88 px");
            Assert.That(texture.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "white stays exactly white, so the tint is exactly the frog colour");
        }

        [Test]
        public void TheHopClip_LastsFrogHopDuration_AndBeginsAndEndsSitting()
        {
            // The approved clip and the board agree about how long a hop is,
            // and the clip starts and ends on the Sit pose — so a frog that
            // has just landed looks exactly like one that never moved.
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;

                Assert.That(frog.HopClip.length, Is.EqualTo(GameBoardScreenView.FrogHopDuration).Within(1f / 60f));

                foreach (var fraction in new[] { 0f, 1f })
                {
                    var sitting = SampledRig(frog.SitClip, 0f);
                    var hopping = SampledRig(frog.HopClip, fraction * frog.HopClip.length);

                    try
                    {
                        foreach (var bone in BoneNames())
                        {
                            AssertSameTurn(Find(hopping, bone), Find(sitting, bone), $"{bone} at {fraction} of the hop");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(sitting);
                        Object.DestroyImmediate(hopping);
                    }
                }
            });
        }

        [Test]
        public void ThePiece_IsTheModel_WithFourLegs_EachAThighShinAndFoot()
        {
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;

                Assert.That(frog, Is.Not.Null);
                Assert.That(frog.rectTransform, Is.SameAs(view.Lanes[0].PieceRect), "the frog is the piece");
                Assert.That(frog.Skin.mainTexture, Is.Not.Null, "drawn with the model's albedo");
                Assert.That(frog.Skin.mainTexture.name, Is.EqualTo("frog_albedo"));
                Assert.That(frog.Legs.Count, Is.EqualTo(4), "FP-1: four legs");

                Assert.That(frog.Legs.Count(leg => leg.IsFront), Is.EqualTo(2), "two front legs");
                Assert.That(frog.Legs.Count(leg => !leg.IsFront), Is.EqualTo(2), "two back legs");
                Assert.That(frog.Legs.Count(leg => leg.IsLeft), Is.EqualTo(2), "two on the left");

                foreach (var leg in frog.Legs)
                {
                    var prefix = (leg.IsFront ? "front" : "back") + "_";
                    var suffix = "." + (leg.IsLeft ? "L" : "R");

                    Assert.That(leg.Thigh.name, Is.EqualTo(prefix + "thigh" + suffix));
                    Assert.That(leg.Shin.name, Is.EqualTo(prefix + "shin" + suffix));
                    Assert.That(leg.Foot.name, Is.EqualTo(prefix + "foot" + suffix));
                    Assert.That(leg.Shin.parent, Is.SameAs(leg.Thigh), "the knee swings with the hip");
                    Assert.That(leg.Foot.parent, Is.SameAs(leg.Shin), "the ankle swings with the knee");
                    Assert.That(leg.Thigh.IsChildOf(frog.transform), Is.True, "the leg belongs to this frog");
                }
            });
        }

        [Test]
        public void TheRig_HasAnAnimator_SoTheClipCanBeSampledInAPlayer()
        {
            // AnimationClip.SampleAnimation on a non-legacy clip needs an
            // Animator on the sampled object outside the editor (Unity issue
            // "Non-Legacy animations cannot be sampled outside the Editor
            // without an Animator"; no controller needed since 2019.1). The
            // editor does not need it, so only this test notices its absence.
            WithBoard((view, game) =>
            {
                Assert.That(view.Lanes[0].Frog.Rig.GetComponent<Animator>(), Is.Not.Null);
            });
        }

        [Test]
        public void EachFrog_IsDrawnInItsOwnFrogColour()
        {
            WithBoard((view, game) =>
            {
                foreach (var lane in view.Lanes)
                {
                    Assert.That(lane.Frog.color, Is.EqualTo(FrogColours.For(lane.Colour)), "FP-3");
                }
            });
        }

        [Test]
        public void PartWayThroughAMove_EveryBoneIsTheHopClipsPose_ForThatPointOfTheHop()
        {
            // FP-6, and the invariant that nothing re-poses a bone by hand:
            // each bone is exactly what the approved clip says at that point.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                foreach (var progress in new[] { 0.1f, FrogPose.TakeOffProgress, FrogPose.InTheAirProgress, 0.6f, FrogPose.LandingProgress })
                {
                    lane.PlacePiecePartWay(1, 2, progress);

                    Assert.That(lane.Frog.Pose, Is.EqualTo(FrogPose.At(progress)));

                    var reference = SampledRig(lane.Frog.HopClip, progress * lane.Frog.HopClip.length);

                    try
                    {
                        foreach (var bone in BoneNames())
                        {
                            AssertSameTurn(Find(lane.Frog.Rig.gameObject, bone), Find(reference, bone), $"{bone} at {progress}");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(reference);
                    }
                }
            });
        }

        [Test]
        public void AtTakeOff_TheLegsHaveMovedFromSitting()
        {
            // The board is wired to the legs, not merely to a number: the back
            // thighs are visibly turned at take-off compared with sitting.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var sitting = lane.Frog.Legs.Where(leg => !leg.IsFront).Select(leg => leg.Thigh.localRotation).ToArray();

                lane.PlacePiecePartWay(1, 2, FrogPose.TakeOffProgress);

                var kicking = lane.Frog.Legs.Where(leg => !leg.IsFront).Select(leg => leg.Thigh.localRotation).ToArray();

                for (var i = 0; i < sitting.Length; i++)
                {
                    Assert.That(Quaternion.Angle(sitting[i], kicking[i]), Is.GreaterThan(AVisibleTurn), "a back thigh turns at take-off");
                }
            });
        }

        [Test]
        public void AMoveThatGoesNowhere_LeavesTheFrogSitting()
        {
            // FP-7: a wrong answer on the Start log is not a hop.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                lane.PlacePiecePartWay(0, 0, FrogPose.TakeOffProgress);

                Assert.That(lane.Frog.Pose, Is.EqualTo(FrogPose.Rest));
            });
        }

        [Test]
        public void AnOrdinaryRedraw_PutsTheFrogBackToSitting()
        {
            // FP-8.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                lane.PlacePiecePartWay(1, 2, FrogPose.TakeOffProgress);
                view.Refresh();

                Assert.That(lane.Frog.Pose, Is.EqualTo(FrogPose.Rest));
            });
        }

        [Test]
        public void TheFrog_FacesTheEndLog_TheRightWayUp()
        {
            // FP-5: head toward the End log (screen right), feet at the bottom.
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;

                foreach (var side in s_sides)
                {
                    var front = frog.PiecePointOf(Find(frog.Rig.gameObject, "front_thigh." + side));
                    var back = frog.PiecePointOf(Find(frog.Rig.gameObject, "back_thigh." + side));
                    var foot = frog.PiecePointOf(Find(frog.Rig.gameObject, "front_foot." + side));

                    Assert.That(front.x, Is.GreaterThan(back.x), "the front legs are nearer the End log");
                    Assert.That(foot.y, Is.LessThan(front.y), "the feet are below the hips");
                }
            });
        }

        [Test]
        public void InTheAir_TheFrogIsOffThePad()
        {
            // The approved hop rises: at its top the whole frog is higher than
            // when it sits.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var sitting = VerticesOf(lane.Frog.Skin).Min(vertex => vertex.position.y);

                lane.PlacePiecePartWay(1, 2, FrogPose.InTheAirProgress);

                var flying = VerticesOf(lane.Frog.Skin).Min(vertex => vertex.position.y);

                Assert.That(flying, Is.GreaterThan(sitting + 1f), "lifted off the pad");
            });
        }

        [Test]
        public void TheFrog_StaysInsideItsFootprint_ThroughTheWholeHop()
        {
            // FP-2: inside the FrogPieceDiameter square the disc filled, at
            // every moment of a hop, outline included.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var half = GameBoardLaneView.FrogPieceDiameter / 2f;
                const float pixelOfSlack = 0.01f;

                Assert.That(lane.PieceRect.sizeDelta, Is.EqualTo(new Vector2(GameBoardLaneView.FrogPieceDiameter, GameBoardLaneView.FrogPieceDiameter)));

                for (var frame = 0; frame <= 40; frame++)
                {
                    lane.PlacePiecePartWay(1, 2, frame / 40f);

                    var vertices = VerticesOf(lane.Frog.Skin).Concat(VerticesOf(lane.Frog)).ToList();
                    Assert.That(vertices, Is.Not.Empty, "the frog draws something");

                    // The furthest any vertex reaches, then one assert each —
                    // thousands of vertices a frame make per-vertex asserts slow.
                    var reachX = vertices.Max(vertex => Mathf.Abs(vertex.position.x));
                    var reachY = vertices.Max(vertex => Mathf.Abs(vertex.position.y));

                    Assert.That(reachX, Is.LessThanOrEqualTo(half + pixelOfSlack), $"frame {frame}, x");
                    Assert.That(reachY, Is.LessThanOrEqualTo(half + pixelOfSlack), $"frame {frame}, y");
                }
            });
        }

        [Test]
        public void TheFrog_IsTintedItsColour_ShadedOnlyDarker_WithAPieceEdgeOutline()
        {
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;
                var tint = FrogColours.For(view.Lanes[0].Colour);
                var skin = VerticesOf(frog.Skin).Select(vertex => (Color)vertex.color).ToArray();
                var outline = VerticesOf(frog).Select(vertex => (Color)vertex.color).ToArray();

                Assert.That(skin, Is.Not.Empty);
                Assert.That(skin.Any(colour => Same(colour, tint)), Is.True, "lit skin is exactly the frog colour times the albedo (FP-3)");

                // Shading only ever darkens, and by no more than FrogShadeFloor.
                // The albedo multiplies on top and is never lighter than white.
                foreach (var colour in skin)
                {
                    var shade = colour.g / tint.g;

                    Assert.That(shade, Is.LessThanOrEqualTo(1f + 0.01f), "never lighter than the frog colour");
                    Assert.That(shade, Is.GreaterThanOrEqualTo(FrogModel.FrogShadeFloor - 0.01f), "never darker than the floor");
                }

                Assert.That(outline, Is.Not.Empty, "FP-4: the outline is drawn");
                Assert.That(outline.All(colour => Same(colour, BoardColours.PieceEdge)), Is.True, "FP-4: in PieceEdge");
            });
        }

        static IEnumerable<string> BoneNames()
        {
            yield return "root";
            yield return "spine";

            foreach (var leg in s_legs)
            {
                foreach (var side in s_sides)
                {
                    yield return leg + "_thigh." + side;
                    yield return leg + "_shin." + side;
                    yield return leg + "_foot." + side;
                }
            }
        }

        // A second copy of the imported model, posed by the clip directly —
        // what the approved animation says, independent of the board.
        static GameObject SampledRig(AnimationClip clip, float time)
        {
            var rig = Object.Instantiate(Resources.Load<GameObject>(FrogModel.ModelResource));
            clip.SampleAnimation(rig, time);
            return rig;
        }

        static Transform Find(GameObject root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name);

            Assert.That(found, Is.Not.Null, "bone " + name);
            return found;
        }

        static void AssertSameTurn(Transform actual, Transform expected, string because)
        {
            Assert.That(Quaternion.Angle(actual.localRotation, expected.localRotation), Is.LessThanOrEqualTo(Degrees), because + " — rotation");
            Assert.That(Vector3.Distance(actual.localPosition, expected.localPosition), Is.LessThanOrEqualTo(0.0001f), because + " — position");
        }

        static bool Same(Color a, Color b)
        {
            const float byteOfSlack = 2f / 255f;

            return Mathf.Abs(a.r - b.r) <= byteOfSlack
                && Mathf.Abs(a.g - b.g) <= byteOfSlack
                && Mathf.Abs(a.b - b.b) <= byteOfSlack
                && Mathf.Abs(a.a - b.a) <= byteOfSlack;
        }

        static List<UIVertex> VerticesOf(Graphic graphic)
        {
            var vertices = new List<UIVertex>();

            using (var helper = new VertexHelper())
            {
                if (graphic is FrogModel frog)
                {
                    frog.DrawOutline(helper);
                }
                else
                {
                    ((FrogSkin)graphic).Draw(helper);
                }

                helper.GetUIVertexStream(vertices);
            }

            return vertices;
        }

        static void WithBoard(System.Action<GameBoardScreenView, Game> check)
        {
            var host = new GameObject("Canvas", typeof(RectTransform));
            var canvas = (RectTransform)host.transform;
            canvas.anchorMin = new Vector2(0.5f, 0.5f);
            canvas.anchorMax = new Vector2(0.5f, 0.5f);
            canvas.pivot = new Vector2(0.5f, 0.5f);
            canvas.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            try
            {
                var game = new Game(
                    new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink },
                    AnySeed);

                var view = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform))
                    .AddComponent<GameBoardScreenView>();
                view.transform.SetParent(canvas, worldPositionStays: false);
                view.Initialize(game);

                check(view, game);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
