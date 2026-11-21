using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The import settings of the lily pad model and its albedo atlas
    /// (docs/engineering/tech-stack.md § The lily pad model), applied through
    /// the typed importer API every time Unity imports either file — for the
    /// same reasons as <see cref="FrogModelImport"/>: a wrong name here is a
    /// compile error, where a wrong key in a hand-written <c>.meta</c> is
    /// silently ignored, and GameBoardLilyPadTests reads every setting back.
    ///
    /// Only these two paths are touched.
    /// </summary>
    public sealed class LilyPadModelImport : AssetPostprocessor
    {
        /// <summary>The approved Blender pads — tools/art/lilypad_model.py's lilypad.fbx, byte for byte.</summary>
        public const string ModelPath = "Assets/Art/LilyPad/Resources/LilyPad/lilypad.fbx";

        /// <summary>Their albedo atlas — the pad's own colours, rim and veins, one quadrant per notch width.</summary>
        public const string AlbedoPath = "Assets/Art/LilyPad/Resources/LilyPad/lilypad_albedo.png";

        // The atlas is 2048 px as baked, four 1024 px quadrants. A pad is
        // drawn 112 px across, so a 512 px quadrant is still more than four
        // times what is shown, at a quarter of the memory.
        const int AlbedoMaxSize = 1024;

        // Bumped whenever the settings below change, so Unity re-imports both
        // files with them instead of keeping its cached import.
        const uint SettingsVersion = 1;

        public override uint GetVersion()
        {
            return SettingsVersion;
        }

        void OnPreprocessModel()
        {
            if (assetPath != ModelPath)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;

            // Four static meshes: no rig, no animation.
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;

            // LilyPadModel lays each mesh flat on the CPU to draw it into the
            // uGUI canvas, so a player build must keep the mesh data readable.
            importer.isReadable = true;

            // The model's own normals say which faces look up; nothing is
            // recalculated.
            importer.importNormals = ModelImporterNormals.Import;

            // No material: the pads are drawn with uGUI's default material and
            // lilypad_albedo.png, so no lit shader enters the build.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != AlbedoPath)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;

            // Drawn at 112 px from a 512 px quadrant: mipmaps, smooth
            // filtering. Everything outside each pad in its quadrant is
            // painted rim colour, so a mip never bleeds the next pad in.
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = AlbedoMaxSize;

            // Uncompressed, so `LilyPadGreen` and `LilyPadEdge` are drawn as
            // the colours the separability bar was measured against, not a
            // block-compressed approximation of them.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
