using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// Whole games, played end to end through the real Core types from one
    /// fixed seed — issue #230, the acceptance pass for the shape-only
    /// playable proof of concept (#198).
    ///
    /// Every other Core fixture proves one piece in isolation: that a lane
    /// moves a frog correctly, that a roll maps to the right pile, that a
    /// grid takes the right shape. **None of them proves the pieces fit
    /// together into a game somebody can play from the first roll to the
    /// last.** That is the only thing this file is for, and it is why the
    /// tests here are long and sequential where the rest of the suite is
    /// short and pointed: the value is in the joints, so the joints are what
    /// is asserted, in the order a real turn passes through them.
    ///
    /// **These games are played, not staged.** Nothing here reaches for
    /// <see cref="Lane.MoveForward"/> to put a frog where a scenario wants
    /// it, and nothing hand-builds a <see cref="StandingsRow"/>. A frog gets
    /// up its lane by answering questions drawn from the game's own seeded
    /// generator, which is the whole point — a seam that is never called in
    /// a real sequence is a seam that can be broken without any isolated
    /// test noticing.
    ///
    /// One named seed, shared by all three games, so a failure here is the
    /// same failure every run — docs/adr/0004-core-owns-the-save-format.md is
    /// what put a seeded generator in Core, and this is the fixture that
    /// spends it.
    /// </summary>
    public sealed class ScriptedGameTests
    {
        /// <summary>
        /// The one seed every scripted game in this file is built from. Any
        /// fixed value would do; what matters is that it is fixed and named,
        /// so the rolls, the cards and therefore every assertion below replay
        /// identically on an ARM64 device, an x86_64 emulator and
        /// <c>dotnet test</c> — see <see cref="Rng"/>.
        /// </summary>
        const ulong ScriptedGameSeed = 20260810UL;

        /// <summary>
        /// The floor of a lane. <see cref="Lane"/> keeps its own copy private
        /// and there is no public name for it, so this is named here rather
        /// than written as a bare literal at each of the places the Start log
        /// is the expected answer.
        /// </summary>
        const int StartLogPosition = 0;

        /// <summary>
        /// A stop on the turn loop, so a scripted game that stops making
        /// progress fails as a test rather than hanging the suite. Comfortably
        /// above the turns two frogs need — eight correct answers each, plus
        /// the scripted wrong ones — and never reached by a passing run.
        /// </summary>
        const int MaxScriptedTurns = 64;

        // Reads better at the call site than a bare true/false, in a file
        // whose whole subject is which answers were right.
        const bool Right = true;
        const bool Wrong = false;

        // docs/specs/ui/working-out-grid.md: "`331 × 41` needs five digit
        // columns (`13571`); `68 × 5` needs three (`340`)" — plus the operator
        // column, in every case. A medium card, 2-digit × 2-digit, tops out at
        // `9801`: four digit columns.
        const int EasyColumnCount = 4;
        const int MediumColumnCount = 5;
        const int HardColumnCount = 6;

        // The row sequence every card is dealt, at the addition section's
        // starting size — docs/specs/ui/working-out-grid.md#how-many-columns-and-rows.
        static GridRowKind[] ExpectedRowKinds()
        {
            return new[]
            {
                GridRowKind.CarryStrip,
                GridRowKind.Multiplicand,
                GridRowKind.Multiplier,
                GridRowKind.AdditionRow,
                GridRowKind.AdditionRow,
                GridRowKind.CarryStrip,
                GridRowKind.AnswerRow
            };
        }

        // Two frogs, in the order docs/specs/ui/game-setup.md hands to Core
        // when `Start` is tapped: "`Start` begins the game with the chosen
        // frogs in badge order."
        static FrogColour[] ScriptedRoster()
        {
            return new[] { FrogColour.Green, FrogColour.Blue };
        }

        /// <summary>
        /// The whole thing: two frogs, one seed, played turn by turn until
        /// both are home and the game ends itself — with the winner being the
        /// frog that got home *first*, not the one whose finish ended the
        /// game (docs/specs/ui/game-over.md#invariants).
        /// </summary>
        [Test]
        public void AWholeGame_PlayedFromTheFixedSeedUntilEveryFrogIsHome_EndsItself_WithTheFirstFinisherAsTheWinner()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);

            // --- The roster, and the turn order that follows from it. -------
            // docs/specs/ui/game-board.md#behaviour: "Entering from game
            // setup: every frog on its Start log, frog 1 active, `Roll`
            // enabled."
            Assert.That(game.TurnOrder, Is.EqualTo(new[] { FrogColour.Green, FrogColour.Blue }));
            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
            Assert.That(game.NextActiveFrog, Is.EqualTo(FrogColour.Blue));
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
            Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(StartLogPosition));
            Assert.That(game.LaneFor(FrogColour.Blue).Position, Is.EqualTo(StartLogPosition));
            Assert.That(game.IsOver, Is.False);
            Assert.That(game.FrogsStillSwimming, Is.EqualTo(2));

            // --- Turn 1: Green, on the Start log, answers wrong. ------------
            // The floor. docs/specs/reference/index.md — the Start log is a
            // floor, not a special space: a wrong answer there leaves the frog
            // where it is rather than pushing it off the bottom of the lane.
            var greenOnTheFloor = PlayOneTurn(game, Wrong);

            Assert.That(greenOnTheFloor.Frog, Is.EqualTo(FrogColour.Green));
            Assert.That(greenOnTheFloor.Resolution.Outcome, Is.EqualTo(TurnOutcome.WrongOnStartLog));
            Assert.That(greenOnTheFloor.Resolution.PositionBefore, Is.EqualTo(StartLogPosition));
            Assert.That(greenOnTheFloor.Resolution.PositionAfter, Is.EqualTo(StartLogPosition));
            Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(StartLogPosition));

            // Hand-off ran: the device is the next player's.
            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));

            // --- Turn 2: Blue answers right and hops forward. ---------------
            var blueHopsForward = PlayOneTurn(game, Right);

            Assert.That(blueHopsForward.Frog, Is.EqualTo(FrogColour.Blue));
            Assert.That(blueHopsForward.Resolution.Outcome, Is.EqualTo(TurnOutcome.Correct));
            Assert.That(blueHopsForward.Resolution.PositionBefore, Is.EqualTo(StartLogPosition));
            Assert.That(blueHopsForward.Resolution.PositionAfter, Is.EqualTo(StartLogPosition + 1));
            Assert.That(game.LaneFor(FrogColour.Blue).Position, Is.EqualTo(StartLogPosition + 1));

            // --- Turn 3: Green answers right, off the Start log. ------------
            PlayOneTurn(game, Right);
            Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(StartLogPosition + 1));

            // --- Turn 4: Blue, above the Start log, answers wrong. ----------
            // The other wrong outcome: this one really does move the frog.
            var blueHopsBack = PlayOneTurn(game, Wrong);

            Assert.That(blueHopsBack.Frog, Is.EqualTo(FrogColour.Blue));
            Assert.That(blueHopsBack.Resolution.Outcome, Is.EqualTo(TurnOutcome.WrongAboveStartLog));
            Assert.That(blueHopsBack.Resolution.PositionAfter, Is.EqualTo(blueHopsBack.Resolution.PositionBefore - 1));
            Assert.That(game.LaneFor(FrogColour.Blue).Position, Is.EqualTo(StartLogPosition));

            // --- Everything after: both frogs answer right, to the end. -----
            var turnsAfterGreenGotHome = new List<FrogColour>();

            while (!game.IsOver)
            {
                Assert.That(
                    turnsAfterGreenGotHome.Count, Is.LessThan(MaxScriptedTurns),
                    "the scripted game stopped making progress");

                var greenWasAlreadyHome = game.LaneFor(FrogColour.Green).IsHome;
                var turn = PlayOneTurn(game, Right);

                if (greenWasAlreadyHome)
                {
                    turnsAfterGreenGotHome.Add(turn.Frog);
                }
            }

            // --- Play continued after the first frog got home. --------------
            // docs/specs/ui/game-board.md#behaviour: "A frog that reaches the
            // End log stays there... Play continues — the other frogs keep
            // taking turns," and "a frog that is home is skipped in turn
            // order." Both are asserted by the same list: it is not empty, and
            // Green is not in it.
            Assert.That(turnsAfterGreenGotHome, Is.Not.Empty, "the other frog must keep taking turns");
            Assert.That(
                turnsAfterGreenGotHome, Is.All.EqualTo(FrogColour.Blue),
                "a frog that is home is skipped in turn order");

            // --- The game ended itself, and named the right winner. ---------
            // Blue's finish is what ended the game; Green's is what won it.
            Assert.That(game.IsOver, Is.True);
            Assert.That(game.LaneFor(FrogColour.Green).IsHome, Is.True);
            Assert.That(game.LaneFor(FrogColour.Blue).IsHome, Is.True);
            Assert.That(game.FrogsStillSwimming, Is.EqualTo(0));
            Assert.That(
                game.FinishingOrder, Is.EqualTo(new[] { FrogColour.Green, FrogColour.Blue }),
                "finishing order is the order frogs got home");
            Assert.That(
                game.Winner, Is.EqualTo(FrogColour.Green),
                "the winner is the frog that reached the End log first, not the one whose finish ended the game");

            // --- The standings that come out of it. -------------------------
            var standings = game.Standings;

            Assert.That(standings.Count, Is.EqualTo(2));
            Assert.That(standings[0].Colour, Is.EqualTo(FrogColour.Green));
            Assert.That(standings[0].Place, Is.EqualTo(1));
            Assert.That(standings[0].Position, Is.EqualTo(Lane.LaneWinningPosition));
            Assert.That(standings[0].IsHome, Is.True);
            Assert.That(standings[1].Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(standings[1].Place, Is.EqualTo(2));
            Assert.That(standings[1].IsHome, Is.True);
        }

        /// <summary>
        /// The `Game over` route: ended deliberately before anybody got home.
        /// docs/specs/ui/game-over.md#behaviour's route table — "`<c>Colour</c>
        /// frog wins!` if anyone got home, otherwise `Game over`" — and
        /// docs/specs/ui/end-game-confirm.md, which "stops the game and shows
        /// the results" rather than resetting anyone.
        /// </summary>
        [Test]
        public void AGameEndedDeliberatelyBeforeAnyFrogIsHome_IsOverAtOnce_KeepsEveryFrogsLilyPads_AndHasNoWinner()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);

            // Four real turns, both frogs answering right, so each frog has
            // pads it would mind losing.
            PlayOneTurn(game, Right);
            PlayOneTurn(game, Right);
            PlayOneTurn(game, Right);
            PlayOneTurn(game, Right);

            var padsBefore = ScriptedRoster().ToDictionary(
                colour => colour, colour => game.LaneFor(colour).Position);

            Assert.That(game.IsOver, Is.False, "nobody is home, so nothing has ended the game yet");
            Assert.That(padsBefore.Values, Is.All.GreaterThan(StartLogPosition));

            game.EndGame();

            Assert.That(game.IsOver, Is.True);
            Assert.That(game.Winner, Is.Null, "announcing a winner who did not win is worse than announcing nobody");
            Assert.That(game.FinishingOrder, Is.Empty);

            foreach (var colour in ScriptedRoster())
            {
                Assert.That(
                    game.LaneFor(colour).Position, Is.EqualTo(padsBefore[colour]),
                    $"{colour} lost lily pads to a deliberate ending");
                Assert.That(game.LaneFor(colour).IsHome, Is.False);
            }

            var standings = game.Standings;

            Assert.That(standings.Count, Is.EqualTo(2));
            Assert.That(standings.Select(row => row.IsHome), Is.All.False);
            Assert.That(
                standings.Select(row => row.Position),
                Is.Ordered.Descending,
                "frogs that did not finish are ranked by how many lily pads they made");
        }

        /// <summary>
        /// The other half of that route table, and the branch most likely to
        /// be got wrong: ended deliberately *after* one frog is home. The
        /// headline still names a winner, because one frog really did reach
        /// the End log first.
        /// </summary>
        [Test]
        public void AGameEndedDeliberatelyAfterOneFrogIsHome_NamesThatFrogAsTheWinner()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);

            // Green answers every question right; Blue answers every question
            // wrong, so it never leaves the Start log and the game cannot end
            // itself. Green gets home on its own steam, one correct answer per
            // turn, exactly as a player would.
            while (!game.LaneFor(FrogColour.Green).IsHome)
            {
                Assert.That(game.IsOver, Is.False, "the game must not end itself with Blue still swimming");

                PlayOneTurn(game, game.ActiveFrog == FrogColour.Green ? Right : Wrong);
            }

            Assert.That(
                game.IsOver, Is.False,
                "one frog home is not the end of a game — the others keep taking turns");
            Assert.That(game.Winner, Is.EqualTo(FrogColour.Green), "Green is home, so Green got home first");
            Assert.That(game.LaneFor(FrogColour.Blue).Position, Is.EqualTo(StartLogPosition));
            Assert.That(game.FrogsStillSwimming, Is.EqualTo(1));

            game.EndGame();

            Assert.That(game.IsOver, Is.True);
            Assert.That(game.Winner, Is.EqualTo(FrogColour.Green));
            Assert.That(game.FinishingOrder, Is.EqualTo(new[] { FrogColour.Green }));

            var standings = game.Standings;

            Assert.That(standings[0].Colour, Is.EqualTo(FrogColour.Green));
            Assert.That(standings[0].Place, Is.EqualTo(1));
            Assert.That(standings[0].IsHome, Is.True);
            Assert.That(standings[1].Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(standings[1].Place, Is.EqualTo(2));
            Assert.That(standings[1].Position, Is.EqualTo(StartLogPosition));
            Assert.That(standings[1].IsHome, Is.False);
        }

        /// <summary>
        /// What the fixed seed buys: the same script played twice deals the
        /// same rolls and the same cards, in the same order, and ends the same
        /// way. Without this, a failure above would be a different failure
        /// every run and worth nothing as evidence.
        /// </summary>
        [Test]
        public void TheSameScriptPlayedTwiceFromTheSameSeed_DealsTheSameRollsAndCards_AndEndsTheSameWay()
        {
            var first = PlayEveryFrogHome();
            var second = PlayEveryFrogHome();

            Assert.That(second.Log.Count, Is.EqualTo(first.Log.Count), "the same script took a different number of turns");

            for (var turn = 0; turn < first.Log.Count; turn++)
            {
                Assert.That(second.Log[turn].Frog, Is.EqualTo(first.Log[turn].Frog), $"turn {turn}: frog");
                Assert.That(second.Log[turn].Face, Is.EqualTo(first.Log[turn].Face), $"turn {turn}: die face");
                Assert.That(second.Log[turn].Pile, Is.EqualTo(first.Log[turn].Pile), $"turn {turn}: pile");
                Assert.That(
                    second.Log[turn].Card.Multiplicand, Is.EqualTo(first.Log[turn].Card.Multiplicand),
                    $"turn {turn}: multiplicand");
                Assert.That(
                    second.Log[turn].Card.Multiplier, Is.EqualTo(first.Log[turn].Card.Multiplier),
                    $"turn {turn}: multiplier");
            }

            Assert.That(second.Game.Winner, Is.EqualTo(first.Game.Winner));
            Assert.That(second.Game.FinishingOrder, Is.EqualTo(first.Game.FinishingOrder));
        }

        // --- The driver -----------------------------------------------------

        // One turn's worth of what the screens saw, kept so the test that
        // played it can assert against it afterwards.
        sealed class PlayedTurn
        {
            public PlayedTurn(FrogColour frog, int face, Pile pile, Card card, TurnResolution resolution)
            {
                Frog = frog;
                Face = face;
                Pile = pile;
                Card = card;
                Resolution = resolution;
            }

            public FrogColour Frog { get; }

            public int Face { get; }

            public Pile Pile { get; }

            public Card Card { get; }

            public TurnResolution Resolution { get; }
        }

        sealed class PlayedGame
        {
            public PlayedGame(Game game, IReadOnlyList<PlayedTurn> log)
            {
                Game = game;
                Log = log;
            }

            public Game Game { get; }

            public IReadOnlyList<PlayedTurn> Log { get; }
        }

        /// <summary>
        /// Plays exactly one turn, through every phase, in the order
        /// docs/specs/ui/game-board.md#behaviour lists them: "`Roll` → roll
        /// and card → working-out grid → answer result → back here with the
        /// frog moved and the turn passed to the next frog in order."
        ///
        /// This is the only place in the file a frog moves, and it moves the
        /// way the shell moves one — <see cref="Lane.Resolve"/> against the
        /// card the game itself drew. The joints that are the same on every
        /// turn (the roll picking the pile, the card matching the pile's
        /// shape, the grid matching the card) are asserted here rather than
        /// re-asserted by each test, so they are checked on every turn of
        /// every scripted game rather than on the one turn a test remembered
        /// to look at.
        /// </summary>
        static PlayedTurn PlayOneTurn(Game game, bool answerCorrectly)
        {
            var frog = game.ActiveFrog;

            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
            Assert.That(game.LaneFor(frog).IsHome, Is.False, "a frog that is home is never asked to take a turn");

            // `Roll` — the roll and the card it sends the turn to.
            game.RollDie();

            Assert.That(game.Phase, Is.EqualTo(TurnPhase.RolledAndCardDrawn));

            var roll = game.DrawnRoll;
            var card = game.DrawnCard;

            AssertTheRollSelectedThePile(roll);
            AssertTheCardMatchesItsPilesShape(card, roll.Pile);

            // The working-out grid, derived from that card and nothing else.
            AssertTheGridMatchesTheCard(WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart), card, roll.Pile);

            game.BeginAnswering();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.Answering));

            // `Check it`: the answer row's digits leave the grid and are
            // graded against the card. A wrong answer is the true product
            // missed by one — wrong is wrong, and how wrong is not a fact the
            // game has any use for.
            var submitted = answerCorrectly ? card.Product : card.Product + 1;
            var resolution = game.LaneFor(frog).Resolve(submitted, card);

            Assert.That(resolution.CorrectAnswer, Is.EqualTo(card.Product));
            Assert.That(game.LaneFor(frog).Position, Is.EqualTo(resolution.PositionAfter));

            // The answer-result dialog, and the hand-off its one button runs.
            game.ShowResult();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.ResultShown));

            game.BeginHandOff();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.HandOff));

            // docs/specs/ui/game-board.md#behaviour: "When the last frog gets
            // home, the game ends itself... A finished game never sits on this
            // screen waiting to be dismissed." So there is no next player to
            // hand off to, and asking for one is the mistake this branch
            // avoids.
            if (!game.IsOver)
            {
                game.CompleteHandOff();
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
                Assert.That(game.LaneFor(game.ActiveFrog).IsHome, Is.False);
            }

            return new PlayedTurn(frog, roll.Face, roll.Pile, card, resolution);
        }

        // Both frogs answer everything right, until the game ends itself.
        static PlayedGame PlayEveryFrogHome()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);
            var log = new List<PlayedTurn>();

            while (!game.IsOver)
            {
                Assert.That(log.Count, Is.LessThan(MaxScriptedTurns), "the scripted game stopped making progress");
                log.Add(PlayOneTurn(game, Right));
            }

            return new PlayedGame(game, log);
        }

        // docs/specs/rules.md — "the roll selects the pile and does nothing
        // else", and the mapping is fixed: 1 or 2 easy, 3 or 4 medium, 5 or 6
        // hard.
        static void AssertTheRollSelectedThePile(Roll roll)
        {
            Assert.That(roll.Face, Is.InRange(Roll.MinimumFace, Roll.MaximumFace));
            Assert.That(roll.Pile, Is.EqualTo(Roll.PileForFace(roll.Face)), $"a face of {roll.Face} chose the wrong pile");
        }

        // docs/adr/0002-structured-working-out-grid.md pins three shapes and
        // nothing else about a card: easy is 2-digit × 1-digit, medium
        // 2-digit × 2-digit, hard 3-digit × 2-digit.
        static void AssertTheCardMatchesItsPilesShape(Card card, Pile pile)
        {
            switch (pile)
            {
                case Pile.Easy:
                    AssertOperandIsInRange(card.Multiplicand, Card.TwoDigitMinimum, Card.TwoDigitMaximum, pile);
                    AssertOperandIsInRange(card.Multiplier, Card.OneDigitMinimum, Card.OneDigitMaximum, pile);
                    break;

                case Pile.Medium:
                    AssertOperandIsInRange(card.Multiplicand, Card.TwoDigitMinimum, Card.TwoDigitMaximum, pile);
                    AssertOperandIsInRange(card.Multiplier, Card.TwoDigitMinimum, Card.TwoDigitMaximum, pile);
                    break;

                default:
                    AssertOperandIsInRange(card.Multiplicand, Card.ThreeDigitMinimum, Card.ThreeDigitMaximum, pile);
                    AssertOperandIsInRange(card.Multiplier, Card.TwoDigitMinimum, Card.TwoDigitMaximum, pile);
                    break;
            }
        }

        static void AssertOperandIsInRange(int operand, int minimum, int maximum, Pile pile)
        {
            Assert.That(operand, Is.InRange(minimum, maximum), $"an operand of {operand} is the wrong shape for the {pile} pile");
        }

        // docs/specs/ui/working-out-grid.md — the grid's columns are the
        // largest possible product for the card's *shape* plus an operator
        // column, and every card is dealt the same row kinds.
        static void AssertTheGridMatchesTheCard(WorkingOutGrid grid, Card card, Pile pile)
        {
            var expectedColumnCount = pile == Pile.Easy
                ? EasyColumnCount
                : pile == Pile.Medium ? MediumColumnCount : HardColumnCount;

            Assert.That(grid.ColumnCount, Is.EqualTo(expectedColumnCount), $"the {pile} pile's grid is the wrong width");
            Assert.That(grid.Rows.Select(row => row.Kind), Is.EqualTo(ExpectedRowKinds()));
            Assert.That(grid.Rows.Select(row => row.Cells.Count), Is.All.EqualTo(grid.ColumnCount));

            // Exactly one graded row, and it is the last one and it is empty.
            // ADR-0002: nothing in the grid is marked, and the answer row is
            // the only row the player is graded on.
            Assert.That(grid.Rows.Count(row => row.Kind == GridRowKind.AnswerRow), Is.EqualTo(1));
            Assert.That(grid.Rows[grid.Rows.Count - 1].Kind, Is.EqualTo(GridRowKind.AnswerRow));

            // The card's own digits are the printed ones, right-aligned.
            AssertPrintedRowReads(grid, GridRowKind.Multiplicand, card.Multiplicand);
            AssertPrintedRowReads(grid, GridRowKind.Multiplier, card.Multiplier);
        }

        static void AssertPrintedRowReads(WorkingOutGrid grid, GridRowKind kind, int operand)
        {
            var printed = grid.Rows
                .Single(row => row.Kind == kind).Cells
                .Where(cell => cell.Kind == GridCellKind.Printed)
                .Select(cell => cell.Digit.ToString());

            Assert.That(string.Concat(printed), Is.EqualTo(operand.ToString()), $"the {kind} row does not read {operand}");
        }
    }
}
