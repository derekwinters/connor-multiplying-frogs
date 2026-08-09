using System;
using Frogs.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// What the app calls itself, and which way up it sits.
    ///
    /// `ProjectBootstrap` has always held the right answers — "Multiplying
    /// Frogs", portrait only, com.derekwinters.multiplyingfrogs — but nothing
    /// ran it, so no build ever had them. The first APK installed on a phone
    /// was called *workspace*, after the CI container's working directory, and
    /// it rotated freely.
    ///
    /// The settings therefore have to be applied by the build itself, the same
    /// way the version is. These tests assert that they are, by running the
    /// pre-processor over a deliberately wrong project state.
    /// </summary>
    public sealed class ProjectIdentityTests
    {
        const string BuildShaVariable = "FROGS_BUILD_SHA";
        const string VersionCodeVariable = "FROGS_VERSION_CODE";

        string previousSha;
        string previousVersionCode;

        [SetUp]
        public void StampADebugBuildWithoutTouchingGit()
        {
            // The pre-processor stamps a version before anything else, and the
            // debug path reads both of these from the environment. Set, so the
            // stamp cannot shell out to git — a shallow CI checkout has no
            // history to count, and this test is not about the version anyway.
            previousSha = Environment.GetEnvironmentVariable(BuildShaVariable);
            previousVersionCode = Environment.GetEnvironmentVariable(VersionCodeVariable);

            Environment.SetEnvironmentVariable(BuildShaVariable, "abc1234");
            Environment.SetEnvironmentVariable(VersionCodeVariable, "1");
        }

        [TearDown]
        public void RestoreTheEnvironment()
        {
            Environment.SetEnvironmentVariable(BuildShaVariable, previousSha);
            Environment.SetEnvironmentVariable(VersionCodeVariable, previousVersionCode);
        }

        [Test]
        public void ABuildIsNamedAfterTheGameRatherThanItsBuildDirectory()
        {
            PlayerSettings.productName = "workspace";

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(PlayerSettings.productName, Is.EqualTo(ProjectBootstrap.ProductName));
        }

        [Test]
        public void ABuildCarriesTheCompanyName()
        {
            PlayerSettings.companyName = "DefaultCompany";

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(PlayerSettings.companyName, Is.EqualTo(ProjectBootstrap.CompanyName));
        }

        [Test]
        public void ABuildStaysInPortrait()
        {
            // Not just the default orientation: the three rotations we do not
            // want have to be off, or Android rotates anyway. This is the check
            // that would have caught the first APK, which auto-rotated.
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.Multiple(() =>
            {
                Assert.That(
                    PlayerSettings.defaultInterfaceOrientation,
                    Is.EqualTo(UIOrientation.Portrait));
                Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
                Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
                Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.False);
                Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.False);
            });
        }

        [Test]
        public void AnAndroidBuildUsesTheRealApplicationIdentifier()
        {
            // Without this the identifier is com.DefaultCompany.workspace, which
            // is what a second app installing over the first looks like.
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.DefaultCompany.workspace");

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Does.StartWith(ProjectBootstrap.ApplicationIdentifier));
        }

        [Test]
        public void AnAndroidBuildTargetsTheMinimumApiLevelTheDocsPromise()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(
                PlayerSettings.Android.minSdkVersion,
                Is.EqualTo(AndroidSdkVersions.AndroidApiLevel24));
        }
    }
}
