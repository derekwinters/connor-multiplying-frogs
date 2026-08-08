using System;
using System.Globalization;

namespace Frogs.Core
{
    /// <summary>
    /// The app's version, as read from /VERSION.
    ///
    /// Lives in Core rather than in an editor script so the parsing and the
    /// versionCode derivation are covered by the fast NUnit suite. The Unity
    /// side reads /VERSION and hands the string to <see cref="Parse"/>; it does
    /// not do arithmetic on version numbers itself.
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public readonly struct AppVersion : IEquatable<AppVersion>
    {
        const int MajorMultiplier = 10000;
        const int MinorMultiplier = 100;
        const int ComponentCount = 3;

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        AppVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Android's monotonically increasing build number, derived rather than
        /// stored, so a rebuild of a tag produces an identical artifact rather
        /// than a new number.
        ///
        /// The formula carries a constraint: minor and patch must each stay
        /// below 100. See docs/engineering/versioning.md.
        /// </summary>
        public int AndroidVersionCode => (Major * MajorMultiplier) + (Minor * MinorMultiplier) + Patch;

        /// <summary>
        /// Parses "major.minor.patch". Throws <see cref="FormatException"/> on
        /// anything else — /VERSION is read by the build, so a malformed value
        /// has to fail at the point of reading rather than silently become
        /// 0.0.0 in a shipped APK.
        /// </summary>
        public static AppVersion Parse(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parts = text.Trim().Split('.');
            if (parts.Length != ComponentCount)
            {
                throw new FormatException(
                    $"Expected a version of the form major.minor.patch, got '{text}'.");
            }

            return new AppVersion(
                ParseComponent(parts[0], "major", text),
                ParseComponent(parts[1], "minor", text),
                ParseComponent(parts[2], "patch", text));
        }

        static int ParseComponent(string part, string name, string whole)
        {
            var parsed = int.TryParse(
                part, NumberStyles.None, CultureInfo.InvariantCulture, out var value);

            // NumberStyles.None rejects signs and whitespace, so a negative or
            // padded component fails here rather than parsing to something
            // plausible.
            if (!parsed)
            {
                throw new FormatException(
                    $"The {name} component of '{whole}' is not a non-negative number.");
            }

            return value;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);

        public bool Equals(AppVersion other) =>
            Major == other.Major && Minor == other.Minor && Patch == other.Patch;

        public override bool Equals(object obj) => obj is AppVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
    }
}
