namespace Frogs.Unity.Views
{
    /// <summary>
    /// What one key of game setup's name keyboard does —
    /// docs/specs/ui/game-setup.md#the-keyboard.
    /// </summary>
    public enum NameKeyKind
    {
        /// <summary>Appends its own letter.</summary>
        Letter,

        /// <summary>Appends a space.</summary>
        Space,

        /// <summary>Deletes the last character.</summary>
        Backspace,

        /// <summary>
        /// The shift key the mockups draw. What it does is not yet settled —
        /// see <c>GameSetupScreenView</c>'s note and the open
        /// <c>type:question</c> issue — so it is drawn disabled rather than
        /// wired to a guess.
        /// </summary>
        Shift,

        /// <summary>Closes the keyboard. The only way out of it.</summary>
        Done
    }
}
