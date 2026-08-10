namespace Frogs.Core
{
    /// <summary>
    /// The fact produced by grading one submitted answer — which of the three
    /// <see cref="TurnOutcome"/>s occurred, the frog's <see cref="Lane"/>
    /// position immediately before and after, and the card's correct answer.
    /// See <see cref="Lane.Resolve"/>, the only place this is constructed.
    ///
    /// Nothing else. docs/specs/ui/answer-result.md's regions read backwards
    /// pin exactly these four facts and no more — no formatted strings, no
    /// colour, no next player, no button label. Those are the dialog's job to
    /// render (#224) and #208's turn-advancement job to compute, not this
    /// type's to carry.
    /// </summary>
    public sealed class TurnResolution
    {
        public TurnResolution(TurnOutcome outcome, int positionBefore, int positionAfter, int correctAnswer)
        {
            Outcome = outcome;
            PositionBefore = positionBefore;
            PositionAfter = positionAfter;
            CorrectAnswer = correctAnswer;
        }

        /// <summary>Which of the three things happened.</summary>
        public TurnOutcome Outcome { get; }

        /// <summary>The frog's lane position immediately before this answer was graded.</summary>
        public int PositionBefore { get; }

        /// <summary>The frog's lane position immediately after this answer was graded.</summary>
        public int PositionAfter { get; }

        /// <summary>
        /// The card's true answer — revealed on a wrong answer per
        /// docs/adr/0002-structured-working-out-grid.md ("the correct answer
        /// is revealed; the working is not"), and equally present on a
        /// correct one since <c>verdict</c> shows the full equation either way.
        /// </summary>
        public int CorrectAnswer { get; }
    }
}
