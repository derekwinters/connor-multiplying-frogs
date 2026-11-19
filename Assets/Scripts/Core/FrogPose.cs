using System;

namespace Frogs.Core
{
    /// <summary>
    /// The approved frog model's two animation clips — docs/specs/ui/game-board.md
    /// § The frog piece. The clips themselves are art (tools/art/frog_model.py
    /// authors them); which one is drawn, and how far through it, is
    /// <see cref="FrogPose"/>'s.
    /// </summary>
    public enum FrogClip
    {
        /// <summary>Sitting on the pad. The pose of every frog that is not hopping.</summary>
        Sit,

        /// <summary>One hop: sit, push off, tuck, reach, sit.</summary>
        Hop
    }

    /// <summary>
    /// How the frog is posed — game-board.md § The frog piece, FP-6 to FP-8.
    ///
    /// **A pose is a pure function of hop progress, 0 to 1, and of nothing
    /// else.** The board holds no clock for it: the one hop that carries the
    /// frog from pad to pad hands its progress to <see cref="At"/>, and the
    /// frog is drawn that same <see cref="Fraction"/> of the way through the
    /// model's <see cref="FrogClip.Hop"/> clip — so the legs and the slide
    /// cannot drift apart, whatever the clip's own length.
    ///
    /// Outside a hop the frog is <see cref="Rest"/>, the start of the
    /// <see cref="FrogClip.Sit"/> clip. The Hop clip begins and ends on that
    /// same sitting pose, so a frog that has just landed is drawn exactly like
    /// one that has been sitting there all along.
    ///
    /// What the bones do at each point is the clip's — the approved model's —
    /// and Unity samples it. Nothing here, and nothing in Unity, re-poses a
    /// bone by hand.
    /// </summary>
    public readonly struct FrogPose : IEquatable<FrogPose>
    {
        // game-board.md's table, "The frog through a hop" — the progress at
        // which the approved Hop clip holds each of its key poses
        // (frog_model.py's HOP_KEYS). Sit is at 0 and again at 1.

        /// <summary>The back legs drive out straight behind.</summary>
        public const float TakeOffProgress = 0.25f;

        /// <summary>In the air, everything tucked, at the top of the hop.</summary>
        public const float InTheAirProgress = 0.5f;

        /// <summary>The front legs reach forward and down for the pad.</summary>
        public const float LandingProgress = 0.8f;

        /// <summary>Sitting — the first frame of the Sit clip.</summary>
        public static readonly FrogPose Rest = new FrogPose(FrogClip.Sit, 0f);

        public FrogPose(FrogClip clip, float fraction)
        {
            Clip = clip;
            Fraction = fraction;
        }

        /// <summary>Which of the model's clips the frog is drawn from.</summary>
        public FrogClip Clip { get; }

        /// <summary>How far through <see cref="Clip"/>, 0 its first frame and 1 its last.</summary>
        public float Fraction { get; }

        /// <summary>
        /// The pose <paramref name="progress"/> of the way through a hop: that
        /// fraction of the way through the Hop clip. Anything at or outside 0
        /// and 1 — or not a number at all — is not part of a hop, and is
        /// <see cref="Rest"/>.
        /// </summary>
        public static FrogPose At(float progress)
        {
            if (float.IsNaN(progress) || progress <= 0f || progress >= 1f)
            {
                return Rest;
            }

            return new FrogPose(FrogClip.Hop, progress);
        }

        /// <summary>
        /// The pose for a frog <paramref name="progress"/> of the way through
        /// a move from lane position <paramref name="from"/> to
        /// <paramref name="to"/>. A move from a position to itself — a wrong
        /// answer on the Start log — is not a hop, and the frog stays sitting
        /// (FP-7).
        /// </summary>
        public static FrogPose ForMove(int from, int to, float progress)
        {
            return from == to ? Rest : At(progress);
        }

        public bool Equals(FrogPose other)
        {
            return Clip == other.Clip && Fraction.Equals(other.Fraction);
        }

        public override bool Equals(object obj)
        {
            return obj is FrogPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            const int spread = 397;

            return ((int)Clip * spread) ^ Fraction.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Clip} at {Fraction:0.###}";
        }
    }
}
