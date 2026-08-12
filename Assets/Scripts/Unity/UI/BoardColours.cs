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
    /// brown logs, green lily pads — is settled. The exact hues are Connor's
    /// call, made on the tablet against the committed mockups, and until he
    /// makes it these carry the proposal drawn there. See the spec page's own
    /// "Placeholder, or settled?" note, which says the same thing about the
    /// same values, and <see cref="FrogColours"/>, which carries the identical
    /// caveat for the four frogs.
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

        /// <summary>The lily pads — `LilyPadGreen`. The seven positions a lane has to itself.</summary>
        public static readonly Color LilyPadGreen = new Color32(0xCC, 0xEA, 0xAF, 0xFF);

        /// <summary>The lily pad's rim, drawn `TrackOutline` (3 px) thick — `LilyPadEdge`.</summary>
        public static readonly Color LilyPadEdge = new Color32(0x7F, 0xAE, 0x5E, 0xFF);

        /// <summary>The Start and End logs — `LogBrown`.</summary>
        public static readonly Color LogBrown = new Color32(0xE2, 0xC7, 0x9C, 0xFF);

        /// <summary>
        /// The log's rim, drawn `TrackOutline` (3 px) thick — `LogEdge`.
        ///
        /// It is doing more work than the lily pad's. A log and the water are
        /// deliberately close in brightness and far apart in hue, so this rim
        /// is what makes the log's shape read against the water rather than
        /// its fill — see the spec page's "keeping the frogs visible".
        /// </summary>
        public static readonly Color LogEdge = new Color32(0xA9, 0x7F, 0x4F, 0xFF);

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
