using System;
using System.Diagnostics;
using System.IO;
using Frogs.Core;
using UnityEditor;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace Frogs.EditorTools
{
    /// <summary>
    /// Puts the version into PlayerSettings at build time, from /VERSION and the
    /// commit history — never from a value typed into ProjectSettings.asset.
    ///
    /// This is the thin shell around <see cref="BuildStamp"/>: it reads files,
    /// environment variables, and git, and hands the values to Core. The rules
    /// about what a version name looks like and what makes a versionCode valid
    /// live in Core, where the fast suite covers them.
    ///
    /// Headlessly:
    ///
    ///     Unity -batchmode -quit -projectPath . \
    ///           -executeMethod Frogs.EditorTools.BuildStampApplier.ApplyRelease
    ///
    /// CI passes the version code and the sha in rather than making this shell
    /// out — a checkout with `fetch-depth: 1` has no history to count. See
    /// <see cref="BuildInputs"/> for how they travel. Locally, both fall back
    /// to git.
    /// </summary>
    public static class BuildStampApplier
    {
        const string VersionFileName = "VERSION";

        /// <summary>A release build: PlayerSettings shows the bare version.</summary>
        public static void ApplyRelease() => Apply(BuildStamp.Release(ReadVersion(), ReadCommitCount()));

        /// <summary>A PR build: the version name carries the commit sha.</summary>
        public static void ApplyDebug() =>
            Apply(BuildStamp.Debug(ReadVersion(), ReadCommitCount(), ReadCommitSha()));

        /// <summary>A release candidate: the version name carries "rcN".</summary>
        public static void ApplyReleaseCandidate() =>
            Apply(BuildStamp.ReleaseCandidate(ReadVersion(), ReadCommitCount(), ReadRcNumber()));

        public static void Apply(BuildStamp stamp)
        {
            PlayerSettings.bundleVersion = stamp.VersionName;
            PlayerSettings.Android.bundleVersionCode = stamp.VersionCode;

            AssetDatabase.SaveAssets();
            Debug.Log($"Build stamped {stamp.VersionName} (versionCode {stamp.VersionCode}).");
        }

        static AppVersion ReadVersion()
        {
            // Unity runs with the project root as the working directory, and
            // /VERSION sits beside Assets/.
            var path = Path.Combine(Directory.GetCurrentDirectory(), VersionFileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"No {VersionFileName} at {path}. It is the single source of the app's "
                    + "version and the build cannot invent one.",
                    path);
            }

            return AppVersion.ReadFrom(File.ReadAllText(path));
        }

        static int ReadCommitCount()
        {
            var given = BuildInputs.VersionCode;

            if (!string.IsNullOrWhiteSpace(given))
            {
                if (!int.TryParse(given, out var parsed))
                {
                    var source = BuildInputs.Describe(
                        BuildInputs.VersionCodeFlag, BuildInputs.VersionCodeVariable);

                    throw new FormatException(
                        $"The version code is '{given}', which is not a number. It comes "
                        + $"from {source}.");
                }

                return parsed;
            }

            return int.Parse(Git("rev-list --count HEAD"));
        }

        static int ReadRcNumber()
        {
            var given = BuildInputs.RcNumber;

            if (string.IsNullOrWhiteSpace(given) || !int.TryParse(given, out var parsed))
            {
                var source = BuildInputs.Describe(
                    BuildInputs.RcNumberFlag, BuildInputs.RcNumberVariable);

                throw new FormatException(
                    $"The release-candidate number is '{given}', which is not a number. "
                    + $"It comes from {source}, and is derived by "
                    + ".github/scripts/next_rc_number.py.");
            }

            return parsed;
        }

        static string ReadCommitSha()
        {
            var given = BuildInputs.BuildSha;

            return string.IsNullOrWhiteSpace(given) ? Git("rev-parse --short=7 HEAD") : given;
        }

        static string Git(string arguments)
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"`git {arguments}` failed with exit code {process.ExitCode}: {error}. "
                        + $"In CI, pass {BuildInputs.VersionCodeFlag} and "
                        + $"{BuildInputs.BuildShaFlag} on the Unity command line instead — a "
                        + "shallow checkout has no history to count, and the build container "
                        + "may have no git at all.");
                }

                return output;
            }
        }
    }
}
