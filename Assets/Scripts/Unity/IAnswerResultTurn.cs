using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// Everything docs/specs/ui/answer-result.md's dialog needs of the turn it
    /// was opened on, and the one way it hands that turn on. Every fact here
    /// was decided before the dialog existed: **"Nothing is decided here. Core
    /// has already compared the answer and computed the new position; this
    /// dialog reads it out."**
    ///
    /// Like <see cref="IWorkingOutTurn"/> and unlike
    /// <see cref="IRollAndCardReadout"/>, this is not called a *readout*,
    /// because it is not only read from: the two hand-off calls are how the
    /// one button on the dialog passes the device to the next player.
    ///
    /// The problem arrives as its two operands rather than as a
    /// <see cref="Card"/>, the same choice <see cref="IRollAndCardReadout"/>
    /// makes and for the same reason — the dialog prints `331 × 41 = 13,571`,
    /// and a test that wants to assert that string should not have to reach a
    /// seeded generator to get it.
    /// </summary>
    public interface IAnswerResultTurn
    {
        /// <summary>The frog whose answer this was — the chip, and the name in the consequence sentence.</summary>
        FrogColour Frog { get; }

        /// <summary>The card's first operand — the `331` in `331 × 41 = 13,571`.</summary>
        int Multiplicand { get; }

        /// <summary>The card's second operand — the `41` in `331 × 41 = 13,571`.</summary>
        int Multiplier { get; }

        /// <summary>
        /// What Core made of the answer (#210): which of the three outcomes
        /// happened, the lane position before and after, and the card's
        /// correct answer. The dialog renders these; it derives none of them,
        /// and it never re-grades.
        /// </summary>
        TurnResolution Resolution { get; }

        /// <summary>
        /// Who the button is named for — <see cref="Game.NextActiveFrog"/>
        /// (#208): the frog that *would* become active next, skipping any that
        /// are home, asked while this turn's result is still on screen and
        /// nothing has advanced. Not <see cref="Frog"/>, and not something the
        /// dialog works out by walking turn order.
        ///
        /// **Null on exactly one turn in a game:** the one whose hop got the
        /// last frog home. docs/specs/ui/game-board.md#behaviour — "When the
        /// last frog gets home, the game ends itself" — so there is no next
        /// player to name, and <see cref="Game.NextActiveFrog"/> says so by
        /// throwing. Nullable rather than throwing here, because the dialog
        /// asks this question on every turn including that one, and a fact the
        /// caller is expected to handle is a value, not an exception.
        /// </summary>
        FrogColour? NextFrog { get; }

        /// <summary>
        /// The result dialog has closed and the board is back on screen for
        /// the frog's hop — <see cref="Game.BeginHandOff"/>'s own moment.
        /// Called once, as the button is pressed.
        /// </summary>
        void BeginHandOff();

        /// <summary>
        /// The hop has finished: the next player's turn begins —
        /// <see cref="Game.CompleteHandOff"/>. Called once, and only after the
        /// frog has landed, so nothing about whose turn it is changes while
        /// the frog is still mid-air.
        /// </summary>
        void CompleteHandOff();
    }
}
