namespace Frogs.Core
{
    /// <summary>
    /// One seat's worth of roster: which frog, and what that player is
    /// called. docs/specs/ui/game-setup.md#behaviour — "Seating a frog gives
    /// it the bare colour name — `Blue`, not `Blue Frog`. A default name is a
    /// real name, not a placeholder: it is stored, drawn and spoken about
    /// exactly like a typed one, and nothing anywhere appends a word to it."
    ///
    /// A name is roster data, so it lives here with the rest of the roster
    /// rather than in the setup screen that happens to collect it: a name the
    /// Unity layer keeps to itself is a name the board cannot draw, and — per
    /// docs/adr/0004-core-owns-the-save-format.md — a save that restores a
    /// game without its players' names restores the wrong game.
    /// </summary>
    public sealed class RosterEntry
    {
        /// <summary>This seat's frog, on its default name.</summary>
        public RosterEntry(FrogColour colour)
            : this(colour, null)
        {
        }

        /// <summary>
        /// This seat's frog under <paramref name="name"/>, put through
        /// <see cref="PlayerName.Resolve"/> — so a blank name is the colour
        /// name and an over-long one is capped, whoever the caller is.
        /// </summary>
        public RosterEntry(FrogColour colour, string name)
        {
            Colour = colour;
            Name = PlayerName.Resolve(name, colour);
        }

        /// <summary>Which frog this seat holds.</summary>
        public FrogColour Colour { get; }

        /// <summary>
        /// What this player is called — never empty, and never anything but
        /// the name itself.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The same seat under a different name. Blank restores the colour
        /// name; anything longer than <see cref="PlayerName.PlayerNameMaxLength"/>
        /// is capped.
        /// </summary>
        public RosterEntry WithName(string name)
        {
            return new RosterEntry(Colour, name);
        }
    }
}
