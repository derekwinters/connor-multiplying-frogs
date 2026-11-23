using System;

namespace Frogs.Core
{
    /// <summary>
    /// How big the frog piece is drawn, and where — docs/specs/ui/game-board.md
    /// § The frog piece, FP-2 (#474).
    ///
    /// **The frog's one scale is chosen from its Sit pose.** Given the box the
    /// sitting frog fills in the board's view, in the model's own units, the
    /// fit is the largest scale at which that box, plus the outline on every
    /// side, still fits the <c>FrogPieceDiameter</c> square — and the box's
    /// centre is drawn at the square's centre. The hop is drawn at that same
    /// scale and is allowed to overshoot the square; the fit never pulls a
    /// point in.
    ///
    /// Plain arithmetic on a box, so it lives here and is tested in the fast
    /// suite; the Unity side measures the box off the skinned mesh.
    /// </summary>
    public readonly struct FrogPieceFit
    {
        FrogPieceFit(float scale, float centreX, float centreY)
        {
            Scale = scale;
            CentreX = centreX;
            CentreY = centreY;
        }

        /// <summary>Pixels per model unit.</summary>
        public float Scale { get; }

        /// <summary>The model x drawn at the piece's centre: the middle of the fitted box.</summary>
        public float CentreX { get; }

        /// <summary>The model y drawn at the piece's centre: the middle of the fitted box.</summary>
        public float CentreY { get; }

        /// <summary>
        /// Fits the box <paramref name="minX"/>..<paramref name="maxX"/> ×
        /// <paramref name="minY"/>..<paramref name="maxY"/>, with
        /// <paramref name="outline"/> around it, into a square
        /// <paramref name="footprint"/> across.
        /// </summary>
        /// <exception cref="ArgumentException">The box has no width or no height.</exception>
        public static FrogPieceFit Fit(float minX, float minY, float maxX, float maxY, float footprint, float outline)
        {
            var width = maxX - minX;
            var height = maxY - minY;

            // Written so a NaN fails it too.
            if (!(width > 0f) || !(height > 0f))
            {
                throw new ArgumentException($"A box to fit must have a size; this one is {width} × {height}.");
            }

            var room = footprint - (2f * outline);
            var scale = Math.Max(0f, Math.Min(room / width, room / height));

            return new FrogPieceFit(scale, (minX + maxX) / 2f, (minY + maxY) / 2f);
        }

        /// <summary>Where a model x is drawn, in pixels from the piece's centre.</summary>
        public float ToPieceX(float x)
        {
            return (x - CentreX) * Scale;
        }

        /// <summary>Where a model y is drawn, in pixels from the piece's centre.</summary>
        public float ToPieceY(float y)
        {
            return (y - CentreY) * Scale;
        }
    }
}
