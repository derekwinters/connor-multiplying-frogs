using System;
using System.Collections.Generic;
using System.IO;
using Frogs.Core;
using Frogs.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// Connor's frog is the icon in every Android launcher slot, put there by
    /// the build itself (#459).
    ///
    /// v0.6.0 shipped with Unity's default icon: the generated icons were in
    /// Assets/Art/AppIcon/, and nothing assigned them. These run the build's
    /// pre-processor over cleared icon slots and read every slot back from
    /// PlayerSettings, the same way ProjectIdentityTests checks the app's name.
    ///
    /// Unlike the sibling project, which asserts the icons by reading a
    /// committed ProjectSettings.asset as text, this reads the live
    /// PlayerSettings. That is only sound because the textures are assigned in
    /// the same session they are read back in, after being imported: there is
    /// no serialized reference for a lazily imported texture to break.
    ///
    /// Android-only API — UnityEditor.Android needs Android Build Support and
    /// the Android target — so the slot tests compile only under
    /// UNITY_ANDROID. The first test fails when that is not the case, so the
    /// rest cannot quietly compile away.
    /// </summary>
    public sealed class AppIconTests
    {
        [Test]
        public void TheSuiteRunsWithAndroidAsTheActiveBuildTarget()
        {
            Assert.That(
                EditorUserBuildSettings.activeBuildTarget,
                Is.EqualTo(BuildTarget.Android),
                "the EditMode suite is not running against the Android target, so every "
                + "app icon test below has compiled to nothing — see ci-tests.yml (#459)");
        }

#if UNITY_ANDROID
        const string BuildShaVariable = BuildInputs.BuildShaVariable;
        const string VersionCodeVariable = BuildInputs.VersionCodeVariable;

        static readonly AppIconKind[] Kinds =
        {
            AppIconKind.Adaptive,
            AppIconKind.Round,
            AppIconKind.Legacy,
        };

        string previousSha;
        string previousVersionCode;

        [SetUp]
        public void StartFromEmptyIconSlots()
        {
            // The pre-processor stamps a version first; see ProjectIdentityTests
            // for why these come from the environment here.
            previousSha = Environment.GetEnvironmentVariable(BuildShaVariable);
            previousVersionCode = Environment.GetEnvironmentVariable(VersionCodeVariable);
            Environment.SetEnvironmentVariable(BuildShaVariable, "abc1234");
            Environment.SetEnvironmentVariable(VersionCodeVariable, "1");

            foreach (var kind in Kinds)
            {
                var unityKind = UnityKind(kind);
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, unityKind);

                foreach (var icon in icons)
                {
                    icon.SetTextures(new Texture2D[icon.maxLayerCount]);
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, unityKind, icons);
            }

            var defaultSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Any);
            PlayerSettings.SetIcons(
                NamedBuildTarget.Unknown, new Texture2D[defaultSizes.Length], IconKind.Any);
        }

        [TearDown]
        public void RestoreTheEnvironment()
        {
            Environment.SetEnvironmentVariable(BuildShaVariable, previousSha);
            Environment.SetEnvironmentVariable(VersionCodeVariable, previousVersionCode);
        }

        [TestCase(AppIconKind.Adaptive)]
        [TestCase(AppIconKind.Round)]
        [TestCase(AppIconKind.Legacy)]
        public void ABuildFillsEverySlotWithTheGeneratedIcon(AppIconKind kind)
        {
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, UnityKind(kind));

            Assert.That(icons, Is.Not.Empty, $"Unity offers no {kind} icon slots for Android");

            foreach (var icon in icons)
            {
                var expected = AppIconSet.FilesFor(kind, icon.width);
                var textures = icon.GetTextures();

                Assert.That(
                    textures.Length,
                    Is.EqualTo(expected.Length),
                    $"the {icon.width} px {kind} slot has the wrong number of layers");

                for (var layer = 0; layer < expected.Length; layer++)
                {
                    Assert.That(
                        AssetDatabase.GetAssetPath(textures[layer]),
                        Is.EqualTo($"{AppIconSet.Folder}/{expected[layer]}"),
                        $"layer {layer} of the {icon.width} px {kind} slot");
                }
            }
        }

        [Test]
        public void ABuildSetsTheDefaultIconSoUnitysLogoHasNowhereToShowThrough()
        {
            new BuildStampPreprocessor().OnPreprocessBuild(null);

            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);

            Assert.That(icons, Is.Not.Empty, "Unity offers no default icon slot");
            Assert.That(
                AssetDatabase.GetAssetPath(icons[0]),
                Is.EqualTo($"{AppIconSet.Folder}/{AppIconSet.DefaultIconFile}"));
        }

        [Test]
        public void ApplyingTheIconsTwiceGivesTheSameSlots()
        {
            AppIcons.ApplyToBuild();
            var first = Snapshot();

            AppIcons.ApplyToBuild();
            var second = Snapshot();

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void AMissingIconFileFailsTheBuildAndNamesTheFile()
        {
            const string folder = "Assets/Art/NoSuchAppIconFolder";

            var thrown = Assert.Throws<BuildFailedException>(() => AppIcons.ApplyFrom(folder));

            Assert.That(thrown.Message, Does.Contain(folder + "/"));
        }

        [Test]
        public void EveryIconImportsAtItsOwnSizeAndUncompressed()
        {
            // The generated sizes are not powers of two (162, 324 …). Unity's
            // default importer scales those to the nearest power of two and
            // compresses them; either would hand the build something other
            // than the pixels the generator drew.
            var paths = Directory.GetFiles(AppIconSet.Folder, "*.png");

            Assert.That(paths, Is.Not.Empty);

            foreach (var file in paths)
            {
                var path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);

                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
        }

        static List<string> Snapshot()
        {
            var paths = new List<string>();

            foreach (var kind in Kinds)
            {
                foreach (var icon in PlayerSettings.GetPlatformIcons(
                             NamedBuildTarget.Android, UnityKind(kind)))
                {
                    foreach (var texture in icon.GetTextures())
                    {
                        paths.Add($"{kind} {icon.width}: {AssetDatabase.GetAssetPath(texture)}");
                    }
                }
            }

            foreach (var texture in PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any))
            {
                paths.Add($"default: {AssetDatabase.GetAssetPath(texture)}");
            }

            return paths;
        }

        static PlatformIconKind UnityKind(AppIconKind kind)
        {
            switch (kind)
            {
                case AppIconKind.Adaptive:
                    return AndroidPlatformIconKind.Adaptive;
                case AppIconKind.Round:
                    return AndroidPlatformIconKind.Round;
                default:
                    return AndroidPlatformIconKind.Legacy;
            }
        }
#endif
    }
}
