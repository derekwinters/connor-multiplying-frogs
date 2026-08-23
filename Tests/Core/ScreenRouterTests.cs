using System.Collections.Generic;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// The traffic cop — issue #213. Owns which one of four screens is
    /// current, which one of five dialogs (if any) is layered over it, and
    /// what the hardware back button does in every state, per the
    /// back-button table on the issue and each screen page's own
    /// `## Behaviour` section.
    ///
    /// This type does not decide *whether* a game has ended — that is
    /// <c>Game.IsOver</c> (#211) — it only reacts to being told. These tests
    /// drive it with a bare call to <see cref="ScreenRouter.GameHasEnded"/>,
    /// standing in for that not-yet-wired signal.
    /// </summary>
    public sealed class ScreenRouterTests
    {
        // docs/specs/ui/game-board.md#behaviour: "Hardware back opens the
        // settings dialog. It does not quit, and it never quits without the
        // confirm."
        [Test]
        public void HandleBack_OnTheGameBoardWithNoDialogOpen_OpensTheSettingsDialog()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.Settings));
        }

        // settings-dialog.md#behaviour: "Hardware back inside this dialog
        // does what `Back to the game` does — the least destructive button."
        // Closing "returns to the board with nothing changed."
        [Test]
        public void HandleBack_WithSettingsOpen_DoesWhatBackToTheGameDoes()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);
            router.OpenDialog(Dialog.Settings);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.Null);
        }

        // end-game-confirm.md#behaviour: "Hardware back does what `Keep
        // playing` does" — closes back to the board, game continues.
        [Test]
        public void HandleBack_WithEndGameConfirmOpen_DoesWhatKeepPlayingDoes()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);
            router.OpenDialog(Dialog.EndGameConfirm);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.Null);
        }

        // roll-and-card.md, working-out-grid.md, answer-result.md,
        // player-won.md #behaviour: "Hardware back does nothing." Not a
        // close — genuinely inert, per shared-components.md#dialog's
        // now-amended invariant, which counts four of them.
        [TestCase(Dialog.RollAndCard)]
        [TestCase(Dialog.WorkingOutGrid)]
        [TestCase(Dialog.AnswerResult)]
        [TestCase(Dialog.PlayerWon)]
        public void HandleBack_WithAnInertDialogOpen_IsANoOp(Dialog inertDialog)
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);
            router.OpenDialog(inertDialog);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.EqualTo(inertDialog));
        }

        // title-screen.md#behaviour: "The hardware back button on this
        // screen exits the app. It is the only screen where back exits."
        [Test]
        public void HandleBack_OnTheTitleScreen_SignalsAppExit()
        {
            var router = new ScreenRouter();

            router.HandleBack();

            Assert.That(router.AppExitRequested, Is.True);
        }

        // "It is the only screen where back exits" — the other three screens
        // and every dialog leave AppExitRequested false.
        [TestCase(Screen.GameSetup, null)]
        [TestCase(Screen.GameBoard, null)]
        [TestCase(Screen.GameOver, null)]
        [TestCase(Screen.GameBoard, Dialog.Settings)]
        [TestCase(Screen.GameBoard, Dialog.EndGameConfirm)]
        [TestCase(Screen.GameBoard, Dialog.RollAndCard)]
        [TestCase(Screen.GameBoard, Dialog.WorkingOutGrid)]
        [TestCase(Screen.GameBoard, Dialog.AnswerResult)]
        [TestCase(Screen.GameBoard, Dialog.PlayerWon)]
        public void HandleBack_AnywhereOtherThanTheTitleScreen_NeverSignalsAppExit(
            Screen screen, Dialog? dialog)
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(screen);
            if (dialog.HasValue)
            {
                router.OpenDialog(dialog.Value);
            }

            router.HandleBack();

            Assert.That(router.AppExitRequested, Is.False);
        }

        // game-setup.md#behaviour: "Hardware back does what `Back` does" —
        // returns to the title screen.
        [Test]
        public void HandleBack_OnGameSetup_ReturnsToTheTitleScreen()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameSetup);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.TitleScreen));
            Assert.That(router.CurrentDialog, Is.Null);
        }

        // game-over.md#behaviour: "Hardware back does what `Back to the
        // title` does."
        [Test]
        public void HandleBack_OnGameOver_ReturnsToTheTitleScreen()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameOver);

            router.HandleBack();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.TitleScreen));
            Assert.That(router.CurrentDialog, Is.Null);
        }

        // shared-components.md#dialog: "Stacked... Does not happen. A dialog
        // never opens over another dialog." Requesting a second dialog closes
        // the first before the second becomes current — asserted through the
        // sequence of CurrentDialog values StateChanged reports, not just the
        // final value, because a Dialog? field alone cannot fail to prove
        // "closes before opens": the sequencing is the thing under test.
        [Test]
        public void OpeningADialog_WhileAnotherIsOpen_ClosesTheFirstBeforeTheSecondBecomesCurrent()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);
            router.OpenDialog(Dialog.RollAndCard);

            var observed = new List<Dialog?>();
            router.StateChanged += () => observed.Add(router.CurrentDialog);

            router.OpenDialog(Dialog.WorkingOutGrid);

            Assert.That(observed, Is.EqualTo(new Dialog?[] { null, Dialog.WorkingOutGrid }));
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.WorkingOutGrid));
        }

        // game-board.md#behaviour: "When the last frog gets home, the game
        // ends itself... game over follows with no input from anybody."
        // GameHasEnded is the stand-in for that not-yet-wired signal — see
        // issue #213's "Presentation decides which screen; Core decides
        // whether the game ended."
        [Test]
        public void GameHasEnded_FiredFromTheBoardWithNoDialogOpen_MovesToGameOver()
        {
            var router = new ScreenRouter();
            router.NavigateToScreen(Screen.GameBoard);

            router.GameHasEnded();

            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameOver));
            Assert.That(router.CurrentDialog, Is.Null);
        }

        // Issue #213's navigation graph, driven start to finish. The
        // router's own primitives — NavigateToScreen, OpenDialog, CloseDialog
        // — are what a later screen's button handler calls; this test drives
        // them in the order a real game visits every node, and checks the
        // graph lands where it says at each step. Dialog-to-dialog edges
        // (roll-and-card → working-out grid → answer result, and settings →
        // end-game confirm) are OpenDialog calls: close-then-open, never two
        // dialogs current at once.
        [Test]
        public void DrivingTheRouter_ThroughTheWholeNavigationGraph_LandsOnTheExpectedScreenAtEveryStep()
        {
            var router = new ScreenRouter();
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.TitleScreen));
            Assert.That(router.CurrentDialog, Is.Null);

            // Title screen --Play--> Game setup
            router.NavigateToScreen(Screen.GameSetup);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameSetup));

            // Game setup --Start--> Game board
            router.NavigateToScreen(Screen.GameBoard);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.Null);

            // Game board --Roll--> [Roll and card]
            router.OpenDialog(Dialog.RollAndCard);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.RollAndCard));

            // [Roll and card] --Solve it--> [Working-out grid]
            router.OpenDialog(Dialog.WorkingOutGrid);
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.WorkingOutGrid));

            // [Working-out grid] --Check it--> [Answer result]
            router.OpenDialog(Dialog.AnswerResult);
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.AnswerResult));

            // [Answer result] --(closes)--> Game board
            router.CloseDialog();
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.Null);

            // Game board --Settings gear--> [Settings dialog]
            router.OpenDialog(Dialog.Settings);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.Settings));

            // [Settings dialog] --End the game--> [End-game confirm]
            router.OpenDialog(Dialog.EndGameConfirm);
            Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.EndGameConfirm));

            // [End-game confirm] --End the game--> Game over
            router.NavigateToScreen(Screen.GameOver);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameOver));
            Assert.That(router.CurrentDialog, Is.Null);

            // Game over --Play again--> Game board
            router.NavigateToScreen(Screen.GameBoard);
            Assert.That(router.CurrentScreen, Is.EqualTo(Screen.GameBoard));
            Assert.That(router.CurrentDialog, Is.Null);
        }
    }
}
