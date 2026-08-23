using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BoardColours = Frogs.Unity.UI.BoardColours;
using SharedButton = Frogs.Unity.UI.Button;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The board's gear — docs/specs/ui/game-board.md's Elements section:
    /// "top right, `SettingsButtonSize` square, a gear."
    ///
    /// Deliberately **not** the shared <see cref="SharedButton"/>. That
    /// component is a pill with a text label, a corner radius and a minimum
    /// width; the gear is a bare icon in a square, and
    /// docs/specs/ui/shared-components.md has no icon-button component today
    /// to fit it into. Rather than force the gear into a Button it does not
    /// fit, this is the board's own small square button — see this issue's
    /// PR.
    ///
    /// **It draws one thing: the gear.** It used to stack three — a grey ring,
    /// a white disc inside it, and the glyph on top — and on a pond that made
    /// a white button the loudest thing in the header. Derek, on #321: "make
    /// the settings icon a normal gear icon, not inside a circle." So the
    /// ring and the disc are gone and the glyph is drawn straight onto the
    /// board, in <see cref="BoardColours.BoardInk"/>, which clears
    /// game-board.md's separability bar against the header band it sits in
    /// (12.7 : 1, ΔE 77.9) and against the water behind that (10.2 : 1,
    /// ΔE 72.9).
    ///
    /// Its size and its glyph are game-board.md's own
    /// <see cref="GameBoardScreenView.SettingsButtonSize"/> and
    /// <see cref="GameBoardScreenView.SettingsGlyphSize"/>. The square is
    /// shared-components.md's <see cref="SharedButton.MinTouchTarget"/>
    /// exactly, so the touch-target invariant is met by construction with no
    /// extra number — and it is the reason the glyph, not the deleted ring,
    /// is now the raycast target: something has to fill that square and take
    /// the touch.
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

        RectTransform _rect;
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

        /// <summary>The gear — the only thing this control draws.</summary>
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

            _rect.sizeDelta = new Vector2(
                GameBoardScreenView.SettingsButtonSize,
                GameBoardScreenView.SettingsButtonSize);

            var glyphGO = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            _glyph = glyphGO.GetComponent<Text>();
            _glyph.text = GearGlyph;
            _glyph.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);

            _glyph.fontSize = (int)GameBoardScreenView.SettingsGlyphSize;
            _glyph.color = BoardColours.BoardInk;
            _glyph.alignment = TextAnchor.MiddleCenter;

            // Overflow both ways so the gear is never clipped: the glyph is
            // set at the button's own size, so a font whose gear is drawn tall
            // in its em box would otherwise lose its teeth to the rect rather
            // than simply spill a little into the header's own clear space.
            _glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
            _glyph.verticalOverflow = VerticalWrapMode.Overflow;

            // The gear takes the touch, because there is no ring left to take
            // it. uGUI hit-tests a Text over its rect rather than its ink, and
            // this rect fills the button, so the tap area is still the whole
            // SettingsButtonSize square.
            _glyph.raycastTarget = true;
            var glyphRect = _glyph.rectTransform;
            glyphRect.SetParent(_rect, worldPositionStays: false);
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;
        }
    }
}
