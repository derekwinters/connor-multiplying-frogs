using System;
using System.Reflection;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    public sealed class LaneTests
    {
        [Test]
        public void ANewLane_StartsOnTheStartLog()
        {
            var lane = new Lane();

            Assert.That(lane.Position, Is.EqualTo(0));
        }

        [Test]
        public void MoveForward_AdvancesTheFrogExactlyOneLilyPad()
        {
            var lane = new Lane();

            lane.MoveForward();

            Assert.That(lane.Position, Is.EqualTo(1));
        }

        [Test]
        public void MoveBack_MovesTheFrogExactlyOneLilyPad()
        {
            var lane = new Lane();
            lane.MoveForward();
            lane.MoveForward();

            lane.MoveBack();

            Assert.That(lane.Position, Is.EqualTo(1));
        }

        // The Start log is a floor, not a special space: a wrong answer there
        // leaves the frog where it is rather than moving it off the lane.
        // docs/specs/rules.md — "The Start log is a floor, not a special space".
        [Test]
        public void MoveBack_OnTheStartLog_LeavesTheFrogAtTheFloor()
        {
            var lane = new Lane();

            lane.MoveBack();

            Assert.That(lane.Position, Is.EqualTo(0));
        }

        // A lane is nine positions — the Start log, seven lily pads, and the
        // End log — so eight correct answers from the Start log land the frog
        // on the End log. docs/specs/rules.md: "a lane has nine positions".
        [Test]
        public void EightForwardMoves_FromTheStartLog_LandOnTheEndLog()
        {
            var lane = new Lane();

            for (var move = 0; move < 8; move++)
            {
                lane.MoveForward();
            }

            Assert.That(lane.Position, Is.EqualTo(Lane.LaneWinningPosition));
        }

        // IsHome matches the Home chip state in game-board.md § Behaviour: a
        // frog that reaches the End log stays there and its chip switches to
        // Home. False until the frog lands on the End log, true after.
        [Test]
        public void IsHome_IsFalseUntilTheFrogReachesTheEndLog_ThenTrue()
        {
            var lane = new Lane();

            for (var move = 0; move < 7; move++)
            {
                lane.MoveForward();
                Assert.That(lane.IsHome, Is.False, $"home after only {move + 1} forward moves");
            }

            lane.MoveForward();

            Assert.That(lane.IsHome, Is.True);
        }

        // The top of the lane is a clamp, symmetric with the Start log's
        // floor: a home frog is never legally asked to move (a home frog is
        // skipped in turn order), so a further move guards a call that should
        // not happen rather than exercising a rule of play. Settled by Derek
        // and recorded in docs/specs/rules.md.
        [Test]
        public void MoveForward_OnTheEndLog_LeavesTheFrogHomeOnTheEndLog()
        {
            var lane = new Lane();
            for (var move = 0; move < 8; move++)
            {
                lane.MoveForward();
            }

            lane.MoveForward();

            Assert.That(lane.Position, Is.EqualTo(Lane.LaneWinningPosition));
            Assert.That(lane.IsHome, Is.True);
        }

        [Test]
        public void MoveBack_OnTheEndLog_LeavesTheFrogHomeOnTheEndLog()
        {
            var lane = new Lane();
            for (var move = 0; move < 8; move++)
            {
                lane.MoveForward();
            }

            lane.MoveBack();

            Assert.That(lane.Position, Is.EqualTo(Lane.LaneWinningPosition));
            Assert.That(lane.IsHome, Is.True);
        }

        // Resolving a turn grades the submitted answer against the card and
        // moves the frog exactly as the outcome table in docs/specs/rules.md
        // says — the resolution entry point maps onto MoveForward/MoveBack,
        // adding no position arithmetic of its own.
        [Test]
        public void Resolve_WithACorrectAnswer_MovesForwardOneLilyPadAndReportsCorrect()
        {
            var lane = new Lane();
            lane.MoveForward();
            lane.MoveForward();
            var card = Card.Draw(Pile.Easy, Rng.FromSeed(1));

            var resolution = lane.Resolve(card.Product, card);

            Assert.That(resolution.Outcome, Is.EqualTo(TurnOutcome.Correct));
            Assert.That(resolution.PositionBefore, Is.EqualTo(2));
            Assert.That(resolution.PositionAfter, Is.EqualTo(3));
            Assert.That(resolution.CorrectAnswer, Is.EqualTo(card.Product));
            Assert.That(lane.Position, Is.EqualTo(3));
        }

        [Test]
        public void Resolve_WithAWrongAnswerAboveTheStartLog_MovesBackOneLilyPadAndReportsWrongAboveStartLog()
        {
            var lane = new Lane();
            lane.MoveForward();
            lane.MoveForward();
            var card = Card.Draw(Pile.Easy, Rng.FromSeed(1));

            var resolution = lane.Resolve(card.Product + 1, card);

            Assert.That(resolution.Outcome, Is.EqualTo(TurnOutcome.WrongAboveStartLog));
            Assert.That(resolution.PositionBefore, Is.EqualTo(2));
            Assert.That(resolution.PositionAfter, Is.EqualTo(1));
            Assert.That(resolution.CorrectAnswer, Is.EqualTo(card.Product));
            Assert.That(lane.Position, Is.EqualTo(1));
        }

        // The Start log is a floor, not a special space: a wrong answer here
        // resolves via the same MoveBack() call as any other wrong answer —
        // it is Lane's own floor clamp that leaves the position unchanged,
        // not a clamp reimplemented in Resolve. docs/specs/rules.md — "the
        // Start log is a floor, not a special space".
        [Test]
        public void Resolve_WithAWrongAnswerOnTheStartLog_LeavesThePositionUnchangedAndReportsWrongOnStartLog()
        {
            var lane = new Lane();
            var card = Card.Draw(Pile.Easy, Rng.FromSeed(1));

            var resolution = lane.Resolve(card.Product + 1, card);

            Assert.That(resolution.Outcome, Is.EqualTo(TurnOutcome.WrongOnStartLog));
            Assert.That(resolution.PositionBefore, Is.EqualTo(0));
            Assert.That(resolution.PositionAfter, Is.EqualTo(0));
            Assert.That(resolution.CorrectAnswer, Is.EqualTo(card.Product));
            Assert.That(lane.Position, Is.EqualTo(0));
        }

        // Consistent with WorkingOutGrid.For's guard on the same Card
        // parameter: a null card is a caller error, not a fourth outcome.
        [Test]
        public void Resolve_WithANullCard_ThrowsArgumentNullException()
        {
            var lane = new Lane();

            Assert.That(() => lane.Resolve(42, null), Throws.ArgumentNullException);
        }

        // ADR-0002: "nothing in the grid is marked" — only the answer row is
        // graded. Proved structurally, not just by convention: Resolve's
        // signature is exactly (int, Card), so there is no parameter or field
        // the working-out grid's carry boxes or partial-product rows could
        // even be passed through.
        [Test]
        public void Resolve_TakesOnlyTheSubmittedAnswerAndTheCard()
        {
            var method = typeof(Lane).GetMethod(nameof(Lane.Resolve));

            Assert.That(method, Is.Not.Null);

            var parameters = method.GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(Card)));
        }

        // Frogs are independent: each player keeps their own lane for the
        // whole game, and a lane never affects another. Worth an explicit
        // test rather than an assumption — it is what lets the rest of the
        // game treat a player's whole state as one number.
        // docs/specs/rules.md — "frogs are independent".
        [Test]
        public void TwoIndependentLanes_NeverAffectOneAnother()
        {
            var moved = new Lane();
            var untouched = new Lane();

            moved.MoveForward();
            moved.MoveForward();
            moved.MoveForward();

            Assert.That(untouched.Position, Is.EqualTo(0));
            Assert.That(untouched.IsHome, Is.False);
        }
    }
}
