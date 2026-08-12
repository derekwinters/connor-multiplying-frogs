using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The one dialog panel in the game —
    /// docs/specs/ui/shared-components.md#dialog. Built entirely through the
    /// typed Unity API, no committed <c>.prefab</c>, the same decision #214
    /// made for <see cref="Button"/> (issue #219).
    ///
    /// This type owns the panel's own contract only: what a dialog looks
    /// like, how its title/body/button row are laid out and spaced, how it
    /// opens and closes, and — per instance — which of its buttons is the
    /// least destructive one. It has no concept of z-order, of a
    /// parent-dialog, or of "the dialog already open" — <see cref="Open"/>
    /// takes no such argument, and no member on this type names one. That
    /// absence is deliberate: at-most-one-dialog (both that at most one is
    /// current, and that at most one is ever instantiated) is owned entirely
    /// by the screen router's dialog layer (#213); this component structurally
    /// cannot compete with that guarantee, and does not try to.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DialogPanel : MonoBehaviour
    {
        // docs/specs/ui/shared-components.md#dialog — Named constants.
        public const float DialogScrimOpacity = 0.66f;
        public const float DialogRadius = 32f;
        public const float DialogPadding = 56f;
        public const float DialogTitleSize = 56f;
        public const float DialogTitleGap = 40f;
        public const float DialogButtonRowGap = 48f;
        public const float DialogFadeDuration = 0.15f;

        // The one canvas every component is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // "DialogMaxWidth and DialogMaxHeight are the canvas inset by 48 px
        // on every side" — built as that relationship, not two more
        // independent literals, so a change to the canvas or the inset only
        // has one place to change.
        const float DialogMargin = 48f;
        public const float DialogMaxWidth = CanvasWidth - (DialogMargin * 2f);
        public const float DialogMaxHeight = CanvasHeight - (DialogMargin * 2f);

        // No imported texture, sprite, or font — "no external assets".
        // Matches Button.cs's own choice of font, and RoundedRectSprite's
        // technique for a rounded panel with none to import — see that
        // type's own comment for why Resources.GetBuiltinResource<Sprite>
        // is not used here.
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockups' CSS
        // custom properties, the same line Button.cs and TitleScreenView.cs
        // both draw for their own colours — not a shared-components.md
        // geometry/opacity constant, so not declared as a named spec
        // constant.
        static readonly Color ScrimColor = new Color(0f, 0f, 0f, DialogScrimOpacity); // mockups' scrim over --ink
        static readonly Color PanelColor = Color.white; // mockups' --paper / #fff panel
        static readonly Color TitleColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink

        static Sprite s_panelSprite;

        static Sprite PanelSprite
        {
            get
            {
                if (s_panelSprite == null)
                {
                    s_panelSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(DialogRadius));
                }

                return s_panelSprite;
            }
        }

        RectTransform _rect;
        CanvasGroup _canvasGroup;
        Image _scrim;
        Image _panelImage;
        RectTransform _panelRect;
        Text _titleText;
        RectTransform _bodyRect;
        RectTransform _buttonRowRect;

        readonly List<Button> _buttons = new List<Button>();

        bool _initialized;
        bool _isOpen;
        float _fadeElapsed;
        Button _leastDestructiveButton;

        /// <summary>The root, full-canvas <see cref="RectTransform"/> — scrim and panel both sit under this.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>Drives the open/close cross-fade over <see cref="DialogFadeDuration"/> — never the panel's position.</summary>
        public CanvasGroup CanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _canvasGroup;
            }
        }

        /// <summary>The dim over the screen underneath. Always present — a Dialog never renders without one.</summary>
        public Image Scrim
        {
            get
            {
                EnsureInitialized();
                return _scrim;
            }
        }

        /// <summary>The panel itself — its size is the caller-supplied one, clamped to <see cref="DialogMaxWidth"/> x <see cref="DialogMaxHeight"/>.</summary>
        public RectTransform PanelRect
        {
            get
            {
                EnsureInitialized();
                return _panelRect;
            }
        }

        /// <summary>The dialog's title.</summary>
        public Text TitleText
        {
            get
            {
                EnsureInitialized();
                return _titleText;
            }
        }

        /// <summary>The body area a caller composes its own content into — a card, a working-out grid, a confirm's cost sentence.</summary>
        public RectTransform BodyRect
        {
            get
            {
                EnsureInitialized();
                return _bodyRect;
            }
        }

        /// <summary>The row <see cref="Button"/>s (#214) are added to, bottom of the panel, primary rightmost.</summary>
        public RectTransform ButtonRowRect
        {
            get
            {
                EnsureInitialized();
                return _buttonRowRect;
            }
        }

        /// <summary>Every button added to this dialog, in the order it was added.</summary>
        public IReadOnlyList<Button> Buttons
        {
            get
            {
                EnsureInitialized();
                return _buttons;
            }
        }

        /// <summary>
        /// Which button is least destructive, for this dialog instance — the
        /// value the router (#213) reads and invokes on hardware back for a
        /// dialog that decides something. This type exposes the value; it
        /// does not itself listen for hardware back.
        /// </summary>
        public Button LeastDestructiveButton
        {
            get
            {
                EnsureInitialized();
                return _leastDestructiveButton;
            }
        }

        /// <summary>True from <see cref="Open"/> until <see cref="Close"/>.</summary>
        public bool IsOpen
        {
            get
            {
                EnsureInitialized();
                return _isOpen;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Sets the title text.</summary>
        public void SetTitle(string text)
        {
            EnsureInitialized();
            _titleText.text = text;
        }

        /// <summary>
        /// Sets the panel's size to the caller's request, clamped down to
        /// <see cref="DialogMaxWidth"/> x <see cref="DialogMaxHeight"/> if it
        /// exceeds either. A size at or below the cap is honoured exactly —
        /// the cap is a ceiling on how big a dialog may be, not the size
        /// every dialog is.
        /// </summary>
        public void SetSize(float width, float height)
        {
            EnsureInitialized();
            _panelRect.sizeDelta = new Vector2(Mathf.Min(width, DialogMaxWidth), Mathf.Min(height, DialogMaxHeight));
        }

        /// <summary>
        /// Adds a <see cref="Button"/> (#214) to the button row, in the
        /// order called — later calls sit further right, which is what puts
        /// the primary button rightmost when it is added last.
        /// </summary>
        public Button AddButton(ButtonKind kind, string label, Action onClick, bool isLeastDestructive = false)
        {
            EnsureInitialized();

            var buttonGO = new GameObject(string.IsNullOrEmpty(label) ? "Button" : label, typeof(RectTransform));
            buttonGO.transform.SetParent(_buttonRowRect, worldPositionStays: false);

            var button = buttonGO.AddComponent<Button>();
            button.SetKind(kind);
            button.SetLabelText(label);

            if (onClick != null)
            {
                button.Clicked += onClick;
            }

            _buttons.Add(button);

            if (isLeastDestructive)
            {
                _leastDestructiveButton = button;
            }

            LayoutButtonRow();

            return button;
        }

        /// <summary>
        /// Opens the dialog: the scrim and panel begin their cross-fade in
        /// from fully transparent. Takes no ordering or parent-dialog
        /// argument — see this type's own summary for why.
        /// </summary>
        public void Open()
        {
            EnsureInitialized();

            _isOpen = true;
            _fadeElapsed = 0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            RefreshFade();
        }

        /// <summary>Closes the dialog: the scrim and panel begin their cross-fade out.</summary>
        public void Close()
        {
            EnsureInitialized();

            _isOpen = false;
            _fadeElapsed = 0f;
            RefreshFade();
        }

        /// <summary>
        /// Advances the open/close cross-fade by <paramref name="deltaSeconds"/>,
        /// clamped to <see cref="DialogFadeDuration"/> — the same shape as
        /// <c>TitleScreenView.AdvanceFade</c>. A public method of its own,
        /// rather than reachable only through <c>Update</c>, so an EditMode
        /// test can simulate elapsed time directly without advancing real
        /// time, which it cannot do.
        /// </summary>
        public void AdvanceFade(float deltaSeconds)
        {
            EnsureInitialized();

            _fadeElapsed = Mathf.Clamp(_fadeElapsed + Mathf.Max(deltaSeconds, 0f), 0f, DialogFadeDuration);
            RefreshFade();
        }

        void RefreshFade()
        {
            var t = _fadeElapsed / DialogFadeDuration;
            _canvasGroup.alpha = _isOpen ? t : 1f - t;

            if (!_isOpen && _fadeElapsed >= DialogFadeDuration)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as Button and ScreenRouterAdapter's
        // own EnsureInitialized. Every public entry point funnels through
        // this idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            RefreshFade();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            // The whole canvas, not the reference rectangle. The scrim hangs
            // off this, and "a dialog always dims what is behind it" has to
            // stay true on a device whose canvas is bigger than 1920 x 1200 —
            // a scrim that stopped at the reference would leave the screen
            // underneath undimmed in a strip down each side. The panel itself
            // is centre-anchored, so it does not move when this grows:
            // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
            StretchToFill(_rect);

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            BuildScrim();
            BuildPanel();
        }

        void BuildScrim()
        {
            // The scrim: a dimmed copy of the screen underneath — "a dialog
            // always dims what is behind it". It carries no click handler of
            // any kind: "a dialog that decides something has no
            // tap-outside-to-dismiss". It still blocks the raycast, so a tap
            // on the board underneath cannot reach it while a dialog is open.
            var scrimGO = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            _scrim = scrimGO.GetComponent<Image>();
            _scrim.color = ScrimColor;
            _scrim.raycastTarget = true;

            var scrimRect = _scrim.rectTransform;
            scrimRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(scrimRect);
        }

        void BuildPanel()
        {
            var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panelImage = panelGO.GetComponent<Image>();
            _panelImage.sprite = PanelSprite;
            _panelImage.type = Image.Type.Sliced;
            _panelImage.color = PanelColor;
            _panelImage.raycastTarget = true;

            _panelRect = _panelImage.rectTransform;
            _panelRect.SetParent(_rect, worldPositionStays: false);
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(DialogMaxWidth, DialogMaxHeight);

            BuildTitle();
            BuildBody();
            BuildButtonRow();
        }

        void BuildTitle()
        {
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            _titleText = titleGO.GetComponent<Text>();
            _titleText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _titleText.fontSize = (int)DialogTitleSize;
            _titleText.color = TitleColor;
            _titleText.alignment = TextAnchor.UpperLeft;
            _titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _titleText.verticalOverflow = VerticalWrapMode.Overflow;
            _titleText.raycastTarget = false;

            var titleRect = _titleText.rectTransform;
            titleRect.SetParent(_panelRect, worldPositionStays: false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = Vector2.zero;
            titleRect.anchoredPosition = new Vector2(DialogPadding, -DialogPadding);
        }

        void BuildBody()
        {
            // A generic area, not a Text — the body is a card, a
            // working-out grid, a cost sentence, depending on the screen.
            // This issue builds the region; what a caller parents into it is
            // that screen's own issue.
            var bodyGO = new GameObject("Body", typeof(RectTransform));
            _bodyRect = (RectTransform)bodyGO.transform;
            _bodyRect.SetParent(_panelRect, worldPositionStays: false);
            _bodyRect.anchorMin = Vector2.zero;
            _bodyRect.anchorMax = Vector2.one;
            _bodyRect.offsetMin = new Vector2(DialogPadding, DialogPadding + Button.ButtonHeight + DialogButtonRowGap);
            _bodyRect.offsetMax = new Vector2(-DialogPadding, -(DialogPadding + DialogTitleSize + DialogTitleGap));
        }

        void BuildButtonRow()
        {
            var rowGO = new GameObject("ButtonRow", typeof(RectTransform));
            _buttonRowRect = (RectTransform)rowGO.transform;
            _buttonRowRect.SetParent(_panelRect, worldPositionStays: false);
            _buttonRowRect.anchorMin = new Vector2(0f, 0f);
            _buttonRowRect.anchorMax = new Vector2(1f, 0f);
            _buttonRowRect.pivot = new Vector2(0.5f, 0f);
            _buttonRowRect.offsetMin = new Vector2(DialogPadding, DialogPadding);
            _buttonRowRect.offsetMax = new Vector2(-DialogPadding, DialogPadding + Button.ButtonHeight);
        }

        // Right-to-left, so the button added last ends up rightmost — the
        // shared Button (#214) is composed here, not resized: adjacent
        // buttons keep Button.ButtonGap apart, widened to
        // Button.ButtonDestructiveGap whenever either neighbour is
        // ButtonKind.Destructive, per that component's own invariant.
        void LayoutButtonRow()
        {
            var cursor = 0f;

            for (var index = _buttons.Count - 1; index >= 0; index--)
            {
                var button = _buttons[index];
                var rect = button.RectTransform;
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-cursor, 0f);

                cursor += rect.sizeDelta.x;

                if (index > 0)
                {
                    var neighbour = _buttons[index - 1];
                    var gap = button.Kind == ButtonKind.Destructive || neighbour.Kind == ButtonKind.Destructive
                        ? Button.ButtonDestructiveGap
                        : Button.ButtonGap;
                    cursor += gap;
                }
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
