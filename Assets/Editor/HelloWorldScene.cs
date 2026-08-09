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
    /// Run it from the menu — **Frogs → Create the Hello World scene** — and
    /// commit `Assets/Scenes/HelloWorld.unity`, its `.meta`, and the changed
    /// `ProjectSettings/EditorBuildSettings.asset` together.
    ///
    /// Nothing calls it automatically, and CI does not run it. Two automatic
    /// triggers were tried headlessly and neither produced a scene: an
    /// `[InitializeOnLoad]` static constructor, and `-executeMethod` from a
    /// GameCI step. Both ran, both left `Assets/Scenes/` empty. Why is not
    /// something that can be worked out without an editor to watch, and
    /// guessing at it is what
    /// docs/engineering/unity-serialization.md exists to prevent — so this is
    /// a menu item a person runs once, and the scene it produces is committed.
    ///
    /// It is idempotent: it creates the scene only when there is not one, so
    /// running it against a checkout that already has the committed scene does
    /// nothing. The EditMode tests assert the scene on disk either way.
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
            // below land in it. Additive would need each object moving across,
            // which is more moving parts for no gain.
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
