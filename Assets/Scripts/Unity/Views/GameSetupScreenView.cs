using System;
using System.Collections.Generic;
using System.Linq;
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
using ScreenColours = Frogs.Unity.UI.ScreenColours;

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
        public const float SeatHeight = 480f;
        public const float SeatGap = 48f;
        public const float SeatRadius = 32f;
        public const float SeatSwatchDiameter = 200f;
        public const float SeatLabelSize = 48f;
        public const float SeatChosenRing = 8f;
        public const float HintGap = 56f;
        public const float SetupHintSize = 36f;
        public const float SeatOrderBadge = 72f;
        public const float SeatContentGap = 16f;
        public const float SeatBadgeInset = 24f;

        // docs/specs/ui/game-setup.md#named-constants — the rows #310's
        // wireframe added when the seat gained a name row and a remove
        // target of its own.
        public const float SeatTopBand = 136f;
        public const float SeatCornerTarget = 96f;
        public const float SeatCornerInset = 16f;
        public const float SeatNameRowWidth = 312f;
        public const float SeatNameRowHeight = 96f;
        public const float SeatNameRowRadius = 20f;
        public const float SeatNameRowPaddingX = 16f;
        public const float SeatRowTop = 300f;
        public const float SeatRowEditingTop = 150f;

        // docs/specs/ui/game-setup.md#the-keyboard — a keyboard this game
        // draws, never Android's.
        public const float NameKeyboardHeight = 480f;
        public const float NameKeyboardWidth = 1664f;
        public const float NameKeyWidth = 152f;
        public const float NameKeyHeight = 108f;
        public const float NameKeyGap = 16f;
        public const float NameKeyRadius = 20f;
        public const float NameKeyLabelSize = 52f;
        public const float NameSpaceKeyWidth = 1160f;
        public const float NameDoneKeyWidth = 488f;

        /// <summary>
        /// docs/specs/ui/game-setup.md#named-constants — the longest name a
        /// player can type. Read off <see cref="Frogs.Core.PlayerName"/>'s
        /// own constant rather than repeating the number, because the cap is
        /// Core's rule and this screen only renders the refusal.
        /// </summary>
        public const int PlayerNameMaxLength = Frogs.Core.PlayerName.PlayerNameMaxLength;

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

        // While a name is being typed the header carries the prompt, because
        // the hint line's resting position is underneath the keyboard —
        // docs/specs/ui/mockups/game-setup-name-edit-inline.html draws
        // "Name the green frog". The word `frog` here is attached to a
        // colour, not to a name; nothing appends anything to a name.
        const string NamingHeaderFormat = "Name the {0} frog";

        const string RemoveKeyLabel = "×";
        const string BackspaceKeyLabel = "⌫";
        const string ShiftKeyLabel = "⇧";
        const string SpaceKeyLabel = "space";
        const string DoneKeyLabel = "Done";
        const char SpaceCharacter = ' ';

        // docs/specs/ui/game-setup.md#the-keyboard, rows 1-3. QWERTY, which
        // is what the mockups draw; whether it should be alphabetical instead
        // is a taste call left open for Connor and changes only which glyph
        // sits on which key.
        static readonly string[] LetterRows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };

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
        static readonly Color AccentColor = new Color32(0x2E, 0x7D, 0x4F, 0xFF); // mockups' --accent
        static readonly Color WarnColor = new Color32(0xB0, 0x3A, 0x2E, 0xFF); // mockups' --warn
        static readonly Color RemoveFillColor = new Color32(0xFB, 0xF1, 0xF0, 0xFF); // mockups' .remove background
        static readonly Color NameRowFillColor = new Color32(0xF4, 0xF7, 0xF5, 0xFF); // mockups' .namerow background
        static readonly Color SpaceKeyLabelColor = new Color32(0x7A, 0x87, 0x83, 0xFF); // mockups' .k.space colour
        static readonly Color DisabledKeyColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockups' --faint, as a disabled key reads

        const float FullyOpaqueByte = 255f;

        // Stroke widths the mockups draw but game-setup.md's constants table
        // does not name — the same line SeatBadgeLabelSize and
        // SeatEmptyBorderWidth above already draw: a rendering detail the
        // spec leaves to presentation (ADR-0001), kept as a named private
        // constant rather than a bare literal.
        const float SeatNameRowBorderWidth = 3f;
        const float SeatNameFieldBorderWidth = 5f;
        const float SeatRemoveBorderWidth = 4f;
        const float CaretWidth = 5f;
        const float CaretHeight = 56f;
        const float CaretGap = 4f;
        const float NameKeyBorderWidth = 3f;
        const float SeatRemoveLabelSize = 48f;
        const float NameKeyGlyphLabelSize = 44f;
        const float NameSpaceKeyLabelSize = 30f;
        const float NameDoneKeyLabelSize = 44f;

        // No imported texture, sprite, or font — docs/specs/ui/shared-components.md
        // "no external assets". Same procedural rounded-rect technique as
        // Frogs.Unity.UI.Button.CreateRoundedRectSprite, duplicated locally
        // rather than exposing that private helper: touching #214's merged
        // Button.cs is not this issue's to do as a side effect of its own
        // shape needs — see this issue's PR.
        static Sprite s_panelSprite;
        static Sprite s_swatchSprite;
        static Sprite s_badgeSprite;
        static Sprite s_nameRowSprite;
        static Sprite s_removeSprite;
        static Sprite s_keySprite;

        static Sprite NameRowSprite
        {
            get
            {
                if (s_nameRowSprite == null)
                {
                    s_nameRowSprite = CreateRoundedRectSprite(Mathf.RoundToInt(SeatNameRowRadius));
                }

                return s_nameRowSprite;
            }
        }

        static Sprite RemoveSprite
        {
            get
            {
                if (s_removeSprite == null)
                {
                    s_removeSprite = CreateRoundedRectSprite(Mathf.RoundToInt(SeatCornerTarget / 2f));
                }

                return s_removeSprite;
            }
        }

        static Sprite KeySprite
        {
            get
            {
                if (s_keySprite == null)
                {
                    s_keySprite = CreateRoundedRectSprite(Mathf.RoundToInt(NameKeyRadius));
                }

                return s_keySprite;
            }
        }

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
        RectTransform _contentRect;
        Image _background;

        RectTransform _headerRect;
        Text _headerText;

        RectTransform _seatsRect;
        readonly Dictionary<FrogColour, Seat> _seats = new Dictionary<FrogColour, Seat>();

        RectTransform _hintRect;
        Text _hintText;

        RectTransform _controlsRect;
        Button _backButton;
        Button _startButton;

        RectTransform _keyboardRect;
        GameObject _keyboardRoot;
        readonly List<NameKeyboardKey> _nameKeys = new List<NameKeyboardKey>();

        // Tap order, first tapped to last — the badge order, and the roster
        // Start hands to Core. A frog that is removed loses its entry
        // entirely, so re-seating it starts from its colour name again.
        readonly List<RosterEntry> _chosen = new List<RosterEntry>();

        PlayerNameEditor _editor;

        bool _initialized;
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
        /// Tap order, first tapped to last — the badge order `Start` hands to
        /// Core. Empty on a clean slate.
        /// </summary>
        public IReadOnlyList<FrogColour> ChosenOrder
        {
            get
            {
                EnsureInitialized();
                return _chosen.Select(entry => entry.Colour).ToArray();
            }
        }

        /// <summary>
        /// The roster as it stands — every chosen frog with the name it will
        /// take into the game, in badge order.
        /// </summary>
        public IReadOnlyList<RosterEntry> ChosenRoster
        {
            get
            {
                EnsureInitialized();
                return _chosen.ToArray();
            }
        }

        /// <summary>
        /// Which seat's name is being typed, or null when the keyboard is
        /// down. Only one seat is edited at a time.
        /// </summary>
        public FrogColour? EditingSeat
        {
            get
            {
                EnsureInitialized();
                return _editor == null ? (FrogColour?)null : _editor.Colour;
            }
        }

        /// <summary>What has been typed so far in the open naming session — empty when the keyboard is down.</summary>
        public string TypedName
        {
            get
            {
                EnsureInitialized();
                return _editor == null ? string.Empty : _editor.Text;
            }
        }

        /// <summary>The `keyboard` region's root — active only while a name is being typed.</summary>
        public GameObject KeyboardRoot
        {
            get
            {
                EnsureInitialized();
                return _keyboardRoot;
            }
        }

        /// <summary>The `keyboard` region's <see cref="RectTransform"/>, <see cref="NameKeyboardWidth"/> x <see cref="NameKeyboardHeight"/>.</summary>
        public RectTransform KeyboardRect
        {
            get
            {
                EnsureInitialized();
                return _keyboardRect;
            }
        }

        /// <summary>Every key on the name keyboard, in layout order.</summary>
        public IReadOnlyList<NameKeyboardKey> NameKeys
        {
            get
            {
                EnsureInitialized();
                return _nameKeys;
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

            _chosen.Clear();
            _editor = null;
            RefreshAll();
        }

        /// <summary>The key that types <paramref name="character"/>, or null if the keyboard has none.</summary>
        public NameKeyboardKey NameKey(char character)
        {
            EnsureInitialized();

            var wanted = char.ToUpperInvariant(character);

            foreach (var key in _nameKeys)
            {
                if ((key.Kind == NameKeyKind.Letter || key.Kind == NameKeyKind.Space)
                    && char.ToUpperInvariant(key.Character) == wanted)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>The space bar.</summary>
        public NameKeyboardKey SpaceKey
        {
            get { return KeyOfKind(NameKeyKind.Space); }
        }

        /// <summary>The backspace key.</summary>
        public NameKeyboardKey BackspaceKey
        {
            get { return KeyOfKind(NameKeyKind.Backspace); }
        }

        /// <summary>The shift key — drawn, and disabled; see this type's own note.</summary>
        public NameKeyboardKey ShiftKey
        {
            get { return KeyOfKind(NameKeyKind.Shift); }
        }

        /// <summary>`Done` — the only way out of the keyboard.</summary>
        public NameKeyboardKey DoneKey
        {
            get { return KeyOfKind(NameKeyKind.Done); }
        }

        NameKeyboardKey KeyOfKind(NameKeyKind kind)
        {
            EnsureInitialized();

            foreach (var key in _nameKeys)
            {
                if (key.Kind == kind)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>
        /// What this seat's player is called, or the empty string while the
        /// seat is empty — "a seat that is not playing has no name and cannot
        /// be given one."
        /// </summary>
        public string NameFor(FrogColour colour)
        {
            EnsureInitialized();

            var entry = EntryFor(colour);
            return entry == null ? string.Empty : entry.Name;
        }

        /// <summary>
        /// Empties the name being typed, without closing the keyboard — what
        /// holding backspace down arrives at. A no-op when nothing is being
        /// typed.
        /// </summary>
        public void ClearName()
        {
            EnsureInitialized();

            if (_editor == null)
            {
                return;
            }

            _editor.Clear();
            RefreshAll();
        }

        /// <summary>
        /// `Done`: closes the keyboard, puts the seat row back at
        /// <see cref="SeatRowTop"/>, and brings the hint and the controls
        /// back. A blank name becomes the frog's colour name again.
        /// </summary>
        public void DoneNaming()
        {
            EnsureInitialized();

            if (_editor == null)
            {
                return;
            }

            var committed = _editor.Commit();
            var index = IndexOf(committed.Colour);

            if (index >= 0)
            {
                _chosen[index] = committed;
            }

            _editor = null;
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

        /// <summary>
        /// The seat's name row — the edit target. Tapping it opens the
        /// keyboard on this seat. Present only on a chosen seat.
        /// </summary>
        public RectTransform SeatNameRowRect(FrogColour colour) => SeatFor(colour).NameRowRect;

        /// <summary>The name row's root — active only while the seat is chosen.</summary>
        public GameObject SeatNameRowRoot(FrogColour colour) => SeatFor(colour).NameRowRoot;

        /// <summary>The name row's tap target — the edit target, and nothing else.</summary>
        public SeatTapTarget SeatNameRowTapTarget(FrogColour colour) => SeatFor(colour).NameRowTapTarget;

        /// <summary>The remove target's root — active only while the seat is chosen and not being edited.</summary>
        public GameObject SeatRemoveRoot(FrogColour colour) => SeatFor(colour).RemoveRoot;

        /// <summary>The remove target's <see cref="RectTransform"/> — <see cref="SeatCornerTarget"/> at <see cref="SeatCornerInset"/> from the top-right corner.</summary>
        public RectTransform SeatRemoveRect(FrogColour colour) => SeatFor(colour).RemoveRect;

        /// <summary>The remove target's tap target — the only way a player leaves the game.</summary>
        public SeatTapTarget SeatRemoveTapTarget(FrogColour colour) => SeatFor(colour).RemoveTapTarget;

        /// <summary>The caret drawn in the name field while this seat is being edited.</summary>
        public GameObject SeatCaret(FrogColour colour) => SeatFor(colour).Caret;

        /// <summary>Whether this seat currently holds a frog.</summary>
        public bool IsSeatChosen(FrogColour colour)
        {
            EnsureInitialized();
            return IndexOf(colour) >= 0;
        }

        /// <summary>This seat's turn-order badge (1-based), or null while the seat is empty.</summary>
        public int? SeatBadgeNumber(FrogColour colour)
        {
            EnsureInitialized();
            var index = IndexOf(colour);
            return index >= 0 ? index + 1 : (int?)null;
        }

        int IndexOf(FrogColour colour)
        {
            for (var index = 0; index < _chosen.Count; index++)
            {
                if (_chosen[index].Colour == colour)
                {
                    return index;
                }
            }

            return -1;
        }

        RosterEntry EntryFor(FrogColour colour)
        {
            var index = IndexOf(colour);
            return index >= 0 ? _chosen[index] : null;
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

            // The root fills the whole canvas, which on a device that is not
            // 16:10 is larger than the 1920 x 1200 reference — see
            // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
            // Only the background hangs off it. Everything laid out in
            // reference pixels hangs off `Content` instead, so the extra space
            // a wider or taller device gives us is painted and nothing else.
            StretchToFill(_rect);

            BuildBackground();
            BuildContent();

            BuildHeader();
            BuildSeats();
            BuildHint();
            BuildControls();
            BuildKeyboard();
        }

        // keyboard — pinned to the bottom safe area, centred,
        // NameKeyboardHeight tall, laid out over hint and controls rather
        // than beside them. docs/specs/ui/game-setup.md#the-keyboard.
        void BuildKeyboard()
        {
            var keyboardGO = new GameObject("Keyboard", typeof(RectTransform));
            _keyboardRoot = keyboardGO;
            _keyboardRect = (RectTransform)keyboardGO.transform;
            _keyboardRect.SetParent(_contentRect, worldPositionStays: false);
            _keyboardRect.anchorMin = new Vector2(0.5f, 0f);
            _keyboardRect.anchorMax = new Vector2(0.5f, 0f);
            _keyboardRect.pivot = new Vector2(0.5f, 0f);
            _keyboardRect.sizeDelta = new Vector2(NameKeyboardWidth, NameKeyboardHeight);
            _keyboardRect.anchoredPosition = new Vector2(0f, SafeMargin);

            var rowPitch = NameKeyHeight + NameKeyGap;

            for (var rowIndex = 0; rowIndex < LetterRows.Length; rowIndex++)
            {
                var letters = LetterRows[rowIndex];
                var isLastLetterRow = rowIndex == LetterRows.Length - 1;

                // Row 3 carries shift and backspace either side of its seven
                // letters — docs/specs/ui/game-setup.md#the-keyboard.
                var keyCount = isLastLetterRow ? letters.Length + 2 : letters.Length;
                var rowWidth = (keyCount * NameKeyWidth) + ((keyCount - 1) * NameKeyGap);
                var x = -(rowWidth / 2f) + (NameKeyWidth / 2f);
                var y = (NameKeyboardHeight / 2f) - (NameKeyHeight / 2f) - (rowIndex * rowPitch);

                if (isLastLetterRow)
                {
                    BuildKey(NameKeyKind.Shift, default(char), ShiftKeyLabel, NameKeyGlyphLabelSize, NameKeyWidth, new Vector2(x, y));
                    x += NameKeyWidth + NameKeyGap;
                }

                foreach (var letter in letters)
                {
                    BuildKey(NameKeyKind.Letter, letter, letter.ToString(), NameKeyLabelSize, NameKeyWidth, new Vector2(x, y));
                    x += NameKeyWidth + NameKeyGap;
                }

                if (isLastLetterRow)
                {
                    BuildKey(NameKeyKind.Backspace, default(char), BackspaceKeyLabel, NameKeyGlyphLabelSize, NameKeyWidth, new Vector2(x, y));
                }
            }

            // Row 4: the space bar and `Done`, filling the block's width.
            var bottomY = (NameKeyboardHeight / 2f) - (NameKeyHeight / 2f) - (LetterRows.Length * rowPitch);
            var spaceX = -(NameKeyboardWidth / 2f) + (NameSpaceKeyWidth / 2f);
            var doneX = (NameKeyboardWidth / 2f) - (NameDoneKeyWidth / 2f);

            BuildKey(NameKeyKind.Space, SpaceCharacter, SpaceKeyLabel, NameSpaceKeyLabelSize, NameSpaceKeyWidth, new Vector2(spaceX, bottomY));
            BuildKey(NameKeyKind.Done, default(char), DoneKeyLabel, NameDoneKeyLabelSize, NameDoneKeyWidth, new Vector2(doneX, bottomY));

            // The shift key is drawn because the agreed mockup draws it, and
            // disabled because what it does is not settled — see this type's
            // note at the top of the naming section.
            ShiftKey.SetDisabled(true);
            ApplyKeyColours(ShiftKey);

            _keyboardRoot.SetActive(false);
        }

        void BuildKey(NameKeyKind kind, char character, string label, float labelSize, float width, Vector2 position)
        {
            var keyGO = new GameObject("Key" + label, typeof(RectTransform));
            var keyRect = (RectTransform)keyGO.transform;
            keyRect.SetParent(_keyboardRect, worldPositionStays: false);
            keyRect.anchorMin = new Vector2(0.5f, 0.5f);
            keyRect.anchorMax = new Vector2(0.5f, 0.5f);
            keyRect.pivot = new Vector2(0.5f, 0.5f);
            keyRect.sizeDelta = new Vector2(width, NameKeyHeight);
            keyRect.anchoredPosition = position;

            // The key's hit area: the outline, covering the whole key, with
            // the fill and the label refusing the raycast underneath it.
            // Without it there is nothing raycastable under the key, the
            // GraphicRaycaster never finds it, and the keyboard types
            // nothing (#288).
            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var border = borderGO.GetComponent<Image>();
            border.sprite = KeySprite;
            border.type = Image.Type.Sliced;
            border.raycastTarget = true;
            var borderRect = border.rectTransform;
            borderRect.SetParent(keyRect, worldPositionStays: false);
            StretchToFill(borderRect);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = KeySprite;
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;
            var fillRect = fill.rectTransform;
            fillRect.SetParent(borderRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(NameKeyBorderWidth, NameKeyBorderWidth);
            fillRect.offsetMax = new Vector2(-NameKeyBorderWidth, -NameKeyBorderWidth);

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            text.fontSize = (int)labelSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.fontStyle = kind == NameKeyKind.Space ? FontStyle.Normal : FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.SetParent(borderRect, worldPositionStays: false);
            StretchToFill(text.rectTransform);

            var key = keyGO.AddComponent<NameKeyboardKey>();
            key.Describe(kind, character, border, fill, text);
            ApplyKeyColours(key);
            key.Tapped += HandleKeyTapped;

            _nameKeys.Add(key);
        }

        static void ApplyKeyColours(NameKeyboardKey key)
        {
            var isDone = key.Kind == NameKeyKind.Done;

            key.Border.color = key.IsDisabled
                ? DisabledKeyColor
                : (isDone ? AccentColor : LineColor);
            key.Fill.color = isDone && !key.IsDisabled ? AccentColor : PaperColor;
            key.Label.color = key.IsDisabled
                ? DisabledKeyColor
                : (isDone ? PaperColor : (key.Kind == NameKeyKind.Space ? SpaceKeyLabelColor : InkColor));
        }

        void HandleKeyTapped(NameKeyboardKey key)
        {
            if (_editor == null)
            {
                return;
            }

            switch (key.Kind)
            {
                case NameKeyKind.Letter:
                case NameKeyKind.Space:
                    // A refusal at the cap is silently ignored: the key does
                    // nothing, the name is unchanged, and nothing explains
                    // itself.
                    _editor.Append(key.Character);
                    break;

                case NameKeyKind.Backspace:
                    _editor.Backspace();
                    break;

                case NameKeyKind.Done:
                    DoneNaming();
                    return;

                default:
                    return;
            }

            RefreshAll();
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
            _headerRect.SetParent(_contentRect, worldPositionStays: false);
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
            _seatsRect.SetParent(_contentRect, worldPositionStays: false);
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

            // The name band. On a chosen seat it is drawn as a field — a
            // bordered box — because it is the one part of a seat a player
            // can change and it has to look like it. On an empty seat only
            // the text inside it shows, reading `Tap to play`.
            var nameRowGO = new GameObject("NameRow", typeof(RectTransform), typeof(Image));
            var nameRowBorder = nameRowGO.GetComponent<Image>();
            nameRowBorder.sprite = NameRowSprite;
            nameRowBorder.type = Image.Type.Sliced;
            nameRowBorder.raycastTarget = true;
            var nameRowRect = nameRowBorder.rectTransform;
            nameRowRect.SetParent(seatRect, worldPositionStays: false);
            nameRowRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRowRect.pivot = new Vector2(0.5f, 0.5f);
            nameRowRect.sizeDelta = new Vector2(SeatNameRowWidth, SeatNameRowHeight);

            var nameRowFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var nameRowFill = nameRowFillGO.GetComponent<Image>();
            nameRowFill.sprite = NameRowSprite;
            nameRowFill.type = Image.Type.Sliced;
            nameRowFill.raycastTarget = false;
            var nameRowFillRect = nameRowFill.rectTransform;
            nameRowFillRect.SetParent(nameRowRect, worldPositionStays: false);
            StretchToFill(nameRowFillRect);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            label.fontSize = (int)SeatLabelSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            // The label and the caret hang off the seat rather than off the
            // name row, because an empty seat hides the row but still shows a
            // word in the same place — `Tap to play`.
            var labelRect = label.rectTransform;
            labelRect.SetParent(seatRect, worldPositionStays: false);
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(SeatNameRowWidth - (SeatNameRowPaddingX * 2f), SeatNameRowHeight);

            // The caret, drawn only on the seat being edited.
            var caretGO = new GameObject("Caret", typeof(RectTransform), typeof(Image));
            var caret = caretGO.GetComponent<Image>();
            caret.color = InkColor;
            caret.raycastTarget = false;
            var caretRect = caret.rectTransform;
            caretRect.SetParent(seatRect, worldPositionStays: false);
            caretRect.anchorMin = new Vector2(0.5f, 0.5f);
            caretRect.anchorMax = new Vector2(0.5f, 0.5f);
            caretRect.pivot = new Vector2(0.5f, 0.5f);
            caretRect.sizeDelta = new Vector2(CaretWidth, CaretHeight);
            caretGO.SetActive(false);

            var nameRowTapTarget = nameRowGO.AddComponent<SeatTapTarget>();
            nameRowTapTarget.Clicked += () => HandleNameRowTapped(colour);

            // The remove target — SeatCornerTarget at SeatCornerInset from
            // the seat's top-right corner, and the only way a player leaves
            // the game.
            var removeGO = new GameObject("Remove", typeof(RectTransform), typeof(Image));
            var removeBorder = removeGO.GetComponent<Image>();
            removeBorder.sprite = RemoveSprite;
            removeBorder.type = Image.Type.Sliced;
            removeBorder.color = WarnColor;
            removeBorder.raycastTarget = true;
            var removeRect = removeBorder.rectTransform;
            removeRect.SetParent(seatRect, worldPositionStays: false);
            removeRect.anchorMin = new Vector2(1f, 1f);
            removeRect.anchorMax = new Vector2(1f, 1f);
            removeRect.pivot = new Vector2(1f, 1f);
            removeRect.sizeDelta = new Vector2(SeatCornerTarget, SeatCornerTarget);
            removeRect.anchoredPosition = new Vector2(-SeatCornerInset, -SeatCornerInset);

            var removeFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var removeFill = removeFillGO.GetComponent<Image>();
            removeFill.sprite = RemoveSprite;
            removeFill.type = Image.Type.Sliced;
            removeFill.color = RemoveFillColor;
            removeFill.raycastTarget = false;
            var removeFillRect = removeFill.rectTransform;
            removeFillRect.SetParent(removeRect, worldPositionStays: false);
            removeFillRect.anchorMin = Vector2.zero;
            removeFillRect.anchorMax = Vector2.one;
            removeFillRect.offsetMin = new Vector2(SeatRemoveBorderWidth, SeatRemoveBorderWidth);
            removeFillRect.offsetMax = new Vector2(-SeatRemoveBorderWidth, -SeatRemoveBorderWidth);

            var removeLabelGO = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            var removeLabel = removeLabelGO.GetComponent<Text>();
            removeLabel.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            removeLabel.fontSize = (int)SeatRemoveLabelSize;
            removeLabel.alignment = TextAnchor.MiddleCenter;
            removeLabel.color = WarnColor;
            removeLabel.fontStyle = FontStyle.Bold;
            removeLabel.text = RemoveKeyLabel;
            removeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            removeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            removeLabel.raycastTarget = false;
            removeLabel.rectTransform.SetParent(removeRect, worldPositionStays: false);
            StretchToFill(removeLabel.rectTransform);

            var removeTapTarget = removeGO.AddComponent<SeatTapTarget>();
            removeTapTarget.Clicked += () => HandleRemoveTapped(colour);

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
                BadgeText = badgeText,
                NameRowRoot = nameRowGO,
                NameRowRect = nameRowRect,
                NameRowBorder = nameRowBorder,
                NameRowFill = nameRowFill,
                NameRowTapTarget = nameRowTapTarget,
                Caret = caretGO,
                RemoveRoot = removeGO,
                RemoveRect = removeRect,
                RemoveTapTarget = removeTapTarget
            };

            PositionSeatContent(seat);

            tapTarget.Clicked += () => HandleSeatTapped(colour);

            return seat;
        }

        // The swatch sits SeatTopBand below the seat's top edge — that band
        // is the space the two corner targets live in — and the name band
        // sits SeatContentGap below the swatch. Both mockups draw exactly
        // this: `.seat{padding-top:136px; gap:16px}`.
        static void PositionSeatContent(Seat seat)
        {
            var seatTop = SeatHeight / 2f;
            var swatchCenterY = seatTop - SeatTopBand - (SeatSwatchDiameter / 2f);
            var nameRowCenterY = swatchCenterY
                - (SeatSwatchDiameter / 2f)
                - SeatContentGap
                - (SeatNameRowHeight / 2f);

            seat.Swatch.rectTransform.anchoredPosition = new Vector2(0f, swatchCenterY);
            seat.NameRowRect.anchoredPosition = new Vector2(0f, nameRowCenterY);
            seat.Label.rectTransform.anchoredPosition = new Vector2(0f, nameRowCenterY);
            seat.Caret.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, nameRowCenterY);
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
            _hintRect.SetParent(_contentRect, worldPositionStays: false);
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
            _controlsRect.SetParent(_contentRect, worldPositionStays: false);
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

        // The seat row's top edge is a named number now, not a centring
        // calculation — docs/specs/ui/game-setup.md#named-constants gives
        // SeatRowTop 300 at rest and SeatRowEditingTop 150 while a name is
        // being typed. It has to be named, because "the distance they move"
        // is the whole reason the game draws its own keyboard rather than
        // Android's: a system keyboard's height is not a number a spec can
        // carry.
        static float ComputeSeatsCenterY()
        {
            return SeatsCenterYFor(SeatRowTop);
        }

        static float SeatsCenterYFor(float rowTop)
        {
            return (CanvasHeight / 2f) - rowTop - (SeatHeight / 2f);
        }

        // hint sits HintGap beneath seats, centred —
        // docs/specs/ui/game-setup.md's Anchors section.
        static float ComputeHintCenterY(float seatsCenterY)
        {
            return seatsCenterY - (SeatHeight / 2f) - HintGap - (SetupHintSize / 2f);
        }

        void HandleSeatTapped(FrogColour colour)
        {
            // **A chosen seat's body is inert, and that is the change.** It
            // used to be the remove target. Adding an edit target inside a
            // 360 x 480 destructive target means a child aiming at the name
            // and missing loses the player, with no confirm to catch it.
            // An empty seat's body still seats the frog, unchanged.
            if (IsSeatChosen(colour))
            {
                return;
            }

            _chosen.Add(new RosterEntry(colour));
            RefreshAll();
        }

        void HandleNameRowTapped(FrogColour colour)
        {
            // "A seat that is not playing has no name and cannot be given
            // one. Naming a frog that is not in the game is a state with no
            // meaning."
            var entry = EntryFor(colour);

            if (entry == null)
            {
                return;
            }

            // Only one seat is being edited at a time, so opening this one
            // commits whatever the last one had typed.
            DoneNaming();

            _editor = new PlayerNameEditor(entry);
            RefreshAll();
        }

        void HandleRemoveTapped(FrogColour colour)
        {
            var index = IndexOf(colour);

            if (index < 0)
            {
                return;
            }

            if (_editor != null && _editor.Colour == colour)
            {
                _editor = null;
            }

            // The badges after it renumber — computed fresh on every refresh
            // below, never cached, so removal renumbers immediately rather
            // than at Start.
            _chosen.RemoveAt(index);
            RefreshAll();
        }

        void RefreshAll()
        {
            var editing = EditingSeat;

            foreach (var colour in SeatOrder)
            {
                var seat = _seats[colour];
                var index = IndexOf(colour);
                ApplySeatState(seat, index, editing.HasValue && editing.Value == colour);
            }

            RefreshKeyboard(editing);
            RefreshControls(editing);
        }

        void ApplySeatState(Seat seat, int index, bool isEditing)
        {
            var isChosen = index >= 0;

            seat.Outline.color = isEditing ? AccentColor : (isChosen ? InkColor : FaintColor);

            var ringWidth = isChosen ? SeatChosenRing : SeatEmptyBorderWidth;
            seat.Fill.rectTransform.offsetMin = new Vector2(ringWidth, ringWidth);
            seat.Fill.rectTransform.offsetMax = new Vector2(-ringWidth, -ringWidth);
            seat.Fill.color = isChosen ? PaperColor : NoFillColor;

            seat.Swatch.color = isChosen ? FrogColours.For(seat.Colour) : EmptySwatchColor;

            // An empty seat has no name row and no remove target, but its
            // body still seats the frog and it still shows a word in the name
            // band's place.
            seat.NameRowRoot.SetActive(isChosen);
            seat.NameRowBorder.color = isEditing ? AccentColor : FaintColor;
            seat.NameRowFill.color = isEditing ? PaperColor : NameRowFillColor;

            var borderWidth = isEditing ? SeatNameFieldBorderWidth : SeatNameRowBorderWidth;
            seat.NameRowFill.rectTransform.offsetMin = new Vector2(borderWidth, borderWidth);
            seat.NameRowFill.rectTransform.offsetMax = new Vector2(-borderWidth, -borderWidth);

            seat.Label.text = isEditing
                ? _editor.Text
                : (isChosen ? _chosen[index].Name : EmptySeatLabel);
            seat.Label.color = isChosen ? InkColor : EmptyLabelColor;
            seat.Label.fontStyle = isChosen ? FontStyle.Bold : FontStyle.Normal;

            seat.Caret.SetActive(isEditing);
            if (isEditing)
            {
                // Just to the right of the text, the way the mockup draws it.
                var caretRect = (RectTransform)seat.Caret.transform;
                caretRect.anchoredPosition = new Vector2(
                    (seat.Label.preferredWidth / 2f) + CaretGap,
                    seat.NameRowRect.anchoredPosition.y);
            }

            // The remove target is not drawn on the seat being edited —
            // "Done first, then remove if that is what you meant."
            seat.RemoveRoot.SetActive(isChosen && !isEditing);

            seat.BadgeRoot.SetActive(isChosen);
            if (isChosen)
            {
                seat.BadgeText.text = (index + 1).ToString();
            }
        }

        void RefreshKeyboard(FrogColour? editing)
        {
            var isTyping = editing.HasValue;

            _keyboardRoot.SetActive(isTyping);

            // While a name is being typed the seat row moves up to clear the
            // keyboard. Nothing else moves and nothing resizes.
            _seatsRect.anchoredPosition = new Vector2(
                0f,
                SeatsCenterYFor(isTyping ? SeatRowEditingTop : SeatRowTop));

            _headerText.text = isTyping
                ? string.Format(NamingHeaderFormat, editing.Value.ToString().ToLowerInvariant())
                : HeaderLabel;
        }

        void RefreshControls(FrogColour? editing)
        {
            // Start disabled below GameSetupMinFrogs, enabled from
            // GameSetupMinFrogs up to GameSetupMaxFrogs (all four seats
            // chosen) — there are exactly four seats, so the seat count is
            // the maximum; no separate ceiling check is needed.
            var startEnabled = _chosen.Count >= GameSetupMinFrogs;
            _startButton.SetDisabled(!startEnabled);

            // The hint names whoever goes first by their name, so
            // `Connor goes first` rather than `Green goes first` once Green
            // has been renamed.
            _hintText.text = startEnabled
                ? _chosen[0].Name + HintGoesFirstSuffix
                : HintDisabledText;

            // hint and the controls are laid out under the keyboard rather
            // than beside it, so both are hidden while it is up.
            var isTyping = editing.HasValue;
            _hintRect.gameObject.SetActive(!isTyping);
            _controlsRect.gameObject.SetActive(!isTyping);
        }

        void HandleBackClicked()
        {
            _router?.NavigateToScreen(CoreScreen.TitleScreen);
        }

        void HandleStartClicked()
        {
            // Button never invokes Clicked while disabled, so `_chosen`
            // is always between GameSetupMinFrogs and GameSetupMaxFrogs
            // here, already unique (a seat can only ever appear once in tap
            // order) — exactly what Frogs.Core.Game's constructor itself
            // re-validates. This screen adds no game rules of its own;
            // docs/specs/ui/game-setup.md#behaviour: "`Start` begins the
            // game with the chosen frogs in badge order."
            // Their names go with them, and are what every later screen
            // shows — docs/specs/ui/game-setup.md#behaviour.
            var roster = _chosen.ToArray();
            var seed = _seedFactory();

            StartedGame = new Frogs.Core.Game(roster, seed);

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
            public GameObject NameRowRoot;
            public RectTransform NameRowRect;
            public Image NameRowBorder;
            public Image NameRowFill;
            public SeatTapTarget NameRowTapTarget;
            public GameObject Caret;
            public GameObject RemoveRoot;
            public RectTransform RemoveRect;
            public SeatTapTarget RemoveTapTarget;
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
