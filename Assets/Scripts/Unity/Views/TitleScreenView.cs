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
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The title screen — docs/specs/ui/title-screen.md — built to the
    /// wireframe #484 settled: Connor's splash picture, frogs, log and drawn
    /// title and all, fitted whole inside the screen's safe area in front of
    /// the same pond carried on to the screen's edges (#533), and a wooden sign in the
    /// grass bottom-right that says `START` (#520). `START` is the control
    /// that used to read `NEW`: it goes to game setup and does nothing else.
    /// `RESUME` is still a real shared <see cref="Button"/> behind the
    /// saved-game query, hidden whenever there is no save, which is always
    /// today. Built entirely through the typed Unity API when the component
    /// first needs itself, no committed prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleScreenView : MonoBehaviour
    {
        // docs/specs/ui/title-screen.md#named-constants.
        public const float SafeMargin = 48f;
        public const float TitleSignWidth = 320f;
        public const float TitleSignHeight = 128f;
        public const float TitleSignBottomOffset = 94f;
        public const float TitleSignTilt = 3f;
        public const float TitleSignCornerRadius = 18f;
        public const float TitleSignOutlineWidth = 7f;
        public const float TitleSignGrainWidth = 4f;
        public const float TitleSignGrainSpacing = 32f;
        public const float TitleSignPostWidth = 32f;
        public const float TitleSignPostDrop = 114f;
        public const float TitleSignPostOutlineWidth = 6f;
        public const float TitleSignPostCornerRadius = 8f;
        public const float TitleSignLabelSize = 72f;
        public const float TitleSignLabelOutlineWidth = 7f;
        public const float TitleSignLabelTracking = 2f;

        // Open question 9, settled 2026-11-26 ("Press down"): the plank
        // shrinks to this while held.
        public const float TitleSignPressScale = 0.95f;

        public const float VersionLabelSize = 28f;

        // docs/specs/ui/title-screen.md#behaviour: the art — the drawn title
        // with it — fades in over TitleFadeDuration.
        public const float TitleFadeDuration = 0.3f;

        // docs/specs/ui/title-screen.md#sign-colours: painted from the art,
        // not from the UI palette.
        public static readonly Color TitleSignBark = new Color32(0x9C, 0x54, 0x0C, 0xFF);
        public static readonly Color TitleSignBarkShade = new Color32(0x84, 0x3C, 0x0C, 0xFF);
        public static readonly Color TitleSignGrain = new Color32(0x54, 0x24, 0x0C, 0x8C); // at 55%: round(0.55 * 255) = 140
        public static readonly Color TitleSignOutline = new Color32(0x06, 0x0B, 0x04, 0xFF);
        public static readonly Color TitleSignLabelFill = new Color32(0xF8, 0xFA, 0xF6, 0xFF);

        /// <summary>The art, for <see cref="Resources.Load(string)"/>: Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png, written by art-source/splash/build_splash.py --home-only.</summary>
        public const string ArtResource = "TitleScreen/title-screen-1920x1200";

        /// <summary>The extended scene, for <see cref="Resources.Load(string)"/>: Assets/Art/TitleScreen/Resources/TitleScreen/title-scene-2880x1440.png, the same layers rendered past the art's frame by art-source/splash/build_splash.py --home-only (#533).</summary>
        public const string SceneResource = "TitleScreen/title-scene-2880x1440";

        // The extended scene's size in the art's reference pixels, with the art's
        // 1920 x 1200 in its middle — docs/specs/ui/title-screen.md's Anchors.
        public const float TitleSceneWidth = 2880f;
        public const float TitleSceneHeight = 1440f;

        /// <summary>The sign's font, for <see cref="Resources.Load(string)"/>: Assets/Fonts/Fredoka/Resources/Fredoka/Fredoka-Bold.ttf, the static weight-700 instance of the title's variable font.</summary>
        public const string SignFontResource = "Fredoka/Fredoka-Bold";

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        const string ResumeLabel = "RESUME";

        // The version string keeps the built-in font it always had (open
        // question 10 leaves it as it is).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        const string VersionPrefix = "v";

        // Copied verbatim from the committed mockup's CSS (--line), as it was
        // before #520; restyling it is open question 10.
        static readonly Color VersionColor = new Color32(0x6C, 0x78, 0x73, 0xFF);

        static Sprite s_postOutlineSprite;
        static Sprite s_postFillSprite;

        RectTransform _rect;
        RectTransform _contentRect;
        Image _background;

        RectTransform _backdropRect;
        CanvasGroup _backdropCanvasGroup;
        RectTransform _sceneRect;
        Image _sceneImage;
        RectTransform _artRect;
        Image _artImage;

        // What the art was last fitted to: the canvas, the screen in pixels
        // and its safe area. Refitted only when one of them changes.
        Vector2 _fittedCanvas = new Vector2(-1f, -1f);
        Vector2 _fittedScreen = new Vector2(-1f, -1f);
        Rect _fittedSafeArea;

        // Set only by UseSafeArea; otherwise the device's own are read.
        Rect? _safeAreaOverride;
        Vector2? _screenOverride;

        Button _resumeButton;

        RectTransform _signRect;
        RectTransform _postRect;
        Image _postFill;
        TitleSignButton _startButton;

        RectTransform _footprintRect;
        Text _versionText;

        bool _initialized;
        float _fadeElapsed;

        ScreenRouter _router;
        ISavedGameQuery _savedGameQuery = new NoSavedGameQuery();

        /// <summary>The screen's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The paint that reaches every edge of the screen, whatever the device's aspect ratio, under the art.</summary>
        public Image BackgroundImage
        {
            get
            {
                EnsureInitialized();
                return _background;
            }
        }

        /// <summary>Everything laid out in reference pixels — the 1920 x 1200 reference canvas, laid exactly over the fitted art: the sign, `RESUME` and the footprint.</summary>
        public RectTransform ContentRect
        {
            get
            {
                EnsureInitialized();
                FitArt();
                return _contentRect;
            }
        }

        /// <summary>Wraps `art` — the drawn title is part of it — the one thing that fades in on entering.</summary>
        public CanvasGroup BackdropCanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _backdropCanvasGroup;
            }
        }

        /// <summary>The `art` region: the whole splash composite, drawn title included.</summary>
        public Image ArtImage
        {
            get
            {
                EnsureInitialized();
                return _artImage;
            }
        }

        /// <summary>The `scene` region: the same pond carried on past the art's frame, behind the art, reaching every edge of the screen.</summary>
        public Image SceneImage
        {
            get
            {
                EnsureInitialized();
                FitArt();
                return _sceneImage;
            }
        }

        /// <summary>The `art` region's <see cref="RectTransform"/>: the whole picture, fitted inside the safe area and never cropped.</summary>
        public RectTransform ArtRect
        {
            get
            {
                EnsureInitialized();
                FitArt();
                return _artRect;
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

        /// <summary>`START` — the sign's plank; navigates to game setup and decides nothing else.</summary>
        public TitleSignButton StartButton
        {
            get
            {
                EnsureInitialized();
                return _startButton;
            }
        }

        /// <summary>The sign's post: drawing only, behind the plank, not part of the tap target.</summary>
        public RectTransform PostRect
        {
            get
            {
                EnsureInitialized();
                return _postRect;
            }
        }

        /// <summary>The post's wood, inside its outline.</summary>
        public Image PostFillImage
        {
            get
            {
                EnsureInitialized();
                return _postFill;
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
            FitArt();
            AdvanceFade(Time.deltaTime);
        }

        void OnRectTransformDimensionsChange()
        {
            if (_initialized)
            {
                FitArt();
            }
        }

        /// <summary>
        /// Fits the art, and everything laid out on it, inside
        /// <paramref name="safeAreaPixels"/> on a screen of
        /// <paramref name="screenPixels"/> — both measured the way Unity's
        /// <see cref="UnityEngine.Screen"/> reports them — from now on, in
        /// place of the device's own. Public so an EditMode test can hand the
        /// screen a phone with a cut-out; nothing in the game calls it.
        /// </summary>
        public void UseSafeArea(Rect safeAreaPixels, Vector2 screenPixels)
        {
            EnsureInitialized();

            _safeAreaOverride = safeAreaPixels;
            _screenOverride = screenPixels;
            FitArt();
        }

        void FitToSafeArea(Rect safeAreaPixels, Vector2 screenPixels)
        {
            var canvas = _rect.rect.size;
            if (canvas == _fittedCanvas && screenPixels == _fittedScreen && safeAreaPixels == _fittedSafeArea)
            {
                return;
            }

            _fittedCanvas = canvas;
            _fittedScreen = screenPixels;
            _fittedSafeArea = safeAreaPixels;

            var safeArea = TitleArtFit.SafeAreaInCanvas(
                screenPixels.x,
                screenPixels.y,
                new LayoutRect(safeAreaPixels.x, safeAreaPixels.y, safeAreaPixels.width, safeAreaPixels.height),
                canvas.x,
                canvas.y);
            var fit = TitleArtFit.Fit(canvas.x, canvas.y, safeArea, CanvasWidth, CanvasHeight);

            // Both are anchored at the canvas's centre, so each is placed by
            // its own centre's distance from it.
            var artCentre = new Vector2(
                fit.Art.X + (fit.Art.Width / 2f) - (canvas.x / 2f),
                fit.Art.Y + (fit.Art.Height / 2f) - (canvas.y / 2f));

            _artRect.sizeDelta = new Vector2(fit.Art.Width, fit.Art.Height);
            _artRect.anchoredPosition = artCentre;

            // The sign, RESUME and the footprint are laid out in reference
            // pixels on the reference canvas; it is laid over the art at the
            // art's scale, so each stays on its spot of the picture.
            _contentRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _contentRect.localScale = new Vector3(fit.Scale, fit.Scale, 1f);
            _contentRect.anchoredPosition = artCentre;

            // The scene behind it, at the art's scale and centred on it, so its
            // middle is exactly the art.
            var scene = fit.Scene(canvas.x, canvas.y, TitleSceneWidth, TitleSceneHeight);
            _sceneRect.sizeDelta = new Vector2(scene.Width, scene.Height);
            _sceneRect.anchoredPosition = new Vector2(
                scene.X + (scene.Width / 2f) - (canvas.x / 2f),
                scene.Y + (scene.Height / 2f) - (canvas.y / 2f));
        }

        /// <summary>
        /// Wires the screen to the router `START` navigates through, and to the
        /// query that decides whether `RESUME` is laid out at all. Call once,
        /// after the view exists.
        /// </summary>
        public void Initialize(ScreenRouter router, ISavedGameQuery savedGameQuery = null)
        {
            EnsureInitialized();

            _router = router ?? throw new ArgumentNullException(nameof(router));
            _savedGameQuery = savedGameQuery ?? new NoSavedGameQuery();

            RefreshResumeVisibility();
        }

        /// <summary>
        /// Re-reads <see cref="ISavedGameQuery.HasSavedGame"/> and shows or
        /// hides `RESUME` accordingly. Called once during initialisation;
        /// public so a caller can re-check after a save is written or cleared
        /// elsewhere, once that exists.
        /// </summary>
        public void RefreshResumeVisibility()
        {
            EnsureInitialized();

            _resumeButton.SetHidden(!_savedGameQuery.HasSavedGame());
        }

        /// <summary>
        /// Advances the entering fade by <paramref name="deltaSeconds"/>,
        /// clamped to <see cref="TitleFadeDuration"/>. Public so an EditMode
        /// test can simulate elapsed time directly.
        /// </summary>
        public void AdvanceFade(float deltaSeconds)
        {
            EnsureInitialized();

            _fadeElapsed = Mathf.Clamp(_fadeElapsed + Mathf.Max(deltaSeconds, 0f), 0f, TitleFadeDuration);
            _backdropCanvasGroup.alpha = _fadeElapsed / TitleFadeDuration;
        }

        /// <summary>
        /// The line `footprint` shows, from the build name. Total: it never
        /// throws, because a title screen that cannot construct itself over a
        /// broken build stamp is a worse way to report it than a quiet
        /// fallback string.
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
        // AddComponent. Every public entry point funnels through this
        // idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            RefreshResumeVisibility();
            FitArt();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            // The root fills the whole canvas, which on a device that is not
            // 16:10 is larger than the 1920 x 1200 reference.
            StretchToFill(_rect);

            // Back to front: the background paint, the art over it, then
            // everything laid out in reference pixels.
            BuildBackground();
            BuildBackdrop();
            BuildContent();

            BuildResume();
            BuildSign();
            BuildFootprint();
        }

        void BuildBackground()
        {
            // Under the art rather than part of it, because the art fades in
            // and this must not: the first frame is this colour, not whatever
            // is behind the canvas.
            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.color = ScreenColours.Background;
            _background.raycastTarget = false;

            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(backgroundRect);
        }

        void BuildBackdrop()
        {
            // The backdrop is the one thing that fades in on entering: the art,
            // with the drawn title that is part of it. The sign does not
            // animate — docs/specs/ui/title-screen.md#behaviour.
            var backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup));
            _backdropRect = (RectTransform)backdropGO.transform;
            _backdropRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(_backdropRect);

            _backdropCanvasGroup = backdropGO.GetComponent<CanvasGroup>();
            _backdropCanvasGroup.alpha = 0f;
            _backdropCanvasGroup.interactable = false;
            _backdropCanvasGroup.blocksRaycasts = false;

            // scene — the same pond carried on past the art's frame, rendered
            // from the same layers (#533), so on a screen that is not 16:10 the
            // room round the fitted art is more of the picture. Placed by FitArt.
            var sceneGO = new GameObject("Scene", typeof(RectTransform), typeof(Image));
            _sceneImage = sceneGO.GetComponent<Image>();
            _sceneImage.sprite = Resources.Load<Sprite>(SceneResource);
            _sceneImage.color = Color.white;
            _sceneImage.type = Image.Type.Simple;
            _sceneImage.raycastTarget = false;

            _sceneRect = _sceneImage.rectTransform;
            _sceneRect.SetParent(_backdropRect, worldPositionStays: false);
            _sceneRect.anchorMin = new Vector2(0.5f, 0.5f);
            _sceneRect.anchorMax = new Vector2(0.5f, 0.5f);
            _sceneRect.pivot = new Vector2(0.5f, 0.5f);
            _sceneRect.sizeDelta = new Vector2(TitleSceneWidth, TitleSceneHeight);

            var artSprite = Resources.Load<Sprite>(ArtResource);

            // art — the whole splash composite, fitted whole inside the
            // safe area at its own 16:10 shape; see FitArt.
            var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
            _artImage = artGO.GetComponent<Image>();
            _artImage.sprite = artSprite;
            _artImage.color = Color.white;
            _artImage.type = Image.Type.Simple;
            _artImage.raycastTarget = false;

            _artRect = _artImage.rectTransform;
            _artRect.SetParent(_backdropRect, worldPositionStays: false);
            _artRect.anchorMin = new Vector2(0.5f, 0.5f);
            _artRect.anchorMax = new Vector2(0.5f, 0.5f);
            _artRect.pivot = new Vector2(0.5f, 0.5f);
            _artRect.anchoredPosition = Vector2.zero;
            _artRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        }

        void BuildContent()
        {
            // The reference canvas, laid over the fitted art (FitArt).
            // The sign, RESUME and the version keep their distance from this
            // — the picture's edges — not from the screen edge.
            var contentGO = new GameObject("Content", typeof(RectTransform));
            _contentRect = (RectTransform)contentGO.transform;
            _contentRect.SetParent(_rect, worldPositionStays: false);
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _contentRect.anchoredPosition = Vector2.zero;
        }

        void BuildResume()
        {
            // RESUME (secondary). Hidden whenever there is no saved game,
            // which is always today. Its Clicked is deliberately left
            // unwired: it can never be tapped yet, and what it would restore
            // is the save format's business (ADR-0004).
            //
            // NOT AGREED: where RESUME goes and how it looks when it can be
            // shown is open question 8 on docs/specs/ui/title-screen.md, for
            // the save-system issue. The row it used to share with NEW is
            // gone with its constants, so it is given no layout of its own
            // here — it keeps the shared Button's default size, at the
            // reference canvas's centre — rather than a position nobody chose.
            var resumeGO = new GameObject("Resume", typeof(RectTransform));
            resumeGO.transform.SetParent(_contentRect, worldPositionStays: false);

            _resumeButton = resumeGO.AddComponent<Button>();
            _resumeButton.SetKind(ButtonKind.Secondary);
            _resumeButton.SetLabelText(ResumeLabel);
        }

        void BuildSign()
        {
            // start — the sign: a post, and the plank in front of it.
            var signGO = new GameObject("Sign", typeof(RectTransform));
            _signRect = (RectTransform)signGO.transform;
            _signRect.SetParent(_contentRect, worldPositionStays: false);
            StretchToFill(_signRect);

            // The plank's centre, measured from the canvas's bottom-right
            // corner: its right edge on the right safe area, its bottom edge
            // TitleSignBottomOffset up from the bottom safe area.
            var plankCentre = new Vector2(
                -(SafeMargin + (TitleSignWidth / 2f)),
                SafeMargin + TitleSignBottomOffset + (TitleSignHeight / 2f));

            BuildPost(plankCentre);

            var plankGO = new GameObject("Plank", typeof(RectTransform));
            var plankRect = (RectTransform)plankGO.transform;
            plankRect.SetParent(_signRect, worldPositionStays: false);
            plankRect.anchorMin = new Vector2(1f, 0f);
            plankRect.anchorMax = new Vector2(1f, 0f);
            plankRect.pivot = new Vector2(0.5f, 0.5f);
            plankRect.anchoredPosition = plankCentre;

            // Built now rather than whenever Awake gets to it, so the plank has
            // its size from the first frame (it sets its own).
            _startButton = plankGO.AddComponent<TitleSignButton>();
            _startButton.Clicked += HandleStartClicked;

            // Anticlockwise about its own centre: Unity's positive z.
            _startButton.RectTransform.localRotation = Quaternion.Euler(0f, 0f, TitleSignTilt);
        }

        void BuildPost(Vector2 plankCentre)
        {
            // Centred under the plank, upright, from behind it down to
            // TitleSignPostDrop below its bottom edge. It starts at the plank's
            // centre: everything above the plank's bottom edge is hidden behind
            // the plank, whatever the tilt, so where exactly it starts is never
            // seen.
            var plankBottom = plankCentre.y - (TitleSignHeight / 2f);
            var postBottom = plankBottom - TitleSignPostDrop;
            var postHeight = plankCentre.y - postBottom;

            var postGO = new GameObject("Post", typeof(RectTransform), typeof(Image));
            var outline = postGO.GetComponent<Image>();
            outline.sprite = PostOutlineSprite;
            outline.type = Image.Type.Sliced;
            outline.color = TitleSignOutline;
            outline.raycastTarget = false;

            _postRect = outline.rectTransform;
            _postRect.SetParent(_signRect, worldPositionStays: false);
            _postRect.anchorMin = new Vector2(1f, 0f);
            _postRect.anchorMax = new Vector2(1f, 0f);
            _postRect.pivot = new Vector2(0.5f, 0f);
            _postRect.sizeDelta = new Vector2(TitleSignPostWidth, postHeight);
            _postRect.anchoredPosition = new Vector2(plankCentre.x, postBottom);

            // The mockup's `border:6px solid` on a `border-radius:8px` box:
            // the fill's corners are 8 - 6 = 2 px.
            var fillGO = new GameObject("Wood", typeof(RectTransform), typeof(Image));
            _postFill = fillGO.GetComponent<Image>();
            _postFill.sprite = PostFillSprite;
            _postFill.type = Image.Type.Sliced;
            _postFill.color = TitleSignBarkShade;
            _postFill.raycastTarget = false;

            var fillRect = _postFill.rectTransform;
            fillRect.SetParent(_postRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(TitleSignPostOutlineWidth, TitleSignPostOutlineWidth);
            fillRect.offsetMax = new Vector2(-TitleSignPostOutlineWidth, -TitleSignPostOutlineWidth);
        }

        void BuildFootprint()
        {
            // footprint — version string, bottom-left, small and quiet.
            // Unchanged by #520.
            var footprintGO = new GameObject("Footprint", typeof(RectTransform));
            _footprintRect = (RectTransform)footprintGO.transform;
            _footprintRect.SetParent(_contentRect, worldPositionStays: false);
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

        void HandleStartClicked()
        {
            // START does exactly what NEW did: ask the router to go to game
            // setup. No player roster, no Core call, no game-start side
            // effect — docs/specs/ui/title-screen.md's Elements section.
            _router?.NavigateToScreen(CoreScreen.GameSetup);
        }

        // Fits the art inside the device's own safe area — docs/specs/ui/
        // title-screen.md's Anchors, and TitleArtFit for the arithmetic.
        void FitArt()
        {
            FitToSafeArea(
                _safeAreaOverride ?? UnityEngine.Screen.safeArea,
                _screenOverride ?? new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height));
        }

        static Sprite PostOutlineSprite
        {
            get
            {
                if (s_postOutlineSprite == null)
                {
                    s_postOutlineSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(TitleSignPostCornerRadius));
                }

                return s_postOutlineSprite;
            }
        }

        static Sprite PostFillSprite
        {
            get
            {
                if (s_postFillSprite == null)
                {
                    s_postFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt(TitleSignPostCornerRadius - TitleSignPostOutlineWidth));
                }

                return s_postFillSprite;
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
