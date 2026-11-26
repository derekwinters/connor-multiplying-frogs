using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// How the launch splash picture imports, applied through the typed
    /// importer API on every import, the way <see cref="WaterTileImport"/>
    /// does it: a wrong name here is a compile error, where a wrong key in a
    /// hand-written <c>.meta</c> is silently ignored. LaunchSplashTests reads
    /// it back.
    ///
    /// PlayerSettings.SplashScreen.background is a Sprite, so the picture
    /// imports as one. It is drawn at the tablet's full 1920 x 1200, so the
    /// import keeps that size, and it is only ever drawn full-screen, so it
    /// has no mipmaps. Compression is left at Unity's default.
    /// </summary>
    public sealed class LaunchSplashImport : AssetPostprocessor
    {
        // Bumped whenever the settings below change, so Unity re-imports the
        // file with them instead of keeping its cached import.
        const uint SettingsVersion = 1;

        // The picture's long side is 1920; Unity's sizes are powers of two.
        const int MaxSize = 2048;

        public override uint GetVersion()
        {
            return SettingsVersion;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != LaunchSplash.PicturePath)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxSize;
        }
    }
}
