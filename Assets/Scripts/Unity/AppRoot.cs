using System;
using Frogs.Core;
using Frogs.Unity.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CoreScreen = Frogs.Core.Screen;

namespace Frogs.Unity
{
    /// <summary>
    /// The one thing that puts a screen on screen — issue #285.
    ///
    /// Every screen and dialog in the v0.2 proof of concept was built and
    /// tested before this existed, and none of them was ever constructed
    /// outside a test, so the installed app cleared the camera to its
    /// background colour and stopped. This type is the seam between "the game
    /// is built" and "the game runs": it makes the canvas, makes the views,
    /// hands each one the router and the <see cref="Game"/> it reads, and
    /// connects the events they already raise to the calls they already
    /// expect.
    ///
    /// **It decides nothing about the game.** There is no rule here: no
    /// grading, no movement, no turn order, no wording. Every branch below is
    /// either "which view is shown next" — which #213's <see cref="ScreenRouter"/>
    /// owns and this only reacts to — or a phase call Core's own
    /// <see cref="TurnPhase"/> machine requires in that order.
    ///
    /// **There is no scene edit, and deliberately so.** The entry point is
    /// <see cref="BootTheApp"/>, a <c>[RuntimeInitializeOnLoadMethod]</c> hook
    /// that does nothing but call <see cref="Create"/>.
    /// <c>Assets/Scenes/Game.unity</c> stays exactly what #209 committed — one
    /// camera, no MonoBehaviour — because a component reference in a scene is a
    /// GUID that turns into a silent *Missing Script* if a <c>.meta</c> drifts,
    /// and because hand-authoring scene YAML is what
    /// docs/engineering/unity-serialization.md exists to forbid. Nothing on
    /// this type is serialized: it is built at runtime, saved into no asset,
    /// and every field below is deliberately not a <c>[SerializeField]</c>.
    ///
    /// **Every view is asked to build itself here, up front.** Unity does not
    /// run <c>Awake</c> on a child of an inactive object, and eight of the nine
    /// roots are inactive at boot, so each view's own <c>EnsureInitialized</c>
    /// guard is prodded through its public surface rather than left to
    /// lifecycle timing — the same reasoning the views themselves record.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class AppRoot : MonoBehaviour
    {
        /// <summary>
        /// The canvas every screen and every shared component is measured in —
        /// docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in
        /// and docs/engineering/tech-stack.md#target-platform. The
        /// <see cref="CanvasScaler"/> is set to exactly this, so a constant on
        /// a spec page is the constant the code lays out with.
        /// </summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>The other half of the reference resolution: 1920 x 1200, 16:10, landscape.</summary>
        public const float ReferenceHeight = 1200f;

        /// <summary>What the running app's one root object is called in the hierarchy.</summary>
        public const string AppRootObjectName = "Multiplying Frogs";

        const string EventSystemObjectName = "EventSystem";
        const string ScreensObjectName = "Screens";

        RectTransform _rect;
        Canvas _canvas;
        CanvasScaler _scaler;
        EventSystem _eventSystem;
        ScreenRouterAdapter _screens;
        ScreenRouter _router;

        TitleScreenView _titleScreen;
        GameSetupScreenView _gameSetup;
        GameBoardScreenView _board;
        GameOverScreenView _gameOver;
        RollAndCardDialogView _rollAndCard;
        WorkingOutGridView _workingOutGrid;
        AnswerResultDialogView _answerResult;
        SettingsDialogView _settings;
        EndGameConfirmView _endGameConfirm;

        Game _game;
        GameWorkingOutTurn _turn;

        CoreScreen _shownScreen;
        Dialog? _shownDialog;

        bool _initialized;

        /// <summary>The Core router every screen navigates through.</summary>
        public ScreenRouter Router
        {
            get
            {
                Initialize();
                return _router;
            }
        }

        /// <summary>The one canvas, in screen-space overlay.</summary>
        public Canvas Canvas
        {
            get
            {
                Initialize();
                return _canvas;
            }
        }

        /// <summary>The scaler holding the 1920 x 1200 reference resolution.</summary>
        public CanvasScaler CanvasScaler
        {
            get
            {
                Initialize();
                return _scaler;
            }
        }

        /// <summary>The event system, without which no tap reaches any button.</summary>
        public EventSystem EventSystem
        {
            get
            {
                Initialize();
                return _eventSystem;
            }
        }

        /// <summary>The [title screen](docs/specs/ui/title-screen.md) — the screen the app opens on.</summary>
        public TitleScreenView TitleScreen
        {
            get
            {
                Initialize();
                return _titleScreen;
            }
        }

        /// <summary>[Game setup](docs/specs/ui/game-setup.md).</summary>
        public GameSetupScreenView GameSetup
        {
            get
            {
                Initialize();
                return _gameSetup;
            }
        }

        /// <summary>[The game board](docs/specs/ui/game-board.md).</summary>
        public GameBoardScreenView Board
        {
            get
            {
                Initialize();
                return _board;
            }
        }

        /// <summary>[Game over](docs/specs/ui/game-over.md).</summary>
        public GameOverScreenView GameOver
        {
            get
            {
                Initialize();
                return _gameOver;
            }
        }

        /// <summary>[Roll and card](docs/specs/ui/roll-and-card.md).</summary>
        public RollAndCardDialogView RollAndCard
        {
            get
            {
                Initialize();
                return _rollAndCard;
            }
        }

        /// <summary>[The working-out grid](docs/specs/ui/working-out-grid.md).</summary>
        public WorkingOutGridView WorkingOutGrid
        {
            get
            {
                Initialize();
                return _workingOutGrid;
            }
        }

        /// <summary>[Answer result](docs/specs/ui/answer-result.md).</summary>
        public AnswerResultDialogView AnswerResult
        {
            get
            {
                Initialize();
                return _answerResult;
            }
        }

        /// <summary>[The settings dialog](docs/specs/ui/settings-dialog.md).</summary>
        public SettingsDialogView Settings
        {
            get
            {
                Initialize();
                return _settings;
            }
        }

        /// <summary>[The end-game confirm](docs/specs/ui/end-game-confirm.md).</summary>
        public EndGameConfirmView EndGameConfirm
        {
            get
            {
                Initialize();
                return _endGameConfirm;
            }
        }

        /// <summary>
        /// The <see cref="Game"/> this session is playing, or null before the
        /// first one is started. One instance is held for as long as it is
        /// being played, so the board, every dialog and the standings all read
        /// the same game rather than each asking a different copy.
        /// </summary>
        public Game CurrentGame
        {
            get { return _game; }
        }

        /// <summary>
        /// Builds the whole running app and returns its root. The app's one
        /// object: the canvas, the event system, the router's screen roots, and
        /// a view under each of them.
        /// </summary>
        public static AppRoot Create()
        {
            // The canvas parts are named at construction rather than left to
            // [RequireComponent], so the Transform is replaced by a
            // RectTransform the same explicit way every view in this assembly
            // does it.
            var host = new GameObject(
                AppRootObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var root = host.AddComponent<AppRoot>();

            // Awake has already run this in a player; in an edit-mode session
            // it has not run at all. The guard inside makes the second call
            // free either way.
            root.Initialize();

            return root;
        }

        /// <summary>
        /// Where a brand-new <see cref="Game"/>'s seed comes from. Defaults to
        /// the source <c>GameSetupScreenView</c> and <c>GameOverScreenView</c>
        /// each default to — the Unity layer is where real-world entropy enters
        /// the system, and <c>Frogs.Core</c> still never reads a clock. Handing
        /// in a fixed source is how a test gets the same deal twice.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="seedFactory"/> is null.</exception>
        public void UseSeed(Func<ulong> seedFactory)
        {
            Initialize();

            if (seedFactory == null)
            {
                throw new ArgumentNullException(nameof(seedFactory));
            }

            // Both screens that start a game read it, and both take it the same
            // way — see GameSetupScreenView.Initialize.
            _gameSetup.Initialize(_router, seedFactory);
            _gameOver.Initialize(_router, seedFactory);
        }

        /// <summary>
        /// The root <c>GameObject</c> a screen's view lives under — active only
        /// while that screen is current.
        /// </summary>
        public GameObject RootFor(CoreScreen screen)
        {
            Initialize();
            return _screens.RootFor(screen);
        }

        /// <summary>The root <c>GameObject</c> a dialog's view lives under.</summary>
        public GameObject RootFor(Dialog dialog)
        {
            Initialize();
            return _screens.RootFor(dialog);
        }

        /// <summary>
        /// What the hardware back key does, for every screen and every dialog:
        /// #213's back-button table, run by the router adapter. A public method
        /// of its own so an EditMode test can press back without simulating a
        /// key.
        /// </summary>
        public void HandleBackButton()
        {
            Initialize();
            _screens.HandleBackButton();
        }

        /// <summary>
        /// Builds and wires everything. Idempotent, and every public entry
        /// point above funnels through it, for the reason every view in this
        /// assembly records: Unity does not guarantee <c>Awake</c> has run
        /// before something else reaches this component, and in an edit-mode
        /// session it never runs at all.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildCanvas();
            BuildEventSystem();
            BuildScreenRoots();
            BuildViews();
            Wire();
        }

        void Awake()
        {
            Initialize();
        }

        void Update()
        {
            if (!_initialized)
            {
                return;
            }

            AdvanceCurrentDialogsFade(Time.deltaTime);
        }

        // The app's entry point, and the whole of it. Everything it could
        // usefully say is in Initialize, which an EditMode test can call;
        // a lifecycle hook is not testable without a player, so nothing lives
        // in here that would benefit from being.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootTheApp()
        {
            Create();
        }

        // --- Building ----------------------------------------------------------

        void BuildCanvas()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            // Overlay, so the canvas needs no camera of its own and draws over
            // whatever the scene's camera cleared to.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null)
            {
                _scaler = gameObject.AddComponent<CanvasScaler>();
            }

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

            // Expand: the canvas is never smaller than the reference in either
            // direction, so nothing designed at 1920 x 1200 is ever cropped off
            // the edge of a device that is not exactly 16:10. On the target
            // tablet, which is, every match mode gives the same result — this
            // one is chosen for what it does everywhere else.
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        void BuildEventSystem()
        {
            var host = new GameObject(
                EventSystemObjectName,
                typeof(EventSystem),
                typeof(StandaloneInputModule));

            host.transform.SetParent(transform, worldPositionStays: false);

            // StandaloneInputModule is the module for the legacy input manager,
            // which is the one this project uses: Packages/manifest.json has no
            // com.unity.inputsystem, and ScreenRouterAdapter reads hardware back
            // through UnityEngine.Input.
            _eventSystem = host.GetComponent<EventSystem>();
        }

        void BuildScreenRoots()
        {
            var host = new GameObject(ScreensObjectName, typeof(RectTransform));
            var rect = (RectTransform)host.transform;

            rect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(rect);

            _screens = host.AddComponent<ScreenRouterAdapter>();
            _router = _screens.Router;

            _shownScreen = _router.CurrentScreen;
            _shownDialog = _router.CurrentDialog;
        }

        void BuildViews()
        {
            _titleScreen = AddView<TitleScreenView>(RootFor(CoreScreen.TitleScreen));
            _gameSetup = AddView<GameSetupScreenView>(RootFor(CoreScreen.GameSetup));
            _board = AddView<GameBoardScreenView>(RootFor(CoreScreen.GameBoard));
            _gameOver = AddView<GameOverScreenView>(RootFor(CoreScreen.GameOver));

            _rollAndCard = AddView<RollAndCardDialogView>(RootFor(Dialog.RollAndCard));
            _workingOutGrid = AddView<WorkingOutGridView>(RootFor(Dialog.WorkingOutGrid));
            _answerResult = AddView<AnswerResultDialogView>(RootFor(Dialog.AnswerResult));
            _settings = AddView<SettingsDialogView>(RootFor(Dialog.Settings));
            _endGameConfirm = AddView<EndGameConfirmView>(RootFor(Dialog.EndGameConfirm));

            // Ask each one to build itself now, rather than at whatever moment
            // its root first becomes active. Every view exposes its own
            // RectTransform through its EnsureInitialized guard, which is the
            // published way to say "build".
            PrimeView(_titleScreen.RectTransform);
            PrimeView(_gameSetup.RectTransform);
            PrimeView(_board.RectTransform);
            PrimeView(_gameOver.RectTransform);
            PrimeView(_rollAndCard.RectTransform);
            PrimeView(_workingOutGrid.RectTransform);
            PrimeView(_answerResult.RectTransform);
            PrimeView(_settings.RectTransform);
            PrimeView(_endGameConfirm.RectTransform);
        }

        static TView AddView<TView>(GameObject root) where TView : MonoBehaviour
        {
            var host = new GameObject(typeof(TView).Name, typeof(RectTransform));
            var rect = (RectTransform)host.transform;

            rect.SetParent(root.transform, worldPositionStays: false);
            rect.anchoredPosition = Vector2.zero;

            return host.AddComponent<TView>();
        }

        // Reading a view's own rect is what makes it build; there is nothing to
        // do with the answer.
        static void PrimeView(RectTransform rect)
        {
            if (rect == null)
            {
                throw new InvalidOperationException(
                    "a screen was added but has no RectTransform of its own, so it never built itself.");
            }
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // --- Wiring --------------------------------------------------------------

        void Wire()
        {
            _titleScreen.Initialize(_router, new NoSavedGameQuery());
            _gameSetup.Initialize(_router);
            _gameOver.Initialize(_router);

            // The gear, and only the gear. The board raises SettingsRequested
            // from its own hardware-back handler too, and ScreenRouterAdapter
            // already runs the back-button table for the same key press — so
            // driving the dialog from both would open and immediately close it,
            // in whichever order Unity happened to run the two Updates.
            // docs/specs/ui/game-board.md's rule is honoured once, by the
            // router.
            _board.SettingsButton.Clicked += OpenSettings;
            _board.RollPressed += StartTurn;

            _rollAndCard.SolveItPressed += OpenWorkingOutGrid;
            _workingOutGrid.AnswerSubmitted += ShowAnswerResult;
            _answerResult.TurnHandedOff += HandOffFinished;

            _settings.CloseRequested += CloseDialog;
            _settings.EndGameConfirmRequested += OpenEndGameConfirm;

            _endGameConfirm.GameEnded += ShowGameOver;
            _endGameConfirm.KeepPlayingRequested += CloseDialog;

            _router.StateChanged += RouterStateChanged;
        }

        // --- Reacting to the router ----------------------------------------------

        void RouterStateChanged()
        {
            var screen = _router.CurrentScreen;
            if (screen != _shownScreen)
            {
                _shownScreen = screen;
                EnterScreen(screen);
            }

            var dialog = _router.CurrentDialog;
            if (dialog != _shownDialog)
            {
                _shownDialog = dialog;

                if (dialog.HasValue)
                {
                    EnterDialog(dialog.Value);
                }
            }
        }

        void EnterScreen(CoreScreen screen)
        {
            switch (screen)
            {
                case CoreScreen.GameSetup:
                    // docs/specs/ui/game-setup.md#behaviour: "Entering: seats
                    // all empty, every time."
                    _gameSetup.ResetToEmptySeats();
                    break;

                case CoreScreen.GameBoard:
                    AdoptNewlyStartedGame();
                    break;
            }
        }

        void EnterDialog(Dialog dialog)
        {
            switch (dialog)
            {
                // These two are opened by the router — from the gear, from
                // `End the game`, or from hardware back — rather than by a
                // handler that also has something to hand them, so this is
                // where their panels are told to open.
                case Dialog.Settings:
                    _settings.Open();
                    break;

                case Dialog.EndGameConfirm:
                    _endGameConfirm.Open();
                    break;
            }
        }

        // `Start` on game setup and `Play again` on game over each construct a
        // Game and then navigate here. Whichever of them holds one this session
        // has not adopted yet is the game that was just started.
        void AdoptNewlyStartedGame()
        {
            var started = NewlyStartedGame();

            if (started == null)
            {
                return;
            }

            _game = started;
            _board.Initialize(_game);
            _endGameConfirm.Initialize(_game);
        }

        Game NewlyStartedGame()
        {
            if (_gameSetup.StartedGame != null && !ReferenceEquals(_gameSetup.StartedGame, _game))
            {
                return _gameSetup.StartedGame;
            }

            if (_gameOver.StartedGame != null && !ReferenceEquals(_gameOver.StartedGame, _game))
            {
                return _gameOver.StartedGame;
            }

            return null;
        }

        // --- The turn ------------------------------------------------------------

        // `Roll` — the only way to start a turn. Core draws the roll and the
        // card; the dialog is pointed at what it drew and then shown.
        void StartTurn()
        {
            _game.RollDie();

            _rollAndCard.Initialize(new GameRollAndCardReadout(_game), _router);
            _router.OpenDialog(Dialog.RollAndCard);
        }

        // `Solve it`. The dialog has already asked the router for the grid by
        // the time this runs, so the grid is pointed at the turn in the same
        // frame it becomes current.
        void OpenWorkingOutGrid()
        {
            _game.BeginAnswering();

            _turn = new GameWorkingOutTurn(_game);
            _workingOutGrid.Initialize(_turn, _router);
        }

        // `Check it`. The submitted answer has already gone out through the
        // grid's one seam and been graded by Core by the time this fires —
        // which is why the number it carries is not used here; the dialog is
        // pointed at Core's verdict, at the board the frog will hop on, and at
        // the router it closes itself through.
        void ShowAnswerResult(int submittedAnswer)
        {
            _answerResult.Initialize(new GameAnswerResultTurn(_game, _turn.Resolution), _board, _router);
        }

        // The frog has landed and the next player's turn has begun. The only
        // thing left is the ending docs/specs/ui/game-board.md describes: "When
        // the last frog gets home, the game ends itself... with no input from
        // anybody."
        void HandOffFinished()
        {
            if (_game.IsOver)
            {
                ShowGameOver();
            }
        }

        // --- Settings, and the two ways a game ends -------------------------------

        void OpenSettings()
        {
            _router.OpenDialog(Dialog.Settings);
        }

        void OpenEndGameConfirm()
        {
            _router.OpenDialog(Dialog.EndGameConfirm);
        }

        void CloseDialog()
        {
            _router.CloseDialog();
        }

        void ShowGameOver()
        {
            _gameOver.Show(_game);
            _router.GameHasEnded();
        }

        // --- The clock ------------------------------------------------------------

        // Three of the five dialogs have no Update of their own, so their
        // shared Dialog's cross-fade in has nothing advancing it and they would
        // open at zero opacity and stay there. The other two run their own
        // entering and hand-off sequences from their own Update and must not be
        // advanced twice.
        void AdvanceCurrentDialogsFade(float deltaSeconds)
        {
            var dialog = _router.CurrentDialog;

            if (!dialog.HasValue)
            {
                return;
            }

            switch (dialog.Value)
            {
                case Dialog.WorkingOutGrid:
                    _workingOutGrid.Dialog.AdvanceFade(deltaSeconds);
                    break;

                case Dialog.Settings:
                    _settings.Dialog.AdvanceFade(deltaSeconds);
                    break;

                case Dialog.EndGameConfirm:
                    _endGameConfirm.Dialog.AdvanceFade(deltaSeconds);
                    break;
            }
        }
    }
}
