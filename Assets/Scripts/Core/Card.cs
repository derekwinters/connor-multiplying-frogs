using System;

namespace Frogs.Core
{
    /// <summary>
    /// A multiplication problem shaped to a pile, together with its true
    /// product — CONTEXT.md's glossary: "a single multiplication problem
    /// together with its answer". Drawn from the game's seeded
    /// <see cref="Rng"/> (#200) and from nothing else, so a given seed
    /// replays the same sequence of cards.
    ///
    /// docs/adr/0002-structured-working-out-grid.md pins exactly three
    /// shapes, read off the pile labels in the board photograph:
    ///
    /// | Pile | Shape | Example |
    /// | --- | --- | --- |
    /// | Easy | 2-digit × 1-digit | `68 × 5` |
    /// | Medium | 2-digit × 2-digit | `22 × 41` |
    /// | Hard | 3-digit × 2-digit | `331 × 41` |
    ///
    /// That table pins digit counts and nothing else. Which values inside a
    /// shape's bounds come up, and how often, is not decided by this type —
    /// see <see cref="OneDigitMinimum"/> — and per docs/specs/reference/index.md
    /// #still-unsettled, the rest of what the classroom deck actually
    /// constrains (repeated digits, forced carries, trivial problems) waits on
    /// the real deck being photographed (#171).
    /// </summary>
    public sealed class Card
    {
        /// <summary>
        /// The lowest value a single-digit operand may draw.
        ///
        /// Whether the classroom deck ever deals a multiplier of `0` is not
        /// settled by any sample card, ADR, or spec page (#203) — "1-digit"
        /// could mean 0–9 or 1–9. `0` is the arithmetically neutral reading of
        /// "single digit" and adds no constraint beyond the digit count, so it
        /// is what this provisional generator draws from; it is not a claim
        /// that `0` belongs in the real deck, and nothing here asserts it one
        /// way or the other.
        /// </summary>
        public const int OneDigitMinimum = 0;

        /// <summary>The highest value a single-digit operand may draw.</summary>
        public const int OneDigitMaximum = 9;

        /// <summary>The lowest value a 2-digit operand may draw.</summary>
        public const int TwoDigitMinimum = 10;

        /// <summary>The highest value a 2-digit operand may draw.</summary>
        public const int TwoDigitMaximum = 99;

        /// <summary>The lowest value a 3-digit operand may draw.</summary>
        public const int ThreeDigitMinimum = 100;

        /// <summary>The highest value a 3-digit operand may draw.</summary>
        public const int ThreeDigitMaximum = 999;

        Card(int multiplicand, int multiplier)
        {
            Multiplicand = multiplicand;
            Multiplier = multiplier;
        }

        /// <summary>The first operand — printed first on the card, e.g. the `68` in `68 × 5`.</summary>
        public int Multiplicand { get; }

        /// <summary>The second operand — printed second on the card, e.g. the `5` in `68 × 5`.</summary>
        public int Multiplier { get; }

        /// <summary>
        /// The true product of <see cref="Multiplicand"/> and
        /// <see cref="Multiplier"/> — the only thing ADR-0002 says is graded.
        /// </summary>
        public int Product
        {
            get { return Multiplicand * Multiplier; }
        }

        /// <summary>
        /// A card shaped for <paramref name="pile"/>, drawn from
        /// <paramref name="rng"/> and from nothing else.
        /// </summary>
        public static Card Draw(Pile pile, Rng rng)
        {
            switch (pile)
            {
                case Pile.Easy:
                    return new Card(
                        rng.NextInt(TwoDigitMinimum, TwoDigitMaximum),
                        rng.NextInt(OneDigitMinimum, OneDigitMaximum));

                case Pile.Medium:
                    return new Card(
                        rng.NextInt(TwoDigitMinimum, TwoDigitMaximum),
                        rng.NextInt(TwoDigitMinimum, TwoDigitMaximum));

                case Pile.Hard:
                    return new Card(
                        rng.NextInt(ThreeDigitMinimum, ThreeDigitMaximum),
                        rng.NextInt(TwoDigitMinimum, TwoDigitMaximum));

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(pile),
                        pile,
                        $"no card shape is defined for {pile}.");
            }
        }
    }
}
