using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;
using SharedButton = Frogs.Unity.UI.Button;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The board's gear — docs/specs/ui/game-board.md's Elements section:
    /// "top right, `SettingsButtonSize` square, a gear."
    ///
    /// Deliberately **not** the shared <see cref="SharedButton"/>. That
    /// component is a pill with a text label, a corner radius and a minimum
    /// width; the gear is a round icon square, and
    /// docs/specs/ui/shared-components.md has no icon-button component today
    /// to fit it into. Rather than force the gear into a Button it does not
    /// fit, this is the board's own small square button — see this issue's
    /// PR.
    ///
    /// Its size comes from the board's <c>SettingsButtonSize</c>, which
    /// already equals shared-components.md's <see cref="SharedButton.MinTouchTarget"/>,
    /// so the touch-target invariant is met by construction with no extra
    /// number.
    ///
    /// Pointer handling mirrors <see cref="SharedButton"/>'s exactly: acts on
    /// release, and only when the release lands over the button, so a finger
    /// that lands wrong can slide off and cancel. It is never disabled — the
    /// gear is "available on any turn, at any time, including while it is not
    /// your turn."
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameBoardSettingsButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        // No imported texture, sprite, or font — docs/specs/ui/shared-components.md
        // "no external assets", the same choice Button.cs and PlayerChip.cs made.
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // The gear itself, as a character rather than an imported icon —
        // U+2699 GEAR, written as an escape so the source file's encoding
        // can never be what decides whether the board draws one.
        const string GearGlyph = "\u2699";

        // Chrome colours copied verbatim from the committed mockup
        // (docs/specs/ui/mockups/game-board.html) — not a geometry constant
        // on any spec page's table, so not declared as a named spec constant.
        static readonly Color BackgroundColor = Color.white; // mockup's --paper
        static readonly Color GlyphColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockup's --ink

        RectTransform _rect;
        Image _background;
        Text _glyph;

        bool _initialized;
        bool _isPressed;

        /// <summary>Fires on release, only when the release lands over the button.</summary>
        public event Action Clicked;

        /// <summary>The button's own <see cref="RectTransform"/> — a square, and its hit-test bounds.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The round backdrop, drawn as a flat circle.</summary>
        public Image Background
        {
            get
            {
                EnsureInitialized();
                return _background;
            }
        }

        /// <summary>The gear.</summary>
        public Text Glyph
        {
            get
            {
                EnsureInitialized();
                return _glyph;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Sizes the square, and the circle drawn inside it.</summary>
        public void SetSize(float size)
        {
            EnsureInitialized();
            SetSizeInternal(size);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            _isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();

            var wasPressed = _isPressed;
            _isPressed = false;

            if (!wasPressed)
            {
                return;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(_rect, eventData.position, eventData.pressEventCamera))
            {
                Clicked?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
        }

        // Unity does not guarantee Awake() has run before a caller reaches
        // this right after AddComponent — the same reasoning as every other
        // EnsureInitialized in this codebase.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.type = Image.Type.Sliced;
            _background.color = BackgroundColor;
            _background.raycastTarget = true;
            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var glyphGO = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            _glyph = glyphGO.GetComponent<Text>();
            _glyph.text = GearGlyph;
            _glyph.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);

            // The mockup draws the gear at the same 44 px the shared Button
            // sets its own label at, so this names shared-components.md's
            // `ButtonLabelSize` rather than adding a number game-board.md's
            // tables do not have.
            _glyph.fontSize = (int)SharedButton.ButtonLabelSize;
            _glyph.color = GlyphColor;
            _glyph.alignment = TextAnchor.MiddleCenter;
            _glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
            _glyph.verticalOverflow = VerticalWrapMode.Overflow;
            _glyph.raycastTarget = false;
            var glyphRect = _glyph.rectTransform;
            glyphRect.SetParent(_rect, worldPositionStays: false);
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;

            // A default circle, so the button is drawable before SetSize —
            // the board always calls SetSize with SettingsButtonSize.
            SetSizeInternal(SharedButton.MinTouchTarget);
        }

        void SetSizeInternal(float size)
        {
            _rect.sizeDelta = new Vector2(size, size);
            _background.sprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(size / 2f));
        }
    }
}
