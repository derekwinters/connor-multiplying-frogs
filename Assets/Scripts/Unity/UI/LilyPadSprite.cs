using System.Collections.Generic;
using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// One lily pad's whole drawing, generated rather than imported — a
    /// notched circle with a rim and a fan of veins across it, per
    /// docs/specs/ui/game-board.md's "The lily pad is notched, veined, and
    /// varies per pad" (#411). The sibling of
    /// <see cref="RoundedRectSprite"/>, and here for the same reason: this UI
    /// has no imported art, and a CSS-style border cannot follow a notch.
    ///
    /// **The notch always points at 0 degrees**, straight along the lane. What
    /// each pad points at is a rotation of the transform the sprite is drawn
    /// on, which is free — so the twelve rows of that page's variation table
    /// cost **four** sprites, one per notch width, and not twelve. Everything
    /// that varies with the direction the notch points is therefore absent
    /// from this file.
    ///
    /// Angles are the page's own: 0 points right along the lane and 90 points
    /// down, the way the mockup's SVG measures them. A texture's rows run
    /// bottom to top, so <see cref="DirectionOf"/> is the one place that flips
    /// y and the rest of this file stays in the page's convention.
    ///
    /// The pad's colours are **in** the sprite rather than tinted onto it: the
    /// rim, the surface and the veins are three colours in one drawing, so the
    /// caller hands them in and the <c>Image</c> draws it untinted. That is
    /// also the shape an imported PNG would have when real art replaces these.
    /// </summary>
    public static class LilyPadSprite
    {
        const float FullyOpaqueByte = 255f;

        // Matches CanvasScaler's default referencePixelsPerUnit (100), so a
        // sprite generated at `diameter` pixels is that many UI pixels across
        // — the same 1:1 mapping RoundedRectSprite and every geometry constant
        // in this folder already assume.
        const float PixelsPerUnit = 100f;

        /// <summary>
        /// A pad <paramref name="diameter"/> pixels across, with its notch
        /// pointing at 0 degrees.
        /// </summary>
        /// <param name="diameter">The pad's width and height in pixels — `LilyPadDiameter`.</param>
        /// <param name="outline">The rim's thickness, drawn inside the pad's own bounds — `TrackOutline`.</param>
        /// <param name="notchWidth">How wide the wedge is, in degrees — one of `LilyPadNotchAngles`.</param>
        /// <param name="notchDepth">Where the wedge's apex sits, as a fraction of the radius out from the centre — `LilyPadNotchDepth`.</param>
        /// <param name="veinAngles">Each vein's angle, measured from the notch's own axis.</param>
        /// <param name="veinInset">The gap the veins leave at the centre, as a fraction of the radius — `LilyPadVeinInset`.</param>
        /// <param name="veinOutset">The gap they leave at the rim, as a fraction of the radius — `LilyPadVeinOutset`.</param>
        /// <param name="veinWidth">A vein's stroke, in pixels — `LilyPadVeinWidth`.</param>
        /// <param name="veinOpacity">How strongly a vein reads against the surface — `LilyPadVeinOpacity`.</param>
        /// <param name="fill">The pad's surface — `LilyPadGreen`.</param>
        /// <param name="edge">Its rim, and the colour its veins are drawn in — `LilyPadEdge`.</param>
        public static Sprite Create(
            int diameter,
            float outline,
            float notchWidth,
            float notchDepth,
            IReadOnlyList<float> veinAngles,
            float veinInset,
            float veinOutset,
            float veinWidth,
            float veinOpacity,
            Color fill,
            Color edge)
        {
            var size = Mathf.Max(diameter, 1);
            var radius = size / 2f;

            // The wedge: two straight edges running from the rim to an apex
            // `notchDepth` of the way out from the centre, so the cut crosses
            // most of the pad and stops short of the middle. Its two rim ends
            // are half the notch's width either side of the direction it
            // points, which is what the table's `Notch` column measures.
            var half = notchWidth / 2f;
            var apex = DirectionOf(0f) * (notchDepth * radius);
            var rimAbove = DirectionOf(-half) * radius;
            var rimBelow = DirectionOf(half) * radius;
            var above = (rimAbove - apex).normalized;
            var below = (rimBelow - apex).normalized;

            // The veins radiate from the circle's geometric centre, not from
            // the notch's apex — game-board.md is explicit about it, because
            // drawn from the apex the fan sits off-centre and leans
            // differently on every pad.
            var veinFrom = new Vector2[veinAngles.Count];
            var veinTo = new Vector2[veinAngles.Count];

            for (var vein = 0; vein < veinAngles.Count; vein++)
            {
                var direction = DirectionOf(veinAngles[vein]);

                veinFrom[vein] = direction * (veinInset * radius);
                veinTo[vein] = direction * ((1f - veinOutset) * radius);
            }

            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Sampled at the pixel's centre, in pixels from the pad's.
                    var point = new Vector2(x + 0.5f - radius, y + 0.5f - radius);

                    // How far this pixel is inside the pad: inside the circle,
                    // and outside the wedge. Both distances are exact, so the
                    // rim follows the notch's edges at the same thickness it
                    // has anywhere else.
                    var fromApex = point - apex;
                    var inNotch = Cross(above, fromApex) <= 0f && Cross(below, fromApex) >= 0f;
                    var toNotch = Mathf.Min(
                        DistanceToSegment(point, apex, rimAbove),
                        DistanceToSegment(point, apex, rimBelow));

                    var pad = Mathf.Max(point.magnitude - radius, inNotch ? toNotch : -toNotch);

                    // A soft one-pixel edge rather than a hard cutoff, so
                    // neither the curve nor the notch aliases at the size this
                    // renders at — RoundedRectSprite's own reasoning.
                    var covered = Mathf.Clamp01(0.5f - pad);
                    var surface = Mathf.Clamp01(0.5f - (pad + outline));

                    var veined = 0f;

                    for (var vein = 0; vein < veinFrom.Length; vein++)
                    {
                        var distance = DistanceToSegment(point, veinFrom[vein], veinTo[vein]);

                        veined = Mathf.Max(veined, Mathf.Clamp01(0.5f - (distance - (veinWidth / 2f))));
                    }

                    // Rim, then surface over it, then the veins over that —
                    // and the veins only ever on the surface, so a fan can
                    // never reach the rim however the numbers move.
                    var colour = Color.Lerp(edge, fill, surface);
                    colour = Color.Lerp(colour, edge, veined * veinOpacity * surface);

                    pixels[(y * size) + x] = new Color32(
                        ToByte(colour.r),
                        ToByte(colour.g),
                        ToByte(colour.b),
                        ToByte(covered));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Frogs Lily Pad (procedural)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            var rect = new Rect(0f, 0f, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            const uint extrude = 0;

            // Drawn whole rather than sliced: a pad has no stretchable middle,
            // and it is exactly `LilyPadDiameter` across wherever it appears.
            return Sprite.Create(texture, rect, pivot, PixelsPerUnit, extrude, SpriteMeshType.FullRect);
        }

        // The page measures angles the way the mockup's SVG does — 0 points
        // right along the lane, 90 points down — and a texture's rows run the
        // other way, so this is where y is flipped and nowhere else.
        static Vector2 DirectionOf(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        }

        static float Cross(Vector2 first, Vector2 second)
        {
            return (first.x * second.y) - (first.y * second.x);
        }

        static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var along = to - from;
            var lengthSquared = along.sqrMagnitude;

            if (lengthSquared <= 0f)
            {
                return (point - from).magnitude;
            }

            var howFar = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);

            return (point - (from + (howFar * along))).magnitude;
        }

        static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * FullyOpaqueByte);
        }
    }
}
