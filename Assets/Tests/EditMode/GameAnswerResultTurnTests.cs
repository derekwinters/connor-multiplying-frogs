using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The runtime <c>IAnswerResultTurn</c>'s two hand-off steps, and the one
    /// turn in a game where the second of them must not run.
    ///
    /// Everything else this type does is a straight read of something Core
    /// already decided, and <c>AnswerResultDialogViewTests</c> asserts those
    /// reads through the dialog that makes them. What is here is the pair of
    /// calls the `Next turn` button runs, because
    /// docs/specs/ui/game-board.md#behaviour makes the last one conditional and
    /// nothing had ever reached that turn outside a Core-only test.
    /// </summary>
    public sealed class GameAnswerResultTurnTests
    {
        static readonly FrogColour[] Roster = { FrogColour.Green, FrogColour.Blue };

        // The same named seed the Core-tier scripted games use.
        const ulong Seed = 20260810UL;

        // A stop on the turn loop, so a play that stops making progress fails
        // as a test rather than hanging the editor.
        const int MaxTurns = 64;

        [Test]
        public void CompleteHandOff_PassesTheDevice_WhileSomebodyIsStillSwimming()
        {
            var game = new Game(Roster, Seed);

            PlayOneTurnRight(game);

            Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue), "the turn passed to the next frog in order");
            Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
        }

        [Test]
        public void CompleteHandOff_DoesNothingAtAll_WhenThatHopGotTheLastFrogHome()
        {
            var game = new Game(Roster, Seed);

            // Every answer right, so the game plays itself out to the ending
            // docs/specs/ui/game-board.md describes: "When the last frog gets
            // home, the game ends itself. The hop finishes, and game over
            // follows with no input from anybody."
            //
            // There is no next frog to advance to at that point, and
            // Game.CompleteHandOff says so by throwing. Before this was
            // guarded, the very last `Next turn` in every completed game threw
            // inside the answer-result dialog's hand-off — the one turn no
            // EditMode test had ever reached.
            for (var turn = 0; turn < MaxTurns && !game.IsOver; turn++)
            {
                PlayOneTurnRight(game);
            }

            Assert.That(game.IsOver, Is.True, "the scripted game never ended");
            Assert.That(game.TurnOrder.All(colour => game.LaneFor(colour).IsHome), Is.True);
            Assert.That(
                game.Phase, Is.EqualTo(TurnPhase.HandOff),
                "the last hand-off is left uncompleted, because there is nobody to hand off to");
        }

        [Test]
        public void NextFrog_IsNobody_WhenThatHopGotTheLastFrogHome_RatherThanThrowing()
        {
            var game = new Game(Roster, Seed);

            // One frog short of the ending, so the turn built below is the one
            // whose answer gets the last frog home.
            foreach (var colour in Roster)
            {
                var lane = game.LaneFor(colour);
                var steps = colour == game.ActiveFrog
                    ? Lane.LaneWinningPosition - 1
                    : Lane.LaneWinningPosition;

                for (var step = 0; step < steps; step++)
                {
                    lane.MoveForward();
                }
            }

            game.RollDie();
            var card = game.DrawnCard;
            game.BeginAnswering();

            var resolution = game.LaneFor(game.ActiveFrog).Resolve(card.Product, card);
            game.ShowResult();

            Assert.That(game.IsOver, Is.True, "the scripted position did not end the game");

            var turn = new GameAnswerResultTurn(game, resolution);

            // Game.NextActiveFrog throws here, and the answer-result dialog
            // reads this on every turn to name its one button — so before this
            // was guarded, the winning move of every completed game threw
            // instead of showing a result.
            Assert.That(turn.NextFrog, Is.Null, "there is no next player once every frog is home");
        }

        // One turn through the phases a played turn moves through, ending with
        // the two steps GameAnswerResultTurn owns.
        static void PlayOneTurnRight(Game game)
        {
            game.RollDie();

            var card = game.DrawnCard;
            game.BeginAnswering();

            var resolution = game.LaneFor(game.ActiveFrog).Resolve(card.Product, card);
            game.ShowResult();

            var turn = new GameAnswerResultTurn(game, resolution);
            turn.BeginHandOff();
            turn.CompleteHandOff();
        }
    }
}
