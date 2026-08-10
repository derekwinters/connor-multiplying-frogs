using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// A frog's identity — docs/specs/ui/shared-components.md#player-chip.
    /// Built entirely through the typed Unity API, no committed
    /// <c>.prefab</c>, the same decision #214 made for <see cref="Button"/>
    /// (issue #219).
    ///
    /// A readout only, everywhere it appears in v0.2 — it implements no
    /// pointer-event interface and declares no event of its own. Tapping to
    /// add or remove a frog belongs to game setup's own **Frog seat**
    /// element (docs/specs/ui/game-setup.md), not this component; see this
    /// issue's PR for the reconciliation between the two pages.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayerChip : MonoBehaviour
    {
        // docs/specs/ui/shared-components.md#player-chip — Named constants.
        public const float PlayerChipHeight = 96f;
        public const float PlayerChipWidth = 256f;
        public const float PlayerSwatchDiameter = 64f;
        public const float PlayerChipSwatchGap = 24f;
        public const float PlayerChipLabelSize = 32f;
        public const float PlayerChipPadCountSize = 24f;
        public const float PlayerChipRadius = 20f;
        public const float PlayerChipActiveRing = 6f;

        const string HomeLabel = "Home!";

        // No imported texture, sprite, or font — "no external assets".
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockups' CSS
        // custom properties, the same line Button.cs, TitleScreenView.cs and
        // DialogPanel.cs each draw for their own colours — not a
        // shared-components.md geometry/opacity constant, so not declared as
        // a named spec constant.
        static readonly Color ChipBackgroundColor = Color.white; // mockups' --paper / #fff chip fill
        static readonly Color RingColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' .chip.act border, --ink
        static readonly Color NoRingColor = new Color(0f, 0f, 0f, 0f); // no ring — fully transparent
        static readonly Color LabelColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color PadCountColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' .chip s, --line

        static Sprite s_chipSprite;
        static Sprite s_swatchSprite;

        static Sprite ChipSprite
        {
            get
            {
                if (s_chipSprite == null)
                {
                    s_chipSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(PlayerChipRadius));
                }

                return s_chipSprite;
            }
        }

        static Sprite SwatchSprite
        {
            get
            {
                if (s_swatchSprite == null)
                {
                    // A rounded rect whose radius is half its own size is a
                    // circle — no separate circle-drawing code needed.
                    s_swatchSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(PlayerSwatchDiameter / 2f));
                }

                return s_swatchSprite;
            }
        }

        RectTransform _rect;
        Image _border;
        Image _fill;
        Image _swatch;
        Text _label;
        Text _padCountText;

        bool _initialized;
        PlayerChipState _state = PlayerChipState.Default;
        string _padCount = string.Empty;

        /// <summary>The chip's own <see cref="RectTransform"/>, sized to <see cref="PlayerChipWidth"/> x <see cref="PlayerChipHeight"/>.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The frog's colour swatch — <see cref="PlayerSwatchDiameter"/> across.</summary>
        public Image Swatch
        {
            get
            {
                EnsureInitialized();
                return _swatch;
            }
        }

        /// <summary>The colour's name, in words — always shown alongside the swatch, never colour alone.</summary>
        public Text Label
        {
            get
            {
                EnsureInitialized();
                return _label;
            }
        }

        /// <summary>The pad-count line — replaced by <c>Home!</c> while <see cref="State"/> is <see cref="PlayerChipState.Home"/>.</summary>
        public Text PadCountText
        {
            get
            {
                EnsureInitialized();
                return _padCountText;
            }
        }

        /// <summary>The <see cref="PlayerChipActiveRing"/> ring's colour — transparent outside <see cref="PlayerChipState.Active"/>.</summary>
        public Color BorderColor
        {
            get
            {
                EnsureInitialized();
                return _border.color;
            }
        }

        public PlayerChipState State
        {
            get
            {
                EnsureInitialized();
                return _state;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Sets the frog's colour and its colour's name, in words —
        /// docs/specs/ui/shared-components.md#player-chip's first invariant:
        /// colour and name always together, never colour alone.
        /// </summary>
        public void SetFrog(Color color, string colourName)
        {
            EnsureInitialized();
            _swatch.color = color;
            _label.text = colourName;
        }

        /// <summary>
        /// Sets the pad-count line's text while not <see cref="PlayerChipState.Home"/> —
        /// e.g. game-board.md's <c>"3 of 8"</c>. The chip is a readout: it
        /// does not compute this string itself, since what "8" means is a
        /// screen's own constant (`LaneWinningPosition`), not this
        /// component's.
        /// </summary>
        public void SetPadCount(string text)
        {
            EnsureInitialized();
            _padCount = text;
            RefreshVisual();
        }

        /// <summary>Sets which of the three built states the chip is in, and refreshes its appearance to match.</summary>
        public void SetState(PlayerChipState state)
        {
            EnsureInitialized();
            _state = state;
            RefreshVisual();
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as Button, DialogPanel and
        // ScreenRouterAdapter's own EnsureInitialized.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            RefreshVisual();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            _rect.sizeDelta = new Vector2(PlayerChipWidth, PlayerChipHeight);

            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            _border = borderGO.GetComponent<Image>();
            _border.sprite = ChipSprite;
            _border.type = Image.Type.Sliced;
            _border.raycastTarget = false;
            var borderRect = _border.rectTransform;
            borderRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(borderRect);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _fill = fillGO.GetComponent<Image>();
            _fill.sprite = ChipSprite;
            _fill.type = Image.Type.Sliced;
            _fill.color = ChipBackgroundColor;
            _fill.raycastTarget = false;
            var fillRect = _fill.rectTransform;
            fillRect.SetParent(_rect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(PlayerChipActiveRing, PlayerChipActiveRing);
            fillRect.offsetMax = new Vector2(-PlayerChipActiveRing, -PlayerChipActiveRing);

            var swatchGO = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            _swatch = swatchGO.GetComponent<Image>();
            _swatch.sprite = SwatchSprite;
            _swatch.raycastTarget = false;
            var swatchRect = _swatch.rectTransform;
            swatchRect.SetParent(_fill.rectTransform, worldPositionStays: false);
            swatchRect.anchorMin = new Vector2(0f, 0.5f);
            swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.pivot = new Vector2(0f, 0.5f);
            swatchRect.sizeDelta = new Vector2(PlayerSwatchDiameter, PlayerSwatchDiameter);
            swatchRect.anchoredPosition = Vector2.zero;

            // The text stack — colour name above, pad count below — fills
            // the chip's height to the right of the swatch, split evenly.
            var textStackGO = new GameObject("TextStack", typeof(RectTransform));
            var textStackRect = (RectTransform)textStackGO.transform;
            textStackRect.SetParent(_fill.rectTransform, worldPositionStays: false);
            textStackRect.anchorMin = Vector2.zero;
            textStackRect.anchorMax = Vector2.one;
            textStackRect.offsetMin = new Vector2(PlayerSwatchDiameter + PlayerChipSwatchGap, 0f);
            textStackRect.offsetMax = Vector2.zero;

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            _label = labelGO.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _label.fontSize = (int)PlayerChipLabelSize;
            _label.color = LabelColor;
            _label.alignment = TextAnchor.LowerLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var labelRect = _label.rectTransform;
            labelRect.SetParent(textStackRect, worldPositionStays: false);
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var padCountGO = new GameObject("PadCount", typeof(RectTransform), typeof(Text));
            _padCountText = padCountGO.GetComponent<Text>();
            _padCountText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _padCountText.fontSize = (int)PlayerChipPadCountSize;
            _padCountText.color = PadCountColor;
            _padCountText.alignment = TextAnchor.UpperLeft;
            _padCountText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _padCountText.verticalOverflow = VerticalWrapMode.Overflow;
            _padCountText.raycastTarget = false;
            var padCountRect = _padCountText.rectTransform;
            padCountRect.SetParent(textStackRect, worldPositionStays: false);
            padCountRect.anchorMin = new Vector2(0f, 0f);
            padCountRect.anchorMax = new Vector2(1f, 0.5f);
            padCountRect.offsetMin = Vector2.zero;
            padCountRect.offsetMax = Vector2.zero;
        }

        void RefreshVisual()
        {
            var isActive = _state == PlayerChipState.Active;
            var isHome = _state == PlayerChipState.Home;

            _border.color = isActive ? RingColor : NoRingColor;
            _label.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
            _padCountText.text = isHome ? HomeLabel : _padCount;
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
