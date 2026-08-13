using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// GameBoardScreenView.cs and TitleScreenView.cs work around — so the shared
// components are pulled in by explicit alias, and a bare `Button`,
// `ButtonKind`, `DialogPanel`, `FrogColours` or `PlayerChip` in this file
// always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The die that landed, the pile it sends you to, and the card you drew —
    /// docs/specs/ui/roll-and-card.md, built to that page and its committed
    /// 1:1 mockup, on the shared <see cref="DialogPanel"/> (#219).
    ///
    /// **It is a readout, not a source of randomness.** The roll and the draw
    /// both happened in Core (<c>Game.RollDie</c>) before this dialog opened.
    /// This view asks an <see cref="IRollAndCardReadout"/> what face came up,
    /// what pile Core reported, and what the two operands are — and draws
    /// them. It contains no generator, and it does not re-derive the pile
    /// from the face even though that mapping is public on
    /// <see cref="Roll.PileForFace"/>: Core reports the pile, and this
    /// displays what Core reports.
    ///
    /// Three things it deliberately does not do:
    ///
    /// - **It does not handle hardware back.** Back is inert on this dialog,
    ///   and it is inert because the alternative is losing a drawn card. The
    ///   router already knows that (<c>ScreenRouter.HandleBack</c> does
    ///   nothing for <see cref="Frogs.Core.Dialog.RollAndCard"/>), so the way
    ///   to be inert here is to add no handler at all — and to nominate no
    ///   least-destructive button on the shared Dialog, which is what would
    ///   otherwise turn a back press into a `Solve it`.
    /// - **It does not build the working-out grid** (#223) or the answer
    ///   result (#224). `Solve it` opens the next dialog through the router
    ///   and says the press happened; what that dialog contains is not this
    ///   issue's.
    /// - **It does not advance Core's turn phase.** Moving from
    ///   <c>RolledAndCardDrawn</c> to <c>Answering</c> belongs to whoever
    ///   owns the screen that answering happens on, not to the screen being
    ///   left.
    ///
    /// Both of roll-and-card.md's open questions are left exactly as open as
    /// it leaves them: this is its own beat rather than the top of the grid,
    /// and the pile is named here and nowhere else.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RollAndCardDialogView : MonoBehaviour
    {
        // docs/specs/ui/roll-and-card.md#named-constants — the page's own
        // table, under the identical names.
        public const float RollDialogWidth = 1280f;
        public const float RollDialogHeight = 760f;
        public const float RolledLabelSize = 40f;
        public const float RolledLabelGap = 24f;
        public const float DieColumnWidth = 400f;
        public const float DieGroupTop = 220f;
        public const float DieFaceSize = 240f;
        public const float DieCornerRadius = 40f;
        public const float DieBorderWidth = 4f;
        public const float DiePipInset = 34f;
        public const float DiePipDiameter = 40f;
        public const float DiePileGap = 32f;
        public const float PileLabelSize = 40f;
        public const float CardTop = 180f;
        public const float CardWidth = 560f;
        public const float CardHeight = 420f;
        public const float CardRadius = 24f;
        public const float CardBorderWidth = 4f;
        public const float CardProblemSize = 120f;
        public const float CardRuleGap = 8f;
        public const float CardRuleThickness = 8f;
        public const float CardRuleLength = 360f;
        public const float RollCardGap = 208f;
        public const float DieRollDuration = 0.8f;
        public const float CardDealDuration = 0.3f;

        // The dialog's own scrim, corners, padding and cross-fade are the
        // shared Dialog's constants, referenced rather than redeclared —
        // DialogPanel.DialogPadding, DialogRadius, DialogScrimOpacity and
        // DialogFadeDuration. DialogTitleSize and DialogTitleGap are the two
        // this dialog does not use: it has no title text, and `whose` does
        // that job instead.

        // How long the whole entry runs: the die settles, then the card
        // deals. A relationship between two named constants rather than a
        // third number — the spec's own "total about 1.2 s" is this plus the
        // shared Dialog's fade.
        const float EntryDuration = DieRollDuration + CardDealDuration;

        // Three pips across a die face; two lines to a pile label; two lines
        // to a problem. Counts, not measurements — but named all the same,
        // because they are what turn DiePipInset, PileLabelSize and
        // CardProblemSize into positions.
        const int DiePipAcross = 3;
        const int PileLabelLines = 2;
        const int ProblemLines = 2;

        // A pip sits centred in one cell of the three-across lattice inside
        // the die's border and inset.
        const float PipCellSize =
            (DieFaceSize - (2f * DieBorderWidth) - (2f * DiePipInset)) / DiePipAcross;

        const string RolledLabel = "rolled";
        const string SolveItLabel = "Solve it";

        // docs/specs/ui/roll-and-card.md § Elements — the pile label names
        // both the pile and the two faces that reach it, "how the board
        // itself is labelled". The separator is what makes the two halves one
        // label: `Hard pile · 5 or 6`.
        const string PileLabelSeparator = " · ";
        const string EasyPileName = "Easy pile";
        const string MediumPileName = "Medium pile";
        const string HardPileName = "Hard pile";
        const string EasyPileFaces = "1 or 2";
        const string MediumPileFaces = "3 or 4";
        const string HardPileFaces = "5 or 6";

        // `×` to the left of the second number, the way the classroom cards
        // are written.
        const string MultiplierFormat = "× {0}";

        // No imported font — matches Button.cs's, PlayerChip.cs's and
        // GameBoardScreenView.cs's own choice, for the same reason (no
        // external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockup
        // (docs/specs/ui/mockups/roll-and-card.html) — the same line
        // Button.cs, PlayerChip.cs and GameBoardScreenView.cs each draw for
        // their own colours: not a geometry constant on any spec page's
        // table, so not declared as a named spec constant.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockup's --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockup's --line
        static readonly Color PaperColor = Color.white; // mockup's --paper / #fff

        // The pip lattice, in cell units: -1, 0 and 1 across and down, y up.
        // These are the ordinary die-face arrangements — a universal
        // convention for what a die face looks like, not a rule about this
        // game — and the six is the two columns of three the mockup draws for
        // its hard-pile worked example.
        static readonly Vector2[][] PipLayouts =
        {
            new[] { new Vector2(0f, 0f) },
            new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f) },
            new[] { new Vector2(-1f, 1f), new Vector2(0f, 0f), new Vector2(1f, -1f) },
            new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) },
            new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-1f, -1f), new Vector2(1f, -1f) },
            new[]
            {
                new Vector2(-1f, 1f), new Vector2(1f, 1f),
                new Vector2(-1f, 0f), new Vector2(1f, 0f),
                new Vector2(-1f, -1f), new Vector2(1f, -1f)
            }
        };

        static Sprite s_dieSprite;
        static Sprite s_cardSprite;
        static Sprite s_pipSprite;

        static Sprite DieSprite
        {
            get
            {
                if (s_dieSprite == null)
                {
                    s_dieSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(DieCornerRadius));
                }

                return s_dieSprite;
            }
        }

        static Sprite CardSprite
        {
            get
            {
                if (s_cardSprite == null)
                {
                    s_cardSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(CardRadius));
                }

                return s_cardSprite;
            }
        }

        static Sprite PipSprite
        {
            get
            {
                if (s_pipSprite == null)
                {
                    // A rounded rect whose radius is half its own size is a
                    // circle — the same trick PlayerChip's swatch uses.
                    s_pipSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(DiePipDiameter / 2f));
                }

                return s_pipSprite;
            }
        }

        RectTransform _rect;
        DialogPanel _dialog;
        RollAndCardSkipCatcher _scrimSkipCatcher;
        RollAndCardSkipCatcher _panelSkipCatcher;

        RectTransform _whoseRect;
        PlayerChip _whoseChip;
        Text _rolledText;

        RectTransform _dieGroupRect;
        RectTransform _dieRect;
        Image _dieBorder;
        Image _dieFace;
        readonly List<Image> _pips = new List<Image>();
        readonly List<Vector2> _pipLattice = new List<Vector2>();
        readonly List<Image> _visiblePips = new List<Image>();

        RectTransform _pileRect;
        CanvasGroup _pileCanvasGroup;
        Text _pileNameText;
        Text _pileFacesText;

        RectTransform _cardRect;
        CanvasGroup _cardCanvasGroup;
        Image _cardBorder;
        Image _cardFace;
        Text _multiplicandText;
        Text _multiplierText;
        RectTransform _cardRuleRect;

        Button _solveItButton;

        bool _initialized;
        IRollAndCardReadout _readout;
        ScreenRouter _router;
        float _elapsed;

        /// <summary>
        /// `Solve it` was pressed, on release, per the shared Button. The
        /// transition to the working-out grid has already been asked of the
        /// router by the time this fires; this only says the press happened.
        /// </summary>
        public event Action SolveItPressed;

        /// <summary>The view's own <see cref="RectTransform"/> — the full canvas the dialog covers.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this is built on — its scrim, corners, padding and cross-fade.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>A tap on the dimmed board behind the dialog. Skips the entry, and can do nothing else.</summary>
        public RollAndCardSkipCatcher ScrimSkipCatcher
        {
            get
            {
                EnsureInitialized();
                return _scrimSkipCatcher;
            }
        }

        /// <summary>A tap on the panel itself. Skips the entry, and can do nothing else.</summary>
        public RollAndCardSkipCatcher PanelSkipCatcher
        {
            get
            {
                EnsureInitialized();
                return _panelSkipCatcher;
            }
        }

        /// <summary>`whose` — the chip and the word `rolled`, above the row.</summary>
        public RectTransform WhoseRect
        {
            get
            {
                EnsureInitialized();
                return _whoseRect;
            }
        }

        /// <summary>The player chip of the frog taking this turn, active state.</summary>
        public PlayerChip WhoseChip
        {
            get
            {
                EnsureInitialized();
                return _whoseChip;
            }
        }

        /// <summary>The word beside the chip that makes `whose` a sentence.</summary>
        public Text RolledText
        {
            get
            {
                EnsureInitialized();
                return _rolledText;
            }
        }

        /// <summary>`die` and `pile` as one group, <see cref="DieColumnWidth"/> wide.</summary>
        public RectTransform DieGroupRect
        {
            get
            {
                EnsureInitialized();
                return _dieGroupRect;
            }
        }

        /// <summary>`die` — a <see cref="DieFaceSize"/> square with <see cref="DieCornerRadius"/> corners.</summary>
        public RectTransform DieRect
        {
            get
            {
                EnsureInitialized();
                return _dieRect;
            }
        }

        /// <summary>The die's outline, <see cref="DieBorderWidth"/> of it.</summary>
        public Image DieBorder
        {
            get
            {
                EnsureInitialized();
                return _dieBorder;
            }
        }

        /// <summary>The die's face, inside its outline. The pips sit on this.</summary>
        public Image DieFace
        {
            get
            {
                EnsureInitialized();
                return _dieFace;
            }
        }

        /// <summary>Every pip position on the face, whether or not this face uses it.</summary>
        public IReadOnlyList<Image> Pips
        {
            get
            {
                EnsureInitialized();
                return _pips;
            }
        }

        /// <summary>The pips currently drawn — the rolled face's own arrangement, and empty while the die is still rolling.</summary>
        public IReadOnlyList<Image> VisiblePips
        {
            get
            {
                EnsureInitialized();
                return _visiblePips;
            }
        }

        /// <summary>`pile` — the label, and the fade that holds it back until the die settles.</summary>
        public CanvasGroup PileCanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _pileCanvasGroup;
            }
        }

        /// <summary>The pile's name — `Hard pile`.</summary>
        public Text PileNameText
        {
            get
            {
                EnsureInitialized();
                return _pileNameText;
            }
        }

        /// <summary>The two faces that reach that pile — `5 or 6`.</summary>
        public Text PileFacesText
        {
            get
            {
                EnsureInitialized();
                return _pileFacesText;
            }
        }

        /// <summary>The whole label as the spec writes it — `Hard pile · 5 or 6`.</summary>
        public string PileLabel
        {
            get
            {
                EnsureInitialized();
                return _pileNameText.text + PileLabelSeparator + _pileFacesText.text;
            }
        }

        /// <summary>`card` — <see cref="CardWidth"/> by <see cref="CardHeight"/>. A picture of a card; nothing is typed here.</summary>
        public RectTransform CardRect
        {
            get
            {
                EnsureInitialized();
                return _cardRect;
            }
        }

        /// <summary>The card's deal-in fade, over <see cref="CardDealDuration"/>.</summary>
        public CanvasGroup CardCanvasGroup
        {
            get
            {
                EnsureInitialized();
                return _cardCanvasGroup;
            }
        }

        /// <summary>The card's outline, <see cref="CardBorderWidth"/> of it.</summary>
        public Image CardBorder
        {
            get
            {
                EnsureInitialized();
                return _cardBorder;
            }
        }

        /// <summary>The card's face, inside its outline.</summary>
        public Image CardFace
        {
            get
            {
                EnsureInitialized();
                return _cardFace;
            }
        }

        /// <summary>The problem's first line — the `331` in `331 × 41`.</summary>
        public Text MultiplicandText
        {
            get
            {
                EnsureInitialized();
                return _multiplicandText;
            }
        }

        /// <summary>The problem's second line — the `× 41` in `331 × 41`.</summary>
        public Text MultiplierText
        {
            get
            {
                EnsureInitialized();
                return _multiplierText;
            }
        }

        /// <summary>The rule under the problem, <see cref="CardRuleLength"/> by <see cref="CardRuleThickness"/>.</summary>
        public RectTransform CardRuleRect
        {
            get
            {
                EnsureInitialized();
                return _cardRuleRect;
            }
        }

        /// <summary>`Solve it` — the only way out, at the shared Button's own size.</summary>
        public Button SolveItButton
        {
            get
            {
                EnsureInitialized();
                return _solveItButton;
            }
        }

        /// <summary>How far through its entry the dialog is.</summary>
        public RollAndCardEntryPhase Phase
        {
            get
            {
                EnsureInitialized();

                if (_elapsed < DieRollDuration)
                {
                    return RollAndCardEntryPhase.Rolling;
                }

                return _elapsed < EntryDuration
                    ? RollAndCardEntryPhase.Dealing
                    : RollAndCardEntryPhase.Settled;
            }
        }

        /// <summary>True while the die is still rolling and no face is shown.</summary>
        public bool DieIsRolling
        {
            get { return Phase == RollAndCardEntryPhase.Rolling; }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            if (_readout == null)
            {
                return;
            }

            Advance(Time.deltaTime);
        }

        /// <summary>
        /// Points the dialog at the roll and card Core already drew, and at
        /// the router `Solve it` navigates through, then opens it — the die
        /// starts rolling immediately, per the spec's "the dialog fades in
        /// already showing the die rolling".
        ///
        /// The router is optional for the same reason
        /// <c>TitleScreenView.Initialize</c>'s query is: a test that only
        /// cares what the dialog draws should not have to hand it a
        /// navigation graph.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="readout"/> is null.</exception>
        public void Initialize(IRollAndCardReadout readout, ScreenRouter router = null)
        {
            EnsureInitialized();

            _readout = readout ?? throw new ArgumentNullException(nameof(readout));
            _router = router;
            _elapsed = 0f;

            Refresh();
            _dialog.Open();
            ApplyEntry();
        }

        /// <summary>
        /// Re-reads the readout and redraws. Every value it draws is asked
        /// for again — the face, the pile, and the two operands all come back
        /// from Core, and none of them is remembered from the last pass.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_readout == null)
            {
                return;
            }

            var frog = _readout.Frog;
            _whoseChip.SetFrog(FrogColours.For(frog), _readout.FrogName);
            _whoseChip.SetState(PlayerChipState.Active);

            var pile = _readout.Pile;
            _pileNameText.text = PileNameFor(pile);
            _pileFacesText.text = PileFacesFor(pile);

            _multiplicandText.text = _readout.Multiplicand.ToString();
            _multiplierText.text = string.Format(MultiplierFormat, _readout.Multiplier);

            ApplyEntry();
        }

        /// <summary>
        /// Advances the entry by <paramref name="deltaSeconds"/>, clamped to
        /// <see cref="DieRollDuration"/> + <see cref="CardDealDuration"/>. A
        /// public method of its own, rather than reachable only through
        /// <see cref="Update"/>, so an EditMode test can simulate elapsed
        /// time directly — the same reasoning as
        /// <c>TitleScreenView.AdvanceFade</c>.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            EnsureInitialized();

            var delta = Mathf.Max(deltaSeconds, 0f);
            _elapsed = Mathf.Clamp(_elapsed + delta, 0f, EntryDuration);

            _dialog.AdvanceFade(delta);
            ApplyEntry();
        }

        /// <summary>
        /// Jumps straight to the settled state — face shown, pile label
        /// shown, card in place. This is all a tap anywhere outside
        /// `Solve it` does: it hurries the readout along and decides nothing,
        /// so it never opens the working-out grid and never dismisses
        /// anything.
        /// </summary>
        public void SkipEntry()
        {
            EnsureInitialized();

            _elapsed = EntryDuration;
            _dialog.AdvanceFade(DialogPanel.DialogFadeDuration);
            ApplyEntry();
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a test reaches this one right after
        // AddComponent — the same reasoning as Button, DialogPanel and
        // GameBoardScreenView's own EnsureInitialized.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
            ApplyEntry();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            _dialog = GetComponent<DialogPanel>();
            if (_dialog == null)
            {
                _dialog = gameObject.AddComponent<DialogPanel>();
            }

            // Its own named size, comfortably inside the shared Dialog's
            // maxima — not an override of a default footprint, because the
            // maxima were never one.
            _dialog.SetSize(RollDialogWidth, RollDialogHeight);

            _scrimSkipCatcher = AddSkipCatcher(_dialog.Scrim.gameObject);
            _panelSkipCatcher = AddSkipCatcher(_dialog.PanelRect.gameObject);

            BuildWhose();
            BuildDieGroup();
            BuildCard();
            BuildControls();
        }

        RollAndCardSkipCatcher AddSkipCatcher(GameObject target)
        {
            var catcher = target.GetComponent<RollAndCardSkipCatcher>();
            if (catcher == null)
            {
                catcher = target.AddComponent<RollAndCardSkipCatcher>();
            }

            catcher.Tapped += SkipEntry;
            return catcher;
        }

        void BuildWhose()
        {
            var whoseGO = new GameObject("Whose", typeof(RectTransform));
            _whoseRect = (RectTransform)whoseGO.transform;
            _whoseRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _whoseRect.anchorMin = new Vector2(0f, 1f);
            _whoseRect.anchorMax = new Vector2(0f, 1f);
            _whoseRect.pivot = new Vector2(0f, 1f);
            _whoseRect.sizeDelta = new Vector2(DieColumnWidth, PlayerChip.PlayerChipHeight);
            _whoseRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, -DialogPanel.DialogPadding);

            var chipGO = new GameObject("WhoseChip", typeof(RectTransform));
            var chipRect = (RectTransform)chipGO.transform;
            chipRect.SetParent(_whoseRect, worldPositionStays: false);
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.pivot = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = Vector2.zero;
            _whoseChip = chipGO.AddComponent<PlayerChip>();

            // Unity does not run Awake() on AddComponent outside play mode,
            // and the chip sizes itself when it builds. Touch its rect so it
            // builds now, while `whose` is being laid out.
            chipRect = _whoseChip.RectTransform;

            _rolledText = BuildText("Rolled", _whoseRect, RolledLabelSize, LineColor, TextAnchor.MiddleLeft);
            _rolledText.text = RolledLabel;
            var rolledRect = _rolledText.rectTransform;
            rolledRect.anchorMin = new Vector2(0f, 0.5f);
            rolledRect.anchorMax = new Vector2(0f, 0.5f);
            rolledRect.pivot = new Vector2(0f, 0.5f);
            rolledRect.sizeDelta = Vector2.zero;
            rolledRect.anchoredPosition = new Vector2(PlayerChip.PlayerChipWidth + RolledLabelGap, 0f);
        }

        void BuildDieGroup()
        {
            // die and pile as one group: the die on top, the label
            // DiePileGap under it, both centred in a DieColumnWidth column
            // that starts where the chip above it does.
            var groupHeight = DieFaceSize + DiePileGap + (PileLabelSize * PileLabelLines);

            var groupGO = new GameObject("DieGroup", typeof(RectTransform));
            _dieGroupRect = (RectTransform)groupGO.transform;
            _dieGroupRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _dieGroupRect.anchorMin = new Vector2(0f, 1f);
            _dieGroupRect.anchorMax = new Vector2(0f, 1f);
            _dieGroupRect.pivot = new Vector2(0f, 1f);
            _dieGroupRect.sizeDelta = new Vector2(DieColumnWidth, groupHeight);
            _dieGroupRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, -DieGroupTop);

            BuildDie();
            BuildPileLabel();
        }

        void BuildDie()
        {
            var dieGO = new GameObject("Die", typeof(RectTransform), typeof(Image));
            _dieBorder = dieGO.GetComponent<Image>();
            _dieBorder.sprite = DieSprite;
            _dieBorder.type = Image.Type.Sliced;
            _dieBorder.color = LineColor;
            _dieBorder.raycastTarget = false;

            _dieRect = _dieBorder.rectTransform;
            _dieRect.SetParent(_dieGroupRect, worldPositionStays: false);
            _dieRect.anchorMin = new Vector2(0.5f, 1f);
            _dieRect.anchorMax = new Vector2(0.5f, 1f);
            _dieRect.pivot = new Vector2(0.5f, 1f);
            _dieRect.sizeDelta = new Vector2(DieFaceSize, DieFaceSize);
            _dieRect.anchoredPosition = Vector2.zero;

            var faceGO = new GameObject("DieFace", typeof(RectTransform), typeof(Image));
            _dieFace = faceGO.GetComponent<Image>();
            _dieFace.sprite = DieSprite;
            _dieFace.type = Image.Type.Sliced;
            _dieFace.color = PaperColor;
            _dieFace.raycastTarget = false;

            var faceRect = _dieFace.rectTransform;
            faceRect.SetParent(_dieRect, worldPositionStays: false);
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(DieBorderWidth, DieBorderWidth);
            faceRect.offsetMax = new Vector2(-DieBorderWidth, -DieBorderWidth);

            BuildPips(faceRect);
        }

        // One pip per lattice position, built once and shown or hidden per
        // face — no pip is created or destroyed as the face changes.
        void BuildPips(RectTransform faceRect)
        {
            const int extent = (DiePipAcross - 1) / 2;

            for (var row = extent; row >= -extent; row--)
            {
                for (var column = -extent; column <= extent; column++)
                {
                    var pipGO = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                    var pip = pipGO.GetComponent<Image>();
                    pip.sprite = PipSprite;
                    pip.color = InkColor;
                    pip.raycastTarget = false;

                    var pipRect = pip.rectTransform;
                    pipRect.SetParent(faceRect, worldPositionStays: false);
                    pipRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pipRect.anchorMax = new Vector2(0.5f, 0.5f);
                    pipRect.pivot = new Vector2(0.5f, 0.5f);
                    pipRect.sizeDelta = new Vector2(DiePipDiameter, DiePipDiameter);
                    pipRect.anchoredPosition = new Vector2(column * PipCellSize, row * PipCellSize);

                    pipGO.SetActive(false);
                    _pips.Add(pip);
                    _pipLattice.Add(new Vector2(column, row));
                }
            }
        }

        void BuildPileLabel()
        {
            var labelGO = new GameObject("PileLabel", typeof(RectTransform), typeof(CanvasGroup));
            _pileRect = (RectTransform)labelGO.transform;
            _pileRect.SetParent(_dieGroupRect, worldPositionStays: false);
            _pileRect.anchorMin = new Vector2(0.5f, 1f);
            _pileRect.anchorMax = new Vector2(0.5f, 1f);
            _pileRect.pivot = new Vector2(0.5f, 1f);
            _pileRect.sizeDelta = new Vector2(DieColumnWidth, PileLabelSize * PileLabelLines);
            _pileRect.anchoredPosition = new Vector2(0f, -(DieFaceSize + DiePileGap));

            _pileCanvasGroup = labelGO.GetComponent<CanvasGroup>();

            // The pile's name, then the two faces that reach it. Two lines
            // because the mockup draws two; one label because the spec writes
            // one — see PileLabel.
            _pileNameText = BuildText("PileName", _pileRect, PileLabelSize, InkColor, TextAnchor.UpperCenter);
            _pileNameText.fontStyle = FontStyle.Bold;
            StackLine(_pileNameText.rectTransform, 0);

            _pileFacesText = BuildText("PileFaces", _pileRect, PileLabelSize, LineColor, TextAnchor.UpperCenter);
            StackLine(_pileFacesText.rectTransform, 1);
        }

        static void StackLine(RectTransform rect, int line)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, PileLabelSize);
            rect.anchoredPosition = new Vector2(0f, -(line * PileLabelSize));
        }

        void BuildCard()
        {
            var cardGO = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _cardBorder = cardGO.GetComponent<Image>();
            _cardBorder.sprite = CardSprite;
            _cardBorder.type = Image.Type.Sliced;
            _cardBorder.color = LineColor;
            _cardBorder.raycastTarget = false;

            _cardCanvasGroup = cardGO.GetComponent<CanvasGroup>();

            _cardRect = _cardBorder.rectTransform;
            _cardRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _cardRect.anchorMin = new Vector2(1f, 1f);
            _cardRect.anchorMax = new Vector2(1f, 1f);
            _cardRect.pivot = new Vector2(1f, 1f);
            _cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            _cardRect.anchoredPosition = new Vector2(-DialogPanel.DialogPadding, -CardTop);

            var faceGO = new GameObject("CardFace", typeof(RectTransform), typeof(Image));
            _cardFace = faceGO.GetComponent<Image>();
            _cardFace.sprite = CardSprite;
            _cardFace.type = Image.Type.Sliced;
            _cardFace.color = PaperColor;
            _cardFace.raycastTarget = false;

            var faceRect = _cardFace.rectTransform;
            faceRect.SetParent(_cardRect, worldPositionStays: false);
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(CardBorderWidth, CardBorderWidth);
            faceRect.offsetMax = new Vector2(-CardBorderWidth, -CardBorderWidth);

            BuildProblem(faceRect);
        }

        // The problem, written the way the classroom cards are written: the
        // two numbers stacked and right-aligned, × to the left of the second,
        // a rule underneath. The block is centred on the card.
        void BuildProblem(RectTransform faceRect)
        {
            var blockHeight = (CardProblemSize * ProblemLines) + CardRuleGap + CardRuleThickness;

            var blockGO = new GameObject("Problem", typeof(RectTransform));
            var blockRect = (RectTransform)blockGO.transform;
            blockRect.SetParent(faceRect, worldPositionStays: false);
            blockRect.anchorMin = new Vector2(0.5f, 0.5f);
            blockRect.anchorMax = new Vector2(0.5f, 0.5f);
            blockRect.pivot = new Vector2(0.5f, 0.5f);
            blockRect.sizeDelta = new Vector2(CardRuleLength, blockHeight);
            blockRect.anchoredPosition = Vector2.zero;

            _multiplicandText = BuildText("Multiplicand", blockRect, CardProblemSize, InkColor, TextAnchor.MiddleRight);
            _multiplicandText.fontStyle = FontStyle.Bold;
            ProblemLine(_multiplicandText.rectTransform, 0);

            _multiplierText = BuildText("Multiplier", blockRect, CardProblemSize, InkColor, TextAnchor.MiddleRight);
            _multiplierText.fontStyle = FontStyle.Bold;
            ProblemLine(_multiplierText.rectTransform, 1);

            var ruleGO = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            var rule = ruleGO.GetComponent<Image>();
            rule.color = InkColor;
            rule.raycastTarget = false;

            _cardRuleRect = rule.rectTransform;
            _cardRuleRect.SetParent(blockRect, worldPositionStays: false);
            _cardRuleRect.anchorMin = new Vector2(1f, 0f);
            _cardRuleRect.anchorMax = new Vector2(1f, 0f);
            _cardRuleRect.pivot = new Vector2(1f, 0f);
            _cardRuleRect.sizeDelta = new Vector2(CardRuleLength, CardRuleThickness);
            _cardRuleRect.anchoredPosition = Vector2.zero;
        }

        static void ProblemLine(RectTransform rect, int line)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, CardProblemSize);
            rect.anchoredPosition = new Vector2(0f, -(line * CardProblemSize));
        }

        void BuildControls()
        {
            // A plain primary Button at the shared component's own size,
            // added through the shared Dialog so it lands in the button row,
            // primary-on-the-right. It is deliberately *not* nominated as
            // this dialog's least destructive button: that is the value the
            // router would invoke on hardware back, and back is inert here.
            _solveItButton = _dialog.AddButton(ButtonKind.Primary, SolveItLabel, HandleSolveItClicked);
        }

        void HandleSolveItClicked()
        {
            // The only way out. What the working-out grid contains is #223's;
            // this asks the router to open it and says the press happened.
            if (_router != null)
            {
                _router.OpenDialog(Frogs.Core.Dialog.WorkingOutGrid);
            }

            var handler = SolveItPressed;
            if (handler != null)
            {
                handler();
            }
        }

        // The entry, drawn at whatever moment it has reached: the die's face
        // appears when it stops rolling, the pile label with it, and the card
        // fades in over CardDealDuration after that.
        void ApplyEntry()
        {
            var rolling = _elapsed < DieRollDuration;

            _visiblePips.Clear();

            var layout = LayoutForFace();

            for (var index = 0; index < _pips.Count; index++)
            {
                var pip = _pips[index];
                var used = !rolling && IsIn(layout, index);

                pip.gameObject.SetActive(used);

                if (used)
                {
                    _visiblePips.Add(pip);
                }
            }

            _pileCanvasGroup.alpha = rolling ? 0f : 1f;
            _cardCanvasGroup.alpha = Mathf.Clamp01((_elapsed - DieRollDuration) / CardDealDuration);
        }

        // The lattice positions this face uses, or none at all when there is
        // nothing to draw yet — a dialog built but not yet pointed at a roll.
        Vector2[] LayoutForFace()
        {
            if (_readout == null)
            {
                return null;
            }

            var face = _readout.Face;

            if (face < Roll.MinimumFace || face > Roll.MaximumFace)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(face),
                    face,
                    $"a die face is {Roll.MinimumFace} to {Roll.MaximumFace}; {face} is not a face.");
            }

            return PipLayouts[face - Roll.MinimumFace];
        }

        bool IsIn(Vector2[] layout, int pipIndex)
        {
            if (layout == null)
            {
                return false;
            }

            var lattice = _pipLattice[pipIndex];

            foreach (var offset in layout)
            {
                if (offset == lattice)
                {
                    return true;
                }
            }

            return false;
        }

        static string PileNameFor(Pile pile)
        {
            switch (pile)
            {
                case Pile.Easy:
                    return EasyPileName;
                case Pile.Medium:
                    return MediumPileName;
                case Pile.Hard:
                    return HardPileName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pile), pile, "unhandled pile.");
            }
        }

        static string PileFacesFor(Pile pile)
        {
            switch (pile)
            {
                case Pile.Easy:
                    return EasyPileFaces;
                case Pile.Medium:
                    return MediumPileFaces;
                case Pile.Hard:
                    return HardPileFaces;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pile), pile, "unhandled pile.");
            }
        }

        static Text BuildText(string name, RectTransform parent, float size, Color colour, TextAnchor alignment)
        {
            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            text.fontSize = (int)size;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.SetParent(parent, worldPositionStays: false);
            return text;
        }
    }
}
