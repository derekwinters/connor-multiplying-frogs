using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LetteringEffect = Frogs.Unity.UI.LetteringEffect;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The title screen's `START` control, drawn as the wooden sign's plank —
    /// docs/specs/ui/title-screen.md, `start` region, as #484 settled it and
    /// #520 builds it. Every number is a `TitleSign*` constant on
    /// <see cref="TitleScreenView"/>, named as on the spec page.
    ///
    /// It is not the shared <see cref="Frogs.Unity.UI.Button"/>: that
    /// component builds its own border, fill and label and redraws them in its
    /// kind's colours on every press, which is the green primary style this
    /// sign is deliberately not drawn in. It keeps the shared Button's click
    /// contract instead, exactly — <see cref="Clicked"/> fires on release,
    /// only when the release lands on the plank and the press began there;
    /// sliding off cancels.
    ///
    /// Its pressed look is the spec's open question 9, settled 2026-11-26 as
    /// "Press down": while held, the plank shrinks to
    /// <see cref="TitleScreenView.TitleSignPressScale"/> about its own centre.
    /// Only <see cref="VisualRoot"/> shrinks — the tap target, this
    /// component's own rectangle, never does, the same split the shared Button
    /// makes between its bounds and its moving visual.
    ///
    /// The post is not part of this component. It is drawing only, and is not
    /// part of the tap target.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleSignButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const string LabelText = "START";

        // A colour that draws nothing but still catches the tap: a uGUI
        // Graphic raycasts on its rectangle whatever its alpha.
        static readonly Color Invisible = new Color(1f, 1f, 1f, 0f);

        static Sprite s_outlineSprite;
        static Sprite s_woodSprite;

        RectTransform _rect;
        Image _tapTarget;
        RectTransform _visualRoot;
        Image _outline;
        Image _wood;
        Text _label;

        bool _initialized;
        bool _isPressed;

        /// <summary>Fires on release, only when the release lands on the plank and the press began there.</summary>
        public event Action Clicked;

        /// <summary>The plank's own rectangle: its layout, its tilt, and its tap target. Never scaled.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The one raycast target: an invisible graphic on the plank's own rectangle.</summary>
        public Image TapTarget
        {
            get
            {
                EnsureInitialized();
                return _tapTarget;
            }
        }

        /// <summary>Everything drawn on the plank. Shrinks to <see cref="TitleScreenView.TitleSignPressScale"/> while held.</summary>
        public RectTransform VisualRoot
        {
            get
            {
                EnsureInitialized();
                return _visualRoot;
            }
        }

        /// <summary>The plank's dark outline: the whole plank, rounded, in `TitleSignOutline`.</summary>
        public Image OutlineImage
        {
            get
            {
                EnsureInitialized();
                return _outline;
            }
        }

        /// <summary>The wood inside the outline: its gradient and grain, painted once.</summary>
        public Image WoodImage
        {
            get
            {
                EnsureInitialized();
                return _wood;
            }
        }

        /// <summary>The word `START`, in Fredoka Bold.</summary>
        public Text Label
        {
            get
            {
                EnsureInitialized();
                return _label;
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

        void Awake()
        {
            EnsureInitialized();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureInitialized();
            _isPressed = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureInitialized();

            var wasPressed = _isPressed;
            _isPressed = false;
            RefreshVisual();

            if (!wasPressed)
            {
                return;
            }

            // Acts on release, and only when the release lands on the plank —
            // the shared Button's "a finger that lands wrong can slide off and
            // cancel". The rectangle is the plank's own, tilt included.
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

        // The same idempotent guard as the shared Button and every view:
        // Unity does not promise Awake() has run before a caller or a test
        // reaches this right after AddComponent.
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
            _rect.sizeDelta = new Vector2(TitleScreenView.TitleSignWidth, TitleScreenView.TitleSignHeight);
            _rect.pivot = new Vector2(0.5f, 0.5f);

            _tapTarget = gameObject.AddComponent<Image>();
            _tapTarget.color = Invisible;
            _tapTarget.raycastTarget = true;

            var visualGO = new GameObject("Visual", typeof(RectTransform));
            _visualRoot = (RectTransform)visualGO.transform;
            _visualRoot.SetParent(_rect, worldPositionStays: false);
            _visualRoot.pivot = new Vector2(0.5f, 0.5f);
            StretchToFill(_visualRoot, 0f);

            // The outline is the whole plank in the outline colour; the wood
            // covers all of it but a TitleSignOutlineWidth rim. That is the
            // mockup's `border:7px solid` on a `border-radius:18px` box, whose
            // inside corners are 18 - 7 = 11 px.
            var outlineGO = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            _outline = outlineGO.GetComponent<Image>();
            _outline.sprite = OutlineSprite;
            _outline.type = Image.Type.Sliced;
            _outline.color = TitleScreenView.TitleSignOutline;
            _outline.raycastTarget = false;
            _outline.rectTransform.SetParent(_visualRoot, worldPositionStays: false);
            StretchToFill(_outline.rectTransform, 0f);

            var woodGO = new GameObject("Wood", typeof(RectTransform), typeof(Image));
            _wood = woodGO.GetComponent<Image>();
            _wood.sprite = WoodSprite;
            _wood.type = Image.Type.Simple;
            _wood.color = Color.white;
            _wood.raycastTarget = false;
            _wood.rectTransform.SetParent(_visualRoot, worldPositionStays: false);
            StretchToFill(_wood.rectTransform, TitleScreenView.TitleSignOutlineWidth);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            _label = labelGO.GetComponent<Text>();
            _label.text = LabelText;
            _label.font = Resources.Load<Font>(TitleScreenView.SignFontResource);
            _label.fontStyle = FontStyle.Normal;
            _label.fontSize = (int)TitleScreenView.TitleSignLabelSize;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = TitleScreenView.TitleSignLabelFill;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            _label.rectTransform.SetParent(_visualRoot, worldPositionStays: false);
            StretchToFill(_label.rectTransform, 0f);

            var lettering = labelGO.AddComponent<LetteringEffect>();
            lettering.Configure(
                TitleScreenView.TitleSignLabelTracking,
                TitleScreenView.TitleSignLabelOutlineWidth,
                TitleScreenView.TitleSignOutline);
        }

        void RefreshVisual()
        {
            // "Press down": smaller while held, nothing else — no colour
            // change, no travel. Applied at once, as the shared Button applies
            // its pressed state; neither tweens.
            var scale = _isPressed ? TitleScreenView.TitleSignPressScale : 1f;
            _visualRoot.localScale = new Vector3(scale, scale, 1f);
        }

        static Sprite OutlineSprite
        {
            get
            {
                if (s_outlineSprite == null)
                {
                    s_outlineSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(TitleScreenView.TitleSignCornerRadius));
                }

                return s_outlineSprite;
            }
        }

        static Sprite WoodSprite
        {
            get
            {
                if (s_woodSprite == null)
                {
                    s_woodSprite = PaintWood();
                }

                return s_woodSprite;
            }
        }

        /// <summary>
        /// The wood inside the outline, one texture pixel per canvas pixel:
        /// a vertical gradient from `TitleSignBark` at the top to
        /// `TitleSignBarkShade` at the bottom, with a `TitleSignGrainWidth`
        /// line of `TitleSignGrain` over it at the end of every
        /// `TitleSignGrainSpacing` — the mockup's
        /// <c>repeating-linear-gradient(180deg, transparent 0 28px, grain 28px 32px)</c>
        /// over <c>linear-gradient(180deg, bark, bark-mid)</c>, mixed the way
        /// a browser mixes them, in the colours' own (sRGB) values. Its
        /// corners are rounded to the outline's inside radius.
        /// </summary>
        static Sprite PaintWood()
        {
            var inset = 2f * TitleScreenView.TitleSignOutlineWidth;
            var width = (int)(TitleScreenView.TitleSignWidth - inset);
            var height = (int)(TitleScreenView.TitleSignHeight - inset);
            var radius = TitleScreenView.TitleSignCornerRadius - TitleScreenView.TitleSignOutlineWidth;

            var spacing = (int)TitleScreenView.TitleSignGrainSpacing;
            var grainWidth = (int)TitleScreenView.TitleSignGrainWidth;
            var grain = TitleScreenView.TitleSignGrain;
            var grainOpaque = new Color(grain.r, grain.g, grain.b, 1f);

            var pixels = new Color32[width * height];

            for (var rowFromTop = 0; rowFromTop < height; rowFromTop++)
            {
                var t = (rowFromTop + 0.5f) / height;
                var colour = Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, t);

                if ((rowFromTop % spacing) >= spacing - grainWidth)
                {
                    colour = Color.Lerp(colour, grainOpaque, grain.a);
                }

                // A texture's row 0 is its bottom.
                var y = height - 1 - rowFromTop;

                for (var x = 0; x < width; x++)
                {
                    var c = colour;
                    c.a = RoundedCoverage(x, y, width, height, radius);
                    pixels[(y * width) + x] = c;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Frogs Title Sign Wood (procedural)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            // RoundedRectSprite's 1:1 mapping: CanvasScaler's default
            // referencePixelsPerUnit.
            const float pixelsPerUnit = 100f;
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        // How much of pixel (x, y) a box this size, rounded by `radius`,
        // covers — RoundedRectSprite's signed distance, with its one-pixel
        // soft edge.
        static float RoundedCoverage(int x, int y, int width, int height, float radius)
        {
            var px = Mathf.Abs(x + 0.5f - (width / 2f));
            var py = Mathf.Abs(y + 0.5f - (height / 2f));
            var dx = Mathf.Max(px - ((width / 2f) - radius), 0f);
            var dy = Mathf.Max(py - ((height / 2f) - radius), 0f);
            var distance = Mathf.Sqrt((dx * dx) + (dy * dy)) - radius;

            return Mathf.Clamp01(0.5f - distance);
        }

        static void StretchToFill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
