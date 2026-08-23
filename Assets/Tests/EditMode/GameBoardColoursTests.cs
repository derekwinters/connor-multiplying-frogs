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
    /// The pond reads as water — issues #291 and #301, and
    /// docs/specs/ui/game-board.md § Colours, which is where the board's
    /// colours now live. Written before the change, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning them red against the values they replace before green —
    /// there is no editor here to watch them fail in.
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
        static readonly Color LilyPadGreen = new Color32(0xB2, 0xE6, 0x7F, 0xFF);
        static readonly Color LilyPadEdge = new Color32(0x6E, 0x9E, 0x4A, 0xFF);
        static readonly Color LogBrown = new Color32(0x4A, 0x2E, 0x1A, 0xFF);
        static readonly Color LogLabelInk = new Color32(0xC6, 0xB4, 0x9C, 0xFF);
        static readonly Color BandFill = new Color32(0xE2, 0xE8, 0xE5, 0xFF);
        static readonly Color BoardInk = new Color32(0x1E, 0x24, 0x22, 0xFF);

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

        /// <summary>
        /// Read out of the pad's own pixels rather than off an
        /// <c>Image.color</c>, because since #411 a pad is one drawing rather
        /// than a green disc inside a darker one: its surface, its rim and its
        /// veins are three colours in one sprite. That is a stronger check
        /// than the tint was — it is what ends up on the screen — and it is
        /// still this page's two named colours it is checked against.
        /// </summary>
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
                    Assert.That(lane.LilyPads.Count, Is.EqualTo(Lane.LanePositionCount - 2));

                    for (var index = 0; index < lane.LilyPads.Count; index++)
                    {
                        var position = index + 1;
                        var pad = lane.LilyPads[index].sprite;

                        // The pad is drawn with its notch pointing at 0 and
                        // turned into place, so both samples are taken well
                        // away from it: on the surface between two veins, and
                        // half way through the rim directly opposite.
                        AssertPixel(
                            pad,
                            SurfaceDegrees,
                            SurfaceRadius,
                            LilyPadGreen,
                            $"{lane.Colour}'s lily pad at position {position} is `LilyPadGreen`");
                        AssertPixel(
                            pad,
                            RimDegrees,
                            RimRadius,
                            LilyPadEdge,
                            $"{lane.Colour}'s lily pad at position {position} has the `LilyPadEdge` rim");
                    }
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        // Where a pad is sampled, in degrees from its own notch and as a
        // fraction of its radius: a patch of surface between two veins, and
        // the middle of the rim opposite the notch, where no vein reaches
        // (they stop `LilyPadVeinOutset` short of it).
        const float SurfaceDegrees = 210f;
        const float SurfaceRadius = 0.35f;
        const float RimDegrees = 180f;

        static readonly float RimRadius =
            1f - (GameBoardLaneView.TrackOutline / GameBoardLaneView.LilyPadDiameter);

        // A byte of slack: the pad is composited in floats and stored as
        // Color32.
        const float ColourTolerance = 2f / 255f;

        static void AssertPixel(Sprite sprite, float degrees, float radiusFraction, Color expected, string what)
        {
            var size = sprite.texture.width;
            var radius = size / 2f;
            var radians = degrees * Mathf.Deg2Rad;

            // game-board.md measures angles the way the mockup's SVG does — 0
            // right, 90 down — and a texture's rows run the other way.
            var x = Mathf.FloorToInt(radius + (radiusFraction * radius * Mathf.Cos(radians)));
            var y = Mathf.FloorToInt(radius - (radiusFraction * radius * Mathf.Sin(radians)));

            var actual = (Color)sprite.texture.GetPixels32()[(y * size) + x];

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColourTolerance), $"{what}: red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColourTolerance), $"{what}: green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColourTolerance), $"{what}: blue");
            Assert.That(actual.a, Is.EqualTo(1f).Within(ColourTolerance), $"{what}: opaque");
        }

        /// <summary>
        /// The log is one colour and one shape — game-board.md § Colours.
        ///
        /// **`LogEdge` is gone**, so this asserts the *absence* of a rim as
        /// well as the presence of the fill. On the old pale tan log a rim was
        /// what made a log read as floating on the pond rather than as a hole
        /// in it; against `#4A2E1A` the fill clears the water on its own, and
        /// a rim darker than that fill was invisible. A log that quietly
        /// regrew a second painted layer would look almost right and would be
        /// nobody's decision, which is why one Image per log is asserted
        /// rather than assumed.
        /// </summary>
        [Test]
        public void BothLogsAreBrown_AndNeitherHasARim()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                // Two logs for the whole pond, not a pair per lane (#296), so
                // this walks the board rather than every lane on it.
                var logs = new Dictionary<string, Image>
                {
                    { "Start", view.StartLog },
                    { "End", view.EndLog },
                };

                foreach (var log in logs)
                {
                    Assert.That(
                        log.Value.color,
                        Is.EqualTo(LogBrown),
                        $"the {log.Key} log is `LogBrown`");

                    Assert.That(
                        log.Value.GetComponentsInChildren<Image>(includeInactive: true).Length,
                        Is.EqualTo(1),
                        $"the {log.Key} log draws one thing — its own fill. `LogEdge` is gone "
                        + "and the log has no rim, so there is no second painted layer inside it.");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// `Start` and `End`, at the **top** of their own log, in
        /// `LogLabelInk` — game-board.md § Colours and the committed mockup's
        /// `.log` rule.
        ///
        /// Top rather than middle, because the middle of a log is where frogs
        /// stand: the pond band is 896 px tall and every lane's centre line
        /// crosses both logs, so a centred word is a word with a frog on it.
        /// </summary>
        [Test]
        public void EachLogIsLabelledAtItsTopInLogLabelInk()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var labels = new Dictionary<string, KeyValuePair<Text, Image>>
                {
                    { "Start", new KeyValuePair<Text, Image>(view.StartLogLabel, view.StartLog) },
                    { "End", new KeyValuePair<Text, Image>(view.EndLogLabel, view.EndLog) },
                };

                foreach (var entry in labels)
                {
                    var label = entry.Value.Key;
                    var log = entry.Value.Value;

                    Assert.That(label.text, Is.EqualTo(entry.Key), "the log says what it is");
                    Assert.That(
                        label.color,
                        Is.EqualTo(LogLabelInk),
                        $"the {entry.Key} log's word is `LogLabelInk`. The old mid-brown was "
                        + "chosen against a pale tan log and is unreadable on this one.");
                    Assert.That(
                        label.fontSize,
                        Is.EqualTo((int)GameBoardScreenView.LogLabelSize));

                    var labelRect = label.rectTransform;

                    Assert.That(
                        labelRect.parent,
                        Is.SameAs(log.transform),
                        "the word belongs to the log, so it travels with it on every screen width");

                    // Pinned to the log's top edge and spanning its width, so
                    // `UpperCenter` centres it across the log rather than
                    // across whatever size the text happens to be.
                    Assert.That(label.alignment, Is.EqualTo(TextAnchor.UpperCenter));
                    Assert.That(labelRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                    Assert.That(labelRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                    Assert.That(labelRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                    Assert.That(
                        labelRect.anchoredPosition,
                        Is.EqualTo(new Vector2(0f, -GameBoardScreenView.LogLabelTopPadding)),
                        $"the {entry.Key} log's word sits `LogLabelTopPadding` below the top of "
                        + "the log — not in its middle, which is where the frogs stand");
                    Assert.That(labelRect.sizeDelta.x, Is.EqualTo(0f).Within(0.001f));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// The two arithmetic claims that removing `LogEdge` rests on, held to
        /// the same bar the frogs are held to rather than taken on trust.
        ///
        /// The rim used to be what separated a log from the water — the old
        /// tan log and the water were 1.05 : 1 apart, so the fill could not do
        /// it. `LogBrown` can, which is the whole reason the rim could go. And
        /// with the rim gone the word on the log has only the fill behind it,
        /// so it is measured against the fill.
        /// </summary>
        [Test]
        public void TheLogReadsAgainstTheWater_AndItsLabelAgainstTheLog_WithNoRimToHelp()
        {
            var pairs = new Dictionary<string, KeyValuePair<Color, Color>>
            {
                { "the log against the water", new KeyValuePair<Color, Color>(LogBrown, PondWater) },
                { "the log's label against the log", new KeyValuePair<Color, Color>(LogLabelInk, LogBrown) },
            };

            foreach (var pair in pairs)
            {
                Assert.That(
                    ContrastRatio(pair.Value.Key, pair.Value.Value),
                    Is.GreaterThanOrEqualTo(MinimumContrastRatio),
                    $"{pair.Key} is too close in brightness, and there is no rim left to carry it");

                Assert.That(
                    ColourDistance(pair.Value.Key, pair.Value.Value),
                    Is.GreaterThanOrEqualTo(MinimumColourDistance),
                    $"{pair.Key} is too close in colour, and there is no rim left to carry it");
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
                        + "Either side may move: Derek reversed \"the surface moves, not the "
                        + "frog\" on #301, because with the pond he picked no set of four frog "
                        + "colours existed. Move them together and re-measure — "
                        + "game-board.md#how-the-ponds-colours-are-constrained.");

                    Assert.That(
                        ColourDistance(piece, surface.Value),
                        Is.GreaterThanOrEqualTo(MinimumColourDistance),
                        $"the {frog} frog is too close in colour to {surface.Key}. "
                        + "Either side may move, and they move together — "
                        + "game-board.md#how-the-ponds-colours-are-constrained.");
                }
            }
        }

        /// <summary>
        /// #321 — the gear lost the white disc it used to be drawn on, so it
        /// is now ink straight onto the board. The disc was doing legibility
        /// work for free; with it gone the gear has to clear the same
        /// separability bar every frog clears, against every surface it can
        /// sit on.
        ///
        /// Two surfaces, because the gear sits in the header band and the
        /// header band sits on the water — `BandFill` is what is behind it
        /// today, and `PondWater` is what would be behind it if the band were
        /// ever dropped. It clears the bar against both, so which one is
        /// behind it does not decide anything.
        /// </summary>
        [Test]
        public void TheSettingsGearStaysSeparableFromWhateverIsBehindIt()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var gear = view.SettingsButton.Glyph.color;

                Assert.That(
                    gear,
                    Is.EqualTo(BoardInk),
                    "the gear is drawn in the board's own ink — game-board.md's `BoardInk`");

                var surfaces = new Dictionary<string, Color>
                {
                    { "the header band", BandFill },
                    { "the water", PondWater },
                };

                foreach (var surface in surfaces)
                {
                    Assert.That(
                        ContrastRatio(gear, surface.Value),
                        Is.GreaterThanOrEqualTo(MinimumContrastRatio),
                        $"the gear is too close in brightness to {surface.Key}");

                    Assert.That(
                        ColourDistance(gear, surface.Value),
                        Is.GreaterThanOrEqualTo(MinimumColourDistance),
                        $"the gear is too close in colour to {surface.Key}");
                }
            }
            finally
            {
                Destroy(view);
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
