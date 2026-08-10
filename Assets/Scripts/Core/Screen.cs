namespace Frogs.Core
{
    /// <summary>
    /// One of the four full-screen destinations the <see cref="ScreenRouter"/>
    /// moves between — issue #213's navigation graph: title screen, game
    /// setup, game board, game over. A <see cref="Dialog"/> is a separate,
    /// layered concept that opens over whichever of these is current; it is
    /// never one of these four.
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
        GameOver
    }
}
