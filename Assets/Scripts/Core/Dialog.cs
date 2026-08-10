namespace Frogs.Core
{
    /// <summary>
    /// One of the five panels the <see cref="ScreenRouter"/> can hold at most
    /// one of at a time, layered over whichever <see cref="Screen"/> is
    /// current — issue #213's navigation graph. Dialogs never stack: opening
    /// one while another is open closes the first before the second becomes
    /// current. See docs/specs/ui/shared-components.md#dialog.
    /// </summary>
    public enum Dialog
    {
        /// <summary>[Roll and card](docs/specs/ui/roll-and-card.md). Hardware back is inert here.</summary>
        RollAndCard,

        /// <summary>[Working-out grid](docs/specs/ui/working-out-grid.md). Hardware back is inert here.</summary>
        WorkingOutGrid,

        /// <summary>[Answer result](docs/specs/ui/answer-result.md). Hardware back is inert here.</summary>
        AnswerResult,

        /// <summary>[Settings dialog](docs/specs/ui/settings-dialog.md), opened from the game board.</summary>
        Settings,

        /// <summary>[End-game confirm](docs/specs/ui/end-game-confirm.md), reached only from settings.</summary>
        EndGameConfirm
    }
}
