using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
using BoardColours = Frogs.Unity.UI.BoardColours;
using FrogColours = Frogs.Unity.UI.FrogColours;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The pond reads as water — issue #291, and
    /// docs/specs/ui/game-board.md § Colours, which is where the board's
    /// colours now live. Written before the change, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning them red against the old pale values before green — there is
    /// no editor here to watch them fail in.
    ///
    /// The hex values are written out **as literals** rather than read from
    /// <see cref="BoardColours"/>. A test that asserts a constant equals itself
    /// is a test that passes whatever the constant is changed to; these are
    /// game-board.md's table, copied here by hand, so a colour cannot be
    /// changed in the code without the spec's own value being changed with it.
    ///
    /// The last test is the odd one out and deliberately so. It asserts the
    /// acceptance criterion the issue set — every frog stays clearly separable
    /// from everything it can sit on — as arithmetic rather than as a hex
    /// value, so a future repaint of the pond, or the real frog palette
    /// arriving, cannot quietly land a green frog on a green lily pad.
    /// </summary>
    public sealed class GameBoardColoursTests
    {
        const ulong AnySeed = 20260812UL;

        // docs/specs/ui/game-board.md § Colours — the page's own table,
        // copied by hand rather than read from the code under test.
        static readonly Color PondWater = new Color32(0x9F, 0xD8, 0xF2, 0xFF);
        static readonly Color LilyPadGreen = new Color32(0xCC, 0xEA, 0xAF, 0xFF);
        static readonly Color LilyPadEdge = new Color32(0x7F, 0xAE, 0x5E, 0xFF);
        static readonly Color LogBrown = new Color32(0xE2, 0xC7, 0x9C, 0xFF);
        static readonly Color LogEdge = new Color32(0xA9, 0x7F, 0x4F, 0xFF);
        static readonly Color BandFill = new Color32(0xE2, 0xE8, 0xE5, 0xFF);

        // The bar the spec page sets for "clearly separable", and the reason
        // this issue could change the board's fills at all —
        // docs/specs/ui/game-board.md#keeping-the-frogs-visible.
        const float MinimumContrastRatio = 1.9f;
        const float MinimumColourDistance = 30f;

        [Test]
        public void TheBoardIsPaintedInPondWater()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                Assert.That(
                    view.BackgroundImage.color,
                    Is.EqualTo(PondWater),
                    "the board's background is the water — game-board.md's `PondWater`. "
                    + "It is this screen's own colour, not the app background the other "
                    + "screens paint, and it is what reaches every edge of the device.");

                Assert.That(
                    BoardColours.PondWater,
                    Is.EqualTo(PondWater),
                    "the named constant carries the spec page's value");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryLilyPadIsGreen()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                foreach (var lane in view.Lanes)
                {
                    // The seven lily pads — positions 1 to 7, and the whole of
                    // what a lane draws for itself. Position 0 and position 8
                    // are on the two logs the pond shares (#296).
                    Assert.That(lane.LilyPadFills.Count, Is.EqualTo(Lane.LanePositionCount - 2));

                    for (var index = 0; index < lane.LilyPadFills.Count; index++)
                    {
                        var position = index + 1;

                        Assert.That(
                            lane.LilyPadFills[index].color,
                            Is.EqualTo(LilyPadGreen),
                            $"{lane.Colour}'s lily pad at position {position} is `LilyPadGreen`");
                        Assert.That(
                            lane.LilyPadOutlines[index].color,
                            Is.EqualTo(LilyPadEdge),
                            $"{lane.Colour}'s lily pad at position {position} has the `LilyPadEdge` rim");
                    }
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BothLogsAreBrown()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                // Two logs for the whole pond, not a pair per lane (#296), so
                // this walks the board rather than every lane on it.
                var logs = new Dictionary<string, KeyValuePair<Image, Image>>
                {
                    { "Start", new KeyValuePair<Image, Image>(view.StartLogFill, view.StartLogOutline) },
                    { "End", new KeyValuePair<Image, Image>(view.EndLogFill, view.EndLogOutline) },
                };

                foreach (var log in logs)
                {
                    Assert.That(
                        log.Value.Key.color,
                        Is.EqualTo(LogBrown),
                        $"the {log.Key} log is `LogBrown`");
                    Assert.That(
                        log.Value.Value.color,
                        Is.EqualTo(LogEdge),
                        $"the {log.Key} log has the `LogEdge` rim, which is what "
                        + "separates a log from the water it floats on — the two are close in "
                        + "brightness on purpose and far apart in hue");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// game-board.md's open question 2, asserted rather than assumed: the
        /// header and controls bands were **deliberately left** the pale
        /// grey-green they were chosen as, and this issue did not repaint
        /// them on its way past. If they are ever changed, it is because
        /// Connor looked at the blue board and said so — and this test is the
        /// thing that has to be edited to do it.
        /// </summary>
        [Test]
        public void TheHeaderAndControlsBandsAreLeftExactlyAsTheyWere()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                Assert.That(
                    view.HeaderRect.GetComponent<Image>().color,
                    Is.EqualTo(BandFill),
                    "the header band is unchanged by this issue");
                Assert.That(
                    view.ControlsRect.GetComponent<Image>().color,
                    Is.EqualTo(BandFill),
                    "the controls band is unchanged by this issue");
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// The acceptance criterion, as arithmetic. Every one of the four frog
        /// colours against every surface a frog can sit on: the water, a lily
        /// pad, and a log.
        ///
        /// Two measures, because either one alone can be fooled. Luminance
        /// contrast catches two colours of different hue and identical
        /// brightness — which is what a colour-blind player, or anyone in
        /// bright sunlight, is left with. CIE L*a*b* distance catches two
        /// colours of the same brightness that a contrast ratio calls fine
        /// but nobody could name apart.
        ///
        /// The 4 px `FrogPieceOutline` is separation on top of this, not
        /// instead of it.
        /// </summary>
        [Test]
        public void EveryFrogStaysSeparableFromEverythingItCanSitOn()
        {
            var surfaces = new Dictionary<string, Color>
            {
                { "the water", PondWater },
                { "a lily pad", LilyPadGreen },
                { "a log", LogBrown },
            };

            foreach (FrogColour frog in Enum.GetValues(typeof(FrogColour)))
            {
                foreach (var surface in surfaces)
                {
                    var piece = FrogColours.For(frog);

                    Assert.That(
                        ContrastRatio(piece, surface.Value),
                        Is.GreaterThanOrEqualTo(MinimumContrastRatio),
                        $"the {frog} frog is too close in brightness to {surface.Key}. "
                        + "Fix the surface, not the frog — the frog colours are the art "
                        + "decision this issue does not get to move.");

                    Assert.That(
                        ColourDistance(piece, surface.Value),
                        Is.GreaterThanOrEqualTo(MinimumColourDistance),
                        $"the {frog} frog is too close in colour to {surface.Key}. "
                        + "Fix the surface, not the frog.");
                }
            }
        }

        // --- the two measures ------------------------------------------------

        // WCAG's relative luminance contrast, (L1 + 0.05) / (L2 + 0.05).
        static float ContrastRatio(Color a, Color b)
        {
            var first = RelativeLuminance(a);
            var second = RelativeLuminance(b);

            var lighter = Mathf.Max(first, second);
            var darker = Mathf.Min(first, second);

            return (lighter + 0.05f) / (darker + 0.05f);
        }

        static float RelativeLuminance(Color colour)
        {
            return (0.2126f * ToLinear(colour.r))
                + (0.7152f * ToLinear(colour.g))
                + (0.0722f * ToLinear(colour.b));
        }

        static float ToLinear(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        // Straight-line distance in CIE L*a*b* (ΔE*ab). Roughly: 2.3 is the
        // smallest difference anyone can see, and 30 is two colours nobody
        // would call shades of one another.
        static float ColourDistance(Color a, Color b)
        {
            var first = ToLab(a);
            var second = ToLab(b);

            var dl = first.x - second.x;
            var da = first.y - second.y;
            var db = first.z - second.z;

            return Mathf.Sqrt((dl * dl) + (da * da) + (db * db));
        }

        static Vector3 ToLab(Color colour)
        {
            var r = ToLinear(colour.r);
            var g = ToLinear(colour.g);
            var b = ToLinear(colour.b);

            // sRGB to CIE XYZ, D65.
            var x = ((0.4124564f * r) + (0.3575761f * g) + (0.1804375f * b)) / 0.95047f;
            var y = (0.2126729f * r) + (0.7151522f * g) + (0.0721750f * b);
            var z = ((0.0193339f * r) + (0.1191920f * g) + (0.9503041f * b)) / 1.08883f;

            var fx = LabF(x);
            var fy = LabF(y);
            var fz = LabF(z);

            return new Vector3(
                (116f * fy) - 16f,
                500f * (fx - fy),
                200f * (fy - fz));
        }

        static float LabF(float t)
        {
            const float Epsilon = 216f / 24389f;

            return t > Epsilon
                ? Mathf.Pow(t, 1f / 3f)
                : ((841f / 108f) * t) + (4f / 29f);
        }

        // --- fixtures --------------------------------------------------------

        static Game TwoFrogGame()
        {
            return new Game(new[] { FrogColour.Green, FrogColour.Blue }, AnySeed);
        }

        static GameBoardScreenView CreateView(Game game)
        {
            var host = new GameObject(nameof(GameBoardColoursTests), typeof(RectTransform));
            var view = host.AddComponent<GameBoardScreenView>();
            view.Initialize(game);
            return view;
        }

        static void Destroy(GameBoardScreenView view)
        {
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }
    }
}
