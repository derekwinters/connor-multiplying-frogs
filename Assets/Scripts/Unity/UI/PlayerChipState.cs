namespace Frogs.Unity.UI
{
    /// <summary>
    /// The three states the shared <see cref="PlayerChip"/> is built in for
    /// v0.2 — docs/specs/ui/shared-components.md#player-chip's States table.
    /// There is no fourth, "Empty seat", state: that reading of the page was
    /// a stale disagreement with docs/specs/ui/game-setup.md, corrected in
    /// the same PR that added this type — see issue #219.
    /// </summary>
    public enum PlayerChipState
    {
        /// <summary>Swatch, colour name, pad count.</summary>
        Default,

        /// <summary><see cref="PlayerChip.PlayerChipActiveRing"/> ring, label at full weight.</summary>
        Active,

        /// <summary>Pad count replaced by <c>Home!</c>.</summary>
        Home
    }
}
