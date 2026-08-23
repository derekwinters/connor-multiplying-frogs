using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>
    /// One of the digit products a card is made of: a single part of the
    /// multiplier times a single part of the multiplicand, both
    /// **place-value expanded** — the tens digit `3` of a multiplier `34` is
    /// the part `30`, not `3`.
    ///
    /// Numbers, not strings. Drawing `30 x 10` is the shell's job
    /// (docs/specs/ui/working-out-grid.md#what-core-owns-the-product-list).
    /// </summary>
    public readonly struct DigitProduct
    {
        internal DigitProduct(int multiplierPart, int multiplicandPart)
        {
            MultiplierPart = multiplierPart;
            MultiplicandPart = multiplicandPart;
        }

        /// <summary>The part of the card's multiplier, place-value expanded.</summary>
        public int MultiplierPart { get; }

        /// <summary>The part of the card's multiplicand, place-value expanded.</summary>
        public int MultiplicandPart { get; }

        /// <summary>
        /// `30x10`. A diagnostic, so a failing test says which product it
        /// was looking at — not a format anything draws. What the player sees
        /// is the shell's to compose.
        /// </summary>
        public override string ToString()
        {
            return $"{MultiplierPart}x{MultiplicandPart}";
        }
    }

    /// <summary>
    /// The products a card's working-out is made of — what `Help me` prints
    /// (docs/specs/ui/working-out-grid.md#what-core-owns-the-product-list).
    ///
    /// Working out *which* products a card makes is game logic, so it lives
    /// here rather than in the shell, and it is testable in the fast suite
    /// with no editor. Nothing in this type draws, formats, or grades.
    /// </summary>
    public static class DigitProducts
    {
        /// <summary>
        /// The products <paramref name="card"/> is made of, in the order
        /// Derek's list has them: every part of the **multiplier**, units
        /// first; and within each, every part of the **multiplicand**, units
        /// first. `12 x 34` gives `(4,2) (4,10) (30,2) (30,10)`.
        ///
        /// A pure function: the same card gives the same list every time. No
        /// state, no <see cref="Rng"/>, nothing to reset.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="card"/> is null.</exception>
        public static IReadOnlyList<DigitProduct> For(Card card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            var products = new List<DigitProduct>();

            foreach (var multiplierPart in NonZeroPartsOf(card.Multiplier))
            {
                foreach (var multiplicandPart in NonZeroPartsOf(card.Multiplicand))
                {
                    products.Add(new DigitProduct(multiplierPart, multiplicandPart));
                }
            }

            // Read-only, and not a List handed out behind an interface: the
            // shell prints this list and never edits it.
            return products.AsReadOnly();
        }

        // An operand's place-value parts, units first, with the zeros left
        // out: 34 is 4 then 30, and 102 is 2 then 100 — the tens place
        // contributes nothing, so it contributes no part
        // (docs/specs/ui/working-out-grid.md open question 11). An operand of
        // `0` therefore has no parts at all, which is the same rule and not a
        // special case of its own.
        static IEnumerable<int> NonZeroPartsOf(int operand)
        {
            var placeValue = 1;
            var remaining = operand;

            do
            {
                var part = remaining % DigitsPerPlace * placeValue;

                if (part != 0)
                {
                    yield return part;
                }

                remaining /= DigitsPerPlace;
                placeValue *= DigitsPerPlace;
            }
            while (remaining > 0);
        }

        // Base ten: what makes the next place worth ten times this one. Not a
        // measurement and not a tunable — the game is written in decimal.
        const int DigitsPerPlace = 10;
    }
}
