using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterMatch3.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterMatch3.Editor
{
    public static class LevelMapBackgroundTools
    {
        private const string PrimaryUiFolder = "Assets/Char/UI";
        private const string FlatUiFolder = "Assets/Assets_Char_UI";
        private const string LiteralUiFolder = "Assets_Char_UI";
        private const string PrimaryMapsFolder = PrimaryUiFolder + "/Maps";
        private const string FlatMapsFolder = "Assets/Assets_Char_UI_Maps";
        private const string LiteralMapsFolder = "Assets_Char_UI_Maps";
        private const string BackgroundFileName = "BG.png";
        private const string ContinuousMapFileName = "Map_Continuous_01_15.png";
        private const string LevelMapScenePath = CharacterMatch3Constants.RootPath + "/Scenes/LevelMap.unity";
        private const float DefaultMeadowPercent = 0.36f;
        private const float DefaultBeachPercent = 0.32f;
        private const float DefaultOverlapPercent = 0.05f;

        private static readonly MapAssetDefinition BackgroundAsset =
            new MapAssetDefinition(BackgroundFileName, "backgroundSprite", "Primary BG map");

        private static readonly MapSliceDefinition[] Slices =
        {
            new MapSliceDefinition("Map_Meadow_Slice", "mapMeadowSprite", "Meadow"),
            new MapSliceDefinition("Map_Beach_Slice", "mapBeachSprite", "Beach"),
            new MapSliceDefinition("Map_Desert_Slice", "mapDesertSprite", "Desert")
        };

        private static readonly MapAssetDefinition[] SegmentAssets =
        {
            new MapAssetDefinition("Map_Meadow.png", "mapMeadowSprite", "Meadow"),
            new MapAssetDefinition("Map_Beach.png", "mapBeachSprite", "Beach"),
            new MapAssetDefinition("Map_Desert.png", "mapDesertSprite", "Desert")
        };

        private static readonly MapAssetDefinition[] NodeAssets =
        {
            new MapAssetDefinition("LevelNode_Unlocked_Blue.png", "unlockedNodeSprite", "Unlocked node"),
            new MapAssetDefinition("LevelNode_Current_BlueGlow.png", "currentNodeSprite", "Current node"),
            new MapAssetDefinition("LevelNode_Completed_Green.png", "completedNodeSprite", "Completed node"),
            new MapAssetDefinition("LevelNode_Locked_PurpleGrey.png", "lockedNodeSprite", "Locked node")
        };

        private readonly struct MapAssetDefinition
        {
            public readonly string FileName;
            public readonly string PropertyName;
            public readonly string DisplayName;

            public MapAssetDefinition(string fileName, string propertyName, string displayName)
            {
                FileName = fileName;
                PropertyName = propertyName;
                DisplayName = displayName;
            }
        }

        private readonly struct MapSliceDefinition
        {
            public readonly string SpriteName;
            public readonly string PropertyName;
            public readonly string DisplayName;

            public MapSliceDefinition(string spriteName, string propertyName, string displayName)
            {
                SpriteName = spriteName;
                PropertyName = propertyName;
                DisplayName = displayName;
            }
        }

        private readonly struct SliceSettings
        {
            public readonly float MeadowPercent;
            public readonly float BeachPercent;
            public readonly float OverlapPercent;

            public SliceSettings(float meadowPercent, float beachPercent, float overlapPercent)
            {
                MeadowPercent = meadowPercent;
                BeachPercent = beachPercent;
                OverlapPercent = overlapPercent;
            }
        }

        [MenuItem("Character Match-3/Map Tools/Build Continuous Map Slices")]
        public static void BuildContinuousMapSlices()
        {
            var folder = ResolveMapsFolder();
            var path = $"{folder}/{ContinuousMapFileName}";
            var report = new List<string>();
            var changed = false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Character Match-3 continuous map slice build cancelled because modified scenes were not saved.");
                return;
            }

            var levelMapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelMapScenePath);
            Scene scene = default;
            LevelMapUI[] sceneMaps = null;
            var sliceSettings = new SliceSettings(DefaultMeadowPercent, DefaultBeachPercent, DefaultOverlapPercent);

            if (levelMapScene != null)
            {
                scene = EditorSceneManager.OpenScene(LevelMapScenePath, OpenSceneMode.Single);
                sceneMaps = Object.FindObjectsByType<LevelMapUI>(FindObjectsSortMode.None);
                if (sceneMaps.Length > 0)
                {
                    sliceSettings = ReadSliceSettings(sceneMaps[0]);
                }
            }
            else
            {
                report.Add($"WARNING: Level map scene was not found at {LevelMapScenePath}.");
            }

            var sprites = new Dictionary<string, Sprite>();
            if (!File.Exists(path))
            {
                report.Add($"WARNING: Missing {ContinuousMapFileName} in {folder}. Existing map placeholders were kept.");
            }
            else
            {
                ConfigureAndSliceContinuousMap(path, sliceSettings, report);
                sprites = LoadContinuousSliceSprites(path, report);
            }

            var nodeSprites = LoadNodeSprites(folder, report);

            if (sceneMaps != null && (sprites.Count > 0 || nodeSprites.Count > 0))
            {
                foreach (var map in sceneMaps)
                {
                    changed |= AssignSpritesToMap(map, sprites);
                    changed |= AssignSpritesToMap(map, nodeSprites);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Add($"Updated scene references: {LevelMapScenePath}.");
                }
            }

            var prefabsChanged = sprites.Count > 0 && UpdateMapPrefabs(sprites);
            prefabsChanged |= nodeSprites.Count > 0 && UpdateMapPrefabs(nodeSprites);
            if (prefabsChanged)
            {
                changed = true;
                report.Add("Updated LevelMapUI prefab references.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Add("Runtime segment order remains bottom-to-top: Meadow, Beach, Desert, then Desert repeats for later levels.");
            report.Add("Level ranges are Meadow 1-7, Beach 8-14, Desert 15-21, then Desert repeats for later levels.");
            report.Add("Fixed HUD, scrolling, save, node, and scene logic were not rebuilt.");
            Debug.Log("Character Match-3 continuous map slice build complete.\n" + string.Join("\n", report));
        }

        [MenuItem("Character Match-3/Map Tools/Refresh Map Backgrounds")]
        public static void RefreshMapBackgrounds()
        {
            RefreshSeparateMapBackgrounds();
        }

        internal static void AssignAvailableSpritesToMap(LevelMapUI map)
        {
            if (map == null)
            {
                return;
            }

            var folder = ResolveMapsFolder();
            var report = new List<string>();
            var backgroundSprites = LoadPrimaryBackgroundSprite(report);
            if (backgroundSprites.Count > 0)
            {
                AssignSpritesToMap(map, backgroundSprites);
            }
            else
            {
                var segmentSprites = LoadSeparateMapSprites(folder, report);
                if (segmentSprites.Count > 0)
                {
                    AssignSpritesToMap(map, segmentSprites);
                }
                else
                {
                    var path = $"{folder}/{ContinuousMapFileName}";
                    if (!File.Exists(path))
                    {
                        Debug.LogWarning($"Character Match-3 map backgrounds missing: expected {BackgroundFileName} in {ResolveUiFolder()}, or Map_Meadow.png, Map_Beach.png, Map_Desert.png, or {ContinuousMapFileName} in {folder}. Keeping existing map background placeholders.");
                    }
                    else
                    {
                        ConfigureAndSliceContinuousMap(path, ReadSliceSettings(map), report);
                        AssignSpritesToMap(map, LoadContinuousSliceSprites(path, report));
                    }
                }
            }

            AssignSpritesToMap(map, LoadNodeSprites(folder, report));
        }

        private static void RefreshSeparateMapBackgrounds()
        {
            var folder = ResolveMapsFolder();
            var report = new List<string>();
            var changed = false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Character Match-3 map background refresh cancelled because modified scenes were not saved.");
                return;
            }

            var sprites = LoadPrimaryBackgroundSprite(report);
            if (sprites.Count == 0)
            {
                sprites = LoadSeparateMapSprites(folder, report);
            }
            var nodeSprites = LoadNodeSprites(folder, report);
            var levelMapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelMapScenePath);
            Scene scene = default;
            LevelMapUI[] sceneMaps = null;

            if (levelMapScene != null)
            {
                scene = EditorSceneManager.OpenScene(LevelMapScenePath, OpenSceneMode.Single);
                sceneMaps = Object.FindObjectsByType<LevelMapUI>(FindObjectsSortMode.None);
            }
            else
            {
                report.Add($"WARNING: Level map scene was not found at {LevelMapScenePath}.");
            }

            if (sceneMaps != null && (sprites.Count > 0 || nodeSprites.Count > 0))
            {
                foreach (var map in sceneMaps)
                {
                    changed |= AssignSpritesToMap(map, sprites);
                    changed |= AssignSpritesToMap(map, nodeSprites);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Add($"Updated scene references: {LevelMapScenePath}.");
                }
            }

            var prefabsChanged = sprites.Count > 0 && UpdateMapPrefabs(sprites);
            prefabsChanged |= nodeSprites.Count > 0 && UpdateMapPrefabs(nodeSprites);
            if (prefabsChanged)
            {
                changed = true;
                report.Add("Updated LevelMapUI prefab references.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Add("Runtime segment order remains bottom-to-top: Meadow, Beach, Desert, then Desert repeats for later levels.");
            report.Add("Level ranges are Meadow 1-7, Beach 8-14, Desert 15-21, then Desert repeats for later levels.");
            report.Add("BottomBar remains disabled by LevelMapUI.");
            Debug.Log("Character Match-3 map background refresh complete.\n" + string.Join("\n", report));
        }

        private static string ResolveUiFolder()
        {
            if (AssetDatabase.IsValidFolder(PrimaryUiFolder))
            {
                return PrimaryUiFolder;
            }

            if (AssetDatabase.IsValidFolder(FlatUiFolder))
            {
                return FlatUiFolder;
            }

            if (AssetDatabase.IsValidFolder(LiteralUiFolder))
            {
                return LiteralUiFolder;
            }

            if (Directory.Exists(LiteralUiFolder))
            {
                Debug.LogWarning($"Character Match-3 found {LiteralUiFolder}, but Unity can only assign sprites from imported asset folders. Expected {PrimaryUiFolder} or {FlatUiFolder}.");
            }

            return PrimaryUiFolder;
        }

        private static string ResolveMapsFolder()
        {
            if (AssetDatabase.IsValidFolder(PrimaryMapsFolder))
            {
                return PrimaryMapsFolder;
            }

            if (AssetDatabase.IsValidFolder(FlatMapsFolder))
            {
                return FlatMapsFolder;
            }

            if (AssetDatabase.IsValidFolder(LiteralMapsFolder))
            {
                return LiteralMapsFolder;
            }

            if (Directory.Exists(LiteralMapsFolder))
            {
                Debug.LogWarning($"Character Match-3 found {LiteralMapsFolder}, but Unity can only assign sprites from imported asset folders. Expected {PrimaryMapsFolder} or {FlatMapsFolder}.");
            }

            return PrimaryMapsFolder;
        }

        private static SliceSettings ReadSliceSettings(LevelMapUI map)
        {
            if (map == null)
            {
                return new SliceSettings(DefaultMeadowPercent, DefaultBeachPercent, DefaultOverlapPercent);
            }

            var serialized = new SerializedObject(map);
            var meadow = ReadFloat(serialized, "meadowSlicePercent", DefaultMeadowPercent, 0.34f, 0.46f);
            var beach = ReadFloat(serialized, "beachSlicePercent", DefaultBeachPercent, 0.2f, 0.34f);
            var overlap = ReadFloat(serialized, "mapSliceOverlapPercent", DefaultOverlapPercent, 0.04f, 0.08f);

            if (meadow + beach > 0.92f)
            {
                beach = 0.92f - meadow;
            }

            return new SliceSettings(meadow, beach, overlap);
        }

        private static float ReadFloat(SerializedObject serialized, string propertyName, float fallback, float min, float max)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? Mathf.Clamp(property.floatValue, min, max) : fallback;
        }

        private static Dictionary<string, Sprite> LoadPrimaryBackgroundSprite(List<string> report)
        {
            var sprites = new Dictionary<string, Sprite>();
            var folder = ResolveUiFolder();
            var path = $"{folder}/{BackgroundFileName}";
            if (!File.Exists(path))
            {
                report.Add($"WARNING: Missing {BackgroundFileName} in {folder}. Falling back to separate map backgrounds.");
                return sprites;
            }

            ConfigureSingleMapTexture(path, report);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                report.Add($"WARNING: Could not load Sprite for {BackgroundFileName} from {folder}. Falling back to separate map backgrounds.");
                return sprites;
            }

            sprites[BackgroundAsset.PropertyName] = sprite;
            report.Add($"Assigned {BackgroundAsset.FileName} to {BackgroundAsset.DisplayName} background.");
            return sprites;
        }

        private static Dictionary<string, Sprite> LoadSeparateMapSprites(string folder, List<string> report)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (var asset in SegmentAssets)
            {
                var path = $"{folder}/{asset.FileName}";
                if (!File.Exists(path))
                {
                    report.Add($"WARNING: Missing {asset.FileName} in {folder}. Existing {asset.DisplayName} placeholder was kept.");
                    continue;
                }

                ConfigureSingleMapTexture(path, report);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    report.Add($"WARNING: Could not load Sprite for {asset.FileName} from {folder}. Existing {asset.DisplayName} placeholder was kept.");
                    continue;
                }

                sprites[asset.PropertyName] = sprite;
                report.Add($"Assigned {asset.FileName} to {asset.DisplayName} segment.");
            }

            return sprites;
        }

        private static Dictionary<string, Sprite> LoadNodeSprites(string folder, List<string> report)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (var asset in NodeAssets)
            {
                var path = $"{folder}/{asset.FileName}";
                if (!File.Exists(path))
                {
                    report.Add($"WARNING: Missing {asset.FileName} in {folder}. Existing {asset.DisplayName} art was kept.");
                    continue;
                }

                ConfigureSingleMapTexture(path, report);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    report.Add($"WARNING: Could not load Sprite for {asset.FileName} from {folder}. Existing {asset.DisplayName} art was kept.");
                    continue;
                }

                sprites[asset.PropertyName] = sprite;
                report.Add($"Assigned {asset.FileName} to {asset.DisplayName} art.");
            }

            return sprites;
        }

        private static void ConfigureSingleMapTexture(string path, List<string> report)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.Add($"WARNING: Could not read TextureImporter for {path}.");
                return;
            }

            if (!TryReadPngSize(path, out var width, out var height))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                width = texture != null ? texture.width : 0;
                height = texture != null ? texture.height : 0;
            }

            if (width <= 0 || height <= 0)
            {
                report.Add($"WARNING: Could not read dimensions for {path}.");
                return;
            }

            var maxSize = ChooseMaxTextureSize(width, height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 65;

            ConfigurePlatform(importer, "DefaultTexturePlatform", maxSize);
            ConfigurePlatform(importer, "Android", maxSize);
            ConfigurePlatform(importer, "iPhone", maxSize);
            ForceSerializedImporterSettings(importer, maxSize, SpriteImportMode.Single);
            importer.SaveAndReimport();
        }

        private static void ConfigureAndSliceContinuousMap(string path, SliceSettings settings, List<string> report)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.Add($"WARNING: Could not read TextureImporter for {path}.");
                return;
            }

            if (!TryReadPngSize(path, out var width, out var height))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                width = texture != null ? texture.width : 0;
                height = texture != null ? texture.height : 0;
            }

            if (width <= 0 || height <= 0)
            {
                report.Add($"WARNING: Could not read dimensions for {path}.");
                return;
            }

            var maxSize = ChooseMaxTextureSize(width, height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 65;

            ConfigurePlatform(importer, "DefaultTexturePlatform", maxSize);
            ConfigurePlatform(importer, "Android", maxSize);
            ConfigurePlatform(importer, "iPhone", maxSize);
            ForceSerializedImporterSettings(importer, maxSize, SpriteImportMode.Multiple);
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.Add($"WARNING: Could not reload TextureImporter for {path} after applying base settings.");
                return;
            }

            SetSpriteRects(importer, width, height, settings);
            importer.SaveAndReimport();

            report.Add($"Sliced {ContinuousMapFileName}: Meadow bottom {settings.MeadowPercent:P0}, Beach middle {settings.BeachPercent:P0}, Desert top {(1f - settings.MeadowPercent - settings.BeachPercent):P0}, overlap {settings.OverlapPercent:P0}.");
        }

        private static void ForceSerializedImporterSettings(TextureImporter importer, int maxSize, SpriteImportMode spriteImportMode)
        {
            var serialized = new SerializedObject(importer);
            SetSerializedInt(serialized, "m_SpriteMode", (int)spriteImportMode);
            SetSerializedInt(serialized, "m_MaxTextureSize", maxSize);
            SetSerializedInt(serialized, "m_CompressionQuality", 65);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedInt(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetSpriteRects(TextureImporter importer, int width, int height, SliceSettings settings)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var existingIds = provider.GetSpriteRects()
                .GroupBy(rect => rect.name)
                .ToDictionary(group => group.Key, group => group.First().spriteID);

            var rects = BuildSpriteRects(width, height, settings, existingIds);
            provider.SetSpriteRects(rects);

            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                nameFileIdProvider.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)).ToList());
            }

            provider.Apply();
        }

        private static SpriteRect[] BuildSpriteRects(int width, int height, SliceSettings settings, Dictionary<string, GUID> existingIds)
        {
            var meadowBeachSplit = Mathf.RoundToInt(height * settings.MeadowPercent);
            var beachDesertSplit = Mathf.RoundToInt(height * (settings.MeadowPercent + settings.BeachPercent));
            var halfOverlap = Mathf.Max(1, Mathf.RoundToInt(height * settings.OverlapPercent * 0.5f));

            var meadowEnd = Mathf.Clamp(meadowBeachSplit + halfOverlap, 1, height);
            var beachStart = Mathf.Clamp(meadowBeachSplit - halfOverlap, 0, height - 1);
            var beachEnd = Mathf.Clamp(beachDesertSplit + halfOverlap, beachStart + 1, height);
            var desertStart = Mathf.Clamp(beachDesertSplit - halfOverlap, 0, height - 1);

            return new[]
            {
                CreateSpriteRect(Slices[0].SpriteName, new Rect(0f, 0f, width, meadowEnd), existingIds),
                CreateSpriteRect(Slices[1].SpriteName, new Rect(0f, beachStart, width, beachEnd - beachStart), existingIds),
                CreateSpriteRect(Slices[2].SpriteName, new Rect(0f, desertStart, width, height - desertStart), existingIds)
            };
        }

        private static SpriteRect CreateSpriteRect(string name, Rect rect, Dictionary<string, GUID> existingIds)
        {
            return new SpriteRect
            {
                name = name,
                spriteID = existingIds.TryGetValue(name, out var existingId) ? existingId : GUID.Generate(),
                rect = rect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        private static Dictionary<string, Sprite> LoadContinuousSliceSprites(string path, List<string> report)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var allSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            var sprites = new Dictionary<string, Sprite>();

            foreach (var slice in Slices)
            {
                var sprite = allSprites.FirstOrDefault(candidate => candidate.name == slice.SpriteName);
                if (sprite == null)
                {
                    report.Add($"WARNING: Generated slice sprite missing after import: {slice.SpriteName} from {path}. Existing {slice.DisplayName} placeholder was kept.");
                    continue;
                }

                sprites[slice.PropertyName] = sprite;
                report.Add($"Assigned {slice.SpriteName} to {slice.DisplayName} segment.");
            }

            return sprites;
        }

        private static void ConfigurePlatform(TextureImporter importer, string platform, int maxSize)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 65;
            importer.SetPlatformTextureSettings(settings);
        }

        private static int ChooseMaxTextureSize(int width, int height)
        {
            return Mathf.Max(width, height) <= 2048 ? 2048 : 4096;
        }

        private static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 ||
                bytes[0] != 0x89 ||
                bytes[1] != 0x50 ||
                bytes[2] != 0x4E ||
                bytes[3] != 0x47)
            {
                return false;
            }

            width = ReadBigEndianInt(bytes, 16);
            height = ReadBigEndianInt(bytes, 20);
            return width > 0 && height > 0;
        }

        private static int ReadBigEndianInt(byte[] bytes, int index)
        {
            return (bytes[index] << 24) |
                   (bytes[index + 1] << 16) |
                   (bytes[index + 2] << 8) |
                   bytes[index + 3];
        }

        private static bool AssignSpritesToMap(LevelMapUI map, Dictionary<string, Sprite> sprites)
        {
            if (map == null || sprites.Count == 0)
            {
                return false;
            }

            var serialized = new SerializedObject(map);
            var changed = false;
            foreach (var spriteReference in sprites)
            {
                changed |= SetObjectReference(serialized, spriteReference.Key, spriteReference.Value);
                if (spriteReference.Key == "backgroundSprite")
                {
                    var useSingleBackgroundProperty = serialized.FindProperty("useSingleBackgroundSprite");
                    if (useSingleBackgroundProperty != null && !useSingleBackgroundProperty.boolValue)
                    {
                        useSingleBackgroundProperty.boolValue = true;
                        changed = true;
                    }
                }

                if (spriteReference.Key == "mapMeadowSprite")
                {
                    var backgroundProperty = serialized.FindProperty("backgroundSprite");
                    if (backgroundProperty != null && backgroundProperty.objectReferenceValue == null)
                    {
                        changed |= SetObjectReference(serialized, "backgroundSprite", spriteReference.Value);
                    }
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(map);
            }

            return changed;
        }

        private static bool SetObjectReference(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static bool UpdateMapPrefabs(Dictionary<string, Sprite> sprites)
        {
            var changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var map = prefabRoot.GetComponentInChildren<LevelMapUI>(true);
                    if (map == null)
                    {
                        continue;
                    }

                    if (AssignSpritesToMap(map, sprites))
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        changed = true;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return changed;
        }
    }
}
