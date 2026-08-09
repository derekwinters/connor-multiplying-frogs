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
            ApplyToBuild();
            ApplyAndroidTarget();

            AssetDatabase.SaveAssets();
            Debug.Log("Project settings applied. Commit the changes under ProjectSettings/.");
        }

        /// <summary>
        /// The settings a build must have, applied by the build itself.
        ///
        /// <c>ProjectSettings/ProjectSettings.asset</c> is not in the repository,
        /// so every CI build starts from whatever Unity generates in the
        /// container — which named the first APK <c>workspace</c>, after the
        /// working directory, and let it rotate. Relying on someone having run
        /// the menu item above is what produced that.
        ///
        /// So <see cref="BuildStampPreprocessor"/> calls this on every build,
        /// for the same reason it stamps the version there: a step that can be
        /// forgotten will be.
        ///
        /// Architecture and scripting backend are deliberately NOT here — those
        /// are the device/emulator profile's, and it runs after this.
        /// </summary>
        public static void ApplyToBuild()
        {
            ApplyIdentity();
            ApplyMinimumApiLevel();
            ApplyOrientation();
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

        static void ApplyMinimumApiLevel()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        }

        static void ApplyAndroidTarget()
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
        }

        static void ApplyOrientation()
        {
            // Landscape only. The game is built for kids' tablets, which are
            // held landscape — Derek's call, overriding the portrait-only rule
            // this file used to carry. That rule's reason was one-handed play
            // on a phone, which is not the device any more.
            //
            // Both landscape rotations, because a tablet gets picked up either
            // way up and neither is upside down. Neither portrait rotation,
            // because rotating into portrait needs a portrait layout and no
            // wireframe specifies one — the same argument the old comment made,
            // pointed the other way.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }
    }
}
