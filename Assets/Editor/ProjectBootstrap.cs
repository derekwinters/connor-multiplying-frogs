using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Frogs.EditorTools
{
    /// <summary>
    /// Project settings, applied through the typed PlayerSettings API rather
    /// than by hand-editing ProjectSettings.asset.
    ///
    /// Unity's YAML deserializer silently ignores keys it does not recognise, so
    /// a hand-authored settings file with one wrong key builds fine and is wrong
    /// at runtime. Going through the C# API turns that class of mistake into a
    /// compile error. See docs/engineering/unity-serialization.md.
    ///
    /// This lives under Assets/Editor/, so it compiles into the editor-only
    /// assembly and never ships in a player build.
    ///
    /// Run it from the menu, or headlessly:
    ///
    ///     Unity -batchmode -quit -projectPath . \
    ///           -executeMethod Frogs.EditorTools.ProjectBootstrap.Apply
    ///
    /// It is idempotent: running it twice produces the same settings. Commit
    /// whatever it changes in ProjectSettings/.
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string CompanyName = "Derek Winters";
        public const string ProductName = "Multiplying Frogs";
        public const string ApplicationIdentifier = "com.derekwinters.multiplyingfrogs";

        [MenuItem("Frogs/Apply project settings")]
        public static void Apply()
        {
            ApplyIdentity();
            ApplyAndroidTarget();
            ApplyOrientation();

            AssetDatabase.SaveAssets();
            Debug.Log("Project settings applied. Commit the changes under ProjectSettings/.");
        }

        static void ApplyIdentity()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);

            // Deliberately NOT set here: PlayerSettings.bundleVersion and
            // PlayerSettings.Android.bundleVersionCode. Those are derived from
            // /VERSION at build time so there is exactly one source of the
            // version — see docs/engineering/versioning.md.
        }

        static void ApplyAndroidTarget()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
        }

        static void ApplyOrientation()
        {
            // Portrait only. The game is played one-handed on a phone; letting
            // it rotate would mean specifying a landscape layout for every
            // screen, which is a second design nobody asked for.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }
    }
}
