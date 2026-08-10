using System;
using Frogs.Core;

namespace Frogs.Unity
{
    /// <summary>
    /// The runtime <see cref="IRollAndCardReadout"/>: a live
    /// <see cref="Game"/>, read at the moment it is asked. Every property is
    /// a straight read of what Core already decided — there is no arithmetic
    /// anywhere in this type, and no <c>Rng</c>.
    ///
    /// It reads the game each time rather than copying values at
    /// construction, so a dialog built before <see cref="Game.RollDie"/> and
    /// shown after it still shows the right roll.
    /// </summary>
    public sealed class GameRollAndCardReadout : IRollAndCardReadout
    {
        readonly Game _game;

        /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
        public GameRollAndCardReadout(Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <inheritdoc />
        public FrogColour Frog
        {
            get { return _game.ActiveFrog; }
        }

        /// <inheritdoc />
        public int Face
        {
            get { return RequireRoll().Face; }
        }

        /// <inheritdoc />
        public Pile Pile
        {
            get { return RequireRoll().Pile; }
        }

        /// <inheritdoc />
        public int Multiplicand
        {
            get { return RequireCard().Multiplicand; }
        }

        /// <inheritdoc />
        public int Multiplier
        {
            get { return RequireCard().Multiplier; }
        }

        // The dialog only ever opens on a turn that has rolled. Asking before
        // that is a wiring mistake, and it says so rather than reporting a
        // face of zero that would draw a die with no pips on it.
        Roll RequireRoll()
        {
            var roll = _game.DrawnRoll;

            if (roll == null)
            {
                throw new InvalidOperationException(
                    "this turn has not rolled yet; there is no face to show.");
            }

            return roll;
        }

        Card RequireCard()
        {
            var card = _game.DrawnCard;

            if (card == null)
            {
                throw new InvalidOperationException(
                    "this turn has not drawn a card yet; there is no problem to show.");
            }

            return card;
        }
    }
}
