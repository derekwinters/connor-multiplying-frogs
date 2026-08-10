using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs and TitleScreenView.cs work around — so these are pulled in
// by explicit alias rather than a wildcard `using Frogs.Unity.UI;`, and a
// bare `Button`, `ButtonKind` or `FrogColours` in this file always means the
// shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using FrogColours = Frogs.Unity.UI.FrogColours;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The game setup screen — docs/specs/ui/game-setup.md: four frog seats,
    /// tapped in the order a family wants to play, then `Start`. Built
    /// directly from that page's own "Frog seat" element and its own named
    /// constants — <b>not</b> the shared Player chip
    /// (docs/specs/ui/shared-components.md#player-chip). That component's
    /// issue (#219) is not built yet, and shared-components.md's claim that
    /// game setup uses it contradicts game-setup.md's own Elements section,
    /// which defines a separate "Frog seat" with its own constants
    /// (<c>Seat*</c>) and never mentions the chip — a known, deliberately
    /// unresolved contradiction flagged during #214's work. This issue's own
    /// checklist and named-constants list only ever reference the seat, so
    /// that is what this type builds to; see this issue's PR.
    ///
    /// Composes the shared <see cref="Button"/> (#214) for `Back`/`Start` the
    /// same way <c>TitleScreenView</c> does: built entirely through the typed
    /// Unity API when the component first needs itself, no committed prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameSetupScreenView : MonoBehaviour
    {
        // docs/specs/ui/game-setup.md#named-constants.
        public const float SafeMargin = 48f;
        public const float SetupHeaderSize = 72f;
        public const float SeatWidth = 360f;
        public const float SeatHeight = 440f;
        public const float SeatGap = 48f;
        public const float SeatRadius = 32f;
        public const float SeatSwatchDiameter = 200f;
        public const float SeatLabelSize = 48f;
        public const float SeatChosenRing = 8f;
        public const float HintGap = 56f;
        public const float SetupHintSize = 36f;
        public const float SeatOrderBadge = 72f;

        // docs/specs/ui/game-setup.md#named-constants — added by this issue's
        // PR, distilling values already stated in the Invariants section
        // (SeatContentGap, SeatBadgeInset) and already drawn in the
        // committed mockup: docs/specs/ui/mockups/game-setup.html draws
        // every seat column with `gap:32px` between the swatch and the
        // content below it, and the turn-order badge at a fixed
        // `left:24px; top:24px` inset from the seat's own corner.
        public const float SeatContentGap = 32f;
        public const float SeatBadgeInset = 24f;

        // docs/specs/ui/game-setup.md#named-constants — added by this
        // issue's PR, distilling the Invariants section's prose: "a game
        // cannot start with fewer than two frogs or more than four." Read
        // directly off Frogs.Core.Game's own constants (core-game-turns,
        // #208) rather than repeating the literal 2/4 a second time — both
        // name the exact same rule, under the name this screen's own spec
        // page uses.
        public const int GameSetupMinFrogs = Frogs.Core.Game.MinFrogsPerGame;
        public const int GameSetupMaxFrogs = Frogs.Core.Game.MaxFrogsPerGame;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const string HeaderLabel = "Who is playing?";
        const string EmptySeatLabel = "Tap to play";
        const string BackLabel = "Back";
        const string StartLabel = "Start";
        const string HintDisabledText = "Pick two to four frogs";
        const string HintGoesFirstSuffix = " goes first";

        // No imported font — matches Button.cs's and TitleScreenView.cs's
        // own choice, for the same reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // The badge digit's label size. Drawn in the committed mockup at
        // 38px (`font-size:38px;font-weight:700` on the badge circle), but
        // not a geometry/tuning value with a named row of its own on
        // game-setup.md's constants table — the same reasoning Button.cs
        // gives for `PressedDarkenFactor`: a rendering detail the spec
        // leaves to presentation (ADR-0001), not a size this issue's own
        // constants-gap audit found missing.
        const float SeatBadgeLabelSize = 38f;

        // The empty seat's outline stroke width. The mockup draws it at 6px
        // (`border:6px dashed`), but — like SeatBadgeLabelSize above — this
        // is not named on game-setup.md's constants table, and this issue's
        // own gap audit names exactly four missing constants, none of them
        // this one. Kept as a private presentation constant rather than a
        // bare literal, for the same reason ButtonBorderWidth style choices
        // are kept local when the spec does not name them.
        const float SeatEmptyBorderWidth = 6f;

        static readonly FrogColour[] SeatOrder =
        {
            FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink
        };

        // Chrome colours copied verbatim from the committed mockup's CSS
        // custom properties — the same line Button.cs and TitleScreenView.cs
        // draw for their own colours: not a shared-components.md
        // geometry/opacity constant, so not declared as a named spec
        // constant.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color FaintColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockups' --faint
        static readonly Color PaperColor = Color.white; // mockups' --paper / #fff
        static readonly Color EmptySwatchColor = new Color32(0xDD, 0xE4, 0xE1, 0xFF); // mockup's empty-seat swatch fill
        static readonly Color EmptyLabelColor = new Color32(0x8C, 0x97, 0x93, 0xFF); // mockup's "Tap to play" colour
        static readonly Color NoFillColor = new Color(1f, 1f, 1f, 0f); // "background:transparent" — fully transparent

        const float FullyOpaqueByte = 255f;

        // No imported texture, sprite, or font — docs/specs/ui/shared-components.md
        // "no external assets". Same procedural rounded-rect technique as
        // Frogs.Unity.UI.Button.CreateRoundedRectSprite, duplicated locally
        // rather than exposing that private helper: touching #214's merged
        // Button.cs is not this issue's to do as a side effect of its own
        // shape needs — see this issue's PR.
        static Sprite s_panelSprite;
        static Sprite s_swatchSprite;
        static Sprite s_badgeSprite;

        static Sprite PanelSprite
        {
            get
            {
                if (s_panelSprite == null)
                {
                    s_panelSprite = CreateRoundedRectSprite(Mathf.RoundToInt(SeatRadius));
                }

                return s_panelSprite;
            }
        }

        static Sprite SwatchSprite
        {
            get
            {
                if (s_swatchSprite == null)
                {
                    s_swatchSprite = CreateRoundedRectSprite(Mathf.RoundToInt(SeatSwatchDiameter / 2f));
                }

                return s_swatchSprite;
            }
        }

        static Sprite BadgeSprite
        {
            get
            {
                if (s_badgeSprite == null)
                {
                    s_badgeSprite = CreateRoundedRectSprite(Mathf.RoundToInt(SeatOrderBadge / 2f));
                }

                return s_badgeSprite;
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
                name = "Frogs Game Setup Rounded Rect (procedural)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            var rect = new Rect(0f, 0f, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            var border = new Vector4(radius, radius, radius, radius);

            const float pixelsPerUnit = 100f;
            const uint extrude = 0;
            return Sprite.Create(texture, rect, pivot, pixelsPerUnit, extrude, SpriteMeshType.FullRect, border);
        }

        RectTransform _rect;

        RectTransform _headerRect;
        Text _headerText;

        RectTransform _seatsRect;
        readonly Dictionary<FrogColour, Seat> _seats = new Dictionary<FrogColour, Seat>();

        RectTransform _hintRect;
        Text _hintText;

        RectTransform _controlsRect;
        Button _backButton;
        Button _startButton;

        readonly List<FrogColour> _chosenOrder = new List<FrogColour>();

        bool _initialized;
        ScreenRouter _router;
        Func<ulong> _seedFactory = DefaultSeedFactory;

        /// <summary>The screen's own <see cref="RectTransform"/>, sized to the full 1920 x 1200 reference canvas.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The `header` region's <see cref="RectTransform"/>.</summary>
        public RectTransform HeaderRect
        {
            get
            {
                EnsureInitialized();
                return _headerRect;
            }
        }

        /// <summary>The `header` region: "Who is playing?"</summary>
        public Text HeaderText
        {
            get
            {
                EnsureInitialized();
                return _headerText;
            }
        }

        /// <summary>The `seats` region's <see cref="RectTransform"/> — the row all four seats sit in.</summary>
        public RectTransform SeatsRect
        {
            get
            {
                EnsureInitialized();
                return _seatsRect;
            }
        }

        /// <summary>The `hint` region's <see cref="RectTransform"/>.</summary>
        public RectTransform HintRect
        {
            get
            {
                EnsureInitialized();
                return _hintRect;
            }
        }

        /// <summary>The `hint` region: one line, always present.</summary>
        public Text HintText
        {
            get
            {
                EnsureInitialized();
                return _hintText;
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

        /// <summary>`Back` — secondary; returns to the title screen.</summary>
        public Button BackButton
        {
            get
            {
                EnsureInitialized();
                return _backButton;
            }
        }

        /// <summary>`Start` — primary; disabled below <see cref="GameSetupMinFrogs"/>.</summary>
        public Button StartButton
        {
            get
            {
                EnsureInitialized();
                return _startButton;
            }
        }

        /// <summary>
        /// Tap order, first tapped to last — the badge order <see cref="Start"/>
        /// hands to Core. Empty on a clean slate.
        /// </summary>
        public IReadOnlyList<FrogColour> ChosenOrder
        {
            get
            {
                EnsureInitialized();
                return _chosenOrder;
            }
        }

        /// <summary>
        /// The <see cref="Frogs.Core.Game"/> the last successful `Start`
        /// created, or null before the first one. Exposed for tests and for
        /// whatever screen eventually reads a live game off the board — see
        /// this issue's PR under "Deviations and Decisions" for what is and
        /// is not wired yet.
        /// </summary>
        public Frogs.Core.Game StartedGame { get; private set; }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Wires the screen to the router `Back`/`Start` navigate through,
        /// and to the source `Start` reads a new game's seed from. Call once,
        /// after the view exists — the same shape as <see cref="Button.SetKind"/>
        /// being called right after <c>AddComponent</c>, or
        /// <c>TitleScreenView.Initialize</c>.
        /// </summary>
        /// <param name="seedFactory">
        /// Where a new <see cref="Frogs.Core.Game"/>'s seed comes from.
        /// Defaults to the system clock. <see cref="Rng"/>'s own contract is
        /// that it is "never [built] from the clock, and never from nothing"
        /// — but that is a rule about <c>Frogs.Core</c> reading the clock
        /// itself, which it never does; this Unity-layer boundary is exactly
        /// where a brand-new, unsaved game's real-world entropy is expected
        /// to enter the system. Overridable so a test can pin the seed a
        /// started <see cref="Frogs.Core.Game"/> is asserted against.
        /// </param>
        public void Initialize(ScreenRouter router, Func<ulong> seedFactory = null)
        {
            EnsureInitialized();

            _router = router ?? throw new ArgumentNullException(nameof(router));
            _seedFactory = seedFactory ?? DefaultSeedFactory;
        }

        /// <summary>
        /// Clears the roster back to four empty seats and refreshes every
        /// visual that depends on it — docs/specs/ui/game-setup.md#behaviour:
        /// "Entering: seats all empty, every time." A freshly constructed
        /// view already starts this way; this is the hook for re-entering an
        /// existing one, once something wires this screen's root back up on
        /// every navigation to it (not this issue's scope — see the PR).
        /// </summary>
        public void ResetToEmptySeats()
        {
            EnsureInitialized();

            _chosenOrder.Clear();
            RefreshAll();
        }

        /// <summary>The `seats` region's seat for <paramref name="colour"/> — the `Rect` around it all.</summary>
        public RectTransform SeatRect(FrogColour colour) => SeatFor(colour).Rect;

        /// <summary>The seat's tappable hit target — pointer events fire the same way <see cref="Button"/>'s do: on release, only if it lands inside.</summary>
        public SeatTapTarget SeatTapTargetFor(FrogColour colour) => SeatFor(colour).TapTarget;

        /// <summary>The seat's outline — <see cref="InkColor"/> when chosen, <see cref="FaintColor"/> when empty.</summary>
        public Image SeatOutline(FrogColour colour) => SeatFor(colour).Outline;

        /// <summary>The seat's fill — <see cref="PaperColor"/> when chosen, transparent when empty.</summary>
        public Image SeatFill(FrogColour colour) => SeatFor(colour).Fill;

        /// <summary>The seat's frog swatch — a flat circle, never an imported sprite.</summary>
        public Image SeatSwatch(FrogColour colour) => SeatFor(colour).Swatch;

        /// <summary>The seat's bottom label — the colour name when chosen, `Tap to play` when empty.</summary>
        public Text SeatLabel(FrogColour colour) => SeatFor(colour).Label;

        /// <summary>The seat's turn-order badge root — active only while the seat is chosen.</summary>
        public GameObject SeatBadgeRoot(FrogColour colour) => SeatFor(colour).BadgeRoot;

        /// <summary>The turn-order badge's own <see cref="RectTransform"/>, top-left inset by <see cref="SeatBadgeInset"/>.</summary>
        public RectTransform SeatBadgeRect(FrogColour colour) => SeatFor(colour).BadgeRect;

        /// <summary>The turn-order badge's digit.</summary>
        public Text SeatBadgeText(FrogColour colour) => SeatFor(colour).BadgeText;

        /// <summary>Whether this seat currently holds a frog.</summary>
        public bool IsSeatChosen(FrogColour colour)
        {
            EnsureInitialized();
            return _chosenOrder.Contains(colour);
        }

        /// <summary>This seat's turn-order badge (1-based), or null while the seat is empty.</summary>
        public int? SeatBadgeNumber(FrogColour colour)
        {
            EnsureInitialized();
            var index = _chosenOrder.IndexOf(colour);
            return index >= 0 ? index + 1 : (int?)null;
        }

        Seat SeatFor(FrogColour colour)
        {
            EnsureInitialized();
            return _seats[colour];
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as ScreenRouterAdapter,
        // Button and TitleScreenView's own EnsureInitialized. Every public
        // entry point funnels through this idempotent guard instead of
        // trusting Awake's timing.
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

            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            BuildHeader();
            BuildSeats();
            BuildHint();
            BuildControls();
        }

        void BuildHeader()
        {
            // header — pinned to the top safe area, centred.
            var headerGO = new GameObject("Header", typeof(RectTransform), typeof(Text));
            _headerText = headerGO.GetComponent<Text>();
            _headerText.text = HeaderLabel;
            _headerText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _headerText.fontSize = (int)SetupHeaderSize;
            _headerText.alignment = TextAnchor.UpperCenter;
            _headerText.color = InkColor;
            _headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _headerText.verticalOverflow = VerticalWrapMode.Overflow;
            _headerText.raycastTarget = false;

            _headerRect = _headerText.rectTransform;
            _headerRect.SetParent(_rect, worldPositionStays: false);
            _headerRect.anchorMin = new Vector2(0.5f, 1f);
            _headerRect.anchorMax = new Vector2(0.5f, 1f);
            _headerRect.pivot = new Vector2(0.5f, 1f);
            _headerRect.sizeDelta = Vector2.zero;
            _headerRect.anchoredPosition = new Vector2(0f, -SafeMargin);
        }

        void BuildSeats()
        {
            // seats — four seats, always four, always in the same
            // left-to-right order, centred both ways in the space between
            // header and controls. docs/specs/ui/game-setup.md: "Four seats
            // at 360 px with three 48 px gaps is 1584 px, centred in 1920
            // with 168 px either side."
            var rowWidth = (SeatOrder.Length * SeatWidth) + ((SeatOrder.Length - 1) * SeatGap);
            var seatsCenterY = ComputeSeatsCenterY();

            var seatsGO = new GameObject("Seats", typeof(RectTransform));
            _seatsRect = (RectTransform)seatsGO.transform;
            _seatsRect.SetParent(_rect, worldPositionStays: false);
            _seatsRect.anchorMin = new Vector2(0.5f, 0.5f);
            _seatsRect.anchorMax = new Vector2(0.5f, 0.5f);
            _seatsRect.pivot = new Vector2(0.5f, 0.5f);
            _seatsRect.sizeDelta = new Vector2(rowWidth, SeatHeight);
            _seatsRect.anchoredPosition = new Vector2(0f, seatsCenterY);

            var leftmostCenterX = (-rowWidth / 2f) + (SeatWidth / 2f);

            for (var index = 0; index < SeatOrder.Length; index++)
            {
                var colour = SeatOrder[index];
                var x = leftmostCenterX + (index * (SeatWidth + SeatGap));
                _seats[colour] = BuildSeat(colour, x);
            }
        }

        Seat BuildSeat(FrogColour colour, float x)
        {
            var seatGO = new GameObject(colour.ToString(), typeof(RectTransform));
            var seatRect = (RectTransform)seatGO.transform;
            seatRect.SetParent(_seatsRect, worldPositionStays: false);
            seatRect.anchorMin = new Vector2(0.5f, 0.5f);
            seatRect.anchorMax = new Vector2(0.5f, 0.5f);
            seatRect.pivot = new Vector2(0.5f, 0.5f);
            seatRect.sizeDelta = new Vector2(SeatWidth, SeatHeight);
            seatRect.anchoredPosition = new Vector2(x, 0f);

            var outlineGO = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            var outline = outlineGO.GetComponent<Image>();
            outline.sprite = PanelSprite;
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = true;
            var outlineRect = outline.rectTransform;
            outlineRect.SetParent(seatRect, worldPositionStays: false);
            StretchToFill(outlineRect);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = PanelSprite;
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;
            var fillRect = fill.rectTransform;
            fillRect.SetParent(seatRect, worldPositionStays: false);
            StretchToFill(fillRect);

            var swatchGO = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            var swatch = swatchGO.GetComponent<Image>();
            swatch.sprite = SwatchSprite;
            swatch.type = Image.Type.Sliced;
            swatch.raycastTarget = false;
            var swatchRect = swatch.rectTransform;
            swatchRect.SetParent(seatRect, worldPositionStays: false);
            swatchRect.anchorMin = new Vector2(0.5f, 0.5f);
            swatchRect.anchorMax = new Vector2(0.5f, 0.5f);
            swatchRect.pivot = new Vector2(0.5f, 0.5f);
            swatchRect.sizeDelta = new Vector2(SeatSwatchDiameter, SeatSwatchDiameter);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            label.fontSize = (int)SeatLabelSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.SetParent(seatRect, worldPositionStays: false);
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = Vector2.zero;

            var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            var badgeImage = badgeGO.GetComponent<Image>();
            badgeImage.sprite = BadgeSprite;
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = InkColor;
            badgeImage.raycastTarget = false;
            var badgeRect = badgeImage.rectTransform;
            badgeRect.SetParent(seatRect, worldPositionStays: false);
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(SeatOrderBadge, SeatOrderBadge);
            badgeRect.anchoredPosition = new Vector2(SeatBadgeInset, -SeatBadgeInset);

            var badgeTextGO = new GameObject("Number", typeof(RectTransform), typeof(Text));
            var badgeText = badgeTextGO.GetComponent<Text>();
            badgeText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            badgeText.fontSize = (int)SeatBadgeLabelSize;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = PaperColor;
            badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            badgeText.verticalOverflow = VerticalWrapMode.Overflow;
            badgeText.raycastTarget = false;
            var badgeTextRect = badgeText.rectTransform;
            badgeTextRect.SetParent(badgeRect, worldPositionStays: false);
            StretchToFill(badgeTextRect);

            var tapTarget = seatGO.AddComponent<SeatTapTarget>();

            var seat = new Seat
            {
                Colour = colour,
                Rect = seatRect,
                TapTarget = tapTarget,
                Outline = outline,
                Fill = fill,
                Swatch = swatch,
                Label = label,
                BadgeRoot = badgeGO,
                BadgeRect = badgeRect,
                BadgeText = badgeText
            };

            PositionSeatContent(seat);

            tapTarget.Clicked += () => HandleSeatTapped(colour);

            return seat;
        }

        // The swatch and the label below it, centred as a block within the
        // seat, SeatContentGap apart — docs/specs/ui/mockups/game-setup.html:
        // every seat column draws `gap:32px` between the swatch and the
        // content below it.
        void PositionSeatContent(Seat seat)
        {
            var labelHeight = SeatLabelSize;
            var stackHeight = SeatSwatchDiameter + SeatContentGap + labelHeight;
            var swatchCenterY = (stackHeight / 2f) - (SeatSwatchDiameter / 2f);
            var labelCenterY = swatchCenterY - (SeatSwatchDiameter / 2f) - SeatContentGap - (labelHeight / 2f);

            seat.Swatch.rectTransform.anchoredPosition = new Vector2(0f, swatchCenterY);
            seat.Label.rectTransform.anchoredPosition = new Vector2(0f, labelCenterY);
        }

        void BuildHint()
        {
            // hint — one line, always present, HintGap beneath seats.
            var hintGO = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            _hintText = hintGO.GetComponent<Text>();
            _hintText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _hintText.fontSize = (int)SetupHintSize;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = LineColor;
            _hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hintText.verticalOverflow = VerticalWrapMode.Overflow;
            _hintText.raycastTarget = false;

            _hintRect = _hintText.rectTransform;
            _hintRect.SetParent(_rect, worldPositionStays: false);
            _hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            _hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            _hintRect.pivot = new Vector2(0.5f, 0.5f);
            _hintRect.sizeDelta = Vector2.zero;
            _hintRect.anchoredPosition = new Vector2(0f, ComputeHintCenterY(ComputeSeatsCenterY()));
        }

        void BuildControls()
        {
            // controls — Back at the left, Start at the right, both
            // SafeMargin from their edge, pinned to the bottom safe area.
            var controlsGO = new GameObject("Controls", typeof(RectTransform));
            _controlsRect = (RectTransform)controlsGO.transform;
            _controlsRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_controlsRect);

            _backButton = CreateControlButton("Back", ButtonKind.Secondary, BackLabel);
            _backButton.RectTransform.anchorMin = new Vector2(0f, 0f);
            _backButton.RectTransform.anchorMax = new Vector2(0f, 0f);
            _backButton.RectTransform.pivot = new Vector2(0f, 0f);
            _backButton.RectTransform.anchoredPosition = new Vector2(SafeMargin, SafeMargin);
            _backButton.Clicked += HandleBackClicked;

            _startButton = CreateControlButton("Start", ButtonKind.Primary, StartLabel);
            _startButton.RectTransform.anchorMin = new Vector2(1f, 0f);
            _startButton.RectTransform.anchorMax = new Vector2(1f, 0f);
            _startButton.RectTransform.pivot = new Vector2(1f, 0f);
            _startButton.RectTransform.anchoredPosition = new Vector2(-SafeMargin, SafeMargin);
            _startButton.Clicked += HandleStartClicked;
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

        // seats centred both ways in the space between header and controls
        // — docs/specs/ui/game-setup.md's Anchors section. Unlike
        // title-screen.md, game-setup.md names no absolute pixel offset for
        // this: the header and controls zones are derived from the
        // constants that describe them (SafeMargin/SetupHeaderSize for the
        // header, SafeMargin/ButtonHeight for controls), and the seats+hint
        // block is centred in whatever is left.
        static float ComputeSeatsCenterY()
        {
            var topReserved = SafeMargin + SetupHeaderSize;
            var bottomReserved = SafeMargin + Button.ButtonHeight;
            var hintBlockHeight = HintGap + SetupHintSize;

            var availableMiddle = CanvasHeight - topReserved - bottomReserved;
            var blockHeight = SeatHeight + hintBlockHeight;

            var topOfMiddleZone = (CanvasHeight / 2f) - topReserved;
            var topOfBlock = topOfMiddleZone - ((availableMiddle - blockHeight) / 2f);

            return topOfBlock - (SeatHeight / 2f);
        }

        // hint sits HintGap beneath seats, centred —
        // docs/specs/ui/game-setup.md's Anchors section.
        static float ComputeHintCenterY(float seatsCenterY)
        {
            return seatsCenterY - (SeatHeight / 2f) - HintGap - (SetupHintSize / 2f);
        }

        void HandleSeatTapped(FrogColour colour)
        {
            // Empty: adds this frog; it takes the next free turn-order
            // number. Chosen: removes this frog; the badges after it
            // renumber — computed fresh on every refresh below, never
            // cached, so removal renumbers immediately rather than at
            // Start. docs/specs/ui/game-setup.md's Elements section.
            if (_chosenOrder.Contains(colour))
            {
                _chosenOrder.Remove(colour);
            }
            else
            {
                _chosenOrder.Add(colour);
            }

            RefreshAll();
        }

        void RefreshAll()
        {
            foreach (var colour in SeatOrder)
            {
                var seat = _seats[colour];
                var index = _chosenOrder.IndexOf(colour);
                ApplySeatState(seat, index >= 0, index + 1);
            }

            RefreshControls();
        }

        void ApplySeatState(Seat seat, bool isChosen, int badgeNumber)
        {
            seat.Outline.color = isChosen ? InkColor : FaintColor;

            var ringWidth = isChosen ? SeatChosenRing : SeatEmptyBorderWidth;
            seat.Fill.rectTransform.offsetMin = new Vector2(ringWidth, ringWidth);
            seat.Fill.rectTransform.offsetMax = new Vector2(-ringWidth, -ringWidth);
            seat.Fill.color = isChosen ? PaperColor : NoFillColor;

            seat.Swatch.color = isChosen ? FrogColours.For(seat.Colour) : EmptySwatchColor;

            seat.Label.text = isChosen ? seat.Colour.ToString() : EmptySeatLabel;
            seat.Label.color = isChosen ? InkColor : EmptyLabelColor;
            seat.Label.fontStyle = isChosen ? FontStyle.Bold : FontStyle.Normal;

            seat.BadgeRoot.SetActive(isChosen);
            if (isChosen)
            {
                seat.BadgeText.text = badgeNumber.ToString();
            }
        }

        void RefreshControls()
        {
            // Start disabled below GameSetupMinFrogs, enabled from
            // GameSetupMinFrogs up to GameSetupMaxFrogs (all four seats
            // chosen) — there are exactly four seats, so the seat count is
            // the maximum; no separate ceiling check is needed.
            var startEnabled = _chosenOrder.Count >= GameSetupMinFrogs;
            _startButton.SetDisabled(!startEnabled);

            _hintText.text = startEnabled
                ? _chosenOrder[0].ToString() + HintGoesFirstSuffix
                : HintDisabledText;
        }

        void HandleBackClicked()
        {
            _router?.NavigateToScreen(CoreScreen.TitleScreen);
        }

        void HandleStartClicked()
        {
            // Button never invokes Clicked while disabled, so `_chosenOrder`
            // is always between GameSetupMinFrogs and GameSetupMaxFrogs
            // here, already unique (a seat can only ever appear once in tap
            // order) — exactly what Frogs.Core.Game's constructor itself
            // re-validates. This screen adds no game rules of its own;
            // docs/specs/ui/game-setup.md#behaviour: "`Start` begins the
            // game with the chosen frogs in badge order."
            var turnOrder = _chosenOrder.ToArray();
            var seed = _seedFactory();

            StartedGame = new Frogs.Core.Game(turnOrder, seed);

            _router?.NavigateToScreen(CoreScreen.GameBoard);
        }

        // Frogs.Core.Rng is seeded and, by its own contract, is "never
        // [built] from the clock, and never from nothing" — but that rule
        // is about Frogs.Core reading the clock itself, which it never
        // does. This Unity-layer boundary is exactly where a brand-new,
        // unsaved game's real-world entropy is expected to enter the
        // system; see Initialize's own doc comment.
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
        /// One frog seat's pieces — a plain data holder, not a
        /// <see cref="MonoBehaviour"/> itself; <see cref="SeatTapTarget"/> is
        /// the piece of it that is one.
        /// </summary>
        sealed class Seat
        {
            public FrogColour Colour;
            public RectTransform Rect;
            public SeatTapTarget TapTarget;
            public Image Outline;
            public Image Fill;
            public Image Swatch;
            public Text Label;
            public GameObject BadgeRoot;
            public RectTransform BadgeRect;
            public Text BadgeText;
        }

        /// <summary>
        /// A seat's tappable hit target. Mirrors <see cref="Button"/>'s own
        /// pointer handling exactly: acts on release, and only when the
        /// release lands over the seat, so a finger that lands wrong can
        /// slide off and cancel — docs/specs/ui/shared-components.md#button's
        /// Behaviour, applied to the one other tappable element on this
        /// screen. Public, with public pointer-event methods, the same
        /// shape as <see cref="Button.OnPointerDown"/>/<see cref="Button.OnPointerUp"/>,
        /// so a test can call them directly without a live
        /// <c>EventSystem</c>.
        /// </summary>
        public sealed class SeatTapTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            bool _isPressed;
            RectTransform _rect;

            /// <summary>Fires on release, only when the release lands over this seat.</summary>
            public event Action Clicked;

            /// <summary>
            /// This seat's own <see cref="RectTransform"/> — its hit-test
            /// bounds. Fetched lazily rather than in <c>Awake()</c>: Unity
            /// does not guarantee <c>Awake()</c> has run before a caller
            /// reaches this right after <c>AddComponent</c>, the same
            /// reasoning every other <c>EnsureInitialized</c> in this
            /// codebase gives.
            /// </summary>
            public RectTransform RectTransform
            {
                get
                {
                    if (_rect == null)
                    {
                        _rect = GetComponent<RectTransform>();
                    }

                    return _rect;
                }
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                _isPressed = true;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                var wasPressed = _isPressed;
                _isPressed = false;

                if (!wasPressed)
                {
                    return;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(RectTransform, eventData.position, eventData.pressEventCamera))
                {
                    Clicked?.Invoke();
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _isPressed = false;
            }
        }
    }
}
