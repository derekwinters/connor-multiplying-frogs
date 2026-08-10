namespace Frogs.Unity
{
    /// <summary>
    /// Whether a saved game exists to resume — the question the title
    /// screen's `RESUME` button (docs/specs/ui/title-screen.md) asks before
    /// deciding whether to lay itself out at all.
    ///
    /// The save/resume system does not exist anywhere in this v0.2
    /// shape-only proof of concept — epic #198 explicitly excludes it from
    /// scope — so <see cref="NoSavedGameQuery"/> is the only implementation
    /// today, and it always answers no. This interface exists anyway,
    /// instead of the title screen hardcoding "RESUME never exists", so a
    /// future save-system issue can implement the real answer without
    /// reworking this screen — see the settled decision on issue #216.
    /// </summary>
    public interface ISavedGameQuery
    {
        /// <summary>True if there is a saved game `RESUME` could restore.</summary>
        bool HasSavedGame();
    }
}
