using System;

namespace Frogs.Core
{
    /// <summary>
    /// Fitting a string into a column that may be too narrow for it —
    /// docs/specs/ui/shared-components.md#player-chip: "the chip never
    /// refuses or alters a name it is given; if a name does not fit, the chip
    /// truncates it with an ellipsis. A readout is not where a limit is
    /// enforced."
    ///
    /// The cut is Core's rule and the measuring is the renderer's job. That
    /// split exists because <b>a character count cannot promise a width</b>:
    /// `Mohammed` is eight characters and 314 px, wider than `Alexander` at
    /// nine and 274 px. So the caller passes something that can measure its
    /// own font — a Unity <c>Text</c>'s generator, in practice — and no
    /// engine type crosses into Core to do it.
    /// </summary>
    public static class DisplayText
    {
        /// <summary>
        /// The single character a truncated string ends with. One glyph
        /// rather than three dots, because three dots are three characters
        /// wide in a column that is already short of room.
        /// </summary>
        public const string Ellipsis = "…";

        /// <summary>
        /// <paramref name="text"/> if it fits inside
        /// <paramref name="availableWidth"/>; otherwise as much of it as fits
        /// alongside an <see cref="Ellipsis"/>, ending in one.
        /// </summary>
        /// <param name="measure">
        /// How wide a given string renders in the caller's own font. When
        /// null, nothing can be known about width, so the text is handed back
        /// untouched rather than cut on a guess.
        /// </param>
        public static string TruncateToWidth(string text, float availableWidth, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text) || measure == null)
            {
                return text;
            }

            if (measure(text) <= availableWidth)
            {
                return text;
            }

            // The longest prefix that still leaves room for the ellipsis.
            // Walked down from the whole string rather than solved for,
            // because a proportional font gives no arithmetic relationship
            // between a character count and a width — the very reason this
            // takes a measuring function at all.
            for (var length = text.Length - 1; length > 0; length--)
            {
                var candidate = text.Substring(0, length) + Ellipsis;

                if (measure(candidate) <= availableWidth)
                {
                    return candidate;
                }
            }

            // Not even one character and an ellipsis fit. The ellipsis alone
            // still says "there is a name here, and it is longer than this",
            // which is the one thing the column has to convey.
            return Ellipsis;
        }
    }
}
