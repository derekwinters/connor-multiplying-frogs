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
        public StandingsRow(FrogColour colour, int place, int position, bool isHome)
        {
            Colour = colour;
            Place = place;
            Position = position;
            IsHome = isHome;
        }

        /// <summary>Which frog this row is about.</summary>
        public FrogColour Colour { get; }

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
