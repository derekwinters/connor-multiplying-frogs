using System;
using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The four colours a player can be in v1 — docs/specs/ui/shared-components.md
    /// § Frog colours. These four hex values are wireframe placeholders, not the
    /// final palette: "the real palette is an area:art decision and it lands
    /// with the frog sprites; when it does, these four constants take the real
    /// values and nothing else on any screen changes."
    ///
    /// <see cref="Frogs.Core.FrogColour"/> names *which* frog a player is; it
    /// carries no <c>UnityEngine.Color</c> because Core never references
    /// UnityEngine. This is the one place that maps that identity to the
    /// colour it is painted — declared once so no screen hard-codes a hex value
    /// a second time.
    /// </summary>
    public static class FrogColours
    {
        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogGreen`, `#3F8E4F`.</summary>
        public static readonly Color FrogGreen = new Color32(0x3F, 0x8E, 0x4F, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogBlue`, `#2C6DAF`.</summary>
        public static readonly Color FrogBlue = new Color32(0x2C, 0x6D, 0xAF, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogOrange`, `#D2762B`.</summary>
        public static readonly Color FrogOrange = new Color32(0xD2, 0x76, 0x2B, 0xFF);

        /// <summary>docs/specs/ui/shared-components.md § Frog colours — `FrogPink`, `#C24C86`.</summary>
        public static readonly Color FrogPink = new Color32(0xC2, 0x4C, 0x86, 0xFF);

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
