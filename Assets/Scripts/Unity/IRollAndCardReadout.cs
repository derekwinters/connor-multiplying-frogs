using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// The two questions docs/specs/ui/roll-and-card.md's dialog asks about a
    /// turn that has already begun: what face was rolled, and what card was
    /// drawn — plus whose turn it is, for the chip. Nothing here decides any
    /// of those; the roll and the draw both happened in Core
    /// (<see cref="Game.RollDie"/>) before the dialog existed.
    ///
    /// The pile is reported, not derived. The face-to-pile mapping is public
    /// on <see cref="Roll.PileForFace"/> and the dialog could work it out for
    /// itself — it deliberately does not, because "the roll selects the pile"
    /// is a rule Core owns, and a second copy of it in a view is a second
    /// copy that can drift.
    ///
    /// The problem arrives as its two operands rather than as a
    /// <see cref="Card"/>, for the same reason <see cref="ISavedGameQuery"/>
    /// exists: a test needs to state a fixed readout —
    /// docs/specs/ui/roll-and-card.md's worked example is `331 × 41` — and a
    /// <see cref="Card"/> can only come out of a draw against the game's
    /// seeded generator. Asking for the numbers the dialog prints keeps the
    /// die out of the test entirely, which is the point: the die is not
    /// random *here*.
    /// </summary>
    public interface IRollAndCardReadout
    {
        /// <summary>The frog taking this turn — the chip in `whose`.</summary>
        FrogColour Frog { get; }

        /// <summary>
        /// What this frog's player is called — their colour's name unless
        /// they typed one on game setup. The chip draws the name and only the
        /// name; nothing appends anything to it.
        /// </summary>
        string FrogName { get; }

        /// <summary>The face that came up, <see cref="Roll.MinimumFace"/> to <see cref="Roll.MaximumFace"/>.</summary>
        int Face { get; }

        /// <summary>The pile that roll sent the turn to, as Core reported it.</summary>
        Pile Pile { get; }

        /// <summary>The drawn card's first operand — the `331` in `331 × 41`.</summary>
        int Multiplicand { get; }

        /// <summary>The drawn card's second operand — the `41` in `331 × 41`.</summary>
        int Multiplier { get; }
    }
}
