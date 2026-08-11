using System;
using System.Globalization;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// WorkingOutGridView.cs and RollAndCardDialogView.cs work around — so the
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
    /// Right or wrong, what the number was, and what the frog does about it —
    /// docs/specs/ui/answer-result.md, built to that page and its two committed
    /// 1:1 mockups, on the shared <see cref="DialogPanel"/> (#219). Both states
    /// are one layout with different content: the same panel size and the same
    /// anchors, because "two dialogs that jump about are two dialogs a child
    /// reads as two different things happening."
    ///
    /// **Nothing is decided here.** Core compared the answer and computed the
    /// new position before this dialog opened (<see cref="Lane.Resolve"/>,
    /// #210); this view reads the resulting <see cref="TurnResolution"/> out.
    /// The three consequence sentences are rendered *from the outcome* — no
    /// English is stored in Core, and no outcome is inferred here from the
    /// position numbers. Whose turn is next is
    /// <see cref="IAnswerResultTurn.NextFrog"/>, which is
    /// <see cref="Game.NextActiveFrog"/> (#208) — asked while this dialog is
    /// still showing the *current* player's result, and answered without
    /// advancing anything. On the one turn that has no next player — the hop
    /// that got the last frog home — the button falls back to
    /// <see cref="NoNextPlayerLabel"/>, whose wording is still Connor's to
    /// settle (#287).
    ///
    /// On a wrong answer **the correct answer is revealed and the working is
    /// not** — docs/adr/0002-structured-working-out-grid.md. There is no
    /// affordance here of any kind to show partial products, and adding one
    /// would make a method canonical, which is the stance the project has
    /// deliberately not taken.
    ///
    /// **Hardware back is inert**, the same way it is on the two dialogs
    /// before this one: by adding no handler at all and nominating no
    /// least-destructive button on the shared Dialog. The router (#213)
    /// already knows back does nothing over
    /// <see cref="Frogs.Core.Dialog.AnswerResult"/>.
    ///
    /// This view also owns **the hop**, which is the one piece of motion on
    /// the board. <c>unity-game-board</c> (#220) draws a frog at rest at
    /// whatever position Core reports and has no motion of its own; this view
    /// plays the interpolation between the before and after positions Core
    /// already reported, using #220's own placement for both endpoints, and
    /// hands the board back through its <c>NotifyTurnResolved</c> seam when the
    /// frog lands. Core's position changed at grading time; nothing here
    /// touches Core's state beyond the two hand-off steps the button runs.
    ///
    /// Both of answer-result.md's open questions are left exactly as open as it
    /// leaves them: there is no celebration beyond the hop — no sound, no
    /// flourish, not a silent <c>AudioSource</c> waiting for a clip — and the
    /// wrong state does not offer to show the working.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class AnswerResultDialogView : MonoBehaviour
    {
        // docs/specs/ui/answer-result.md#named-constants — the page's own
        // table, under the identical names.
        public const float ResultDialogWidth = 1100f;
        public const float ResultDialogHeight = 620f;
        public const float ResultMarkSize = 180f;
        public const float ResultMarkRingWidth = 8f;
        public const float ResultMarkGlyphSize = 110f;
        public const float ResultVerdictSize = 76f;
        public const float ResultVerdictTop = 70f;
        public const float ResultConsequenceSize = 48f;
        public const float ResultConsequenceTop = 180f;
        public const float ResultTextWidth = 760f;
        public const float ResultTextLeft = 280f;
        public const float ResultChipTop = 340f;
        public const float ResultHopDelay = 0.2f;

        // `FrogHopDuration` is the *board's* constant, not this page's — the
        // hop happens on that screen, after this dialog has gone. Referenced
        // rather than redeclared, so the two can never disagree:
        // GameBoardScreenView.FrogHopDuration.
        //
        // The panel's scrim, corners, padding, radius and cross-fade are the
        // shared Dialog's, referenced the same way — DialogPanel.DialogPadding
        // places `mark`, `chip` and `controls`, and DialogFadeDuration is the
        // close this sequence waits on rather than a second animation of its
        // own.

        // The three moments of the hand-off, as offsets from the press. Every
        // one of them is a named duration added to the one before it, so the
        // sequence's shape is visible in one place.
        const float HandOffFadeEnd = DialogPanel.DialogFadeDuration;
        const float HandOffHopStart = HandOffFadeEnd + ResultHopDelay;
        const float HandOffDuration = HandOffHopStart + GameBoardScreenView.FrogHopDuration;

        // The consequence is two sentences on the wrong states and one on the
        // right one; the column is given room for two lines either way, so the
        // block does not move between them. A count, not a measurement.
        const int ConsequenceLines = 2;

        // The mark's two glyphs, as the mockups draw them: system characters,
        // the same kind of glyph the board's gear already uses, and a string
        // rather than an imported asset.
        const string TickGlyph = "✓";
        const string CrossGlyph = "✗";

        // docs/specs/ui/answer-result.md § Elements and § The three
        // consequence sentences, written out verbatim. The dialog picks one of
        // the three by outcome; it never composes a fourth.
        const string EquationFormat = "{0} × {1} = {2}";
        const string NotThisTimeLabel = "Not this time";
        const string RightConsequenceFormat = "Right! {0}";
        const string WrongConsequenceFormat = "{0}. {1}";
        const string ForwardSentenceFormat = "{0} hops forward one lily pad.";
        const string RetreatSentenceFormat = "{0} hops back one lily pad.";
        const string StaysSentenceFormat = "{0} stays on the Start log.";

        // The chip shows a *move*, not the pad count its Default state draws
        // on the board — including in the floor case, where before and after
        // are the same number.
        const string ChipMoveFormat = "pad {0} → {1}";

        // The button is named for the next player, never `OK`.
        const string NextTurnFormat = "{0}'s turn";

        /// <summary>
        /// What the one button reads on the one turn that has no next player —
        /// the hop that got the last frog home.
        ///
        /// **This wording is not Connor's yet.** answer-result.md says the
        /// button is "named for the next player" and says nothing about the
        /// turn where there is not one, so this is a placeholder that keeps the
        /// dialog working rather than a decision: it borrows game-over.md's own
        /// words for the screen the button now leads to. Issue #287 asks Connor
        /// what it should say, and this constant is the only thing that has to
        /// change when he answers.
        /// </summary>
        public const string NoNextPlayerLabel = "Game over";

        // No imported font — matches Button.cs's, PlayerChip.cs's,
        // DialogPanel.cs's and WorkingOutGridView.cs's own choice, for the same
        // reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colours copied verbatim from the committed mockups
        // (docs/specs/ui/mockups/answer-result-*.html) — the same line
        // Button.cs, PlayerChip.cs, DialogPanel.cs and WorkingOutGridView.cs
        // each draw for their own colours: not a geometry constant on any spec
        // page's table, so not declared as a named spec constant.
        static readonly Color InkColor = new Color32(0x1E, 0x24, 0x22, 0xFF); // mockups' --ink
        static readonly Color LineColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockups' --line
        static readonly Color AccentColor = new Color32(0x2E, 0x7D, 0x4F, 0xFF); // mockups' --accent
        static readonly Color WarningColor = new Color32(0xB0, 0x3A, 0x2E, 0xFF); // mockups' --warn
        static readonly Color PaperColor = Color.white; // mockups' --paper / #fff

        static Sprite s_markSprite;
        static Sprite s_markFillSprite;

        // A rounded rect whose radius is half its own size is a circle — the
        // same shape PlayerChip's swatch and the board's lily pads use, rather
        // than a third way of drawing one. The inside gets its own sprite at
        // its own radius so its curve does not square off when it is inset.
        static Sprite MarkSprite
        {
            get
            {
                if (s_markSprite == null)
                {
                    s_markSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(ResultMarkSize / 2f));
                }

                return s_markSprite;
            }
        }

        static Sprite MarkFillSprite
        {
            get
            {
                if (s_markFillSprite == null)
                {
                    s_markFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt((ResultMarkSize - (2f * ResultMarkRingWidth)) / 2f));
                }

                return s_markFillSprite;
            }
        }

        RectTransform _rect;
        DialogPanel _dialog;

        RectTransform _markRect;
        Image _markRing;
        Image _markFill;
        Text _markGlyph;

        Text _verdictText;
        Text _consequenceText;

        RectTransform _chipRect;
        PlayerChip _chip;

        Button _nextTurnButton;

        bool _initialized;
        IAnswerResultTurn _turn;
        GameBoardScreenView _board;
        ScreenRouter _router;

        bool _handOffStarted;
        bool _handedOff;
        float _handOffElapsed;

        /// <summary>
        /// The hop has finished and the next player's turn has begun — the
        /// last of the four steps. Core has already been told
        /// (<see cref="IAnswerResultTurn.CompleteHandOff"/>) and the board has
        /// been handed back by the time this fires; this only says the
        /// sequence finished.
        /// </summary>
        public event Action TurnHandedOff;

        /// <summary>The view's own <see cref="RectTransform"/> — the full canvas the dialog covers.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this is built on — and the only thing that closes it is its one button.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>`mark` — <see cref="ResultMarkSize"/> across, pinned top-left of the panel's padding box.</summary>
        public RectTransform MarkRect
        {
            get
            {
                EnsureInitialized();
                return _markRect;
            }
        }

        /// <summary>
        /// The mark's outer circle. On a right answer it is the disc itself;
        /// on a wrong one it is a <see cref="ResultMarkRingWidth"/> ring around
        /// nothing.
        /// </summary>
        public Image MarkRing
        {
            get
            {
                EnsureInitialized();
                return _markRing;
            }
        }

        /// <summary>
        /// The mark's inside. It carries the shape contrast the "never
        /// signalled by colour alone" invariant rests on: the same colour as
        /// the ring makes a filled disc, the panel's own colour makes a hollow
        /// circle, and the two differ with the palette removed entirely.
        /// </summary>
        public Image MarkFill
        {
            get
            {
                EnsureInitialized();
                return _markFill;
            }
        }

        /// <summary>The tick or the cross, centred in the mark at <see cref="ResultMarkGlyphSize"/>.</summary>
        public Text MarkGlyph
        {
            get
            {
                EnsureInitialized();
                return _markGlyph;
            }
        }

        /// <summary>`verdict` — the sum, or that it was wrong.</summary>
        public Text VerdictText
        {
            get
            {
                EnsureInitialized();
                return _verdictText;
            }
        }

        /// <summary>`consequence` — one of the three sentences, and on a wrong answer the revealed equation before it.</summary>
        public Text ConsequenceText
        {
            get
            {
                EnsureInitialized();
                return _consequenceText;
            }
        }

        /// <summary>`chip`'s own rect, <see cref="ResultChipTop"/> below the panel's top edge.</summary>
        public RectTransform ChipRect
        {
            get
            {
                EnsureInitialized();
                return _chipRect;
            }
        }

        /// <summary>The shared Player chip, showing the move as `pad 3 → 4`.</summary>
        public PlayerChip Chip
        {
            get
            {
                EnsureInitialized();
                return _chip;
            }
        }

        /// <summary>`controls` — the one button, named for the next player.</summary>
        public Button NextTurnButton
        {
            get
            {
                EnsureInitialized();
                return _nextTurnButton;
            }
        }

        /// <summary>Which of the four steps the hand-off is in, or <see cref="AnswerResultHandOffStage.Waiting"/> before the press.</summary>
        public AnswerResultHandOffStage Stage
        {
            get
            {
                EnsureInitialized();

                if (!_handOffStarted)
                {
                    return AnswerResultHandOffStage.Waiting;
                }

                if (_handOffElapsed < HandOffFadeEnd)
                {
                    return AnswerResultHandOffStage.Closing;
                }

                if (_handOffElapsed < HandOffHopStart)
                {
                    return AnswerResultHandOffStage.Holding;
                }

                return _handOffElapsed < HandOffDuration
                    ? AnswerResultHandOffStage.Hopping
                    : AnswerResultHandOffStage.Complete;
            }
        }

        /// <summary>
        /// How far through the hop the frog is, 0 to 1 — zero for the whole of
        /// the close and the hold, so the frog is still standing where the
        /// sentence said it was until the moment it moves.
        /// </summary>
        public float HopProgress
        {
            get
            {
                EnsureInitialized();

                if (!_handOffStarted)
                {
                    return 0f;
                }

                return Mathf.Clamp01(
                    (_handOffElapsed - HandOffHopStart) / GameBoardScreenView.FrogHopDuration);
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            if (_turn == null)
            {
                return;
            }

            Advance(Time.deltaTime);
        }

        /// <summary>
        /// Points the dialog at the turn Core has already graded, at the board
        /// the frog will hop on, and at the router the dialog layer belongs to
        /// — then opens it.
        ///
        /// The board and the router are optional for the same reason
        /// <c>RollAndCardDialogView.Initialize</c>'s router is: a test that
        /// only cares what the dialog draws should not have to hand it a whole
        /// board and a navigation graph.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="turn"/> is null.</exception>
        public void Initialize(IAnswerResultTurn turn, GameBoardScreenView board = null, ScreenRouter router = null)
        {
            EnsureInitialized();

            _turn = turn ?? throw new ArgumentNullException(nameof(turn));
            _board = board;
            _router = router;

            _handOffStarted = false;
            _handedOff = false;
            _handOffElapsed = 0f;

            Refresh();
            _dialog.Open();
        }

        /// <summary>
        /// Re-reads the turn and redraws. Every value is asked for again — the
        /// outcome, the two positions, the correct answer and the next frog
        /// all come back from Core, and none of them is remembered from the
        /// last pass.
        ///
        /// It also puts the frog back where the sentence says it still is:
        /// Core moved it at grading time, so the board left to itself would
        /// already be drawing the move this dialog is about to describe.
        /// </summary>
        public void Refresh()
        {
            EnsureInitialized();

            if (_turn == null)
            {
                return;
            }

            var resolution = _turn.Resolution;
            var frog = _turn.Frog;
            var equation = Equation(resolution);
            var isRight = resolution.Outcome == TurnOutcome.Correct;

            // The mark. A filled disc versus a ringed circle — the shape is
            // what carries the verdict; the glyph reinforces it.
            _markRing.color = isRight ? AccentColor : WarningColor;
            _markFill.color = isRight ? AccentColor : PaperColor;
            _markGlyph.text = isRight ? TickGlyph : CrossGlyph;
            _markGlyph.color = isRight ? PaperColor : WarningColor;

            // The wrong state leads with the words, not the number: the first
            // thing a child reads should not be the size of their mistake.
            _verdictText.text = isRight ? equation : NotThisTimeLabel;

            var movement = string.Format(MovementFormatFor(resolution.Outcome), frog);
            _consequenceText.text = isRight
                ? string.Format(RightConsequenceFormat, movement)
                : string.Format(WrongConsequenceFormat, equation, movement);

            _chip.SetFrog(FrogColours.For(frog), frog.ToString());
            _chip.SetPadCount(string.Format(
                ChipMoveFormat,
                resolution.PositionBefore,
                resolution.PositionAfter));

            // Default, not Home: the Home chip is what the board draws on its
            // next ordinary render once the frog has landed there, and this
            // chip has a move to show in the line Home would replace.
            _chip.SetState(PlayerChipState.Default);

            // Null on the hop that got the last frog home, and only then —
            // see IAnswerResultTurn.NextFrog and NoNextPlayerLabel.
            var next = _turn.NextFrog;
            _nextTurnButton.SetLabelText(next.HasValue
                ? string.Format(NextTurnFormat, next.Value)
                : NoNextPlayerLabel);

            PlaceFrog();
        }

        /// <summary>
        /// Advances the dialog's fade and, once the button has been pressed,
        /// the hand-off sequence, by <paramref name="deltaSeconds"/>. A public
        /// method of its own, rather than reachable only through
        /// <see cref="Update"/>, so an EditMode test can drive the four steps
        /// on a clock it controls — the same reasoning as
        /// <c>RollAndCardDialogView.Advance</c>.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            EnsureInitialized();

            var delta = Mathf.Max(deltaSeconds, 0f);
            _dialog.AdvanceFade(delta);

            if (!_handOffStarted)
            {
                return;
            }

            _handOffElapsed = Mathf.Clamp(_handOffElapsed + delta, 0f, HandOffDuration);
            ApplyHandOff();
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and WorkingOutGridView's own
        // EnsureInitialized.
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

            // Its own named size, comfortably inside the shared Dialog's
            // maxima, and identical in both states.
            _dialog.SetSize(ResultDialogWidth, ResultDialogHeight);

            BuildMark();
            BuildText();
            BuildChip();
            BuildControls();
        }

        void BuildMark()
        {
            var markGO = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            _markRing = markGO.GetComponent<Image>();
            _markRing.sprite = MarkSprite;
            _markRing.type = Image.Type.Sliced;
            _markRing.raycastTarget = false;

            _markRect = _markRing.rectTransform;
            _markRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _markRect.anchorMin = new Vector2(0f, 1f);
            _markRect.anchorMax = new Vector2(0f, 1f);
            _markRect.pivot = new Vector2(0f, 1f);
            _markRect.sizeDelta = new Vector2(ResultMarkSize, ResultMarkSize);
            _markRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, -DialogPanel.DialogPadding);

            var fillGO = new GameObject("MarkFill", typeof(RectTransform), typeof(Image));
            _markFill = fillGO.GetComponent<Image>();
            _markFill.sprite = MarkFillSprite;
            _markFill.type = Image.Type.Sliced;
            _markFill.raycastTarget = false;

            var fillRect = _markFill.rectTransform;
            fillRect.SetParent(_markRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(ResultMarkRingWidth, ResultMarkRingWidth);
            fillRect.offsetMax = new Vector2(-ResultMarkRingWidth, -ResultMarkRingWidth);

            _markGlyph = BuildLabel("MarkGlyph", _markRect, ResultMarkGlyphSize, InkColor, TextAnchor.MiddleCenter);
            StretchToFill(_markGlyph.rectTransform);
        }

        void BuildText()
        {
            // verdict and consequence sit to the right of the mark, in a
            // column — both pinned to the panel's own top-left corner, so
            // neither moves when the other's content changes length.
            _verdictText = BuildLabel("Verdict", _dialog.PanelRect, ResultVerdictSize, InkColor, TextAnchor.UpperLeft);
            _verdictText.fontStyle = FontStyle.Bold;
            PlaceInTextColumn(_verdictText.rectTransform, ResultVerdictTop, ResultVerdictSize);

            _consequenceText = BuildLabel(
                "Consequence",
                _dialog.PanelRect,
                ResultConsequenceSize,
                LineColor,
                TextAnchor.UpperLeft);

            // The only wrapped text on the dialog: the wrong state's
            // consequence is an equation and a sentence, and it wraps inside
            // the column rather than running past it.
            _consequenceText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceInTextColumn(
                _consequenceText.rectTransform,
                ResultConsequenceTop,
                ResultConsequenceSize * ConsequenceLines);
        }

        static void PlaceInTextColumn(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(ResultTextWidth, height);
            rect.anchoredPosition = new Vector2(ResultTextLeft, -top);
        }

        void BuildChip()
        {
            var chipGO = new GameObject("Chip", typeof(RectTransform));
            _chipRect = (RectTransform)chipGO.transform;
            _chipRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _chipRect.anchorMin = new Vector2(0f, 1f);
            _chipRect.anchorMax = new Vector2(0f, 1f);
            _chipRect.pivot = new Vector2(0f, 1f);
            _chipRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, -ResultChipTop);

            _chip = chipGO.AddComponent<PlayerChip>();

            // Unity does not run Awake() on AddComponent outside play mode,
            // and the chip sizes itself when it builds. Touch its rect so it
            // builds now, while the panel is being laid out.
            _chipRect = _chip.RectTransform;
        }

        void BuildControls()
        {
            // A plain primary Button at the shared component's own size, added
            // through the shared Dialog so it lands in the button row,
            // bottom-right — nothing here overrides ButtonHeight or
            // ButtonMinWidth. Its label is set on every Refresh, because the
            // next frog is a fact about the game, not about this button.
            //
            // It is deliberately *not* nominated as this dialog's least
            // destructive button: that is the value the router would invoke on
            // hardware back, and back is inert here.
            _nextTurnButton = _dialog.AddButton(ButtonKind.Primary, string.Empty, HandleNextTurnClicked);
        }

        void HandleNextTurnClicked()
        {
            // The only control on the dialog, and it can only ever run once —
            // a second press while the frog is mid-hop must not restart the
            // sequence or hand the turn on twice.
            if (_handOffStarted)
            {
                return;
            }

            _handOffStarted = true;
            _handOffElapsed = 0f;

            // Core's own first hand-off step: the dialog closes and the board
            // is back on screen for the hop.
            _turn.BeginHandOff();

            // The shared Dialog's fade, already built by #219 — not a second
            // close animation.
            _dialog.Close();

            ApplyHandOff();
        }

        void ApplyHandOff()
        {
            if (_handedOff)
            {
                return;
            }

            PlaceFrog();

            if (_handOffElapsed < HandOffDuration)
            {
                return;
            }

            _handedOff = true;

            // The frog has landed. Only now does the turn pass, and only now
            // does the board go back to drawing itself from Core — which is
            // also what switches a chip to `Home` if that hop was the one that
            // got a frog there. There is no second code path here for landing
            // on the End log; letting the hop finish is the whole of it.
            _turn.CompleteHandOff();

            if (_board != null)
            {
                _board.NotifyTurnResolved();
            }

            // The dialog layer is cleared last. Clearing it deactivates this
            // view's root (ScreenRouterAdapter), which would stop the sequence
            // mid-hop if it happened when the panel faded out — and by now the
            // panel has been fully transparent since the end of step one, so
            // what a player sees is still the spec's order.
            if (_router != null)
            {
                _router.CloseDialog();
            }

            var handler = TurnHandedOff;
            if (handler != null)
            {
                handler();
            }
        }

        // The frog, drawn between the two positions Core reported, at whatever
        // point of the hop this is. The endpoints are the board's own
        // placement for those two lane positions — this view does not work out
        // a second formula for where a lane position sits on screen.
        void PlaceFrog()
        {
            if (_board == null || _turn == null)
            {
                return;
            }

            var resolution = _turn.Resolution;

            _board.LaneFor(_turn.Frog).PlacePiecePartWay(
                resolution.PositionBefore,
                resolution.PositionAfter,
                HopProgress);
        }

        string Equation(TurnResolution resolution)
        {
            // `13,571`, the way both mockups write it — grouped in the
            // invariant culture, so the separator is the one the mockups draw
            // rather than whatever the tablet's locale prefers.
            return string.Format(
                CultureInfo.InvariantCulture,
                EquationFormat,
                _turn.Multiplicand,
                _turn.Multiplier,
                resolution.CorrectAnswer.ToString("N0", CultureInfo.InvariantCulture));
        }

        // One of docs/specs/ui/answer-result.md's three sentences, chosen by
        // the outcome Core reported. Three, not two: the third is the floor
        // rule, and a wrong answer on the Start log moves nothing and says so.
        static string MovementFormatFor(TurnOutcome outcome)
        {
            switch (outcome)
            {
                case TurnOutcome.Correct:
                    return ForwardSentenceFormat;

                case TurnOutcome.WrongAboveStartLog:
                    return RetreatSentenceFormat;

                case TurnOutcome.WrongOnStartLog:
                    return StaysSentenceFormat;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unhandled outcome.");
            }
        }

        static Text BuildLabel(string name, RectTransform parent, float size, Color colour, TextAnchor alignment)
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

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
