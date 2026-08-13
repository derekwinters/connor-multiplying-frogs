namespace Frogs.Core
{
    /// <summary>
    /// The rules a player's name obeys — docs/specs/ui/game-setup.md.
    ///
    /// They live in Core rather than in the text field that collects them,
    /// because "a Core rule the UI happens to respect is one the UI can stop
    /// respecting": the cap, and the fall back to the colour name, hold for
    /// every caller and not only for the one screen that types.
    /// </summary>
    public static class PlayerName
    {
        /// <summary>
        /// The longest name a player can have —
        /// docs/specs/ui/game-setup.md#named-constants,
        /// <c>PlayerNameMaxLength</c>. Read off the setup seat's name row and
        /// not off the board's player chip: the chip's label column holds
        /// about five characters, so a chip-derived cap would refuse
        /// <c>Connor</c> at the sixth keystroke. The chip truncates instead —
        /// see <see cref="DisplayText.TruncateToWidth"/>.
        /// </summary>
        public const int PlayerNameMaxLength = 10;

        /// <summary>
        /// The name a frog carries until somebody changes it: the bare colour
        /// name, <c>Blue</c> rather than <c>Blue Frog</c>.
        /// </summary>
        public static string DefaultFor(FrogColour colour)
        {
            return colour.ToString();
        }

        /// <summary>
        /// <paramref name="typed"/> as a name this game will actually carry:
        /// blank — empty, whitespace or null — becomes
        /// <see cref="DefaultFor"/> <paramref name="colour"/>, and anything
        /// past <see cref="PlayerNameMaxLength"/> is cut to it.
        ///
        /// The cap is applied here, and not only at the keyboard that refuses
        /// the eleventh keystroke, so that no caller can hand the game a name
        /// no surface was designed to draw.
        /// </summary>
        public static string Resolve(string typed, FrogColour colour)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return DefaultFor(colour);
            }

            return typed.Length > PlayerNameMaxLength
                ? typed.Substring(0, PlayerNameMaxLength)
                : typed;
        }
    }
}
