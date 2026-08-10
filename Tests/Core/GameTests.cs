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
    }
}
