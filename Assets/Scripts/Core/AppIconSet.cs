using System;

namespace Frogs.Core
{
    /// <summary>The three kinds of Android launcher icon Unity has slots for.</summary>
    public enum AppIconKind
    {
        /// <summary>Android 8.0 and later: a background and a foreground layer.</summary>
        Adaptive,

        /// <summary>Android 7.1: the icon cut to a circle.</summary>
        Round,

        /// <summary>Older launchers: the square icon.</summary>
        Legacy,
    }

    /// <summary>
    /// Which of the generated icon files goes into which of Unity's Android
    /// icon slots (#459).
    ///
    /// `.github/scripts/generate_app_icons.py` draws one file per kind per
    /// density into <see cref="Folder"/>. Unity offers one slot per density
    /// and says only how many pixels wide each is. This turns a slot's width
    /// back into the file drawn for it, in plain C# so it is tested without an
    /// editor; <c>Frogs.EditorTools.AppIcons</c> does the loading and the
    /// assigning.
    ///
    /// A slot whose width is no Android density is refused rather than given
    /// the nearest file: it means Unity's slot list changed, and the nearest
    /// file would build fine and ship a blurred icon nobody looked at.
    ///
    /// See docs/engineering/tech-stack.md#the-app-icon.
    /// </summary>
    public static class AppIconSet
    {
        /// <summary>Where the generator writes the icons, relative to the project.</summary>
        public const string Folder = "Assets/Art/AppIcon";

        /// <summary>
        /// The texture for Unity's platform-independent default icon. The
        /// Android slots are all filled, so this is only a backstop against
        /// Unity's logo turning up anywhere a slot is missed.
        /// </summary>
        public const string DefaultIconFile = "legacy-xxxhdpi.png";

        /// <summary>The 108 dp adaptive canvas, and the 48 dp round and legacy icons.</summary>
        const int AdaptiveDp = 108;
        const int LauncherDp = 48;

        /// <summary>
        /// Android's density buckets as quarters of mdpi, matching
        /// generate_app_icons.py's DENSITIES.
        /// </summary>
        static readonly (string Name, int Quarters)[] Densities =
        {
            ("mdpi", 4),
            ("hdpi", 6),
            ("xhdpi", 8),
            ("xxhdpi", 12),
            ("xxxhdpi", 16),
        };

        /// <summary>ldpi is 0.75x. No file is drawn for it; mdpi is scaled down into it.</summary>
        const int LdpiQuarters = 3;

        /// <summary>
        /// The file names, within <see cref="Folder"/>, for one slot of
        /// <paramref name="kind"/> that is <paramref name="pixels"/> wide.
        /// One name per layer: background then foreground for
        /// <see cref="AppIconKind.Adaptive"/>, a single name otherwise.
        /// </summary>
        /// <exception cref="ArgumentException">The width is no Android density.</exception>
        public static string[] FilesFor(AppIconKind kind, int pixels)
        {
            var density = DensityFor(kind, pixels);

            switch (kind)
            {
                case AppIconKind.Adaptive:
                    return new[]
                    {
                        $"adaptive-background-{density}.png",
                        $"adaptive-foreground-{density}.png",
                    };

                case AppIconKind.Round:
                    return new[] { $"round-{density}.png" };

                case AppIconKind.Legacy:
                    return new[] { $"legacy-{density}.png" };

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an app icon kind.");
            }
        }

        static string DensityFor(AppIconKind kind, int pixels)
        {
            var dp = kind == AppIconKind.Adaptive ? AdaptiveDp : LauncherDp;

            if (pixels == dp * LdpiQuarters / 4)
            {
                return Densities[0].Name;
            }

            foreach (var (name, quarters) in Densities)
            {
                if (pixels == dp * quarters / 4)
                {
                    return name;
                }
            }

            throw new ArgumentException(
                $"Unity asked for a {kind} icon {pixels} px wide, which is no Android density "
                + $"of a {dp} dp icon. Unity's icon slots have changed; check "
                + "generate_app_icons.py still draws what they need.",
                nameof(pixels));
        }
    }
}
