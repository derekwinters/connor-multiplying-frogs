using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// SettingsDialogView.cs and GameBoardScreenView.cs work around — so these are
// pulled in by explicit alias rather than a wildcard `using Frogs.Unity.UI;`,
// and a bare `Button`, `ButtonKind`, `BoardColours`, `FrogColours`,
// `ScreenColours`, `LilyPadSprite` or `RoundedRectSprite` in this file always
// means the shared component's.
using BoardColours = Frogs.Unity.UI.BoardColours;
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using LilyPadSprite = Frogs.Unity.UI.LilyPadSprite;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;
using ScreenColours = Frogs.Unity.UI.ScreenColours;
// Two Core types are reachable from this screen, and only as numbers: how many
// positions a lane has, and which frog a colour is. Pulled in by alias so that
// stays visible at a glance — nothing here is handed a Game.
using FrogColour = Frogs.Core.FrogColour;
using Lane = Frogs.Core.Lane;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The five pages the settings dialog's `How to play` opens —
    /// docs/specs/ui/how-to-play.md, built to that page and its five committed
    /// 1:1 mockups (#414, from wireframe #324).
    ///
    /// **It is a screen, not a dialog**, and that is not a preference. It is
    /// opened from *inside* the settings dialog, and
    /// docs/specs/ui/shared-components.md#dialog says a dialog never opens
    /// over another dialog — so it replaces what is on screen rather than
    /// covering it. Structurally that means two things: it holds no
    /// <see cref="DialogPanel"/>, so it paints no scrim over anything, and its
    /// router entry is <c>Frogs.Core.Screen.HowToPlay</c> rather than a
    /// <c>Dialog</c>, so navigating to it closes the dialog it was opened from
    /// on the way in. Leaving is <see cref="LeaveRequested"/>, and what that
    /// returns to — the settings dialog, open, exactly as it was — is
    /// <c>AppRoot</c>'s to wire, not this type's to decide.
    ///
    /// **It changes nothing about the game.** how-to-play.md's first invariant
    /// is structural here rather than a promise: this type is never handed a
    /// <c>Game</c>, a <c>Lane</c> or a <c>ScreenRouter</c>, so there is no
    /// method it could call. The frogs in its pictures are drawings.
    ///
    /// **Nothing on it scrolls.** It is <see cref="HowToPlayPageCount"/> pages
    /// reached with `Back` and `Next`, and a page that does not fit is a page
    /// whose copy is too long.
    ///
    /// **Both buttons are real on every page.** Neither is ever hidden or
    /// disabled: on page 1 `Back` leaves the screen and on the last page
    /// `Next` reads `Done` and does the same. A control that disappears on one
    /// page of five is a control a child stops trusting.
    ///
    /// The pictures are drawn in the game's own vocabulary — the pond's real
    /// <see cref="BoardColours.PondWater"/>, <see cref="BoardColours.LogBrown"/>
    /// and lily pads, and the four real <see cref="FrogColours"/> — at the
    /// second, smaller set of sizes how-to-play.md carries for them. The one
    /// deliberate difference from the board is that **the logs are per-lane
    /// here**: a picture of a single lane needs the log at the end of *that*
    /// lane, where the board has one Start log and one End log for the whole
    /// pond.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HowToPlayScreenView : MonoBehaviour
    {
        /// <summary>The safe margin every screen keeps — docs/specs/ui/title-screen.md#named-constants.</summary>
        public const float SafeMargin = 48f;

        // docs/specs/ui/how-to-play.md#named-constants.
        public const float HowToPlayHeadingSize = 72f;
        public const float HowToPlayHeadingLineBox = 88f;
        public const float HowToPlayHeadingGap = 48f;
        public const float HowToPlayPictureInset = 56f;
        public const float HowToPlayColumnGap = 64f;
        public const float HowToPlayWordsWidth = 656f;
        public const float HowToPlayBodySize = 40f;
        public const float HowToPlayBodyLineHeight = 1.35f;
        public const float HowToPlayParagraphGap = 32f;
        public const float HowToPlayControlsGap = 48f;
        public const float HowToPlayDotSize = 20f;
        public const float HowToPlayDotGap = 24f;
        public const int HowToPlayPageCount = 5;

        /// <summary>
        /// The picture's width — how-to-play.md's "the two numbers that are
        /// derived, and the sums that derive them":
        ///
        /// <code>
        /// 1920 − 48 − 48 − 64 − 656 = 1104
        /// </code>
        ///
        /// Written as that sum rather than as the bare 1104, because it is
        /// what is *left over*: a change to <see cref="HowToPlayWordsWidth"/>
        /// or <see cref="HowToPlayColumnGap"/> has to move the picture, and a
        /// literal would let the two columns overlap instead.
        /// </summary>
        public const float HowToPlayPictureWidth = CanvasWidth
            - (2f * SafeMargin)
            - HowToPlayColumnGap
            - HowToPlayWordsWidth;

        /// <summary>
        /// The picture's height, the same way — everything anchored above and
        /// below it, taken off the canvas:
        ///
        /// <code>
        /// 1200 − 48 − 88 − 48 − 48 − 112 − 48 = 808
        /// </code>
        ///
        /// <see cref="Button.ButtonHeight"/> is the shared Button's, not a
        /// number restated here, which is why a taller button would move the
        /// picture's bottom edge rather than run into it.
        /// </summary>
        public const float HowToPlayPictureHeight = CanvasHeight
            - SafeMargin
            - HowToPlayHeadingLineBox
            - HowToPlayHeadingGap
            - HowToPlayControlsGap
            - Button.ButtonHeight
            - SafeMargin;

        // how-to-play.md#the-pond-drawn-smaller — a second set of numbers for
        // the same shapes, at a size that fits HowToPlayPictureWidth. Not a
        // rescaling rule: "70 % of the board's" is a rule that produces
        // fractional pixels.
        public const float HowToPlayPadDiameter = 64f;
        public const float HowToPlayLogWidth = 100f;
        public const float HowToPlayLogHeight = 120f;
        public const float HowToPlayFrogDiameter = 50f;
        public const float HowToPlayLaneGap = 48f;
        public const float HowToPlayLogLabelSize = 20f;

        /// <summary>
        /// The gap between two positions in a drawn lane — derived from the
        /// picture's width exactly as the board's `LanePositionGap` is derived
        /// from the screen's, and exact:
        ///
        /// <code>
        /// (1104 − 2 × 56 − 2 × 100 − 7 × 64) ÷ 8 = 43
        /// </code>
        ///
        /// The seven and the eight are <see cref="Lane.LanePositionCount"/>'s,
        /// not this screen's: a lane is nine positions, two of which are its
        /// logs, so seven pads sit between eight gaps.
        /// </summary>
        public const float HowToPlayLanePositionGap =
            (HowToPlayPictureWidth
                - (2f * HowToPlayPictureInset)
                - (2f * HowToPlayLogWidth)
                - ((Lane.LanePositionCount - 2) * HowToPlayPadDiameter))
            / (Lane.LanePositionCount - 1);

        // how-to-play.md#what-the-pictures-are-drawn-from — the numbers inside
        // the five pictures. Every one of them is a rule in a committed
        // mockup; #414 transcribed them onto the spec page rather than
        // inventing them here, which is the "distill" step of the wireframe
        // loop finishing late. No value below is new.
        public const float HowToPlayLogRadius = 20f;
        public const float HowToPlayLogLabelTopPadding = 16f;
        public const float HowToPlayLogLabelGap = 12f;
        public const float HowToPlayFrogOutline = 3f;
        public const float HowToPlayPaperOutline = 3f;
        public const float HowToPlayNoteSize = 34f;
        public const float HowToPlayNoteLineHeight = 1.4f;
        public const float HowToPlayCaptionLineBox = 52f;
        public const float HowToPlayExampleGap = 56f;
        public const float HowToPlayDieLeft = 96f;
        public const float HowToPlayDieTop = 164f;
        public const float HowToPlayDieSize = 200f;
        public const float HowToPlayDieRadius = 32f;
        public const float HowToPlayDieOutline = 4f;
        public const float HowToPlayDiePadding = 28f;
        public const float HowToPlayDiePipSize = 34f;
        public const float HowToPlayArrowLeft = 336f;
        public const float HowToPlayArrowSize = 56f;
        public const float HowToPlayPileLeft = 452f;
        public const float HowToPlayPileWidth = 200f;
        public const float HowToPlayPileHeight = 120f;
        public const float HowToPlayPileRadius = 16f;
        public const float HowToPlayPileOutline = 4f;
        public const float HowToPlayPileGap = 24f;
        public const float HowToPlayPileLabelSize = 36f;
        public const float HowToPlayPileDimOpacity = 0.4f;
        public const float HowToPlayRollTableLeft = 96f;
        public const float HowToPlayRollTableTop = 520f;
        public const float HowToPlayRollTableColumnWidth = 220f;
        public const float HowToPlayRollTableColumnGap = 32f;
        public const float HowToPlayRollTableHeaderGap = 20f;
        public const float HowToPlayGridLeft = 120f;
        public const float HowToPlayGridTop = 96f;
        public const float HowToPlayCellSize = 88f;
        public const float HowToPlayCellGap = 8f;
        public const float HowToPlayCellRadius = 8f;
        public const float HowToPlayCellOutline = 3f;
        public const float HowToPlayCellDigitSize = 48f;
        public const float HowToPlayCarryRowHeight = 56f;
        public const float HowToPlayCarryBoxWidth = 48f;
        public const float HowToPlayCarryBoxHeight = 44f;
        public const float HowToPlayCarryBoxRadius = 6f;
        public const float HowToPlayAnswerOutline = 4f;
        public const float HowToPlayCalloutLeft = 560f;
        public const float HowToPlayCalloutTop = 208f;
        public const float HowToPlayCalloutWidth = 440f;
        public const float HowToPlayCalloutGap = 40f;
        public const float HowToPlayCalloutLineHeight = 1.5f;

        // The rest of what this screen is laid out from is other pages' rows,
        // referenced under the identical name rather than redeclared:
        //
        // - `DialogRadius` is the shared Dialog's — how-to-play.md gives the
        //   picture that corner rather than a corner of its own.
        // - `ButtonHeight`, `ButtonMinWidth` and the rest of the two buttons
        //   are the shared Button's (shared-components.md#button).
        // - `DialogFadeDuration` is the shared Dialog's, which is what
        //   how-to-play.md names for the picture and words cross-fade.
        // - `LanePositionCount` is Frogs.Core.Lane's, so the drawn lane and
        //   Core's lane can never disagree about being nine positions long.
        // - the pad's notch, veins and variation table are
        //   `GameBoardLaneView`'s: the picture draws the game's own lily pad,
        //   at this page's smaller diameter.

        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // How far the picture's top sits below the top safe edge, and how much
        // of the bottom the controls row takes — the two halves of the height
        // sum above, written once because the picture is anchored between them
        // rather than sized by them.
        const float PictureTopInset = SafeMargin + HowToPlayHeadingLineBox + HowToPlayHeadingGap;
        const float ControlsBandHeight = SafeMargin + Button.ButtonHeight + HowToPlayControlsGap;

        const string BackLabel = "Back";
        const string NextLabel = "Next";
        const string DoneLabel = "Done";
        const string StartLogLabel = "Start";
        const string EndLogLabel = "End";

        // No imported font — matches Button.cs's and every other view's own
        // choice, for the same reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours, copied verbatim from the five committed mockups' CSS
        // custom properties — the same line Button.cs, TitleScreenView.cs and
        // SettingsDialogView.cs each draw. They are not geometry, so they are
        // not rows on how-to-play.md's tables. The pond's own colours are not
        // here: those are game-board.md's, received by name from
        // BoardColours, because a picture of the pond that invents its own
        // blue is a picture of a different game.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color FaintColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockups' --faint
        static readonly Color AccentColor = new Color32(0x2E, 0x7D, 0x4F, 0xFF); // mockups' --accent
        static readonly Color PaperColor = Color.white; // mockups' --paper

        static Sprite s_dotSprite;
        static Sprite s_frogSprite;
        static Sprite s_frogFillSprite;
        static readonly Dictionary<int, Sprite> s_roundedRects = new Dictionary<int, Sprite>();
        static readonly Dictionary<float, Sprite> s_lilyPadSprites = new Dictionary<float, Sprite>();

        RectTransform _rect;
        Image _background;
        Text _heading;
        RectTransform _controlsRect;
        RectTransform _progressRect;
        Button _backButton;
        Button _nextButton;

        readonly List<Image> _progressDots = new List<Image>();
        readonly List<RectTransform> _pageRects = new List<RectTransform>();
        readonly List<CanvasGroup> _pageGroups = new List<CanvasGroup>();
        readonly List<RectTransform> _pictureRects = new List<RectTransform>();
        readonly List<Image> _pictureSurfaces = new List<Image>();
        readonly List<RectTransform> _wordsRects = new List<RectTransform>();
        readonly List<List<Text>> _paragraphs = new List<List<Text>>();

        int _currentPage = FirstPage;
        int _outgoingPage;
        float _fadeElapsed;
        bool _initialized;

        const int FirstPage = 1;
        const int NoPage = 0;

        /// <summary>
        /// `Back` was pressed on page 1, or `Done` on the last page, or
        /// hardware back arrived on page 1 — one rule for all three, because
        /// how-to-play.md gives all three the same one: leave, and return to
        /// the settings dialog, open, exactly as it was. Where that is, is
        /// <c>AppRoot</c>'s.
        /// </summary>
        public event Action LeaveRequested;

        /// <summary>The view's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The paint that reaches every edge of the device — this is a screen, so nothing shows around it.</summary>
        public Image Background
        {
            get
            {
                EnsureInitialized();
                return _background;
            }
        }

        /// <summary>`heading` — the current page's own two or three words. This screen has no title of its own.</summary>
        public Text HeadingText
        {
            get
            {
                EnsureInitialized();
                return _heading;
            }
        }

        /// <summary>`controls` — the row `Back` and `Next` sit at either end of.</summary>
        public RectTransform ControlsRect
        {
            get
            {
                EnsureInitialized();
                return _controlsRect;
            }
        }

        /// <summary>`progress` — the row of dots, centred between the two buttons.</summary>
        public RectTransform ProgressRect
        {
            get
            {
                EnsureInitialized();
                return _progressRect;
            }
        }

        /// <summary>The <see cref="HowToPlayPageCount"/> dots. Not tappable — they say where you are, they are not a way to jump.</summary>
        public IReadOnlyList<Image> ProgressDots
        {
            get
            {
                EnsureInitialized();
                return _progressDots;
            }
        }

        /// <summary>`Back` — secondary, bottom-left. Never hidden, never disabled; on page 1 it leaves.</summary>
        public Button BackButton
        {
            get
            {
                EnsureInitialized();
                return _backButton;
            }
        }

        /// <summary>`Next` — primary, bottom-right. On the last page its label is `Done` and it leaves.</summary>
        public Button NextButton
        {
            get
            {
                EnsureInitialized();
                return _nextButton;
            }
        }

        /// <summary>Which page is showing, 1 to <see cref="HowToPlayPageCount"/>. Entering is always 1.</summary>
        public int CurrentPage
        {
            get
            {
                EnsureInitialized();
                return _currentPage;
            }
        }

        /// <summary>One page's root — the `picture` and `words` that cross-fade together.</summary>
        public RectTransform PageRect(int page)
        {
            EnsureInitialized();
            return _pageRects[IndexOf(page)];
        }

        /// <summary>The group one page's cross-fade is driven through.</summary>
        public CanvasGroup PageGroup(int page)
        {
            EnsureInitialized();
            return _pageGroups[IndexOf(page)];
        }

        /// <summary>`picture` on one page — the drawing, which is the point of the page.</summary>
        public RectTransform PictureRect(int page)
        {
            EnsureInitialized();
            return _pictureRects[IndexOf(page)];
        }

        /// <summary>
        /// What one page's picture is drawn *on*: the pond's own water for the
        /// three pond pages, paper for the two that are a diagram rather than
        /// a board.
        /// </summary>
        public Image PictureSurface(int page)
        {
            EnsureInitialized();
            return _pictureSurfaces[IndexOf(page)];
        }

        /// <summary>`words` on one page — the caption, top-aligned with the picture.</summary>
        public RectTransform WordsRect(int page)
        {
            EnsureInitialized();
            return _wordsRects[IndexOf(page)];
        }

        /// <summary>One page's paragraphs, in the order they are read.</summary>
        public IReadOnlyList<Text> Paragraphs(int page)
        {
            EnsureInitialized();
            return _paragraphs[IndexOf(page)];
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            // Android back arrives as Escape. This screen owns the key rather
            // than delegating it, because "back does what `Back` does — one
            // page back, and from page 1 it leaves" is a rule about which page
            // is showing, and the page is this screen's. The router is
            // deliberately inert on Screen.HowToPlay for exactly that reason,
            // so one press is acted on once — see ScreenRouter.HandleBackOnScreen.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleHardwareBack();
            }

            AdvanceFade(Time.deltaTime);
        }

        /// <summary>
        /// Entering the screen — how-to-play.md#behaviour: "It always opens on
        /// page 1. It does not remember where you got to, because remembering
        /// is a mode that arrives at the next player in whatever state the last
        /// one left it."
        ///
        /// Deliberately not a cross-fade: arriving is not paging, so page 1 is
        /// simply there.
        /// </summary>
        public void Open()
        {
            EnsureInitialized();

            _outgoingPage = NoPage;
            _fadeElapsed = 0f;
            _currentPage = FirstPage;

            for (var page = FirstPage; page <= HowToPlayPageCount; page++)
            {
                var showing = page == _currentPage;

                _pageGroups[IndexOf(page)].alpha = showing ? 1f : 0f;
                _pageRects[IndexOf(page)].gameObject.SetActive(showing);
            }

            RefreshFurniture();
        }

        /// <summary>
        /// What hardware back does — the same thing `Back` does, which on
        /// page 1 is leaving. Public so an EditMode test can press back
        /// without simulating a key, exactly as
        /// <c>GameBoardScreenView.HandleHardwareBack</c> is.
        /// </summary>
        public void HandleHardwareBack()
        {
            EnsureInitialized();
            GoBack();
        }

        /// <summary>
        /// Advances the cross-fade between the outgoing page and the incoming
        /// one by <paramref name="deltaSeconds"/>, clamped to
        /// <see cref="DialogPanel.DialogFadeDuration"/> — the duration
        /// how-to-play.md names, and the same shape
        /// <see cref="DialogPanel.AdvanceFade"/> has. A no-op when no page is
        /// on its way out.
        ///
        /// Only `picture` and `words` fade. `heading`, `progress` and
        /// `controls` are the furniture that says where you are, and furniture
        /// that moves is furniture a child chases.
        /// </summary>
        public void AdvanceFade(float deltaSeconds)
        {
            EnsureInitialized();

            if (_outgoingPage == NoPage)
            {
                return;
            }

            _fadeElapsed = Mathf.Clamp(
                _fadeElapsed + Mathf.Max(deltaSeconds, 0f), 0f, DialogPanel.DialogFadeDuration);

            var t = _fadeElapsed / DialogPanel.DialogFadeDuration;

            _pageGroups[IndexOf(_outgoingPage)].alpha = 1f - t;
            _pageGroups[IndexOf(_currentPage)].alpha = t;

            if (_fadeElapsed < DialogPanel.DialogFadeDuration)
            {
                return;
            }

            _pageRects[IndexOf(_outgoingPage)].gameObject.SetActive(false);
            _outgoingPage = NoPage;
        }

        void GoBack()
        {
            if (_currentPage <= FirstPage)
            {
                RaiseLeaveRequested();
                return;
            }

            GoToPage(_currentPage - 1);
        }

        void GoNext()
        {
            if (_currentPage >= HowToPlayPageCount)
            {
                RaiseLeaveRequested();
                return;
            }

            GoToPage(_currentPage + 1);
        }

        void GoToPage(int page)
        {
            // A press that lands mid-fade finishes the fade it interrupted
            // rather than stacking a second one: two pages fading and a third
            // arriving is a state with no picture fully on screen.
            if (_outgoingPage != NoPage)
            {
                AdvanceFade(DialogPanel.DialogFadeDuration);
            }

            _outgoingPage = _currentPage;
            _currentPage = page;
            _fadeElapsed = 0f;

            _pageGroups[IndexOf(_currentPage)].alpha = 0f;
            _pageRects[IndexOf(_currentPage)].gameObject.SetActive(true);

            RefreshFurniture();
        }

        void RaiseLeaveRequested()
        {
            var handler = LeaveRequested;
            if (handler != null)
            {
                handler();
            }
        }

        // The heading, the dots and `Next`'s label, set at once rather than
        // faded — they are what says where you are.
        void RefreshFurniture()
        {
            _heading.text = HowToPlayPages.HeadingFor(_currentPage);

            _nextButton.SetLabelText(_currentPage >= HowToPlayPageCount ? DoneLabel : NextLabel);

            for (var dot = 0; dot < _progressDots.Count; dot++)
            {
                _progressDots[dot].color = dot == IndexOf(_currentPage) ? InkColor : FaintColor;
            }
        }

        static int IndexOf(int page)
        {
            if (page < FirstPage || page > HowToPlayPageCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(page), page, $"how to play is {HowToPlayPageCount} pages, numbered from {FirstPage}.");
            }

            return page - FirstPage;
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and GameBoardScreenView's own
        // EnsureInitialized. Every public entry point funnels through this
        // idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            // The root fills the whole canvas, which on a device that is not
            // 16:10 is larger than the 1920 x 1200 reference. Everything below
            // keeps its distance from that real safe area rather than from a
            // reference rectangle inside it, and the picture keeps its width
            // and takes the difference in height — how-to-play.md#anchors.
            StretchToFill(_rect);

            BuildBackground();
            BuildHeading();
            BuildPages();
            BuildControls();
            BuildProgress();

            Open();
        }

        void BuildBackground()
        {
            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _background = backgroundGO.GetComponent<Image>();
            _background.color = ScreenColours.Background;
            _background.raycastTarget = false;

            var backgroundRect = _background.rectTransform;
            backgroundRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(backgroundRect);
        }

        void BuildHeading()
        {
            _heading = CreateText(
                "Heading", _rect, HowToPlayHeadingSize, FontStyle.Bold, InkColor, TextAnchor.UpperLeft);

            var rect = _heading.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SafeMargin, -(SafeMargin + HowToPlayHeadingLineBox));
            rect.offsetMax = new Vector2(-SafeMargin, -SafeMargin);
        }

        void BuildPages()
        {
            var pagesGO = new GameObject("Pages", typeof(RectTransform));
            var pagesRect = (RectTransform)pagesGO.transform;
            pagesRect.SetParent(_rect, worldPositionStays: false);
            StretchToFill(pagesRect);

            for (var page = FirstPage; page <= HowToPlayPageCount; page++)
            {
                BuildPage(pagesRect, page);
            }
        }

        void BuildPage(RectTransform parent, int page)
        {
            var pageGO = new GameObject("Page" + page, typeof(RectTransform), typeof(CanvasGroup));
            var pageRect = (RectTransform)pageGO.transform;
            pageRect.SetParent(parent, worldPositionStays: false);
            StretchToFill(pageRect);

            _pageRects.Add(pageRect);
            _pageGroups.Add(pageGO.GetComponent<CanvasGroup>());

            BuildPicture(pageRect, page);
            BuildWords(pageRect, page);
        }

        void BuildPicture(RectTransform parent, int page)
        {
            var isPond = HowToPlayPages.IsPondPage(page);

            var pictureGO = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            var picture = pictureGO.GetComponent<Image>();
            picture.sprite = RoundedRect(DialogPanel.DialogRadius);
            picture.type = Image.Type.Sliced;

            // A pond picture is water to its own corners; a paper one is the
            // mockups' 3 px hairline around white, which is drawn as the
            // hairline's colour with the paper laid inside it — the same
            // rim-and-fill the board's own pieces are drawn with.
            picture.color = isPond ? BoardColours.PondWater : FaintColor;
            picture.raycastTarget = false;

            var pictureRect = picture.rectTransform;
            pictureRect.SetParent(parent, worldPositionStays: false);
            pictureRect.anchorMin = new Vector2(0f, 0f);
            pictureRect.anchorMax = new Vector2(0f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.offsetMin = new Vector2(SafeMargin, ControlsBandHeight);
            pictureRect.offsetMax = new Vector2(SafeMargin + HowToPlayPictureWidth, -PictureTopInset);

            _pictureRects.Add(pictureRect);

            var surface = picture;

            if (!isPond)
            {
                var paperGO = new GameObject("Paper", typeof(RectTransform), typeof(Image));
                var paper = paperGO.GetComponent<Image>();
                paper.sprite = RoundedRect(DialogPanel.DialogRadius - HowToPlayPaperOutline);
                paper.type = Image.Type.Sliced;
                paper.color = PaperColor;
                paper.raycastTarget = false;

                var paperRect = paper.rectTransform;
                paperRect.SetParent(pictureRect, worldPositionStays: false);
                StretchToFill(paperRect);
                paperRect.offsetMin = new Vector2(HowToPlayPaperOutline, HowToPlayPaperOutline);
                paperRect.offsetMax = new Vector2(-HowToPlayPaperOutline, -HowToPlayPaperOutline);

                surface = paper;
            }

            _pictureSurfaces.Add(surface);

            switch (page)
            {
                case 1:
                    BuildLaneStack(pictureRect, HowToPlayPages.PageOneLanes);
                    break;

                case 2:
                    BuildRollThePicture(pictureRect);
                    break;

                case 3:
                    BuildWorkItOutPicture(pictureRect);
                    break;

                case 4:
                    BuildExamples(pictureRect, HowToPlayPages.PageFourExamples);
                    break;

                default:
                    BuildLaneStack(pictureRect, HowToPlayPages.PageFiveLanes);
                    break;
            }
        }

        void BuildWords(RectTransform parent, int page)
        {
            var wordsGO = new GameObject("Words", typeof(RectTransform));
            var wordsRect = (RectTransform)wordsGO.transform;
            wordsRect.SetParent(parent, worldPositionStays: false);
            wordsRect.anchorMin = new Vector2(0f, 0f);
            wordsRect.anchorMax = new Vector2(1f, 1f);
            wordsRect.pivot = new Vector2(0.5f, 0.5f);
            wordsRect.offsetMin = new Vector2(
                SafeMargin + HowToPlayPictureWidth + HowToPlayColumnGap, ControlsBandHeight);
            wordsRect.offsetMax = new Vector2(-SafeMargin, -PictureTopInset);

            _wordsRects.Add(wordsRect);

            var paragraphs = new List<Text>();
            _paragraphs.Add(paragraphs);

            // Top-aligned with the picture, never centred against it: pages
            // have different amounts to say, and text that floats to a
            // different height on every page is text that has to be re-found
            // five times.
            var cursor = 0f;

            foreach (var paragraph in HowToPlayPages.WordsFor(page))
            {
                var text = CreateText(
                    "Paragraph", wordsRect, HowToPlayBodySize, FontStyle.Normal, InkColor, TextAnchor.UpperLeft);

                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.lineSpacing = HowToPlayBodyLineHeight;
                text.text = paragraph.Text;

                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(0f, -cursor);

                // Measured, not counted: how tall a wrapped paragraph is
                // depends on the renderer's own line breaking, and a paragraph
                // laid out on a guessed line count is a paragraph that
                // overlaps the next one in the font that disagrees.
                var height = text.preferredHeight;
                rect.sizeDelta = new Vector2(0f, height);

                paragraphs.Add(text);

                // A question sticks to its own answer — the mockups' `.q` rule
                // — so the paragraph gap falls between pairs, not inside them.
                cursor += height + (paragraph.KeepsNext ? 0f : HowToPlayParagraphGap);
            }
        }

        void BuildControls()
        {
            var controlsGO = new GameObject("Controls", typeof(RectTransform));
            _controlsRect = (RectTransform)controlsGO.transform;
            _controlsRect.SetParent(_rect, worldPositionStays: false);
            _controlsRect.anchorMin = new Vector2(0f, 0f);
            _controlsRect.anchorMax = new Vector2(1f, 0f);
            _controlsRect.pivot = new Vector2(0.5f, 0f);
            _controlsRect.offsetMin = new Vector2(SafeMargin, SafeMargin);
            _controlsRect.offsetMax = new Vector2(-SafeMargin, SafeMargin + Button.ButtonHeight);

            _backButton = CreateControl("Back", ButtonKind.Secondary, BackLabel, atLeft: true);
            _backButton.Clicked += GoBack;

            // The primary button, bottom-right — the shared Dialog's own
            // button placement applied to a screen, which is also where the
            // settings dialog's `Back to the game` was: the finger does not
            // move.
            _nextButton = CreateControl("Next", ButtonKind.Primary, NextLabel, atLeft: false);
            _nextButton.Clicked += GoNext;
        }

        Button CreateControl(string name, ButtonKind kind, string label, bool atLeft)
        {
            var buttonGO = new GameObject(name, typeof(RectTransform));
            buttonGO.transform.SetParent(_controlsRect, worldPositionStays: false);

            var button = buttonGO.AddComponent<Button>();
            button.SetKind(kind);
            button.SetLabelText(label);

            var rect = button.RectTransform;
            rect.anchorMin = new Vector2(atLeft ? 0f : 1f, 0f);
            rect.anchorMax = new Vector2(atLeft ? 0f : 1f, 0f);
            rect.pivot = new Vector2(atLeft ? 0f : 1f, 0f);
            rect.anchoredPosition = Vector2.zero;

            return button;
        }

        void BuildProgress()
        {
            var run = (HowToPlayPageCount * HowToPlayDotSize) + ((HowToPlayPageCount - 1) * HowToPlayDotGap);

            var progressGO = new GameObject("Progress", typeof(RectTransform));
            _progressRect = (RectTransform)progressGO.transform;
            _progressRect.SetParent(_rect, worldPositionStays: false);

            // Centred horizontally across the full canvas and vertically on
            // the controls row, so the dots sit between the two buttons rather
            // than under them.
            _progressRect.anchorMin = new Vector2(0.5f, 0f);
            _progressRect.anchorMax = new Vector2(0.5f, 0f);
            _progressRect.pivot = new Vector2(0.5f, 0.5f);
            _progressRect.sizeDelta = new Vector2(run, HowToPlayDotSize);
            _progressRect.anchoredPosition = new Vector2(0f, SafeMargin + (Button.ButtonHeight / 2f));

            for (var dot = 0; dot < HowToPlayPageCount; dot++)
            {
                var dotGO = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                var image = dotGO.GetComponent<Image>();
                image.sprite = DotSprite;
                image.type = Image.Type.Sliced;

                // Not tappable: a row of five 20 px dots is well under
                // MinTouchTarget, and making them targets would mean making
                // them bigger than they should look.
                image.raycastTarget = false;

                var rect = image.rectTransform;
                rect.SetParent(_progressRect, worldPositionStays: false);
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(HowToPlayDotSize, HowToPlayDotSize);
                rect.anchoredPosition = new Vector2(dot * (HowToPlayDotSize + HowToPlayDotGap), 0f);

                _progressDots.Add(image);
            }
        }

        // --- The pond pictures ----------------------------------------------------

        // A stack of drawn lanes, sitting where the mockups put it: centred in
        // a picture of HowToPlayPictureHeight, held as a distance from the
        // picture's top rather than as a centring, because how-to-play.md
        // anchors the lane drawings to the picture's top-left inset — a
        // picture that grows taller on a screen that is not 16:10 grows below
        // its lanes rather than moving them.
        void BuildLaneStack(RectTransform picture, IReadOnlyList<HowToPlayPages.DrawnLane> lanes)
        {
            var height = (lanes.Count * HowToPlayLogHeight) + ((lanes.Count - 1) * HowToPlayLaneGap);
            var stack = CreateInsetColumn(picture, "LaneStack", height);

            for (var lane = 0; lane < lanes.Count; lane++)
            {
                var laneRect = CreateLane(stack, lanes[lane]);
                laneRect.anchoredPosition = new Vector2(0f, -lane * (HowToPlayLogHeight + HowToPlayLaneGap));
            }
        }

        // Page 4's three lanes are three separate examples rather than three
        // lanes of one game, so each carries its own caption.
        void BuildExamples(RectTransform picture, IReadOnlyList<HowToPlayPages.DrawnLane> examples)
        {
            var exampleHeight = HowToPlayCaptionLineBox + HowToPlayLogHeight;
            var height = (examples.Count * exampleHeight) + ((examples.Count - 1) * HowToPlayExampleGap);
            var stack = CreateInsetColumn(picture, "ExampleStack", height);

            for (var example = 0; example < examples.Count; example++)
            {
                var top = example * (exampleHeight + HowToPlayExampleGap);

                var caption = CreateText(
                    "Caption", stack, HowToPlayNoteSize, FontStyle.Bold, InkColor, TextAnchor.UpperLeft);

                var captionRect = caption.rectTransform;
                captionRect.anchorMin = new Vector2(0f, 1f);
                captionRect.anchorMax = new Vector2(1f, 1f);
                captionRect.pivot = new Vector2(0.5f, 1f);
                captionRect.sizeDelta = new Vector2(0f, HowToPlayCaptionLineBox);
                captionRect.anchoredPosition = new Vector2(0f, -top);
                caption.text = examples[example].Caption;

                var laneRect = CreateLane(stack, examples[example]);
                laneRect.anchoredPosition = new Vector2(0f, -(top + HowToPlayCaptionLineBox));
            }
        }

        /// <summary>
        /// Where a picture's stack of lanes begins, measured down from the
        /// picture's top: what is left of <see cref="HowToPlayPictureHeight"/>
        /// once the stack has taken its share, halved. The mockups draw page
        /// 1's four lanes 92 px down, and that is this sum rather than a
        /// number of their own.
        /// </summary>
        public static float LaneStackTop(float stackHeight)
        {
            return (HowToPlayPictureHeight - stackHeight) / 2f;
        }

        // A column as wide as the picture inside its two insets, at the
        // picture's own top-left inset.
        RectTransform CreateInsetColumn(RectTransform picture, string name, float height)
        {
            var columnGO = new GameObject(name, typeof(RectTransform));
            var column = (RectTransform)columnGO.transform;
            column.SetParent(picture, worldPositionStays: false);
            column.sizeDelta = new Vector2(LaneWidth, height);
            PlaceInPicture(column, HowToPlayPictureInset, LaneStackTop(height));

            return column;
        }

        RectTransform CreateLane(RectTransform parent, HowToPlayPages.DrawnLane lane)
        {
            var laneGO = new GameObject("Lane", typeof(RectTransform));
            var laneRect = (RectTransform)laneGO.transform;
            laneRect.SetParent(parent, worldPositionStays: false);
            laneRect.anchorMin = new Vector2(0f, 1f);
            laneRect.anchorMax = new Vector2(0f, 1f);
            laneRect.pivot = new Vector2(0f, 1f);
            laneRect.sizeDelta = new Vector2(LaneWidth, HowToPlayLogHeight);

            var startLog = CreateLog(laneRect, StartLogLabel, atStart: true);
            CreateLog(laneRect, EndLogLabel, atStart: false);

            for (var position = 1; position < Lane.LaneWinningPosition; position++)
            {
                CreateLilyPad(laneRect, position);
            }

            if (!lane.HasFrog)
            {
                return laneRect;
            }

            if (lane.Position <= 0)
            {
                CreateFrogOnLog(startLog, lane.Frog);
            }
            else if (lane.Position >= Lane.LaneWinningPosition)
            {
                CreateFrogOnLog(EndLogOf(laneRect), lane.Frog);
            }
            else
            {
                CreateFrogOnPad(PadOf(laneRect, lane.Position), lane.Frog);
            }

            return laneRect;
        }

        RectTransform CreateLog(RectTransform lane, string label, bool atStart)
        {
            var logGO = new GameObject("Log", typeof(RectTransform), typeof(Image));
            var log = logGO.GetComponent<Image>();
            log.sprite = RoundedRect(HowToPlayLogRadius);
            log.type = Image.Type.Sliced;
            log.color = BoardColours.LogBrown;
            log.raycastTarget = false;

            var rect = log.rectTransform;
            rect.SetParent(lane, worldPositionStays: false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(HowToPlayLogWidth, HowToPlayLogHeight);
            rect.anchoredPosition = new Vector2(
                PositionCentreX(atStart ? 0 : Lane.LaneWinningPosition), 0f);

            var word = CreateText(
                "Label", rect, HowToPlayLogLabelSize, FontStyle.Normal, BoardColours.LogLabelInk, TextAnchor.UpperCenter);

            word.text = label;

            var wordRect = word.rectTransform;
            wordRect.anchorMin = new Vector2(0f, 1f);
            wordRect.anchorMax = new Vector2(1f, 1f);
            wordRect.pivot = new Vector2(0.5f, 1f);
            wordRect.sizeDelta = new Vector2(0f, HowToPlayLogLabelSize);
            wordRect.anchoredPosition = new Vector2(0f, -HowToPlayLogLabelTopPadding);

            return rect;
        }

        void CreateLilyPad(RectTransform lane, int position)
        {
            var padGO = new GameObject("LilyPad", typeof(RectTransform));
            var padRect = (RectTransform)padGO.transform;
            padRect.SetParent(lane, worldPositionStays: false);
            padRect.anchorMin = new Vector2(0f, 0.5f);
            padRect.anchorMax = new Vector2(0f, 0.5f);
            padRect.pivot = new Vector2(0.5f, 0.5f);
            padRect.sizeDelta = new Vector2(HowToPlayPadDiameter, HowToPlayPadDiameter);
            padRect.anchoredPosition = new Vector2(PositionCentreX(position), 0f);

            // The board's own pad, at this page's diameter: the same notched,
            // veined shape from the same variation table, so a pad in a
            // picture is a pad. Which row it draws is the table's row for the
            // *first* lane at that position — every picture lane draws the
            // same seven, as all five mockups do. The board's `x 5` stagger,
            // which stops four lanes lining up into columns, is a rule about a
            // board with four lanes of one game; page 4's three lanes are
            // three separate examples.
            var notch = GameBoardLaneView.LilyPadNotchWidthFor(0, position);

            var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
            var art = artGO.GetComponent<Image>();
            art.sprite = LilyPad(notch);
            art.type = Image.Type.Simple;

            // Untinted: the pad's three colours are in the sprite, exactly as
            // they are on the board.
            art.color = Color.white;
            art.raycastTarget = false;

            var artRect = art.rectTransform;
            artRect.SetParent(padRect, worldPositionStays: false);
            StretchToFill(artRect);

            // game-board.md measures its angles the way the mockups' SVG does
            // — 0 right along the lane, 90 down — and a uGUI z-rotation turns
            // the other way about.
            artRect.localRotation = Quaternion.Euler(0f, 0f, -GameBoardLaneView.LilyPadNotchAngleFor(0, position));
        }

        void CreateFrogOnLog(RectTransform log, FrogColour colour)
        {
            var frog = CreateFrog(log, colour);
            frog.anchorMin = new Vector2(0.5f, 1f);
            frog.anchorMax = new Vector2(0.5f, 1f);
            frog.pivot = new Vector2(0.5f, 1f);

            // Under the log's own word, rather than over it: the label is at
            // the top of the log because the middle of a log is where the
            // frogs stand.
            frog.anchoredPosition = new Vector2(
                0f, -(HowToPlayLogLabelTopPadding + HowToPlayLogLabelSize + HowToPlayLogLabelGap));
        }

        void CreateFrogOnPad(RectTransform pad, FrogColour colour)
        {
            var frog = CreateFrog(pad, colour);
            frog.anchorMin = new Vector2(0.5f, 0.5f);
            frog.anchorMax = new Vector2(0.5f, 0.5f);
            frog.pivot = new Vector2(0.5f, 0.5f);
            frog.anchoredPosition = Vector2.zero;
        }

        RectTransform CreateFrog(RectTransform parent, FrogColour colour)
        {
            // A flat-coloured circle inside its own outline — the board's own
            // piece, at this page's diameter.
            var frogGO = new GameObject("Frog", typeof(RectTransform), typeof(Image));
            var outline = frogGO.GetComponent<Image>();
            outline.sprite = FrogSprite;
            outline.type = Image.Type.Sliced;
            outline.color = BoardColours.PieceEdge;
            outline.raycastTarget = false;

            var rect = outline.rectTransform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.sizeDelta = new Vector2(HowToPlayFrogDiameter, HowToPlayFrogDiameter);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = FrogFillSprite;
            fill.type = Image.Type.Sliced;
            fill.color = FrogColours.For(colour);
            fill.raycastTarget = false;

            var fillRect = fill.rectTransform;
            fillRect.SetParent(rect, worldPositionStays: false);
            StretchToFill(fillRect);
            fillRect.offsetMin = new Vector2(HowToPlayFrogOutline, HowToPlayFrogOutline);
            fillRect.offsetMax = new Vector2(-HowToPlayFrogOutline, -HowToPlayFrogOutline);

            return rect;
        }

        static RectTransform EndLogOf(RectTransform lane)
        {
            RectTransform last = null;

            for (var child = 0; child < lane.childCount; child++)
            {
                var rect = lane.GetChild(child) as RectTransform;

                if (rect != null && rect.gameObject.name == "Log")
                {
                    last = rect;
                }
            }

            return last;
        }

        static RectTransform PadOf(RectTransform lane, int position)
        {
            var pad = 0;

            for (var child = 0; child < lane.childCount; child++)
            {
                var rect = lane.GetChild(child) as RectTransform;

                if (rect == null || rect.gameObject.name != "LilyPad")
                {
                    continue;
                }

                pad++;

                if (pad == position)
                {
                    return rect;
                }
            }

            return null;
        }

        /// <summary>
        /// The centre of one lane position, measured from the lane's left
        /// edge: the Start log's column at 0, seven lily pads, the End log's
        /// column at <see cref="Lane.LaneWinningPosition"/>. The board's own
        /// arithmetic at this page's sizes.
        /// </summary>
        static float PositionCentreX(int position)
        {
            if (position <= 0)
            {
                return HowToPlayLogWidth / 2f;
            }

            if (position >= Lane.LaneWinningPosition)
            {
                return LaneWidth - (HowToPlayLogWidth / 2f);
            }

            return HowToPlayLogWidth
                + HowToPlayLanePositionGap
                + ((position - 1) * (HowToPlayPadDiameter + HowToPlayLanePositionGap))
                + (HowToPlayPadDiameter / 2f);
        }

        /// <summary>The width a drawn lane runs across: the picture inside its two insets.</summary>
        static float LaneWidth
        {
            get { return HowToPlayPictureWidth - (2f * HowToPlayPictureInset); }
        }

        // --- Page 2: the die, the arrow and the three piles ------------------------

        void BuildRollThePicture(RectTransform picture)
        {
            var die = CreateFramedBox(
                picture, "Die", HowToPlayDieSize, HowToPlayDieSize, HowToPlayDieRadius, HowToPlayDieOutline, LineColor);

            PlaceInPicture(die, HowToPlayDieLeft, HowToPlayDieTop);

            BuildDiePips(die);

            // The arrow and the pile stack are both centred on the die's own
            // centre line, rather than each carrying a top of its own.
            var dieCentre = HowToPlayDieTop + (HowToPlayDieSize / 2f);

            var arrow = CreateText(
                "Arrow", picture, HowToPlayArrowSize, FontStyle.Normal, LineColor, TextAnchor.UpperLeft);

            arrow.text = HowToPlayPages.ArrowGlyph;
            arrow.rectTransform.sizeDelta = new Vector2(HowToPlayArrowSize, HowToPlayArrowSize);
            PlaceInPicture(arrow.rectTransform, HowToPlayArrowLeft, dieCentre - (HowToPlayArrowSize / 2f));

            var piles = HowToPlayPages.Piles;
            var stackHeight = (piles.Count * HowToPlayPileHeight) + ((piles.Count - 1) * HowToPlayPileGap);

            var pilesGO = new GameObject("Piles", typeof(RectTransform));
            var pilesRect = (RectTransform)pilesGO.transform;
            pilesRect.SetParent(picture, worldPositionStays: false);
            pilesRect.sizeDelta = new Vector2(HowToPlayPileWidth, stackHeight);
            PlaceInPicture(pilesRect, HowToPlayPileLeft, dieCentre - (stackHeight / 2f));

            for (var pile = 0; pile < piles.Count; pile++)
            {
                var box = CreateFramedBox(
                    pilesRect, "Pile", HowToPlayPileWidth, HowToPlayPileHeight,
                    HowToPlayPileRadius, HowToPlayPileOutline, LineColor);

                box.anchorMin = new Vector2(0f, 1f);
                box.anchorMax = new Vector2(0f, 1f);
                box.pivot = new Vector2(0f, 1f);
                box.anchoredPosition = new Vector2(0f, -pile * (HowToPlayPileHeight + HowToPlayPileGap));

                var label = CreateText(
                    "Label", box, HowToPlayPileLabelSize, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);

                label.text = piles[pile].Text;
                StretchToFill(label.rectTransform);

                // The two piles the roll did not pick are dimmed rather than
                // hidden — the picture says the roll chose one of three.
                if (piles[pile].IsPicked)
                {
                    continue;
                }

                var group = box.gameObject.AddComponent<CanvasGroup>();
                group.alpha = HowToPlayPileDimOpacity;
            }

            BuildRollTable(picture);
        }

        void BuildDiePips(RectTransform die)
        {
            // A die showing 3: the diagonal, in a three-by-three grid inside
            // the die's own padding.
            var cell = (HowToPlayDieSize - (2f * HowToPlayDiePadding)) / HowToPlayPages.DieGridSize;

            for (var slot = 0; slot < HowToPlayPages.DieGridSize; slot++)
            {
                var pipGO = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                var pip = pipGO.GetComponent<Image>();
                pip.sprite = RoundedRect(HowToPlayDiePipSize / 2f);
                pip.type = Image.Type.Sliced;
                pip.color = InkColor;
                pip.raycastTarget = false;

                var rect = pip.rectTransform;
                rect.SetParent(die, worldPositionStays: false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(HowToPlayDiePipSize, HowToPlayDiePipSize);
                rect.anchoredPosition = new Vector2(
                    HowToPlayDiePadding + (cell * (slot + 0.5f)),
                    -(HowToPlayDiePadding + (cell * (slot + 0.5f))));
            }
        }

        void BuildRollTable(RectTransform picture)
        {
            var tableGO = new GameObject("RollTable", typeof(RectTransform));
            var table = (RectTransform)tableGO.transform;
            table.SetParent(picture, worldPositionStays: false);

            var rows = HowToPlayPages.RollTable;
            var lineBox = HowToPlayNoteSize * HowToPlayNoteLineHeight;
            var height = (rows.Count * lineBox) + HowToPlayRollTableHeaderGap;

            table.sizeDelta = new Vector2(
                (2f * HowToPlayRollTableColumnWidth) + HowToPlayRollTableColumnGap, height);

            PlaceInPicture(table, HowToPlayRollTableLeft, HowToPlayRollTableTop);

            var cursor = 0f;

            for (var row = 0; row < rows.Count; row++)
            {
                var isHeader = row == 0;
                var weight = isHeader ? FontStyle.Bold : FontStyle.Normal;

                CreateTableCell(table, rows[row].Roll, weight, 0f, cursor, lineBox);
                CreateTableCell(
                    table, rows[row].Card, weight,
                    HowToPlayRollTableColumnWidth + HowToPlayRollTableColumnGap, cursor, lineBox);

                cursor += lineBox + (isHeader ? HowToPlayRollTableHeaderGap : 0f);
            }
        }

        void CreateTableCell(RectTransform table, string label, FontStyle weight, float left, float top, float lineBox)
        {
            var text = CreateText("TableCell", table, HowToPlayNoteSize, weight, InkColor, TextAnchor.UpperLeft);
            text.text = label;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(HowToPlayRollTableColumnWidth, lineBox);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        // --- Page 3: the grid, and what its rows are for ---------------------------

        void BuildWorkItOutPicture(RectTransform picture)
        {
            var rows = HowToPlayPages.GridRows;
            var columns = HowToPlayPages.GridColumns;

            var gridGO = new GameObject("Grid", typeof(RectTransform));
            var grid = (RectTransform)gridGO.transform;
            grid.SetParent(picture, worldPositionStays: false);

            var width = (columns * HowToPlayCellSize) + ((columns - 1) * HowToPlayCellGap);
            var height = HowToPlayCarryRowHeight
                + (rows.Count * (HowToPlayCellSize + HowToPlayCellGap));

            grid.sizeDelta = new Vector2(width, height);
            PlaceInPicture(grid, HowToPlayGridLeft, HowToPlayGridTop);

            // The carry row: a dashed hint box per column, and no cell —
            // "carry boxes along the top" are a place to put a digit, not a
            // row of the sum.
            for (var column = 0; column < columns; column++)
            {
                var box = CreateFramedBox(
                    grid, "CarryBox", HowToPlayCarryBoxWidth, HowToPlayCarryBoxHeight,
                    HowToPlayCarryBoxRadius, HowToPlayCellOutline, FaintColor);

                box.anchorMin = new Vector2(0f, 1f);
                box.anchorMax = new Vector2(0f, 1f);
                box.pivot = new Vector2(0.5f, 0.5f);
                box.anchoredPosition = new Vector2(
                    (column * (HowToPlayCellSize + HowToPlayCellGap)) + (HowToPlayCellSize / 2f),
                    -(HowToPlayCarryRowHeight / 2f));
            }

            for (var row = 0; row < rows.Count; row++)
            {
                var top = HowToPlayCarryRowHeight + (row * (HowToPlayCellSize + HowToPlayCellGap));

                for (var column = 0; column < columns; column++)
                {
                    CreateGridCell(grid, rows[row], column, top);
                }
            }

            BuildCallouts(picture);
        }

        void CreateGridCell(RectTransform grid, HowToPlayPages.GridRow row, int column, float top)
        {
            var kind = row.KindAt(column);
            var digit = row.DigitAt(column);

            RectTransform cell;

            if (kind == HowToPlayPages.CellKind.Printed)
            {
                // What the card itself says — printed on the paper rather than
                // written into a box, so it carries no border at all.
                var printedGO = new GameObject("PrintedCell", typeof(RectTransform));
                cell = (RectTransform)printedGO.transform;
                cell.SetParent(grid, worldPositionStays: false);
                cell.sizeDelta = new Vector2(HowToPlayCellSize, HowToPlayCellSize);
            }
            else
            {
                var isAnswer = kind == HowToPlayPages.CellKind.Answer;

                cell = CreateFramedBox(
                    grid,
                    isAnswer ? "AnswerCell" : "Cell",
                    HowToPlayCellSize,
                    HowToPlayCellSize,
                    HowToPlayCellRadius,
                    isAnswer ? HowToPlayAnswerOutline : HowToPlayCellOutline,
                    isAnswer ? AccentColor : LineColor);
            }

            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0.5f, 0.5f);
            cell.anchoredPosition = new Vector2(
                (column * (HowToPlayCellSize + HowToPlayCellGap)) + (HowToPlayCellSize / 2f),
                -(top + (HowToPlayCellSize / 2f)));

            if (string.IsNullOrEmpty(digit))
            {
                return;
            }

            var text = CreateText(
                "Digit",
                cell,
                HowToPlayCellDigitSize,
                kind == HowToPlayPages.CellKind.Printed ? FontStyle.Bold : FontStyle.Normal,
                InkColor,
                TextAnchor.MiddleCenter);

            text.text = digit;
            StretchToFill(text.rectTransform);
        }

        void BuildCallouts(RectTransform picture)
        {
            var calloutsGO = new GameObject("Callouts", typeof(RectTransform));
            var callouts = (RectTransform)calloutsGO.transform;
            callouts.SetParent(picture, worldPositionStays: false);

            var blocks = HowToPlayPages.Callouts;
            var lineBox = HowToPlayNoteSize * HowToPlayCalloutLineHeight;
            var blockHeight = 2f * lineBox;

            callouts.sizeDelta = new Vector2(
                HowToPlayCalloutWidth,
                (blocks.Count * blockHeight) + ((blocks.Count - 1) * HowToPlayCalloutGap));

            PlaceInPicture(callouts, HowToPlayCalloutLeft, HowToPlayCalloutTop);

            for (var block = 0; block < blocks.Count; block++)
            {
                var top = block * (blockHeight + HowToPlayCalloutGap);

                CreateCalloutLine(
                    callouts, blocks[block].Heading, FontStyle.Bold,
                    blocks[block].IsAnswerRow ? AccentColor : InkColor, top, lineBox);

                CreateCalloutLine(callouts, blocks[block].Detail, FontStyle.Normal, LineColor, top + lineBox, lineBox);
            }
        }

        void CreateCalloutLine(RectTransform callouts, string label, FontStyle weight, Color colour, float top, float lineBox)
        {
            var text = CreateText("Callout", callouts, HowToPlayNoteSize, weight, colour, TextAnchor.UpperLeft);
            text.text = label;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, lineBox);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        // --- Shared drawing -------------------------------------------------------

        // A rounded box drawn as a rim with its own fill laid inside it — the
        // same two images the board's frog piece is drawn with, and what every
        // `border: Npx solid` rule in the mockups means. The fill is the
        // paper, because every framed box on these two pages sits on it.
        RectTransform CreateFramedBox(
            RectTransform parent, string name, float width, float height, float radius, float outline, Color rim)
        {
            var boxGO = new GameObject(name, typeof(RectTransform), typeof(Image));
            var border = boxGO.GetComponent<Image>();
            border.sprite = RoundedRect(radius);
            border.type = Image.Type.Sliced;
            border.color = rim;
            border.raycastTarget = false;

            var rect = border.rectTransform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.sizeDelta = new Vector2(width, height);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = RoundedRect(radius - outline);
            fill.type = Image.Type.Sliced;
            fill.color = PaperColor;
            fill.raycastTarget = false;

            var fillRect = fill.rectTransform;
            fillRect.SetParent(rect, worldPositionStays: false);
            StretchToFill(fillRect);
            fillRect.offsetMin = new Vector2(outline, outline);
            fillRect.offsetMax = new Vector2(-outline, -outline);

            return rect;
        }

        // Everything inside a picture is placed from its top-left inside
        // corner, which is what the mockups measure from and what
        // how-to-play.md#anchors says the lane drawings are anchored to.
        static void PlaceInPicture(RectTransform rect, float left, float top)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        Text CreateText(
            string name, RectTransform parent, float size, FontStyle style, Color colour, TextAnchor alignment)
        {
            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            text.fontSize = (int)size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = colour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // Nothing on this screen responds to a touch except its two
            // buttons — not the dots, not the pictures, not a word of the
            // copy.
            text.raycastTarget = false;

            text.rectTransform.SetParent(parent, worldPositionStays: false);

            return text;
        }

        static Sprite DotSprite
        {
            get
            {
                if (s_dotSprite == null)
                {
                    s_dotSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(HowToPlayDotSize / 2f));
                }

                return s_dotSprite;
            }
        }

        static Sprite FrogSprite
        {
            get
            {
                if (s_frogSprite == null)
                {
                    s_frogSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(HowToPlayFrogDiameter / 2f));
                }

                return s_frogSprite;
            }
        }

        static Sprite FrogFillSprite
        {
            get
            {
                if (s_frogFillSprite == null)
                {
                    s_frogFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt((HowToPlayFrogDiameter - (2f * HowToPlayFrogOutline)) / 2f));
                }

                return s_frogFillSprite;
            }
        }

        // One sprite per corner radius this screen asks for, built the first
        // time it is asked for and shared by every box that wants it.
        static Sprite RoundedRect(float radius)
        {
            var rounded = Mathf.Max(Mathf.RoundToInt(radius), 1);

            if (!s_roundedRects.ContainsKey(rounded))
            {
                s_roundedRects[rounded] = RoundedRectSprite.CreateRoundedRect(rounded);
            }

            return s_roundedRects[rounded];
        }

        // The board's own lily pad at this page's diameter — one sprite per
        // notch width, as the board does it.
        static Sprite LilyPad(float notchWidth)
        {
            if (!s_lilyPadSprites.ContainsKey(notchWidth))
            {
                s_lilyPadSprites[notchWidth] = LilyPadSprite.Create(
                    Mathf.RoundToInt(HowToPlayPadDiameter),
                    GameBoardLaneView.TrackOutline,
                    notchWidth,
                    GameBoardLaneView.LilyPadNotchDepth,
                    GameBoardLaneView.LilyPadVeinAnglesFor(notchWidth),
                    GameBoardLaneView.LilyPadVeinInset,
                    GameBoardLaneView.LilyPadVeinOutset,
                    GameBoardLaneView.LilyPadVeinWidth,
                    GameBoardLaneView.LilyPadVeinOpacity,
                    BoardColours.LilyPadGreen,
                    BoardColours.LilyPadEdge);
            }

            return s_lilyPadSprites[notchWidth];
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
