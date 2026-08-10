using System;
using System.Collections.Generic;
using System.Text;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// RollAndCardDialogView.cs and GameBoardScreenView.cs work around — so the
// shared components are pulled in by explicit alias, and a bare `Button`,
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
    /// The screen the multiplication actually gets done on —
    /// docs/specs/ui/working-out-grid.md, built to that page and its three
    /// committed 1:1 mockups, on the shared <see cref="DialogPanel"/> (#219).
    ///
    /// **It decides no part of the grid's shape.** Which columns exist, which
    /// rows exist and which cells in each are printed rather than fillable all
    /// come from <see cref="WorkingOutGrid.For"/> (#204), asked afresh for the
    /// card and the addition-row count as they are right now — ADR-0002:
    /// "which cells exist for a given problem is game logic and belongs in
    /// Core; only drawing them belongs in the Unity shell." There is no loop
    /// here that re-derives a row count from the multiplier's digits, and no
    /// column arithmetic of any kind.
    ///
    /// Two pieces of *drawing* it does derive, both from the row kinds Core
    /// already reports and from nothing else:
    ///
    /// - **The rule lines.** A rule is a drawn separator with no cells, so
    ///   Core never reports one. One goes under the multiplier row and one
    ///   under the bottom of the addition section, which are the two
    ///   adjacencies the reported row kinds fix.
    /// - **The operator glyphs.** Core reports that column zero exists and is
    ///   never fillable, not what sits in it: `×` on the multiplier row, `+`
    ///   on the bottom row of the addition section, `=` on the answer row,
    ///   nothing anywhere else.
    ///
    /// **Nothing on this screen is graded.** No cell is marked, coloured or
    /// checked against a correct value while it is being filled in, and this
    /// type has no notion of a correct value to check against. The only thing
    /// that ever leaves is the answer row's digits, handed to
    /// <see cref="IWorkingOutTurn.SubmitAnswer"/> when `Check it` is pressed;
    /// grading them is #210's and showing the verdict is #224's.
    ///
    /// **Hardware back is inert here**, and it is inert the same way it is on
    /// the roll-and-card dialog: by this view adding no handler at all and
    /// nominating no least-destructive button on the shared Dialog. The router
    /// (#213) already knows back does nothing over
    /// <see cref="Frogs.Core.Dialog.WorkingOutGrid"/>; a second handler here
    /// could only disagree with it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkingOutGridView : MonoBehaviour
    {
        // docs/specs/ui/working-out-grid.md#named-constants — the page's own
        // table, under the identical names. GridAdditionRowsAtStart and
        // GridAdditionRowsMax are on that table too and are deliberately not
        // redeclared here: they are counts Core owns
        // (WorkingOutGrid.GridAdditionRowsAtStart / GridAdditionRowsMax), and
        // a second copy in the shell is the exact drift the page warns about.
        public const float GridCellSize = 104f;
        public const float GridCellGap = 8f;
        public const float GridCellBorderWidth = 3f;
        public const float GridCellRadius = 10f;
        public const float GridCarryRowHeight = 56f;
        public const float GridCarryBoxBorderWidth = 3f;
        public const float GridCarryBoxRadius = 8f;
        public const float GridRuleThickness = 6f;
        public const float GridAdditionRowHeight = 56f;
        public const float GridAnswerRowHeight = 128f;
        public const float GridAnswerBorderWidth = 6f;
        public const float GridDigitSize = 56f;
        public const float GridSmallDigitSize = 28f;
        public const float GridEqualsSize = 28f;
        public const float GridHeaderHeight = 140f;
        public const float GridHeaderTop = 44f;
        public const float GridHeaderGap = 32f;
        public const float GridPromptSize = 44f;
        public const float GridCardReadoutHeight = 96f;
        public const float GridCardReadoutPaddingX = 32f;
        public const float GridCardReadoutRadius = 20f;
        public const float GridCardReadoutBorderWidth = 3f;
        public const float GridCardReadoutLabelSize = 40f;
        public const float KeypadTop = 216f;
        public const float KeypadKeySize = 140f;
        public const float KeypadKeyGap = 16f;
        public const float KeypadKeyRadius = 20f;
        public const float KeypadKeyBorderWidth = 3f;
        public const float KeypadKeyLabelSize = 56f;
        public const float KeypadBackspaceLabelSize = 40f;
        public const float KeypadClearLabelSize = 32f;
        public const float KeypadWidth = 452f;
        public const float KeypadSubmitGap = 24f;
        public const float CheckButtonHeight = 128f;

        /// <summary>
        /// The carry box inside a carry strip's slot —
        /// docs/specs/ui/working-out-grid.md's table writes it as one row,
        /// `56 × 52 px`, so it is one value here too rather than two
        /// half-named ones.
        /// </summary>
        public static readonly Vector2 GridCarryBoxSize = new Vector2(56f, 52f);

        // The dialog's own scrim, corners, padding, radius and cross-fade are
        // the shared Dialog's constants, referenced rather than redeclared —
        // DialogPanel.DialogPadding, DialogMaxWidth, DialogMaxHeight,
        // DialogRadius, DialogScrimOpacity and DialogFadeDuration. This dialog
        // is full-bleed: the panel *is* DialogMaxWidth by DialogMaxHeight,
        // which is the canvas inset by SafeMargin on every side.

        // The keypad's shape: three keys across, four rows down —
        // `1`–`9`, then backspace, `0`, `clear`. Counts, not measurements.
        const int KeypadColumns = 3;
        const int KeypadRows = 4;

        // Column zero is the operator column, in every row Core reports.
        const int OperatorColumn = 0;

        const string PromptLabel = "Work it out";
        const string CheckItLabel = "Check it";
        const string BackspaceLabel = "⌫";
        const string ClearLabel = "clear";

        const string MultiplyGlyph = "×";
        const string AddGlyph = "+";
        const string EqualsGlyph = "=";
        const string NoGlyph = "";

        // The header's card readout, as the mockups write it: the problem, a
        // separator, and the pile it came from in lower case — `331 × 41 ·
        // hard pile`.
        const string CardReadoutFormat = "{0} × {1} · {2}";
        const string EasyPileName = "easy pile";
        const string MediumPileName = "medium pile";
        const string HardPileName = "hard pile";

        // No imported font — matches Button.cs's, PlayerChip.cs's,
        // DialogPanel.cs's and RollAndCardDialogView.cs's own choice, for the
        // same reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockups
        // (docs/specs/ui/mockups/working-out-grid-*.html) — the same line
        // Button.cs, PlayerChip.cs, DialogPanel.cs and RollAndCardDialogView.cs
        // each draw for their own colours: not a geometry constant on any spec
        // page's table, so not declared as a named spec constant.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color FaintColor = new Color32(0xB9, 0xC0, 0xBD, 0xFF); // mockups' --faint
        static readonly Color AccentColor = new Color32(0x2E, 0x7D, 0x4F, 0xFF); // mockups' --accent
        static readonly Color PaperColor = Color.white; // mockups' --paper / #fff
        static readonly Color AnswerTintColor = new Color32(0xF3, 0xF8, 0xF5, 0xFF); // mockups' answer-row fill

        static Sprite s_cellSprite;
        static Sprite s_carryBoxSprite;
        static Sprite s_keySprite;
        static Sprite s_readoutSprite;

        static Sprite CellSprite
        {
            get
            {
                if (s_cellSprite == null)
                {
                    s_cellSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(GridCellRadius));
                }

                return s_cellSprite;
            }
        }

        static Sprite CarryBoxSprite
        {
            get
            {
                if (s_carryBoxSprite == null)
                {
                    s_carryBoxSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(GridCarryBoxRadius));
                }

                return s_carryBoxSprite;
            }
        }

        static Sprite KeySprite
        {
            get
            {
                if (s_keySprite == null)
                {
                    s_keySprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(KeypadKeyRadius));
                }

                return s_keySprite;
            }
        }

        static Sprite ReadoutSprite
        {
            get
            {
                if (s_readoutSprite == null)
                {
                    s_readoutSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(GridCardReadoutRadius));
                }

                return s_readoutSprite;
            }
        }

        RectTransform _rect;
        DialogPanel _dialog;

        RectTransform _headerRect;
        PlayerChip _whoseChip;
        Text _promptText;
        RectTransform _cardReadoutRect;
        Image _cardReadoutBorder;
        Image _cardReadoutFill;
        Text _cardReadoutText;

        RectTransform _gridRect;
        readonly List<GridRowKind> _rowKinds = new List<GridRowKind>();
        readonly List<RectTransform> _rowRects = new List<RectTransform>();
        readonly List<RectTransform> _ruleRects = new List<RectTransform>();
        readonly List<IReadOnlyList<WorkingOutGridCell>> _cells = new List<IReadOnlyList<WorkingOutGridCell>>();

        RectTransform _keypadRect;
        RectTransform _keyGridRect;
        readonly List<WorkingOutKeypadKey> _keys = new List<WorkingOutKeypadKey>();
        Button _checkItButton;

        // What the player has typed, held by row *identity* rather than by
        // display index: growing the section shifts every row below it down by
        // one, and a digit must not move with it.
        readonly List<RowContents> _carryRows = new List<RowContents>();
        readonly List<RowContents> _additionRows = new List<RowContents>();
        RowContents _answerRow;
        int _columnCount;
        int _nextStamp;

        GridRowKind _caretRowKind = GridRowKind.AnswerRow;
        int _caretRowOrdinal;
        int _caretColumn;

        bool _initialized;
        IWorkingOutTurn _turn;
        ScreenRouter _router;

        /// <summary>
        /// `Check it` was pressed, on release, with the answer the row spells
        /// out. The answer has already been handed to
        /// <see cref="IWorkingOutTurn.SubmitAnswer"/> by the time this fires;
        /// this only says the press happened, and carries no verdict, because
        /// this screen never learns one.
        /// </summary>
        public event Action<int> AnswerSubmitted;

        /// <summary>The view's own <see cref="RectTransform"/> — the full canvas the dialog covers.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this is built on — full-bleed, and with no way to dismiss it.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>`header` — whose turn it is, and the card being worked.</summary>
        public RectTransform HeaderRect
        {
            get
            {
                EnsureInitialized();
                return _headerRect;
            }
        }

        /// <summary>The chip of the frog whose turn this is, active state.</summary>
        public PlayerChip WhoseChip
        {
            get
            {
                EnsureInitialized();
                return _whoseChip;
            }
        }

        /// <summary>`Work it out` — the words beside the chip.</summary>
        public Text PromptText
        {
            get
            {
                EnsureInitialized();
                return _promptText;
            }
        }

        /// <summary>The pill around the card readout.</summary>
        public RectTransform CardReadoutRect
        {
            get
            {
                EnsureInitialized();
                return _cardReadoutRect;
            }
        }

        /// <summary>The readout pill's outline, <see cref="GridCardReadoutBorderWidth"/> of it.</summary>
        public Image CardReadoutBorder
        {
            get
            {
                EnsureInitialized();
                return _cardReadoutBorder;
            }
        }

        /// <summary>The readout pill's fill, inside its outline.</summary>
        public Image CardReadoutFill
        {
            get
            {
                EnsureInitialized();
                return _cardReadoutFill;
            }
        }

        /// <summary>The card being worked — `331 × 41 · hard pile`.</summary>
        public Text CardReadoutText
        {
            get
            {
                EnsureInitialized();
                return _cardReadoutText;
            }
        }

        /// <summary>`grid` — the working-out itself, sized to whatever Core reports.</summary>
        public RectTransform GridRect
        {
            get
            {
                EnsureInitialized();
                return _gridRect;
            }
        }

        /// <summary>The row kinds drawn, top to bottom — exactly Core's own list, in Core's own order.</summary>
        public IReadOnlyList<GridRowKind> RowKinds
        {
            get
            {
                EnsureInitialized();
                return _rowKinds;
            }
        }

        /// <summary>One rect per drawn row, top to bottom, matching <see cref="RowKinds"/>.</summary>
        public IReadOnlyList<RectTransform> RowRects
        {
            get
            {
                EnsureInitialized();
                return _rowRects;
            }
        }

        /// <summary>The drawn rule lines, top to bottom. Two of them, and they are not rows.</summary>
        public IReadOnlyList<RectTransform> RuleRects
        {
            get
            {
                EnsureInitialized();
                return _ruleRects;
            }
        }

        /// <summary>Every drawn cell, by row then column — column zero being the operator column.</summary>
        public IReadOnlyList<IReadOnlyList<WorkingOutGridCell>> Cells
        {
            get
            {
                EnsureInitialized();
                return _cells;
            }
        }

        /// <summary>`keypad` — the twelve keys, fixed in place for every problem.</summary>
        public RectTransform KeypadRect
        {
            get
            {
                EnsureInitialized();
                return _keypadRect;
            }
        }

        /// <summary>The keys, in reading order: `1`–`9`, then backspace, `0`, `clear`.</summary>
        public IReadOnlyList<WorkingOutKeypadKey> Keys
        {
            get
            {
                EnsureInitialized();
                return _keys;
            }
        }

        /// <summary>`submit` — `Check it`, full keypad width, disabled until the answer row has a digit.</summary>
        public Button CheckItButton
        {
            get
            {
                EnsureInitialized();
                return _checkItButton;
            }
        }

        /// <summary>How many rows the addition section currently holds — the count Core's model is asked for.</summary>
        public int AdditionRowCount
        {
            get
            {
                EnsureInitialized();
                return _additionRows.Count;
            }
        }

        /// <summary>The cell the caret is in. Every typed digit lands here.</summary>
        public WorkingOutGridCell CaretCell
        {
            get
            {
                EnsureInitialized();
                return FindCell(_caretRowKind, _caretRowOrdinal, _caretColumn);
            }
        }

        /// <summary>
        /// The answer row's digits, read left to right, with empty cells
        /// contributing nothing — the string `Check it` submits. Empty when
        /// nothing has been typed into the answer row.
        /// </summary>
        public string AnswerText
        {
            get
            {
                EnsureInitialized();
                return _answerRow == null ? string.Empty : _answerRow.ReadLeftToRight();
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Points the grid at the turn it was opened on, and at the router
        /// `Check it` navigates through, then opens it. The caret starts in
        /// the rightmost answer cell — "the first empty answer cell is the
        /// focused one; typing fills the answer row right to left."
        ///
        /// The router is optional for the same reason
        /// <c>RollAndCardDialogView.Initialize</c>'s is: a test that only
        /// cares what the grid draws should not have to hand it a navigation
        /// graph.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="turn"/> is null.</exception>
        public void Initialize(IWorkingOutTurn turn, ScreenRouter router = null)
        {
            EnsureInitialized();

            _turn = turn ?? throw new ArgumentNullException(nameof(turn));
            _router = router;

            _carryRows.Clear();
            _additionRows.Clear();
            _answerRow = null;
            _columnCount = 0;
            _nextStamp = 0;

            Refresh();

            // The rightmost answer cell, which on a grid nobody has typed in
            // yet is also the first empty one.
            MoveCaretTo(GridRowKind.AnswerRow, 0, _columnCount - 1);

            _dialog.Open();
        }

        /// <summary>
        /// Re-reads the turn and redraws: the chip, the card readout, and the
        /// whole grid, asked of <see cref="WorkingOutGrid.For"/> again at the
        /// addition-row count the section currently holds.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_turn == null)
            {
                return;
            }

            var frog = _turn.Frog;
            _whoseChip.SetFrog(FrogColours.For(frog), frog.ToString());
            _whoseChip.SetState(PlayerChipState.Active);

            var card = _turn.Card;
            _cardReadoutText.text = string.Format(
                CardReadoutFormat,
                card.Multiplicand,
                card.Multiplier,
                PileNameFor(_turn.Pile));

            LayoutHeader();
            RebuildGrid();
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and RollAndCardDialogView's
        // own EnsureInitialized.
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

            _dialog = GetComponent<DialogPanel>();
            if (_dialog == null)
            {
                _dialog = gameObject.AddComponent<DialogPanel>();
            }

            // Full-bleed: the panel is the shared Dialog's maximum, which is
            // the canvas inset by SafeMargin on every side.
            _dialog.SetSize(DialogPanel.DialogMaxWidth, DialogPanel.DialogMaxHeight);

            BuildHeader();
            BuildGridContainer();
            BuildKeypad();
        }

        void BuildHeader()
        {
            var headerGO = new GameObject("Header", typeof(RectTransform));
            _headerRect = (RectTransform)headerGO.transform;
            _headerRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _headerRect.anchorMin = new Vector2(0f, 1f);
            _headerRect.anchorMax = new Vector2(0f, 1f);
            _headerRect.pivot = new Vector2(0f, 1f);
            _headerRect.sizeDelta = new Vector2(
                _dialog.PanelRect.rect.width - (2f * DialogPanel.DialogPadding),
                PlayerChip.PlayerChipHeight);
            _headerRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, -GridHeaderTop);

            var chipGO = new GameObject("WhoseChip", typeof(RectTransform));
            var chipRect = (RectTransform)chipGO.transform;
            chipRect.SetParent(_headerRect, worldPositionStays: false);
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.pivot = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = Vector2.zero;
            _whoseChip = chipGO.AddComponent<PlayerChip>();

            // Unity does not run Awake() on AddComponent outside play mode,
            // and the chip sizes itself when it builds. Touch its rect so it
            // builds now, while the header is being laid out.
            chipRect = _whoseChip.RectTransform;

            _promptText = BuildText("Prompt", _headerRect, GridPromptSize, LineColor, TextAnchor.MiddleLeft);
            _promptText.text = PromptLabel;

            var promptRect = _promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0f, 0.5f);
            promptRect.anchorMax = new Vector2(0f, 0.5f);
            promptRect.pivot = new Vector2(0f, 0.5f);

            var readoutGO = new GameObject("CardReadout", typeof(RectTransform), typeof(Image));
            _cardReadoutBorder = readoutGO.GetComponent<Image>();
            _cardReadoutBorder.sprite = ReadoutSprite;
            _cardReadoutBorder.type = Image.Type.Sliced;
            _cardReadoutBorder.color = FaintColor;
            _cardReadoutBorder.raycastTarget = false;

            _cardReadoutRect = _cardReadoutBorder.rectTransform;
            _cardReadoutRect.SetParent(_headerRect, worldPositionStays: false);
            _cardReadoutRect.anchorMin = new Vector2(0f, 0.5f);
            _cardReadoutRect.anchorMax = new Vector2(0f, 0.5f);
            _cardReadoutRect.pivot = new Vector2(0f, 0.5f);

            var readoutFillGO = new GameObject("CardReadoutFill", typeof(RectTransform), typeof(Image));
            _cardReadoutFill = readoutFillGO.GetComponent<Image>();
            _cardReadoutFill.sprite = ReadoutSprite;
            _cardReadoutFill.type = Image.Type.Sliced;
            _cardReadoutFill.color = PaperColor;
            _cardReadoutFill.raycastTarget = false;

            var readoutFillRect = _cardReadoutFill.rectTransform;
            readoutFillRect.SetParent(_cardReadoutRect, worldPositionStays: false);
            readoutFillRect.anchorMin = Vector2.zero;
            readoutFillRect.anchorMax = Vector2.one;
            readoutFillRect.offsetMin = new Vector2(GridCardReadoutBorderWidth, GridCardReadoutBorderWidth);
            readoutFillRect.offsetMax = new Vector2(-GridCardReadoutBorderWidth, -GridCardReadoutBorderWidth);

            _cardReadoutText = BuildText("CardReadoutLabel", _cardReadoutRect, GridCardReadoutLabelSize, InkColor, TextAnchor.MiddleCenter);
            _cardReadoutText.fontStyle = FontStyle.Bold;
            var readoutTextRect = _cardReadoutText.rectTransform;
            readoutTextRect.anchorMin = Vector2.zero;
            readoutTextRect.anchorMax = Vector2.one;
            readoutTextRect.offsetMin = Vector2.zero;
            readoutTextRect.offsetMax = Vector2.zero;

            LayoutHeader();
        }

        // The header is one row: the chip, the prompt, then the card readout,
        // GridHeaderGap apart. The two labels are content-sized, so the row is
        // laid out again whenever the card changes rather than positioned once
        // at build time.
        void LayoutHeader()
        {
            var promptWidth = _promptText.preferredWidth;
            var promptRect = _promptText.rectTransform;
            promptRect.sizeDelta = new Vector2(promptWidth, GridPromptSize);
            promptRect.anchoredPosition = new Vector2(PlayerChip.PlayerChipWidth + GridHeaderGap, 0f);

            var readoutWidth = _cardReadoutText.preferredWidth + (2f * GridCardReadoutPaddingX);
            _cardReadoutRect.sizeDelta = new Vector2(readoutWidth, GridCardReadoutHeight);
            _cardReadoutRect.anchoredPosition = new Vector2(
                PlayerChip.PlayerChipWidth + GridHeaderGap + promptWidth + GridHeaderGap,
                0f);
        }

        void BuildGridContainer()
        {
            var gridGO = new GameObject("Grid", typeof(RectTransform));
            _gridRect = (RectTransform)gridGO.transform;
            _gridRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _gridRect.anchorMin = new Vector2(0f, 1f);
            _gridRect.anchorMax = new Vector2(0f, 1f);
            _gridRect.pivot = new Vector2(0f, 1f);
        }

        // Everything about the grid, from Core's model outward. Called once
        // per shape change — a card, or the addition section growing or
        // shrinking — and never per keystroke.
        void RebuildGrid()
        {
            var model = WorkingOutGrid.For(_turn.Card, _additionRows.Count == 0
                ? WorkingOutGrid.GridAdditionRowsAtStart
                : _additionRows.Count);

            EnsureContents(model.ColumnCount);

            for (var index = _gridRect.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(_gridRect.GetChild(index).gameObject);
            }

            _rowKinds.Clear();
            _rowRects.Clear();
            _ruleRects.Clear();
            _cells.Clear();

            var width = (model.ColumnCount * GridCellSize) + ((model.ColumnCount - 1) * GridCellGap);
            var height = GridHeightFor(model);

            _gridRect.sizeDelta = new Vector2(width, height);
            _gridRect.anchoredPosition = GridOrigin(width, height);

            var ordinals = new Dictionary<GridRowKind, int>();
            var cursor = 0f;

            for (var index = 0; index < model.Rows.Count; index++)
            {
                var row = model.Rows[index];

                var ordinal = 0;
                if (ordinals.ContainsKey(row.Kind))
                {
                    ordinal = ordinals[row.Kind];
                }

                ordinals[row.Kind] = ordinal + 1;

                var rowHeight = RowHeightFor(row.Kind);
                var rowRect = BuildRow(row, ordinal, IsBottomOfSection(model, index), width, rowHeight, cursor);

                _rowKinds.Add(row.Kind);
                _rowRects.Add(rowRect);

                cursor += rowHeight + GridCellGap;

                if (!RuleFollows(model, index))
                {
                    continue;
                }

                _ruleRects.Add(BuildRule(width, cursor));
                cursor += GridRuleThickness + GridCellGap;
            }

            RefreshCells();
        }

        // A rule line goes under the multiplier row, and under the bottom row
        // of the addition section. Both adjacencies are read off the row kinds
        // Core reports — this is the one piece of shape the shell derives, and
        // it derives nothing else.
        static bool RuleFollows(WorkingOutGrid model, int index)
        {
            if (index + 1 >= model.Rows.Count)
            {
                return false;
            }

            var kind = model.Rows[index].Kind;
            var next = model.Rows[index + 1].Kind;

            if (kind == GridRowKind.Multiplier)
            {
                return true;
            }

            return kind == GridRowKind.AdditionRow && next != GridRowKind.AdditionRow;
        }

        // The `+` sits in the operator column of the bottom row of the
        // addition section, wherever the bottom currently is — growing the
        // section moves the glyph down with it rather than stamping a `+` on
        // every row.
        static bool IsBottomOfSection(WorkingOutGrid model, int index)
        {
            if (model.Rows[index].Kind != GridRowKind.AdditionRow)
            {
                return false;
            }

            return index + 1 >= model.Rows.Count
                || model.Rows[index + 1].Kind != GridRowKind.AdditionRow;
        }

        float GridHeightFor(WorkingOutGrid model)
        {
            var total = 0f;
            var parts = 0;

            for (var index = 0; index < model.Rows.Count; index++)
            {
                total += RowHeightFor(model.Rows[index].Kind);
                parts++;

                if (!RuleFollows(model, index))
                {
                    continue;
                }

                total += GridRuleThickness;
                parts++;
            }

            return total + ((parts - 1) * GridCellGap);
        }

        // docs/specs/ui/working-out-grid.md#anchors: the keypad and `Check it`
        // are pinned right, DialogPadding from the panel edge, and the grid is
        // centred in the space left over — which is the panel inside its
        // padding, less the keypad column, and below the header band.
        Vector2 GridOrigin(float width, float height)
        {
            var panel = _dialog.PanelRect.rect;

            var regionLeft = DialogPanel.DialogPadding;
            var regionRight = panel.width - DialogPanel.DialogPadding - KeypadWidth;
            var regionTop = GridHeaderHeight;
            var regionBottom = panel.height - DialogPanel.DialogPadding;

            var left = regionLeft + ((regionRight - regionLeft - width) / 2f);
            var top = regionTop + ((regionBottom - regionTop - height) / 2f);

            return new Vector2(left, -top);
        }

        /// <summary>
        /// How tall one row of <paramref name="kind"/> is drawn.
        ///
        /// The addition section is the only kind whose height is not fixed:
        /// Derek's call on #223 — "smaller cells for addition rows only" —
        /// gives it <see cref="GridAdditionRowHeight"/> once the player has
        /// grown it past the count every card is dealt, and leaves the
        /// multiplicand, the multiplier, the rules, the carry strips and the
        /// answer row at their full size at every count. That is what keeps
        /// the section inside the dialog at <c>GridAdditionRowsMax</c>: six
        /// grown rows make an 892 px grid in the 908 px the panel has below
        /// its header.
        /// </summary>
        public float RowHeightFor(GridRowKind kind)
        {
            switch (kind)
            {
                case GridRowKind.CarryStrip:
                    return GridCarryRowHeight;

                case GridRowKind.AnswerRow:
                    return GridAnswerRowHeight;

                case GridRowKind.AdditionRow:
                    return _additionRows.Count > WorkingOutGrid.GridAdditionRowsAtStart
                        ? GridAdditionRowHeight
                        : GridCellSize;

                default:
                    return GridCellSize;
            }
        }

        float DigitSizeFor(GridRowKind kind)
        {
            if (kind == GridRowKind.CarryStrip)
            {
                return GridSmallDigitSize;
            }

            return RowHeightFor(kind) < GridCellSize ? GridSmallDigitSize : GridDigitSize;
        }

        RectTransform BuildRow(
            GridRow row,
            int ordinal,
            bool isBottomOfSection,
            float width,
            float rowHeight,
            float top)
        {
            var rowGO = new GameObject(row.Kind + "Row", typeof(RectTransform));
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.SetParent(_gridRect, worldPositionStays: false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(width, rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -top);

            var cells = new List<WorkingOutGridCell>();

            for (var column = 0; column < row.Cells.Count; column++)
            {
                cells.Add(BuildCell(row, ordinal, isBottomOfSection, column, rowRect, rowHeight));
            }

            _cells.Add(cells);
            return rowRect;
        }

        WorkingOutGridCell BuildCell(
            GridRow row,
            int ordinal,
            bool isBottomOfSection,
            int column,
            RectTransform rowRect,
            float rowHeight)
        {
            var cellGO = new GameObject("Cell" + column, typeof(RectTransform));
            var cellRect = (RectTransform)cellGO.transform;
            cellRect.SetParent(rowRect, worldPositionStays: false);
            cellRect.anchorMin = new Vector2(0f, 1f);
            cellRect.anchorMax = new Vector2(0f, 1f);
            cellRect.pivot = new Vector2(0f, 1f);
            cellRect.sizeDelta = new Vector2(GridCellSize, rowHeight);
            cellRect.anchoredPosition = new Vector2(column * (GridCellSize + GridCellGap), 0f);

            var cell = cellGO.AddComponent<WorkingOutGridCell>();
            var kind = row.Cells[column].Kind;

            Image border = null;
            Image fill = null;
            Text label = null;

            switch (kind)
            {
                case GridCellKind.CarryBox:
                    border = BuildBox(
                        cellRect,
                        CarryBoxSprite,
                        GridCarryBoxSize,
                        GridCarryBoxBorderWidth,
                        FaintColor,
                        out fill);
                    label = BuildText("Digit", border.rectTransform, GridSmallDigitSize, InkColor, TextAnchor.MiddleCenter);
                    StretchToFill(label.rectTransform);
                    break;

                case GridCellKind.Editable:
                    var borderWidth = row.Kind == GridRowKind.AnswerRow
                        ? GridAnswerBorderWidth
                        : GridCellBorderWidth;

                    border = BuildBox(
                        cellRect,
                        CellSprite,
                        new Vector2(GridCellSize, rowHeight),
                        borderWidth,
                        LineColor,
                        out fill);

                    if (row.Kind == GridRowKind.AnswerRow)
                    {
                        fill.color = AnswerTintColor;
                    }

                    label = BuildText("Digit", border.rectTransform, DigitSizeFor(row.Kind), InkColor, TextAnchor.MiddleCenter);
                    StretchToFill(label.rectTransform);
                    break;

                case GridCellKind.Printed:
                    // The card's own digits: no box, no fill — the mockups'
                    // `.cell.fix` is border:none on a transparent background.
                    label = BuildText("Digit", cellRect, DigitSizeFor(row.Kind), InkColor, TextAnchor.MiddleCenter);
                    label.fontStyle = FontStyle.Bold;
                    StretchToFill(label.rectTransform);
                    label.text = row.Cells[column].Digit.ToString();
                    break;

                default:
                    // Blank: the leading columns of a printed row, and the
                    // operator column of every row. Only the operator column
                    // ever has anything drawn in it, and what that is comes
                    // from the row kind.
                    if (column == OperatorColumn)
                    {
                        var glyph = OperatorGlyphFor(row.Kind, isBottomOfSection);

                        if (glyph.Length > 0)
                        {
                            var isEquals = glyph == EqualsGlyph;
                            label = BuildText(
                                "Operator",
                                cellRect,
                                isEquals ? GridEqualsSize : GridDigitSize,
                                isEquals ? LineColor : InkColor,
                                TextAnchor.MiddleCenter);
                            label.fontStyle = FontStyle.Bold;
                            StretchToFill(label.rectTransform);
                            label.text = glyph;
                        }
                    }

                    break;
            }

            cell.Describe(row.Kind, ordinal, kind, column, border, fill, label);
            cell.Tapped += HandleCellTapped;
            return cell;
        }

        // `×` on the multiplier row, `+` on the bottom row of the addition
        // section, `=` on the answer row, and nothing on the carry strips or
        // the multiplicand row — derived from the row kind being drawn, never
        // from a per-card lookup.
        static string OperatorGlyphFor(GridRowKind kind, bool isBottomOfSection)
        {
            switch (kind)
            {
                case GridRowKind.Multiplier:
                    return MultiplyGlyph;

                case GridRowKind.AdditionRow:
                    return isBottomOfSection ? AddGlyph : NoGlyph;

                case GridRowKind.AnswerRow:
                    return EqualsGlyph;

                default:
                    return NoGlyph;
            }
        }

        // An outlined box: the border image, with the fill inset by the
        // border's own width. "Flat" in these mockups means flat-coloured,
        // not borderless.
        static Image BuildBox(
            RectTransform parent,
            Sprite sprite,
            Vector2 size,
            float borderWidth,
            Color borderColor,
            out Image fill)
        {
            var borderGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var border = borderGO.GetComponent<Image>();
            border.sprite = sprite;
            border.type = Image.Type.Sliced;
            border.color = borderColor;
            border.raycastTarget = false;

            var borderRect = border.rectTransform;
            borderRect.SetParent(parent, worldPositionStays: false);
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = size;
            borderRect.anchoredPosition = Vector2.zero;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill = fillGO.GetComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Sliced;
            fill.color = PaperColor;
            fill.raycastTarget = false;

            var fillRect = fill.rectTransform;
            fillRect.SetParent(borderRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(borderWidth, borderWidth);
            fillRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

            return border;
        }

        RectTransform BuildRule(float width, float top)
        {
            var ruleGO = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            var rule = ruleGO.GetComponent<Image>();
            rule.color = InkColor;
            rule.raycastTarget = false;

            var ruleRect = rule.rectTransform;
            ruleRect.SetParent(_gridRect, worldPositionStays: false);
            ruleRect.anchorMin = new Vector2(0f, 1f);
            ruleRect.anchorMax = new Vector2(0f, 1f);
            ruleRect.pivot = new Vector2(0f, 1f);
            ruleRect.sizeDelta = new Vector2(width, GridRuleThickness);
            ruleRect.anchoredPosition = new Vector2(0f, -top);

            return ruleRect;
        }

        void BuildKeypad()
        {
            var keyGridHeight = (KeypadRows * KeypadKeySize) + ((KeypadRows - 1) * KeypadKeyGap);
            var columnHeight = keyGridHeight + KeypadSubmitGap + CheckButtonHeight;

            var keypadGO = new GameObject("Keypad", typeof(RectTransform));
            _keypadRect = (RectTransform)keypadGO.transform;
            _keypadRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _keypadRect.anchorMin = new Vector2(1f, 1f);
            _keypadRect.anchorMax = new Vector2(1f, 1f);
            _keypadRect.pivot = new Vector2(1f, 1f);
            _keypadRect.sizeDelta = new Vector2(KeypadWidth, columnHeight);
            _keypadRect.anchoredPosition = new Vector2(-DialogPanel.DialogPadding, -KeypadTop);

            var keyGridGO = new GameObject("Keys", typeof(RectTransform));
            _keyGridRect = (RectTransform)keyGridGO.transform;
            _keyGridRect.SetParent(_keypadRect, worldPositionStays: false);
            _keyGridRect.anchorMin = new Vector2(0f, 1f);
            _keyGridRect.anchorMax = new Vector2(0f, 1f);
            _keyGridRect.pivot = new Vector2(0f, 1f);
            _keyGridRect.sizeDelta = new Vector2(KeypadWidth, keyGridHeight);
            _keyGridRect.anchoredPosition = Vector2.zero;

            // `1`–`9`, then backspace, `0`, `clear` — the mockups' own order.
            for (var digit = 1; digit <= 9; digit++)
            {
                BuildKey(KeypadKeyKind.Digit, digit, digit.ToString(), KeypadKeyLabelSize);
            }

            BuildKey(KeypadKeyKind.Backspace, -1, BackspaceLabel, KeypadBackspaceLabelSize);
            BuildKey(KeypadKeyKind.Digit, 0, "0", KeypadKeyLabelSize);
            BuildKey(KeypadKeyKind.Clear, -1, ClearLabel, KeypadClearLabelSize);

            _checkItButton = BuildCheckIt(keyGridHeight);
        }

        void BuildKey(KeypadKeyKind kind, int digit, string label, float labelSize)
        {
            var index = _keys.Count;
            var column = index % KeypadColumns;
            var row = index / KeypadColumns;

            var keyGO = new GameObject("Key" + label, typeof(RectTransform));
            var keyRect = (RectTransform)keyGO.transform;
            keyRect.SetParent(_keyGridRect, worldPositionStays: false);
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0f, 1f);
            keyRect.sizeDelta = new Vector2(KeypadKeySize, KeypadKeySize);
            keyRect.anchoredPosition = new Vector2(
                column * (KeypadKeySize + KeypadKeyGap),
                -(row * (KeypadKeySize + KeypadKeyGap)));

            Image fill;
            var border = BuildBox(
                keyRect,
                KeySprite,
                new Vector2(KeypadKeySize, KeypadKeySize),
                KeypadKeyBorderWidth,
                LineColor,
                out fill);

            var text = BuildText("Label", border.rectTransform, labelSize, InkColor, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            StretchToFill(text.rectTransform);

            var key = keyGO.AddComponent<WorkingOutKeypadKey>();
            key.Describe(kind, digit, border, fill, text);
            key.Tapped += HandleKeyTapped;

            _keys.Add(key);
        }

        Button BuildCheckIt(float keyGridHeight)
        {
            var buttonGO = new GameObject(CheckItLabel, typeof(RectTransform));
            buttonGO.transform.SetParent(_keypadRect, worldPositionStays: false);

            // A shared primary Button, parented into the keypad column rather
            // than the shared Dialog's button row: this page pins `Check it`
            // under the keypad, at full keypad width, so the row's
            // right-aligned layout is not what puts it where it goes.
            var button = buttonGO.AddComponent<Button>();
            button.SetKind(ButtonKind.Primary);
            button.SetLabelText(CheckItLabel);
            button.SetSize(KeypadWidth, CheckButtonHeight);
            button.Clicked += HandleCheckItClicked;

            var buttonRect = button.RectTransform;
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -(keyGridHeight + KeypadSubmitGap));

            // An empty answer is not a wrong answer, so it cannot be
            // submitted at all.
            button.SetDisabled(true);

            return button;
        }

        void EnsureContents(int columnCount)
        {
            if (_columnCount == columnCount && _answerRow != null)
            {
                return;
            }

            _columnCount = columnCount;
            _carryRows.Clear();
            _additionRows.Clear();

            for (var index = 0; index < WorkingOutGrid.CarryStripCount; index++)
            {
                _carryRows.Add(new RowContents(columnCount));
            }

            for (var index = 0; index < WorkingOutGrid.GridAdditionRowsAtStart; index++)
            {
                _additionRows.Add(new RowContents(columnCount));
            }

            _answerRow = new RowContents(columnCount);
        }

        void HandleCellTapped(WorkingOutGridCell cell)
        {
            // "Tapping any cell moves the caret there, so the grid can be
            // filled in any order." A printed digit and the operator column
            // are not cells anything can be typed into, so a tap on one moves
            // nothing.
            if (!cell.IsEditable)
            {
                return;
            }

            MoveCaretTo(cell.RowKind, cell.RowOrdinal, cell.Column);
        }

        void HandleKeyTapped(WorkingOutKeypadKey key)
        {
            switch (key.Kind)
            {
                case KeypadKeyKind.Digit:
                    TypeDigit(key.Digit);
                    break;

                case KeypadKeyKind.Backspace:
                    Backspace();
                    break;

                case KeypadKeyKind.Clear:
                    ClearBlock();
                    break;
            }
        }

        void TypeDigit(int digit)
        {
            var contents = CaretContents();

            if (contents == null)
            {
                return;
            }

            contents.Write(_caretColumn, digit, _nextStamp++);

            var grew = GrowSectionIfWrittenAtItsBottom();

            if (grew)
            {
                RebuildGrid();
            }

            // Right to left, "which is the direction the digits are worked
            // out in": the next digit goes in the cell to the left, until
            // there is no cell to the left.
            var nextColumn = Mathf.Max(_caretColumn - 1, OperatorColumn + 1);
            MoveCaretTo(_caretRowKind, _caretRowOrdinal, nextColumn);
        }

        // "Typing a single digit into the section's current bottom row appends
        // another row beneath it, still above the answer row. That repeats
        // each time the new bottom row is written in, until the section holds
        // GridAdditionRowsMax rows, after which nothing more is appended."
        bool GrowSectionIfWrittenAtItsBottom()
        {
            if (_caretRowKind != GridRowKind.AdditionRow)
            {
                return false;
            }

            if (_caretRowOrdinal != _additionRows.Count - 1)
            {
                return false;
            }

            if (_additionRows.Count >= WorkingOutGrid.GridAdditionRowsMax)
            {
                return false;
            }

            _additionRows.Add(new RowContents(_columnCount));
            return true;
        }

        void Backspace()
        {
            var contents = CaretContents();

            if (contents == null)
            {
                return;
            }

            // "Backspace removes the digit in the caret's current cell, or the
            // last-entered digit of the current block if the caret's own cell
            // is empty" — and nothing outside that block, ever.
            var column = contents.HasDigit(_caretColumn)
                ? _caretColumn
                : contents.LastEnteredColumn();

            if (column < 0)
            {
                return;
            }

            contents.Erase(column);
            MoveCaretTo(_caretRowKind, _caretRowOrdinal, column);

            ShrinkSectionIfEmptiedAtItsBottom();
        }

        // Derek's call on #204, open question 5: backspacing the last digit
        // out of a grown row removes the row. Only the section's *bottom* row
        // can go, and only a grown one — GridAdditionRowsAtStart is the floor,
        // because no card is ever dealt fewer.
        void ShrinkSectionIfEmptiedAtItsBottom()
        {
            if (_caretRowKind != GridRowKind.AdditionRow)
            {
                return;
            }

            if (_additionRows.Count <= WorkingOutGrid.GridAdditionRowsAtStart)
            {
                return;
            }

            var bottom = _additionRows.Count - 1;

            if (_caretRowOrdinal != bottom || !_additionRows[bottom].IsEmpty())
            {
                return;
            }

            _additionRows.RemoveAt(bottom);
            RebuildGrid();

            // The row the caret was in is gone; it lands in the same column of
            // whatever is the bottom of the section now.
            MoveCaretTo(GridRowKind.AdditionRow, _additionRows.Count - 1, _caretColumn);
        }

        void ClearBlock()
        {
            var contents = CaretContents();

            if (contents == null)
            {
                return;
            }

            // "`clear` empties only the cell block you are in, not the whole
            // grid." The narrowest reading of "block" that satisfies that
            // sentence is the row the caret is in — see this issue's PR.
            contents.EraseAll();
            RefreshCells();
        }

        void HandleCheckItClicked()
        {
            var answer = AnswerText;

            if (answer.Length == 0)
            {
                return;
            }

            var value = int.Parse(answer);

            // Out through the one seam, ungraded. What it comes back as is
            // #210's to decide and #224's to draw.
            _turn.SubmitAnswer(value);

            if (_router != null)
            {
                _router.OpenDialog(Frogs.Core.Dialog.AnswerResult);
            }

            var handler = AnswerSubmitted;
            if (handler != null)
            {
                handler(value);
            }
        }

        RowContents CaretContents()
        {
            return ContentsFor(_caretRowKind, _caretRowOrdinal);
        }

        RowContents ContentsFor(GridRowKind kind, int ordinal)
        {
            switch (kind)
            {
                case GridRowKind.CarryStrip:
                    return ordinal >= 0 && ordinal < _carryRows.Count ? _carryRows[ordinal] : null;

                case GridRowKind.AdditionRow:
                    return ordinal >= 0 && ordinal < _additionRows.Count ? _additionRows[ordinal] : null;

                case GridRowKind.AnswerRow:
                    return _answerRow;

                default:
                    return null;
            }
        }

        void MoveCaretTo(GridRowKind kind, int ordinal, int column)
        {
            _caretRowKind = kind;
            _caretRowOrdinal = ordinal;
            _caretColumn = column;

            RefreshCells();
        }

        // Every cell's text and outline, from what has been typed and where
        // the caret is. No cell's colour depends on whether what is in it is
        // right — this view has no idea what right would be.
        void RefreshCells()
        {
            for (var row = 0; row < _cells.Count; row++)
            {
                var cells = _cells[row];

                for (var column = 0; column < cells.Count; column++)
                {
                    var cell = cells[column];

                    if (!cell.IsEditable)
                    {
                        continue;
                    }

                    var contents = ContentsFor(cell.RowKind, cell.RowOrdinal);
                    cell.SetText(contents == null ? string.Empty : contents.TextAt(column));

                    if (cell.Border == null)
                    {
                        continue;
                    }

                    var focused = cell.RowKind == _caretRowKind
                        && cell.RowOrdinal == _caretRowOrdinal
                        && cell.Column == _caretColumn;

                    cell.Border.color = focused
                        ? AccentColor
                        : cell.Kind == GridCellKind.CarryBox ? FaintColor : LineColor;
                }
            }

            if (_checkItButton != null)
            {
                _checkItButton.SetDisabled(AnswerText.Length == 0);
            }
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

        WorkingOutGridCell FindCell(GridRowKind kind, int ordinal, int column)
        {
            for (var row = 0; row < _cells.Count; row++)
            {
                var cells = _cells[row];

                if (cells.Count <= column || column < 0)
                {
                    continue;
                }

                var cell = cells[column];

                if (cell.RowKind == kind && cell.RowOrdinal == ordinal)
                {
                    return cell;
                }
            }

            return null;
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        /// <summary>
        /// What one row of the grid holds, and in what order it was typed.
        /// Kept per row *identity* — the second carry strip, the fourth
        /// addition row — rather than per drawn row, so a digit stays where it
        /// was put when the section grows or shrinks underneath it.
        ///
        /// It holds digits and nothing else: no correct value, no flag for
        /// whether a digit is right, nothing that could be rendered as a mark.
        /// </summary>
        sealed class RowContents
        {
            readonly int?[] _digits;
            readonly int[] _stamps;

            internal RowContents(int columnCount)
            {
                _digits = new int?[columnCount];
                _stamps = new int[columnCount];
            }

            internal bool HasDigit(int column)
            {
                return column >= 0 && column < _digits.Length && _digits[column].HasValue;
            }

            internal string TextAt(int column)
            {
                return HasDigit(column) ? _digits[column].Value.ToString() : string.Empty;
            }

            internal void Write(int column, int digit, int stamp)
            {
                if (column < 0 || column >= _digits.Length)
                {
                    return;
                }

                _digits[column] = digit;
                _stamps[column] = stamp;
            }

            internal void Erase(int column)
            {
                if (column < 0 || column >= _digits.Length)
                {
                    return;
                }

                _digits[column] = null;
                _stamps[column] = 0;
            }

            internal void EraseAll()
            {
                for (var column = 0; column < _digits.Length; column++)
                {
                    _digits[column] = null;
                    _stamps[column] = 0;
                }
            }

            internal bool IsEmpty()
            {
                for (var column = 0; column < _digits.Length; column++)
                {
                    if (_digits[column].HasValue)
                    {
                        return false;
                    }
                }

                return true;
            }

            // The digit typed most recently anywhere in this row, which is
            // what backspace takes away when the caret's own cell is already
            // empty.
            internal int LastEnteredColumn()
            {
                var best = -1;

                for (var column = 0; column < _digits.Length; column++)
                {
                    if (!_digits[column].HasValue)
                    {
                        continue;
                    }

                    if (best < 0 || _stamps[column] > _stamps[best])
                    {
                        best = column;
                    }
                }

                return best;
            }

            internal string ReadLeftToRight()
            {
                var builder = new StringBuilder();

                for (var column = 0; column < _digits.Length; column++)
                {
                    if (_digits[column].HasValue)
                    {
                        builder.Append(_digits[column].Value);
                    }
                }

                return builder.ToString();
            }
        }
    }
}
