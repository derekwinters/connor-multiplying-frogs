using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// Procedural rounded-rect sprite generation, shared by every component
    /// under this folder that needs a rounded panel and has no imported
    /// texture to draw one with — docs/specs/ui/shared-components.md's "no
    /// external assets".
    ///
    /// <see cref="Button"/> (#214) generates its own copy of this same shape
    /// inline, after <c>Resources.GetBuiltinResource&lt;Sprite&gt;("UI/Skin/UISprite.psd")</c>
    /// returned null on CI's Unity version rather than throwing — see
    /// <c>Button.cs</c>'s own comment for the full story. This issue
    /// (#219) needs the identical shape for the Dialog panel and the Player
    /// chip, so it lives here once rather than as two more inline copies;
    /// <c>Button.cs</c> is left untouched rather than refactored to call
    /// this, since it already has a tested implementation of its own and
    /// there is no editor here to re-verify a refactor of it against.
    /// </summary>
    public static class RoundedRectSprite
    {
        const float FullyOpaqueByte = 255f;

        // Matches CanvasScaler's default referencePixelsPerUnit (100), so the
        // sliced border renders as exactly `radius` UI pixels — the same 1:1
        // pixel-to-unit mapping every other geometry constant in this UI
        // folder already assumes.
        const float PixelsPerUnit = 100f;

        /// <summary>
        /// A square alpha-mask sprite just big enough to hold one rounded
        /// corner (radius*2+1, so there is exactly one solid centre pixel),
        /// sliced with a border of <paramref name="radius"/> on every side.
        /// uGUI's Sliced Image stretches only the 1-pixel middle band, so the
        /// corner stays exactly <paramref name="radius"/> texture pixels
        /// regardless of how large the target RectTransform is.
        /// </summary>
        public static Sprite CreateRoundedRect(int radius)
        {
            radius = Mathf.Max(radius, 1);
            var size = radius * 2 + 1;
            var half = size / 2f;
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Signed distance from a box of half-size `half` rounded
                    // by `radius`, sampled at the pixel centre — the standard
                    // rounded-box SDF. <= 0 is inside the shape.
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    var dx = Mathf.Max(Mathf.Abs(px) - (half - radius), 0f);
                    var dy = Mathf.Max(Mathf.Abs(py) - (half - radius), 0f);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    // A soft one-pixel edge instead of a hard cutoff, so the
                    // curve doesn't alias at the sizes this renders at.
                    var alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)(alpha * FullyOpaqueByte));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Frogs Rounded Rect (procedural)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            var rect = new Rect(0f, 0f, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            var border = new Vector4(radius, radius, radius, radius);
            const uint extrude = 0;

            return Sprite.Create(texture, rect, pivot, PixelsPerUnit, extrude, SpriteMeshType.FullRect, border);
        }
    }
}
