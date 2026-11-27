using System;

namespace Frogs.Core
{
    /// <summary>An axis-aligned rectangle, origin bottom-left, y up — the way a uGUI canvas and Unity's screen measure.</summary>
    public readonly struct LayoutRect
    {
        public LayoutRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
    }

    /// <summary>
    /// Where the title screen's art is drawn — docs/specs/ui/title-screen.md's
    /// Anchors (#533).
    ///
    /// **The art is fitted inside the device's safe area, whole and never
    /// cropped,** centred in it, at its reference size whenever the safe area
    /// holds that and scaled down evenly only when a cut-out leaves less. It
    /// is never scaled up and never stretched out of its shape. Whatever the
    /// fit leaves of the screen shows the extended scene behind it — the same
    /// pond, wider and taller — which <see cref="Scene"/> places.
    ///
    /// Plain arithmetic on rectangles, so it lives here and is tested in the
    /// fast suite; the Unity side reads the canvas and Screen.safeArea and
    /// applies the result.
    /// </summary>
    public readonly struct TitleArtFit
    {
        TitleArtFit(float scale, LayoutRect art)
        {
            Scale = scale;
            Art = art;
        }

        /// <summary>The art's drawn size over its reference size: 1, or less where a cut-out forces it.</summary>
        public float Scale { get; }

        /// <summary>Where the art is drawn, in canvas units.</summary>
        public LayoutRect Art { get; }

        /// <summary>
        /// Fits an art of <paramref name="artWidth"/> × <paramref name="artHeight"/>
        /// inside <paramref name="safeArea"/>, on a canvas of
        /// <paramref name="canvasWidth"/> × <paramref name="canvasHeight"/>, all
        /// in canvas units. A safe area with no size — before the first frame,
        /// or in a batch-mode run — is the whole canvas; a canvas with no size
        /// either gets the art at its reference size, centred on it.
        /// </summary>
        /// <exception cref="ArgumentException">The art has no width or no height.</exception>
        public static TitleArtFit Fit(float canvasWidth, float canvasHeight, LayoutRect safeArea, float artWidth, float artHeight)
        {
            // Written so a NaN fails it too.
            if (!(artWidth > 0f) || !(artHeight > 0f))
            {
                throw new ArgumentException($"The art must have a size; this one is {artWidth} × {artHeight}.");
            }

            if (!HasSize(safeArea))
            {
                safeArea = new LayoutRect(0f, 0f, canvasWidth, canvasHeight);
            }

            var scale = HasSize(safeArea)
                ? Math.Min(1f, Math.Min(safeArea.Width / artWidth, safeArea.Height / artHeight))
                : 1f;

            var width = artWidth * scale;
            var height = artHeight * scale;
            var x = safeArea.X + ((safeArea.Width - width) / 2f);
            var y = safeArea.Y + ((safeArea.Height - height) / 2f);

            return new TitleArtFit(scale, new LayoutRect(x, y, width, height));
        }

        /// <summary>
        /// The device's safe area, given in screen pixels as Unity reports it,
        /// measured in canvas units instead. A screen with no size has the
        /// whole canvas as its safe area.
        /// </summary>
        public static LayoutRect SafeAreaInCanvas(float screenWidth, float screenHeight, LayoutRect safeAreaPixels, float canvasWidth, float canvasHeight)
        {
            if (!(screenWidth > 0f) || !(screenHeight > 0f))
            {
                return new LayoutRect(0f, 0f, canvasWidth, canvasHeight);
            }

            var perPixelX = canvasWidth / screenWidth;
            var perPixelY = canvasHeight / screenHeight;

            return new LayoutRect(
                safeAreaPixels.X * perPixelX,
                safeAreaPixels.Y * perPixelY,
                safeAreaPixels.Width * perPixelX,
                safeAreaPixels.Height * perPixelY);
        }

        /// <summary>
        /// Where the extended scene is drawn behind the art, in canvas units: the same
        /// pond, wider and taller, <paramref name="sceneWidth"/> ×
        /// <paramref name="sceneHeight"/> in the art's reference pixels with the art in
        /// its middle. It is drawn at the art's <see cref="Scale"/>, centred on the
        /// art, so the two line up exactly; only where that would stop short of an edge
        /// of the <paramref name="canvasWidth"/> × <paramref name="canvasHeight"/>
        /// canvas — a screen wider than the scene, or a cut-out that shrank the art —
        /// is it scaled up about the art's centre, just enough to cover.
        /// </summary>
        public LayoutRect Scene(float canvasWidth, float canvasHeight, float sceneWidth, float sceneHeight)
        {
            var centreX = Art.X + (Art.Width / 2f);
            var centreY = Art.Y + (Art.Height / 2f);

            var scale = Math.Max(
                Scale,
                Math.Max(
                    2f * Math.Max(centreX, canvasWidth - centreX) / sceneWidth,
                    2f * Math.Max(centreY, canvasHeight - centreY) / sceneHeight));

            var width = sceneWidth * scale;
            var height = sceneHeight * scale;

            return new LayoutRect(centreX - (width / 2f), centreY - (height / 2f), width, height);
        }

        // Written so a NaN fails it too.
        static bool HasSize(LayoutRect rect) => rect.Width > 0f && rect.Height > 0f;
    }
}
