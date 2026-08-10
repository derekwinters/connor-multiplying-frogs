using System;

namespace Frogs.Core
{
    /// <summary>
    /// The game's source of chance: the roll that opens a turn, and the card
    /// the pile hands over. Nothing in the game rolls anything of its own.
    ///
    /// It is **seeded**, and it can only be built from a seed or from a state
    /// it reported earlier — never from the clock, and never from nothing. Two
    /// things depend on that:
    ///
    /// - A save that is restored has to carry on, not re-randomise everything
    ///   that has not happened yet (<c>docs/adr/0004-core-owns-the-save-format.md</c>).
    ///   Restoring from the seed alone would replay rolls the players already
    ///   played, so <see cref="State"/> is what a save writes down and
    ///   <see cref="FromState"/> is what reads it back.
    /// - A generator with no seed is not testable, and a whole game has to be
    ///   replayable move for move in the two-second suite.
    ///
    /// **The sequence is the same everywhere the game runs** — an ARM64 device
    /// build, an x86_64 emulator build, and <c>dotnet test</c>. That is why the
    /// algorithm is written here rather than borrowed, why every step of it is
    /// fixed-width unsigned integer arithmetic in an <c>unchecked</c> context,
    /// and why no part of it — including the bounded draw — touches floating
    /// point. A sequence that drifts between backends is a game that does not
    /// restore the way it was saved.
    /// </summary>
    public sealed class Rng
    {
        // PCG-XSH-RR 64/32: a 64-bit state stepped by a multiply-add, and a
        // 32-bit output folded out of it. All of these are the algorithm's
        // published constants; none of them is a number to tune.
        const ulong Multiplier = 6364136223846793005UL;
        const ulong Increment = 1442695040888963407UL;
        const int XorShiftAmount = 18;
        const int OutputShiftAmount = 27;
        const int RotationSelectShift = 59;
        const int RotationMask = 31;

        // Where seeding starts from, before the seed is stirred in.
        const ulong SeedingInitialState = 0UL;

        // How many values a 32-bit draw can take. What a bounded draw has to
        // fold into its range without leaning on the low end of it.
        const ulong DistinctOutputs = 1UL << OutputBits;
        const int OutputBits = 32;

        ulong _state;

        Rng(ulong state)
        {
            _state = state;
        }

        /// <summary>
        /// A generator at the start of the sequence a seed names.
        ///
        /// The seed is stirred in and the state stepped either side of it, so
        /// that seeds one apart — two games started a moment apart — begin far
        /// apart in the sequence rather than side by side.
        /// </summary>
        public static Rng FromSeed(ulong seed)
        {
            unchecked
            {
                var generator = new Rng(SeedingInitialState);
                generator.Advance();
                generator._state += seed;
                generator.Advance();
                return generator;
            }
        }

        /// <summary>
        /// Where the generator has got to: everything needed to carry the
        /// sequence on from here, and the number a save writes down.
        /// </summary>
        public ulong State
        {
            get { return _state; }
        }

        /// <summary>
        /// A generator that carries on from a state a generator reported
        /// earlier — deliberately not the same call as starting from a seed,
        /// because a seed and a state are both a `ulong` and confusing the two
        /// silently restarts the sequence.
        /// </summary>
        public static Rng FromState(ulong state)
        {
            return new Rng(state);
        }

        /// <summary>The next 32 bits of the sequence.</summary>
        public uint NextUInt()
        {
            var drawn = Output(_state);
            Advance();
            return drawn;
        }

        /// <summary>
        /// A draw from <paramref name="minimumInclusive"/> to
        /// <paramref name="maximumInclusive"/>, both ends reachable.
        /// </summary>
        public int NextInt(int minimumInclusive, int maximumInclusive)
        {
            if (maximumInclusive < minimumInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumInclusive),
                    maximumInclusive,
                    $"A range runs upwards: {minimumInclusive} to {maximumInclusive} is a range "
                    + "with nothing in it, and drawing from one would never return.");
            }

            unchecked
            {
                var span = (ulong)((long)maximumInclusive - minimumInclusive) + 1UL;

                return (int)(minimumInclusive + (long)DrawBelow(span));
            }
        }

        /// <summary>
        /// A draw in [0, span), with no lean toward the low end.
        ///
        /// Folding 2^32 outputs into a span that does not divide it leaves a
        /// remainder, and the values that remainder reaches would otherwise be
        /// drawn one time more often than the rest. So the outputs above the
        /// last whole multiple of the span are thrown away and redrawn rather
        /// than shared out unevenly.
        /// </summary>
        ulong DrawBelow(ulong span)
        {
            unchecked
            {
                var limit = DistinctOutputs - (DistinctOutputs % span);

                while (true)
                {
                    var drawn = (ulong)NextUInt();

                    if (drawn < limit)
                    {
                        return drawn % span;
                    }
                }
            }
        }

        void Advance()
        {
            unchecked
            {
                _state = (_state * Multiplier) + Increment;
            }
        }

        static uint Output(ulong state)
        {
            unchecked
            {
                var xorshifted = (uint)(((state >> XorShiftAmount) ^ state) >> OutputShiftAmount);
                var rotation = (int)(state >> RotationSelectShift);

                return (xorshifted >> rotation) | (xorshifted << ((-rotation) & RotationMask));
            }
        }
    }
}
