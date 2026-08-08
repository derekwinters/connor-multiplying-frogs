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
    /// CI sets FROGS_VERSION_CODE and FROGS_BUILD_SHA rather than making this
    /// shell out — a checkout with `fetch-depth: 1` has no history to count.
    /// Locally, both fall back to git.
    /// </summary>
    public static class BuildStampApplier
    {
        const string VersionFileName = "VERSION";
        const string VersionCodeVariable = "FROGS_VERSION_CODE";
        const string BuildShaVariable = "FROGS_BUILD_SHA";
        const string RcNumberVariable = "FROGS_RC_NUMBER";

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
            var fromEnvironment = Environment.GetEnvironmentVariable(VersionCodeVariable);

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                if (!int.TryParse(fromEnvironment, out var parsed))
                {
                    throw new FormatException(
                        $"{VersionCodeVariable} is '{fromEnvironment}', which is not a number.");
                }

                return parsed;
            }

            return int.Parse(Git("rev-list --count HEAD"));
        }

        static int ReadRcNumber()
        {
            var value = Environment.GetEnvironmentVariable(RcNumberVariable);

            if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var parsed))
            {
                throw new FormatException(
                    $"{RcNumberVariable} is '{value}', which is not a release-candidate "
                    + "number. It is derived by .github/scripts/next_rc_number.py.");
            }

            return parsed;
        }

        static string ReadCommitSha()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(BuildShaVariable);

            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? Git("rev-parse --short=7 HEAD")
                : fromEnvironment;
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
                        + $"In CI, set {VersionCodeVariable} and {BuildShaVariable} instead — a "
                        + "shallow checkout has no history to count.");
                }

                return output;
            }
        }
    }
}
