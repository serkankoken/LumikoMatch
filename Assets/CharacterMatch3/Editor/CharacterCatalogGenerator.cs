using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    public static class CharacterCatalogGenerator
    {
        private static readonly Dictionary<string, CharacterType> FileNameToCharacter = new Dictionary<string, CharacterType>(StringComparer.OrdinalIgnoreCase)
        {
            { "cat", CharacterType.Cat },
            { "bunny", CharacterType.Bunny },
            { "rabbit", CharacterType.Bunny },
            { "dino", CharacterType.Dino },
            { "penguin", CharacterType.Penguin },
            { "bear", CharacterType.Bear }
        };

        [MenuItem("Character Match-3/Create Character Catalog")]
        public static CharacterCatalog CreateOrUpdateCatalogMenu()
        {
            return CreateOrUpdateCatalog(true);
        }

        public static CharacterCatalog CreateOrUpdateCatalog(bool selectAsset)
        {
            CharacterMatch3Setup.EnsureFolders();

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharacterMatch3Constants.CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                catalog.EnsureDefaultEntries();
                AssetDatabase.CreateAsset(catalog, CharacterMatch3Constants.CatalogPath);
            }

            catalog.EnsureDefaultEntries();
            var found = 0;
            foreach (var sprite in FindCharacterSprites())
            {
                var key = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sprite));
                if (FileNameToCharacter.TryGetValue(key, out var characterType))
                {
                    catalog.SetSprite(characterType, sprite);
                    found++;
                }
            }

            AssignSpecialSprites(catalog);
            AssignGridCellSprite(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (found < CharacterMatch3Constants.AllCharacters.Length)
            {
                Debug.LogWarning($"Character Match-3 catalog found {found}/5 character sprites. Expected cat, bunny, dino, penguin, bear in Assets/char or Assets/Char.");
            }
            else
            {
                Debug.Log("Character Match-3 catalog generated successfully.");
            }

            if (selectAsset)
            {
                Selection.activeObject = catalog;
            }

            return catalog;
        }

        private static IEnumerable<Sprite> FindCharacterSprites()
        {
            var folders = new List<string>();
            if (AssetDatabase.IsValidFolder("Assets/char"))
            {
                folders.Add("Assets/char");
            }

            if (AssetDatabase.IsValidFolder("Assets/Char"))
            {
                folders.Add("Assets/Char");
            }

            var guids = folders.Count > 0
                ? AssetDatabase.FindAssets("t:Sprite", folders.ToArray())
                : AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    yield return sprite;
                }
            }
        }

        private static void AssignSpecialSprites(CharacterCatalog catalog)
        {
            AssignSpecialSprites(catalog, "Assets/Char/Horzinal_Line", PieceKind.Line, LineOrientation.Horizontal);
            AssignSpecialSprites(catalog, "Assets/Char/Horizontal_Line", PieceKind.Line, LineOrientation.Horizontal);
            AssignSpecialSprites(catalog, "Assets/Char/Vertical_Line", PieceKind.Line, LineOrientation.Vertical);
            AssignSpecialSprites(catalog, "Assets/Char/Burst", PieceKind.Burst, LineOrientation.Horizontal);
            AssignSpecialSprites(catalog, "Assets/Char/Rainbow", PieceKind.Rainbow, LineOrientation.Horizontal);
        }

        private static void AssignGridCellSprite(CharacterCatalog catalog)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Char/UI/Grid.png");
            if (sprite != null)
            {
                catalog.SetGridCellSprite(sprite);
            }
        }

        private static void AssignSpecialSprites(CharacterCatalog catalog, string folder, PieceKind kind, LineOrientation lineOrientation)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null || !TryResolveCharacterFromFileName(path, out var characterType))
                {
                    continue;
                }

                catalog.SetSpecialSprite(characterType, kind, lineOrientation, sprite);
            }
        }

        private static bool TryResolveCharacterFromFileName(string path, out CharacterType characterType)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            foreach (var pair in FileNameToCharacter)
            {
                if (name.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    characterType = pair.Value;
                    return true;
                }
            }

            characterType = CharacterType.Cat;
            return false;
        }
    }
}
