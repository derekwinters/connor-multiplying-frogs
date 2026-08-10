using System;
using System.Collections.Generic;
using System.Linq;

namespace Frogs.Core
{
    /// <summary>
    /// A whole game: the roster and its turn order, a <see cref="Lane"/> per
    /// frog, and which of the five <see cref="TurnPhase"/> moments the
    /// current turn is in. A screen asks this type "whose turn is it, and
    /// what should I be showing right now?" — nothing else holds that answer.
    ///
    /// A <see cref="Game"/> owns advancing to the next player and the rule
    /// that a turn's phase only ever moves forward one step at a time. It
    /// does **not** own whether an answer is right or how far a frog moves as
    /// a result — that is grading, <c>core-turn-resolution</c> (#210). It
    /// does **not** own how a game ends — that is <c>core-game-end</c> (#211).
    /// This type only guarantees that advancing to the next player never
    /// hangs, even when every frog is home, so #211 has a seam to build on.
    /// </summary>
    public sealed class Game
    {
        /// <summary>
        /// The fewest frogs a game can be played with.
        /// docs/specs/ui/game-setup.md#invariants: "a game cannot start with
        /// fewer than two frogs."
        /// </summary>
        public const int MinFrogsPerGame = 2;

        /// <summary>
        /// The most frogs a game can be played with — a recorded rule change
        /// from the classroom game's 2–8 players; see ADR-0001 and
        /// docs/specs/future-ideas.md#five-to-eight-players.
        /// docs/specs/ui/game-setup.md#invariants: "or more than four."
        /// </summary>
        public const int MaxFrogsPerGame = 4;

        readonly FrogColour[] _turnOrder;
        readonly IReadOnlyDictionary<FrogColour, Lane> _lanes;
        readonly Rng _rng;
        readonly ulong _seed;

        int _activeIndex;
        TurnPhase _phase;
        Roll _drawnRoll;
        Card _drawnCard;

