namespace Frogs.Core
{
    /// <summary>
    /// One player's private column: the Start log, seven lily pads, and the
    /// End log. A <see cref="Lane"/> owns where its frog is and the two moves
    /// a frog can make — nothing else. Lanes never talk to each other; a
    /// player's entire state is this one number.
    /// </summary>
    public sealed class Lane
    {
        /// <summary>
        /// The Start log, seven lily pads, and the End log.
        /// docs/specs/ui/game-board.md § Named constants.
        /// </summary>
        public const int LanePositionCount = 9;

        /// <summary>
        /// The End log's index — the winning space. A frog wins by landing on
        /// it, not by reaching the last lily pad.
        /// docs/specs/ui/game-board.md § Named constants.
        /// </summary>
        public const int LaneWinningPosition = LanePositionCount - 1;

        /// <summary>Where a new frog starts — the floor of the lane.</summary>
        const int StartingPosition = 0;

        int _position = StartingPosition;

        /// <summary>How far up the lane the frog is, 0 to <see cref="LaneWinningPosition"/>.</summary>
        public int Position
        {
            get { return _position; }
        }

        /// <summary>
        /// Whether the frog has landed on the End log. Matches the
        /// <c>Home</c> chip state in docs/specs/ui/game-board.md § Behaviour.
        /// </summary>
        public bool IsHome
        {
            get { return _position == LaneWinningPosition; }
        }

        /// <summary>
        /// A correct answer: advance one lily pad. A frog that is already
        /// home is never legally asked to move — it is skipped in turn order
        /// — so this guards that call rather than encoding a rule of play:
        /// the End log clamps, symmetric with the Start log's floor.
        /// </summary>
        public void MoveForward()
        {
            if (_position < LaneWinningPosition)
            {
                _position++;
            }
        }

        /// <summary>
        /// A wrong answer: retreat one lily pad. The Start log is a floor, not
        /// a special space — a wrong answer there leaves the frog where it is.
        /// Symmetrically, a frog already home stays home: see
        /// <see cref="MoveForward"/>.
        /// </summary>
        public void MoveBack()
        {
            if (_position > StartingPosition && _position < LaneWinningPosition)
            {
                _position--;
            }
        }
    }
}
