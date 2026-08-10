using System;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs works around — so these two are pulled in by explicit
// alias rather than a wildcard `using Frogs.Unity.UI;`, and a bare `Button`
// or `ButtonKind` in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The title screen — docs/specs/ui/title-screen.md — built to its
    /// current wireframe: `RESUME` and `NEW` side by side in the `action`
    /// row (not the single `Play` the screen used to have; see the spec
    /// page's "The invariant this page used to carry"). Composes the shared
    /// <see cref="Button"/> (#214) the same way <c>ScreenRouterAdapter</c>
    /// composes its screen roots: built entirely through the typed Unity API
    /// when the component first needs itself, no committed prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleScreenView : MonoBehaviour
    {
        // docs/specs/ui/title-screen.md#named-constants.
        public const float SafeMargin = 48f;
        public const float TitleBaselineY = 300f;
        public const float TitleSize = 160f;
        public const float TitleButtonWidth = 560f;
        public const float TitleButtonHeight = 160f;
        public const float TitleButtonGap = 48f;
        public const float TitleButtonBottomOffset = 120f;
        public const float TitleButtonLabelSize = 64f;
        public const float VersionLabelSize = 28f;

        // Declared so the no-bare-literal checklist item is satisfiable, per
        // the spec's own noted gap: this value has no agreed shape or
        // geometry anywhere on the page, and the committed mockup draws no
        // visible scrim. Deliberately not applied to any element here — see
        // this issue's PR under "Deviations and Decisions".
        public const float TitleScrimOpacity = 0.35f;

        // docs/specs/ui/title-screen.md#behaviour: "art and title fade in
        // over TitleFadeDuration (0.3 s)." Added to the spec's Named
        // constants table in the same PR that declares this field — it had
        // previously been stated only in that section's prose.
        public const float TitleFadeDuration = 0.3f;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const string TitleLabel = "Multiplying Frogs";
        const string ResumeLabel = "RESUME";
        const string NewLabel = "NEW";

        // No imported font — matches Button.cs's own choice, for the same
        // reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        const string VersionPrefix = "v";

        // Chrome colours copied verbatim from the committed mockup's CSS
        // custom properties — the same line Button.cs draws for its own
        // colours: not a shared-components.md geometry/opacity constant, so
        // not declared as a named spec constant.
        static readonly Color ArtPlaceholderColor = new Color32(0xED, 0xF1, 0xEF, 0xFF); // mockups' --bg
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color VersionColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line

        RectTransform _rect;

        RectTransform _backdropRect;
        CanvasGroup _backdropCanvasGroup;
        RectTransform _artRect;
        Image _artImage;
        RectTransform _titleRect;
        Text _titleText;

        RectTransform _actionRect;
        Button _resumeButton;
        Button _newButton;

        RectTransform _footprintRect;
        Text _versionText;

        bool _initialized;
        float _fadeElapsed;

        ScreenRouter _router;
        ISavedGameQuery _savedGameQuery = new NoSavedGameQuery();

        /// <summary>The screen's own <see cref="RectTransform"/>, sized to the full 1920 x 1200 reference canvas.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>Wraps `art` and `title` — the two elements that fade in together on entering, and nothing else.</summary>
        public CanvasGroup BackdropCanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _backdropCanvasGroup;
            }
        }

        /// <summary>The `art` region: a flat-coloured rectangle filling the canvas.</summary>
        public Image ArtImage
        {
            get
            {
                EnsureInitialized();
                return _artImage;
            }
        }

        /// <summary>The `art` region's <see cref="RectTransform"/>.</summary>
        public RectTransform ArtRect
        {
            get
            {
                EnsureInitialized();
                return _artRect;
            }
        }

        /// <summary>The `title` region: "Multiplying Frogs", as text, not a logo image.</summary>
        public Text TitleText
        {
            get
            {
                EnsureInitialized();
                return _titleText;
            }
        }

        /// <summary>The `title` region's <see cref="RectTransform"/>.</summary>
        public RectTransform TitleRect
        {
            get
            {
                EnsureInitialized();
                return _titleRect;
            }
        }

        /// <summary>The `action` region: the row `RESUME` and `NEW` sit in, centred as a whole.</summary>
        public RectTransform ActionRect
        {
            get
            {
                EnsureInitialized();
                return _actionRect;
            }
        }

        /// <summary>`RESUME` — secondary; hidden entirely while there is no saved game.</summary>
        public Button ResumeButton
        {
            get
            {
                EnsureInitialized();
                return _resumeButton;
            }
        }

        /// <summary>`NEW` — primary; navigates to game setup and decides nothing else.</summary>
        public Button NewButton
        {
            get
            {
                EnsureInitialized();
                return _newButton;
            }
        }

        /// <summary>The `footprint` region's <see cref="RectTransform"/>.</summary>
        public RectTransform FootprintRect
        {
            get
            {
                EnsureInitialized();
                return _footprintRect;
            }
        }

        /// <summary>The `footprint` region: the version string, read from <see cref="AppVersion"/>, never typed.</summary>
        public Text VersionText
        {
            get
            {
                EnsureInitialized();
                return _versionText;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            AdvanceFade(Time.deltaTime);
        }

        /// <summary>
        /// Wires the screen to the router `NEW` navigates through, and to the
        /// query that decides whether `RESUME` is laid out at all. Call once,
        /// after the view exists — the same shape as <see cref="Button.SetKind"/>
        /// being called right after <c>AddComponent</c>.
        /// </summary>
        public void Initialize(ScreenRouter router, ISavedGameQuery savedGameQuery = null)
        {
            EnsureInitialized();

            _router = router ?? throw new ArgumentNullException(nameof(router));
            _savedGameQuery = savedGameQuery ?? new NoSavedGameQuery();

            RefreshResumeVisibility();
        }

        /// <summary>
        /// Re-reads <see cref="ISavedGameQuery.HasSavedGame"/> and lays
        /// `RESUME` out (or hides it) accordingly — docs/specs/ui/title-screen.md's
        /// Anchors section: "if hidden, the row contains one button and
        /// centres it." Called once during initialisation; public so a
        /// caller can re-check after a save is written or cleared elsewhere,
        /// once that exists.
        /// </summary>
        public void RefreshResumeVisibility()
        {
            EnsureInitialized();

            var hasSavedGame = _savedGameQuery.HasSavedGame();
            _resumeButton.SetHidden(!hasSavedGame);
            LayoutActionRow();
        }

        /// <summary>
        /// Advances the entering fade by <paramref name="deltaSeconds"/>,
        /// clamped to <see cref="TitleFadeDuration"/>. A public method of its
        /// own, rather than reachable only through <see cref="Update"/>, so
        /// an EditMode test can simulate elapsed time directly — the same
        /// reasoning as <c>ScreenRouterAdapter.HandleBackButton</c>.
        /// </summary>
        public void AdvanceFade(float deltaSeconds)
        {
            EnsureInitialized();

            _fadeElapsed = Mathf.Clamp(_fadeElapsed + Mathf.Max(deltaSeconds, 0f), 0f, TitleFadeDuration);
            _backdropCanvasGroup.alpha = _fadeElapsed / TitleFadeDuration;
        }

        /// <summary>
        /// The line `footprint` shows — asserted the same way
        /// <c>HelloWorldProbeTests</c> asserts <c>HelloWorldProbe.Describe</c>:
        /// against this static, total formatting method taking the build-name
        /// string as a parameter, not against a live <c>Application.version</c>
        /// at test time. Total in the same sense <c>Describe</c> is: it never
        /// throws, because a build (or an editor session, mid-test, with no
        /// stamp applied yet — <c>PlayerSettings.bundleVersion</c> is only
        /// ever set at build time, per <c>BuildStampApplier</c>) that cannot
        /// read its own version has a broken stamp, and a title screen that
        /// cannot construct itself over that is a worse way to report it
        /// than a quiet fallback string.
        /// </summary>
        public static string FormatVersionLabel(string applicationVersion)
        {
            try
            {
                return VersionPrefix + AppVersion.ReadFromBuildName(applicationVersion);
            }
            catch (Exception error) when (error is FormatException || error is ArgumentNullException)
            {
                return VersionPrefix + applicationVersion;
            }
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as ScreenRouterAdapter and
        // Button's own EnsureInitialized. Every public entry point funnels
        // through this idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            RefreshResumeVisibility();
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

            BuildBackdrop();
            BuildAction();
            BuildFootprint();
        }

        void BuildBackdrop()
        {
            // Backdrop — art and title, the two elements that fade in
            // together on entering. docs/specs/ui/title-screen.md#behaviour:
            // "Neither button animates."
            var backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup));
            _backdropRect = (RectTransform)backdropGO.transform;
            _backdropRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_backdropRect);

            _backdropCanvasGroup = backdropGO.GetComponent<CanvasGroup>();
            _backdropCanvasGroup.alpha = 0f;
            _backdropCanvasGroup.interactable = false;
            _backdropCanvasGroup.blocksRaycasts = false;

            // art — the whole canvas, behind everything, a flat placeholder
            // block. That block is the deliverable for a shape-only POC; the
            // real splash illustration is #168, unscheduled — see this
            // issue's PR.
            var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
            _artImage = artGO.GetComponent<Image>();
            _artImage.color = ArtPlaceholderColor;
            _artImage.raycastTarget = false;
            _artRect = _artImage.rectTransform;
            _artRect.SetParent(_backdropRect, worldPositionStays: false);
            StretchToFill(_artRect);

            // title — text, not a logo image, centred horizontally, its
            // baseline TitleBaselineY from the top.
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            _titleText = titleGO.GetComponent<Text>();
            _titleText.text = TitleLabel;
            _titleText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _titleText.fontSize = (int)TitleSize;
            _titleText.alignment = TextAnchor.UpperCenter;
            _titleText.color = InkColor;
            _titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _titleText.verticalOverflow = VerticalWrapMode.Overflow;
            _titleText.raycastTarget = false;

            _titleRect = _titleText.rectTransform;
            _titleRect.SetParent(_backdropRect, worldPositionStays: false);
            _titleRect.anchorMin = new Vector2(0.5f, 1f);
            _titleRect.anchorMax = new Vector2(0.5f, 1f);
            _titleRect.pivot = new Vector2(0.5f, 1f);
            _titleRect.sizeDelta = Vector2.zero;
            _titleRect.anchoredPosition = new Vector2(0f, -TitleBaselineY);
        }

        void BuildAction()
        {
            // action — RESUME (secondary) and NEW (primary), side by side,
            // the row centred as a whole — docs/specs/ui/title-screen.md's
            // settled open question 1.
            var actionGO = new GameObject("Action", typeof(RectTransform));
            _actionRect = (RectTransform)actionGO.transform;
            _actionRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_actionRect);

            _resumeButton = CreateActionButton("Resume", ButtonKind.Secondary, ResumeLabel);

            // RESUME's Clicked is deliberately left unwired: it is always
            // hidden today (no saved game ever exists — see
            // ISavedGameQuery), so it can never be tapped, and what it would
            // restore is the save format's business
            // (ADR-0004), not settled here. Wiring a handler now would be
            // guessing at a mechanism that does not exist yet.
            _newButton = CreateActionButton("New", ButtonKind.Primary, NewLabel);
            _newButton.Clicked += HandleNewClicked;
        }

        Button CreateActionButton(string name, ButtonKind kind, string label)
        {
            var buttonGO = new GameObject(name, typeof(RectTransform));
            buttonGO.transform.SetParent(_actionRect, worldPositionStays: false);

            var button = buttonGO.AddComponent<Button>();
            button.SetKind(kind);
            button.SetSize(TitleButtonWidth, TitleButtonHeight);
            button.SetLabelSize(TitleButtonLabelSize);
            button.SetLabelText(label);

            return button;
        }

        void BuildFootprint()
        {
            // footprint — version string, bottom-left, small and quiet.
            var footprintGO = new GameObject("Footprint", typeof(RectTransform));
            _footprintRect = (RectTransform)footprintGO.transform;
            _footprintRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_footprintRect);

            var versionGO = new GameObject("Version", typeof(RectTransform), typeof(Text));
            _versionText = versionGO.GetComponent<Text>();
            _versionText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _versionText.fontSize = (int)VersionLabelSize;
            _versionText.alignment = TextAnchor.LowerLeft;
            _versionText.color = VersionColor;
            _versionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _versionText.verticalOverflow = VerticalWrapMode.Overflow;
            _versionText.raycastTarget = false;
            _versionText.text = FormatVersionLabel(Application.version);

            var versionRect = _versionText.rectTransform;
            versionRect.SetParent(_footprintRect, worldPositionStays: false);
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = Vector2.zero;
            versionRect.pivot = Vector2.zero;
            versionRect.sizeDelta = Vector2.zero;
            versionRect.anchoredPosition = new Vector2(SafeMargin, SafeMargin);
        }

        void HandleNewClicked()
        {
            // NEW does exactly one thing: ask the router to go to game
            // setup. No player roster, no Core call, no game-start side
            // effect — docs/specs/ui/title-screen.md's Elements section.
            _router?.NavigateToScreen(CoreScreen.GameSetup);
        }

        // The row is centred as a whole, not per button —
        // docs/specs/ui/title-screen.md's Anchors section. With RESUME
        // hidden, that falls out for free: NEW alone, centred, exactly
        // where the old single Play button sat.
        void LayoutActionRow()
        {
            var halfSpan = (TitleButtonWidth / 2f) + (TitleButtonGap / 2f);
            var y = SafeMargin + TitleButtonBottomOffset;

            if (_resumeButton.IsHidden)
            {
                PositionActionButton(_newButton, 0f, y);
            }
            else
            {
                PositionActionButton(_resumeButton, -halfSpan, y);
                PositionActionButton(_newButton, halfSpan, y);
            }
        }

        static void PositionActionButton(Button button, float x, float y)
        {
            var rect = button.RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
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
