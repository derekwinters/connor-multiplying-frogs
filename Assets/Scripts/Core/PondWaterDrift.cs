using System;

namespace Frogs.Core
{
    /// <summary>
    /// Which part of the water tile a surface shows: a window onto the
    /// texture, measured in tiles, with one tile being
    /// <see cref="PondWaterDrift.PondWaterTileSize"/> pixels of the reference
    /// canvas. It is exactly what a uGUI <c>RawImage</c>'s <c>uvRect</c>
    /// wants, and the tile repeats, so a window past 1 is the next tile over.
    /// </summary>
    public readonly struct PondWaterWindow
    {
        public PondWaterWindow(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The window's left edge, in tiles, counted rightwards over the texture.</summary>
        public float X { get; }

        /// <summary>The window's bottom edge, in tiles, counted upwards over the texture.</summary>
        public float Y { get; }

        /// <summary>How many tiles across the window is.</summary>
        public float Width { get; }

        /// <summary>How many tiles tall the window is.</summary>
        public float Height { get; }
    }

    /// <summary>
    /// How far the pond's water has drifted — docs/specs/ui/game-board.md
    /// § The water's marks, PW-3 and PW-4.
    ///
    /// **The drift is a pure function of elapsed time.** The board hands in
    /// how long the water has been on screen and gets back how far it has
    /// moved left; nothing here counts frames, so a slow tablet and a fast one
    /// draw the same water at the same moment.
    ///
    /// **It wraps at one tile, and the wrap is invisible.** The tile is
    /// seamless (PW-1), so moving it a whole tile shows exactly the picture it
    /// started on: the offset goes back to 0 at that point instead of growing
    /// for ever, and keeps its precision however long the board is left open.
    /// </summary>
    public static class PondWaterDrift
    {
        /// <summary>
        /// How fast the water drifts left — `PondWaterDriftSpeed`, in pixels of
        /// the 1920 x 1200 reference canvas per second (PW-3).
        /// </summary>
        public const float PondWaterDriftSpeed = 25f;

        /// <summary>
        /// How big one tile is drawn — `PondWaterTileSize`, in pixels of the
        /// reference canvas. The 512 px tile at the scale of the preview Derek
        /// approved; a look-at-it number that can be retuned.
        /// </summary>
        public const float PondWaterTileSize = 1024f;

        /// <summary>
        /// How far the water has moved left after <paramref name="elapsedSeconds"/>,
        /// in reference pixels, from 0 up to but not including one tile.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The time is negative, or not a finite number.</exception>
        public static float OffsetAt(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds),
                    elapsedSeconds,
                    "Elapsed time is a finite number of seconds, 0 or more.");
            }

            var travelled = elapsedSeconds * PondWaterDriftSpeed;
            var offset = (float)(travelled % PondWaterTileSize);

            // A remainder a hair under one tile can round up to exactly one
            // tile as a float. One tile is the start again, not past it.
            return offset >= PondWaterTileSize ? 0f : offset;
        }

        /// <summary>
        /// The part of the tile a surface shows after
        /// <paramref name="elapsedSeconds"/>. The surface is given by where it
        /// is on the screen, in reference pixels from the screen's bottom-left
        /// corner, so two surfaces that meet show one unbroken picture (PW-1).
        ///
        /// The window slides **right** over the texture as time passes, which
        /// is what moves the marks on screen **left**.
        /// </summary>
        public static PondWaterWindow WindowAt(double elapsedSeconds, float left, float bottom, float width, float height)
        {
            var offset = OffsetAt(elapsedSeconds);

            return new PondWaterWindow(
                (left + offset) / PondWaterTileSize,
                bottom / PondWaterTileSize,
                width / PondWaterTileSize,
                height / PondWaterTileSize);
        }
    }
}
