using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
using CoreScreen = Frogs.Core.Screen;
using Button = Frogs.Unity.UI.Button;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The composition root — issue #285. Every screen in the POC was built and
    /// unit-tested before this existed, and none of them was ever constructed
    /// outside a test, so the built app showed the camera's clear colour and
    /// nothing else.
    ///
    /// **What EditMode can and cannot say about that.** It cannot run the
    /// player: no <c>Awake</c> on <c>AddComponent</c>, no <c>Start</c>, no
    /// frames, and <c>Time.deltaTime</c> never advances — which is why
    /// <see cref="AppRoot"/> keeps its <c>[RuntimeInitializeOnLoadMethod]</c>
    /// hook down to one call into <see cref="AppRoot.Initialize"/>, and why
    /// every entering sequence in the flow is advanced through a public method
    /// rather than only from an <c>Update</c>. This suite drives those entry
    /// points directly, so what it asserts is the wiring — which screen is on,
    /// which view is under which root, and whether a tap reaches Core — and
    /// never anything about rendering.
    ///
    /// The taps are the shared components' own pointer handlers, called the way
    /// <c>GameBoardScreenViewTests</c> and <c>WorkingOutGridViewTests</c> call
    /// them. Nothing here reaches past a view's public surface: if a press
    /// stops reaching Core, it fails here the way it would fail in a hand.
    /// </summary>
    public sealed class AppRootTests
    {
        // A named seed, so a run is the same run twice. Nothing below asserts a
        // particular card — the answers typed are read back off Core — but a
        // failure that only happens for one deal should be reproducible.
        const ulong Seed = 20260811UL;

        // Long enough to run any entering or hand-off sequence in the flow
        // straight to its end, on a clock this suite controls.
        const float LongEnough = 10f;

        // A stop on the turn loop, so a game that stops making progress fails
        // as a test rather than hanging the editor.
        const int MaxTurns = 64;

        [Test]
        public void TheAppOpensOnTheTitleScreen_WithResumeHidden()
        {
            var root = CreateRoot();

            try
            {
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen),
                    "the app boots into the title screen, not a blue rectangle");
                Assert.That(root.Router.CurrentDialog, Is.Null, "nothing is layered over it");

                Assert.That(ActiveScreenRoots(root), Is.EqualTo(new[] { CoreScreen.TitleScreen }));

                Assert.That(root.TitleScreen.NewButton.gameObject.activeSelf, Is.True);
                Assert.That(root.TitleScreen.ResumeButton.gameObject.activeSelf, Is.False,
                    "there is no save system, so RESUME is not laid out at all");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void TheCanvasIsTheOneEveryScreenIsMeasuredIn_AndTapsCanReachIt()
        {
            var root = CreateRoot();

            try
            {
                Assert.That(root.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(root.CanvasScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(
                    root.CanvasScaler.referenceResolution,
                    Is.EqualTo(new Vector2(AppRoot.ReferenceWidth, AppRoot.ReferenceHeight)),
                    "docs/specs/ui/shared-components.md — the canvas every component is measured in");

                Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Not.Null,
                    "without a raycaster no tap ever reaches a button");
                Assert.That(root.EventSystem, Is.Not.Null);
                Assert.That(
                    root.EventSystem.GetComponent<BaseInputModule>(), Is.Not.Null,
                    "an EventSystem with no input module delivers nothing");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void EveryScreenAndEveryDialog_HasItsViewUnderTheRoutersOwnRoot()
        {
            var root = CreateRoot();

            try
            {
                AssertViewUnder<TitleScreenView>(root.RootFor(CoreScreen.TitleScreen));
                AssertViewUnder<GameSetupScreenView>(root.RootFor(CoreScreen.GameSetup));
                AssertViewUnder<GameBoardScreenView>(root.RootFor(CoreScreen.GameBoard));
                AssertViewUnder<GameOverScreenView>(root.RootFor(CoreScreen.GameOver));
                AssertViewUnder<HowToPlayScreenView>(root.RootFor(CoreScreen.HowToPlay));

                AssertViewUnder<RollAndCardDialogView>(root.RootFor(Dialog.RollAndCard));
                AssertViewUnder<WorkingOutGridView>(root.RootFor(Dialog.WorkingOutGrid));
                AssertViewUnder<AnswerResultDialogView>(root.RootFor(Dialog.AnswerResult));
                AssertViewUnder<PlayerWonDialogView>(root.RootFor(Dialog.PlayerWon));
                AssertViewUnder<SettingsDialogView>(root.RootFor(Dialog.Settings));
                AssertViewUnder<EndGameConfirmView>(root.RootFor(Dialog.EndGameConfirm));
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void EveryScreenAndDialogRoot_FillsTheCanvas_SoAScreenIsNotLaidOutAgainstNothing()
        {
            var root = CreateRoot();

            try
            {
                foreach (CoreScreen screen in Enum.GetValues(typeof(CoreScreen)))
                {
                    AssertFillsItsParent(root.RootFor(screen), screen.ToString());
                }

                foreach (Dialog dialog in Enum.GetValues(typeof(Dialog)))
                {
                    AssertFillsItsParent(root.RootFor(dialog), dialog.ToString());
                }
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void TappingNew_ReachesGameSetup_WithEverySeatEmpty()
        {
            var root = CreateRoot();

            try
            {
                // A stale line-up must not be sitting there —
                // docs/specs/ui/game-setup.md#behaviour: "Entering: seats all
                // empty, every time."
                TapSeat(root.GameSetup, FrogColour.Pink);
                Assert.That(root.GameSetup.IsSeatChosen(FrogColour.Pink), Is.True);

                Tap(root.TitleScreen.NewButton);

                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameSetup));
                Assert.That(ActiveScreenRoots(root), Is.EqualTo(new[] { CoreScreen.GameSetup }));
                Assert.That(root.GameSetup.ChosenOrder, Is.Empty);
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void TappingStart_ReachesTheBoard_ShowingTheGameThoseSeatsStarted()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.CurrentGame, Is.Not.Null, "the board is showing a game");
                Assert.That(
                    root.CurrentGame, Is.SameAs(root.GameSetup.StartedGame),
                    "the board reads the game Start created, not one of its own");
                Assert.That(
                    root.CurrentGame.TurnOrder, Is.EqualTo(new[] { FrogColour.Green, FrogColour.Blue }),
                    "turn order is the order the seats were tapped");
                Assert.That(root.Board.Lanes.Count, Is.EqualTo(2), "one lane per frog, and no placeholder");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void AWholeTurn_CanBeTappedThrough_AndTheFrogMovesAndTheTurnPasses()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var game = root.CurrentGame;

                // Roll -> roll and card.
                Tap(root.Board.RollButton);

                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.RollAndCard));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.RolledAndCardDrawn), "Core rolled, not the dialog");
                Assert.That(
                    root.RollAndCard.MultiplicandText.text, Is.EqualTo(game.DrawnCard.Multiplicand.ToString()),
                    "the dialog is reading the card Core drew");
                Assert.That(root.Board.RollButton.IsDisabled, Is.True, "a double-tap cannot roll twice");

                // Solve it -> the working-out grid, on that same card.
                var card = game.DrawnCard;
                Tap(root.RollAndCard.SolveItButton);

                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.WorkingOutGrid));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.Answering));
                Assert.That(
                    root.WorkingOutGrid.CardReadoutText.text, Does.Contain(card.Multiplicand.ToString()),
                    "the grid opened on the card the dialog showed");

                // Check it -> the answer result, with Core's own verdict.
                TypeAnswer(root.WorkingOutGrid, card.Product);
                Tap(root.WorkingOutGrid.CheckItButton);

                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.AnswerResult));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.ResultShown));
                Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(1), "a right answer is one lily pad");

                // Next turn -> the hop, then the board with the turn passed.
                Tap(root.AnswerResult.NextTurnButton);
                root.AnswerResult.Advance(LongEnough);

                Assert.That(root.Router.CurrentDialog, Is.Null, "the dialog layer is clear again");
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue), "the device has passed on");
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
                Assert.That(root.Board.RollButton.IsDisabled, Is.False, "the next player can roll");
                Assert.That(
                    root.Board.TurnBannerText.text, Does.Contain(FrogColour.Blue.ToString()),
                    "the board redrew for whoever is next");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void AWrongAnswer_IsGradedByCore_AndTheTurnStillPasses()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var game = root.CurrentGame;

                Tap(root.Board.RollButton);
                var card = game.DrawnCard;
                Tap(root.RollAndCard.SolveItButton);

                // Wrong by one, which on the Start log costs nothing —
                // docs/specs/reference: the Start log is a floor.
                TypeAnswer(root.WorkingOutGrid, card.Product + 1);
                Tap(root.WorkingOutGrid.CheckItButton);

                Assert.That(game.LaneFor(FrogColour.Green).Position, Is.Zero);

                Tap(root.AnswerResult.NextTurnButton);
                root.AnswerResult.Advance(LongEnough);

                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));
                Assert.That(root.Router.CurrentDialog, Is.Null);
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void PlayingEveryFrogHome_EndsTheGameItself_AndShowsTheStandings()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var game = root.CurrentGame;

                for (var turn = 0; turn < MaxTurns && !game.IsOver; turn++)
                {
                    PlayOneTurnCorrectly(root, game);
                }

                Assert.That(game.IsOver, Is.True, "the game never ended");

                // docs/specs/ui/game-board.md#behaviour: "When the last frog
                // gets home, the game ends itself... with no input from
                // anybody. A finished game never sits on this screen waiting
                // to be dismissed."
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameOver));
                Assert.That(root.Router.CurrentDialog, Is.Null);
                Assert.That(root.GameOver.HeadlineText.text, Is.EqualTo("Green wins!"),
                    "Green rolled first, so Green got home first");
                Assert.That(root.GameOver.RowCount, Is.EqualTo(game.Standings.Count));
            }
            finally
            {
                Destroy(root);
            }
        }

        // docs/specs/ui/player-won.md#behaviour, and #329's own acceptance
        // check: the dialog fires on the first frog home and on every one
        // after it, never twice for the same frog — and the last frog home
        // still reaches the standings, which is the path that must not
        // regress now that something sits in front of it.
        [Test]
        public void EveryFrogThatGetsHome_IsAnnouncedOnce_AndTheLastOneStillReachesTheStandings()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var game = root.CurrentGame;
                var announced = new List<string>();

                for (var turn = 0; turn < MaxTurns && !game.IsOver; turn++)
                {
                    var headline = PlayOneTurnCorrectly(root, game);

                    if (headline != null)
                    {
                        announced.Add(headline);
                    }
                }

                Assert.That(game.IsOver, Is.True, "the game never ended");

                // Two frogs, two arrivals, in the order they happened — and
                // only the first one "wins".
                Assert.That(announced, Is.EqualTo(new[] { "Green wins!", "Blue is home!" }));

                // The ending is exactly the one game-board.md already
                // described; the arrival dialog delayed it by one tap and
                // changed nothing else.
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameOver));
                Assert.That(root.Router.CurrentDialog, Is.Null);
                Assert.That(root.GameOver.HeadlineText.text, Is.EqualTo("Green wins!"));
                Assert.That(root.GameOver.RowCount, Is.EqualTo(game.Standings.Count));
            }
            finally
            {
                Destroy(root);
            }
        }

        // The other half of the same rule, and the one that is easiest to get
        // wrong: an arrival that is not the last one announces itself and then
        // hands straight back to the board, with the next player's turn
        // already begun. player-won.md's third invariant — "this dialog
        // changes nothing about the game" — and game-board.md's rule that a
        // frog on the End log stays there and play continues.
        [Test]
        public void TheFirstFrogHome_IsAnnounced_AndThenPlayCarriesOnOnTheBoard()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var game = root.CurrentGame;

                for (var turn = 0; turn < MaxTurns && !game.FrogJustHome.HasValue; turn++)
                {
                    Tap(root.Board.RollButton);
                    var card = game.DrawnCard;
                    Tap(root.RollAndCard.SolveItButton);
                    TypeAnswer(root.WorkingOutGrid, card.Product);
                    Tap(root.WorkingOutGrid.CheckItButton);
                    Tap(root.AnswerResult.NextTurnButton);
                    root.AnswerResult.Advance(LongEnough);
                }

                Assert.That(game.FrogJustHome, Is.EqualTo(FrogColour.Green), "Green rolled first");
                Assert.That(game.IsOver, Is.False, "Blue is still swimming");

                // The dialog is up, over the board, saying so.
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.PlayerWon));
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.PlayerWon.HeadlineText.text, Is.EqualTo("Green wins!"));
                Assert.That(root.PlayerWon.HandOnButton.Label.text, Is.EqualTo("Blue's turn"));

                // Hardware back does nothing here — a stray press must not
                // fast-forward past a moment the game stopped to mark.
                root.HandleBackButton();
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.PlayerWon));

                Tap(root.PlayerWon.HandOnButton);

                Assert.That(root.Router.CurrentDialog, Is.Null, "the dialog layer is clear again");
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard), "play carries on");
                Assert.That(game.IsOver, Is.False, "announcing a win does not end the game");
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));
                Assert.That(root.Board.RollButton.IsDisabled, Is.False, "the next player can roll");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void TheGearOpensSettings_AndEndingTheGameThroughItsConfirm_ReachesGameOver()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                TapSettings(root.Board);
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.Settings));
                Assert.That(root.Settings.Dialog.IsOpen, Is.True, "the settings panel was actually opened");

                Tap(root.Settings.EndGameButton);
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.EndGameConfirm),
                    "`End the game` opens the confirm and ends nothing");
                Assert.That(root.CurrentGame.IsOver, Is.False);
                Assert.That(root.EndGameConfirm.Dialog.IsOpen, Is.True);

                Tap(root.EndGameConfirm.EndTheGameButton);

                Assert.That(root.CurrentGame.IsOver, Is.True);
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameOver));
                Assert.That(root.Router.CurrentDialog, Is.Null);
                Assert.That(ActiveScreenRoots(root), Is.EqualTo(new[] { CoreScreen.GameOver }));
                Assert.That(
                    root.GameOver.RowCount, Is.EqualTo(root.CurrentGame.Standings.Count),
                    "the standings screen was shown the game that ended");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void HowToPlay_ReplacesTheSettingsDialog_AndLeavingComesBackToItOpen()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                TapSettings(root.Board);
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.Settings));

                Tap(root.Settings.HowToPlayButton);

                // A screen, not a second dialog: the settings panel is
                // replaced rather than covered, which is
                // shared-components.md#dialog's "a dialog never opens over
                // another dialog" for a screen opened from inside one.
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.HowToPlay));
                Assert.That(root.Router.CurrentDialog, Is.Null, "no dialog is left open underneath");
                Assert.That(ActiveScreenRoots(root), Is.EqualTo(new[] { CoreScreen.HowToPlay }));
                Assert.That(root.RootFor(Dialog.Settings).activeSelf, Is.False);
                Assert.That(root.HowToPlay.CurrentPage, Is.EqualTo(1), "entering always starts on page 1");

                // `Back` on page 1 leaves, and what it returns to is the
                // settings dialog — open, over the board — not the board.
                Tap(root.HowToPlay.BackButton);

                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.Settings));
                Assert.That(root.Settings.Dialog.IsOpen, Is.True, "open, exactly as it was");
                Assert.That(root.CurrentGame.IsOver, Is.False);
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void PagingThroughHowToPlayAndPressingDone_ChangesNothingAboutTheGame()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                var turnBefore = root.CurrentGame.ActiveFrog;
                var positionBefore = root.CurrentGame.LaneFor(turnBefore).Position;

                TapSettings(root.Board);
                Tap(root.Settings.HowToPlayButton);

                for (var page = 1; page < HowToPlayScreenView.HowToPlayPageCount; page++)
                {
                    Tap(root.HowToPlay.NextButton);
                }

                Assert.That(root.HowToPlay.NextButton.Label.text, Is.EqualTo("Done"));

                Tap(root.HowToPlay.NextButton);

                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.Router.CurrentDialog, Is.EqualTo(Dialog.Settings));

                // how-to-play.md's first invariant: "Opening it, paging
                // through it, and leaving it return the board exactly as it
                // was — same turn, same positions, same enabled `Roll`."
                Assert.That(root.CurrentGame.ActiveFrog, Is.EqualTo(turnBefore));
                Assert.That(root.CurrentGame.LaneFor(turnBefore).Position, Is.EqualTo(positionBefore));
                Assert.That(root.Board.RollButton.IsDisabled, Is.False);

                // And opening it again starts at page 1, not where the last
                // reader left it.
                Tap(root.Settings.HowToPlayButton);
                Assert.That(root.HowToPlay.CurrentPage, Is.EqualTo(1));
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void KeepPlaying_ReturnsToExactlyTheBoardThatWasThere()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                TapSettings(root.Board);
                Tap(root.Settings.EndGameButton);
                Tap(root.EndGameConfirm.KeepPlayingButton);

                Assert.That(root.Router.CurrentDialog, Is.Null);
                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.CurrentGame.IsOver, Is.False, "Keep playing never ends a game");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void PlayAgain_StartsAFreshGameForTheSameRoster_AndTheBoardShowsThatOne()
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);
                var first = root.CurrentGame;

                TapSettings(root.Board);
                Tap(root.Settings.EndGameButton);
                Tap(root.EndGameConfirm.EndTheGameButton);

                Tap(root.GameOver.PlayAgainButton);

                Assert.That(root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(root.CurrentGame, Is.Not.SameAs(first), "a new game, not the ended one");
                Assert.That(root.CurrentGame, Is.SameAs(root.GameOver.StartedGame));
                Assert.That(root.CurrentGame.TurnOrder, Is.EqualTo(first.TurnOrder), "the same frogs, no re-tapping");
                Assert.That(root.CurrentGame.IsOver, Is.False);
                Assert.That(
                    root.CurrentGame.LaneFor(FrogColour.Green).Position, Is.Zero,
                    "everyone back on their Start log");
            }
            finally
            {
                Destroy(root);
            }
        }

        [Test]
        public void HardwareBackOnTheBoard_OpensSettingsOnce_WhicheverOfItsTwoHandlersRunsFirst()
        {
            // One press of Android back reaches two Update methods:
            // ScreenRouterAdapter's, which runs the whole back-button table,
            // and GameBoardScreenView's, which raises its own
            // SettingsRequested. Unity does not define which runs first, so if
            // the composition root drove the dialog from both, one press would
            // open and immediately close the settings dialog on some builds
            // and not others.
            AssertBackOpensSettingsOnce(adapterFirst: true);
            AssertBackOpensSettingsOnce(adapterFirst: false);
        }

        static void AssertBackOpensSettingsOnce(bool adapterFirst)
        {
            var root = CreateRoot();

            try
            {
                StartAGame(root);

                if (adapterFirst)
                {
                    root.HandleBackButton();
                    root.Board.HandleHardwareBack();
                }
                else
                {
                    root.Board.HandleHardwareBack();
                    root.HandleBackButton();
                }

                Assert.That(
                    root.Router.CurrentDialog, Is.EqualTo(Dialog.Settings),
                    $"adapterFirst: {adapterFirst} — hardware back opens the settings dialog, exactly once");
                Assert.That(
                    root.Router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard),
                    "it does not quit, and it never quits without the confirm");
            }
            finally
            {
                Destroy(root);
            }
        }

        // --- Driving the app ---------------------------------------------------

        static AppRoot CreateRoot()
        {
            var root = AppRoot.Create();
            root.UseSeed(() => Seed);
            return root;
        }

        static void StartAGame(AppRoot root)
        {
            Tap(root.TitleScreen.NewButton);
            TapSeat(root.GameSetup, FrogColour.Green);
            TapSeat(root.GameSetup, FrogColour.Blue);
            Tap(root.GameSetup.StartButton);
        }

        // One turn, answered right, through the same four taps a player makes
        // — and the fifth that only appears on the turns that get a frog home
        // (#329). Returns the headline that arrival was announced with, or
        // null if the turn landed nobody home.
        static string PlayOneTurnCorrectly(AppRoot root, Game game)
        {
            Tap(root.Board.RollButton);
            var card = game.DrawnCard;

            Tap(root.RollAndCard.SolveItButton);

            TypeAnswer(root.WorkingOutGrid, card.Product);
            Tap(root.WorkingOutGrid.CheckItButton);

            Tap(root.AnswerResult.NextTurnButton);
            root.AnswerResult.Advance(LongEnough);

            if (root.Router.CurrentDialog != Dialog.PlayerWon)
            {
                return null;
            }

            var headline = root.PlayerWon.HeadlineText.text;
            Tap(root.PlayerWon.HandOnButton);
            return headline;
        }

        static void TypeAnswer(WorkingOutGridView view, int answer)
        {
            // The caret starts on the leftmost answer cell and walks right
            // (#305), so an answer is typed the way it is written and read:
            // most significant digit first, no tapping between digits.
            var digits = answer.ToString();

            foreach (var character in digits)
            {
                var digit = character - '0';
                Tap(view.Keys.Single(key => key.Kind == KeypadKeyKind.Digit && key.Digit == digit));
            }

            Assert.That(view.AnswerText, Is.EqualTo(digits), "the answer row spells out what was typed");
        }

        static void Tap(Button button)
        {
            var eventData = EventDataAt(button.RectTransform);

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static void Tap(WorkingOutKeypadKey key)
        {
            key.OnPointerClick(new PointerEventData(null));
        }

        static void TapSeat(GameSetupScreenView view, FrogColour colour)
        {
            var target = view.SeatTapTargetFor(colour);
            var eventData = EventDataAt(target.RectTransform);

            target.OnPointerDown(eventData);
            target.OnPointerUp(eventData);
        }

        static void TapSettings(GameBoardScreenView view)
        {
            var target = view.SettingsButton;
            var eventData = EventDataAt(target.RectTransform);

            target.OnPointerDown(eventData);
            target.OnPointerUp(eventData);
        }

        static PointerEventData EventDataAt(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };
        }

        // --- Assertions ---------------------------------------------------------

        static CoreScreen[] ActiveScreenRoots(AppRoot root)
        {
            return ((CoreScreen[])Enum.GetValues(typeof(CoreScreen)))
                .Where(screen => root.RootFor(screen).activeSelf)
                .ToArray();
        }

        static void AssertViewUnder<TView>(GameObject root) where TView : MonoBehaviour
        {
            var found = root.GetComponentsInChildren<TView>(includeInactive: true);

            Assert.That(
                found.Length, Is.EqualTo(1),
                $"{root.name} should hold exactly one {typeof(TView).Name}; nothing else puts one on screen.");

            Assert.That(
                found[0].transform.childCount, Is.GreaterThan(0),
                $"{typeof(TView).Name} was added but never asked to build itself, so its root is empty.");
        }

        static void AssertFillsItsParent(GameObject root, string name)
        {
            var rect = root.GetComponent<RectTransform>();

            Assert.That(
                rect, Is.Not.Null,
                $"{name}'s root has no RectTransform, so a screen parented to it is laid out against nothing.");

            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero), name);
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), name);
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero), name);
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero), name);
        }

        static void Destroy(AppRoot root)
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
