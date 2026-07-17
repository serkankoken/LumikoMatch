using System;
using UnityEditor;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    public sealed class CharacterSpritePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/char/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("Assets/Char/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
        }
    }
}
