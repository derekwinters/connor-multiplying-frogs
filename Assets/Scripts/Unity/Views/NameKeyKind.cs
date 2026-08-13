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

        /// <summary>Closes the keyboard. The only way out of it.</summary>
        Done
    }
}
