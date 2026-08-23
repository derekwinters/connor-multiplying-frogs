using System;
using System.Collections.Generic;
using System.Linq;

namespace Frogs.Core
{
    /// <summary>
    /// A whole game: the roster and its turn order, a <see cref="Lane"/> per
    /// frog, which of the five <see cref="TurnPhase"/> moments the current
    /// turn is in, and whether the game is over. A screen asks this type
    /// "whose turn is it, and what should I be showing right now?" and,
    /// eventually, "is it over, and who won?" — nothing else holds those
    /// answers.
    ///
    /// A <see cref="Game"/> owns advancing to the next player and the rule
    /// that a turn's phase only ever moves forward one step at a time. It
    /// does **not** own whether an answer is right or how far a frog moves as
    /// a result — that is grading, <c>core-turn-resolution</c> (#210). It
    /// does own the two ways a game ends (<see cref="IsOver"/>) and the
    /// standings that come out of one (<see cref="Winner"/>,
    /// <see cref="FinishingOrder"/>, <see cref="Standings"/>) —
    /// <c>core-game-end</c> (#211). "Every frog home" is computed fresh from
    /// lane position on every call; a deliberate end and finishing order are
    /// the two facts nothing about lane position can reconstruct, so they are
    /// the ones this type remembers, via <see cref="EndGame"/> and
    /// <see cref="RecordFinish"/>. <see cref="FrogJustHome"/> is the third:
    /// which frog the turn that just played got home, if any — the question
    /// docs/specs/ui/player-won.md's dialog asks, and not the same question as
    /// <see cref="IsOver"/>.
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

        readonly RosterEntry[] _roster;
        readonly FrogColour[] _turnOrder;
        readonly IReadOnlyDictionary<FrogColour, Lane> _lanes;
        readonly Rng _rng;
        readonly ulong _seed;

        int _activeIndex;
        TurnPhase _phase;
        Roll _drawnRoll;
        Card _drawnCard;
        bool _endedDeliberately;
        FrogColour? _frogJustHome;
        readonly List<FrogColour> _finishingOrder = new List<FrogColour>();

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
            : this(AsRoster(turnOrder), seed)
        {
        }

