using System;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// SettingsDialogView.cs and GameBoardScreenView.cs work around — so these are
// pulled in by explicit alias rather than a wildcard `using Frogs.Unity.UI;`,
// and a bare `Button`, `ButtonKind` or `DialogPanel` in this file always means
// the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The end-game confirm — docs/specs/ui/end-game-confirm.md, built to that
    /// page and its committed 1:1 mockup. The one dialog in the game that can
    /// take something away from somebody who did nothing wrong: the only door
    /// out of a game in progress, reached only from
    /// <see cref="SettingsDialogView"/> (#222).
    ///
    /// It is a composition of the shared <see cref="DialogPanel"/> (#219) and
    /// the shared <see cref="Button"/> (#214) and nothing else: no new visual
    /// component, no sprite, no bitmap.
    ///
    /// Three things worth knowing before reading the rest:
    ///
    /// - **The cost sentence is the whole of the protection.** Anyone may end
    ///   a game, on anyone's turn — Derek's recorded call in issue #186,
    ///   because the tablet cannot tell who is holding it — so nothing here
    ///   restricts who can tap. What stands in for that restriction is a
    ///   sentence that says something true and specific every time, which is
    ///   why <see cref="FormatCostSentence"/> takes *two* live numbers and
    ///   hard-codes neither.
    /// - **Ending is not losing.** <c>Game.EndGame()</c> (#211) marks the game
    ///   over and leaves every frog's <c>Lane</c> exactly where it stands;
    ///   nothing in this view resets, rewinds or rescores anything, and there
    ///   is no member on it that could.
    /// - **It does not listen for hardware back.** The router (#213) already
    ///   routes back on <c>Dialog.EndGameConfirm</c> to what `Keep playing`
    ///   does; this view exposes that one action as
    ///   <see cref="RequestKeepPlaying"/> rather than adding a second opinion
    ///   about the key. On this dialog in particular, a second opinion is the
    ///   one that could end somebody's game by accident.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class EndGameConfirmView : MonoBehaviour
    {
        // docs/specs/ui/end-game-confirm.md#named-constants.
        public const float ConfirmDialogWidth = 1160f;
        public const float ConfirmDialogHeight = 540f;
        public const float ConfirmQuestionSize = 56f;
        public const float ConfirmBodySize = 40f;
        public const float ConfirmBodyWidth = 1048f;
        public const float ConfirmBodyLineHeight = 1.4f;

        // The rest of what this dialog measures itself with is other pages'
        // rows, referenced under the identical name rather than redeclared
        // here — the same line #220 and #222 both drew:
        //
        // - the panel's padding, corner radius, title metrics, the
        //   question -> cost gap (`DialogTitleGap`), the cost -> controls gap
        //   (`DialogButtonRowGap`), the scrim and the fade are the shared
        //   Dialog's (shared-components.md#dialog).
        // - `ButtonDestructiveGap`, `ButtonHeight` and `ButtonMinWidth` are
        //   the shared Button's (shared-components.md#button).

        const string QuestionLabel = "End the game for everyone?";
        const string EndTheGameLabel = "End the game";
        const string KeepPlayingLabel = "Keep playing";

        // end-game-confirm.md's cost table, as the two templates it is. The
        // singular row is its own sentence rather than a plural with a "1" in
        // it, because "1 frogs are still swimming" is the kind of thing that
        // makes a warning easy to stop reading.
        const string SwimmingClauseSingular = "One frog is still swimming.";
        const string SwimmingClausePluralFormat = "{0} frogs are still swimming.";
        const string CostSentenceFormat =
            "{0} Ending it now stops the game for all {1} players and shows the results.";

        // How many frogs are still swimming, capitalised because it opens the
        // sentence. Index 0 is deliberately absent: there is no
        // everybody-is-home wording, and there does not need to be — the game
        // ends itself the moment the last frog lands on its End log, so this
        // dialog can never open on zero.
        static readonly string[] SwimmingCountWords = { null, "One", "Two", "Three", "Four" };

        // How many frogs are in the game, lower-case because it sits mid
        // sentence. Only Game.MinFrogsPerGame..Game.MaxFrogsPerGame are ever
        // reachable; the shorter entries exist so the array indexes by count.
        static readonly string[] RosterSizeWords = { null, "one", "two", "three", "four" };

        // No imported font — matches Button.cs's and DialogPanel.cs's own
        // choice, for the same reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colour copied verbatim from the committed mockup's CSS custom
        // properties, the same line Button.cs, DialogPanel.cs and
        // SettingsDialogView.cs all draw: not a geometry constant on any spec
        // page's table, so not declared as a named spec constant.
        static readonly Color CostColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockup's --line

        Game _game;

        RectTransform _rect;
        DialogPanel _dialog;
        Text _costText;
        Button _endTheGameButton;
        Button _keepPlayingButton;

        bool _initialized;

        /// <summary>
        /// The game has been ended: <c>Game.EndGame()</c> has already run, so
        /// the standings behind this are final. Whoever is listening drives
        /// the router's `[End-game confirm] --End the game--&gt; Game over`
        /// transition (#213) — this view moves no screens itself.
        /// </summary>
        public event Action GameEnded;

        /// <summary>
        /// `Keep playing` was pressed, or hardware back arrived through
        /// <see cref="RequestKeepPlaying"/>. Whoever is listening drives the
        /// router's `[End-game confirm] --Keep playing--&gt; Game board`
        /// transition (#213), which returns to the exact board state — same
        /// turn, same positions — which is free, because nothing on this path
        /// touched it.
        /// </summary>
        public event Action KeepPlayingRequested;

        /// <summary>The view's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this confirm is laid out on, at <see cref="ConfirmDialogWidth"/> x <see cref="ConfirmDialogHeight"/>.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>
        /// `question` — `End the game for everyone?`. This is the shared
        /// Dialog's own title slot rather than a second text of this
        /// dialog's: the mockup draws the question at exactly the title's
        /// place, size and weight, and `ConfirmQuestionSize` and
        /// `DialogTitleSize` are the same 56 px.
        /// </summary>
        public Text QuestionText
        {
            get
            {
                EnsureInitialized();
                return _dialog.TitleText;
            }
        }

        /// <summary>`cost` — the live sentence from <see cref="FormatCostSentence"/>, <see cref="ConfirmBodyWidth"/> wide.</summary>
        public Text CostText
        {
            get
            {
                EnsureInitialized();
                return _costText;
            }
        }

        /// <summary>`controls` — the shared Dialog's own button row, bottom of the panel.</summary>
        public RectTransform ControlsRect
        {
            get
            {
                EnsureInitialized();
                return _dialog.ButtonRowRect;
            }
        }

        /// <summary>`End the game` — destructive, on the left edge of the padding box.</summary>
        public Button EndTheGameButton
        {
            get
            {
                EnsureInitialized();
                return _endTheGameButton;
            }
        }

        /// <summary>`Keep playing` — primary, on the right edge of the padding box, where the thumb is.</summary>
        public Button KeepPlayingButton
        {
            get
            {
                EnsureInitialized();
                return _keepPlayingButton;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Points the confirm at the game it is asking about. Both numbers in
        /// the cost sentence are read from here and nowhere else — this view
        /// walks no frog positions and counts no seats.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
        public void Initialize(Game game)
        {
            EnsureInitialized();

            _game = game ?? throw new ArgumentNullException(nameof(game));

            Refresh();
        }

        /// <summary>
        /// Re-reads the game and rebuilds the cost sentence. Every value it
        /// draws is asked for again — a frog that got home since the last look
        /// shows up simply by asking.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_game == null)
            {
                return;
            }

            _costText.text = FormatCostSentence(_game.FrogsStillSwimming, _game.TurnOrder.Count);
        }

        /// <summary>Opens the confirm — the shared Dialog's own cross-fade in, over a freshly read cost sentence.</summary>
        public void Open()
        {
            EnsureInitialized();

            Refresh();
            _dialog.Open();
        }

        /// <summary>Closes the confirm — the shared Dialog's own cross-fade out.</summary>
        public void Close()
        {
            EnsureInitialized();
            _dialog.Close();
        }

        /// <summary>
        /// The one action `Keep playing` and hardware back both invoke —
        /// shared-components.md#dialog: "the hardware back button does what
        /// the dialog's least destructive button does, and never what its most
        /// destructive one does", and end-game-confirm.md: "back never ends a
        /// game." Public so the router's back handling (#213) can call it
        /// directly, which is also how an EditMode test simulates hardware
        /// back. There is deliberately no second key handler here.
        /// </summary>
        public void RequestKeepPlaying()
        {
            EnsureInitialized();

            var handler = KeepPlayingRequested;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>
        /// end-game-confirm.md's cost table, rendered. Both numbers are handed
        /// in — this method counts nothing and looks nothing up, the same way
        /// <see cref="SettingsDialogView.FormatVersionLabel"/> parses a string
        /// it is given rather than reading a live one — so it can be asserted
        /// against every reachable pair without a game to build first.
        ///
        /// Both are spelled as words: the swimming count capitalised because
        /// it opens the sentence, the roster size lower-case because it sits
        /// mid sentence. Neither is ever a digit.
        /// </summary>
        /// <param name="frogsStillSwimming">
        /// <c>Game.FrogsStillSwimming</c> — at least one, always, because the
        /// game ends itself the moment the last frog is home.
        /// </param>
        /// <param name="rosterSize">
        /// How many frogs are playing — <c>Game.TurnOrder.Count</c>, which is
        /// <c>Game.MinFrogsPerGame</c> to <c>Game.MaxFrogsPerGame</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="rosterSize"/> is a roster no game can have, or
        /// <paramref name="frogsStillSwimming"/> is not between one and it.
        /// Zero throws on purpose: there is no everybody-is-home wording to
        /// return, so the absence is structural rather than a comment.
        /// </exception>
        public static string FormatCostSentence(int frogsStillSwimming, int rosterSize)
        {
            if (rosterSize < Game.MinFrogsPerGame || rosterSize > Game.MaxFrogsPerGame)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rosterSize),
                    rosterSize,
                    $"a game is {Game.MinFrogsPerGame} to {Game.MaxFrogsPerGame} frogs; {rosterSize} is neither.");
            }

            if (frogsStillSwimming < 1 || frogsStillSwimming > rosterSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frogsStillSwimming),
                    frogsStillSwimming,
                    "this dialog only ever opens with between one frog and the whole roster still swimming.");
            }

            var swimmingClause = frogsStillSwimming == 1
                ? SwimmingClauseSingular
                : string.Format(SwimmingClausePluralFormat, SwimmingCountWords[frogsStillSwimming]);

            return string.Format(CostSentenceFormat, swimmingClause, RosterSizeWords[rosterSize]);
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and SettingsDialogView's own
        // EnsureInitialized. Every public entry point funnels through this
        // idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            // The whole canvas, not the 1920 x 1200 reference rectangle. This
            // dialog paints no background of its own — what it lays over the
            // screen is the shared Dialog's scrim, and that has to reach the
            // edges on a device that is not 16:10. The panel underneath is
            // centre-anchored, so it does not move.
            StretchToFill(_rect);

            BuildDialog();
            BuildCost();
            BuildControls();
        }

        void BuildDialog()
        {
            var dialogGO = new GameObject("EndGameConfirmDialog", typeof(RectTransform));
            dialogGO.transform.SetParent(_rect, worldPositionStays: false);

            _dialog = dialogGO.AddComponent<DialogPanel>();

            // The `question` region. It is the shared Dialog's title, not a
            // text of this dialog's own — see QuestionText.
            _dialog.SetTitle(QuestionLabel);

            // The shared Dialog at this dialog's own size, in place of
            // DialogMaxWidth/DialogMaxHeight. Everything else about it — the
            // scrim, the corners, the padding, the title metrics, the
            // cross-fade — is inherited unchanged.
            _dialog.SetSize(ConfirmDialogWidth, ConfirmDialogHeight);
        }

        void BuildCost()
        {
            // The shared Dialog's body region already starts one
            // DialogTitleGap under the title and stops one DialogButtonRowGap
            // above the button row, so the two gaps this dialog needs are the
            // shared component's own and no new spacing constant is declared.
            //
            // The mockup draws the cost 170 px down from the panel top rather
            // than the 152 px that DialogPadding + ConfirmQuestionSize +
            // DialogTitleGap comes to; the 18 px is the leading a browser puts
            // under a 56 px line and not a spacing value of its own, which is
            // why this composes the named gaps rather than the drawn offset.
            var costGO = new GameObject("Cost", typeof(RectTransform), typeof(Text));
            _costText = costGO.GetComponent<Text>();
            _costText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _costText.fontSize = (int)ConfirmBodySize;
            _costText.lineSpacing = ConfirmBodyLineHeight;
            _costText.alignment = TextAnchor.UpperLeft;
            _costText.color = CostColor;
            // Wrapping horizontally is the point of ConfirmBodyWidth — the
            // sentence is two lines at 40 px. Vertically it overflows rather
            // than truncating: a warning with its ending cut off is worse than
            // a warning that runs long.
            _costText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _costText.verticalOverflow = VerticalWrapMode.Overflow;
            _costText.raycastTarget = false;

            var costRect = _costText.rectTransform;
            costRect.SetParent(_dialog.BodyRect, worldPositionStays: false);
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(0f, 1f);
            costRect.pivot = new Vector2(0f, 1f);
            costRect.sizeDelta = new Vector2(ConfirmBodyWidth, 0f);
            costRect.anchoredPosition = Vector2.zero;
        }

        void BuildControls()
        {
            // Destructive first, primary second: the shared Dialog packs its
            // button row from the right, so adding `Keep playing` last is what
            // puts the safe option on the right, where the thumb is.
            _endTheGameButton = _dialog.AddButton(
                ButtonKind.Destructive,
                EndTheGameLabel,
                HandleEndTheGameClicked);

            _keepPlayingButton = _dialog.AddButton(
                ButtonKind.Primary,
                KeepPlayingLabel,
                RequestKeepPlaying,
                isLeastDestructive: true);

            // ...and then the destructive one is pinned to the *left* edge of
            // the padding box, which is where the mockup draws it and further
            // from the thumb than packing it rightward would leave it.
            // ButtonDestructiveGap is the shared invariant's stated minimum
            // separation, not the measurement these two are laid out from:
            // what is left between them here is over 400 px, and a test
            // asserts it never drops under the minimum.
            var endRect = _endTheGameButton.RectTransform;
            endRect.anchorMin = new Vector2(0f, 0.5f);
            endRect.anchorMax = new Vector2(0f, 0.5f);
            endRect.pivot = new Vector2(0f, 0.5f);
            endRect.anchoredPosition = Vector2.zero;
        }

        void HandleEndTheGameClicked()
        {
            if (_game == null)
            {
                // Nothing to end, so nowhere to go: a confirm that was never
                // pointed at a game cannot answer for one.
                return;
            }

            // Two separate things, in this order. Core's own end-the-game
            // action first — it marks the game over and leaves every frog's
            // lane position exactly as it stands, because ending a game is not
            // losing it...
            _game.EndGame();

            // ...and then the request to move the screen to game over, with
            // those standings. This view does not move screens itself.
            var handler = GameEnded;
            if (handler != null)
            {
                handler();
            }
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
