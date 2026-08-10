using System;
using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Game"/> owns the roster, turn order, and the five phases of
    /// a turn. It does not decide whether an answer is right (#210) or how a
    /// game ends (#211) — every test here stays on this side of both lines.
    /// </summary>
    public sealed class GameTests
    {
        const ulong Seed = 12345UL;

        [Test]
        public void ConstructedFromTwoFrogs_SetsTurnOrderToTheRosterOrder_WithTheFirstFrogActive()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };

            var game = new Game(roster, Seed);

            Assert.That(game.TurnOrder, Is.EqualTo(roster));
            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
        }

        [Test]
        public void ConstructedFromFourFrogs_TheMaximum_MatchesTheRosterOrderForAllFour()
        {
            var roster = new[] { FrogColour.Pink, FrogColour.Orange, FrogColour.Blue, FrogColour.Green };

            var game = new Game(roster, Seed);

            Assert.That(game.TurnOrder, Is.EqualTo(roster));
            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Pink));
        }

        [Test]
        public void ConstructedFromOneFrog_BelowTheMinimum_IsRejected()
        {
            var roster = new[] { FrogColour.Green };

            Assert.That(
                () => new Game(roster, Seed),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructedFromFiveFrogs_AboveTheMaximum_IsRejected()
        {
            var roster = new[]
            {
                FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink, FrogColour.Green
            };

            Assert.That(
                () => new Game(roster, Seed),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructedWithTheSameColourTwice_IsRejected()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Green };

            Assert.That(
                () => new Game(roster, Seed),
                Throws.TypeOf<ArgumentException>());
        }

        // docs/specs/ui/game-board.md#behaviour: "Entering from game setup:
        // every frog on its Start log, frog 1 active, `Roll` enabled."
        [Test]
        public void ANewGame_StartsWaitingToRoll()
        {
            var game = new Game(FourFrogRoster(), Seed);

            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
        }

        [Test]
        public void ThePhase_AdvancesOneStepAtATime_AndRejectsSkippingAStep()
        {
            var game = new Game(FourFrogRoster(), Seed);

            // Skipping straight from waiting-to-roll to answering — nothing
            // has been rolled or drawn yet.
            Assert.That(() => game.BeginAnswering(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));

            game.RollDie();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.RolledAndCardDrawn));

            // Skipping straight from rolled-and-card-drawn to result-shown.
            Assert.That(() => game.ShowResult(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.RolledAndCardDrawn));

            game.BeginAnswering();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.Answering));

            game.ShowResult();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.ResultShown));

            game.BeginHandOff();
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.HandOff));
        }

        [Test]
        public void TheRolledAndCardDrawnPhase_CarriesTheCardTheTurnDrew()
        {
            var game = new Game(FourFrogRoster(), Seed);

            game.RollDie();

            Assert.That(game.DrawnCard, Is.Not.Null);
            Assert.That(game.DrawnCard.Multiplicand, Is.GreaterThan(0));
        }

        [Test]
        public void CompletingHandOff_AdvancesTheActiveFrog_AndResetsItsPhaseToWaitingToRoll()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);

            PlayThroughOneTurn(game);

            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
        }

        [Test]
        public void CompletingHandOff_ForTheLastFrogInTurnOrder_WrapsBackAroundToTheFirst()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);

            PlayThroughOneTurn(game); // Green -> Blue
            PlayThroughOneTurn(game); // Blue -> Orange
            PlayThroughOneTurn(game); // Orange -> back to Green

            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
        }

        // docs/specs/ui/game-board.md#behaviour: "A frog that is home is
        // skipped in turn order." Driven by advancing the Lane directly, not
        // by answering a question — grading and moving a frog is #210's job.
        [Test]
        public void CompletingHandOff_SkipsAFrogThatIsHome()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Blue);

            PlayThroughOneTurn(game); // Green -> Blue is home, so -> Orange

            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Orange));
        }

        [Test]
        public void IfEveryFrogButOneIsHome_AdvancingAlwaysReturnsToThatOneFrog()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Blue);
            SendHome(game, FrogColour.Orange);
            SendHome(game, FrogColour.Pink);

            PlayThroughOneTurn(game); // Green -> Blue, Orange, Pink all home -> back to Green

            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
        }

        [Test]
        [Timeout(5000)]
        public void AdvancingWhenEveryFrogIsHome_ThrowsInsteadOfHangingForever()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Green);
            SendHome(game, FrogColour.Blue);

            game.RollDie();
            game.BeginAnswering();
            game.ShowResult();
            game.BeginHandOff();

            Assert.That(() => game.CompleteHandOff(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void NextActiveFrog_IsTheNextFrogInTurnOrderThatIsNotHome_WithoutAdvancingAnything()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);

            Assert.That(game.NextActiveFrog, Is.EqualTo(FrogColour.Blue));
            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
        }

        [Test]
        public void NextActiveFrog_SkipsOverConsecutiveHomeFrogs()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Blue);
            SendHome(game, FrogColour.Orange);

            Assert.That(game.NextActiveFrog, Is.EqualTo(FrogColour.Pink));
        }

        [Test]
        public void NextActiveFrog_WhenOnlyTheActiveFrogIsNotHome_WalksAllTheWayAroundBackToIt()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Blue);
            SendHome(game, FrogColour.Orange);

            Assert.That(game.NextActiveFrog, Is.EqualTo(FrogColour.Green));
        }

        [Test]
        public void NextActiveFrog_NeverMutatesTheGame_EvenCalledSeveralTimesInARow()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Blue);
            game.RollDie();

            var activeBefore = game.ActiveFrog;
            var phaseBefore = game.Phase;
            var positionsBefore = roster.Select(colour => game.LaneFor(colour).Position).ToArray();

            _ = game.NextActiveFrog;
            _ = game.NextActiveFrog;
            _ = game.NextActiveFrog;

            Assert.That(game.ActiveFrog, Is.EqualTo(activeBefore));
            Assert.That(game.Phase, Is.EqualTo(phaseBefore));
            Assert.That(
                roster.Select(colour => game.LaneFor(colour).Position).ToArray(),
                Is.EqualTo(positionsBefore));
        }

        // Frogs are independent (docs/specs/rules.md), and so are games: a
        // save, a scripted test, and a live game must never be able to leak
        // state into one another through shared mutable objects.
        [Test]
        public void TwoIndependentGames_NeverAffectOneAnother()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var touched = new Game(roster, Seed);
            var untouched = new Game(roster, Seed);

            touched.LaneFor(FrogColour.Green).MoveForward();
            PlayThroughOneTurn(touched);

            Assert.That(untouched.ActiveFrog, Is.EqualTo(FrogColour.Green));
            Assert.That(untouched.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
            Assert.That(untouched.LaneFor(FrogColour.Green).Position, Is.EqualTo(0));
        }

        [Test]
        public void TwoGamesWithTheSameSeedAndRoster_PlayedThroughTheSameTurns_ProduceTheSameRollsAndCards()
        {
            var roster = FourFrogRoster();
            var first = new Game(roster, Seed);
            var second = new Game(roster, Seed);

            for (var turn = 0; turn < TurnsToPlay; turn++)
            {
                first.RollDie();
                second.RollDie();

                Assert.That(second.DrawnRoll.Face, Is.EqualTo(first.DrawnRoll.Face), $"turn {turn}: face");
                Assert.That(
                    second.DrawnCard.Multiplicand, Is.EqualTo(first.DrawnCard.Multiplicand), $"turn {turn}: multiplicand");
                Assert.That(
                    second.DrawnCard.Multiplier, Is.EqualTo(first.DrawnCard.Multiplier), $"turn {turn}: multiplier");

                FinishTurnFrom(first, TurnPhase.RolledAndCardDrawn);
                FinishTurnFrom(second, TurnPhase.RolledAndCardDrawn);
            }
        }

        [Test]
        public void TwoGamesWithDifferentSeeds_PlayedThroughTheSameTurns_Diverge()
        {
            var roster = FourFrogRoster();
            var first = new Game(roster, Seed);
            var second = new Game(roster, Seed + 1);

            var diverged = false;

            for (var turn = 0; turn < TurnsToPlay && !diverged; turn++)
            {
                first.RollDie();
                second.RollDie();

                if (second.DrawnRoll.Face != first.DrawnRoll.Face
                    || second.DrawnCard.Multiplicand != first.DrawnCard.Multiplicand
                    || second.DrawnCard.Multiplier != first.DrawnCard.Multiplier)
                {
                    diverged = true;
                }

                FinishTurnFrom(first, TurnPhase.RolledAndCardDrawn);
                FinishTurnFrom(second, TurnPhase.RolledAndCardDrawn);
            }

            Assert.That(diverged, Is.True, $"no roll or card differed across {TurnsToPlay} turns");
        }

        [Test]
        public void TheSeedAGameIsRunningOn_IsReadableBack_AndMatchesTheConstructorSeed()
        {
            var game = new Game(FourFrogRoster(), Seed);

            Assert.That(game.Seed, Is.EqualTo(Seed));
        }

        // docs/specs/reference/index.md#where-v1-fills-a-gap-the-board-leaves-open:
        // "The game also ends on its own once every frog is home." Computed
        // fresh from lane position every time it's asked — no flag, and
        // nothing is called to "notice" it.
        [Test]
        public void AGameWhereEveryFrogIsOnItsEndLog_ReportsItselfOver_WithNoSeparateCallNeeded()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var game = new Game(roster, Seed);

            SendHome(game, FrogColour.Green);
            SendHome(game, FrogColour.Blue);

            Assert.That(game.IsOver, Is.True);
        }

        // The exact state docs/specs/ui/game-over.md's own mockup is drawn
        // in — four frogs, one home, three still short of LaneWinningPosition
        // — described there as "the state where the game was ended before
        // the others finished."
        [Test]
        public void EndingTheGameDeliberately_WithFrogsShortOfHome_ReportsTheGameAsOver()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Green);
            AdvanceTo(game, FrogColour.Blue, 6);
            AdvanceTo(game, FrogColour.Orange, 4);
            AdvanceTo(game, FrogColour.Pink, 1);

            game.EndGame();

            Assert.That(game.IsOver, Is.True);
        }

        // docs/specs/ui/end-game-confirm.md: ending "stops the game and shows
        // the results" rather than resetting anyone — every frog keeps the
        // pads it has.
        [Test]
        public void EndingTheGameDeliberately_ChangesNoFrogsPosition()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Green);
            AdvanceTo(game, FrogColour.Blue, 6);
            AdvanceTo(game, FrogColour.Orange, 4);
            AdvanceTo(game, FrogColour.Pink, 1);
            var positionsBefore = roster.ToDictionary(colour => colour, colour => game.LaneFor(colour).Position);

            game.EndGame();

            foreach (var colour in roster)
            {
                Assert.That(game.LaneFor(colour).Position, Is.EqualTo(positionsBefore[colour]), colour.ToString());
            }
        }

        // docs/specs/ui/game-over.md#invariants: "the winner is the frog
        // that reached the End log first, and finishing order is the order
        // frogs got home." Every finisher sits on the same
        // Lane.LaneWinningPosition, so position alone cannot tell them apart
        // afterward — Orange finishes before Blue here even though Blue
        // comes first in the roster, proving finishing order tracks arrival,
        // not the roster array.
        [Test]
        public void Winner_IsWhicheverFrogFinishedFirst_AndFinishingOrderIsArrivalOrderNotRosterOrder()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Orange);

            game.RecordFinish(FrogColour.Orange);

            Assert.That(game.Winner, Is.EqualTo(FrogColour.Orange));
            Assert.That(game.FinishingOrder, Is.EqualTo(new[] { FrogColour.Orange }));

            SendHome(game, FrogColour.Blue);
            game.RecordFinish(FrogColour.Blue);

            Assert.That(game.Winner, Is.EqualTo(FrogColour.Orange), "a later finisher must not change who won");
            Assert.That(game.FinishingOrder, Is.EqualTo(new[] { FrogColour.Orange, FrogColour.Blue }));
        }

        // docs/specs/ui/game-over.md#invariants: "the winner is the frog that
        // reached the End log first." Nothing outside Core ever announced an
        // arrival — #211 added RecordFinish and left the wiring to a later
        // issue, #224 declined it as belonging elsewhere, and the result was
        // that a real game's Winner stayed null however it was played. So the
        // arrival is recorded by the turn itself: grading can only happen
        // while the turn is Answering, and ShowResult is the very next call
        // the phase machine will accept, which makes it the first moment
        // after a move that Core is guaranteed to be given.
        [Test]
        public void AFrogThatReachesItsEndLogOnItsTurn_IsRecordedAsAFinisher_WithoutAnybodyCallingRecordFinish()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var game = new Game(roster, Seed);
            AdvanceTo(game, FrogColour.Green, Lane.LaneWinningPosition - 1);

            game.RollDie();
            game.BeginAnswering();
            game.LaneFor(game.ActiveFrog).Resolve(game.DrawnCard.Product, game.DrawnCard);
            game.ShowResult();

            Assert.That(game.LaneFor(FrogColour.Green).IsHome, Is.True, "a correct answer on pad 7 lands on the End log");
            Assert.That(game.FinishingOrder, Is.EqualTo(new[] { FrogColour.Green }));
            Assert.That(game.Winner, Is.EqualTo(FrogColour.Green));
        }

        // The other half of the same rule: a turn that did not land a frog
        // home records nothing, so an arrival cannot be manufactured by
        // taking turns.
        [Test]
        public void ATurnThatDoesNotLandAFrogHome_RecordsNoFinisher()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var game = new Game(roster, Seed);

            game.RollDie();
            game.BeginAnswering();
            game.LaneFor(game.ActiveFrog).Resolve(game.DrawnCard.Product, game.DrawnCard);
            game.ShowResult();

            Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(1));
            Assert.That(game.FinishingOrder, Is.Empty);
            Assert.That(game.Winner, Is.Null);
        }

        // docs/specs/ui/game-over.md: "Frogs that did not finish are ranked
        // by how many lily pads they made", and the open question's own
        // wording is the current, still-open-to-change behaviour to build:
        // "Two frogs on the same pad currently share a place number."
        [Test]
        public void Standings_RanksUnfinishedFrogsBelowFinishedOnes_ByLilyPadsMade_WithTiesSharingAPlaceNumber()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
            var game = new Game(roster, Seed);
            SendHome(game, FrogColour.Green);
            game.RecordFinish(FrogColour.Green);
            AdvanceTo(game, FrogColour.Blue, 4);
            AdvanceTo(game, FrogColour.Orange, 4); // ties with Blue
            AdvanceTo(game, FrogColour.Pink, 1);

            var standings = game.Standings;
            var green = standings.Single(row => row.Colour == FrogColour.Green);
            var blue = standings.Single(row => row.Colour == FrogColour.Blue);
            var orange = standings.Single(row => row.Colour == FrogColour.Orange);
            var pink = standings.Single(row => row.Colour == FrogColour.Pink);

            Assert.That(blue.Place, Is.EqualTo(orange.Place), "same lily pad, same place number");
            Assert.That(blue.Place, Is.GreaterThan(green.Place), "unfinished ranked below every finished frog");
            Assert.That(pink.Place, Is.GreaterThan(blue.Place), "fewer pads, a lower place");
        }

        // docs/specs/ui/game-over.md: the headline reads "Game over" rather
        // than naming a winner when the game was ended before anybody got
        // home. Core's job is the unambiguous fact, not the wording — a null
        // Winner is that fact.
        [Test]
        public void AGameEndedBeforeAnyoneFinished_HasNoWinner()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue };
            var game = new Game(roster, Seed);
            AdvanceTo(game, FrogColour.Green, 3);

            game.EndGame();

            Assert.That(game.IsOver, Is.True);
            Assert.That(game.Winner, Is.Null);
            Assert.That(game.FinishingOrder, Is.Empty);
        }

        // docs/specs/ui/end-game-confirm.md builds its cost sentence from
        // this count — "Three frogs are still swimming..." — and needs it
        // mid-game, not only once the game is already over.
        [Test]
        public void FrogsStillSwimming_IsReadableAtAnyPointDuringPlay_NotOnlyAfterTheGameEnds()
        {
            var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange };
            var game = new Game(roster, Seed);

            Assert.That(game.FrogsStillSwimming, Is.EqualTo(3));

            SendHome(game, FrogColour.Green);
            game.RecordFinish(FrogColour.Green);

            Assert.That(game.IsOver, Is.False, "the game keeps going once one frog is home");
            Assert.That(game.FrogsStillSwimming, Is.EqualTo(2));
        }

        const int TurnsToPlay = 12;

        static FrogColour[] FourFrogRoster()
        {
            return new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
        }

        // Drives one whole turn from wherever the phase currently is through
        // to hand-off completing — the game-board.md sequence, minus the
        // roll if it has already happened this turn.
        static void PlayThroughOneTurn(Game game)
        {
            FinishTurnFrom(game, TurnPhase.WaitingToRoll);
        }

        static void FinishTurnFrom(Game game, TurnPhase phase)
        {
            if (phase == TurnPhase.WaitingToRoll)
            {
                game.RollDie();
            }

            game.BeginAnswering();
            game.ShowResult();
            game.BeginHandOff();
            game.CompleteHandOff();
        }

        // Sends a frog home by advancing its Lane directly to the End log —
        // not by answering a question. Moving a frog as the result of an
        // answer is #210's job and is not reached for here.
        static void SendHome(Game game, FrogColour colour)
        {
            var lane = game.LaneFor(colour);

            while (!lane.IsHome)
            {
                lane.MoveForward();
            }
        }

        // Advances a frog's Lane directly to a given lily pad — for setting
        // up standings scenarios without going through a real turn.
        static void AdvanceTo(Game game, FrogColour colour, int position)
        {
            var lane = game.LaneFor(colour);

            for (var move = 0; move < position; move++)
            {
                lane.MoveForward();
            }
        }
    }
}
