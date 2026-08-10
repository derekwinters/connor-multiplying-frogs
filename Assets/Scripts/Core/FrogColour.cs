namespace Frogs.Core
{
    /// <summary>
    /// Which of the four frogs a player is playing. Exactly four are ever
    /// offered — docs/specs/ui/shared-components.md#frog-colours: "exactly
    /// four are offered" — and a <see cref="Game"/> never accepts two roster
    /// entries of the same colour.
    ///
    /// Turn order is not this type's concern: a colour says who a frog *is*,
    /// not where it sits or when it goes. The order a <see cref="Game"/> is
    /// constructed with is the turn order, first to last — see
    /// docs/specs/ui/game-setup.md#invariants.
    /// </summary>
    public enum FrogColour
    {
        /// <summary>docs/specs/ui/shared-components.md § Named constants — `FrogGreen`.</summary>
        Green,

        /// <summary>docs/specs/ui/shared-components.md § Named constants — `FrogBlue`.</summary>
        Blue,

        /// <summary>docs/specs/ui/shared-components.md § Named constants — `FrogOrange`.</summary>
        Orange,

        /// <summary>docs/specs/ui/shared-components.md § Named constants — `FrogPink`.</summary>
        Pink
    }
}
