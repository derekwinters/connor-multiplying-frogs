namespace Frogs.Core
{
    /// <summary>
    /// Which of the three things happened when a submitted answer was graded
    /// against a <see cref="Card"/>'s answer — docs/specs/rules.md § Moving:
    ///
    /// | Outcome | Effect |
    /// | --- | --- |
    /// | Correct | Forward one lily pad |
    /// | Wrong, anywhere above the Start log | Back one lily pad |
    /// | Wrong, on the Start log | Stay |
    ///
    /// Three outcomes, not two: the Start log is a floor, not a special case,
    /// so a wrong answer there is its own named outcome rather than a "back"
    /// that silently does nothing. See <see cref="Lane.Resolve"/>, which maps
    /// each of these onto the moves <see cref="Lane"/> already provides.
    /// </summary>
    public enum TurnOutcome
    {
        /// <summary>The submitted answer matched the card. The frog hops forward one lily pad.</summary>
        Correct,

        /// <summary>The submitted answer was wrong, and the frog was above the Start log. It hops back one lily pad.</summary>
        WrongAboveStartLog,

        /// <summary>The submitted answer was wrong, and the frog was already on the Start log — the floor. It stays.</summary>
        WrongOnStartLog
    }
}
