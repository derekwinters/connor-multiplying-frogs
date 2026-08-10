using System;

namespace Frogs.Core
{
    /// <summary>
    /// The traffic cop — issue #213. Owns which one of four
    /// <see cref="Screen"/>s is current, which one of five <see cref="Dialog"/>s
    /// (if any) is layered over it, and what the hardware back button does in
    /// every state.
    ///
    /// This is the one place the rule in docs/specs/ui/shared-components.md#dialog
    /// ("a dialog never opens over another dialog") is enforced rather than
    /// hoped for: <see cref="OpenDialog"/> always closes whatever dialog is
    /// current before the requested one becomes current, and every screen
    /// transition clears the dialog layer, so this type can never be in a
    /// state with two dialogs — or a screen and an unrelated dialog — both
    /// reading "open".
    ///
    /// It draws nothing. It activates and deactivates no <c>GameObject</c> —
    /// that is <c>Frogs.Unity</c>'s thin adapter, reading <see cref="CurrentScreen"/>
    /// and <see cref="CurrentDialog"/> off this type and nothing else.
    ///
    /// It does not decide *whether* a game has ended — that is
    /// <c>Game.IsOver</c> (#211). <see cref="GameHasEnded"/> is the stand-in
    /// this issue builds against for that signal; wiring it to the real
    /// <c>Game</c> is future work.
    /// </summary>
    public sealed class ScreenRouter
    {
        /// <summary>
        /// Fires after every change to <see cref="CurrentScreen"/>,
        /// <see cref="CurrentDialog"/>, or <see cref="AppExitRequested"/> —
        /// the one hook the Unity adapter needs to know when to refresh which
        /// root <c>GameObject</c> is active. A dialog swap fires it twice:
        /// once as the old dialog closes (<see cref="CurrentDialog"/> null),
        /// once as the new one opens — never once for both at once, because
        /// this type never holds two dialogs open together.
        /// </summary>
        public event Action StateChanged;

        /// <summary>The screen currently on top. Starts at <see cref="Screen.TitleScreen"/>.</summary>
        public Screen CurrentScreen { get; private set; }

        /// <summary>
        /// The dialog currently layered over <see cref="CurrentScreen"/>, or
        /// null if none is open. At most one dialog is ever current.
        /// </summary>
        public Dialog? CurrentDialog { get; private set; }

        /// <summary>
        /// Set once hardware back is pressed on <see cref="Screen.TitleScreen"/> —
        /// docs/specs/ui/title-screen.md#behaviour: "the only screen where back
        /// exits." The Unity adapter reads this to call <c>Application.Quit()</c>;
        /// this type performs no engine call itself.
        /// </summary>
        public bool AppExitRequested { get; private set; }

        public ScreenRouter()
        {
            CurrentScreen = Screen.TitleScreen;
            CurrentDialog = null;
        }

        /// <summary>
        /// Moves to <paramref name="screen"/> and closes whatever dialog was
        /// open — every screen-to-screen edge in the navigation graph leaves
        /// no dialog behind.
        /// </summary>
        public void NavigateToScreen(Screen screen)
        {
            CurrentScreen = screen;
            CurrentDialog = null;
            RaiseStateChanged();
        }

        /// <summary>
        /// Opens <paramref name="dialog"/> over <see cref="CurrentScreen"/>.
        /// If another dialog is already open, it is closed first — a second
        /// <see cref="StateChanged"/> notification with <see cref="CurrentDialog"/>
        /// null, then a third with the new dialog — so the dialog layer never
        /// holds two dialogs open at once, per
        /// docs/specs/ui/shared-components.md#dialog: "Stacked... does not
        /// happen."
        /// </summary>
        public void OpenDialog(Dialog dialog)
        {
            if (CurrentDialog.HasValue)
            {
                CloseDialog();
            }

            CurrentDialog = dialog;
            RaiseStateChanged();
        }

        /// <summary>Closes whatever dialog is open. A no-op if none is.</summary>
        public void CloseDialog()
        {
            if (!CurrentDialog.HasValue)
            {
                return;
            }

            CurrentDialog = null;
            RaiseStateChanged();
        }

        /// <summary>
        /// The stand-in for <c>core-game-end</c>'s "the game has ended"
        /// signal (#211's <c>Game.IsOver</c>) — moves straight to
        /// <see cref="Screen.GameOver"/> with no dialog open and no
        /// back-button interaction involved. Wiring this to the real signal
        /// is future work; this type has no opinion on when to call it.
        /// </summary>
        public void GameHasEnded()
        {
            NavigateToScreen(Screen.GameOver);
        }

        /// <summary>
        /// What the hardware back button does, read off the back-button
        /// table in issue #213 — collected from each screen and dialog
        /// page's own `## Behaviour` section rather than re-derived here.
        /// </summary>
        public void HandleBack()
        {
            if (CurrentDialog.HasValue)
            {
                HandleBackWithDialogOpen(CurrentDialog.Value);
                return;
            }

            HandleBackOnScreen(CurrentScreen);
        }

        void HandleBackWithDialogOpen(Dialog dialog)
        {
            switch (dialog)
            {
                // settings-dialog.md#behaviour: "does what `Back to the
                // game` does" — the least destructive of its two buttons.
                case Dialog.Settings:
                // end-game-confirm.md#behaviour: "does what `Keep playing`
                // does" — the least destructive of its two buttons.
                case Dialog.EndGameConfirm:
                    CloseDialog();
                    break;

                // roll-and-card.md, working-out-grid.md, answer-result.md
                // #behaviour: "Hardware back does nothing." Genuinely inert —
                // not a close, because none of the three has anything to
                // dismiss back to without a cost the page itself rules out.
                case Dialog.RollAndCard:
                case Dialog.WorkingOutGrid:
                case Dialog.AnswerResult:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(dialog), dialog, "unhandled dialog.");
            }
        }

        void HandleBackOnScreen(Screen screen)
        {
            switch (screen)
            {
                // title-screen.md#behaviour: "exits the app. The only screen
                // where back exits."
                case Screen.TitleScreen:
                    AppExitRequested = true;
                    RaiseStateChanged();
                    break;

                // game-setup.md#behaviour: "does what `Back` does" —
                // returns to the title screen.
                case Screen.GameSetup:
                // game-over.md#behaviour: "does what `Back to the title`
                // does."
                case Screen.GameOver:
                    NavigateToScreen(Screen.TitleScreen);
                    break;

                // game-board.md#behaviour: "opens the settings dialog. It
                // does not quit, and it never quits without the confirm."
                case Screen.GameBoard:
                    OpenDialog(Dialog.Settings);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), screen, "unhandled screen.");
            }
        }

        void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
