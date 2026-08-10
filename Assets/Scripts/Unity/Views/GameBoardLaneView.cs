using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using UnityEngine.UI;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;
using RoundedRectSprite = Frogs.Unity.UI.RoundedRectSprite;
using SharedButton = Frogs.Unity.UI.Button;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// One frog's lane on the board — docs/specs/ui/game-board.md's `chip`,
    /// `track` and `piece`, built to that page's per-lane constants table.
    /// The track is the Start log, seven lily pads and the End log, drawn
    /// left to right (Derek's settled call — see the page's "why the lanes
    /// run across"); the chip is the shared
    /// <see cref="PlayerChip"/> (#219) in this lane's gutter.
    ///
    /// It reads, and never computes. Where the frog sits and whether it is
    /// home come straight off the <see cref="Lane"/> it is handed on every
    /// <see cref="Render"/>: this type places the piece **onto the track
    /// element it already drew** for that index rather than deriving a pixel
    /// offset of its own, and it never counts answers to arrive at either
    /// number in the chip's `3 of 8`.
    ///
    /// Nothing here moves. The frog is drawn at rest at whatever position
    /// Core reports; animating the change between one position and the next
    /// belongs to <c>answer-result</c> (#224), which is what closes and
    /// starts the hop.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameBoardLaneView : MonoBehaviour
    {
        // docs/specs/ui/game-board.md#named-constants — the lane's table.
        public const float LaneHeight = 184f;
        public const float LilyPadDiameter = 112f;
        public const float FrogPieceDiameter = 88f;
        public const float LogWidth = 176f;
        public const float LogHeight = 120f;
        public const float LanePositionGap = 48f;
        public const float LaneGutterWidth = 256f;
        public const float LaneGutterGap = 48f;

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

        // A log is "a flat rounded rectangle" (game-board.md's Shape-only
        // note). The committed mockup draws it with the same 24 px corner the
        // shared Button uses, so this names shared-components.md's own
        // `ButtonRadius` rather than introducing a nineteenth number that
        // game-board.md's tables do not have — see this issue's PR.
        static readonly int LogCornerRadius = Mathf.RoundToInt(SharedButton.ButtonRadius);

        // Chrome colours copied verbatim from the committed mockup
        // (docs/specs/ui/mockups/game-board.html) — the same line Button.cs,
        // PlayerChip.cs and GameSetupScreenView.cs each draw for their own
        // colours: not a geometry constant on any spec page's table, so not
        // declared as a named spec constant.
        static readonly Color LilyPadColor = new Color32(0xCF, 0xE0, 0xD2, 0xFF); // mockup's .pad fill
        static readonly Color LogColor = new Color32(0xE0, 0xD4, 0xC3, 0xFF); // mockup's .log fill

        static Sprite s_logSprite;
        static Sprite s_lilyPadSprite;
        static Sprite s_pieceSprite;

        static Sprite LogSprite
        {
            get
            {
                if (s_logSprite == null)
                {
                    s_logSprite = RoundedRectSprite.CreateRoundedRect(LogCornerRadius);
                }

                return s_logSprite;
            }
        }

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

        /// <summary>
        /// The track's width, fixed by its nine positions rather than by the
        /// space available — game-board.md's Anchors section. Two logs, seven
        /// lily pads and eight gaps: the page's own arithmetic, written out
        /// rather than trusted as the literal 1520.
        /// </summary>
        public static float TrackWidth
        {
            get
            {
                return (2f * LogWidth)
                    + ((Lane.LanePositionCount - 2) * LilyPadDiameter)
                    + ((Lane.LanePositionCount - 1) * LanePositionGap);
            }
        }

        /// <summary>
        /// The centre of one track position, measured from the track's left
        /// edge. The Start log at index 0, seven lily pads, the End log at
        /// <see cref="Lane.LaneWinningPosition"/>.
        /// </summary>
        public static float PositionCenterX(int position)
        {
            if (position <= 0)
            {
                return LogWidth / 2f;
            }

            if (position >= Lane.LaneWinningPosition)
            {
                return TrackWidth - (LogWidth / 2f);
            }

            return LogWidth
                + LanePositionGap
                + ((position - 1) * (LilyPadDiameter + LanePositionGap))
                + (LilyPadDiameter / 2f);
        }

        RectTransform _rect;
        PlayerChip _chip;
        RectTransform _trackRect;
        readonly List<RectTransform> _positionRects = new List<RectTransform>();
        readonly List<Image> _positionImages = new List<Image>();
        Image _piece;

        bool _initialized;
        FrogColour _colour;
        int _renderedPosition;

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

        /// <summary>`track` — pinned to the right, <see cref="TrackWidth"/> wide.</summary>
        public RectTransform TrackRect
        {
            get
            {
                EnsureInitialized();
                return _trackRect;
            }
        }

        /// <summary>The nine track positions, Start log first, End log last.</summary>
        public IReadOnlyList<RectTransform> PositionRects
        {
            get
            {
                EnsureInitialized();
                return _positionRects;
            }
        }

        /// <summary>
        /// The nine positions' fills. Every lily pad is drawn identically —
        /// "the pad a frog is on is drawn no differently from the others; the
        /// frog on it is the marker."
        /// </summary>
        public IReadOnlyList<Image> PositionImages
        {
            get
            {
                EnsureInitialized();
                return _positionImages;
            }
        }

        /// <summary>`piece` — a flat circle in the frog's colour, sitting on its current position.</summary>
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

        /// <summary>Sets which frog this lane belongs to, and paints it.</summary>
        public void Initialize(FrogColour colour)
        {
            EnsureInitialized();

            _colour = colour;
            _chip.SetFrog(FrogColours.For(colour), colour.ToString());
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
            var pieceRect = _piece.rectTransform;
            pieceRect.SetParent(_positionRects[position], worldPositionStays: false);
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.anchoredPosition = Vector2.zero;

            _chip.SetPadCount(string.Format(PadCountFormat, position, Lane.LaneWinningPosition));

            if (lane.IsHome)
            {
                _chip.SetState(PlayerChipState.Home);
                return;
            }

            _chip.SetState(isActive ? PlayerChipState.Active : PlayerChipState.Default);
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
            // positions rather than by the space available.
            var trackGO = new GameObject("Track", typeof(RectTransform));
            _trackRect = (RectTransform)trackGO.transform;
            _trackRect.SetParent(_rect, worldPositionStays: false);
            _trackRect.anchorMin = new Vector2(1f, 0.5f);
            _trackRect.anchorMax = new Vector2(1f, 0.5f);
            _trackRect.pivot = new Vector2(1f, 0.5f);
            _trackRect.sizeDelta = new Vector2(TrackWidth, LogHeight);
            _trackRect.anchoredPosition = Vector2.zero;

            for (var position = 0; position < Lane.LanePositionCount; position++)
            {
                var isLog = position == 0 || position == Lane.LaneWinningPosition;
                var size = isLog
                    ? new Vector2(LogWidth, LogHeight)
                    : new Vector2(LilyPadDiameter, LilyPadDiameter);

                var positionName = position == 0
                    ? "StartLog"
                    : position == Lane.LaneWinningPosition ? "EndLog" : "LilyPad" + position;

                var positionGO = new GameObject(positionName, typeof(RectTransform), typeof(Image));
                var image = positionGO.GetComponent<Image>();
                image.sprite = isLog ? LogSprite : LilyPadSprite;
                image.type = Image.Type.Sliced;
                image.color = isLog ? LogColor : LilyPadColor;
                image.raycastTarget = false;

                var positionRect = image.rectTransform;
                positionRect.SetParent(_trackRect, worldPositionStays: false);
                positionRect.anchorMin = new Vector2(0f, 0.5f);
                positionRect.anchorMax = new Vector2(0f, 0.5f);
                positionRect.pivot = new Vector2(0.5f, 0.5f);
                positionRect.sizeDelta = size;
                positionRect.anchoredPosition = new Vector2(PositionCenterX(position), 0f);

                _positionRects.Add(positionRect);
                _positionImages.Add(image);
            }
        }

        void BuildPiece()
        {
            // piece — a flat circle in the frog's colour, per game-board.md's
            // Shape-only note. It starts on the Start log and is moved onto
            // whichever position Core reports on the first Render.
            var pieceGO = new GameObject("Piece", typeof(RectTransform), typeof(Image));
            _piece = pieceGO.GetComponent<Image>();
            _piece.sprite = PieceSprite;
            _piece.type = Image.Type.Sliced;
            _piece.raycastTarget = false;

            var pieceRect = _piece.rectTransform;
            pieceRect.SetParent(_positionRects[0], worldPositionStays: false);
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.sizeDelta = new Vector2(FrogPieceDiameter, FrogPieceDiameter);
            pieceRect.anchoredPosition = Vector2.zero;
        }
    }
}
