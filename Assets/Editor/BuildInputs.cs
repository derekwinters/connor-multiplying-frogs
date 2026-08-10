using System;
using Frogs.Core;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The values CI passes into a build, and where they come from.
    ///
    /// **Unity's command line first, the environment second**, and the order
    /// is the whole point of this file.
    ///
    /// CI used to pass these as environment variables on the workflow step.
    /// They never reached Unity. `game-ci/unity-builder` runs the editor inside
    /// a container and forwards only its own allow-list of variables into it —
    /// `UNITY_*`, `BUILD_*`, `ANDROID_*`, `CUSTOM_PARAMETERS`, a few
    /// `GITHUB_*`. A `FROGS_*` variable sat on the runner, outside the
    /// container, unread. Every caller here treats an absent value as "this
    /// build did not ask for that", so nothing failed: `v0.1.0` shipped a
    /// device APK and an emulator APK that were the same file, byte for byte
    /// (issue #218).
    ///
    /// What does arrive is the command line. The action appends its
    /// `customParameters` input to the `unity-editor` invocation verbatim, so
    /// the workflows pass `-frogsAndroidProfile emulator` and it lands in
    /// <c>Environment.GetCommandLineArgs()</c>.
    ///
    /// The environment is kept as a fallback because it still works everywhere
    /// the container is not involved — a local headless
    /// `-executeMethod BuildStampApplier.ApplyRelease`, and the EditMode tests,
    /// which set the variables directly.
    ///
    /// See docs/engineering/ci-cd.md and docs/engineering/versioning.md.
    /// </summary>
    public static class BuildInputs
    {
        public const string VersionCodeFlag = "-frogsVersionCode";
        public const string BuildShaFlag = "-frogsBuildSha";
        public const string RcNumberFlag = "-frogsRcNumber";
        public const string ApplicationIdSuffixFlag = "-frogsApplicationIdSuffix";
        public const string AndroidProfileFlag = "-frogsAndroidProfile";

        public const string VersionCodeVariable = "FROGS_VERSION_CODE";
        public const string BuildShaVariable = "FROGS_BUILD_SHA";
        public const string RcNumberVariable = "FROGS_RC_NUMBER";
        public const string ApplicationIdSuffixVariable = "FROGS_APPLICATION_ID_SUFFIX";
        public const string AndroidProfileVariable = "FROGS_ANDROID_PROFILE";

        /// <summary>Android's versionCode: the commit count.</summary>
        public static string VersionCode => Read(VersionCodeFlag, VersionCodeVariable);

        /// <summary>The short commit sha, set for a PR build.</summary>
        public static string BuildSha => Read(BuildShaFlag, BuildShaVariable);

        /// <summary>The release-candidate number, set for an RC build.</summary>
        public static string RcNumber => Read(RcNumberFlag, RcNumberVariable);

        /// <summary>".debug" for a PR or RC build, absent for a release.</summary>
        public static string ApplicationIdSuffix =>
            Read(ApplicationIdSuffixFlag, ApplicationIdSuffixVariable);

        /// <summary>"device" or "emulator", absent for an editor build.</summary>
        public static string AndroidProfile =>
            Read(AndroidProfileFlag, AndroidProfileVariable);

        /// <summary>
        /// How to say, in an error, that a value the build needed is missing.
        ///
        /// Both names, because "set FROGS_ANDROID_PROFILE" is advice that has
        /// already been followed once, in a place where it does nothing.
        /// </summary>
        public static string Describe(string flag, string variable) =>
            $"`{flag}` on the Unity command line (or {variable} in the environment, "
            + "which only works outside the build container)";

        static string Read(string flag, string variable)
        {
            var fromCommandLine =
                BuildArguments.From(Environment.GetCommandLineArgs()).Value(flag);

            return string.IsNullOrWhiteSpace(fromCommandLine)
                ? Environment.GetEnvironmentVariable(variable)
                : fromCommandLine;
        }
    }
}
