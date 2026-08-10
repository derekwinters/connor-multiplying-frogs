using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
