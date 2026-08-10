using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The scene the app actually runs in, created and registered through the
    /// typed editor API rather than by hand-authoring YAML.
    ///
    /// This is the same decision as <see cref="HelloWorldScene"/>, for the
    /// same reason: a `.unity` file is file IDs and GUID references that have
    /// to be internally consistent and consistent with every `.meta` in the
    /// project, and Unity's deserializer ignores keys it does not recognise
    /// rather than complaining about them. Writing one by reasoning about the
    /// format is what docs/engineering/unity-serialization.md exists to
    /// forbid. Asking Unity to write it removes the guess entirely.
    ///
    /// This is what produced the committed scene, and how to produce it again:
    ///
    ///     Frogs → Create the Game scene
    ///
    ///     Unity -batchmode -quit -projectPath . \
    ///           -executeMethod Frogs.EditorTools.GameScene.EnsureReadyToBuild
    ///
    /// Commit `Assets/Scenes/Game.unity`, its `.meta`, and the changed
    /// `ProjectSettings/EditorBuildSettings.asset` together — and the `.meta`
    /// of any script the scene refers to, because that is where the GUID it
    /// points at lives. This scene has nothing in it but a camera, so there is
    /// no such script yet.
    ///
    /// Nothing calls this automatically, and it is not part of a build. It is
    /// a tool for making the asset; the asset is what ships.
    ///
    /// The scene is deliberately close to empty: a camera, so there is
    /// something to render, and nothing else. What a screen shows comes from
    /// an agreed wireframe first — the screen router that puts content into
    /// this scene is its own, later issue.
    ///
    /// It is idempotent: it creates the scene only when there is not one, and
    /// it registers the scene in build settings only when it is not already
    /// the enabled entry at index 0. Running it against a checkout that
    /// already has both does nothing.
    /// </summary>
    public static class GameScene
    {
        /// <summary>Where the build settings and the EditMode tests look.</summary>
        public const string AssetPath = "Assets/Scenes/Game.unity";

        const string CameraName = "Camera";

        /// <summary>
        /// Makes sure there is a Game scene and that a build would boot into
        /// it. Safe to call repeatedly.
        /// </summary>
        [MenuItem("Frogs/Create the Game scene")]
        public static void EnsureReadyToBuild()
        {
            EnsureSceneExists();
            EnsureRegisteredAsTheStartingScene();
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
                    + "Game scene was not created.");
            }

            // Single, so the new scene is the active one and objects created
            // below land in it.
            //
            // Not Additive. An additive scene is not the active one, so every
            // object has to be moved across with SceneManager.MoveGameObjectToScene
            // — and headlessly that combination produced no scene at all, twice,
            // without raising anything (see HelloWorldScene). This version was
            // run in CI and its output is the committed scene, so leave it
            // alone unless you have an editor open to check the replacement in.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera, so the app renders a screen rather than nothing.
            // Nothing else — what a screen looks like is a wireframe decision,
            // not something this tool gets to invent.
            new GameObject(CameraName, typeof(Camera));

            if (!EditorSceneManager.SaveScene(scene, AssetPath))
            {
                throw new InvalidOperationException(
                    $"Unity would not save the scene to {AssetPath}.");
            }

            Debug.Log($"Created {AssetPath}. Commit it, and its .meta file, together.");
        }

        static void EnsureRegisteredAsTheStartingScene()
        {
            var scenes = EditorBuildSettings.scenes;

            if (scenes.Length > 0 && scenes[0].path == AssetPath && scenes[0].enabled)
            {
                return;
            }

            // Placed first, not appended: Unity launches whichever enabled
            // entry is first in the list, and this scene is the one the app
            // is supposed to boot into. Every existing entry survives behind
            // it, so HelloWorld.unity stays registered and enabled — just no
            // longer what the app starts in.
            var updated = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(AssetPath, enabled: true),
            };
            updated.AddRange(scenes.Where(scene => scene.path != AssetPath));

            EditorBuildSettings.scenes = updated.ToArray();

            Debug.Log($"Registered {AssetPath} as the first scene in build settings.");
        }
    }
}
