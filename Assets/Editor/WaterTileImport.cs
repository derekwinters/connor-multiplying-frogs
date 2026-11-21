using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The import settings of the pond's water tile
    /// (docs/engineering/tech-stack.md § The water tile), applied through the
    /// typed importer API every time Unity imports the file — for the same
    /// reasons as <see cref="LilyPadModelImport"/>: a wrong name here is a
    /// compile error, where a wrong key in a hand-written <c>.meta</c> is
    /// silently ignored, and GameBoardWaterTests reads every setting back.
    ///
    /// Only this one path is touched.
    /// </summary>
    public sealed class WaterTileImport : AssetPostprocessor
    {
        /// <summary>`PondWaterTile` — tools/art/water_tile.py's output, byte for byte.</summary>
        public const string TilePath = "Assets/Art/Water/Resources/Water/pond_water_tile.png";

        // The tile is 512 px as drawn, and is drawn larger than that on the
        // board (PondWaterTileSize, 1024 px), so it is kept at its own size.
        const int TileMaxSize = 512;

        // Bumped whenever the settings below change, so Unity re-imports the
        // file with them instead of keeping its cached import.
        const uint SettingsVersion = 1;

        public override uint GetVersion()
        {
            return SettingsVersion;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != TilePath)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;

            // The board draws the tile many times over by asking a RawImage
            // for UVs past 1 (PW-1). Repeat is what makes those UVs the next
            // tile over; clamp would smear the edge pixels across the screen.
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;

            // Only ever drawn at its own size or larger, so no smaller mip is
            // ever wanted.
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = TileMaxSize;

            // Uncompressed, so the water between the marks is exactly
            // `PondWater` (PW-2), the colour every contrast check on the
            // board was measured against, not a block-compressed
            // approximation of it.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
