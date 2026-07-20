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
            if (normalized.StartsWith("Assets/Char/UI/LevelsBG/", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureSprite(importer, 2048, TextureImporterCompression.Compressed);
                return;
            }

            if (normalized.StartsWith("Assets/Char/UI/Maps/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ConfigureSprite(importer, 1024, TextureImporterCompression.CompressedHQ);
        }

        private static void ConfigureSprite(TextureImporter importer, int maxTextureSize, TextureImporterCompression compression)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = compression;
            importer.maxTextureSize = maxTextureSize;
            importer.mipmapEnabled = false;
        }
    }
}