        /// <summary>
        /// A game for <paramref name="turnOrder"/> — already-ordered, first
        /// frog to last — running on <paramref name="seed"/>.
        /// docs/specs/ui/game-setup.md#behaviour: "`Start` begins the game
        /// with the chosen frogs in badge order" — the order the caller hands
        /// in *is* the turn order; this type does not reorder it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="turnOrder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="turnOrder"/> has fewer than <see cref="MinFrogsPerGame"/>
        /// or more than <see cref="MaxFrogsPerGame"/> frogs.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="turnOrder"/> lists the same colour twice — two
        /// frogs in the same game are never the same colour.
        /// </exception>
        public Game(IReadOnlyList<FrogColour> turnOrder, ulong seed)
        {
            if (turnOrder == null)
            {
                throw new ArgumentNullException(nameof(turnOrder));
            }

            if (turnOrder.Count < MinFrogsPerGame || turnOrder.Count > MaxFrogsPerGame)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnOrder),
                    turnOrder.Count,
                    $"a game is {MinFrogsPerGame} to {MaxFrogsPerGame} frogs; {turnOrder.Count} is neither.");
            }

            if (turnOrder.Distinct().Count() != turnOrder.Count)
            {
                throw new ArgumentException(
                    "two frogs in the same game are never the same colour.",
                    nameof(turnOrder));
            }

            _turnOrder = turnOrder.ToArray();

            var lanes = new Dictionary<FrogColour, Lane>();
            foreach (var colour in _turnOrder)
            {
                lanes[colour] = new Lane();
            }
            _lanes = lanes;

            _seed = seed;
            _rng = Rng.FromSeed(seed);
            _activeIndex = 0;
            _phase = TurnPhase.WaitingToRoll;
        }

        /// <summary>The roster, first frog to last — the order turns are taken in.</summary>
        public IReadOnlyList<FrogColour> TurnOrder
        {
            get { return _turnOrder; }
        }

        /// <summary>The frog whose turn it currently is.</summary>
        public FrogColour ActiveFrog
        {
            get { return _turnOrder[_activeIndex]; }
        }

        /// <summary>Which of the five moments the current turn is in.</summary>
        public TurnPhase Phase
        {
            get { return _phase; }
        }

        /// <summary>
        /// The seed this game is running on — exactly the seed it was
        /// constructed with, not the generator's current state. See
        /// docs/adr/0004-core-owns-the-save-format.md.
        /// </summary>
        public ulong Seed
        {
            get { return _seed; }
        }

        /// <summary>
        /// The roll the current turn drew, from <see cref="TurnPhase.RolledAndCardDrawn"/>
        /// onward. Null before the first roll of a turn.
        /// </summary>
        public Roll DrawnRoll
        {
            get { return _drawnRoll; }
        }

        /// <summary>
        /// The card the current turn drew, from <see cref="TurnPhase.RolledAndCardDrawn"/>
        /// onward — the seam #203 exists for. Null before the first roll of a
        /// turn.
        /// </summary>
        public Card DrawnCard
        {
            get { return _drawnCard; }
        }

        /// <summary>
        /// The frog that would become active next — turn order, skipping any
        /// frog that is home — without changing anything. This is the fact
        /// docs/specs/ui/answer-result.md's button label needs while the
        /// dialog is still open on the *current* player's result, before
        /// hand-off has run.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Every frog in turn order is home — there is no next frog.
        /// </exception>
        public FrogColour NextActiveFrog
        {
            get { return _turnOrder[NextActiveIndex()]; }
        }

        /// <summary>This frog's lane — where it is, and whether it is home.</summary>
        /// <exception cref="ArgumentException"><paramref name="colour"/> is not in this game's roster.</exception>
        public Lane LaneFor(FrogColour colour)
        {
            if (!_lanes.TryGetValue(colour, out var lane))
            {
                throw new ArgumentException($"{colour} is not in this game's roster.", nameof(colour));
            }

            return lane;
        }

        /// <summary>
        /// Opens the turn: draws the roll and the card it sends the turn to,
        /// from this game's own seeded generator and nothing else. Moves the
        /// phase from <see cref="TurnPhase.WaitingToRoll"/> to
        /// <see cref="TurnPhase.RolledAndCardDrawn"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The turn is not waiting to roll.</exception>
        public void RollDie()
        {
            RequirePhase(TurnPhase.WaitingToRoll);

            _drawnRoll = Roll.Draw(_rng);
            _drawnCard = Card.Draw(_drawnRoll.Pile, _rng);
            _phase = TurnPhase.RolledAndCardDrawn;
        }

        /// <summary>
        /// Moves the phase from <see cref="TurnPhase.RolledAndCardDrawn"/> to
        /// <see cref="TurnPhase.Answering"/> — the working-out grid opens.
        /// </summary>
        /// <exception cref="InvalidOperationException">The turn has not drawn a card yet.</exception>
        public void BeginAnswering()
        {
            RequirePhase(TurnPhase.RolledAndCardDrawn);
            _phase = TurnPhase.Answering;
        }

        /// <summary>
        /// Moves the phase from <see cref="TurnPhase.Answering"/> to
        /// <see cref="TurnPhase.ResultShown"/>. Deciding whether the answer
        /// was right belongs to <c>core-turn-resolution</c> (#210), not this
        /// type — this only moves the phase along.
        /// </summary>
        /// <exception cref="InvalidOperationException">The turn is not answering.</exception>
        public void ShowResult()
        {
            RequirePhase(TurnPhase.Answering);
            _phase = TurnPhase.ResultShown;
        }

        /// <summary>
        /// Moves the phase from <see cref="TurnPhase.ResultShown"/> to
        /// <see cref="TurnPhase.HandOff"/> — the result dialog closes and the
        /// board is back on screen for the frog's hop.
        /// </summary>
        /// <exception cref="InvalidOperationException">The turn's result is not shown.</exception>
        public void BeginHandOff()
        {
            RequirePhase(TurnPhase.ResultShown);
            _phase = TurnPhase.HandOff;
        }

        /// <summary>
        /// Completes the hand-off: advances the active frog to the next frog
        /// in turn order — skipping any frog that is home — and resets that
        /// frog's turn to <see cref="TurnPhase.WaitingToRoll"/>.
        /// docs/specs/ui/game-board.md#behaviour: "A frog that is home is
        /// skipped in turn order."
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The turn is not mid-hand-off, or every frog in turn order is home.
        /// </exception>
        public void CompleteHandOff()
        {
            RequirePhase(TurnPhase.HandOff);

            _activeIndex = NextActiveIndex();
            _phase = TurnPhase.WaitingToRoll;
            _drawnRoll = null;
            _drawnCard = null;
        }

        void RequirePhase(TurnPhase required)
        {
            if (_phase != required)
            {
                throw new InvalidOperationException(
                    $"this call needs the turn to be {required}, but it is {_phase}.");
            }
        }

        // Turn order minus whoever is already home, starting the search one
        // past the active frog and wrapping all the way around back to it —
        // which is what lets the lone frog left not home be its own answer.
        int NextActiveIndex()
        {
            for (var offset = 1; offset <= _turnOrder.Length; offset++)
            {
                var candidate = (_activeIndex + offset) % _turnOrder.Length;

                if (!_lanes[_turnOrder[candidate]].IsHome)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "every frog in turn order is home; there is no next frog to advance to.");
        }
    }
}
