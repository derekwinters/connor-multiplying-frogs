using System;
using System.Text;

namespace Frogs.Core
{
    /// <summary>
    /// One naming session on one seat — docs/specs/ui/game-setup.md#behaviour.
    /// The setup screen's keyboard is a set of buttons on top of this: a
    /// letter key calls <see cref="Append"/>, backspace calls
    /// <see cref="Backspace"/>, and `Done` calls <see cref="Commit"/>.
    ///
    /// There is no cancel, and this type deliberately offers none: "a name is
    /// edited in place and every keystroke has already happened, so there is
    /// nothing a cancel would undo that backspace does not."
    /// </summary>
    public sealed class PlayerNameEditor
    {
        // What separates one word from the next, for the capitalisation rule.
        const char WordSeparator = ' ';

        readonly FrogColour _colour;
        readonly StringBuilder _text;

        /// <summary>
        /// Opens a session on <paramref name="entry"/>, starting from the
        /// name that seat already carries — docs/specs/ui/game-setup.md#behaviour:
        /// tapping a name row "puts the caret at the end of the name."
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
        public PlayerNameEditor(RosterEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _colour = entry.Colour;
            _text = new StringBuilder(entry.Name);
        }

        /// <summary>The frog being named — the one the header's prompt names.</summary>
        public FrogColour Colour
        {
            get { return _colour; }
        }

        /// <summary>
        /// What has been typed so far. May be empty part-way through an edit;
        /// <see cref="Commit"/> is where empty becomes the colour name again.
        /// </summary>
        public string Text
        {
            get { return _text.ToString(); }
        }

        /// <summary>
        /// Appends one character, unless the name is already
        /// <see cref="PlayerName.PlayerNameMaxLength"/> long — in which case
        /// the keystroke is refused and nothing changes.
        ///
        /// The character's case is **derived from where it lands**, not taken
        /// from the caller: a capital at the start of the name and after a
        /// space, lowercase everywhere else. So `C O N N O R` is `Connor` and
        /// `M A R Y` space `J A N E` is `Mary Jane`, and the field reads that
        /// way while it is being typed rather than being corrected at the
        /// end. docs/specs/ui/game-setup.md#behaviour, settling #319 — the
        /// keyboard has no shift key, so this is the only thing that decides
        /// case.
        /// </summary>
        /// <returns>
        /// Whether the character landed. False is the refusal the keyboard
        /// renders as a key that does nothing: "nothing explains itself,
        /// which is how a disabled button already behaves in this game."
        /// </returns>
        public bool Append(char character)
        {
            if (_text.Length >= PlayerName.PlayerNameMaxLength)
            {
                return false;
            }

            _text.Append(StartsAWord() ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            return true;
        }

        // Read off the text as it stands rather than remembered, which is
        // what makes backspacing and retyping re-derive the case instead of
        // carrying a stale flag.
        bool StartsAWord()
        {
            return _text.Length == 0 || _text[_text.Length - 1] == WordSeparator;
        }

        /// <summary>Deletes the last character. A no-op on an empty name.</summary>
        public void Backspace()
        {
            if (_text.Length > 0)
            {
                _text.Length -= 1;
            }
        }

        /// <summary>Empties the name — what holding backspace down arrives at.</summary>
        public void Clear()
        {
            _text.Length = 0;
        }

        /// <summary>
        /// Closes the session: the seat under the name typed, or — if that
        /// name is blank — back under its colour name, because "a nameless
        /// frog is not a state this screen can reach."
        /// </summary>
        public RosterEntry Commit()
        {
            return new RosterEntry(_colour, Text);
        }
    }
}
