using System;
using System.Globalization;

namespace Frogs.Core
{
    /// <summary>
    /// The app's version, as read from /VERSION.
    ///
    /// Lives in Core rather than in an editor script so the parsing is covered
    /// by the fast NUnit suite. The Unity side reads the file and hands the
    /// string to <see cref="Parse"/>; it does not pick versions apart itself.
    ///
    /// What a build calls itself — the display name and the Android
    /// versionCode — is <see cref="BuildStamp"/>.
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public readonly struct AppVersion : IEquatable<AppVersion>
    {
        const int ComponentCount = 3;

        /// <summary>
        /// What <see cref="BuildStamp.VersionName"/> puts between the version
        /// and whatever identifies the individual build.
        /// </summary>
        const char BuildSuffixSeparator = '-';

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
        /// Reads the version out of the contents of /VERSION.
        ///
        /// That file carries release-please's marker on the same line as the
        /// version — `0.0.1 # x-release-please-version` — because the generic
        /// updater only rewrites a line containing the marker. So every
        /// consumer has to strip from '#' onward, and this is the one place
        /// that does it.
        /// </summary>
        public static AppVersion ReadFrom(string fileContents)
        {
            if (fileContents is null)
            {
                throw new ArgumentNullException(nameof(fileContents));
            }

            var withoutComment = fileContents.Split('#')[0];
            var trimmed = withoutComment.Trim();

            if (trimmed.Length == 0)
            {
                throw new FormatException(
                    "/VERSION has no version on it — expected `0.0.1 # x-release-please-version`.");
            }

            return Parse(trimmed);
        }

        /// <summary>
        /// Reads the version back out of a build's version name — the string a
        /// running app reports as its own version.
        ///
        /// The inverse of <see cref="BuildStamp.VersionName"/>, which appends
        /// whatever distinguishes this build from the plain release of the same
        /// version after a '-': a short commit sha, or an "rcN". Everything from
        /// that separator onward is dropped.
        ///
        /// The running app needs this because the version it can see at runtime
        /// is the stamped name, and the version on the screen is the only
        /// evidence anybody has that /VERSION reached the APK.
        /// </summary>
        public static AppVersion ReadFromBuildName(string versionName)
        {
            if (versionName is null)
            {
                throw new ArgumentNullException(nameof(versionName));
            }

            var beforeSeparator = versionName.Split(BuildSuffixSeparator)[0];

            return Parse(beforeSeparator);
        }

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
