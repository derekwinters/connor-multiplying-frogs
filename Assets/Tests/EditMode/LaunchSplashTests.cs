using System;
using Frogs.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The app opens on Connor's splash picture, not Unity's logo (#482).
    ///
    /// Nothing configured a splash, so every APK showed Unity's. These run the
    /// build's pre-processor over a splash set up the way Unity leaves it and
    /// read PlayerSettings.SplashScreen back, the same way ProjectIdentityTests
    /// checks the app's name.
    /// </summary>
    public sealed class LaunchSplashTests
    {
        const string BuildShaVariable = BuildInputs.BuildShaVariable;
        const string VersionCodeVariable = BuildInputs.VersionCodeVariable;

        string previousSha;
        string previousVersionCode;

        [SetUp]
        public void StartFromUnitysOwnSplash()
        {
            // The pre-processor stamps a version first; see ProjectIdentityTests
            // for why these come from the environment here.
            previousSha = Environment.GetEnvironmentVariable(BuildShaVariable);
            previousVersionCode = Environment.GetEnvironmentVariable(VersionCodeVariable);
            Environment.SetEnvironmentVariable(BuildShaVariable, "abc1234");
            Environment.SetEnvironmentVariable(VersionCodeVariable, "1");

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.blurBackgroundImage = true;
            PlayerSettings.SplashScreen.overlayOpacity = 1f;
        }

        [TearDown]
        public void RestoreTheEnvironment()
        {
            Environment.SetEnvironmentVariable(BuildShaVariable, previousSha);
            Environment.SetEnvironmentVariable(VersionCodeVariable, previousVersionCode);
        }

        [Test]
        public void ABuildShowsTheSplash()
        {
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(PlayerSettings.SplashScreen.show, Is.True);
        }

        [Test]
        public void ABuildShowsNoUnityLogo()
        {
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
            Assert.That(PlayerSettings.SplashScreen.logos, Is.Empty);
        }

        [Test]
        public void ABuildShowsConnorsPictureAsTheBackground()
        {
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(
                AssetDatabase.GetAssetPath(PlayerSettings.SplashScreen.background),
                Is.EqualTo(LaunchSplash.PicturePath));
        }

        [Test]
        public void ABuildShowsThePictureAsDrawnNotBlurredOrDarkened()
        {
            // Unity's splash blurs its background and darkens it under an
            // overlay, to make logos stand out. There are no logos, and the
            // picture is the point.
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            Assert.That(PlayerSettings.SplashScreen.blurBackgroundImage, Is.False);
            Assert.That(PlayerSettings.SplashScreen.overlayOpacity, Is.EqualTo(0f));
        }

        [Test]
        public void ApplyingTheSplashTwiceGivesTheSameSettings()
        {
            LaunchSplash.ApplyToBuild();
            var first = Snapshot();

            LaunchSplash.ApplyToBuild();
            var second = Snapshot();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain(LaunchSplash.PicturePath));
        }

        [Test]
        public void AMissingPictureFailsTheBuildAndNamesTheFile()
        {
            const string path = "Assets/Art/Splash/no-such-splash.png";

            var thrown = Assert.Throws<BuildFailedException>(() => LaunchSplash.ApplyFrom(path));

            Assert.That(thrown.Message, Does.Contain(path));
        }

        [Test]
        public void ThePictureImportsAsAFullSizeSprite()
        {
            // The splash background is a Sprite, and the picture is drawn at
            // the tablet's full 1920 x 1200; a smaller import would be scaled
            // back up on screen.
            var path = LaunchSplash.PicturePath;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(1920));
            Assert.That(importer.mipmapEnabled, Is.False);
        }

        static string Snapshot()
        {
            var splash = PlayerSettings.SplashScreen.background;

            return $"show={PlayerSettings.SplashScreen.show} "
                + $"logo={PlayerSettings.SplashScreen.showUnityLogo} "
                + $"blur={PlayerSettings.SplashScreen.blurBackgroundImage} "
                + $"overlay={PlayerSettings.SplashScreen.overlayOpacity} "
                + $"background={(splash == null ? string.Empty : AssetDatabase.GetAssetPath(splash))}";
        }
    }
}
