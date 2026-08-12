using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The colours a whole screen is painted in, as opposed to the colours a
    /// single component is painted in.
    ///
    /// There is one of them today: the app's own background. It is declared
    /// here rather than on each screen because two different things have to
    /// agree on it — those views' backgrounds, and the scene camera's clear
    /// colour — and a hex value copied into seven places is a hex value that
    /// will be corrected in six.
    ///
    /// docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in
    /// is where the rule lives; this is where the value does.
    /// </summary>
    public static class ScreenColours
    {
        /// <summary>
        /// The app's background — the mockups' `--bg`. The title screen, game
        /// setup and game over paint it, and the scene camera clears to it, so
        /// a frame drawn before any view has painted is the game's own colour
        /// rather than whatever the engine would otherwise show.
        ///
        /// The game board is the exception, and the only one: it paints
        /// <see cref="BoardColours.PondWater"/> instead, because that screen is
        /// a pond rather than a page — docs/specs/ui/game-board.md § Colours.
        /// It paints it to every edge of the device exactly as the others
        /// paint this, so "nothing behind the canvas is ever visible" is as
        /// true there as here.
        /// </summary>
        public static readonly Color Background = new Color32(0xED, 0xF1, 0xEF, 0xFF);
    }
}
