namespace Frogs.Unity.Views
{
    /// <summary>
    /// How far through docs/specs/ui/answer-result.md's fixed hand-off
    /// sequence the dialog is: "The button closes the dialog, waits
    /// `ResultHopDelay`, and the frog hops on the game board over
    /// `FrogHopDuration`. Then the next player's turn begins."
    ///
    /// Four steps, in that order and never compressed into one — the player is
    /// told what will happen and then watches it happen, which only works if
    /// the watching takes time.
    /// </summary>
    public enum AnswerResultHandOffStage
    {
        /// <summary>The dialog is up and the button has not been pressed. Nothing is moving.</summary>
        Waiting,

        /// <summary>The dialog is fading out, over the shared Dialog's own <c>DialogFadeDuration</c>.</summary>
        Closing,

        /// <summary>The pause after the dialog has gone and before the frog moves — <c>ResultHopDelay</c>.</summary>
        Holding,

        /// <summary>The frog is mid-hop, over <c>FrogHopDuration</c>.</summary>
        Hopping,

        /// <summary>The frog has landed and the next player's turn has begun.</summary>
        Complete
    }
}
