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

        /// <inheritdoc />
        public FrogColour NextFrog
        {
            get { return _game.NextActiveFrog; }
        }

        /// <inheritdoc />
        public void BeginHandOff()
        {
            _game.BeginHandOff();
        }

        /// <inheritdoc />
        public void CompleteHandOff()
        {
            // Just the advance. A frog that lands on the End log also needs
            // its place in the finishing order captured
            // (<see cref="Game.RecordFinish"/>, #211), and this is a plausible
            // moment to do it — but recording a finish is not this issue's,
            // nothing here is tested against it, and a silent extra call to
            // Core is exactly the kind of thing that goes unnoticed until it
            // is wrong.
            _game.CompleteHandOff();
        }
    }
}
