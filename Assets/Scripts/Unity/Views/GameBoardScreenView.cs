using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameSetupScreenView.cs work around —
// so these are pulled in by explicit alias rather than a wildcard
// `using Frogs.Unity.UI;`, and a bare `Button`, `ButtonKind`, `FrogColours`
// or `PlayerChip` in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The pond — docs/specs/ui/game-board.md, built to that page and its
    /// committed 1:1 mockup. Three horizontal bands filling the full height
    /// with no gaps: <c>header</c> (whose turn it is, and the gear),
    /// <c>pond</c> (one <see cref="GameBoardLaneView"/> per frog in the game,
    /// stacked and vertically centred), and <c>controls</c> (the oversized
    /// `Roll`).
    ///
    /// **It reads Core; it never computes.** Whose turn it is comes from
    /// <see cref="Game.ActiveFrog"/>, and where every frog sits comes from
    /// that frog's <see cref="Lane"/> — this view never counts a correct
    /// answer, never decides who goes next, and never works out a pixel
    /// offset from a roll or an answer.
    ///
    /// Two things it deliberately does not do:
    ///
    /// - **No motion.** Every frog is drawn at rest at whatever position Core
    ///   reports. The hop that plays after the result dialog closes belongs
    ///   to <c>answer-result</c> (#224), which owns the dialog that closes and
    ///   starts it.
    /// - **No end-of-game detection.** This board keeps rendering whatever
    ///   Core reports for as long as it is shown; noticing that the last frog
    ///   got home and moving off this screen is <c>unity-game-over</c> (#225),
    ///   gated on <c>core-game-end</c> (#211).
    ///
    /// Both of game-board.md's open questions are left exactly as open as it
    /// leaves them: only the lanes in play are drawn, and nothing anywhere
    /// reports the last roll.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameBoardScreenView : MonoBehaviour
    {
        // docs/specs/ui/game-board.md#named-constants — the board's table.
        public const float SafeMargin = 48f;
        public const float BoardHeaderHeight = 128f;
        public const float BoardControlsHeight = 176f;
        public const float BoardBandOutline = 3f;
        public const float TurnBannerSize = 52f;
        public const float TurnBannerGap = 24f;
        public const float SettingsButtonSize = 96f;
        public const float SettingsGlyphSize = 44f;
        public const float SettingsButtonOutline = 4f;
        public const float RollButtonWidth = 480f;
        public const float RollButtonHeight = 144f;
        public const float RollButtonLabelSize = 56f;

        /// <summary>
        /// How long the frog's hop takes — docs/specs/ui/game-board.md's own
        /// row, and the board's value rather than the result dialog's: "the
        /// move is animated on this screen, after the result dialog closes,
        /// over <c>FrogHopDuration</c> (0.4 s), one pad's distance."
        ///
        /// This view still plays no motion of its own. The constant lives here
        /// because it is this page's, and <c>answer-result</c> (#224) — which
        /// owns the dialog that closes and starts the hop — references it
        /// rather than declaring a second copy.
        /// </summary>
        public const float FrogHopDuration = 0.4f;

        // The other two rows of that table, `LanePositionCount` (9) and
        // `LaneWinningPosition` (8), are Frogs.Core.Lane's own constants,
        // referenced under the identical name rather than redeclared here —
        // see GameBoardLaneView.

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // "Whose turn it is is stated in words in the header, not only shown
        // by a highlight." The wording is the frog's colour name, because
        // frogs have no other name.
        const string TurnBannerFormat = "{0} frog's turn";
        const string RollLabel = "Roll";

        // No imported font — matches Button.cs's, PlayerChip.cs's and
        // GameSetupScreenView.cs's own choice, for the same reason (no
        // external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockup
        // (docs/specs/ui/mockups/game-board.html) — the same line Button.cs,
        // PlayerChip.cs and GameSetupScreenView.cs each draw for their own
        // colours: not a geometry constant on any spec page's table, so not
        // declared as a named spec constant.
        //
        // The mockup's `--bg` is the exception. It is the whole screen's
        // background rather than this screen's chrome, and the scene camera
        // has to clear to the same value, so it lives on ScreenColours where
        // both can read it.
        static readonly Color BandColor = new Color32(0xE2, 0xE8, 0xE5, 0xFF); // mockup's header/controls bands
        static readonly Color BandOutlineColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockup's --faint
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockup's --ink

        RectTransform _rect;
        RectTransform _contentRect;
        Image _background;

        RectTransform _headerRect;
        Image _headerHairline;
        PlayerChip _turnBannerChip;
        Text _turnBannerText;
        GameBoardSettingsButton _settingsButton;

        RectTransform _pondRect;
        readonly List<GameBoardLaneView> _lanes = new List<GameBoardLaneView>();
        readonly Dictionary<FrogColour, GameBoardLaneView> _lanesByColour = new Dictionary<FrogColour, GameBoardLaneView>();

        RectTransform _controlsRect;
        Image _controlsHairline;
        Button _rollButton;

        bool _initialized;
        Game _game;

        /// <summary>
        /// `Roll` was pressed. The board disables `Roll` before raising this
        /// and leaves it disabled until <see cref="NotifyTurnResolved"/> — so
        /// a double-tap cannot roll twice. What happens next (opening
        /// docs/specs/ui/roll-and-card.md) is #221's; this view only says
        /// that the press happened.
        /// </summary>
        public event Action RollPressed;

        /// <summary>
        /// Open the settings dialog. Raised by the gear and by hardware back,
        /// which is the same request through two doors —
        /// docs/specs/ui/game-board.md: "Hardware back opens the settings
        /// dialog. It does not quit, and it never quits without the confirm."
        /// The dialog's own contents are #222's.
        /// </summary>
        public event Action SettingsRequested;

        /// <summary>The screen's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The paint that reaches every edge of the screen, whatever the device's aspect ratio.</summary>
        public Image BackgroundImage
        {
            get
            {
                EnsureInitialized();
                return _background;
            }
        }

        /// <summary>Everything laid out in reference pixels — the 1920 x 1200 reference canvas, centred.</summary>
        public RectTransform ContentRect
        {
            get
            {
                EnsureInitialized();
                return _contentRect;
            }
        }

        /// <summary>`header` — pinned to the top, <see cref="BoardHeaderHeight"/> tall, full width.</summary>
        public RectTransform HeaderRect
        {
            get
            {
                EnsureInitialized();
                return _headerRect;
            }
        }

        /// <summary>The hairline under `header` — <see cref="BoardBandOutline"/> tall, drawn inside the band.</summary>
        public Image HeaderHairline
        {
            get
            {
                EnsureInitialized();
                return _headerHairline;
            }
        }

        /// <summary>The hairline over `controls` — <see cref="BoardBandOutline"/> tall, drawn inside the band.</summary>
        public Image ControlsHairline
        {
            get
            {
                EnsureInitialized();
                return _controlsHairline;
            }
        }

        /// <summary>The active frog's chip, left of the turn banner.</summary>
        public PlayerChip TurnBannerChip
        {
            get
            {
                EnsureInitialized();
                return _turnBannerChip;
            }
        }

        /// <summary>The turn banner — `Green frog's turn`, in words.</summary>
        public Text TurnBannerText
        {
            get
            {
                EnsureInitialized();
                return _turnBannerText;
            }
        }

        /// <summary>The gear, top right. Never disabled by turn state.</summary>
        public GameBoardSettingsButton SettingsButton
        {
            get
            {
                EnsureInitialized();
                return _settingsButton;
            }
        }

        /// <summary>`pond` — everything between the two pinned bands.</summary>
        public RectTransform PondRect
        {
            get
            {
                EnsureInitialized();
                return _pondRect;
            }
        }

        /// <summary>One lane per frog in the game, in turn order. Two, three, or four — never a placeholder.</summary>
        public IReadOnlyList<GameBoardLaneView> Lanes
        {
            get
            {
                EnsureInitialized();
                return _lanes;
            }
        }

        /// <summary>`controls` — pinned to the bottom, <see cref="BoardControlsHeight"/> tall, full width.</summary>
        public RectTransform ControlsRect
        {
            get
            {
                EnsureInitialized();
                return _controlsRect;
            }
        }

        /// <summary>`Roll` — the only way to start a turn.</summary>
        public Button RollButton
        {
            get
            {
                EnsureInitialized();
                return _rollButton;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            // Android back arrives as Escape. The board owns this rather than
            // delegating it, because "hardware back opens settings, never
            // quits" is game-board.md's own rule about this screen — see
            // HandleHardwareBack.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleHardwareBack();
            }
        }

        /// <summary>
        /// Points the board at the game it draws, and lays out one lane per
        /// frog in that game's turn order. Everything the board shows is read
        /// from here and nowhere else.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
        public void Initialize(Game game)
        {
            EnsureInitialized();

            _game = game ?? throw new ArgumentNullException(nameof(game));

            BuildLanes();
            Refresh();
        }

        /// <summary>
        /// Re-reads the game and redraws. Every value it draws is asked for
        /// again — nothing is remembered from the last pass, so a frog that
        /// moved, a turn that passed, or a frog that got home all show up
        /// simply by asking.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_game == null)
            {
                return;
            }

            var active = _game.ActiveFrog;

            _turnBannerText.text = string.Format(TurnBannerFormat, active);
            _turnBannerChip.SetFrog(FrogColours.For(active), active.ToString());
            _turnBannerChip.SetState(PlayerChipState.Active);

            foreach (var lane in _lanes)
            {
                lane.Render(_game.LaneFor(lane.Colour), lane.Colour == active);
            }
        }

        /// <summary>
        /// The turn that <see cref="RollPressed"/> started has resolved:
        /// `Roll` becomes pressable again and the board redraws. This is the
        /// only thing that re-enables `Roll` — never a timer, and never the
        /// passage of time.
        ///
        /// This is the seam the turn's own screens wire into.
        /// <see cref="AnswerResultDialogView"/> (#224) calls it once the frog
        /// has landed — after Core's hand-off, so the redraw shows the next
        /// player's turn and, if that hop got a frog home, its `Home` chip.
        /// </summary>
        public void NotifyTurnResolved()
        {
            EnsureInitialized();

            _rollButton.SetDisabled(false);
            Refresh();
        }

        /// <summary>
        /// What hardware back does on this screen: exactly what the gear
        /// does. It does not quit, and this type contains no path that could
        /// — docs/specs/ui/game-board.md's Behaviour section.
        /// </summary>
        public void HandleHardwareBack()
        {
            EnsureInitialized();

            RaiseSettingsRequested();
        }

        /// <summary>This frog's lane.</summary>
        /// <exception cref="KeyNotFoundException"><paramref name="colour"/> is not in the game's roster.</exception>
        public GameBoardLaneView LaneFor(FrogColour colour)
        {
            EnsureInitialized();
            return _lanesByColour[colour];
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as Button, PlayerChip and
        // GameSetupScreenView's own EnsureInitialized.
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

            // The root fills the whole canvas, which on a device that is not
            // 16:10 is larger than the 1920 x 1200 reference — see
            // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
            // Only the background hangs off it. Everything that is laid out in
            // reference pixels hangs off `Content` instead, so the extra space
            // a wider or taller device gives us is painted and nothing else.
            StretchToFill(_rect);

            BuildBackground();
            BuildContent();

            BuildHeader();
            BuildPond();
            BuildControls();
        }

        void BuildBackground()
        {
            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.color = ScreenColours.Background;
            _background.raycastTarget = false;
            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);

            // The canvas, not the reference rectangle: this is the paint that
            // reaches the edge of the screen whatever shape the screen is.
            StretchToFill(backgroundRect);
        }

        void BuildContent()
        {
            // The reference canvas, centred — exactly the rect the root used
            // to be, so every child below keeps the anchors, sizes and offsets
            // it already had and nothing on the board moves by a pixel.
            var contentGO = new GameObject("Content", typeof(RectTransform));
            _contentRect = (RectTransform)contentGO.transform;
            _contentRect.SetParent(_rect, worldPositionStays: false);
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _contentRect.anchoredPosition = Vector2.zero;
        }

        void BuildHeader()
        {
            // header — pinned to the top, full width, not shrinking on a
            // shorter screen.
            var headerGO = new GameObject("Header", typeof(RectTransform), typeof(Image));
            var headerImage = headerGO.GetComponent<Image>();
            headerImage.color = BandColor;
            headerImage.raycastTarget = false;

            _headerRect = headerImage.rectTransform;
            _headerRect.SetParent(_contentRect, worldPositionStays: false);
            _headerRect.anchorMin = new Vector2(0f, 1f);
            _headerRect.anchorMax = new Vector2(1f, 1f);
            _headerRect.pivot = new Vector2(0.5f, 1f);
            _headerRect.sizeDelta = new Vector2(0f, BoardHeaderHeight);
            _headerRect.anchoredPosition = Vector2.zero;

            var chipGO = new GameObject("TurnBannerChip", typeof(RectTransform));
            var chipRect = (RectTransform)chipGO.transform;
            chipRect.SetParent(_headerRect, worldPositionStays: false);
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.pivot = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = new Vector2(SafeMargin, 0f);
            _turnBannerChip = chipGO.AddComponent<PlayerChip>();
            chipRect.sizeDelta = new Vector2(PlayerChip.PlayerChipWidth, PlayerChip.PlayerChipHeight);

            var bannerGO = new GameObject("TurnBanner", typeof(RectTransform), typeof(Text));
            _turnBannerText = bannerGO.GetComponent<Text>();
            _turnBannerText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _turnBannerText.fontSize = (int)TurnBannerSize;
            _turnBannerText.fontStyle = FontStyle.Bold;
            _turnBannerText.color = InkColor;
            _turnBannerText.alignment = TextAnchor.MiddleLeft;
            _turnBannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _turnBannerText.verticalOverflow = VerticalWrapMode.Overflow;
            _turnBannerText.raycastTarget = false;

            var bannerRect = _turnBannerText.rectTransform;
            bannerRect.SetParent(_headerRect, worldPositionStays: false);
            bannerRect.anchorMin = new Vector2(0f, 0.5f);
            bannerRect.anchorMax = new Vector2(0f, 0.5f);
            bannerRect.pivot = new Vector2(0f, 0.5f);
            bannerRect.sizeDelta = Vector2.zero;

            // The banner's words sit TurnBannerGap past the chip beside them —
            // the chip is the shared Player chip, so its width is that
            // component's own constant.
            bannerRect.anchoredPosition = new Vector2(
                SafeMargin + PlayerChip.PlayerChipWidth + TurnBannerGap,
                0f);

            var settingsGO = new GameObject("SettingsButton", typeof(RectTransform));
            var settingsRect = (RectTransform)settingsGO.transform;
            settingsRect.SetParent(_headerRect, worldPositionStays: false);
            settingsRect.anchorMin = new Vector2(1f, 0.5f);
            settingsRect.anchorMax = new Vector2(1f, 0.5f);
            settingsRect.pivot = new Vector2(1f, 0.5f);
            settingsRect.anchoredPosition = new Vector2(-SafeMargin, 0f);

            _settingsButton = settingsGO.AddComponent<GameBoardSettingsButton>();

            // Unity does not run Awake() on AddComponent outside play mode,
            // and the gear sizes itself when it builds. Touch its rect so it
            // builds now, while the header is being laid out, rather than
            // whenever something first happens to read it.
            settingsRect = _settingsButton.RectTransform;

            _settingsButton.Clicked += HandleSettingsClicked;

            _headerHairline = BuildBandHairline(_headerRect, "HeaderHairline", atTop: false);
        }

        // The hairline the mockup draws under `header` and over `controls`,
        // BoardBandOutline tall and drawn inside the band's own bounds so the
        // band's height is untouched.
        Image BuildBandHairline(RectTransform band, string hairlineName, bool atTop)
        {
            var hairlineGO = new GameObject(hairlineName, typeof(RectTransform), typeof(Image));
            var hairline = hairlineGO.GetComponent<Image>();
            hairline.color = BandOutlineColor;
            hairline.raycastTarget = false;

            var hairlineRect = hairline.rectTransform;
            hairlineRect.SetParent(band, worldPositionStays: false);
            hairlineRect.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            hairlineRect.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            hairlineRect.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
            hairlineRect.sizeDelta = new Vector2(0f, BoardBandOutline);
            hairlineRect.anchoredPosition = Vector2.zero;

            return hairline;
        }

        void BuildPond()
        {
            // pond — everything between the other two bands. A shorter screen
            // loses height from here and nothing else.
            var pondGO = new GameObject("Pond", typeof(RectTransform));
            _pondRect = (RectTransform)pondGO.transform;
            _pondRect.SetParent(_contentRect, worldPositionStays: false);
            _pondRect.anchorMin = Vector2.zero;
            _pondRect.anchorMax = Vector2.one;
            _pondRect.offsetMin = new Vector2(0f, BoardControlsHeight);
            _pondRect.offsetMax = new Vector2(0f, -BoardHeaderHeight);
        }

        void BuildControls()
        {
            // controls — pinned to the bottom, full width. A smaller `Roll`
            // is the wrong thing to trade away, so this band does not shrink.
            var controlsGO = new GameObject("Controls", typeof(RectTransform), typeof(Image));
            var controlsImage = controlsGO.GetComponent<Image>();
            controlsImage.color = BandColor;
            controlsImage.raycastTarget = false;

            _controlsRect = controlsImage.rectTransform;
            _controlsRect.SetParent(_contentRect, worldPositionStays: false);
            _controlsRect.anchorMin = new Vector2(0f, 0f);
            _controlsRect.anchorMax = new Vector2(1f, 0f);
            _controlsRect.pivot = new Vector2(0.5f, 0f);
            _controlsRect.sizeDelta = new Vector2(0f, BoardControlsHeight);
            _controlsRect.anchoredPosition = Vector2.zero;

            _controlsHairline = BuildBandHairline(_controlsRect, "ControlsHairline", atTop: true);

            var rollGO = new GameObject("Roll", typeof(RectTransform));
            rollGO.transform.SetParent(_controlsRect, worldPositionStays: false);

            _rollButton = rollGO.AddComponent<Button>();
            _rollButton.SetKind(ButtonKind.Primary);
            _rollButton.SetLabelText(RollLabel);

            // Primary, oversized — game-board.md's own named override of the
            // shared Button's footprint, agreed at the spec level. The three
            // Button kinds still share a footprint with each other; this one
            // instance is bigger than all three.
            _rollButton.SetSize(RollButtonWidth, RollButtonHeight);
            _rollButton.SetLabelSize(RollButtonLabelSize);

            var rollRect = _rollButton.RectTransform;
            rollRect.anchorMin = new Vector2(0.5f, 0.5f);
            rollRect.anchorMax = new Vector2(0.5f, 0.5f);
            rollRect.pivot = new Vector2(0.5f, 0.5f);
            rollRect.anchoredPosition = Vector2.zero;

            _rollButton.Clicked += HandleRollClicked;
        }

        void BuildLanes()
        {
            foreach (var lane in _lanes)
            {
                if (lane != null)
                {
                    UnityEngine.Object.DestroyImmediate(lane.gameObject);
                }
            }

            _lanes.Clear();
            _lanesByColour.Clear();

            // One lane per frog in the game, in turn order — two, three, or
            // four. Every frog is visible at once, with no scrolling, paging
            // or zooming, and an absent frog gets no placeholder lane.
            var turnOrder = _game.TurnOrder;
            var laneWidth = CanvasWidth - (2f * SafeMargin);
            var groupTop = (turnOrder.Count * GameBoardLaneView.LaneHeight) / 2f;

            for (var index = 0; index < turnOrder.Count; index++)
            {
                var colour = turnOrder[index];

                var laneGO = new GameObject(colour.ToString() + "Lane", typeof(RectTransform));
                var laneRect = (RectTransform)laneGO.transform;
                laneRect.SetParent(_pondRect, worldPositionStays: false);

                // Stacked and vertically centred as a group within the pond,
                // so a two-frog game sits centred rather than clinging to the
                // top.
                laneRect.anchorMin = new Vector2(0.5f, 0.5f);
                laneRect.anchorMax = new Vector2(0.5f, 0.5f);
                laneRect.pivot = new Vector2(0.5f, 0.5f);
                laneRect.sizeDelta = new Vector2(laneWidth, GameBoardLaneView.LaneHeight);
                laneRect.anchoredPosition = new Vector2(
                    0f,
                    groupTop - ((index + 0.5f) * GameBoardLaneView.LaneHeight));

                var lane = laneGO.AddComponent<GameBoardLaneView>();
                lane.Initialize(colour);

                _lanes.Add(lane);
                _lanesByColour[colour] = lane;
            }
        }

        void HandleRollClicked()
        {
            // Disabled the moment the press resolves and left that way until
            // told the turn resolved — the invariant that a double-tap cannot
            // roll twice. Disabled *before* the event is raised, so nothing a
            // listener does can arrive at a still-pressable button.
            _rollButton.SetDisabled(true);

            var handler = RollPressed;
            if (handler != null)
            {
                handler();
            }
        }

        void HandleSettingsClicked()
        {
            RaiseSettingsRequested();
        }

        void RaiseSettingsRequested()
        {
            var handler = SettingsRequested;
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
