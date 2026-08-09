using System.Linq;
using Frogs.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The Hello World scene, asserted at the level it fails at: a scene asset
    /// that exists, is in the build, and still has its components attached
    /// after a round trip through Unity's serializer.
    ///
    /// These are the guards docs/engineering/unity-serialization.md asks for
    /// around any asset not authored by hand in an editor — the failure being
    /// watched for is *detachment*, which produces no error anywhere, so the
    /// messages name the likely cause rather than the expected value.
    /// </summary>
    public sealed class HelloWorldSceneTests
    {
        // Named here rather than read from the editor tooling on purpose. This
        // is the path the build settings and the APK depend on, so the test
        // pins it independently — a rename that moves the asset has to fail
        // here rather than quietly agree with itself.
        const string ScenePath = "Assets/Scenes/HelloWorld.unity";

        const string HowToCreateIt =
            "Run Frogs → Create the Hello World scene, or open the project in batch mode, "
            + "which creates it on load. See docs/engineering/tech-stack.md.";

        [Test]
        public void TheSceneAssetIsWhereTheBuildLooksForIt()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null,
                $"There is no scene at {ScenePath}. An Android build with no scenes fails "
                + $"with exit code 103 and an empty output directory. {HowToCreateIt}");
        }

        [Test]
        public void TheSceneIsRegisteredInBuildSettings()
        {
            var registered = EditorBuildSettings.scenes.Any(
                scene => scene.path == ScenePath && scene.enabled);

            Assert.That(
                registered,
                Is.True,
                $"{ScenePath} is not an enabled scene in build settings, so it would not be "
                + $"in the APK even though the asset exists. {HowToCreateIt}");
        }

        [Test]
        public void TheSceneOpensWithItsProbeAndACameraStillAttached()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var roots = scene.GetRootGameObjects();

                var probe = roots
                    .Select(root => root.GetComponent<HelloWorldProbe>())
                    .FirstOrDefault(found => found != null);

                var camera = roots
                    .Select(root => root.GetComponent<Camera>())
                    .FirstOrDefault(found => found != null);

                Assert.That(
                    probe,
                    Is.Not.Null,
                    "The scene has no HelloWorldProbe. The usual cause is a script reference "
                    + "whose GUID no longer resolves, which the editor shows as Missing "
                    + "Script and reports nowhere else.");

                Assert.That(
                    camera,
                    Is.Not.Null,
                    "The scene has no camera, so the app renders nothing at all and there is "
                    + "no way to tell a working build from a broken one by looking at it.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void TheProbeReportsTheVersionCoreReadsOutOfTheBuildName()
        {
            // The point of the assertion: the value on screen came from Core,
            // inside a real Unity compilation, rather than from a string the
            // shell composed itself.
            var expected = AppVersion.Parse("0.2.3").ToString();

            Assert.That(HelloWorldProbe.Describe("0.2.3-abc1234"), Does.Contain(expected));
        }

        [Test]
        public void TheProbeSaysSoRatherThanThrowingWhenTheBuildStampIsUnreadable()
        {
            Assert.That(() => HelloWorldProbe.Describe("nightly"), Throws.Nothing);
            Assert.That(HelloWorldProbe.Describe("nightly"), Does.Contain("nightly"));
        }
    }
}
