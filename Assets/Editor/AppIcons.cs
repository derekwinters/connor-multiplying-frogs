using System.Collections.Generic;
using System.IO;
using Frogs.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace Frogs.EditorTools
{
    /// <summary>
    /// Puts Connor's frog into every Android launcher icon slot, on every
    /// build (#459). Called from <see cref="ProjectBootstrap.ApplyToBuild"/>,
    /// which <see cref="BuildStampPreprocessor"/> runs before each build.
    ///
    /// The icon slots are filled in memory, through the typed PlayerSettings
    /// API, for the same reason as everything else in ProjectBootstrap: CI has
    /// no committed ProjectSettings.asset, and a hand-written one is the guess
    /// docs/engineering/unity-serialization.md rules out.
    ///
    /// The textures are loaded by path. Unity's script reference for
    /// PlatformIcon.SetTextures asks for exactly that — "Texture files must be
    /// stored in the project and instances obtained using
    /// AssetDatabase.LoadAssetAtPath" — and loading by path is also why no
    /// committed `.meta` is needed: nothing refers to a GUID. A texture that has
    /// not been imported yet is imported synchronously first, so a fresh CI
    /// Library cannot hand back null for a file that is there.
    ///
    /// Every failure is a <see cref="BuildFailedException"/>: a missing or
    /// misnamed file, a slot Unity did not keep. The alternative is an APK
    /// with Unity's icon on it, which is what v0.6.0 was.
    ///
    /// Idempotent: the same files go into the same slots every time.
    ///
    /// See docs/engineering/tech-stack.md#the-app-icon.
    /// </summary>
    public static class AppIcons
    {
        public static void ApplyToBuild()
        {
            ApplyFrom(AppIconSet.Folder);
        }

        /// <summary>
        /// The same, from another folder. Exists so a test can point it at one
        /// with nothing in it; builds use <see cref="ApplyToBuild"/>.
        /// </summary>
        public static void ApplyFrom(string folder)
        {
#if UNITY_ANDROID
            ApplyPlatformIcons(folder, AppIconKind.Adaptive, AndroidPlatformIconKind.Adaptive);
            ApplyPlatformIcons(folder, AppIconKind.Round, AndroidPlatformIconKind.Round);
            ApplyPlatformIcons(folder, AppIconKind.Legacy, AndroidPlatformIconKind.Legacy);
            ApplyDefaultIcon(folder);
#else
            // UnityEditor.Android only exists with Android Build Support and the
            // Android target active (Unity script reference,
            // AndroidPlatformIconKind). Without it there are no Android slots to
            // fill — but an Android build that got here would ship Unity's icon,
            // so that case is a failure rather than a skip.
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "This is an Android build, but UNITY_ANDROID is not defined, so the "
                    + "app icon cannot be set and the APK would carry Unity's icon.");
            }

            Debug.Log($"App icon: not set, the active build target is "
                + $"{EditorUserBuildSettings.activeBuildTarget}, not Android.");
#endif
        }

#if UNITY_ANDROID
        static void ApplyPlatformIcons(string folder, AppIconKind kind, PlatformIconKind unityKind)
        {
            // The array Unity hands out, filled and handed back: SetPlatformIcons
            // takes only that array, with one entry per slot.
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, unityKind);

            if (icons.Length == 0)
            {
                throw new BuildFailedException(
                    $"Unity offers no {kind} icon slots for Android, so the app icon cannot be set.");
            }

            var expected = new List<string[]>();

            foreach (var icon in icons)
            {
                string[] files;

                try
                {
                    files = AppIconSet.FilesFor(kind, icon.width);
                }
                catch (System.ArgumentException problem)
                {
                    throw new BuildFailedException(problem.Message);
                }

                if (files.Length != icon.maxLayerCount)
                {
                    throw new BuildFailedException(
                        $"The {icon.width} px {kind} icon slot takes {icon.maxLayerCount} "
                        + $"layers; {files.Length} were drawn for it.");
                }

                var paths = new string[files.Length];
                var textures = new Texture2D[files.Length];

                for (var layer = 0; layer < files.Length; layer++)
                {
                    paths[layer] = $"{folder}/{files[layer]}";
                    textures[layer] = Load(paths[layer]);
                }

                icon.SetTextures(textures);
                expected.Add(paths);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, unityKind, icons);

            // Read back rather than trusting the call: SetPlatformIcons says
            // nothing when it declines an array.
            var applied = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, unityKind);

            for (var slot = 0; slot < applied.Length; slot++)
            {
                var textures = applied[slot].GetTextures();

                for (var layer = 0; layer < expected[slot].Length; layer++)
                {
                    var actual = layer < textures.Length
                        ? AssetPathOf(textures[layer])
                        : string.Empty;

                    if (actual != expected[slot][layer])
                    {
                        throw new BuildFailedException(
                            $"The {applied[slot].width} px {kind} icon slot did not keep "
                            + $"{expected[slot][layer]}; it holds '{actual}'.");
                    }
                }
            }

            Debug.Log($"App icon: {applied.Length} {kind} slots filled from {folder}.");
        }

        static void ApplyDefaultIcon(string folder)
        {
            // NamedBuildTarget.Unknown is the "Default Icon": its target name is
            // "", which is what the Player Settings inspector reads and writes
            // the default icon under (UnityCsReference, PlayerSettingsIconsEditor).
            var path = $"{folder}/{AppIconSet.DefaultIconFile}";
            var texture = Load(path);
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Any);

            if (sizes.Length == 0)
            {
                throw new BuildFailedException(
                    "Unity offers no default icon slot, so the default icon cannot be set.");
            }

            // SetIcons ignores an array that is not one per size.
            var icons = new Texture2D[sizes.Length];

            for (var i = 0; i < icons.Length; i++)
            {
                icons[i] = texture;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, icons, IconKind.Any);

            var applied = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            var actual = applied.Length > 0 ? AssetPathOf(applied[0]) : string.Empty;

            if (actual != path)
            {
                throw new BuildFailedException(
                    $"The default icon did not keep {path}; it holds '{actual}'.");
            }
        }
#endif

        static Texture2D Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new BuildFailedException(
                    $"The app icon {path} is missing. Run "
                    + ".github/scripts/generate_app_icons.py rather than ship Unity's icon.");
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture == null)
            {
                // On a fresh Library a texture nothing else refers to can still be
                // waiting to import.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            if (texture == null)
            {
                throw new BuildFailedException(
                    $"The app icon {path} exists but Unity did not import it as a texture.");
            }

            return texture;
        }

        static string AssetPathOf(Texture2D texture)
        {
            return texture == null ? string.Empty : AssetDatabase.GetAssetPath(texture);
        }
    }
}
