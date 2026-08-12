using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The Game scene — the scene the app actually boots into — asserted at
    /// the level it fails at: a scene asset that exists, is registered ahead
    /// of Hello World, and still has its camera attached after a round trip
    /// through Unity's serializer.
    ///
    /// These are the guards docs/engineering/unity-serialization.md asks for
    /// around any asset that is not authored by hand — the failure being
    /// watched for is *detachment*, which produces no error anywhere, so the
    /// messages name the likely cause rather than the expected value.
    /// </summary>
    public sealed class GameSceneTests
    {
        // Named here rather than read from the editor tooling on purpose. This
        // is the path the build settings and the APK depend on, so the test
        // pins it independently — a rename that moves the asset has to fail
        // here rather than quietly agree with itself.
        const string ScenePath = "Assets/Scenes/Game.unity";

        // One 8-bit step is 1/255; a tenth of that is well inside a round trip
        // through the scene file and nowhere near a different colour.
        const float ColourTolerance = 0.0004f;

        [Test]
        public void TheSceneAssetIsWhereTheBuildLooksForIt()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null,
                $"There is no scene at {ScenePath}. An Android build with no scenes fails "
                + "with exit code 103 and an empty output directory. Recreate it with "
                + "Frogs → Create the Game scene, and commit the scene and its .meta "
                + "together.");
        }

        [Test]
        public void TheSceneIsRegisteredInBuildSettings()
        {
            var registered = EditorBuildSettings.scenes.Any(
                scene => scene.path == ScenePath && scene.enabled);

            Assert.That(
                registered,
                Is.True,
                $"{ScenePath} exists but is not an enabled scene in build settings, so it "
                + "would not be in the APK. ProjectSettings/EditorBuildSettings.asset is "
                + "where that lives, and it has to be committed alongside the scene.");
        }

        [Test]
        public void TheGameSceneIsTheOneTheAppStartsIn()
        {
            var scenes = EditorBuildSettings.scenes;

            Assert.That(
                scenes.Length,
                Is.GreaterThan(0),
                "There are no scenes in build settings at all, so the app has nothing "
                + "to boot into.");

            Assert.That(
                scenes[0].path,
                Is.EqualTo(ScenePath),
                $"{ScenePath} is not first in EditorBuildSettings.scenes, so the app "
                + "would not boot into it. Unity launches whichever enabled entry is "
                + "first, and this is the scene that entry is supposed to be.");

            Assert.That(
                scenes[0].enabled,
                Is.True,
                $"{ScenePath} is first in build settings but not enabled, so a build "
                + "would skip straight past it to whatever is enabled after it.");
        }

        [Test]
        public void TheSceneOpensWithACameraStillAttached()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var camera = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<Camera>())
                    .FirstOrDefault(found => found != null);

                Assert.That(
                    camera,
                    Is.Not.Null,
                    "The scene has no camera, so the app renders nothing at all and there "
                    + "is no way to tell a working build from a broken one by looking at "
                    + "it.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// The camera clears to the screens' own background, not to Unity's
        /// stock skybox — issue #290, where that skybox's blue-to-brown
        /// gradient was what showed around a letterboxed game.
        ///
        /// This is belt-and-braces on top of every screen painting its
        /// background to the edge of the canvas: it is what a frame drawn
        /// before any view has painted looks like. It is asserted here, on the
        /// committed asset, because the asset is what ships — the editor tool
        /// that produced it agrees, but a build reads the `.unity` file.
        /// </summary>
        [Test]
        public void TheCameraClearsToTheScreenBackground_NotToUnitysDefaultSkybox()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var camera = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<Camera>())
                    .FirstOrDefault(found => found != null);

                Assert.That(camera, Is.Not.Null, "the scene has no camera to assert about");

                Assert.That(
                    camera.clearFlags,
                    Is.EqualTo(CameraClearFlags.SolidColor),
                    "The camera still clears to the skybox. Anything the canvas has not "
                    + "painted — a strip at the edge on a device that is not 16:10, or the "
                    + "very first frame — shows Unity's stock sky gradient, which is not "
                    + "anything this game drew.");

                // Component-wise and with a tolerance: the colour makes a
                // round trip through the scene's own float serialization, so
                // an exact struct comparison would be asserting the format
                // rather than the colour.
                Assert.That(camera.backgroundColor.r, Is.EqualTo(ScreenColours.Background.r).Within(ColourTolerance));
                Assert.That(camera.backgroundColor.g, Is.EqualTo(ScreenColours.Background.g).Within(ColourTolerance));
                Assert.That(camera.backgroundColor.b, Is.EqualTo(ScreenColours.Background.b).Within(ColourTolerance));
                Assert.That(
                    camera.backgroundColor.a,
                    Is.EqualTo(1f).Within(ColourTolerance),
                    "a transparent clear colour clears to nothing, which is the bug this "
                    + "is here to stop rather than a version of the fix");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void RunningTheToolTwiceChangesNothing()
        {
            var before = EditorBuildSettings.scenes;

            Frogs.EditorTools.GameScene.EnsureReadyToBuild();

            var after = EditorBuildSettings.scenes;

            Assert.That(
                after.Length,
                Is.EqualTo(before.Length),
                "Running Frogs.EditorTools.GameScene.EnsureReadyToBuild a second time "
                + "changed the number of registered scenes, so it is not idempotent.");

            Assert.That(
                after[0].path,
                Is.EqualTo(before[0].path),
                "Running Frogs.EditorTools.GameScene.EnsureReadyToBuild a second time "
                + "changed which scene is first in build settings, so it is not "
                + "idempotent.");
        }
    }
}
