namespace Frogs.Core
{
    /// <summary>
    /// One of the five moments a turn is in, in the fixed order a turn moves
    /// through them — docs/specs/ui/game-board.md#behaviour: "`Roll` → roll
    /// and card → working-out grid → answer result → back here with the frog
    /// moved and the turn passed to the next frog in order," and CONTEXT.md's
    /// definition of hand-off ("the board is on screen during a hand-off and
    /// not during the rest of a turn").
    ///
    /// A <see cref="Game"/> only ever moves one step forward through this
    /// order; it never skips a phase and never goes backwards. Grading an
    /// answer (what happens inside <see cref="Answering"/> and
    /// <see cref="ResultShown"/>) and how a game ends are not this type's
    /// concern — see <see cref="Game"/>.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>[Game board](docs/specs/ui/game-board.md): `Roll` enabled, nothing rolled yet.</summary>
        WaitingToRoll,

        /// <summary>
        /// [Roll and card](docs/specs/ui/roll-and-card.md): the die has
        /// landed, the pile is fixed, the card is on screen.
        /// </summary>
        RolledAndCardDrawn,

        /// <summary>
        /// [Working-out grid](docs/specs/ui/working-out-grid.md): the grid
        /// and its keypad, filling in the answer row.
        /// </summary>
        Answering,

        /// <summary>
        /// [Answer result](docs/specs/ui/answer-result.md): right or wrong,
        /// the frog's move stated in words before it happens.
        /// </summary>
        ResultShown,

        /// <summary>
        /// Back on the [game board](docs/specs/ui/game-board.md): the frog's
        /// hop animates, then the device passes to the next frog.
        /// </summary>
        HandOff
    }
}
