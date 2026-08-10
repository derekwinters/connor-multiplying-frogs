namespace Frogs.Unity.Views
{
    /// <summary>
    /// How far through its entry the roll-and-card dialog is —
    /// docs/specs/ui/roll-and-card.md's Behaviour section, which describes
    /// the entry as three moments in a fixed order.
    ///
    /// This is a *presentation* state, not a turn state: Core's
    /// <see cref="Frogs.Core.TurnPhase"/> is already
    /// <c>RolledAndCardDrawn</c> throughout all three of these. Nothing here
    /// decides anything — the roll and the draw both happened before the
    /// dialog opened.
    /// </summary>
    public enum RollAndCardEntryPhase
    {
        /// <summary>The die is still rolling. No face is shown yet, and neither is the pile or the card.</summary>
        Rolling,

        /// <summary>The die has settled and the pile label has appeared; the card is dealing in.</summary>
        Dealing,

        /// <summary>Face shown, pile label shown, card in place — where a tap anywhere jumps to.</summary>
        Settled
    }
}
