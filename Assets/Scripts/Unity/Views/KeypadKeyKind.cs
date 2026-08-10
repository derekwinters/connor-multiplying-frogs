namespace Frogs.Unity.Views
{
    /// <summary>
    /// What one key of the working-out grid's keypad does —
    /// docs/specs/ui/working-out-grid.md § Elements: "`1`–`9`, `0`, backspace,
    /// `clear`. No decimal point, no minus: every answer in this game is a
    /// positive whole number."
    ///
    /// There is deliberately no <c>Undo</c> and no <c>Equals</c> entry: "there
    /// is no `undo`, only backspace and `clear`", and open question 6 answers
    /// the `=` key with "no, `Check it` is the commit".
    /// </summary>
    public enum KeypadKeyKind
    {
        /// <summary>One of the ten digit keys, `0` to `9`.</summary>
        Digit,

        /// <summary>Backspace — takes one digit away and nothing else.</summary>
        Backspace,

        /// <summary>`clear` — empties the cell block the caret is in, and only that.</summary>
        Clear
    }
}
