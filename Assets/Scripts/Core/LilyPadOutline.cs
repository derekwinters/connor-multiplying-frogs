using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>
    /// Reads the approved lily pad model's outline — docs/specs/ui/game-board.md
    /// § The lily pad is notched, veined, and varies per pad, LP-3.
    ///
    /// The model is a circle with a wedge cut from it, centred on its own
    /// origin. Which way its notch points, and how big it is, are read off the
    /// points of that outline rather than assumed from how an importer turned
    /// the file's axes or scaled its units — the same reason the frog's facing
    /// is read off its bones. The board then turns the pad from that bearing
    /// to the one the variation table gives.
    ///
    /// Plain arithmetic on points in the pad's own plane, so it lives here and
    /// is tested in the fast suite; the Unity layer hands it the model's
    /// vertices.
    /// </summary>
    public static class LilyPadOutline
    {
        // A point is on the rim when it is at least this fraction of the way
        // out to the furthest point. The model's rings step inwards from the
        // apex, so the next ring in is well inside it.
        const float RimFraction = 0.97f;

        // The notch is the widest gap between rim points, and it must be
        // clearly the widest: at least this many times the next widest. A
        // whole circle has no such gap, and says nothing about which way it
        // points.
        const float NotchGapRatio = 1.5f;

        // Up is the way the rim (beyond OuterFraction of the radius) sits
        // higher than the middle (within InnerFraction): the model's rim
        // curls up from 0.72 of the radius.
        const double OuterFraction = 0.9;
        const double InnerFraction = 0.5;

        const double FullTurn = 360.0;

        /// <summary>How far the outline reaches from its centre: the furthest point's distance.</summary>
        /// <exception cref="ArgumentException">The coordinates are not one list of points.</exception>
        public static float Radius(IReadOnlyList<float> x, IReadOnlyList<float> y)
        {
            Require(x, y);

            var furthest = 0.0;

            for (var i = 0; i < x.Count; i++)
            {
                furthest = Math.Max(furthest, Math.Sqrt((x[i] * (double)x[i]) + (y[i] * (double)y[i])));
            }

            return (float)furthest;
        }

        /// <summary>
        /// Which way the outline's notch points, in degrees counter-clockwise
        /// from +x, in [0, 360): the middle of the widest gap between the
        /// points on its rim.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The coordinates are not one list of points, or the outline has no
        /// gap clearly wider than the rest — no notch to point with.
        /// </exception>
        public static float NotchBearing(IReadOnlyList<float> x, IReadOnlyList<float> y)
        {
            var radius = Radius(x, y);
            var rim = new List<double>();

            for (var i = 0; i < x.Count; i++)
            {
                var distance = Math.Sqrt((x[i] * (double)x[i]) + (y[i] * (double)y[i]));

                if (distance >= RimFraction * radius)
                {
                    rim.Add(Normalise(Math.Atan2(y[i], x[i]) * 180.0 / Math.PI));
                }
            }

            if (rim.Count < 2)
            {
                throw new ArgumentException("A lily pad's outline needs a rim to find its notch on.", nameof(x));
            }

            rim.Sort();

            var widest = 0.0;
            var nextWidest = 0.0;
            var middle = 0.0;

            for (var i = 0; i < rim.Count; i++)
            {
                var from = rim[i];
                var to = i + 1 < rim.Count ? rim[i + 1] : rim[0] + FullTurn;
                var gap = to - from;

                if (gap > widest)
                {
                    nextWidest = widest;
                    widest = gap;
                    middle = from + (gap / 2.0);
                }
                else if (gap > nextWidest)
                {
                    nextWidest = gap;
                }
            }

            if (widest < NotchGapRatio * nextWidest)
            {
                throw new ArgumentException("A lily pad's outline has no notch: no gap in its rim is clearly the widest.", nameof(x));
            }

            return (float)Normalise(middle);
        }

        /// <summary>
        /// Which way is up out of the pad's top face, as a signed axis: ±1 for
        /// x, ±2 for y, ±3 for z, the sign saying which way along it.
        ///
        /// A pad is a thin leaf, so up lies along its **thinnest** axis; and
        /// its rim curls up, so up is the way its outer points sit, on
        /// average, higher than its middle ones. Neither depends on whether
        /// the importer kept the file's Y-up or laid it on its back.
        /// </summary>
        /// <exception cref="ArgumentException">The coordinates are not one list of points.</exception>
        public static int UpAxis(IReadOnlyList<float> x, IReadOnlyList<float> y, IReadOnlyList<float> z)
        {
            Require(x, y);
            Require(y, z);

            var axes = new[] { x, y, z };
            var thinnest = 0;
            var thinnestExtent = double.MaxValue;

            for (var axis = 0; axis < axes.Length; axis++)
            {
                var extent = Extent(axes[axis]);

                if (extent < thinnestExtent)
                {
                    thinnest = axis;
                    thinnestExtent = extent;
                }
            }

            var a = axes[(thinnest + 1) % axes.Length];
            var b = axes[(thinnest + 2) % axes.Length];
            var up = axes[thinnest];
            var radius = Radius(a, b);

            var outer = 0.0;
            var outerCount = 0;
            var inner = 0.0;
            var innerCount = 0;

            for (var i = 0; i < up.Count; i++)
            {
                var distance = Math.Sqrt((a[i] * (double)a[i]) + (b[i] * (double)b[i]));

                if (distance >= OuterFraction * radius)
                {
                    outer += up[i];
                    outerCount++;
                }
                else if (distance <= InnerFraction * radius)
                {
                    inner += up[i];
                    innerCount++;
                }
            }

            if (outerCount == 0 || innerCount == 0)
            {
                throw new ArgumentException("A lily pad's outline needs a middle and a rim to tell which way is up.", nameof(x));
            }

            var sign = (outer / outerCount) >= (inner / innerCount) ? 1 : -1;

            return sign * (thinnest + 1);
        }

        static double Extent(IReadOnlyList<float> values)
        {
            var min = double.MaxValue;
            var max = double.MinValue;

            foreach (var value in values)
            {
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            return max - min;
        }

        static double Normalise(double degrees)
        {
            var d = degrees % FullTurn;

            if (d < 0.0)
            {
                d += FullTurn;
            }

            // A float of 359.99999 rounds to 360, which is not in [0, 360).
            return (float)d >= FullTurn ? 0.0 : d;
        }

        static void Require(IReadOnlyList<float> x, IReadOnlyList<float> y)
        {
            if (x == null || y == null || x.Count != y.Count)
            {
                throw new ArgumentException("A lily pad's outline is one x and one y for every point.", nameof(x));
            }
        }
    }
}
