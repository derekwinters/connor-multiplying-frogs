using System;
using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// The runtime <see cref="IAnswerResultTurn"/>: a live <see cref="Game"/>
    /// and the <see cref="TurnResolution"/> its active frog's answer just
    /// produced. There is no arithmetic in this type and no grading — every
    /// member is a straight read of something Core already decided, or a
    /// forward of one of Core's own two hand-off steps.
    ///
    /// The frog and the card are captured at construction, because that is the
    /// moment they describe: the answer was graded then, and
    /// <see cref="Game.CompleteHandOff"/> clears the drawn card and moves the
    /// active frog on. <see cref="NextFrog"/> is read live, because it is the
    /// one fact the dialog wants *as it is now* — before hand-off has run.
    ///
    /// Two of its members are guarded for the last turn of a completed game,
    /// and they sit next to each other below with one comment over both.
    /// </summary>
    public sealed class GameAnswerResultTurn : IAnswerResultTurn
    {
        readonly Game _game;
        readonly Card _card;

        /// <exception cref="ArgumentNullException"><paramref name="game"/> or <paramref name="resolution"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The turn has not drawn a card — there is no problem to show.</exception>
        public GameAnswerResultTurn(Game game, TurnResolution resolution)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));

            _card = game.DrawnCard;

            if (_card == null)
            {
                throw new InvalidOperationException(
                    "this turn has not drawn a card yet; there is no problem to show.");
            }

            Frog = game.ActiveFrog;
        }

        /// <inheritdoc />
        public FrogColour Frog { get; }

        /// <inheritdoc />
        public int Multiplicand
        {
            get { return _card.Multiplicand; }
        }

        /// <inheritdoc />
        public int Multiplier
        {
            get { return _card.Multiplier; }
        }

        /// <inheritdoc />
        public TurnResolution Resolution { get; }

        // --- The turn whose hop got the last frog home ------------------------
        //
        // Both members below are guarded for the same one turn in a game, and
        // they are kept together so the next person finds both.
        //
        // docs/specs/ui/game-board.md#behaviour: "When the last frog gets home,
        // the game ends itself. The hop finishes, and game over follows with no
        // input from anybody." From the moment that answer is graded there is
        // no next player — not to name on the button, and not to pass the
        // device to — and Core says so by throwing from both
        // Game.NextActiveFrog and Game.CompleteHandOff. The dialog asks each of
        // them once on every turn, including this one, so the shell answers
        // "there is nobody" rather than letting Core's exception reach a child
        // on the winning move.

        /// <inheritdoc />
        public FrogColour? NextFrog
        {
            get { return _game.IsOver ? (FrogColour?)null : _game.NextActiveFrog; }
        }

        /// <inheritdoc />
        public void BeginHandOff()
        {
            _game.BeginHandOff();
        }

        /// <inheritdoc />
        public void CompleteHandOff()
        {
            // The step the spec says does not happen is the step not taken,
            // rather than one taken and caught.
            if (_game.IsOver)
            {
                return;
            }

            // Otherwise just the advance, and deliberately nothing else. A frog
            // that lands on the End log has its place in the finishing order
            // captured by Core itself, inside Game.ShowResult — the phase
            // before this one, and the first one after a move that no caller
            // can skip. The shell does not have to remember to announce an
            // arrival, and must not: a second announcement from here would be
            // a duplicate of a fact Core already holds.
            _game.CompleteHandOff();
        }
    }
}
