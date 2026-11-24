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
        public void TheWholeHopClip_PlaysOverOneFrogHopDuration_AndBeginsAndEndsSitting()
        {
            // #476: the clip's authored length no longer has to equal
            // FrogHopDuration. The clip is sampled by the hop's progress and
            // stretched over however long FrogHopDuration is, so retuning the
            // hop is a one-number change. What the old length check protected
            // still holds: the whole clip, first frame to last, plays over
            // exactly one hop of FrogHopDuration, and it starts and ends on
            // the Sit pose — so a frog that has just landed looks exactly like
            // one that never moved.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var frog = lane.Frog;

                Assert.That(frog.HopClip.length, Is.GreaterThan(0f));

                // Seconds into a hop, as the answer result's clock counts
                // them, and the point of the clip each one must show: the
                // same fraction of the clip as of FrogHopDuration — not the
                // clip at that many seconds, which would cut a longer hop off
                // early or run a shorter one past the clip's end.
                foreach (var share in new[] { 0.1f, FrogPose.TakeOffProgress, FrogPose.InTheAirProgress, FrogPose.LandingProgress, 0.99f })
                {
                    var elapsed = share * GameBoardScreenView.FrogHopDuration;

                    lane.PlacePiecePartWay(1, 2, elapsed / GameBoardScreenView.FrogHopDuration);

                    var reference = SampledRig(frog.HopClip, share * frog.HopClip.length);

                    try
                    {
                        foreach (var bone in BoneNames())
                        {
                            AssertSameTurn(Find(frog.Rig.gameObject, bone), Find(reference, bone), $"{bone} {elapsed:0.###} s into a {GameBoardScreenView.FrogHopDuration} s hop");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(reference);
                    }
                }

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
        public void Sitting_TheFrogFillsItsFootprint_CentredOnIt()
        {
            // FP-2 (#474): the scale is chosen from the Sit pose, so a sitting
            // frog, outline included, is as big as fits the FrogPieceDiameter
            // square — its taller or wider side reaching the edges — with its
            // box centred on the square's centre.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var half = GameBoardLaneView.FrogPieceDiameter / 2f;
                var inside = GameBoardLaneView.FrogPieceDiameter - (2f * GameBoardLaneView.FrogPieceOutline);
                const float pixelOfSlack = 0.01f;

                Assert.That(lane.Frog.Pose, Is.EqualTo(FrogPose.Rest));

                var skin = VerticesOf(lane.Frog.Skin);
                var minX = skin.Min(vertex => vertex.position.x);
                var maxX = skin.Max(vertex => vertex.position.x);
                var minY = skin.Min(vertex => vertex.position.y);
                var maxY = skin.Max(vertex => vertex.position.y);

                Assert.That(Mathf.Max(maxX - minX, maxY - minY), Is.EqualTo(inside).Within(pixelOfSlack), "the longer side fills the square, less the outline either side");
                Assert.That((minX + maxX) / 2f, Is.EqualTo(0f).Within(pixelOfSlack), "centred across the square");
                Assert.That((minY + maxY) / 2f, Is.EqualTo(0f).Within(pixelOfSlack), "centred up and down it");

                var all = skin.Concat(VerticesOf(lane.Frog)).ToList();

                Assert.That(all.Max(vertex => Mathf.Abs(vertex.position.x)), Is.LessThanOrEqualTo(half + pixelOfSlack), "outline included, inside the square across");
                Assert.That(all.Max(vertex => Mathf.Abs(vertex.position.y)), Is.LessThanOrEqualTo(half + pixelOfSlack), "and up and down");
            });
        }

        [Test]
        public void ThroughTheWholeHop_TheFrogIsDrawnAtOneScale_AndOvershootsItsFootprint()
        {
            // FP-2's other half: one scale, fitted once — a frog that has
            // landed is exactly the size it was sitting — and the hop is what
            // overshoots the square, not something squeezed into it.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];
                var half = GameBoardLaneView.FrogPieceDiameter / 2f;
                var sitting = VerticesOf(lane.Frog.Skin).Select(vertex => vertex.position).ToList();

                var reach = 0f;

                for (var frame = 1; frame < 40; frame++)
                {
                    lane.PlacePiecePartWay(1, 2, frame / 40f);
                    reach = Mathf.Max(reach, VerticesOf(lane.Frog.Skin).Max(vertex => Mathf.Max(Mathf.Abs(vertex.position.x), Mathf.Abs(vertex.position.y))));
                }

                Assert.That(reach, Is.GreaterThan(half), "mid-hop the frog reaches outside its square");

                lane.PlacePiecePartWay(1, 2, 1f);
                var landed = VerticesOf(lane.Frog.Skin).Select(vertex => vertex.position).ToList();

                Assert.That(landed.Count, Is.EqualTo(sitting.Count));

                for (var i = 0; i < landed.Count; i++)
                {
                    Assert.That(Vector3.Distance(landed[i], sitting[i]), Is.LessThanOrEqualTo(0.01f), "landed is drawn exactly as sitting");
                }
            });
        }

        [Test]
        public void ThroughTheWholeHop_TheFrogStaysInItsOwnLane_AndReachesNoOtherPad()
        {
            // The invariant FP-2's overshoot is bounded by: a kicking frog
            // does not cover the lane next to it, or any pad but the one it is
            // leaving and the one it is landing on. Forward, and back a pad —
            // where it still faces the End log, so its legs kick the way it
            // is going.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[1];
                var padHalf = GameBoardLaneView.LilyPadDiameter / 2f;
                var laneHalf = GameBoardLaneView.LaneHeight / 2f;
                const float pixelOfSlack = 0.01f;

                foreach (var (from, to) in new[] { (2, 3), (3, 2), (0, 1), (1, 0) })
                {
                    var centreY = WorldCentre(lane.PositionRects[from]).y;

                    for (var frame = 0; frame <= 40; frame++)
                    {
                        lane.PlacePiecePartWay(from, to, frame / 40f);

                        var world = VerticesOf(lane.Frog.Skin).Concat(VerticesOf(lane.Frog))
                            .Select(vertex => lane.Frog.rectTransform.TransformPoint(vertex.position))
                            .ToList();

                        var what = $"hop {from} to {to}, frame {frame}";

                        Assert.That(world.Max(point => point.y), Is.LessThanOrEqualTo(centreY + laneHalf + pixelOfSlack), what + ": not into the lane above");
                        Assert.That(world.Min(point => point.y), Is.GreaterThanOrEqualTo(centreY - laneHalf - pixelOfSlack), what + ": not into the lane below");

                        var left = world.Min(point => point.x);
                        var right = world.Max(point => point.x);

                        for (var pad = 1; pad < Lane.LanePositionCount - 1; pad++)
                        {
                            if (pad == from || pad == to)
                            {
                                continue;
                            }

                            var padX = WorldCentre(lane.PositionRects[pad]).x;

                            Assert.That(
                                right < padX - padHalf || left > padX + padHalf,
                                Is.True,
                                $"{what}: clear of pad {pad} ({left:F1}..{right:F1} against {padX - padHalf:F1}..{padX + padHalf:F1})");
                        }
                    }
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

        static Vector3 WorldCentre(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return (corners[0] + corners[2]) / 2f;
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
