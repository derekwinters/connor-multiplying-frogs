using System;
using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// The runtime <see cref="IWorkingOutTurn"/>: the turn a live
    /// <see cref="Game"/> has already rolled and drawn a card for, and the one
    /// place the answer that comes back out of the grid goes.
    ///
    /// The frog, the pile and the card are captured at construction, because
    /// that is the moment they describe — the grid is opened on one turn's card
    /// and stays on it until the answer is handed over. Nothing here formats
    /// anything, and nothing here grades: <see cref="SubmitAnswer"/> hands the
    /// number to <see cref="Lane.Resolve"/>, which is Core's grader (#210), and
    /// keeps the <see cref="TurnResolution"/> it produced so the answer-result
    /// dialog can be shown Core's verdict rather than a second opinion.
    ///
    /// It also takes the phase step Core requires immediately afterwards.
    /// <see cref="Game.ShowResult"/> documents why that is not optional and why
    /// it belongs to whoever called <see cref="Lane.Resolve"/>: it is "the first
    /// moment after a move that a caller cannot skip", and it is where a frog
    /// landing on its End log has its place in the finishing order captured.
    /// Splitting the two apart is how that recording gets forgotten.
    /// </summary>
    public sealed class GameWorkingOutTurn : IWorkingOutTurn
    {
        readonly Game _game;
        readonly Card _card;

        /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The turn has not rolled and drawn a card yet.</exception>
        public GameWorkingOutTurn(Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));

            var roll = game.DrawnRoll;

            if (roll == null)
            {
                throw new InvalidOperationException(
                    "this turn has not rolled yet; there is no pile to work from.");
            }

            _card = game.DrawnCard;

            if (_card == null)
            {
                throw new InvalidOperationException(
                    "this turn has not drawn a card yet; there is no problem to work.");
            }

            Frog = game.ActiveFrog;
            Pile = roll.Pile;
        }

        /// <inheritdoc />
        public FrogColour Frog { get; }

        /// <inheritdoc />
        public Pile Pile { get; }

        /// <inheritdoc />
        public Card Card
        {
            get { return _card; }
        }

        /// <summary>
        /// What Core made of the answer this turn submitted, or null before
        /// <see cref="SubmitAnswer"/> has run. The one fact the answer-result
        /// dialog is built from — read from here rather than re-derived, so
        /// nothing outside <see cref="Lane.Resolve"/> ever grades anything.
        /// </summary>
        public TurnResolution Resolution { get; private set; }

        /// <inheritdoc />
        public void SubmitAnswer(int answer)
        {
            Resolution = _game.LaneFor(Frog).Resolve(answer, _card);
            _game.ShowResult();
        }
    }
}
