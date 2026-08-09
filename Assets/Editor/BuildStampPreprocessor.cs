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
    ///     FROGS_ANDROID_PROFILE         "device" or "emulator"
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public sealed class BuildStampPreprocessor : IPreprocessBuildWithReport
    {
        const string ApplicationIdSuffixVariable = "FROGS_APPLICATION_ID_SUFFIX";
        const string BuildShaVariable = "FROGS_BUILD_SHA";
        const string RcNumberVariable = "FROGS_RC_NUMBER";
        const string AndroidProfileVariable = "FROGS_ANDROID_PROFILE";

        /// <summary>Early, so later pre-processors see the stamped values.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // First, and before the profile below: the project's own identity —
            // name, company, application id, minimum API, landscape-only. CI
            // builds from a container that has no ProjectSettings.asset, so
            // without this the app is called after the working directory.
            ProjectBootstrap.ApplyToBuild();

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
            ApplyAndroidProfile();
        }

        /// <summary>
        /// The device/emulator split from docs/engineering/tech-stack.md.
        ///
        /// Left alone when unset, so a build that does not ask for a profile
        /// gets whatever the project is configured with — the editor's own
        /// Build button keeps working.
        /// </summary>
        static void ApplyAndroidProfile()
        {
            var profile = Environment.GetEnvironmentVariable(AndroidProfileVariable);

            if (string.IsNullOrWhiteSpace(profile))
            {
                return;
            }

            switch (profile.Trim().ToLowerInvariant())
            {
                case "device":
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    break;

                case "emulator":
                    // x86_64 and Mono. IL2CPP cross-compiling to x86_64 is slow
                    // and buys nothing for a smoke test — this profile trades
                    // fidelity for a build that finishes while you are still
                    // looking at it.
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
                    break;

                default:
                    throw new ArgumentException(
                        $"{AndroidProfileVariable} is '{profile}'. It must be 'device' or "
                        + "'emulator'; guessing would silently ship the wrong architecture.");
            }

            Debug.Log($"Android build profile: {profile}.");
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
