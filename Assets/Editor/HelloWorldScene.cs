using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frogs.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    /// This is what produced the committed scene, and how to produce it again:
    ///
    ///     Frogs → Create the Hello World scene
    ///
    ///     Unity -batchmode -quit -projectPath . \
    ///           -executeMethod Frogs.EditorTools.HelloWorldScene.EnsureReadyToBuild
    ///
    /// Commit `Assets/Scenes/HelloWorld.unity`, its `.meta`, and the changed
    /// `ProjectSettings/EditorBuildSettings.asset` together — and the `.meta` of
    /// any script the scene refers to, because that is where the GUID it points
    /// at lives.
    ///
    /// Nothing calls this automatically, and it is not part of a build. It is
    /// a tool for making the asset; the asset is what ships.
    ///
    /// It is idempotent: it creates the scene only when there is not one, so
    /// running it against a checkout that already has the scene does nothing.
    /// </summary>
    public static class HelloWorldScene
    {
        /// <summary>Where the build settings and the EditMode tests look.</summary>
        public const string AssetPath = "Assets/Scenes/HelloWorld.unity";

        const string CameraName = "Camera";
        const string ProbeName = "Hello World Probe";

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

            // Anything unsaved is offered to whoever is at the keyboard first,
            // because the new scene replaces what is open. In batch mode there
            // is nothing open and this returns immediately.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException(
                    "The open scene has unsaved changes and they were not saved, so the "
                    + "Hello World scene was not created.");
            }

            // Single, so the new scene is the active one and objects created
            // below land in it.
            //
            // Not Additive. An additive scene is not the active one, so every
            // object has to be moved across with SceneManager.MoveGameObjectToScene
            // — and headlessly that combination produced no scene at all, twice,
            // without raising anything. This version was run in CI and its
            // output is the committed scene, so leave it alone unless you have
            // an editor open to check the replacement in.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera, so the app renders a screen rather than nothing, and the
            // probe, which is the only thing in the scene that does anything.
            // Everything about how the screen *looks* is deliberately absent —
            // see the note in docs/engineering/tech-stack.md.
            new GameObject(CameraName, typeof(Camera));
            new GameObject(ProbeName, typeof(HelloWorldProbe));

            if (!EditorSceneManager.SaveScene(scene, AssetPath))
            {
                throw new InvalidOperationException(
                    $"Unity would not save the scene to {AssetPath}.");
            }

            Debug.Log($"Created {AssetPath}. Commit it, and its .meta file, together.");
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
