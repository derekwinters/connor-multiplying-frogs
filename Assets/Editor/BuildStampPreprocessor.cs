using System;
using Frogs.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace Frogs.EditorTools
{
    /// <summary>
    /// Stamps every build with its version, before it builds.
    ///
    /// This runs as a build pre-processor rather than as a step CI remembers to
    /// call, because a stamping step that can be forgotten will be — and a build
    /// with the wrong version is one that cannot be identified afterwards. Local
    /// builds, CI builds, and builds started from the editor all go through
    /// here.
    ///
    /// CI sets the environment; nothing here reads git, because a shallow
    /// checkout has no history to count:
    ///
    ///     FROGS_VERSION_CODE            the Android versionCode
    ///     FROGS_BUILD_SHA               set for a debug/RC build, absent for a release
    ///     FROGS_APPLICATION_ID_SUFFIX   ".debug" for a PR build
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public sealed class BuildStampPreprocessor : IPreprocessBuildWithReport
    {
        const string ApplicationIdSuffixVariable = "FROGS_APPLICATION_ID_SUFFIX";
        const string BuildShaVariable = "FROGS_BUILD_SHA";

        /// <summary>Early, so later pre-processors see the stamped values.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // A build sha means "this is not the release" — a PR build or an RC.
            var sha = Environment.GetEnvironmentVariable(BuildShaVariable);

            if (string.IsNullOrWhiteSpace(sha))
            {
                BuildStampApplier.ApplyRelease();
            }
            else
            {
                BuildStampApplier.ApplyDebug();
            }

            ApplyApplicationIdSuffix();
        }

        static void ApplyApplicationIdSuffix()
        {
            var suffix = Environment.GetEnvironmentVariable(ApplicationIdSuffixVariable);

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return;
            }

            var current = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var suffixed = BuildStamp.ApplicationIdWithSuffix(current, suffix);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, suffixed);
            Debug.Log($"Application identifier for this build: {suffixed}");
        }
    }
}
