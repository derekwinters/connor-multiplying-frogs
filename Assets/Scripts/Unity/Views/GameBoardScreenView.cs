using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameSetupScreenView.cs work around —
// so these are pulled in by explicit alias rather than a wildcard
// `using Frogs.Unity.UI;`, and a bare `Button` or `ButtonKind` in this file
// always means the shared component's. `FrogColours`, `PlayerChip` and
// `PlayerChipState` were three more of these until #326 took the header's
// chip away — this screen now names a frog's colour only through the lane
// views, which alias them for themselves.
using BoardColours = Frogs.Unity.UI.BoardColours;
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The pond — docs/specs/ui/game-board.md, built to that page and its
    /// committed 1:1 mockup. Three horizontal bands filling the full height
    /// with no gaps: <c>header</c> (whose turn it is, and the gear),
    /// <c>pond</c> (one <see cref="GameBoardLaneView"/> per frog in the game,
    /// stacked and vertically centred, plus the two logs they share), and
    /// <c>controls</c> (the oversized `Roll`).
    ///
    /// **The logs are the pond's, not a lane's.** There is one Start log and
    /// one End log on the board however many frogs are playing, each spanning
    /// the whole pond band, and every frog's position 0 and position 8 are on
    /// them (#296). That is a shared *drawing*, not a shared position: each
    /// frog sits on its own lane's centre line, which is what keeps "frogs
    /// never share a lane and never interact" true in the picture as well as
    /// in the state.
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
    /// game-board.md's remaining open questions are left exactly as open as it
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

        // `TurnBannerGap` used to live here, at 24 px. It is gone rather than
        // renamed (#326): it measured the gap between the banner's chip and
        // its words, and there is no chip. Words alone need no gap, because
        // there is nothing to gap —
        // docs/specs/ui/game-board.md#the-constant-this-page-used-to-carry.
        // `PlayerChip.PlayerChipWidth` is untouched: the lane chips, roll and
        // card and the working-out grid all still draw one.
        public const float SettingsButtonSize = 96f;

        // The gear fills the square it is tapped on. It used to be 44 px —
        // ButtonLabelSize's number, a label sized to sit inside a ring and a
        // white disc that #321 deleted. With no chrome left to leave room
        // for, the glyph is sized against the tap target instead, and
        // `SettingsButtonOutline` is gone rather than renamed: nothing on
        // this control draws a ring at any width any more.
        public const float SettingsGlyphSize = 96f;
        public const float RollButtonWidth = 480f;
        public const float RollButtonHeight = 144f;
        public const float RollButtonLabelSize = 56f;

        // docs/specs/ui/game-board.md#named-constants — the two shared logs'
        // own table. They are the pond's, not a lane's: there is one Start log
        // and one End log for the whole board however many frogs are playing,
        // and every frog's position 0 and position 8 are on them (#296).
        public const float LogWidth = 176f;
        public const float LogRadius = 24f;

        /// <summary>
        /// How big the word on a log is — `LogLabelSize`.
        ///
        /// The log used to draw no word at all in this view, and the mockup's
        /// used to be a mid-brown one down the middle of a pale tan log. It is
        /// at the **top** now, in `LogLabelInk`, because the middle of a log
        /// is where the frogs stand: the pond band is `SharedLogHeight` tall
        /// and every lane's centre line crosses both logs, so a centred word
        /// is a word with a frog on it.
        /// </summary>
        public const float LogLabelSize = 26f;

        /// <summary>
        /// How far below the top of its log the word starts —
        /// `LogLabelTopPadding`. The committed mockup's `.log` rule, which is
        /// the drawing this screen is built to.
        /// </summary>
        public const float LogLabelTopPadding = 28f;

        /// <summary>
        /// How tall a shared log is: the pond band's own height, so the log
        /// fills the band edge to edge with the two hairlines.
        ///
        /// It is **the same 896 px at two, three and four frogs**. That is
        /// Derek's answer, on #296, to the question game-board.md left open —
        /// the log spans the full pond rather than only the lanes in play — so
        /// this is one number rather than the `LaneCount x LaneHeight`
        /// expression the mockup proposed. `LogHeight` (120 px) is gone rather
        /// than renamed: it was the height of a log sized to sit inside one
        /// 184 px lane, and there is no such thing on this board any more.
        ///
        /// Written as the band's arithmetic rather than as 896 so that if the
        /// header or the controls band ever changes height, the logs follow
        /// without anybody having to remember to move them.
        /// </summary>
        public const float SharedLogHeight = CanvasHeight - BoardHeaderHeight - BoardControlsHeight;

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
        //
        // The height is what the pond band's own height, and so the logs', is
        // measured from. The width is no longer a layout number: the three
        // bands are the screen's (#303) and, since #408, so is the pond's row
        // — it is the floor the row falls back to on a screen narrower than
        // the canvas every mockup is drawn at, and nothing else.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // "Whose turn it is is stated in words in the header, not only shown
        // by a highlight." The wording is the frog's **name and nothing
        // else** — its colour by default, or the name typed on game setup, so
        // `Connor's turn` for a renamed frog.
        // docs/specs/ui/game-setup.md#behaviour and
        // docs/specs/ui/shared-components.md#player-chip: nothing appends
        // anything to a name. This format used to staple `frog` onto a
        // colour, which read as `Connor frog's turn` the moment a name was
        // typed.
        const string TurnBannerFormat = "{0}'s turn";
        const string RollLabel = "Roll";

        // What each log says. `start-log` and `end-log` are the spec page's
        // names for the two parts; these are the words drawn on them.
        const string StartLogLabelText = "Start";
        const string EndLogLabelText = "End";

        // No imported font — matches Button.cs's, PlayerChip.cs's and
        // GameSetupScreenView.cs's own choice, for the same reason (no
        // external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // The board's colours are docs/specs/ui/game-board.md § Colours,
        // received by name from BoardColours exactly as the geometry above is
        // received from that page's constants table.
        //
        // The one worth reading twice is `PondWater`. Every other screen
        // paints ScreenColours.Background, which is also what the scene camera
        // clears to; the board paints its own water instead, to every edge of
        // the device, because the pond is not a rectangle inside a page — it
        // is the whole screen. #290's rule still holds: nothing behind the
        // canvas is ever visible, because this screen's own paint reaches the
        // edges. What the camera clears to is unchanged, and it is what shows
        // for the frame before *any* screen has painted.

        RectTransform _rect;
        Image _background;

        RectTransform _headerRect;
        Image _headerHairline;
        Text _turnBannerText;
        GameBoardSettingsButton _settingsButton;

        RectTransform _pondRect;
        Image _startLog;
        Text _startLogLabel;
        Image _endLog;
        Text _endLogLabel;
        readonly List<GameBoardLaneView> _lanes = new List<GameBoardLaneView>();
        readonly Dictionary<FrogColour, GameBoardLaneView> _lanesByColour = new Dictionary<FrogColour, GameBoardLaneView>();

        RectTransform _controlsRect;
        Image _controlsHairline;
        Button _rollButton;

        bool _initialized;
        Game _game;

        static Sprite s_logSprite;

        // One sprite, because a log is one drawing. It used to be two — a rim
        // at LogRadius around a fill inset by TrackOutline, each generated at
        // its own radius so the inner one kept its curve. `LogEdge` is gone
        // (#301), so there is no rim to generate and no inset to keep clear
        // of: the log's own fill reaches its full LogWidth x SharedLogHeight.
        static Sprite LogSprite
        {
            get
            {
                if (s_logSprite == null)
                {
                    s_logSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(LogRadius));
                }

                return s_logSprite;
            }
        }

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

        /// <summary>`header` — pinned to the top of the screen, <see cref="BoardHeaderHeight"/> tall, as wide as the screen.</summary>
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

        /// <summary>The turn banner — `Green's turn`, in words.</summary>
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

        /// <summary>`pond` — everything between the two pinned bands, and the water it paints there.</summary>
        public RectTransform PondRect
        {
            get
            {
                EnsureInitialized();
                return _pondRect;
            }
        }

        /// <summary>
        /// `start-log` — **one** log down the left of the pond, position 0 of
        /// every lane at once.
        ///
        /// This is the whole log: one image at `LogWidth` x
        /// `SharedLogHeight`, in `LogBrown`, with no rim. It used to be a rim
        /// and a separate fill, and `StartLogFill` is gone with `LogEdge`
        /// rather than renamed — there is no second layer left for a name to
        /// point at.
        /// </summary>
        public Image StartLog
        {
            get
            {
                EnsureInitialized();
                return _startLog;
            }
        }

        /// <summary>`Start`, written along the top of the Start log in `LogLabelInk`.</summary>
        public Text StartLogLabel
        {
            get
            {
                EnsureInitialized();
                return _startLogLabel;
            }
        }

        /// <summary>
        /// `end-log` — **one** log down the right of the pond, position 8 of
        /// every lane at once, and the winning space. One image, no rim, as
        /// <see cref="StartLog"/> is.
        /// </summary>
        public Image EndLog
        {
            get
            {
                EnsureInitialized();
                return _endLog;
            }
        }

        /// <summary>`End`, written along the top of the End log in `LogLabelInk`.</summary>
        public Text EndLogLabel
        {
            get
            {
                EnsureInitialized();
                return _endLogLabel;
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

        /// <summary>`controls` — pinned to the bottom of the screen, <see cref="BoardControlsHeight"/> tall, as wide as the screen.</summary>
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

            var activeName = _game.NameFor(active);

            _turnBannerText.text = string.Format(TurnBannerFormat, activeName);

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
            //
            // The background and the three bands hang off it, because all four
            // are paint that reaches the screen's edges: the bands are the top
            // and the bottom *of the screen*, not panels laid on the pond
            // (#303). The pond's row reaches them too, since #408 — the lanes
            // and the two shared logs are laid out across the screen's own
            // width. What stays in reference pixels is what `header` and
            // `controls` contain: anchored controls, not a track.
            StretchToFill(_rect);

            BuildBackground();

            BuildHeader();
            BuildPond();
            BuildControls();
        }

        void BuildBackground()
        {
            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.color = BoardColours.PondWater;
            _background.raycastTarget = false;
            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);

            // The canvas, not the reference rectangle: this is the paint that
            // reaches the edge of the screen whatever shape the screen is.
            StretchToFill(backgroundRect);
        }

        void BuildHeader()
        {
            // header — pinned to the top of the *screen*, as wide as the
            // screen, not shrinking on a shorter one. The banner and the gear
            // inside it are anchored to its own left and right edges, so they
            // follow the real edge with it: SafeMargin is a margin from the
            // screen, not from a virtual rectangle (#303).
            var headerGO = new GameObject("Header", typeof(RectTransform), typeof(Image));
            var headerImage = headerGO.GetComponent<Image>();
            headerImage.color = BoardColours.BandFill;
            headerImage.raycastTarget = false;

            _headerRect = headerImage.rectTransform;
            _headerRect.SetParent(_rect, worldPositionStays: false);
            _headerRect.anchorMin = new Vector2(0f, 1f);
            _headerRect.anchorMax = new Vector2(1f, 1f);
            _headerRect.pivot = new Vector2(0.5f, 1f);
            _headerRect.sizeDelta = new Vector2(0f, BoardHeaderHeight);
            _headerRect.anchoredPosition = Vector2.zero;

            var bannerGO = new GameObject("TurnBanner", typeof(RectTransform), typeof(Text));
            _turnBannerText = bannerGO.GetComponent<Text>();
            _turnBannerText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _turnBannerText.fontSize = (int)TurnBannerSize;
            _turnBannerText.fontStyle = FontStyle.Bold;
            _turnBannerText.color = BoardColours.BoardInk;
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

            // The words are what is anchored to the band's left edge now:
            // SafeMargin in, vertically centred. They used to start
            // SafeMargin + PlayerChipWidth + TurnBannerGap in, past a chip
            // that said what the active frog's own lane chip says already.
            bannerRect.anchoredPosition = new Vector2(SafeMargin, 0f);

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
            hairline.color = BoardColours.BandEdge;
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
            // pond — everything between the other two bands, edge to edge with
            // the screen. A shorter screen loses height from here and nothing
            // else, and a taller one gives its extra height to here and
            // nothing else.
            //
            // It paints its own water rather than letting the background show
            // through, which is the half of #303 that was invisible: the band
            // had the same anchoring fault as the other two and nobody could
            // see it, because a bare RectTransform's gap and the water behind
            // it are the same colour by definition.
            var pondGO = new GameObject("Pond", typeof(RectTransform), typeof(Image));
            var pondImage = pondGO.GetComponent<Image>();
            pondImage.color = BoardColours.PondWater;
            pondImage.raycastTarget = false;

            _pondRect = pondImage.rectTransform;
            _pondRect.SetParent(_rect, worldPositionStays: false);
            _pondRect.anchorMin = Vector2.zero;
            _pondRect.anchorMax = Vector2.one;
            _pondRect.offsetMin = new Vector2(0f, BoardControlsHeight);
            _pondRect.offsetMax = new Vector2(0f, -BoardHeaderHeight);

            // The two logs the lanes share. One Start log down the left and
            // one End log down the right, whatever the frog count — they are
            // parts of the pond, not parts of a lane, which is the whole of
            // #296. They are built here, before BuildLanes runs, so every
            // frog is drawn on top of the log it is standing on rather than
            // under it.
            //
            // The Start log stands in the same column every lane's track
            // starts in: past the safe margin, the chip gutter and the gap
            // after it. The End log is pinned to the right of the safe area,
            // where every lane's track ends. Neither is placed by a number of
            // its own.
            _startLog = BuildSharedLog("StartLog", 0f, StartLogLabelText, out _startLogLabel);
            _endLog = BuildSharedLog("EndLog", 1f, EndLogLabelText, out _endLogLabel);

            PlaceSharedLogs(PondRowWidth);
        }

        /// <summary>
        /// The width the pond's row is laid out across: the screen's own
        /// width — the row spreads to it, since #408 — or the reference
        /// canvas if the screen is somehow narrower than the one every mockup
        /// is drawn at, in which case the board keeps its reference row and is
        /// centred, exactly as it always was.
        /// </summary>
        float PondRowWidth
        {
            get { return Mathf.Max(CanvasWidth, _rect.rect.width); }
        }

        // Both logs, against the two ends of the row. This is asked for again
        // when the lanes are built, rather than only when the logs are: a
        // board builds itself as soon as its component is added, which can be
        // before the CanvasScaler has given the canvas its real size, and the
        // logs have to agree with the lanes about where the row's two ends
        // are on every screen.
        void PlaceSharedLogs(float rowWidth)
        {
            var half = rowWidth / 2f;

            _startLog.rectTransform.anchoredPosition = new Vector2(
                -half + SafeMargin + GameBoardLaneView.LaneGutterWidth + GameBoardLaneView.LaneGutterGap,
                0f);

            _endLog.rectTransform.anchoredPosition = new Vector2(half - SafeMargin, 0f);
        }

        // One shared log — LogWidth across, SharedLogHeight tall, vertically
        // centred on the pond and so on the lane stack the pond centres too.
        // Every lane's centre line crosses it, which is what lets a frog on a
        // log still sit on its own lane's line.
        //
        // One image, and one word written along the top of it. The log used to
        // be two images, a `LogEdge` rim around a fill inset by TrackOutline;
        // the rim is gone (#301) and so is the inset, so the log's own fill is
        // the drawing and it reaches the log's full size. `TrackOutline` is
        // the lily pad's now — the log is no longer one of the elements that
        // draws one.
        //
        // `edge` is which of the log's own sides is the one placed against the
        // row's end — its left (0) or its right (1). Where that end is, is
        // PlaceSharedLogs'.
        Image BuildSharedLog(string logName, float edge, string labelText, out Text label)
        {
            var logGO = new GameObject(logName, typeof(RectTransform), typeof(Image));
            var log = logGO.GetComponent<Image>();
            log.sprite = LogSprite;
            log.type = Image.Type.Sliced;
            log.color = BoardColours.LogBrown;
            log.raycastTarget = false;

            var logRect = log.rectTransform;
            logRect.SetParent(_pondRect, worldPositionStays: false);
            logRect.anchorMin = new Vector2(0.5f, 0.5f);
            logRect.anchorMax = new Vector2(0.5f, 0.5f);
            logRect.pivot = new Vector2(edge, 0.5f);
            logRect.sizeDelta = new Vector2(LogWidth, SharedLogHeight);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            label.fontSize = (int)LogLabelSize;
            label.color = BoardColours.LogLabelInk;
            label.alignment = TextAnchor.UpperCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = labelText;

            // Pinned across the top of its own log, LogLabelTopPadding down
            // from its top edge. It stretches to the log's width rather than
            // sizing to the text, so `UpperCenter` centres the word on the log
            // instead of on itself — and it is a child of the log, so it
            // travels with it when PlaceSharedLogs moves the log on a wider
            // screen.
            var labelRect = label.rectTransform;
            labelRect.SetParent(logRect, worldPositionStays: false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(0f, LogLabelSize);
            labelRect.anchoredPosition = new Vector2(0f, -LogLabelTopPadding);

            return log;
        }

        void BuildControls()
        {
            // controls — pinned to the bottom of the *screen*, as wide as the
            // screen. A smaller `Roll` is the wrong thing to trade away, so
            // this band does not shrink.
            var controlsGO = new GameObject("Controls", typeof(RectTransform), typeof(Image));
            var controlsImage = controlsGO.GetComponent<Image>();
            controlsImage.color = BoardColours.BandFill;
            controlsImage.raycastTarget = false;

            _controlsRect = controlsImage.rectTransform;
            _controlsRect.SetParent(_rect, worldPositionStays: false);
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
            //
            // A lane is the **screen's** safe area wide, and centred on the
            // pond, which is the same thing as its chip being against the real
            // left margin and its End log against the real right one. The row
            // is a track whose job is to show how far along a lane a frog has
            // got, so it spreads to whatever width it is given and
            // LanePositionGap takes up the difference (#408). What does not
            // spread is what `header` and `controls` contain — those are
            // anchored controls, not a track.
            //
            // The logs are placed again from the same width, so the two ends
            // of the row and the two logs standing on them can never be
            // computed from different numbers.
            var turnOrder = _game.TurnOrder;
            var rowWidth = PondRowWidth;
            var laneWidth = rowWidth - (2f * SafeMargin);
            var groupTop = (turnOrder.Count * GameBoardLaneView.LaneHeight) / 2f;

            PlaceSharedLogs(rowWidth);

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
                lane.Initialize(colour, _game.NameFor(colour));

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
