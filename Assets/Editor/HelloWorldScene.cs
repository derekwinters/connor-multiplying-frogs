using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frogs.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The Hello World scene, created and registered through the typed editor
    /// API rather than by hand-authoring YAML.
    ///
    /// This is the same decision as <see cref="ProjectBootstrap"/>, for the same
    /// reason: a `.unity` file is file IDs and GUID references that have to be
    /// internally consistent and consistent with every `.meta` in the project,
    /// and Unity's deserializer ignores keys it does not recognise rather than
    /// complaining about them. Writing one by reasoning about the format is what
    /// docs/engineering/unity-serialization.md exists to forbid. Asking Unity to
    /// write it removes the guess entirely.
    ///
    /// Run it from the menu, or let it run itself: in **batch mode** — CI, and
    /// nothing else — it creates the scene on load if it is missing, which is
    /// what lets a headless build produce an APK before anyone has opened the
    /// project in an editor. In an interactive editor it never runs by itself,
    /// because creating and saving assets behind someone's back is how work gets
    /// lost.
    ///
    /// It is idempotent, and it is a *guard*, not an owner: once the scene is
    /// committed it does nothing, and the committed asset is what ships. The
    /// EditMode tests assert the scene on disk either way.
    /// </summary>
    [InitializeOnLoad]
    public static class HelloWorldScene
    {
        /// <summary>Where the build settings and the EditMode tests look.</summary>
        public const string AssetPath = "Assets/Scenes/HelloWorld.unity";

        const string CameraName = "Camera";
        const string ProbeName = "Hello World Probe";

        static HelloWorldScene()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            try
            {
                EnsureReadyToBuild();
            }
            catch (Exception error)
            {
                // A first import can still be settling when this runs. Retrying
                // once the editor is idle costs nothing and turns a race into a
                // slower success; if it fails again the build fails, loudly,
                // which is the right outcome.
                Debug.LogWarning(
                    $"The Hello World scene could not be prepared during load ({error.Message}). "
                    + "Retrying once the editor is idle.");

                EditorApplication.delayCall += () => EnsureReadyToBuild();
            }
        }

        /// <summary>
        /// Makes sure there is a Hello World scene and that a build would
        /// include it. Safe to call repeatedly.
        /// </summary>
        [MenuItem("Frogs/Create the Hello World scene")]
        public static void EnsureReadyToBuild()
        {
            EnsureSceneExists();
            EnsureRegisteredInBuildSettings();
        }

        static void EnsureSceneExists()
        {
            // Unity runs with the project root as the working directory, so the
            // asset path is also the path on disk.
            if (File.Exists(AssetPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            // Additive, so an editor session with work open keeps it. A single
            // NewScene would close whatever is loaded.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                // A camera, so the app renders a screen rather than nothing, and
                // the probe, which is the only thing in the scene that does
                // anything. Everything about how the screen *looks* is
                // deliberately absent — see the note in
                // docs/engineering/tech-stack.md.
                // New GameObjects land in the active scene, which the additive
                // one deliberately is not, so each is moved across.
                SceneManager.MoveGameObjectToScene(
                    new GameObject(CameraName, typeof(Camera)), scene);
                SceneManager.MoveGameObjectToScene(
                    new GameObject(ProbeName, typeof(HelloWorldProbe)), scene);

                if (!EditorSceneManager.SaveScene(scene, AssetPath))
                {
                    throw new InvalidOperationException(
                        $"Unity would not save the scene to {AssetPath}.");
                }

                Debug.Log($"Created {AssetPath}. Commit it, and its .meta file, together.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        static void EnsureRegisteredInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;

            if (scenes.Any(scene => scene.path == AssetPath && scene.enabled))
            {
                return;
            }

            // Appended rather than assigned over the top: an Android build with
            // no scenes fails with exit code 103, and one that silently lost a
            // scene somebody else added fails much later than that.
            var updated = new List<EditorBuildSettingsScene>(
                scenes.Where(scene => scene.path != AssetPath))
            {
                new EditorBuildSettingsScene(AssetPath, enabled: true),
            };

            EditorBuildSettings.scenes = updated.ToArray();

            Debug.Log($"Registered {AssetPath} in build settings.");
        }
    }
}
