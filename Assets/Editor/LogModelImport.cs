using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The import settings of the log model and its albedo
    /// (docs/engineering/tech-stack.md § The log model), applied through the
    /// typed importer API every time Unity imports either file — for the same
    /// reasons as <see cref="LilyPadModelImport"/>: a wrong name here is a
    /// compile error, where a wrong key in a hand-written <c>.meta</c> is
    /// silently ignored, and GameBoardLogTests reads every setting back.
    ///
    /// Only these two paths are touched.
    /// </summary>
    public sealed class LogModelImport : AssetPostprocessor
    {
        /// <summary>The approved Blender log's pieces — tools/art/log_model.py's log.fbx, byte for byte.</summary>
        public const string ModelPath = "Assets/Art/Log/Resources/Log/log.fbx";

        /// <summary>Their albedo — the log's own bark, sawn faces and moss.</summary>
        public const string AlbedoPath = "Assets/Art/Log/Resources/Log/log_albedo.png";

        // The albedo is 1024 px as baked, at about 225 texels to a frog unit,
        // and a frog unit is drawn 112 px: its own size, and no smaller.
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

            // Three static meshes: no rig, no animation.
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;

            // LogModel lays each piece flat on the CPU to draw it into the
            // uGUI canvas, so a player build must keep the mesh data readable.
            importer.isReadable = true;

            // The model's own normals say which faces look up; nothing is
            // recalculated.
            importer.importNormals = ModelImporterNormals.Import;

            // No material: the logs are drawn with uGUI's default material and
            // log_albedo.png, so no lit shader enters the build.
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

            // Drawn smaller than baked: mipmaps, smooth filtering. The script
            // bakes a margin round every island, so a mip does not bleed one
            // piece's bark into the next.
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = AlbedoMaxSize;

            // Uncompressed, so the bark is drawn as the colours `LogLabelInk`
            // and the frogs were measured against, not a block-compressed
            // approximation of them.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
