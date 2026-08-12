using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameSetupScreenView.cs work around —
// so these are pulled in by explicit alias rather than a wildcard
// `using Frogs.Unity.UI;`, and a bare `Button`, `ButtonKind` or `FrogColours`
// in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using FrogColours = Frogs.Unity.UI.FrogColours;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The game over screen — docs/specs/ui/game-over.md: who won, and where
    /// everybody else got to. A full screen, not a dialog, because the game
    /// is over and there is nothing behind it worth dimming.
    ///
    /// This screen decides nothing. The winner, the standings, their order
    /// and their place numbers are all <c>core-game-end</c>'s result (#211),
    /// computed before this screen opens; <see cref="Show(FrogColour?, IReadOnlyList{StandingsRow}, IReadOnlyList{FrogColour})"/>
    /// displays them in the order it is handed. It does not sort, does not
    /// rank, does not break a tie — docs/specs/ui/game-over.md#open-questions
    /// asks whether tied unfinished frogs should be ordered some other way
    /// than sharing a place number, and that question is still open, so
    /// nothing here pre-empts it.
    ///
    /// **There is no score.** docs/specs/ui/game-over.md#invariants: "there is
    /// no score. The classroom game has no score — it has an order — and
    /// inventing one would be inventing a mechanic." A place number and a pad
    /// count are the only numbers this screen shows.
    ///
    /// The standings row is this page's own pattern, <b>not</b> the shared
    /// Player chip (docs/specs/ui/shared-components.md#player-chip). That
    /// page's "Used by" list names game-over.md, but game-over.md's own
    /// Elements section never says "chip" and its constants table has no
    /// <c>PlayerChip*</c> row: its swatch is <see cref="StandingsSwatchDiameter"/>
    /// (88 px) against the chip's 64 px, and its colour name is
    /// <see cref="StandingsNameSize"/> (52 px) against the chip's 40 px. The
    /// committed mockup draws the four rows as their own markup. This is a
    /// doc-vs-doc mismatch; game-over.md is treated as authoritative for what
    /// gets built here, being the more specific page and the one with a
    /// mockup drawing the thing directly — see this issue's PR.
    ///
    /// Composes the shared <see cref="Button"/> (#214) for both controls the
    /// same way <c>TitleScreenView</c> and <c>GameSetupScreenView</c> do:
    /// built entirely through the typed Unity API when the component first
    /// needs itself, no committed prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameOverScreenView : MonoBehaviour
    {
        // docs/specs/ui/game-over.md#named-constants.
        public const float GameOverHeadlineSize = 88f;
        public const float StandingsRowWidth = 1200f;
        public const float StandingsRowHeight = 140f;
        public const float StandingsRowGap = 24f;
        public const float StandingsRowRadius = 24f;
        public const float StandingsWinnerBorder = 6f;
        public const float StandingsPlaceSize = 56f;
        public const float StandingsSwatchDiameter = 88f;
        public const float StandingsNameSize = 52f;
        public const float StandingsProgressSize = 44f;

        // docs/specs/ui/game-over.md#named-constants — the seven rows added
        // to that table by this issue's PR. None of them is a value this
        // issue decided: the six geometry values are drawn directly in the
        // committed mockup (docs/specs/ui/mockups/game-over.html draws
        // `top:64px` on the headline, `top:250px` on the standings column,
        // `padding:0 40px` and `gap:32px` on every row, `width:80px` on the
        // place-number column, and `border:3px solid` on an ordinary row),
        // and StandingsRevealDuration was already named in the page's own
        // Behaviour prose. Distilling them into the table is what
        // docs/engineering/ui-design-process.md means by the named constants
        // being the origin rather than an afterthought.
        public const float GameOverHeadlineTop = 64f;
        public const float StandingsColumnTop = 250f;
        public const float StandingsRowPadding = 40f;
        public const float StandingsRowInnerGap = 32f;
        public const float StandingsRowBorder = 3f;
        public const float StandingsPlaceWidth = 80f;
        public const float StandingsRevealDuration = 0.4f;

        /// <summary>
        /// The bottom safe-area line the `controls` row sits on — the same
        /// 48 px margin already named on docs/specs/ui/title-screen.md and
        /// docs/specs/ui/game-setup.md, referenced rather than re-typed as a
        /// second constant for the same margin.
        /// </summary>
        public const float SafeMargin = TitleScreenView.SafeMargin;

        /// <summary>
        /// Four rows is the maximum — docs/specs/ui/game-over.md's Anchors
        /// section: "Four rows is the maximum, so the column never scrolls."
        /// Read off <c>Frogs.Core.Game</c>'s own roster ceiling rather than
        /// repeating the number, because they are the same rule: every frog
        /// that played gets a row, and a game is at most four frogs.
        /// </summary>
        public const int MaxStandingsRows = Frogs.Core.Game.MaxFrogsPerGame;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const string NoWinnerHeadline = "Game over";
        const string WinnerHeadlineSuffix = " frog wins!";
        const string HomePrefix = "Home — ";
        const string OfSeparator = " of ";
        const string PlayAgainLabel = "Play again";
        const string BackToTheTitleLabel = "Back to the title";

        // No imported font — matches Button.cs's, TitleScreenView.cs's and
        // GameSetupScreenView.cs's own choice, for the same reason (no
        // external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockup's CSS
        // custom properties — the same line Button.cs, TitleScreenView.cs and
        // GameSetupScreenView.cs draw for their own colours: not a
        // shared-components.md geometry/opacity constant, so not declared as
        // a named spec constant. The frog swatch is the exception: it is one
        // of the four shared frog-colour constants, via FrogColours.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color FaintColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockups' --faint
        static readonly Color PaperColor = Color.white; // mockups' --paper / #fff

        const float FullyOpaqueByte = 255f;

        // No imported texture, sprite, or font — docs/specs/ui/shared-components.md
        // "no external assets". Same procedural rounded-rect technique as
        // Frogs.Unity.UI.Button.CreateRoundedRectSprite and
        // GameSetupScreenView's own copy of it, for the reason Button.cs
        // records: Resources.GetBuiltinResource<Sprite> for Unity's own UI
        // skin returns null on this Unity version, silently, so a procedural
        // shape is the option that does not depend on a version's internal
        // resource catalogue at all.
        static Sprite s_rowSprite;
        static Sprite s_swatchSprite;

        static Sprite RowSprite
        {
            get
            {
                if (s_rowSprite == null)
                {
                    s_rowSprite = CreateRoundedRectSprite(Mathf.RoundToInt(StandingsRowRadius));
                }

                return s_rowSprite;
            }
        }

        static Sprite SwatchSprite
        {
            get
            {
                if (s_swatchSprite == null)
                {
                    s_swatchSprite = CreateRoundedRectSprite(Mathf.RoundToInt(StandingsSwatchDiameter / 2f));
                }

                return s_swatchSprite;
            }
        }

        static Sprite CreateRoundedRectSprite(int radius)
        {
            radius = Mathf.Max(radius, 1);
            var size = radius * 2 + 1;
            var half = size / 2f;
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    var dx = Mathf.Max(Mathf.Abs(px) - (half - radius), 0f);
                    var dy = Mathf.Max(Mathf.Abs(py) - (half - radius), 0f);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    var alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)(alpha * FullyOpaqueByte));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Frogs Game Over Rounded Rect (procedural)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            var rect = new Rect(0f, 0f, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            var border = new Vector4(radius, radius, radius, radius);

            // Matches CanvasScaler's default referencePixelsPerUnit, so the
            // sliced border renders as exactly `radius` UI pixels — the same
            // 1:1 pixel-to-unit mapping every other constant on this screen
            // already assumes.
            const float pixelsPerUnit = 100f;
            const uint extrude = 0;
            return Sprite.Create(texture, rect, pivot, pixelsPerUnit, extrude, SpriteMeshType.FullRect, border);
        }

        RectTransform _rect;
        RectTransform _contentRect;
        Image _background;

        RectTransform _headlineRect;
        Text _headlineText;

        RectTransform _standingsRect;
        readonly List<Row> _rows = new List<Row>();

        RectTransform _controlsRect;
        Button _backToTheTitleButton;
        Button _playAgainButton;

        FrogColour? _winner;
        readonly List<StandingsRow> _standings = new List<StandingsRow>();
        readonly List<FrogColour> _turnOrder = new List<FrogColour>();

        bool _initialized;
        float _revealElapsed;

        ScreenRouter _router;
        Func<ulong> _seedFactory = DefaultSeedFactory;

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

        /// <summary>The `headline` region's <see cref="RectTransform"/> — centred, pinned <see cref="GameOverHeadlineTop"/> below the top.</summary>
        public RectTransform HeadlineRect
        {
            get
            {
                EnsureInitialized();
                return _headlineRect;
            }
        }

        /// <summary>The `headline` region: the winner, or `Game over`.</summary>
        public Text HeadlineText
        {
            get
            {
                EnsureInitialized();
                return _headlineText;
            }
        }

        /// <summary>The `standings` region's <see cref="RectTransform"/> — the centred column every row sits in.</summary>
        public RectTransform StandingsRect
        {
            get
            {
                EnsureInitialized();
                return _standingsRect;
            }
        }

        /// <summary>The `controls` region's <see cref="RectTransform"/>.</summary>
        public RectTransform ControlsRect
        {
            get
            {
                EnsureInitialized();
                return _controlsRect;
            }
        }

        /// <summary>`Back to the title` — secondary; returns to the title screen.</summary>
        public Button BackToTheTitleButton
        {
            get
            {
                EnsureInitialized();
                return _backToTheTitleButton;
            }
        }

        /// <summary>`Play again` — primary; same frogs, same turn order, straight to the board.</summary>
        public Button PlayAgainButton
        {
            get
            {
                EnsureInitialized();
                return _playAgainButton;
            }
        }

        /// <summary>
        /// The clear space between the two controls. Neither is destructive —
        /// there is no confirm-guarded action on this screen at all — so this
        /// is the ordinary <c>ButtonGap</c>-or-greater spacing the mockup's
        /// left/right placement produces, never <c>ButtonDestructiveGap</c>.
        /// </summary>
        public float ControlsGap
        {
            get
            {
                EnsureInitialized();

                var occupied = SafeMargin
                    + _backToTheTitleButton.RectTransform.sizeDelta.x
                    + _playAgainButton.RectTransform.sizeDelta.x
                    + SafeMargin;

                return CanvasWidth - occupied;
            }
        }

        /// <summary>How many frogs are in the standings — one row per frog that played.</summary>
        public int RowCount
        {
            get
            {
                EnsureInitialized();
                return _standings.Count;
            }
        }

        /// <summary>How many row objects are actually laid out. Never more than <see cref="RowCount"/>.</summary>
        public int ActiveRowCount
        {
            get
            {
                EnsureInitialized();

                var count = 0;
                foreach (var row in _rows)
                {
                    if (row.Rect.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// The <see cref="Frogs.Core.Game"/> the last <see cref="PlayAgain"/>
        /// created, or null before the first one — the same shape
        /// <c>GameSetupScreenView.StartedGame</c> exposes for `Start`.
        /// </summary>
        public Frogs.Core.Game StartedGame { get; private set; }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            AdvanceReveal(Time.deltaTime);
        }

        /// <summary>
        /// Wires the screen to the router both controls navigate through, and
        /// to the source `Play again` reads a new game's seed from. Call once,
        /// after the view exists — the same shape as
        /// <c>GameSetupScreenView.Initialize</c>.
        /// </summary>
        /// <param name="seedFactory">
        /// Where the next <see cref="Frogs.Core.Game"/>'s seed comes from.
        /// Defaults to the system clock, for the reason
        /// <c>GameSetupScreenView.Initialize</c> records: the Unity layer is
        /// where a brand-new game's real-world entropy is expected to enter
        /// the system, and <c>Frogs.Core</c> still never reads a clock.
        /// </param>
        public void Initialize(ScreenRouter router, Func<ulong> seedFactory = null)
        {
            EnsureInitialized();

            _router = router ?? throw new ArgumentNullException(nameof(router));
            _seedFactory = seedFactory ?? DefaultSeedFactory;
        }

        /// <summary>
        /// Shows the result of <paramref name="endedGame"/> — its winner, its
        /// standings and its roster, read straight off the three facts
        /// <c>core-game-end</c> (#211) already computed. Nothing is
        /// recomputed here, and in particular the roster is read as the
        /// roster, not reconstructed from the standings' finishing order:
        /// those two differ the moment any frog overtakes another.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="endedGame"/> is null.</exception>
        public void Show(Frogs.Core.Game endedGame)
        {
            if (endedGame == null)
            {
                throw new ArgumentNullException(nameof(endedGame));
            }

            Show(endedGame.Winner, endedGame.Standings, endedGame.TurnOrder);
        }

        /// <summary>
        /// Shows one game-end result: <paramref name="winner"/> is Core's own
        /// answer to "did anyone finish, and if so who was first" (null when
        /// the game was ended before anybody got home),
        /// <paramref name="standings"/> is the ordered list of every frog that
        /// played, and <paramref name="turnOrder"/> is the ended game's
        /// roster — the order <see cref="PlayAgain"/> starts the next game in.
        ///
        /// The three are taken separately, and kept separate, because that is
        /// what stops the screen inferring one from another: the winner is
        /// never "whoever is first in the list", and turn order is never "the
        /// order they finished in".
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="standings"/> or <paramref name="turnOrder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="standings"/> has more than <see cref="MaxStandingsRows"/> rows.</exception>
        public void Show(FrogColour? winner, IReadOnlyList<StandingsRow> standings, IReadOnlyList<FrogColour> turnOrder)
        {
            EnsureInitialized();

            if (standings == null)
            {
                throw new ArgumentNullException(nameof(standings));
            }

            if (turnOrder == null)
            {
                throw new ArgumentNullException(nameof(turnOrder));
            }

            if (standings.Count > MaxStandingsRows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(standings),
                    standings.Count,
                    $"a game is at most {MaxStandingsRows} frogs, so the standings never has more rows than that.");
            }

            _winner = winner;

            _standings.Clear();
            foreach (var row in standings)
            {
                _standings.Add(row);
            }

            _turnOrder.Clear();
            foreach (var colour in turnOrder)
            {
                _turnOrder.Add(colour);
            }

            // Entering: the reveal starts over for the result now on screen.
            _revealElapsed = 0f;

            RefreshAll();
        }

        /// <summary>The frog row <paramref name="index"/> is about, in the order Core handed the standings over.</summary>
        public FrogColour RowColour(int index) => StandingsAt(index).Colour;

        /// <summary>Row <paramref name="index"/>'s own <see cref="RectTransform"/>.</summary>
        public RectTransform RowRect(int index) => RowAt(index).Rect;

        /// <summary>Row <paramref name="index"/>'s place number.</summary>
        public Text RowPlaceText(int index) => RowAt(index).Place;

        /// <summary>Row <paramref name="index"/>'s frog swatch — a flat circle, never an imported sprite.</summary>
        public Image RowSwatch(int index) => RowAt(index).Swatch;

        /// <summary>Row <paramref name="index"/>'s colour name.</summary>
        public Text RowNameText(int index) => RowAt(index).Name;

        /// <summary>Row <paramref name="index"/>'s progress readout — `Home — 8 of 8`, or `6 of 8`.</summary>
        public Text RowProgressText(int index) => RowAt(index).Progress;

        /// <summary>Row <paramref name="index"/>'s border colour.</summary>
        public Color RowBorderColour(int index) => RowAt(index).Border.color;

        /// <summary>
        /// Row <paramref name="index"/>'s border width —
        /// <see cref="StandingsWinnerBorder"/> on the winner's row,
        /// <see cref="StandingsRowBorder"/> on every other, finishers
        /// included.
        /// </summary>
        public float RowBorderWidth(int index) => RowAt(index).BorderWidth;

        /// <summary>
        /// Whether row <paramref name="index"/> is the winner's — true only
        /// for the row whose colour matches the <c>Winner</c> Core reported,
        /// and never for any row when Core reported none. This is not "is
        /// this frog home" and not "is this the first row": play continues
        /// after the first frog gets home, so a finished game can have
        /// several finishers and only one winner.
        /// </summary>
        public bool IsWinnerRow(int index)
        {
            var row = StandingsAt(index);
            return _winner.HasValue && row.Colour == _winner.Value;
        }

        /// <summary>How far through its entry reveal row <paramref name="index"/> is, 0 to 1.</summary>
        public float RowRevealAlpha(int index) => RowAt(index).CanvasGroup.alpha;

        /// <summary>
        /// Advances the entering reveal by <paramref name="deltaSeconds"/>,
        /// clamped to <see cref="StandingsRevealDuration"/>. A public method
        /// of its own, rather than reachable only through <see cref="Update"/>,
        /// so an EditMode test can simulate elapsed time directly — the same
        /// reasoning as <c>TitleScreenView.AdvanceFade</c>.
        /// </summary>
        public void AdvanceReveal(float deltaSeconds)
        {
            EnsureInitialized();

            _revealElapsed = Mathf.Clamp(
                _revealElapsed + Mathf.Max(deltaSeconds, 0f),
                0f,
                StandingsRevealDuration);

            ApplyReveal();
        }

        /// <summary>
        /// Starts a new game with the same frogs in the same turn order,
        /// straight to the game board with everyone back on their Start log —
        /// docs/specs/ui/game-over.md's Elements section. It skips
        /// [game setup](docs/specs/ui/game-setup.md) entirely, which the page
        /// calls out as deliberate: "the overwhelmingly common case is the
        /// same four children going again, and making them re-tap their
        /// colours every time is a tax on the thing they most want to do."
        ///
        /// The turn order is the ended game's roster, exactly as handed to
        /// <see cref="Show(FrogColour?, IReadOnlyList{StandingsRow}, IReadOnlyList{FrogColour})"/>
        /// — never the standings' finishing order, which differs from it
        /// whenever a frog overtook another during play.
        ///
        /// A no-op before a result has been shown: with no ended game there
        /// is no roster to reuse, and inventing one would be inventing the
        /// game. Nothing navigates to this screen without a result, so that
        /// case is a wiring mistake, not a state a player can reach.
        /// </summary>
        public void PlayAgain()
        {
            EnsureInitialized();

            if (_turnOrder.Count < Frogs.Core.Game.MinFrogsPerGame)
            {
                return;
            }

            // Constructed exactly the way `Start` on game setup constructs
            // one — the same roster order in, a fresh seed from the Unity
            // layer — just without the tap-through.
            StartedGame = new Frogs.Core.Game(_turnOrder.ToArray(), _seedFactory());

            _router?.NavigateToScreen(CoreScreen.GameBoard);
        }

        /// <summary>
        /// Returns to the title screen. The one navigation action this screen
        /// exposes for that: the `Back to the title` button invokes it, and
        /// the screen router's own `GameOver` → <c>HandleBack()</c> case
        /// (#213) already routes hardware back to the same destination. This
        /// screen deliberately adds no second hardware-back handler of its
        /// own.
        ///
        /// Nothing confirms first. docs/specs/ui/game-over.md#behaviour: "a
        /// game that has been ended deliberately is not an accident to guard
        /// against twice."
        /// </summary>
        public void BackToTheTitle()
        {
            EnsureInitialized();

            _router?.NavigateToScreen(CoreScreen.TitleScreen);
        }

        /// <summary>
        /// The `headline` region's line: `&lt;Colour&gt; frog wins!` when Core
        /// reports a winner — on both routes that produce one — and
        /// `Game over` when it does not, because "announcing a winner who did
        /// not win is worse than announcing nobody".
        /// </summary>
        public static string FormatHeadline(FrogColour? winner)
        {
            return winner.HasValue
                ? winner.Value.ToString() + WinnerHeadlineSuffix
                : NoWinnerHeadline;
        }

        /// <summary>
        /// One row's progress readout: `Home — 8 of 8` for a frog Core
        /// reports as home, `6 of 8` for one still swimming. The denominator
        /// is <see cref="Lane.LaneWinningPosition"/> (8) and only that —
        /// <see cref="Lane.LanePositionCount"/> (9) counts the Start log too
        /// and would print `of 9`, which the mockup never does.
        /// docs/specs/ui/game-board.md: LaneWinningPosition "is what the
        /// `of 8` in every chip's pad count refers to."
        ///
        /// Keyed off the row's own two facts and nothing else — never off
        /// where the row sits in the list.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
        public static string FormatProgress(StandingsRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            var padCount = row.Position.ToString() + OfSeparator + Lane.LaneWinningPosition.ToString();

            return row.IsHome ? HomePrefix + padCount : padCount;
        }

        StandingsRow StandingsAt(int index)
        {
            EnsureInitialized();

            if (index < 0 || index >= _standings.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"the standings has {_standings.Count} row(s).");
            }

            return _standings[index];
        }

        Row RowAt(int index)
        {
            StandingsAt(index);
            return _rows[index];
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as ScreenRouterAdapter, Button, TitleScreenView and
        // GameSetupScreenView's own EnsureInitialized. Every public entry
        // point funnels through this idempotent guard instead of trusting
        // Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            RefreshAll();
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
            // Only the background hangs off it. Everything laid out in
            // reference pixels hangs off `Content` instead, so the extra space
            // a wider or taller device gives us is painted and nothing else.
            StretchToFill(_rect);

            BuildBackground();
            BuildContent();

            BuildHeadline();
            BuildStandings();
            BuildControls();
        }

        void BuildBackground()
        {
            // The paint that reaches the edge of the screen on any aspect
            // ratio — the mockups' `--bg`, which every mockup sets on its
            // 1920 x 1200 frame and which nothing here painted until now.
            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.color = ScreenColours.Background;
            _background.raycastTarget = false;

            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(backgroundRect);
        }

        void BuildContent()
        {
            // The reference canvas, centred — exactly the rect the root used
            // to be, so every child below keeps the anchors, sizes and offsets
            // it already had and nothing on this screen moves by a pixel.
            var contentGO = new GameObject("Content", typeof(RectTransform));
            _contentRect = (RectTransform)contentGO.transform;
            _contentRect.SetParent(_rect, worldPositionStays: false);
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _contentRect.anchoredPosition = Vector2.zero;
        }

        void BuildHeadline()
        {
            // headline — centred, pinned GameOverHeadlineTop below the top of
            // the canvas.
            var headlineGO = new GameObject("Headline", typeof(RectTransform), typeof(Text));
            _headlineText = headlineGO.GetComponent<Text>();
            _headlineText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _headlineText.fontSize = (int)GameOverHeadlineSize;
            _headlineText.fontStyle = FontStyle.Bold;
            _headlineText.alignment = TextAnchor.UpperCenter;
            _headlineText.color = InkColor;
            _headlineText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _headlineText.verticalOverflow = VerticalWrapMode.Overflow;
            _headlineText.raycastTarget = false;

            _headlineRect = _headlineText.rectTransform;
            _headlineRect.SetParent(_contentRect, worldPositionStays: false);
            _headlineRect.anchorMin = new Vector2(0.5f, 1f);
            _headlineRect.anchorMax = new Vector2(0.5f, 1f);
            _headlineRect.pivot = new Vector2(0.5f, 1f);
            _headlineRect.sizeDelta = Vector2.zero;
            _headlineRect.anchoredPosition = new Vector2(0f, -GameOverHeadlineTop);
        }

        void BuildStandings()
        {
            // standings — a centred column StandingsRowWidth wide, its top
            // StandingsColumnTop below the top of the canvas, one row per
            // frog with StandingsRowGap between rows.
            var standingsGO = new GameObject("Standings", typeof(RectTransform));
            _standingsRect = (RectTransform)standingsGO.transform;
            _standingsRect.SetParent(_contentRect, worldPositionStays: false);
            _standingsRect.anchorMin = new Vector2(0.5f, 1f);
            _standingsRect.anchorMax = new Vector2(0.5f, 1f);
            _standingsRect.pivot = new Vector2(0.5f, 1f);
            _standingsRect.sizeDelta = new Vector2(StandingsRowWidth, 0f);
            _standingsRect.anchoredPosition = new Vector2(0f, -StandingsColumnTop);

            // Every row a game can have is built once, and the ones this
            // result does not need are simply not laid out. Four is the
            // ceiling, so the column never scrolls and nothing is created or
            // destroyed when a second result is shown.
            for (var index = 0; index < MaxStandingsRows; index++)
            {
                _rows.Add(BuildRow(index));
            }
        }

        Row BuildRow(int index)
        {
            var rowGO = new GameObject("Row " + index.ToString(), typeof(RectTransform), typeof(CanvasGroup));
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.SetParent(_standingsRect, worldPositionStays: false);
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(StandingsRowWidth, StandingsRowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -(index * (StandingsRowHeight + StandingsRowGap)));

            var canvasGroup = rowGO.GetComponent<CanvasGroup>();

            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var border = borderGO.GetComponent<Image>();
            border.sprite = RowSprite;
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;
            var borderRect = border.rectTransform;
            borderRect.SetParent(rowRect, worldPositionStays: false);
            StretchToFill(borderRect);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = RowSprite;
            fill.type = Image.Type.Sliced;
            fill.color = PaperColor;
            fill.raycastTarget = false;
            var fillRect = fill.rectTransform;
            fillRect.SetParent(rowRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;

            // Place number, swatch, colour name, progress readout — left to
            // right, StandingsRowPadding in from each end and
            // StandingsRowInnerGap apart.
            var placeX = StandingsRowPadding;
            var swatchX = placeX + StandingsPlaceWidth + StandingsRowInnerGap;
            var nameX = swatchX + StandingsSwatchDiameter + StandingsRowInnerGap;

            var place = BuildRowText("Place", rowRect, StandingsPlaceSize, TextAnchor.MiddleLeft, InkColor);
            place.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            place.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            place.rectTransform.pivot = new Vector2(0f, 0.5f);
            place.rectTransform.sizeDelta = new Vector2(StandingsPlaceWidth, StandingsRowHeight);
            place.rectTransform.anchoredPosition = new Vector2(placeX, 0f);

            var swatchGO = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            var swatch = swatchGO.GetComponent<Image>();
            swatch.sprite = SwatchSprite;
            swatch.type = Image.Type.Sliced;
            swatch.raycastTarget = false;
            var swatchRect = swatch.rectTransform;
            swatchRect.SetParent(rowRect, worldPositionStays: false);
            swatchRect.anchorMin = new Vector2(0f, 0.5f);
            swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.pivot = new Vector2(0f, 0.5f);
            swatchRect.sizeDelta = new Vector2(StandingsSwatchDiameter, StandingsSwatchDiameter);
            swatchRect.anchoredPosition = new Vector2(swatchX, 0f);

            var colourName = BuildRowText("Name", rowRect, StandingsNameSize, TextAnchor.MiddleLeft, InkColor);
            colourName.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            colourName.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            colourName.rectTransform.pivot = new Vector2(0f, 0.5f);
            colourName.rectTransform.sizeDelta = Vector2.zero;
            colourName.rectTransform.anchoredPosition = new Vector2(nameX, 0f);

            var progress = BuildRowText("Progress", rowRect, StandingsProgressSize, TextAnchor.MiddleRight, LineColor);
            progress.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            progress.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            progress.rectTransform.pivot = new Vector2(1f, 0.5f);
            progress.rectTransform.sizeDelta = Vector2.zero;
            progress.rectTransform.anchoredPosition = new Vector2(-StandingsRowPadding, 0f);

            return new Row
            {
                Rect = rowRect,
                CanvasGroup = canvasGroup,
                Border = border,
                Fill = fill,
                Place = place,
                Swatch = swatch,
                Name = colourName,
                Progress = progress
            };
        }

        static Text BuildRowText(string objectName, RectTransform parent, float size, TextAnchor alignment, Color colour)
        {
            var textGO = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            text.fontSize = (int)size;
            text.alignment = alignment;
            text.color = colour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.SetParent(parent, worldPositionStays: false);

            return text;
        }

        void BuildControls()
        {
            // controls — Back to the title at the left, Play again at the
            // right, both SafeMargin in from their edge and up from the
            // bottom safe-area line.
            var controlsGO = new GameObject("Controls", typeof(RectTransform));
            _controlsRect = (RectTransform)controlsGO.transform;
            _controlsRect.SetParent(_contentRect, worldPositionStays: false);
            StretchToFill(_controlsRect);

            _backToTheTitleButton = CreateControlButton("BackToTheTitle", ButtonKind.Secondary, BackToTheTitleLabel);
            _backToTheTitleButton.RectTransform.anchorMin = new Vector2(0f, 0f);
            _backToTheTitleButton.RectTransform.anchorMax = new Vector2(0f, 0f);
            _backToTheTitleButton.RectTransform.pivot = new Vector2(0f, 0f);
            _backToTheTitleButton.RectTransform.anchoredPosition = new Vector2(SafeMargin, SafeMargin);
            _backToTheTitleButton.Clicked += BackToTheTitle;

            _playAgainButton = CreateControlButton("PlayAgain", ButtonKind.Primary, PlayAgainLabel);
            _playAgainButton.RectTransform.anchorMin = new Vector2(1f, 0f);
            _playAgainButton.RectTransform.anchorMax = new Vector2(1f, 0f);
            _playAgainButton.RectTransform.pivot = new Vector2(1f, 0f);
            _playAgainButton.RectTransform.anchoredPosition = new Vector2(-SafeMargin, SafeMargin);
            _playAgainButton.Clicked += PlayAgain;
        }

        Button CreateControlButton(string name, ButtonKind kind, string label)
        {
            var buttonGO = new GameObject(name, typeof(RectTransform));
            buttonGO.transform.SetParent(_controlsRect, worldPositionStays: false);

            var button = buttonGO.AddComponent<Button>();
            button.SetKind(kind);
            button.SetLabelText(label);

            return button;
        }

        void RefreshAll()
        {
            _headlineText.text = FormatHeadline(_winner);

            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index];
                var isShown = index < _standings.Count;

                row.Rect.gameObject.SetActive(isShown);

                if (isShown)
                {
                    ApplyRow(row, _standings[index], IsWinnerRow(index));
                }
            }

            ApplyReveal();
        }

        void ApplyRow(Row row, StandingsRow standings, bool isWinner)
        {
            // Exactly one visual distinction between places, and it tracks
            // who Core names as the winner: the heavier border the spec calls
            // out, plus the heavier place and name the mockup draws on that
            // same row — "drawn heavier" is the umbrella term for both. Font
            // weight is an ungated restyle under ui-design-process.md's "What
            // isn't gated", so it carries no named constant of its own.
            //
            // Every other row is drawn identically to every other, because
            // "second place and last place are the same kind of fact" — and a
            // non-winning finisher is one of those other rows.
            var borderWidth = isWinner ? StandingsWinnerBorder : StandingsRowBorder;

            row.BorderWidth = borderWidth;
            row.Border.color = isWinner ? InkColor : FaintColor;
            row.Fill.rectTransform.offsetMin = new Vector2(borderWidth, borderWidth);
            row.Fill.rectTransform.offsetMax = new Vector2(-borderWidth, -borderWidth);

            var weight = isWinner ? FontStyle.Bold : FontStyle.Normal;

            row.Place.text = standings.Place.ToString();
            row.Place.fontStyle = weight;

            row.Swatch.color = FrogColours.For(standings.Colour);

            row.Name.text = standings.Colour.ToString();
            row.Name.fontStyle = weight;

            row.Progress.text = FormatProgress(standings);
        }

        // Rows appear in place, top to bottom, over StandingsRevealDuration
        // in total — docs/specs/ui/game-over.md#behaviour. The total is
        // divided across however many rows are on screen rather than each row
        // taking a fixed slice of its own: the row count varies, and a
        // per-row constant would make the whole sequence take longer with
        // four frogs than with two, which is not what the page says.
        void ApplyReveal()
        {
            if (_standings.Count == 0)
            {
                return;
            }

            var slot = StandingsRevealDuration / _standings.Count;

            for (var index = 0; index < _standings.Count; index++)
            {
                _rows[index].CanvasGroup.alpha = Mathf.Clamp01((_revealElapsed - (index * slot)) / slot);
            }
        }

        // Frogs.Core.Rng is seeded and, by its own contract, is "never
        // [built] from the clock, and never from nothing" — but that rule is
        // about Frogs.Core reading the clock itself, which it never does.
        // This Unity-layer boundary is where a brand-new game's real-world
        // entropy enters, exactly as GameSetupScreenView's own `Start` does.
        static ulong DefaultSeedFactory()
        {
            return unchecked((ulong)DateTime.UtcNow.Ticks);
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// One standings row's pieces — a plain data holder, not a
        /// <see cref="MonoBehaviour"/>, the same shape as
        /// <c>GameSetupScreenView.Seat</c>.
        /// </summary>
        sealed class Row
        {
            public RectTransform Rect;
            public CanvasGroup CanvasGroup;
            public Image Border;
            public Image Fill;
            public Text Place;
            public Image Swatch;
            public Text Name;
            public Text Progress;
            public float BorderWidth;
        }
    }
}
