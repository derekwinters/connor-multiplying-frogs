using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// Everything the working-out grid needs of the turn it was opened on —
    /// docs/specs/ui/working-out-grid.md: whose turn it is, the card being
    /// worked, and the one place the answer goes when `Check it` is pressed.
    ///
    /// It is not called a *readout* the way <see cref="IRollAndCardReadout"/>
    /// is, because it is not only read from: <see cref="SubmitAnswer"/> is the
    /// seam the answer row's digits leave this screen through, on their way to
    /// Core's turn resolution (<see cref="Lane.Resolve"/>, #210). Grading them
    /// happens there and is shown on
    /// [answer result](docs/specs/ui/answer-result.md) (#224); nothing on this
    /// side of the seam ever learns whether the answer was right, which is
    /// what keeps ADR-0002's "nothing in the grid is marked" true by
    /// construction rather than by discipline.
    ///
    /// The card arrives as a <see cref="Card"/> rather than as two operands —
    /// unlike <see cref="IRollAndCardReadout"/>, which prints them — because
    /// this screen does not print the problem so much as *derive its grid
    /// from* it, and <see cref="WorkingOutGrid.For"/> takes a card.
    /// </summary>
    public interface IWorkingOutTurn
    {
        /// <summary>The frog taking this turn — the chip in the header.</summary>
        FrogColour Frog { get; }

        /// <summary>The pile the card came from, as Core reported it. Named in the header, never re-derived here.</summary>
        Pile Pile { get; }

        /// <summary>The card being worked. The grid's columns and printed digits are a function of this.</summary>
        Card Card { get; }

        /// <summary>
        /// Hands the answer row's digits, read left to right as one number, to
        /// Core's turn resolution. Called exactly once per press of an enabled
        /// `Check it`, and never with an empty answer — an empty answer is not
        /// a wrong answer, so it cannot be submitted at all.
        /// </summary>
        void SubmitAnswer(int answer);
    }
}
