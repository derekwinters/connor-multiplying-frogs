using System;
using System.Globalization;

namespace Frogs.Core
{
    /// <summary>
    /// What a build calls itself: the version name Unity shows, and the integer
    /// Android orders installs by.
    ///
    /// Lives in Core so the rules are covered by the fast NUnit suite. The
    /// Unity build script reads /VERSION, the commit count, and the commit sha,
    /// then asks this type what to put in PlayerSettings — it does not compose
    /// version strings itself.
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public readonly struct BuildStamp
    {
        const int ShortShaLength = 7;

        /// <summary>The semantic version, from /VERSION.</summary>
        public AppVersion Version { get; }

        /// <summary>
        /// Android's versionCode: the number of commits on the release branch.
        ///
        /// Not derived from <see cref="Version"/>, deliberately. Android
        /// refuses to install an APK whose code is not greater than the
        /// installed one, and several builds share a version between releases —
        /// every PR build of `0.2.3` would collide. A commit count increases
        /// with every commit, is the same for everyone who builds that commit,
        /// and owes nothing to CI run numbers.
        /// </summary>
        public int VersionCode { get; }

        /// <summary>The short commit sha, or empty for a release build.</summary>
        public string CommitSha { get; }

        /// <summary>
        /// What the app displays: "0.2.3" for a release, "0.2.3-abc1234" for a
        /// build from a PR, so a phone with four test builds on it can say
        /// which is which.
        /// </summary>
        public string VersionName =>
            CommitSha.Length == 0
                ? Version.ToString()
                : string.Format(CultureInfo.InvariantCulture, "{0}-{1}", Version, CommitSha);

        BuildStamp(AppVersion version, int versionCode, string commitSha)
        {
            Version = version;
            VersionCode = versionCode;
            CommitSha = commitSha;
        }

        /// <summary>A release build: bare version, no sha.</summary>
        public static BuildStamp Release(AppVersion version, int commitCount)
        {
            RequirePositive(commitCount);
            return new BuildStamp(version, commitCount, string.Empty);
        }

        /// <summary>A debug or release-candidate build, identified by its commit.</summary>
        public static BuildStamp Debug(AppVersion version, int commitCount, string shortSha)
        {
            RequirePositive(commitCount);
            return new BuildStamp(version, commitCount, RequireUsableSha(shortSha));
        }

        static void RequirePositive(int commitCount)
        {
            if (commitCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(commitCount),
                    commitCount,
                    "The Android versionCode must be positive — a build with a code of "
                    + "zero or less cannot be installed over anything.");
            }
        }

        static string RequireUsableSha(string shortSha)
        {
            if (string.IsNullOrWhiteSpace(shortSha))
            {
                throw new ArgumentException(
                    "A debug build needs the commit sha in its version name, or nobody can "
                    + "tell which of the builds on their phone is which.",
                    nameof(shortSha));
            }

            var trimmed = shortSha.Trim();

            if (trimmed.Length < ShortShaLength || !IsHex(trimmed))
            {
                throw new ArgumentException(
                    $"'{shortSha}' is not a commit sha of at least {ShortShaLength} hex characters.",
                    nameof(shortSha));
            }

            return trimmed.Substring(0, ShortShaLength).ToLowerInvariant();
        }

        static bool IsHex(string text)
        {
            foreach (var character in text)
            {
                var isHexDigit =
                    (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');

                if (!isHexDigit)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
