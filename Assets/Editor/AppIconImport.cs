using Frogs.Core;
using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The import settings of the generated app icons in
    /// <see cref="AppIconSet.Folder"/>, applied through the typed importer API
    /// on every import, the same way <see cref="WaterTileImport"/> does it: a
    /// wrong name here is a compile error, where a wrong key in a hand-written
    /// <c>.meta</c> is silently ignored. AppIconTests reads them back.
    ///
    /// The point is that the texture Unity holds is the file the generator
    /// drew, pixel for pixel. Most of the icons are not powers of two (162,
    /// 324 …), and Unity's default importer scales those to the nearest power
    /// of two and block-compresses them. Unity does not say whether its icon
    /// export reads the imported texture or the source file, so this makes the
    /// answer not matter.
    /// </summary>
    public sealed class AppIconImport : AssetPostprocessor
    {
        // Bumped whenever the settings below change, so Unity re-imports the
        // files with them instead of keeping its cached import.
        const uint SettingsVersion = 1;

        // The largest generated file is the 512 px store image.
        const int MaxSize = 512;

        public override uint GetVersion()
        {
            return SettingsVersion;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AppIconSet.Folder + "/") || !assetPath.EndsWith(".png"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