        /// <summary>
        /// A game for <paramref name="roster"/> — already-ordered, first frog
        /// to last — running on <paramref name="seed"/>. This is the
        /// constructor game setup uses once names have been typed; the
        /// colour-only one above is the same thing with every frog on its
        /// default name.
        ///
        /// docs/specs/ui/game-setup.md#behaviour: "Their names go with them,
        /// and are what every later screen shows." Two entries may carry the
        /// same name — "nothing prevents it, nothing numbers them, nothing
        /// warns" — so only colours are checked for duplicates here.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="roster"/> is null, or holds a null entry.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="roster"/> has fewer than <see cref="MinFrogsPerGame"/>
        /// or more than <see cref="MaxFrogsPerGame"/> frogs.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="roster"/> lists the same colour twice — two frogs
        /// in the same game are never the same colour.
        /// </exception>
        public Game(IReadOnlyList<RosterEntry> roster, ulong seed)
        {
            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            if (roster.Count < MinFrogsPerGame || roster.Count > MaxFrogsPerGame)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roster),
                    roster.Count,
                    $"a game is {MinFrogsPerGame} to {MaxFrogsPerGame} frogs; {roster.Count} is neither.");
            }

            if (roster.Any(entry => entry == null))
            {
                throw new ArgumentNullException(nameof(roster), "a roster holds no null entries.");
            }

            if (roster.Select(entry => entry.Colour).Distinct().Count() != roster.Count)
            {
                throw new ArgumentException(
                    "two frogs in the same game are never the same colour.",
                    nameof(roster));
            }

            _roster = roster.ToArray();
            _turnOrder = _roster.Select(entry => entry.Colour).ToArray();

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

        // A colour-only line-up is a roster of frogs on their default names —
        // docs/specs/ui/game-setup.md#behaviour: "A default name is a real
        // name, not a placeholder." So there is one roster path, not two.
        static RosterEntry[] AsRoster(IReadOnlyList<FrogColour> turnOrder)
        {
            if (turnOrder == null)
            {
                throw new ArgumentNullException(nameof(turnOrder));
            }

            return turnOrder.Select(colour => new RosterEntry(colour)).ToArray();
        }

        /// <summary>The roster, first frog to last — the order turns are taken in.</summary>
        public IReadOnlyList<FrogColour> TurnOrder
        {
            get { return _turnOrder; }
        }

        /// <summary>
        /// The roster with its names, first frog to last. What
        /// <c>Play again</c> starts the next game from, so that names last as
        /// long as the frogs do — docs/specs/ui/game-over.md.
        /// </summary>
        public IReadOnlyList<RosterEntry> Roster
        {
            get { return _roster; }
        }

        /// <summary>
        /// What this frog's player is called — its colour name until somebody
        /// changed it on game setup. Every screen that draws a frog reads it
        /// from here, so nothing has to look a name up anywhere else.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="colour"/> is not in this game's roster.</exception>
        public string NameFor(FrogColour colour)
        {
            foreach (var entry in _roster)
            {
                if (entry.Colour == colour)
                {
                    return entry.Name;
                }
            }

            throw new ArgumentException($"{colour} is not in this game's roster.", nameof(colour));
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

        /// <summary>
        /// Whether the game is over — the OR of the two ways it can be:
        /// every frog is home, or it was ended deliberately.
        /// docs/specs/reference/index.md#where-v1-fills-a-gap-the-board-leaves-open
        /// names both. The first fact is computed fresh from every frog's
        /// lane position each time this is asked — nothing is cached, and
        /// nothing needs to be told to "notice" a frog landing on the End
        /// log. The second cannot be computed the same way — a deliberate end
        /// can happen with frogs still short of home, so it is the one fact
        /// this type has to remember, set by <see cref="EndGame"/>.
        /// </summary>
        public bool IsOver
        {
            get { return _endedDeliberately || _turnOrder.All(colour => _lanes[colour].IsHome); }
        }

        /// <summary>
        /// Ends the game deliberately, right now, regardless of whether any
        /// frog is home. docs/specs/reference/index.md#where-v1-fills-a-gap-the-board-leaves-open:
        /// "A game can be ended deliberately" — purely a v1 gap-fill, not a
        /// rule the classroom board specifies. Every frog's <see cref="Lane"/>
        /// is left exactly as it was: ending a game is not losing it — see
        /// docs/specs/ui/end-game-confirm.md ("stops the game and shows the
        /// results" rather than resetting anyone).
        /// </summary>
        public void EndGame()
        {
            _endedDeliberately = true;
        }

        /// <summary>
        /// The frog that reached the End log first, or null if the game was
        /// ended before anyone finished — a well-defined Core answer to "did
        /// anyone finish, and if so who was first", not a display string.
        /// docs/specs/ui/game-over.md#invariants: "the winner is the frog
        /// that reached the End log first."
        /// </summary>
        public FrogColour? Winner
        {
            get { return _finishingOrder.Count > 0 ? _finishingOrder[0] : (FrogColour?)null; }
        }

        /// <summary>
        /// The frog the most recent <see cref="ShowResult"/> landed on its
        /// End log, or null if that turn landed nobody home.
        ///
        /// This is the question docs/specs/ui/player-won.md asks — "did the
        /// frog that just moved land on its End log" — and it is deliberately
        /// **not** <see cref="IsOver"/>: every arrival but the last happens in
        /// a game that is still running, and the last arrival is the only one
        /// the two questions agree about.
        ///
        /// It is one turn's fact, replaced by the next turn's result rather
        /// than added to, which is what makes one arrival announceable exactly
        /// once. A frog already in <see cref="FinishingOrder"/> can never set
        /// it again, because <see cref="RecordFinish"/> records nobody twice.
        /// It survives <see cref="CompleteHandOff"/> on purpose: the dialog
        /// that reads it opens after the hop, once the next player's turn has
        /// already begun.
        /// </summary>
        public FrogColour? FrogJustHome
        {
            get { return _frogJustHome; }
        }

        /// <summary>
        /// The order frogs got home, first to last. Not roster order, and
        /// not recomputed from position — every finisher sits on the same
        /// <see cref="Lane.LaneWinningPosition"/>, so position cannot tell
        /// them apart; this is only ever the order <see cref="RecordFinish"/>
        /// was told about arrivals. docs/specs/ui/game-over.md#invariants:
        /// "finishing order is the order frogs got home."
        /// </summary>
        public IReadOnlyList<FrogColour> FinishingOrder
        {
            get { return _finishingOrder; }
        }

        /// <summary>
        /// Records that <paramref name="colour"/>'s frog has reached the End
        /// log, if it has — the moment its place in <see cref="FinishingOrder"/>
        /// has to be captured, since nothing about a frog's position can tell
        /// arrival order apart afterward. A no-op if the frog is not home, and
        /// a no-op if it was already recorded — safe to call more than once.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="colour"/> is not in this game's roster.</exception>
        public void RecordFinish(FrogColour colour)
        {
            var lane = LaneFor(colour);

            if (lane.IsHome && !_finishingOrder.Contains(colour))
            {
                _finishingOrder.Add(colour);
            }
        }

        /// <summary>
        /// One row per frog: place, colour, lane position, and whether it is
        /// home — the fact docs/specs/ui/game-over.md's standings screen
        /// reads directly. Finishers come first, in <see cref="FinishingOrder"/>;
        /// everyone else follows, most lily pads made first. Frogs tied on
        /// lane position share the same place number —
        /// docs/specs/ui/game-over.md#open-questions: "Two frogs on the same
        /// pad currently share a place number." No turn-order tiebreak: that
        /// question is still open, and this does not answer it.
        /// </summary>
        public IReadOnlyList<StandingsRow> Standings
        {
            get
            {
                var unfinished = _turnOrder
                    .Where(colour => !_finishingOrder.Contains(colour))
                    .OrderByDescending(colour => _lanes[colour].Position);

                var ranked = _finishingOrder.Concat(unfinished).ToArray();
                var rows = new List<StandingsRow>(ranked.Length);

                for (var index = 0; index < ranked.Length; index++)
                {
                    var colour = ranked[index];
                    var lane = _lanes[colour];

                    var tiedWithPrevious = index > 0
                        && !_finishingOrder.Contains(colour)
                        && !_finishingOrder.Contains(ranked[index - 1])
                        && lane.Position == _lanes[ranked[index - 1]].Position;

                    var place = tiedWithPrevious ? rows[index - 1].Place : index + 1;

                    rows.Add(new StandingsRow(colour, NameFor(colour), place, lane.Position, lane.IsHome));
                }

                return rows;
            }
        }

        /// <summary>
        /// How many frogs have not yet reached their End log — readable at
        /// any point during play, not only once the game is over. This is
        /// the count docs/specs/ui/end-game-confirm.md's cost sentence
        /// builds its wording around ("Three frogs are still swimming...").
        /// </summary>
        public int FrogsStillSwimming
        {
            get { return _turnOrder.Count(colour => !_lanes[colour].IsHome); }
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
        /// <see cref="TurnPhase.ResultShown"/>, and — if that answer landed
        /// the active frog on its End log — captures its place in
        /// <see cref="FinishingOrder"/>.
        ///
        /// Deciding whether the answer was right still belongs to
        /// <c>core-turn-resolution</c> (#210), not this type: this never
        /// grades anything and never moves a frog. It only notices that a
        /// frog is home, which is a fact only this type can keep, because
        /// every finisher ends up on the same
        /// <see cref="Lane.LaneWinningPosition"/> and position cannot tell
        /// arrival order apart afterward.
        ///
        /// **This is the moment, and it is Core's rather than a caller's, on
        /// purpose.** <see cref="Lane.Resolve"/> is the only thing that moves
        /// a frog and it can only be run while the turn is
        /// <see cref="TurnPhase.Answering"/>; this call is the very next one
        /// the phase machine will accept. So it is the first moment after a
        /// move that a caller cannot skip, which makes the recording
        /// impossible to forget — the way it was forgotten while it lived on
        /// the shell's to-do list. Recording later, at hand-off, would miss
        /// the last finisher of all: when the final frog gets home the game
        /// ends itself and <see cref="CompleteHandOff"/> is never called
        /// (docs/specs/ui/game-board.md#behaviour).
        /// </summary>
        /// <exception cref="InvalidOperationException">The turn is not answering.</exception>
        public void ShowResult()
        {
            RequirePhase(TurnPhase.Answering);
            _phase = TurnPhase.ResultShown;

            // A no-op unless this turn's answer actually put the active frog
            // on the End log — see RecordFinish. Whether it was a no-op is
            // itself the answer to "did the frog that just moved land on its
            // End log", so FrogJustHome is read off the list growing rather
            // than from a second look at the lane: a frog that was already
            // home before this turn cannot make it grow, and so cannot be
            // announced a second time.
            var finishersBefore = _finishingOrder.Count;
            RecordFinish(ActiveFrog);
            _frogJustHome = _finishingOrder.Count > finishersBefore ? ActiveFrog : (FrogColour?)null;
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
