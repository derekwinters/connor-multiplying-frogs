using System;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// AnswerResultDialogView.cs and EndGameConfirmView.cs work around — so the
// shared components are pulled in by explicit alias, and a bare `Button`,
// `ButtonKind`, `DialogPanel`, `BoardColours` or `FrogColours` in this file
// always means the shared component's.
using BoardColours = Frogs.Unity.UI.BoardColours;
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The moment a frog gets home — docs/specs/ui/player-won.md, built to that
    /// page and its two committed 1:1 mockups, on the shared
    /// <see cref="DialogPanel"/> (#219) and the shared <see cref="Button"/>
    /// (#214) and nothing else: no new visual component, no sprite, no bitmap.
    ///
    /// Before this existed, a child who got their frog home was handed a dialog
    /// saying whose turn it was next and then watched everybody else finish.
    /// This is the one screen in the game that is about a single child's moment
    /// rather than about the state of the game, and the frog drawn at
    /// <see cref="WonFrogDiameter"/> is the whole of why: "the board says a
    /// frog moved, and this says a frog arrived."
    ///
    /// **It decides nothing and changes nothing.** It does not end the game,
    /// skip anyone, or reorder anything — player-won.md's third invariant. The
    /// two facts that vary are read straight off Core: which frog just got home
    /// (<see cref="Game.FrogJustHome"/>) and whether it was the first
    /// (<see cref="Game.Winner"/>). Whether play continues after the first frog
    /// is home is a **rule**, it is Connor's, and it is that page's own open
    /// question — nothing here has an opinion about it.
    ///
    /// **The wording changes after the first frog.** Only the first frog home
    /// wins; every later one "is home". Saying "wins" four times in one game
    /// would be four lies after the first.
    ///
    /// **Hardware back is inert**, the same way it is on the three dialogs
    /// before this one in the turn's chain: by adding no handler at all and
    /// nominating no least-destructive button on the shared Dialog. The router
    /// (#213) already knows back does nothing over
    /// <see cref="Frogs.Core.Dialog.PlayerWon"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayerWonDialogView : MonoBehaviour
    {
        // docs/specs/ui/player-won.md#named-constants — the page's own table,
        // under the identical names.
        public const float WonDialogWidth = 900f;
        public const float WonDialogHeight = 640f;
        public const float WonFrogDiameter = 220f;
        public const float WonFrogOutline = 8f;
        public const float WonHeadlineGap = 40f;
        public const float WonHeadlineSize = 76f;
        public const float WonHeadlineLineBox = 92f;

        // The rest of what this dialog measures itself with is other pages'
        // rows, referenced under the identical name rather than redeclared
        // here — the same line #220, #222 and #226 all drew:
        //
        // - the panel's padding, corner radius, scrim, cross-fade and the
        //   bottom-right button row are the shared Dialog's
        //   (shared-components.md#dialog).
        // - `ButtonHeight` and `ButtonMinWidth` are the shared Button's
        //   (shared-components.md#button).
        // - `PieceEdge` is the *board's* colour row, not this page's
        //   (game-board.md#named-constants), because it is the same outline
        //   the frog wears on the lane it just left.
        //
        // `WonFrogDiameter` is deliberately not the board's
        // `FrogPieceDiameter` (88 px) — see player-won.md's own note on why
        // the two must not be dragged into step.

        // docs/specs/ui/player-won.md § Elements — the headline's two rows and
        // the controls' two, written out verbatim. This dialog picks one of
        // each; it never composes a third.
        const string FirstHomeHeadlineFormat = "{0} wins!";
        const string LaterHomeHeadlineFormat = "{0} is home!";
        const string NextTurnFormat = "{0}'s turn";
        const string SeeTheResultsLabel = "See the results";

        // No imported font — matches Button.cs's, DialogPanel.cs's and
        // AnswerResultDialogView.cs's own choice, for the same reason (no
        // external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colour copied verbatim from the committed mockups' CSS custom
        // properties, the same line Button.cs, DialogPanel.cs and
        // AnswerResultDialogView.cs each draw for their own: not a geometry
        // constant on any spec page's table, so not declared as a named spec
        // constant.
        static readonly Color HeadlineColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink

        static Sprite s_frogSprite;
        static Sprite s_frogFillSprite;

        // A rounded rect whose radius is half its own size is a circle — the
        // same shape the board's frog piece, the Player chip's swatch and the
        // answer result's mark all use, rather than a fifth way of drawing one.
        // The inside gets its own sprite at its own radius so its curve does
        // not square off when it is inset.
        static Sprite FrogSprite
        {
            get
            {
                if (s_frogSprite == null)
                {
                    s_frogSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(WonFrogDiameter / 2f));
                }

                return s_frogSprite;
            }
        }

        static Sprite FrogFillSprite
        {
            get
            {
                if (s_frogFillSprite == null)
                {
                    s_frogFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt((WonFrogDiameter - (2f * WonFrogOutline)) / 2f));
                }

                return s_frogFillSprite;
            }
        }

        Game _game;

        RectTransform _rect;
        DialogPanel _dialog;
        RectTransform _frogRect;
        Image _frogOutline;
        Image _frogFill;
        Text _headlineText;
        Button _handOnButton;

        bool _initialized;
        bool _handedOn;

        /// <summary>
        /// The one button was pressed. Whoever is listening decides where that
        /// goes — back to the board with the next player's turn already begun,
        /// or on to [game over](docs/specs/ui/game-over.md) if that frog was
        /// the last one home. This view moves no screens itself, and it never
        /// raises this twice for one opening.
        /// </summary>
        public event Action HandedOn;

        /// <summary>The view's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this is laid out on, at <see cref="WonDialogWidth"/> x <see cref="WonDialogHeight"/>.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>`frog` — <see cref="WonFrogDiameter"/> across, horizontally centred, `DialogPadding` from the panel's top edge.</summary>
        public RectTransform FrogRect
        {
            get
            {
                EnsureInitialized();
                return _frogRect;
            }
        }

        /// <summary>The frog's `PieceEdge` outline — the same ring it wears on the lane it just left.</summary>
        public Image FrogOutline
        {
            get
            {
                EnsureInitialized();
                return _frogOutline;
            }
        }

        /// <summary>The frog itself, in its own colour, inset by <see cref="WonFrogOutline"/>.</summary>
        public Image FrogFill
        {
            get
            {
                EnsureInitialized();
                return _frogFill;
            }
        }

        /// <summary>`headline` — `Blue wins!`, or `Pink is home!`. One line, centred, in a <see cref="WonHeadlineLineBox"/> box.</summary>
        public Text HeadlineText
        {
            get
            {
                EnsureInitialized();
                return _headlineText;
            }
        }

        /// <summary>`controls` — the shared Dialog's own button row, bottom-right of the panel.</summary>
        public RectTransform ControlsRect
        {
            get
            {
                EnsureInitialized();
                return _dialog.ButtonRowRect;
            }
        }

        /// <summary>The one button, named for what happens next — never `OK`.</summary>
        public Button HandOnButton
        {
            get
            {
                EnsureInitialized();
                return _handOnButton;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Points the dialog at the game whose turn just landed a frog home,
        /// and opens it. Every word it draws is read from here and nowhere
        /// else: which frog arrived, what that frog is called, whether it was
        /// the first, and whether there is anybody left to hand to.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The turn that just played landed nobody home, so there is no
        /// arrival to announce. Structural rather than a comment: this dialog
        /// has no wording for a turn that got nowhere, and there is no reason
        /// for it to have one — player-won.md's entry condition is exactly
        /// <see cref="Game.FrogJustHome"/> having a value.
        /// </exception>
        public void Initialize(Game game)
        {
            EnsureInitialized();

            _game = game ?? throw new ArgumentNullException(nameof(game));

            if (!_game.FrogJustHome.HasValue)
            {
                _game = null;

                throw new ArgumentException(
                    "this dialog only opens on a turn that landed a frog on its End log.",
                    nameof(game));
            }

            _handedOn = false;

            Refresh();
            _dialog.Open();
        }

        /// <summary>
        /// Re-reads the game and redraws. Every value is asked for again —
        /// the frog, its name, whether it was first and who is next all come
        /// back from Core, and none of them is remembered from the last pass.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_game == null)
            {
                return;
            }

            var frog = _game.FrogJustHome.Value;

            _frogFill.color = FrogColours.For(frog);

            // Only the first frog home "wins" — player-won.md's second
            // invariant. Core already knows which frog that was; this asks
            // rather than counting arrivals for itself.
            var isFirstHome = _game.Winner == frog;

            _headlineText.text = string.Format(
                isFirstHome ? FirstHomeHeadlineFormat : LaterHomeHeadlineFormat,
                _game.NameFor(frog));

            // The hand-off has already run by the time this dialog opens, so
            // "there is a next player" is simply whose turn it now is. On the
            // one turn where there is not — the hop that got the last frog
            // home — Core skipped the hand-off and the button leads to the
            // standings instead.
            _handOnButton.SetLabelText(_game.IsOver
                ? SeeTheResultsLabel
                : string.Format(NextTurnFormat, _game.NameFor(_game.ActiveFrog)));
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and EndGameConfirmView's own
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
            // board is the shared Dialog's scrim, and that has to reach the
            // edges on a device that is not 16:10. The panel underneath is
            // centre-anchored, so it does not move.
            StretchToFill(_rect);

            BuildDialog();
            BuildFrog();
            BuildHeadline();
            BuildControls();
        }

        void BuildDialog()
        {
            var dialogGO = new GameObject("PlayerWonDialog", typeof(RectTransform));
            dialogGO.transform.SetParent(_rect, worldPositionStays: false);

            _dialog = dialogGO.AddComponent<DialogPanel>();

            // The shared Dialog at this dialog's own size. Everything else
            // about it — the scrim, the corners, the padding, the cross-fade —
            // is inherited unchanged.
            //
            // Its title is deliberately left empty: player-won.md's Regions
            // table has three regions and none of them is a title. The frog
            // stands where a title would, which is what "measured from the
            // panel's top down, because the frog is the thing this screen is
            // about" means.
            _dialog.SetSize(WonDialogWidth, WonDialogHeight);
        }

        void BuildFrog()
        {
            // The frog is pinned to the *panel*, not to the shared Dialog's
            // body region: the body starts below a title this dialog does not
            // have, and player-won.md measures everything from the panel's own
            // top edge.
            var frogGO = new GameObject("Frog", typeof(RectTransform), typeof(Image));
            _frogOutline = frogGO.GetComponent<Image>();
            _frogOutline.sprite = FrogSprite;
            _frogOutline.type = Image.Type.Sliced;
            _frogOutline.color = BoardColours.PieceEdge;
            _frogOutline.raycastTarget = false;

            _frogRect = _frogOutline.rectTransform;
            _frogRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _frogRect.anchorMin = new Vector2(0.5f, 1f);
            _frogRect.anchorMax = new Vector2(0.5f, 1f);
            _frogRect.pivot = new Vector2(0.5f, 1f);
            _frogRect.sizeDelta = new Vector2(WonFrogDiameter, WonFrogDiameter);
            _frogRect.anchoredPosition = new Vector2(0f, -DialogPanel.DialogPadding);

            var fillGO = new GameObject("FrogFill", typeof(RectTransform), typeof(Image));
            _frogFill = fillGO.GetComponent<Image>();
            _frogFill.sprite = FrogFillSprite;
            _frogFill.type = Image.Type.Sliced;
            _frogFill.raycastTarget = false;

            var fillRect = _frogFill.rectTransform;
            fillRect.SetParent(_frogRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(WonFrogOutline, WonFrogOutline);
            fillRect.offsetMax = new Vector2(-WonFrogOutline, -WonFrogOutline);
        }

        void BuildHeadline()
        {
            var headlineGO = new GameObject("Headline", typeof(RectTransform), typeof(Text));
            _headlineText = headlineGO.GetComponent<Text>();
            _headlineText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _headlineText.fontSize = (int)WonHeadlineSize;
            _headlineText.fontStyle = FontStyle.Bold;
            _headlineText.color = HeadlineColor;
            _headlineText.alignment = TextAnchor.MiddleCenter;
            // One line, always — a name long enough to wrap would move the
            // button, and WonHeadlineLineBox exists precisely so the block
            // below the frog is the same height whatever the name is.
            _headlineText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _headlineText.verticalOverflow = VerticalWrapMode.Overflow;
            _headlineText.raycastTarget = false;

            // WonHeadlineGap is measured from the frog, so the headline's own
            // top is the padding, the frog and the gap added up — not a fourth
            // number of its own.
            var headlineRect = _headlineText.rectTransform;
            headlineRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            headlineRect.anchorMin = new Vector2(0f, 1f);
            headlineRect.anchorMax = new Vector2(1f, 1f);
            headlineRect.pivot = new Vector2(0.5f, 1f);
            headlineRect.sizeDelta = new Vector2(0f, WonHeadlineLineBox);
            headlineRect.anchoredPosition = new Vector2(
                0f,
                -(DialogPanel.DialogPadding + WonFrogDiameter + WonHeadlineGap));
        }

        void BuildControls()
        {
            // A plain primary Button at the shared component's own size, added
            // through the shared Dialog so it lands in the button row,
            // bottom-right — nothing here overrides ButtonHeight or
            // ButtonMinWidth. Its label is set on every Refresh, because who
            // is next is a fact about the game, not about this button.
            //
            // It is deliberately *not* nominated as this dialog's least
            // destructive button: that is the value the router would invoke on
            // hardware back, and back is inert here.
            _handOnButton = _dialog.AddButton(ButtonKind.Primary, string.Empty, HandleHandOnClicked);
        }

        void HandleHandOnClicked()
        {
            // The only control on the dialog, and it can only ever act once —
            // a second press must not hand the same arrival on twice.
            if (_handedOn)
            {
                return;
            }

            _handedOn = true;

            _dialog.Close();

            var handler = HandedOn;
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
