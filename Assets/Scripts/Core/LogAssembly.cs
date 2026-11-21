using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>Which of the approved log model's three pieces — docs/specs/ui/game-board.md § The log model, LG-1.</summary>
    public enum LogPieceKind
    {
        /// <summary><c>LogCap</c>: the sawn end, <see cref="LogAssembly.CapLength"/> long.</summary>
        Cap,

        /// <summary><c>LogBody_A</c>: a plain middle section, <see cref="LogAssembly.BodyLength"/> long.</summary>
        BodyA,

        /// <summary><c>LogBody_B</c>: a middle section with a sawn-off branch stub on one side.</summary>
        BodyB,
    }

    /// <summary>
    /// One piece of an assembled log: which piece, where its joining face is,
    /// how far it is stretched along the log, and whether it is turned half a
    /// turn. Lengths are in frog units, from the log's start end.
    /// </summary>
    public readonly struct LogPiece
    {
        public LogPiece(LogPieceKind kind, float origin, float stretch, bool reversed)
        {
            Kind = kind;
            Origin = origin;
            Stretch = stretch;
            Reversed = reversed;
        }

        public LogPieceKind Kind { get; }

        /// <summary>Where the piece's own origin — its joining face — sits along the log, in frog units from its start.</summary>
        public float Origin { get; }

        /// <summary>How far the piece is scaled along the log. 1 for a cap, always.</summary>
        public float Stretch { get; }

        /// <summary>
        /// Turned half a turn about up, so it runs back from its origin
        /// towards the log's start rather than on towards its end, and its
        /// two sides swap. Only the start cap is.
        /// </summary>
        public bool Reversed { get; }
    }

    /// <summary>
    /// Which way a log piece runs in whatever space the importer left it in,
    /// as signed axes: ±1 for x, ±2 for y, ±3 for z, the sign saying which
    /// way along it.
    /// </summary>
    public readonly struct LogFrame
    {
        public LogFrame(int along, int up, int across, float unitLength)
        {
            Along = along;
            Up = up;
            Across = across;
            UnitLength = unitLength;
        }

        /// <summary>From the piece's joining face towards its far end.</summary>
        public int Along { get; }

        /// <summary>Out of the top of the log, away from the water.</summary>
        public int Up { get; }

        /// <summary>Towards the side <c>LogBody_B</c>'s branch stub is on — the script's +X.</summary>
        public int Across { get; }

        /// <summary>One frog unit, in the space's own units: a body's length.</summary>
        public float UnitLength { get; }
    }

    /// <summary>
    /// Puts the approved log model together — docs/specs/ui/game-board.md
    /// § The log model, LG-2 and LG-5.
    ///
    /// A log is not one mesh. It is tools/art/log_model.py's three pieces,
    /// laid end to end by the rule in that script's docstring: a cap at each
    /// end, and as many bodies between them as round to the length that is
    /// left, every one stretched the same to fill it exactly, alternating A
    /// and B. That is what lets one set of meshes draw a log of any length
    /// without a seam or a gap.
    ///
    /// Plain arithmetic in frog units (one frog unit is one lily pad, 112 px),
    /// so it lives here and is tested in the fast suite; the Unity layer
    /// turns the pieces into triangles.
    /// </summary>
    public static class LogAssembly
    {
        /// <summary>How long <c>LogCap</c> is, in frog units — the script's CAP_LEN.</summary>
        public const float CapLength = 0.5f;

        /// <summary>How long <c>LogBody_A</c> and <c>LogBody_B</c> are, in frog units — the script's BODY_LEN.</summary>
        public const float BodyLength = 1f;

        /// <summary>
        /// The shortest log the rule is defined for: two caps and one body
        /// squashed to half its length. Below it the script says the caps
        /// would have to shrink too, and how is not decided.
        /// </summary>
        public const float ShortestLength = (2f * CapLength) + (BodyLength / 2f);

        // Round half up, the script's floor(x + 0.5).
        const double RoundHalfUp = 0.5;

        // A piece is one side of a plane when its nearer end is no further
        // from that plane than this fraction of its extent.
        const double OnOneSide = 0.05;

        const int AxisCount = 3;

        /// <summary>
        /// The pieces of a log this many frog units long, from its start end:
        /// the start cap (turned), the bodies, the end cap.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The length is not a number, or shorter than <see cref="ShortestLength"/>.
        /// </exception>
        public static IReadOnlyList<LogPiece> For(float length)
        {
            if (float.IsNaN(length) || float.IsInfinity(length) || length < ShortestLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    $"A log is assembled from {ShortestLength} frog units up; shorter is not decided.");
            }

            var middle = length - (2.0 * CapLength);
            var bodies = Math.Max(1, (int)Math.Floor((middle / BodyLength) + RoundHalfUp));
            var stretch = middle / bodies;

            var pieces = new List<LogPiece>(bodies + 2)
            {
                new LogPiece(LogPieceKind.Cap, CapLength, 1f, reversed: true),
            };

            for (var k = 0; k < bodies; k++)
            {
                var kind = k % 2 == 0 ? LogPieceKind.BodyA : LogPieceKind.BodyB;

                pieces.Add(new LogPiece(kind, (float)(CapLength + (k * stretch * BodyLength)), (float)stretch, reversed: false));
            }

            pieces.Add(new LogPiece(LogPieceKind.Cap, length - CapLength, 1f, reversed: false));

            return pieces;
        }

        /// <summary>
        /// Which way <c>LogBody_B</c> runs, read off its own points, given
        /// relative to its own origin — its joining face, on the waterline —
        /// in whatever axes and units the importer left it in.
        ///
        /// Along is the axis the body lies all on one side of its joining face,
        /// towards that side. Of the other two, up is the less symmetric about
        /// the waterline — the log rides a third under it — towards the top;
        /// across is the last, towards the side its stub reaches out on. No
        /// handedness is assumed, so a mirroring importer cannot put the stub
        /// on the wrong side.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The coordinates are not one non-empty list of points, or no axis
        /// has the body all on one side of its origin.
        /// </exception>
        public static LogFrame MeasureFrame(IReadOnlyList<float> x, IReadOnlyList<float> y, IReadOnlyList<float> z)
        {
            if (x == null || y == null || z == null || x.Count != y.Count || y.Count != z.Count || x.Count == 0)
            {
                throw new ArgumentException("A log piece is one x, one y and one z for every point.", nameof(x));
            }

            var axes = new[] { x, y, z };
            var low = new double[AxisCount];
            var high = new double[AxisCount];

            for (var axis = 0; axis < AxisCount; axis++)
            {
                low[axis] = double.MaxValue;
                high[axis] = double.MinValue;

                foreach (var value in axes[axis])
                {
                    low[axis] = Math.Min(low[axis], value);
                    high[axis] = Math.Max(high[axis], value);
                }
            }

            // How far the nearer end is from the origin, as a fraction of the
            // further: 0 for a body that starts at its joining face, near 1
            // for one centred on it.
            var balance = new double[AxisCount];

            for (var axis = 0; axis < AxisCount; axis++)
            {
                var near = Math.Min(Math.Abs(low[axis]), Math.Abs(high[axis]));
                var far = Math.Max(Math.Abs(low[axis]), Math.Abs(high[axis]));

                balance[axis] = far > 0.0 ? near / far : 1.0;
            }

            var along = IndexOfSmallest(balance, except: -1);

            if (balance[along] > OnOneSide)
            {
                throw new ArgumentException("A log body starts at its joining face: no axis has it all on one side of its origin.", nameof(x));
            }

            var up = IndexOfSmallest(balance, except: along);
            var across = AxisCount - along - up;

            return new LogFrame(
                Signed(along, low, high),
                Signed(up, low, high),
                Signed(across, low, high),
                (float)(high[along] - low[along]));
        }

        static int IndexOfSmallest(double[] values, int except)
        {
            var best = -1;

            for (var i = 0; i < values.Length; i++)
            {
                if (i != except && (best < 0 || values[i] < values[best]))
                {
                    best = i;
                }
            }

            return best;
        }

        // The axis, signed towards whichever end reaches further from 0.
        static int Signed(int axis, double[] low, double[] high)
        {
            return (Math.Abs(high[axis]) >= Math.Abs(low[axis]) ? 1 : -1) * (axis + 1);
        }
    }
}
