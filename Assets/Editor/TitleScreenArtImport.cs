using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// How the title screen's art and the START sign's font import (#520),
    /// applied through the typed importer API on every import, the way
    /// <see cref="LaunchSplashImport"/> and <see cref="WaterTileImport"/> do
    /// it: a wrong name here is a compile error, where a wrong key in a
    /// hand-written <c>.meta</c> is silently ignored
    /// (docs/engineering/unity-serialization.md). TitleScreenViewTests reads
    /// both back.
    ///
    /// The art is the launch splash's picture, rendered by
    /// art-source/splash/build_splash.py --home-only, so it imports with the
    /// launch splash's settings: a single Sprite, because the view draws it
    /// with a uGUI Image; kept at its 1920 x 1200; no mipmaps, because it is
    /// only ever drawn at full screen or larger. Compression is left at
    /// Unity's default, as the launch splash's is.
    ///
    /// The extended scene behind it (#533) is the same picture carried on past
    /// its frame, rendered by the same build step, and imports with the same
    /// settings; only its largest size is bigger, because it is 2880 wide.
    ///
    /// The font is a static Fredoka Bold, instanced at weight 700 from the
    /// variable font by the same build step, because the legacy font importer
    /// has no setting that picks a variable font's weight. It imports as a
    /// dynamic font — glyphs rasterised at whatever size the label asks for —
    /// with the font data included in the build, since no device has Fredoka
    /// of its own to fall back on.
    ///
    /// Only these three paths are touched. All live under a Resources folder,
    /// because TitleScreenView loads them by path at runtime: nothing refers
    /// to any of them by GUID, so none needs a committed <c>.meta</c>.
    /// </summary>
    public sealed class TitleScreenArtImport : AssetPostprocessor
    {
        public const string ArtPath = "Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png";
        public const string ScenePath = "Assets/Art/TitleScreen/Resources/TitleScreen/title-scene-2880x1440.png";
        public const string FontPath = "Assets/Fonts/Fredoka/Resources/Fredoka/Fredoka-Bold.ttf";

        // Bumped whenever the settings below change, so Unity re-imports the
        // files with them instead of keeping its cached import.
        const uint SettingsVersion = 1;

        // The picture's long side is 1920, the scene's 2880; Unity's sizes are
        // powers of two.
        const int MaxSize = 2048;
        const int SceneMaxSize = 4096;

        public override uint GetVersion()
        {
            return SettingsVersion;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != ArtPath && assetPath != ScenePath)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = assetPath == ScenePath ? SceneMaxSize : MaxSize;
        }

        void OnPreprocessAsset()
        {
            if (assetPath != FontPath)
            {
                return;
            }

            if (assetImporter is TrueTypeFontImporter importer)
            {
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.includeFontData = true;
            }
        }
    }
}
