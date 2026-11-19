using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using FrogColours = Frogs.Unity.UI.FrogColours;
using FrogModel = Frogs.Unity.UI.FrogModel;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// **The frog piece** — issue #461, and the section of that name on
    /// docs/specs/ui/game-board.md (FP-1 to FP-8). The piece is a 3D frog built
    /// from simple shapes, with four jointed legs, and the board's hop moves
    /// them.
    ///
    /// Written before the change and pushed unexecuted, per
    /// docs/engineering/testing.md — there is no editor in an agent
    /// environment. The leg angles themselves are Core's and are tested in
    /// Tests/Core/FrogLegPoseTests; what is asserted here is that the Unity
    /// frog has the joints those angles drive, and that the board drives them.
    /// </summary>
    public sealed class FrogPieceTests
    {
        const ulong AnySeed = 20261119UL;

        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const float Degrees = 0.01f;

        [Test]
        public void ThePiece_IsAFrogWithFourLegs_EachAKneeHungFromAHip()
        {
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;

                Assert.That(frog, Is.Not.Null);
                Assert.That(frog.rectTransform, Is.SameAs(view.Lanes[0].PieceRect), "the frog is the piece");
                Assert.That(frog.Legs.Count, Is.EqualTo(4), "FP-1: four legs");

                Assert.That(frog.Legs.Count(leg => leg.IsFront), Is.EqualTo(2), "two front legs");
                Assert.That(frog.Legs.Count(leg => !leg.IsFront), Is.EqualTo(2), "two back legs");
                Assert.That(frog.Legs.Count(leg => leg.IsLeft), Is.EqualTo(2), "two on the left");

                foreach (var leg in frog.Legs)
                {
                    Assert.That(leg.Hip, Is.Not.Null);
                    Assert.That(leg.Knee, Is.Not.Null);
                    Assert.That(leg.Knee.parent, Is.SameAs(leg.Hip), "the knee swings with the hip");
                    Assert.That(leg.Hip.IsChildOf(frog.transform), Is.True, "the leg belongs to this frog");
                }
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
        public void PartWayThroughAMove_TheLegsTakeThePoseForThatPointOfTheHop()
        {
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                foreach (var progress in new[] { 0.1f, FrogLegPose.TakeOffProgress, 0.6f, FrogLegPose.LandingProgress })
                {
                    lane.PlacePiecePartWay(1, 2, progress);

                    var expected = FrogLegPose.At(progress);
                    AssertPose(lane.Frog.LegPose, expected);

                    // And the joints themselves are turned to it — the pose is
                    // drawn, not merely stored.
                    foreach (var leg in lane.Frog.Legs)
                    {
                        var joints = leg.IsFront ? expected.Front : expected.Back;

                        Assert.That(Mathf.DeltaAngle(SwingOf(leg.Hip), joints.Hip), Is.EqualTo(0f).Within(Degrees), "hip");
                        Assert.That(Mathf.DeltaAngle(SwingOf(leg.Knee), -joints.Knee), Is.EqualTo(0f).Within(Degrees), "knee folds back");
                    }
                }
            });
        }

        [Test]
        public void AMoveThatGoesNowhere_LeavesTheLegsAtRest()
        {
            // FP-7: a wrong answer on the Start log is not a hop.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                lane.PlacePiecePartWay(0, 0, FrogLegPose.TakeOffProgress);

                AssertPose(lane.Frog.LegPose, FrogLegPose.Rest);
            });
        }

        [Test]
        public void AnOrdinaryRedraw_PutsTheLegsBackAtRest()
        {
            // FP-8.
            WithBoard((view, game) =>
            {
                var lane = view.Lanes[0];

                lane.PlacePiecePartWay(1, 2, FrogLegPose.TakeOffProgress);
                view.Refresh();

                AssertPose(lane.Frog.LegPose, FrogLegPose.Rest);
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

                Assert.That(lane.PieceRect.sizeDelta, Is.EqualTo(new Vector2(GameBoardLaneView.FrogPieceDiameter, GameBoardLaneView.FrogPieceDiameter)));

                for (var frame = 0; frame <= 40; frame++)
                {
                    lane.PlacePiecePartWay(1, 2, frame / 40f);

                    var vertices = VerticesOf(lane.Frog);
                    Assert.That(vertices, Is.Not.Empty, "the frog draws something");

                    foreach (var vertex in vertices)
                    {
                        Assert.That(Mathf.Abs(vertex.position.x), Is.LessThanOrEqualTo(half), $"frame {frame}, x");
                        Assert.That(Mathf.Abs(vertex.position.y), Is.LessThanOrEqualTo(half), $"frame {frame}, y");
                    }
                }
            });
        }

        [Test]
        public void TheFrog_IsDrawnInItsColour_WithBoardInkPupils_AndAPieceEdgeOutline()
        {
            WithBoard((view, game) =>
            {
                var frog = view.Lanes[0].Frog;
                var colours = VerticesOf(frog).Select(vertex => (Color)vertex.color).ToArray();
                var tint = FrogColours.For(view.Lanes[0].Colour);

                Assert.That(colours.Any(colour => Same(colour, tint)), Is.True, "lit faces are exactly the frog colour (FP-3)");
                Assert.That(colours.Any(colour => Same(colour, BoardColours.BoardInk)), Is.True, "pupils (FP-3)");
                Assert.That(colours.Any(colour => Same(colour, BoardColours.PieceEdge)), Is.True, "the outline (FP-4)");

                // Shading only ever darkens, and by no more than FrogShadeFloor.
                foreach (var colour in colours.Where(colour => colour.a == 1f && !Same(colour, BoardColours.BoardInk)))
                {
                    var shade = colour.g / tint.g;

                    Assert.That(shade, Is.LessThanOrEqualTo(1f + 0.01f), "never lighter than the frog colour");
                    Assert.That(shade, Is.GreaterThanOrEqualTo(FrogModel.FrogShadeFloor - 0.01f), "never darker than the floor");
                }
            });
        }

        // How far a joint is turned about the axis the legs swing on — the
        // frog's own side-to-side axis, which is its local z.
        static float SwingOf(Transform joint)
        {
            return joint.localEulerAngles.z;
        }

        static bool Same(Color a, Color b)
        {
            const float byteOfSlack = 2f / 255f;

            return Mathf.Abs(a.r - b.r) <= byteOfSlack
                && Mathf.Abs(a.g - b.g) <= byteOfSlack
                && Mathf.Abs(a.b - b.b) <= byteOfSlack
                && Mathf.Abs(a.a - b.a) <= byteOfSlack;
        }

        static List<UIVertex> VerticesOf(FrogModel frog)
        {
            var vertices = new List<UIVertex>();

            using (var helper = new VertexHelper())
            {
                frog.Draw(helper);
                helper.GetUIVertexStream(vertices);
            }

            return vertices;
        }

        static void AssertPose(FrogLegPose actual, FrogLegPose expected)
        {
            Assert.That(actual.Front.Hip, Is.EqualTo(expected.Front.Hip).Within(Degrees), "front hip");
            Assert.That(actual.Front.Knee, Is.EqualTo(expected.Front.Knee).Within(Degrees), "front knee");
            Assert.That(actual.Back.Hip, Is.EqualTo(expected.Back.Hip).Within(Degrees), "back hip");
            Assert.That(actual.Back.Knee, Is.EqualTo(expected.Back.Knee).Within(Degrees), "back knee");
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
