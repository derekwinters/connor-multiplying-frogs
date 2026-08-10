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
    /// CI passes these on the Unity command line; nothing here reads git,
    /// because a shallow checkout has no history to count:
    ///
    ///     -frogsVersionCode           the Android versionCode
    ///     -frogsBuildSha              set for a PR build, absent otherwise
    ///     -frogsRcNumber              set for a release candidate
    ///     -frogsApplicationIdSuffix   ".debug" for a PR or RC build
    ///     -frogsAndroidProfile        "device" or "emulator"
    ///
    /// They used to be environment variables, which never reached the editor
    /// inside CI's build container — see <see cref="BuildInputs"/> and #218.
    ///
    /// See docs/engineering/versioning.md.
    /// </summary>
    public sealed class BuildStampPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>Early, so later pre-processors see the stamped values.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // First, and before the profile below: the project's own identity —
            // name, company, application id, minimum API, landscape-only. CI
            // builds from a container that has no ProjectSettings.asset, so
            // without this the app is called after the working directory.
            ProjectBootstrap.ApplyToBuild();

            // Which kind of build this is, decided by which value CI passed.
            // An rc number wins over a sha: a release candidate is identified
            // by its position in the queue, because "is this newer than the one
            // I tried yesterday" is not a question a sha answers by eye.
            var rcNumber = BuildInputs.RcNumber;
            var sha = BuildInputs.BuildSha;

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
            var profile = BuildInputs.AndroidProfile;

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
                    var source = BuildInputs.Describe(
                        BuildInputs.AndroidProfileFlag, BuildInputs.AndroidProfileVariable);

                    throw new ArgumentException(
                        $"The Android build profile is '{profile}'. It must be 'device' or "
                        + "'emulator'; guessing would silently ship the wrong architecture. "
                        + $"It comes from {source}.");
            }

            // Read back rather than echoing the request: this line is how a
            // build log answers "did the profile actually take", which is the
            // question #218 left open.
            Debug.Log(
                $"Android build profile: {profile} — "
                + $"{PlayerSettings.Android.targetArchitectures}, "
                + $"{PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}.");
        }

        static void ApplyApplicationIdSuffix()
        {
            var suffix = BuildInputs.ApplicationIdSuffix;

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
