using System;
using Frogs.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The device/emulator split, applied by the build rather than promised by
    /// a doc — docs/engineering/tech-stack.md#two-build-profiles.
    ///
    /// The emulator profile used to ask for x86_64 with the Mono backend, which
    /// Unity cannot build: there is no Mono JIT for 64-bit Android, so the
    /// editor dropped the architecture, kept nothing, and failed the build at
    /// its prerequisites check with "Target architecture not specified". It
    /// took the v0.2.0 release's APKs with it (issue #282).
    ///
    /// `AndroidBuildSupportTests` in the Core suite covers the rule itself in
    /// two seconds with no editor. These are the other half: that the profiles
    /// this project actually ships obey it, read back out of `PlayerSettings`
    /// after the pre-processor has run.
    /// </summary>
    public sealed class AndroidBuildProfileTests
    {
        const string ProfileVariable = BuildInputs.AndroidProfileVariable;
        const string BuildShaVariable = BuildInputs.BuildShaVariable;
        const string VersionCodeVariable = BuildInputs.VersionCodeVariable;

        string previousProfile;
        string previousSha;
        string previousVersionCode;

        [SetUp]
        public void StampADebugBuildWithoutTouchingGit()
        {
            // As in ProjectIdentityTests: the pre-processor stamps a version
            // before it reaches the profile, and the debug path needs both of
            // these so nothing shells out to git. The environment rather than
            // the command line, because a test cannot change the process's
            // arguments — in CI these ride Unity's command line instead.
            previousProfile = Environment.GetEnvironmentVariable(ProfileVariable);
            previousSha = Environment.GetEnvironmentVariable(BuildShaVariable);
            previousVersionCode = Environment.GetEnvironmentVariable(VersionCodeVariable);

            Environment.SetEnvironmentVariable(BuildShaVariable, "abc1234");
            Environment.SetEnvironmentVariable(VersionCodeVariable, "1");
        }

        [TearDown]
        public void RestoreTheEnvironment()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, previousProfile);
            Environment.SetEnvironmentVariable(BuildShaVariable, previousSha);
            Environment.SetEnvironmentVariable(VersionCodeVariable, previousVersionCode);
        }

        [Test]
        public void TheEmulatorProfileBuildsX86_64WithIl2Cpp()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "emulator");

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            // Sequential rather than Assert.Multiple: the NUnit inside the
            // Unity Test Framework has none.
            Assert.That(
                PlayerSettings.Android.targetArchitectures,
                Is.EqualTo(AndroidArchitecture.X86_64));
            Assert.That(
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
        }

        [Test]
        public void TheDeviceProfileBuildsArm64WithIl2Cpp()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "device");

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(
                PlayerSettings.Android.targetArchitectures,
                Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
        }

        [Test]
        public void NeitherProfileLeavesTheBuildWithNoArchitectureAtAll()
        {
            // The shape of the v0.2.0 failure. An empty set is what Unity is
            // left holding when a profile asks for something it cannot build,
            // and the pre-processor now refuses to hand that on to the build.
            foreach (var profile in new[] { "device", "emulator" })
            {
                Environment.SetEnvironmentVariable(ProfileVariable, profile);

                new BuildStampPreprocessor().OnPreprocessBuild(null);

                Assert.That(
                    PlayerSettings.Android.targetArchitectures,
                    Is.Not.EqualTo(AndroidArchitecture.None),
                    $"the '{profile}' profile left no target architecture set");
            }
        }

        [Test]
        public void AProfileNameTheBuildDoesNotKnowFailsRatherThanGuessing()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "tablet");

            Assert.That(
                () => new BuildStampPreprocessor().OnPreprocessBuild(null),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
