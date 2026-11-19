using System;

namespace Frogs.Core
{
    /// <summary>
    /// Where a frog's legs are — docs/specs/ui/game-board.md § The frog piece,
    /// FP-6 and "The legs through a hop".
    ///
    /// **A pose is a pure function of hop progress, 0 to 1, and of nothing
    /// else.** The board holds no clock for the legs: the one hop that carries
    /// the frog from pad to pad hands its progress to <see cref="At"/>, so the
    /// legs and the frog cannot drift apart. A hop starts and ends at
    /// <see cref="Rest"/>, so a frog that has just landed is drawn exactly like
    /// one that has been standing there all along.
    ///
    /// A pose has one <see cref="Front"/> and one <see cref="Back"/>, never
    /// four independent legs: the two legs of a pair move together, because a
    /// frog hops with both feet. Which way the frog faces, and what its legs
    /// look like, are the Unity layer's — this is angles only.
    /// </summary>
    public readonly struct FrogLegPose
    {
        // game-board.md's table, "The legs through a hop" — where each moment
        // falls in the hop. Rest is at 0 and again at 1.
        public const float TakeOffProgress = 0.25f;
        public const float InTheAirProgress = 0.5f;
        public const float LandingProgress = 0.8f;

        /// <summary>Tucked, standing on the pad — before and after every hop.</summary>
        public static readonly FrogLegPose Rest = new FrogLegPose(new LegJoints(20f, 30f), new LegJoints(60f, 150f));

        /// <summary>The back legs kick out behind, straight.</summary>
        public static readonly FrogLegPose TakeOff = new FrogLegPose(new LegJoints(-10f, 40f), new LegJoints(-50f, 10f));

        /// <summary>All four legs tucked in.</summary>
        public static readonly FrogLegPose InTheAir = new FrogLegPose(new LegJoints(-30f, 60f), new LegJoints(50f, 140f));

        /// <summary>The front legs reach forward for the landing.</summary>
        public static readonly FrogLegPose Landing = new FrogLegPose(new LegJoints(50f, 0f), new LegJoints(40f, 120f));

        static readonly float[] s_moments = { 0f, TakeOffProgress, InTheAirProgress, LandingProgress, 1f };
        static readonly FrogLegPose[] s_poses = { Rest, TakeOff, InTheAir, Landing, Rest };

        public FrogLegPose(LegJoints front, LegJoints back)
        {
            Front = front;
            Back = back;
        }

        /// <summary>Both front legs.</summary>
        public LegJoints Front { get; }

        /// <summary>Both back legs.</summary>
        public LegJoints Back { get; }

        /// <summary>
        /// The pose <paramref name="progress"/> of the way through a hop. Between
        /// two of the table's moments the angles move in a straight line.
        /// Anything outside 0 to 1 — or not a number at all — is not part of a
        /// hop, and is <see cref="Rest"/>.
        /// </summary>
        public static FrogLegPose At(float progress)
        {
            if (float.IsNaN(progress) || progress <= 0f || progress >= 1f)
            {
                return Rest;
            }

            var next = 1;
            while (progress > s_moments[next])
            {
                next++;
            }

            var from = s_moments[next - 1];
            var t = (progress - from) / (s_moments[next] - from);

            return Lerp(s_poses[next - 1], s_poses[next], t);
        }

        static FrogLegPose Lerp(FrogLegPose a, FrogLegPose b, float t)
        {
            return new FrogLegPose(LegJoints.Lerp(a.Front, b.Front, t), LegJoints.Lerp(a.Back, b.Back, t));
        }
    }

    /// <summary>
    /// One leg's two joints, in degrees — game-board.md § The legs through a
    /// hop.
    /// </summary>
    public readonly struct LegJoints
    {
        public LegJoints(float hip, float knee)
        {
            Hip = hip;
            Knee = knee;
        }

        /// <summary>The thigh's angle from straight down: positive swings it forward, toward the head; negative, back toward the tail.</summary>
        public float Hip { get; }

        /// <summary>How far the shin folds back from straight along the thigh: 0 is a straight leg, larger is more tucked.</summary>
        public float Knee { get; }

        internal static LegJoints Lerp(LegJoints a, LegJoints b, float t)
        {
            return new LegJoints(a.Hip + ((b.Hip - a.Hip) * t), a.Knee + ((b.Knee - a.Knee) * t));
        }
    }
}
