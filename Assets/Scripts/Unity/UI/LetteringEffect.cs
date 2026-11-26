using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// Two things a lettered word needs that Unity's legacy <see cref="Text"/>
    /// cannot do on its own, done to the mesh it generates — the same way
    /// uGUI's own <see cref="Shadow"/> and <see cref="Outline"/> effects work
    /// (#520, the title screen's `START` sign):
    ///
    /// - **Tracking.** Extra space after every glyph, the last included, with
    ///   the word kept centred — what CSS <c>letter-spacing</c> does, and so
    ///   what the agreed mockup draws.
    /// - **An outline outside the glyph.** Copies of the glyphs in the outline
    ///   colour, <see cref="OutlineWidth"/> away in a ring of directions, drawn
    ///   before the fill so the fill sits on top — the mockup's
    ///   <c>-webkit-text-stroke</c> with <c>paint-order: stroke fill</c>,
    ///   whose inner half is hidden under the fill. uGUI's own
    ///   <see cref="Outline"/> draws four diagonal copies, which leaves notches
    ///   at a width this large.
    ///
    /// Works on any mesh of glyph quads; it never reads the font.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class LetteringEffect : BaseMeshEffect
    {
        // A quad is two triangles in the stream VertexHelper hands out.
        const int VerticesPerGlyph = 6;

        // How many copies make the ring. Not a size: the outline's width is
        // OutlineWidth. Sixteen leaves at most a 2.7 px chord between
        // neighbouring copies at 7 px, under every stroke of a bold display
        // font at the sizes it is drawn here, so the ring reads as one line.
        const int OutlineDirections = 16;

        readonly List<UIVertex> _stream = new List<UIVertex>();
        readonly List<UIVertex> _output = new List<UIVertex>();

        /// <summary>Extra space after every glyph, in canvas pixels.</summary>
        public float Tracking { get; private set; }

        /// <summary>How far outside the glyph the outline reaches, in canvas pixels. Zero draws none.</summary>
        public float OutlineWidth { get; private set; }

        public Color OutlineColor { get; private set; }

        public void Configure(float tracking, float outlineWidth, Color outlineColor)
        {
            Tracking = tracking;
            OutlineWidth = outlineWidth;
            OutlineColor = outlineColor;

            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            _stream.Clear();
            vh.GetUIVertexStream(_stream);

            ApplyTracking(_stream, Tracking);

            _output.Clear();

            if (OutlineWidth > 0f)
            {
                Color32 ink = OutlineColor;

                for (var direction = 0; direction < OutlineDirections; direction++)
                {
                    var angle = direction * (2f * Mathf.PI / OutlineDirections);
                    var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * OutlineWidth;

                    foreach (var vertex in _stream)
                    {
                        var copy = vertex;
                        copy.position += offset;

                        // The glyph's own coverage, in the outline's colour.
                        var alpha = (byte)((ink.a * vertex.color.a) / byte.MaxValue);
                        copy.color = new Color32(ink.r, ink.g, ink.b, alpha);
                        _output.Add(copy);
                    }
                }
            }

            // The fill last, so it is drawn on top.
            _output.AddRange(_stream);

            vh.Clear();
            vh.AddUIVertexTriangleStream(_output);
        }

        static void ApplyTracking(List<UIVertex> stream, float tracking)
        {
            if (tracking == 0f)
            {
                return;
            }

            var glyphs = stream.Count / VerticesPerGlyph;

            // CSS puts the space after each glyph, the last included, and then
            // centres the whole line — so glyph i moves by i * tracking, less
            // half of the space the line gained.
            var recentre = glyphs * tracking / 2f;

            for (var i = 0; i < stream.Count; i++)
            {
                var vertex = stream[i];
                vertex.position.x += ((i / VerticesPerGlyph) * tracking) - recentre;
                stream[i] = vertex;
            }
        }
    }
}
