namespace Frogs.Core
{
    /// <summary>
    /// One frog's row in <see cref="Game.Standings"/> —
    /// docs/specs/ui/game-over.md's "Standings row × 2–4": a place number, the
    /// frog, and how far it got. Nothing else; the row's rendering — the
    /// colour swatch, the name, and formatting <see cref="Position"/> as
    /// "Home — 8 of 8" or "6 of 8" — is the screen's job, not this type's.
    /// </summary>
    public sealed class StandingsRow
    {
        /// <summary>A row for a frog on its default name — its colour's.</summary>
        public StandingsRow(FrogColour colour, int place, int position, bool isHome)
            : this(colour, PlayerName.DefaultFor(colour), place, position, isHome)
        {
        }

        /// <summary>A row for a frog under the name its player is playing as.</summary>
        public StandingsRow(FrogColour colour, string name, int place, int position, bool isHome)
        {
            Colour = colour;
            Name = PlayerName.Resolve(name, colour);
            Place = place;
            Position = position;
            IsHome = isHome;
        }

        /// <summary>Which frog this row is about.</summary>
        public FrogColour Colour { get; }

        /// <summary>
        /// What this frog's player is called — the word game over's standings
        /// row draws. Its colour's name by default, whatever was typed on
        /// game setup otherwise, and never a colour with a word stapled to it.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// This frog's rank, 1-based. Frogs tied on <see cref="Position"/>
        /// share the same place number — docs/specs/ui/game-over.md#open-questions:
        /// "Two frogs on the same pad currently share a place number."
        /// </summary>
        public int Place { get; }

        /// <summary>
        /// This frog's lane position — how many lily pads it made, 0 to
        /// <see cref="Lane.LaneWinningPosition"/>.
        /// </summary>
        public int Position { get; }

        /// <summary>Whether this frog reached the End log.</summary>
        public bool IsHome { get; }
    }
}
