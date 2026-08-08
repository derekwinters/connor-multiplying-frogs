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
    ///     FROGS_BUILD_SHA               set for a PR build, absent otherwise
    ///     FROGS_RC_NUMBER               set for a release candidate
    ///     FROGS_APPLICATION_ID_SUFFIX   ".debug" for a PR or RC build
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public sealed class BuildStampPreprocessor : IPreprocessBuildWithReport
    {
        const string ApplicationIdSuffixVariable = "FROGS_APPLICATION_ID_SUFFIX";
        const string BuildShaVariable = "FROGS_BUILD_SHA";
        const string RcNumberVariable = "FROGS_RC_NUMBER";

        /// <summary>Early, so later pre-processors see the stamped values.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Which kind of build this is, decided by which variable CI set.
            // An rc number wins over a sha: a release candidate is identified
            // by its position in the queue, because "is this newer than the one
            // I tried yesterday" is not a question a sha answers by eye.
            var rcNumber = Environment.GetEnvironmentVariable(RcNumberVariable);
            var sha = Environment.GetEnvironmentVariable(BuildShaVariable);

            if (!string.IsNullOrWhiteSpace(rcNumber))
            {
                BuildStampApplier.ApplyReleaseCandidate();
            }
            else if (!string.IsNullOrWhiteSpace(sha))
            {
                BuildStampApplier.ApplyDebug();
            }
            else
            {
                BuildStampApplier.ApplyRelease();
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
