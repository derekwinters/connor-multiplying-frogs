using System;
using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The four colours a player can be in v1 — docs/specs/ui/shared-components.md
    /// § Frog colours. These four hex values are still placeholders for the
    /// real palette, which is an area:art decision that lands with the frog
    /// sprites; when it does, these four constants take the real values and
    /// nothing else on any screen changes.
    ///
    /// What changed on #301 is that they are no longer *arbitrary*
    /// placeholders. They were **derived** to clear the game board's
    /// separability bar against the pond's three surfaces — 1.9 : 1 contrast
    /// and ΔE*ab 30 against the water, a lily pad and a log — rather than
    /// picked as four plausible hues and checked afterwards. The band they
    /// have to share spans 2.22 : 1 in total, so hand-editing any one of them
    /// is almost certainly a colour that no longer clears the bar; the
    /// arithmetic is in GameBoardColoursTests and the working is on
    /// docs/specs/ui/game-board.md#how-the-ponds-colours-are-constrained.
    ///
    /// They are also allowed to move when the pond does, which they were not
    /// before: game-board.md used to say "the surface moves, not the frog",
    /// and Derek reversed it on #301 because with the pond he had picked, no
    /// set of four frog colours existed at all.
    ///
    /// One thing these four still do **not** satisfy, said here rather than
    /// left to be discovered by measuring: shared-components.md's invariant
    /// that "the four are distinguishable to a colour-blind player by
    /// lightness alone". They step 1.28 : 1 apart at worst — better than the
    /// 1.11 : 1 of the values they replace, and the most the available band
    /// allows once four colours share it.
    ///
    /// <see cref="Frogs.Core.FrogColour"/> names *which* frog a player is; it
    /// carries no <c>UnityEngine.Color</c> because Core never references
    /// UnityEngine. This is the one place that maps that identity to the
    /// colour it is painted — declared once so no screen hard-codes a hex value
    /// a second time.
    /// </summary>
    public static class FrogColours
    {
        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogGreen`, `#3E933E`.</summary>
        public static readonly Color FrogGreen = new Color32(0x3E, 0x93, 0x3E, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogBlue`, `#37609A`.</summary>
        public static readonly Color FrogBlue = new Color32(0x37, 0x60, 0x9A, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogOrange`, `#D38231`.</summary>
        public static readonly Color FrogOrange = new Color32(0xD3, 0x82, 0x31, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogPink`, `#D41C78`.</summary>
        public static readonly Color FrogPink = new Color32(0xD4, 0x1C, 0x78, 0xFF);

        /// <summary>The concrete colour for a <see cref="Frogs.Core.FrogColour"/> identity.</summary>
        public static Color For(Frogs.Core.FrogColour colour)
        {
            switch (colour)
            {
                case Frogs.Core.FrogColour.Green:
                    return FrogGreen;
                case Frogs.Core.FrogColour.Blue:
                    return FrogBlue;
                case Frogs.Core.FrogColour.Orange:
                    return FrogOrange;
                case Frogs.Core.FrogColour.Pink:
                    return FrogPink;
                default:
                    throw new ArgumentOutOfRangeException(nameof(colour), colour, null);
            }
        }
    }
}
