using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Frogs.EditorTools
{
    /// <summary>
    /// Shows Connor's splash picture while the app starts, instead of Unity's
    /// logo (#482). Called from <see cref="ProjectBootstrap.ApplyToBuild"/>,
    /// which <see cref="BuildStampPreprocessor"/> runs before each build.
    ///
    /// Set in memory through the typed PlayerSettings.SplashScreen API, like
    /// everything else in ProjectBootstrap: CI has no committed
    /// ProjectSettings.asset, and a hand-written one is the guess
    /// docs/engineering/unity-serialization.md rules out. The picture is
    /// loaded by path, so nothing refers to a GUID and no committed `.meta` is
    /// needed; <see cref="LaunchSplashImport"/> sets how it imports.
    ///
    /// The picture is the whole splash composite at the tablet's 1920 x 1200,
    /// rendered by art-source/splash/build_splash.py.
    ///
    /// A missing picture fails the build: the alternative is Unity's splash,
    /// which is what every APK until now has shown. Idempotent.
    ///
    /// See docs/engineering/tech-stack.md#the-launch-splash.
    /// </summary>
    public static class LaunchSplash
    {
        public const string PicturePath = "Assets/Art/Splash/splash-1920x1200.png";

        public static void ApplyToBuild()
        {
            ApplyFrom(PicturePath);
        }

        /// <summary>
        /// The same, with another picture. Exists so a test can point it at a
        /// file that is not there; builds use <see cref="ApplyToBuild"/>.
        /// </summary>
        public static void ApplyFrom(string path)
        {
            var picture = Load(path);

            PlayerSettings.SplashScreen.show = true;

            // Unity 6 lets every licence tier turn its logo off. With it off and
            // no logos of our own, the splash is the picture alone.
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];

            // Landscape only, so there is no portrait picture: Unity shows the
            // landscape one in portrait when this is null.
            PlayerSettings.SplashScreen.background = picture;
            PlayerSettings.SplashScreen.backgroundPortrait = null;

            // Unity blurs the background and darkens it under an overlay so
            // logos stand out against it. There are no logos; the picture is
            // the point, so it is shown as drawn.
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;

            // Read back rather than trusting the setters.
            var applied = PlayerSettings.SplashScreen.background;
            var appliedPath = applied == null ? string.Empty : AssetDatabase.GetAssetPath(applied);

            if (appliedPath != path || PlayerSettings.SplashScreen.showUnityLogo
                || !PlayerSettings.SplashScreen.show)
            {
                throw new BuildFailedException(
                    $"The launch splash did not take: background '{appliedPath}', "
                    + $"show {PlayerSettings.SplashScreen.show}, "
                    + $"Unity logo {PlayerSettings.SplashScreen.showUnityLogo}.");
            }

            // Read back into the build log, which is how CI answers "did the
            // splash take" without anyone unpacking an APK.
            Debug.Log($"Launch splash: background {appliedPath}, "
                + $"show {PlayerSettings.SplashScreen.show}, "
                + $"Unity logo {PlayerSettings.SplashScreen.showUnityLogo}, "
                + $"logos {PlayerSettings.SplashScreen.logos.Length}, "
                + $"blur {PlayerSettings.SplashScreen.blurBackgroundImage}, "
                + $"overlay {PlayerSettings.SplashScreen.overlayOpacity}.");
        }

        static Sprite Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new BuildFailedException(
                    $"The launch splash picture {path} is missing. Run "
                    + "art-source/splash/build_splash.py --splash-only rather than ship "
                    + "Unity's splash.");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                // On a fresh Library a picture nothing else refers to can still be
                // waiting to import, or have imported before LaunchSplashImport
                // made it a sprite.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            if (sprite == null)
            {
                throw new BuildFailedException(
                    $"The launch splash picture {path} exists but Unity did not import it "
                    + "as a sprite.");
            }

            return sprite;
        }
    }
}
