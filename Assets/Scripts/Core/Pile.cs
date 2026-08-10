namespace Frogs.Core
{
    /// <summary>
    /// Which of the three sources a card is drawn from. A pile is decided by
    /// the roll (see <see cref="Roll"/>) and never chosen by the player.
    ///
    /// Named to match CONTEXT.md's glossary exactly — the roll-and-card dialog
    /// prints "Easy pile · 1 or 2", "Medium pile · 3 or 4" and
    /// "Hard pile · 5 or 6" — and deliberately not "Tier" or "Difficulty",
    /// which CONTEXT.md's avoid-list rules out even though ADR-0002's prose
    /// calls the piles "difficulty tiers".
    /// docs/specs/rules.md — "the roll's mapping to piles is fixed".
    /// </summary>
    public enum Pile
    {
        /// <summary>2-digit × 1-digit. Reached by a roll of 1 or 2.</summary>
        Easy,

        /// <summary>2-digit × 2-digit. Reached by a roll of 3 or 4.</summary>
        Medium,

        /// <summary>3-digit × 2-digit. Reached by a roll of 5 or 6.</summary>
        Hard
    }
}
