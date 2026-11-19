using UnityEditor;

namespace Frogs.EditorTools
{
    /// <summary>
    /// The import settings of the frog model and its texture
    /// (docs/engineering/tech-stack.md § The frog model), applied through the
    /// typed importer API every time Unity imports either file — the same
    /// reason <see cref="ProjectBootstrap"/> sets PlayerSettings in code.
    ///
    /// The alternative is a hand-written <c>ModelImporter</c> block in each
    /// <c>.meta</c>: dozens of version-dependent keys and integer enums that
    /// Unity's YAML reader silently ignores or clamps when they are wrong
    /// (docs/engineering/unity-serialization.md). Here a wrong name is a
    /// compile error, and FrogPieceTests reads the settings back from the
    /// importer to prove they took.
    ///
    /// Only these two paths are touched. Anything else under Assets/ imports
    /// exactly as it would without this class.
    /// </summary>
    public sealed class FrogModelImport : AssetPostprocessor
    {
        /// <summary>The approved Blender frog — tools/art/frog_model.py's frog.fbx, byte for byte.</summary>
        public const string ModelPath = "Assets/Art/Frog/Resources/FrogPiece/frog.fbx";

        /// <summary>Its albedo — white where the frog takes its player colour, darker detail elsewhere.</summary>
        public const string AlbedoPath = "Assets/Art/Frog/Resources/FrogPiece/frog_albedo.png";

        // The albedo is 1024 px square as baked; nothing is downscaled.
        const int AlbedoSize = 1024;

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

            // The rig and its clips: a Generic rig — fourteen named bones, no
            // humanoid avatar mapping — with the file's two takes, Sit and
            // Hop, imported as they are. No keyframe reduction: the frog moves
            // exactly as Derek approved it.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;

            // FrogModel skins the mesh itself, on the CPU, to draw it into the
            // uGUI canvas — so a player build must keep the mesh data readable.
            importer.isReadable = true;

            // No material. The frog is drawn with uGUI's default material and
            // frog_albedo.png; importing the file's FrogSkin would bring a lit
            // shader into the build for nothing.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Nothing else in the file is wanted, and a collider needs the 3D
            // physics module, which this project does not have.
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

            // Drawn at 88 px from a 1024 px source: mipmaps, smooth filtering.
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = AlbedoSize;

            // Uncompressed, so white stays exactly white and the tint is
            // exactly the frog colour (game-board.md, FP-3).
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
