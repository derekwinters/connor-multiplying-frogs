using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The one button in the game — docs/specs/ui/shared-components.md#button.
    /// Built entirely through the typed Unity API when it first needs itself,
    /// the way <c>HelloWorldScene.cs</c> builds the committed scene: no
    /// committed <c>.prefab</c>, because there is no editor here to author one
    /// honestly (issue #214). Three <see cref="ButtonKind"/>s share every
    /// geometry constant below and differ only in colour and weight; three
    /// more states — <see cref="SetDisabled"/>, hidden via
    /// <see cref="SetHidden"/>, and pressed, tracked internally — round out
    /// the six the spec's States table lists.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Button : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        // docs/specs/ui/shared-components.md#button — Named constants.
        public const float MinTouchTarget = 96f;
        public const float ButtonHeight = 112f;
        public const float ButtonMinWidth = 320f;
        public const float ButtonPaddingX = 48f;
        public const float ButtonRadius = 24f;
        public const float ButtonLabelSize = 44f;
        public const float ButtonGap = 32f;
        public const float ButtonDestructiveGap = 96f;
        public const float ButtonPressOffset = 4f;
        public const float ButtonBorderWidth = 4f;
        public const float ButtonDisabledOpacity = 0.40f;

        // How much darker the fill/border go while pressed. The spec states
        // "fill darkens" without a number — this is a rendering detail the
        // spec leaves to presentation (ADR-0001), not a geometry or tuning
        // value with a named row of its own.
        const float PressedDarkenFactor = 0.85f;

        // Chrome colours. Not on shared-components.md's Named constants table
        // — that table is geometry and opacity, not colour — so these are not
        // declared as named spec constants; they are copied verbatim from the
        // committed mockups' CSS custom properties (every mockup under
        // docs/specs/ui/mockups/ defines the same four), which is the agreed
        // picture this component is built to match. See this issue's PR for
        // where that line is drawn.
        static readonly Color AccentColor = new Color32(0x2E, 0x7D, 0x4F, 0xFF); // mockups' --accent
        static readonly Color WarningColor = new Color32(0xB0, 0x3A, 0x2E, 0xFF); // mockups' --warn
        static readonly Color OutlineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color DarkLabelColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LightLabelColor = Color.white; // mockups' --paper / #fff labels
        static readonly Color NoFillColor = new Color(1f, 1f, 1f, 0f); // "no fill" — fully transparent

        // No imported texture, sprite, or font — docs/specs/ui/shared-components.md
        // "no external assets". A uGUI Image needs *some* sprite to draw a
        // rounded rect rather than a plain rectangle, so this uses the sprite
        // Unity ships with every editor and player build for exactly this
        // purpose (ADR-0005), not a project asset.
        const string BuiltinButtonSpriteName = "UI/Skin/UISprite.psd";
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        static Sprite s_buttonSprite;

        static Sprite ButtonSprite
        {
            get
            {
                if (s_buttonSprite == null)
                {
                    s_buttonSprite = Resources.GetBuiltinResource<Sprite>(BuiltinButtonSpriteName);
                }

                return s_buttonSprite;
            }
        }

        [SerializeField] ButtonKind _kind = ButtonKind.Primary;

        RectTransform _rect;
        RectTransform _visualRoot;
        Image _border;
        Image _fill;
        Text _label;
        CanvasGroup _canvasGroup;

        bool _initialized;
        bool _isPressed;
        bool _isDisabled;

        Color _defaultBorderColor;
        Color _defaultFillColor;
        Color _defaultLabelColor;

        /// <summary>Fires on release, only when the release lands over the button and it was the one pressed.</summary>
        public event Action Clicked;

        /// <summary>The button's own <see cref="RectTransform"/> — its layout and hit-test bounds.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The child that moves by <see cref="ButtonPressOffset"/> while pressed, so the outer bounds never do.</summary>
        public RectTransform VisualRoot
        {
            get
            {
                EnsureInitialized();
                return _visualRoot;
            }
        }

        /// <summary>The button's label.</summary>
        public Text Label
        {
            get
            {
                EnsureInitialized();
                return _label;
            }
        }

        /// <summary>The <see cref="CanvasGroup"/> that carries <see cref="ButtonDisabledOpacity"/>.</summary>
        public CanvasGroup CanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _canvasGroup;
            }
        }

        public ButtonKind Kind
        {
            get
            {
                EnsureInitialized();
                return _kind;
            }
        }

        public bool IsDisabled
        {
            get
            {
                EnsureInitialized();
                return _isDisabled;
            }
        }

        public bool IsPressed
        {
            get
            {
                EnsureInitialized();
                return _isPressed;
            }
        }

        /// <summary>Not laid out at all — the shared component's Hidden state.</summary>
        public bool IsHidden => !gameObject.activeSelf;

        public Color FillColor
        {
            get
            {
                EnsureInitialized();
                return _fill.color;
            }
        }

        public Color BorderColor
        {
            get
            {
                EnsureInitialized();
                return _border.color;
            }
        }

        public Color LabelColor
        {
            get
            {
                EnsureInitialized();
                return _label.color;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Sets the kind. Touches colour and weight only, never geometry.</summary>
        public void SetKind(ButtonKind kind)
        {
            EnsureInitialized();
            _kind = kind;
            ApplyKindColours();
            RefreshVisual();
        }

        public void SetLabelText(string text)
        {
            EnsureInitialized();
            _label.text = text;
        }

        /// <summary>
        /// Overrides the button's default size. Never smaller than
        /// <see cref="MinTouchTarget"/> in either direction, at any
        /// caller-supplied size — docs/specs/ui/shared-components.md#button's
        /// first invariant.
        /// </summary>
        public void SetSize(float width, float height)
        {
            EnsureInitialized();
            _rect.sizeDelta = new Vector2(Mathf.Max(width, MinTouchTarget), Mathf.Max(height, MinTouchTarget));
        }

        /// <summary>Disabled does nothing at all and does not explain itself.</summary>
        public void SetDisabled(bool disabled)
        {
            EnsureInitialized();
            _isDisabled = disabled;

            if (disabled)
            {
                _isPressed = false;
            }

            RefreshVisual();
        }

        /// <summary>Hidden is not laid out at all — buttons do not leave gaps behind.</summary>
        public void SetHidden(bool hidden)
        {
            EnsureInitialized();
            gameObject.SetActive(!hidden);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();

            if (_isDisabled)
            {
                return;
            }

            _isPressed = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();

            if (_isDisabled)
            {
                return;
            }

            var wasPressed = _isPressed;
            _isPressed = false;
            RefreshVisual();

            if (!wasPressed)
            {
                return;
            }

            // Acts on release, and only when the release lands over the
            // button — docs/specs/ui/shared-components.md#button's Behaviour:
            // "a finger that lands wrong can slide off and cancel."
            if (RectTransformUtility.RectangleContainsScreenPoint(_rect, eventData.position, eventData.pressEventCamera))
            {
                Clicked?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureInitialized();

            if (!_isPressed)
            {
                return;
            }

            _isPressed = false;
            RefreshVisual();
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as ScreenRouterAdapter.EnsureInitialized. Every
        // public entry point funnels through this idempotent guard instead of
        // trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            ApplyKindColours();
            RefreshVisual();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            _rect.sizeDelta = new Vector2(ButtonMinWidth, ButtonHeight);

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Always blocks — a disabled button swallows the tap rather than
            // letting it pass through to whatever is behind it.
            _canvasGroup.blocksRaycasts = true;

            var visualGO = new GameObject("Visual", typeof(RectTransform));
            _visualRoot = visualGO.GetComponent<RectTransform>();
            _visualRoot.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_visualRoot);

            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            _border = borderGO.GetComponent<Image>();
            _border.sprite = ButtonSprite;
            _border.type = Image.Type.Sliced;
            _border.raycastTarget = true;
            var borderRect = _border.rectTransform;
            borderRect.SetParent(_visualRoot, worldPositionStays: false);
            StretchToFill(borderRect);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _fill = fillGO.GetComponent<Image>();
            _fill.sprite = ButtonSprite;
            _fill.type = Image.Type.Sliced;
            _fill.raycastTarget = false;
            var fillRect = _fill.rectTransform;
            fillRect.SetParent(_visualRoot, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(ButtonBorderWidth, ButtonBorderWidth);
            fillRect.offsetMax = new Vector2(-ButtonBorderWidth, -ButtonBorderWidth);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            _label = labelGO.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _label.fontSize = (int)ButtonLabelSize;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var labelRect = _label.rectTransform;
            labelRect.SetParent(_visualRoot, worldPositionStays: false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(ButtonPaddingX, 0f);
            labelRect.offsetMax = new Vector2(-ButtonPaddingX, 0f);
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void ApplyKindColours()
        {
            switch (_kind)
            {
                case ButtonKind.Primary:
                    _defaultBorderColor = AccentColor;
                    _defaultFillColor = AccentColor;
                    _defaultLabelColor = LightLabelColor;
                    break;

                case ButtonKind.Secondary:
                    _defaultBorderColor = OutlineColor;
                    _defaultFillColor = NoFillColor;
                    _defaultLabelColor = DarkLabelColor;
                    break;

                case ButtonKind.Destructive:
                    _defaultBorderColor = WarningColor;
                    _defaultFillColor = NoFillColor;
                    _defaultLabelColor = WarningColor;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_kind), _kind, null);
            }
        }

        void RefreshVisual()
        {
            _border.color = _isPressed ? Darken(_defaultBorderColor) : _defaultBorderColor;
            _fill.color = _isPressed ? Darken(_defaultFillColor) : _defaultFillColor;
            _label.color = _defaultLabelColor;

            _visualRoot.anchoredPosition = _isPressed ? new Vector2(0f, -ButtonPressOffset) : Vector2.zero;

            _canvasGroup.alpha = _isDisabled ? ButtonDisabledOpacity : 1f;
            _canvasGroup.interactable = !_isDisabled;
        }

        static Color Darken(Color colour)
        {
            return new Color(colour.r * PressedDarkenFactor, colour.g * PressedDarkenFactor, colour.b * PressedDarkenFactor, colour.a);
        }
    }
}
