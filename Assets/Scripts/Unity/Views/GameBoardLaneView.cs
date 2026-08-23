using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
using BoardColours = Frogs.Unity.UI.BoardColours;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// One frog's lane on the board — docs/specs/ui/game-board.md's `chip`,
    /// `track` and `piece`, built to that page's per-lane constants table.
    /// The track is this lane's **seven lily pads**, drawn left to right
    /// (Derek's settled call — see the page's "why the lanes run across"); the
    /// chip is the shared <see cref="PlayerChip"/> (#219) in this lane's
    /// gutter.
    ///
    /// A lane is still nine positions, and it still holds a rect for every one
    /// of them. Two of those rects — position 0 and
    /// <see cref="Lane.LaneWinningPosition"/> — draw nothing at all: they are
    /// where this lane crosses the Start log and the End log, and those are one
    /// drawing each for the whole pond, owned by
    /// <see cref="GameBoardScreenView"/> (#296). A four-frog game draws two
    /// logs, not eight. What the lane keeps is the *place*, so its piece sits
    /// on its own lane's centre line on a log it shares with nobody's position
    /// but its own.
    ///
    /// It reads, and never computes. Where the frog sits and whether it is
    /// home come straight off the <see cref="Lane"/> it is handed on every
    /// <see cref="Render"/>: this type places the piece **onto the track
    /// element it already drew** for that index rather than deriving a pixel
    /// offset of its own, and it never counts answers to arrive at either
    /// number in the chip's `3 of 8`.
    ///
    /// Nothing here moves *itself*. The frog is drawn at rest at whatever
    /// position Core reports, and this type holds no clock and no tween;
    /// <see cref="PlacePiecePartWay"/> draws one frame of the hop that
    /// <c>answer-result</c> (#224) owns and drives, using the same placement
    /// the at-rest render already uses for its two endpoints.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameBoardLaneView : MonoBehaviour
    {
        // docs/specs/ui/game-board.md#named-constants — the lane's table.
        public const float LaneHeight = 184f;
        public const float LilyPadDiameter = 112f;
        public const float FrogPieceDiameter = 88f;
        public const float FrogPieceOutline = 4f;
        public const float TrackOutline = 3f;
        public const float LaneGutterWidth = 256f;
        public const float LaneGutterGap = 48f;

        // `LanePositionGap` is not here, and that is the point of #408: it is
        // the one number on game-board.md that the screen decides, so it is
        // derived — see LanePositionGapFor. These three are what it is derived
        // from, and all three are that page's own rows.

        /// <summary>The smallest the gap between two positions may be — the floor a screen narrower than the reference canvas gets.</summary>
        public const float LanePositionGapMin = 48f;

        /// <summary>
        /// Everything on the row that does not stretch: two safe margins, the
        /// chip gutter and the gap after it, the two logs' columns and the
        /// seven lily pads.
        ///
        /// Written as that sum rather than as the bare 1536 px, because it is
        /// load-bearing — the elastic gap is computed against it, so a change
        /// to <see cref="LaneGutterWidth"/>,
        /// <see cref="GameBoardScreenView.LogWidth"/>,
        /// <see cref="LilyPadDiameter"/>, <see cref="LaneGutterGap"/> or
        /// <see cref="GameBoardScreenView.SafeMargin"/> has to move it, and
        /// nothing else has to move at all: the gap absorbs the difference by
        /// construction.
        /// </summary>
        public const float LaneFixedWidth = (2f * GameBoardScreenView.SafeMargin)
            + LaneGutterWidth
            + LaneGutterGap
            + (2f * GameBoardScreenView.LogWidth)
            + ((Lane.LanePositionCount - 2) * LilyPadDiameter);

        /// <summary>
        /// How many gaps the width left over from <see cref="LaneFixedWidth"/>
        /// is divided between: one either side of the seven pads, six among
        /// them — which is the spaces between the lane's nine positions.
        /// </summary>
        public const float LanePositionGapCount = Lane.LanePositionCount - 1;

        // `LanePositionCount` (9) and `LaneWinningPosition` (8) are the other
        // two rows of that same table, and they are not this screen's to
        // define: they are Frogs.Core.Lane's own constants, reused here under
        // the identical name so the board's nine-position track and Core's
        // nine-position lane can never disagree. Referenced, never
        // redeclared — see Lane.LanePositionCount / Lane.LaneWinningPosition
        // throughout this file.

        // The chip's progress line — docs/specs/ui/game-board.md's Elements
        // section: "Pad count — on the chip: `3 of 8`". The numerator is
        // whatever Lane reports; the denominator is Lane.LaneWinningPosition.
        const string PadCountFormat = "{0} of {1}";

        // `LogWidth`, `SharedLogHeight` and `LogRadius` are game-board.md's
        // third table, and they are not this type's either. The logs belong to
        // the pond, so they are GameBoardScreenView's — referenced here, under
        // the identical names, because the track's own width arithmetic still
        // has to reserve the column each log stands in.

        // The track's colours are docs/specs/ui/game-board.md § Colours,
        // received by name from BoardColours exactly as the geometry above is
        // received from that page's constants table. They used to be private
        // hex values here, copied out of the mockup's CSS; issue #291 gave
        // them a home on the spec page and this file the same relationship to
        // them it already had to every other number on this screen.

        static Sprite s_lilyPadSprite;
        static Sprite s_lilyPadFillSprite;
        static Sprite s_pieceSprite;
        static Sprite s_pieceFillSprite;

        // Every outline is the element's own image with its fill inset inside
        // it — the same two-image shape PlayerChip uses for its active ring.
        // Each rounded shape gets a sprite generated at its own radius rather
        // than one sprite stretched to two sizes, so the inset one keeps its
        // curve instead of squaring off.
        static Sprite LilyPadSprite
        {
            get
            {
                if (s_lilyPadSprite == null)
                {
                    // A rounded rect whose radius is half its own size is a
                    // circle — the same shape the Player chip's swatch uses,
                    // rather than a second way of drawing one.
                    s_lilyPadSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(LilyPadDiameter / 2f));
                }

                return s_lilyPadSprite;
            }
        }

        static Sprite LilyPadFillSprite
        {
            get
            {
                if (s_lilyPadFillSprite == null)
                {
                    s_lilyPadFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt((LilyPadDiameter - (2f * TrackOutline)) / 2f));
                }

                return s_lilyPadFillSprite;
            }
        }

        static Sprite PieceSprite
        {
            get
            {
                if (s_pieceSprite == null)
                {
                    s_pieceSprite = RoundedRectSprite.CreateRoundedRect(Mathf.RoundToInt(FrogPieceDiameter / 2f));
                }

                return s_pieceSprite;
            }
        }

        static Sprite PieceFillSprite
        {
            get
            {
                if (s_pieceFillSprite == null)
                {
                    s_pieceFillSprite = RoundedRectSprite.CreateRoundedRect(
                        Mathf.RoundToInt((FrogPieceDiameter - (2f * FrogPieceOutline)) / 2f));
                }

                return s_pieceFillSprite;
            }
        }

        /// <summary>
        /// The gap between two positions on a lane drawn on a screen this
        /// wide — game-board.md's Anchors section, and the one number on that
        /// page a screen is allowed to decide:
        ///
        /// <code>
        /// LanePositionGap = max( LanePositionGapMin, (screen width - LaneFixedWidth) / LanePositionGapCount )
        /// </code>
        ///
        /// **At exactly 1920 px this gives exactly 48 px**, which is what
        /// `LanePositionGap` was typed as before #408. That is not a
        /// coincidence to be grateful for — it is the condition the rule was
        /// chosen to satisfy, so that the reference canvas keeps being a
        /// picture of the game.
        ///
        /// The floor is what a *narrower* screen gets: below the reference
        /// width the formula would start closing the pads up, so it stops and
        /// the board keeps its reference row.
        /// </summary>
        public static float LanePositionGapFor(float screenWidth)
        {
            return Mathf.Max(
                LanePositionGapMin,
                (screenWidth - LaneFixedWidth) / LanePositionGapCount);
        }

        /// <summary>
        /// The track's width on a screen this wide: two log columns, seven
        /// lily pads and eight gaps — the page's own arithmetic, written out
        /// rather than trusted as a literal. Only the gaps move with the
        /// screen; sharing the logs did not disturb this and neither does
        /// spreading it, because a shared log stands in the same column its
        /// per-lane predecessor did.
        /// </summary>
        public static float TrackWidthFor(float screenWidth)
        {
            return (2f * GameBoardScreenView.LogWidth)
                + ((Lane.LanePositionCount - 2) * LilyPadDiameter)
                + ((Lane.LanePositionCount - 1) * LanePositionGapFor(screenWidth));
        }

        /// <summary>
        /// The centre of one track position, measured from the track's left
        /// edge, on a screen this wide. The Start log's column at index 0,
        /// seven lily pads, the End log's column at
        /// <see cref="Lane.LaneWinningPosition"/>.
        /// </summary>
        public static float PositionCenterXFor(int position, float screenWidth)
        {
            if (position <= 0)
            {
                return GameBoardScreenView.LogWidth / 2f;
            }

            if (position >= Lane.LaneWinningPosition)
            {
                return TrackWidthFor(screenWidth) - (GameBoardScreenView.LogWidth / 2f);
            }

            var gap = LanePositionGapFor(screenWidth);

            return GameBoardScreenView.LogWidth
                + gap
                + ((position - 1) * (LilyPadDiameter + gap))
                + (LilyPadDiameter / 2f);
        }

        RectTransform _rect;
        PlayerChip _chip;
        RectTransform _trackRect;
        readonly List<RectTransform> _positionRects = new List<RectTransform>();
        readonly List<Image> _lilyPadFills = new List<Image>();
        readonly List<Image> _lilyPadOutlines = new List<Image>();
        RectTransform _pieceRect;
        Image _pieceOutline;
        Image _piece;

        bool _initialized;
        FrogColour _colour;
        int _renderedPosition;
        float _screenWidth;

        /// <summary>
        /// The screen this lane was laid out for, and so the width its nine
        /// positions are spread across.
        ///
        /// It is read off the lane's own rect rather than passed in, because
        /// in play mode a lane builds itself the moment the component is
        /// added and there is no call between those two points to hand it a
        /// number. A lane spans the safe area — the chip against the real left
        /// margin, the End log against the real right one — so the screen is
        /// the lane's own width plus the two margins.
        /// </summary>
        public float ScreenWidth
        {
            get
            {
                EnsureInitialized();
                return _screenWidth;
            }
        }

        /// <summary>
        /// The gap between two of this lane's positions —
        /// <see cref="LanePositionGapFor"/> at the width this lane was laid
        /// out for. 48 px on the reference canvas, 128 px at 2560.
        /// </summary>
        public float LanePositionGap
        {
            get { return LanePositionGapFor(ScreenWidth); }
        }

        /// <summary>This lane's track width — <see cref="TrackWidthFor"/> at the width it was laid out for.</summary>
        public float TrackWidth
        {
            get { return TrackWidthFor(ScreenWidth); }
        }

        /// <summary>
        /// The centre of one of this lane's track positions, measured from the
        /// track's left edge — <see cref="PositionCenterXFor"/> at the width
        /// this lane was laid out for.
        /// </summary>
        public float PositionCenterX(int position)
        {
            return PositionCenterXFor(position, ScreenWidth);
        }

        /// <summary>Which frog's lane this is.</summary>
        public FrogColour Colour
        {
            get
            {
                EnsureInitialized();
                return _colour;
            }
        }

        /// <summary>The lane's own band — <see cref="LaneHeight"/> tall.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>`chip` — the shared Player chip, pinned to the left of the lane's gutter.</summary>
        public PlayerChip Chip
        {
            get
            {
                EnsureInitialized();
                return _chip;
            }
        }

        /// <summary>`track` — pinned to the right, <see cref="TrackWidth"/> wide at the screen this lane was laid out for.</summary>
        public RectTransform TrackRect
        {
            get
            {
                EnsureInitialized();
                return _trackRect;
            }
        }

        /// <summary>
        /// The nine track positions, indexed by <see cref="Lane.Position"/>.
        /// The first and last draw nothing — they are where this lane crosses
        /// the pond's two shared logs — and the seven between them are this
        /// lane's own lily pads.
        /// </summary>
        public IReadOnlyList<RectTransform> PositionRects
        {
            get
            {
                EnsureInitialized();
                return _positionRects;
            }
        }

        /// <summary>
        /// The seven lily pads' fills — positions 1 to 7, in order, and the
        /// whole of what this lane draws for itself. Every one is drawn
        /// identically: "the pad a frog is on is drawn no differently from the
        /// others; the frog on it is the marker."
        /// </summary>
        public IReadOnlyList<Image> LilyPadFills
        {
            get
            {
                EnsureInitialized();
                return _lilyPadFills;
            }
        }

        /// <summary>
        /// The seven lily pads' outlines — <see cref="TrackOutline"/> thick,
        /// drawn inside each pad's own bounds so the track's width is exactly
        /// the sum the spec's arithmetic gives.
        /// </summary>
        public IReadOnlyList<Image> LilyPadOutlines
        {
            get
            {
                EnsureInitialized();
                return _lilyPadOutlines;
            }
        }

        /// <summary>
        /// `piece` — the frog itself. This is the transform that gets
        /// re-parented onto whichever track element Core reports, so its
        /// parent is the answer to "which position is the frog on".
        /// </summary>
        public RectTransform PieceRect
        {
            get
            {
                EnsureInitialized();
                return _pieceRect;
            }
        }

        /// <summary>The piece's outline — <see cref="FrogPieceOutline"/> thick.</summary>
        public Image PieceOutline
        {
            get
            {
                EnsureInitialized();
                return _pieceOutline;
            }
        }

        /// <summary>The piece's fill — a circle in the frog's colour.</summary>
        public Image Piece
        {
            get
            {
                EnsureInitialized();
                return _piece;
            }
        }

        /// <summary>The position index the piece was last drawn on — whatever <see cref="Lane"/> reported.</summary>
        public int RenderedPosition
        {
            get
            {
                EnsureInitialized();
                return _renderedPosition;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Sets which frog this lane belongs to and what its player is
        /// called, and paints it. The name comes in rather than being
        /// derived from the colour, because a renamed frog's lane has to
        /// say the typed name.
        /// </summary>
        public void Initialize(FrogColour colour, string name = null)
        {
            EnsureInitialized();

            _colour = colour;
            _chip.SetFrog(FrogColours.For(colour), name ?? PlayerName.DefaultFor(colour));
            _piece.color = FrogColours.For(colour);
        }

        /// <summary>
        /// Draws this lane from <paramref name="lane"/> and nothing else: the
        /// piece onto the track element Core's position names, the chip's pad
        /// count from that same position over
        /// <see cref="Lane.LaneWinningPosition"/>, and the chip's state from
        /// whether Core says the frog is home.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="lane"/> is null.</exception>
        public void Render(Lane lane, bool isActive)
        {
            if (lane == null)
            {
                throw new ArgumentNullException(nameof(lane));
            }

            EnsureInitialized();

            var position = lane.Position;
            _renderedPosition = position;

            // Placed onto the element the track already holds for that index
            // — never at an offset this type worked out for itself.
            _pieceRect.SetParent(_positionRects[position], worldPositionStays: false);
            _pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            _pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            _pieceRect.pivot = new Vector2(0.5f, 0.5f);
            _pieceRect.anchoredPosition = Vector2.zero;

            _chip.SetPadCount(string.Format(PadCountFormat, position, Lane.LaneWinningPosition));

            if (lane.IsHome)
            {
                _chip.SetState(PlayerChipState.Home);
                return;
            }

            _chip.SetState(isActive ? PlayerChipState.Active : PlayerChipState.Default);
        }

        /// <summary>
        /// Places the piece part-way from one lane position to another, for
        /// the hop <c>answer-result</c> (#224) plays once its dialog closes.
        /// <paramref name="progress"/> 0 puts the frog exactly where
        /// <see cref="Render"/> puts a resting one on
        /// <paramref name="from"/>, and 1 exactly where it puts one on
        /// <paramref name="to"/> — both endpoints are
        /// <see cref="PositionCenterX"/>, the same placement the at-rest
        /// render already uses, so no second formula for where a lane position
        /// sits on screen exists anywhere.
        ///
        /// This lane still decides nothing and animates nothing: it holds no
        /// clock and no tween, and only draws the frame it is asked for. (It
        /// is deliberately not *named* for one either — #220's
        /// <c>Board_HasNoHopAnimation_AndNoEndOfGameDetection</c> greps both
        /// board types for words like that, and a method called
        /// `…Between` trips it on the four letters in the middle. The guard is
        /// right to be that blunt, so this is named around it.) When the hop
        /// is over, the next ordinary <see cref="Render"/> parents the piece
        /// back onto the track element for Core's position — which, at
        /// <paramref name="progress"/> 1, is the pixel it is already on.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="from"/> or <paramref name="to"/> is not a lane position.
        /// </exception>
        public void PlacePiecePartWay(int from, int to, float progress)
        {
            EnsureInitialized();

            RequirePosition(from, nameof(from));
            RequirePosition(to, nameof(to));

            // Parented to the track rather than to a position element, because
            // between two positions is not on either of them. The track is
            // what both positions are measured against, so the two endpoints
            // are the same numbers the elements themselves were placed at.
            _pieceRect.SetParent(_trackRect, worldPositionStays: false);
            _pieceRect.anchorMin = new Vector2(0f, 0.5f);
            _pieceRect.anchorMax = new Vector2(0f, 0.5f);
            _pieceRect.pivot = new Vector2(0.5f, 0.5f);
            _pieceRect.anchoredPosition = new Vector2(
                Mathf.Lerp(PositionCenterX(from), PositionCenterX(to), Mathf.Clamp01(progress)),
                0f);
        }

        static void RequirePosition(int position, string name)
        {
            if (position < 0 || position >= Lane.LanePositionCount)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    position,
                    $"a lane has {Lane.LanePositionCount} positions; {position} is not one of them.");
            }
        }

        // Unity does not guarantee Awake() has run before another
        // component's Awake() or a caller reaches this one right after
        // AddComponent — the same reasoning as Button, PlayerChip and
        // GameSetupScreenView's own EnsureInitialized.
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

            // The width everything below is spread across — see ScreenWidth.
            // A lane built with no width to speak of (one standing on its own
            // in a test, rather than in a pond) falls through the formula's
            // own floor and draws the reference row, which is the same answer
            // a screen narrower than the reference gets.
            _screenWidth = _rect.rect.width + (2f * GameBoardScreenView.SafeMargin);

            BuildChip();
            BuildTrack();
            BuildPiece();
        }

        void BuildChip()
        {
            // chip — pinned to the left of the lane, filling the gutter.
            var chipGO = new GameObject("Chip", typeof(RectTransform));
            var chipRect = (RectTransform)chipGO.transform;
            chipRect.SetParent(_rect, worldPositionStays: false);
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.pivot = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = Vector2.zero;

            _chip = chipGO.AddComponent<PlayerChip>();

            // The Player chip is exactly LaneGutterWidth wide — the gutter is
            // sized for it — but say so rather than assume it.
            chipRect.sizeDelta = new Vector2(LaneGutterWidth, PlayerChip.PlayerChipHeight);
        }

        void BuildTrack()
        {
            // track — pinned to the right, its width fixed by its nine
            // positions rather than by the space available. It is the lane's
            // full height now that both of its ends are drawn by the pond: the
            // rects standing in the two shared logs' columns are as tall as
            // the lane, because the log they mark a place on runs past this
            // lane in both directions.
            var trackGO = new GameObject("Track", typeof(RectTransform));
            _trackRect = (RectTransform)trackGO.transform;
            _trackRect.SetParent(_rect, worldPositionStays: false);
            _trackRect.anchorMin = new Vector2(1f, 0.5f);
            _trackRect.anchorMax = new Vector2(1f, 0.5f);
            _trackRect.pivot = new Vector2(1f, 0.5f);
            _trackRect.sizeDelta = new Vector2(TrackWidth, LaneHeight);
            _trackRect.anchoredPosition = Vector2.zero;

            for (var position = 0; position < Lane.LanePositionCount; position++)
            {
                var onSharedLog = position == 0 || position == Lane.LaneWinningPosition;

                var positionRect = onSharedLog
                    ? BuildSharedLogPosition(position)
                    : BuildLilyPad(position);

                positionRect.SetParent(_trackRect, worldPositionStays: false);
                positionRect.anchorMin = new Vector2(0f, 0.5f);
                positionRect.anchorMax = new Vector2(0f, 0.5f);
                positionRect.pivot = new Vector2(0.5f, 0.5f);
                positionRect.anchoredPosition = new Vector2(PositionCenterX(position), 0f);

                _positionRects.Add(positionRect);
            }
        }

        // Where this lane crosses one of the pond's two shared logs. It draws
        // nothing — the log is one drawing for the whole board, and
        // GameBoardScreenView owns it — and exists so the piece has this
        // lane's own place on that log to sit at: the log's column, on this
        // lane's centre line.
        RectTransform BuildSharedLogPosition(int position)
        {
            var positionName = position == 0 ? "StartPosition" : "EndPosition";

            var positionGO = new GameObject(positionName, typeof(RectTransform));
            var positionRect = (RectTransform)positionGO.transform;
            positionRect.sizeDelta = new Vector2(GameBoardScreenView.LogWidth, LaneHeight);

            return positionRect;
        }

        RectTransform BuildLilyPad(int position)
        {
            var padGO = new GameObject("LilyPad" + position, typeof(RectTransform), typeof(Image));
            var outline = padGO.GetComponent<Image>();
            outline.sprite = LilyPadSprite;
            outline.type = Image.Type.Sliced;
            outline.color = BoardColours.LilyPadEdge;
            outline.raycastTarget = false;

            var padRect = outline.rectTransform;
            padRect.sizeDelta = new Vector2(LilyPadDiameter, LilyPadDiameter);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = LilyPadFillSprite;
            fill.type = Image.Type.Sliced;
            fill.color = BoardColours.LilyPadGreen;
            fill.raycastTarget = false;

            var fillRect = fill.rectTransform;
            fillRect.SetParent(padRect, worldPositionStays: false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(TrackOutline, TrackOutline);
            fillRect.offsetMax = new Vector2(-TrackOutline, -TrackOutline);

            _lilyPadOutlines.Add(outline);
            _lilyPadFills.Add(fill);

            return padRect;
        }

        void BuildPiece()
        {
            // piece — a flat-coloured circle in the frog's colour inside its
            // own outline, per game-board.md's Shape-only note and the
            // committed mockup's `.frog` ring. It starts on the Start log and
            // is moved onto whichever position Core reports on the first
            // Render.
            var pieceGO = new GameObject("Piece", typeof(RectTransform), typeof(Image));
            _pieceOutline = pieceGO.GetComponent<Image>();
            _pieceOutline.sprite = PieceSprite;
            _pieceOutline.type = Image.Type.Sliced;
            _pieceOutline.color = BoardColours.PieceEdge;
            _pieceOutline.raycastTarget = false;

            _pieceRect = _pieceOutline.rectTransform;
            _pieceRect.SetParent(_positionRects[0], worldPositionStays: false);
            _pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            _pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            _pieceRect.pivot = new Vector2(0.5f, 0.5f);
            _pieceRect.sizeDelta = new Vector2(FrogPieceDiameter, FrogPieceDiameter);
            _pieceRect.anchoredPosition = Vector2.zero;

            var pieceFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _piece = pieceFillGO.GetComponent<Image>();
            _piece.sprite = PieceFillSprite;
            _piece.type = Image.Type.Sliced;
            _piece.raycastTarget = false;

            var pieceFillRect = _piece.rectTransform;
            pieceFillRect.SetParent(_pieceRect, worldPositionStays: false);
            pieceFillRect.anchorMin = Vector2.zero;
            pieceFillRect.anchorMax = Vector2.one;
            pieceFillRect.offsetMin = new Vector2(FrogPieceOutline, FrogPieceOutline);
            pieceFillRect.offsetMax = new Vector2(-FrogPieceOutline, -FrogPieceOutline);
        }
    }
}
