namespace Frogs.Unity
{
    /// <summary>
    /// The only <see cref="ISavedGameQuery"/> in this v0.2 shape-only proof
    /// of concept: the save/resume system does not exist yet (epic #198), so
    /// there is never a saved game to report. See <see cref="ISavedGameQuery"/>
    /// for why this is a query object rather than the title screen assuming
    /// the answer directly.
    /// </summary>
    public sealed class NoSavedGameQuery : ISavedGameQuery
    {
        public bool HasSavedGame() => false;
    }
}
