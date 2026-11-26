using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LetteringEffect = Frogs.Unity.UI.LetteringEffect;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The START sign's lettering (#520): the two things the mockup's CSS does
    /// to the word that Unity's legacy <see cref="Text"/> cannot —
    /// <c>letter-spacing</c>, and a stroke painted <i>under</i> the fill so
    /// the outline sits outside the glyph (<c>paint-order: stroke fill</c>).
    /// Driven through <see cref="LetteringEffect.ModifyMesh(VertexHelper)"/>
    /// directly, on hand-made glyph quads, so the arithmetic is checked
    /// without a font.
    /// </summary>
    public sealed class LetteringEffectTests
    {
        const float GlyphWidth = 40f;
        const float Tracking = 2f;
        const float OutlineWidth = 7f;
        const float Tolerance = 0.001f;

        static readonly Color Fill = Color.white;
        static readonly Color Ink = Color.black;

        [Test]
        public void Tracking_AddsItsWidthAfterEveryGlyph_AndKeepsTheWordCentred()
        {
            var effect = CreateEffect(Tracking, 0f);

            try
            {
                var output = Run(effect, glyphs: 3);

                // No outline asked for: the same three quads, moved.
                Assert.That(output.Count, Is.EqualTo(3 * 6));

                // CSS letter-spacing puts the space after each letter, the last
                // included, and the whole line is centred — so glyph i moves by
                // i * tracking, less half of all three.
                for (var glyph = 0; glyph < 3; glyph++)
                {
                    var expectedShift = (glyph * Tracking) - (3 * Tracking / 2f);
                    Assert.That(output[glyph * 6].position.x, Is.EqualTo(GlyphLeft(glyph, 3) + expectedShift).Within(Tolerance), $"glyph {glyph}");
                }
            }
            finally
            {
                Object.DestroyImmediate(effect.gameObject);
            }
        }

        [Test]
        public void TheOutline_IsDrawnUnderTheFill_InItsOwnColour_OutlineWidthAway()
        {
            var effect = CreateEffect(0f, OutlineWidth);

            try
            {
                var output = Run(effect, glyphs: 1);
                var fillStart = output.Count - 6;

                Assert.That(output.Count, Is.GreaterThan(6), "outline copies were added");
                Assert.That(output.Count % 6, Is.EqualTo(0));

                // The fill is last, so it is drawn on top, unchanged.
                for (var i = fillStart; i < output.Count; i++)
                {
                    Assert.That((Color)output[i].color, Is.EqualTo(Fill));
                }

                Assert.That(output[fillStart].position.x, Is.EqualTo(GlyphLeft(0, 1)).Within(Tolerance));

                // Every copy before it is ink, exactly OutlineWidth from the fill.
                for (var copy = 0; copy < fillStart; copy += 6)
                {
                    Assert.That((Color)output[copy].color, Is.EqualTo(Ink), $"copy {copy / 6}");

                    var offset = (Vector2)(output[copy].position - output[fillStart].position);
                    Assert.That(offset.magnitude, Is.EqualTo(OutlineWidth).Within(Tolerance), $"copy {copy / 6}");
                }
            }
            finally
            {
                Object.DestroyImmediate(effect.gameObject);
            }
        }

        static LetteringEffect CreateEffect(float tracking, float outlineWidth)
        {
            var go = new GameObject(nameof(LetteringEffectTests), typeof(RectTransform), typeof(Text));
            var effect = go.AddComponent<LetteringEffect>();
            effect.Configure(tracking, outlineWidth, Ink);
            return effect;
        }

        // Glyphs laid side by side with no gap, centred on zero, the way a
        // centred Text lays them out.
        static float GlyphLeft(int glyph, int count) => (glyph * GlyphWidth) - (count * GlyphWidth / 2f);

        static List<UIVertex> Run(LetteringEffect effect, int glyphs)
        {
            using (var helper = new VertexHelper())
            {
                for (var glyph = 0; glyph < glyphs; glyph++)
                {
                    var left = GlyphLeft(glyph, glyphs);
                    var quad = new UIVertex[4];
                    var corners = new[]
                    {
                        new Vector2(left, 0f), new Vector2(left, GlyphWidth),
                        new Vector2(left + GlyphWidth, GlyphWidth), new Vector2(left + GlyphWidth, 0f)
                    };

                    for (var i = 0; i < 4; i++)
                    {
                        quad[i] = UIVertex.simpleVert;
                        quad[i].position = corners[i];
                        quad[i].color = Fill;
                    }

                    helper.AddUIVertexQuad(quad);
                }

                effect.ModifyMesh(helper);

                var output = new List<UIVertex>();
                helper.GetUIVertexStream(output);
                return output;
            }
        }
    }
}
