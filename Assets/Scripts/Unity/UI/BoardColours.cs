using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The pond's palette — docs/specs/ui/game-board.md § Colours, which is
    /// the table these values are received from, exactly as
    /// <see cref="Frogs.Unity.Views.GameBoardScreenView"/> receives that page's
    /// geometry.
    ///
    /// Until issue #291 these colours lived as private fields in two view
    /// files, annotated "copied verbatim from the committed mockup". That was
    /// honest and it was still the wrong home: every other number on this
    /// screen is born on the spec page and received by the code, and a colour
    /// that only exists in a mockup's CSS and a private field is a colour
    /// nobody can change on purpose.
    ///
    /// They live in one class rather than on the two views because the board's
    /// shape is still moving. Issue #296 takes the logs off the lane and gives
    /// them to the pond; a log's colour should not have to move house when its
    /// drawing does.
    ///
    /// **These are not the final palette.** Derek's decision — blue water,
    /// brown logs, green lily pads — is settled, and since #301 so are the
    /// three hues themselves. What is still open is the look-at-it call on
    /// the tablet: see the spec page's own "Placeholder, or settled?" note,
    /// which says the same thing about the same values, and
    /// <see cref="FrogColours"/>, which carries the identical caveat for the
    /// four frogs.
    ///
    /// **Six of these values were derived together and cannot be moved one at
    /// a time** — docs/specs/ui/game-board.md#how-the-ponds-colours-are-constrained.
    /// The water caps every frog's luminance, the log floors it, and what is
    /// left is a band spanning 2.22 : 1 that all four frogs have to share; the
    /// pad then has to clear that whole band, which leaves it two usable
    /// regions and nothing in between. Changing one of them in isolation
    /// breaks the separability bar for the others, quietly and in a way only
    /// the arithmetic in GameBoardColoursTests will say out loud.
    /// </summary>
    public static class BoardColours
    {
        /// <summary>
        /// The water — docs/specs/ui/game-board.md § Colours, `PondWater`.
        ///
        /// It is the board's background, and it is what the board paints to
        /// every edge of the device: the pond is not a rectangle inside a page,
        /// it is the whole screen. That makes it this screen's own colour
        /// rather than <see cref="ScreenColours.Background"/>, which is what
        /// every other screen paints and what the camera clears to.
        /// </summary>
        public static readonly Color PondWater = new Color32(0x9F, 0xD8, 0xF2, 0xFF);

        /// <summary>
        /// The lily pads — `LilyPadGreen`. The seven positions a lane has to
        /// itself.
        ///
        /// It is the darkest natural leaf green the bar allows, not a
        /// preference: the pad has to sit clear of the whole band the four
        /// frogs live in, and the threshold is a cliff rather than a slope —
        /// `#9CE45C` passes and `#93E04F` fails.
        /// </summary>
        public static readonly Color LilyPadGreen = new Color32(0xB2, 0xE6, 0x7F, 0xFF);

        /// <summary>The lily pad's rim, drawn `TrackOutline` (3 px) thick — `LilyPadEdge`.</summary>
        public static readonly Color LilyPadEdge = new Color32(0x6E, 0x9E, 0x4A, 0xFF);

        /// <summary>
        /// The Start and End logs — `LogBrown`. The log's whole drawing: it
        /// has no rim, so this one colour is what has to read against the
        /// water, and it does, by 8.0 : 1.
        /// </summary>
        public static readonly Color LogBrown = new Color32(0x4A, 0x2E, 0x1A, 0xFF);

        /// <summary>
        /// `Start` and `End`, written along the top of their own log —
        /// `LogLabelInk`.
        ///
        /// It exists because the log moved. The words used to be drawn in a
        /// mid-brown chosen against a pale tan log, which on chocolate is
        /// unreadable; this is 6.1 : 1 against the fill. There is no rim
        /// behind them and nothing else on the log, so the fill is the only
        /// thing they have to clear.
        /// </summary>
        public static readonly Color LogLabelInk = new Color32(0xC6, 0xB4, 0x9C, 0xFF);

        /// <summary>
        /// The header and controls bands — `BandFill`.
        ///
        /// Unchanged by issue #291, deliberately: it is the pond that Derek
        /// asked to be water, and whether these two bands still read as a
        /// frame against blue is game-board.md's open question for Connor.
        /// </summary>
        public static readonly Color BandFill = new Color32(0xE2, 0xE8, 0xE5, 0xFF);

        /// <summary>The hairline under `header` and over `controls`, `BoardBandOutline` (3 px) thick — `BandEdge`.</summary>
        public static readonly Color BandEdge = new Color32(0xB9, 0xC0, 0xBD, 0xFF);

        // `LogEdge` used to live here, at #A97F4F, and it is **gone rather
        // than renamed** (#301). On the old pale tan log a rim did real work:
        // the log and the water were 1.05 : 1 apart, so the fill could not
        // separate them and the rim, at 2.33 : 1, was what made a log read as
        // floating on the pond rather than as a hole in it. `LogBrown` clears
        // the water by 8.0 : 1 on its own, and a rim darker than that fill
        // measured 1.4 : 1 against it — invisible, and doing nothing that
        // needed doing. Nothing draws a log outline at any width any more.

        /// <summary>The board's words — `BoardInk`.</summary>
        public static readonly Color BoardInk = new Color32(0x1E, 0x24, 0x22, 0xFF);

        /// <summary>
        /// The frog piece's outline, drawn `FrogPieceOutline` (4 px) thick —
        /// `PieceEdge`. Black at 35%, so it darkens whatever it is over rather
        /// than being a fifth colour that has to work against four surfaces.
        /// </summary>
        public static readonly Color PieceEdge = new Color(0f, 0f, 0f, 0.35f);
    }
}
