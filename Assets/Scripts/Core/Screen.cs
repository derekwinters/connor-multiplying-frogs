namespace Frogs.Core
{
    /// <summary>
    /// One of the five full-screen destinations the <see cref="ScreenRouter"/>
    /// moves between — issue #213's navigation graph: title screen, game
    /// setup, game board, game over, and how to play. A <see cref="Dialog"/>
    /// is a separate, layered concept that opens over whichever of these is
    /// current; it is never one of these five.
    /// </summary>
    public enum Screen
    {
        /// <summary>[Title screen](docs/specs/ui/title-screen.md). The router's starting screen.</summary>
        TitleScreen,

        /// <summary>[Game setup](docs/specs/ui/game-setup.md): choosing frogs before a game starts.</summary>
        GameSetup,

        /// <summary>[Game board](docs/specs/ui/game-board.md): the pond, and every dialog a turn opens over it.</summary>
        GameBoard,

        /// <summary>[Game over](docs/specs/ui/game-over.md): standings, reached when a game ends.</summary>
        GameOver,

        /// <summary>
        /// [How to play](docs/specs/ui/how-to-play.md): the five pages the
        /// settings dialog's `How to play` opens.
        ///
        /// It is a screen rather than a <see cref="Dialog"/> because it is
        /// opened from inside a dialog, and
        /// docs/specs/ui/shared-components.md#dialog says a dialog never
        /// opens over another dialog. Navigating here therefore closes the
        /// settings dialog on the way in — which is that page's own
        /// "it replaces what is on screen rather than covering it" — and
        /// leaving is the shell reopening it.
        /// </summary>
        HowToPlay
    }
}
