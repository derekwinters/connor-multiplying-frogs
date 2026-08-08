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

        /// <summary>
        /// What distinguishes this build from the plain release of the same
        /// version — a short commit sha, or an "rcN" — and empty for a release.
        /// </summary>
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

        /// <summary>
        /// A release candidate: "0.1.0-rc2".
        ///
        /// Named by its position in the queue rather than by its commit,
        /// because the question an RC has to answer is "is this newer than the
        /// one I tried yesterday" — which a sha cannot answer by eye.
        /// </summary>
        public static BuildStamp ReleaseCandidate(AppVersion version, int commitCount, int rcNumber)
        {
            RequirePositive(commitCount);

            if (rcNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rcNumber),
                    rcNumber,
                    "Release candidates start at rc1. An rc0 means the counting went wrong, "
                    + "and an RC nobody can order against its siblings is worse than no RC.");
            }

            return new BuildStamp(
                version,
                commitCount,
                string.Format(CultureInfo.InvariantCulture, "rc{0}", rcNumber));
        }

        /// <summary>A debug build, identified by its commit.</summary>
        public static BuildStamp Debug(AppVersion version, int commitCount, string shortSha)
        {
            RequirePositive(commitCount);
            return new BuildStamp(version, commitCount, RequireUsableSha(shortSha));
        }

        /// <summary>
        /// The Android application identifier for a build, with its suffix
        /// applied — ".debug" for a PR build, nothing for a release.
        ///
        /// A suffixed build installs *alongside* the release rather than
        /// replacing it, so the game Connor plays survives someone testing a
        /// change on the same phone.
        ///
        /// Idempotent: applying the same suffix twice leaves one, so a build
        /// that stamps more than once cannot produce `...debug.debug`.
        /// </summary>
        public static string ApplicationIdWithSuffix(string applicationId, string suffix)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new ArgumentException(
                    "A build needs an application identifier.", nameof(applicationId));
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return applicationId;
            }

            RequireUsableSuffix(suffix);

            return applicationId.EndsWith(suffix, StringComparison.Ordinal)
                ? applicationId
                : applicationId + suffix;
        }

        static void RequireUsableSuffix(string suffix)
        {
            // Android package segments are lowercase letters, digits, and
            // underscores, each introduced by a dot. A suffix that does not fit
            // that produces an APK the device refuses to install, with an error
            // that does not mention the identifier.
            var valid = suffix.Length > 1 && suffix[0] == '.';

            if (valid)
            {
                foreach (var character in suffix.Substring(1))
                {
                    var allowed =
                        (character >= 'a' && character <= 'z')
                        || (character >= '0' && character <= '9')
                        || character == '_'
                        || character == '.';

                    if (!allowed)
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid)
            {
                throw new ArgumentException(
                    $"'{suffix}' is not a usable applicationId suffix — it must start with a "
                    + "dot and contain only lowercase letters, digits, and underscores.",
                    nameof(suffix));
            }
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
