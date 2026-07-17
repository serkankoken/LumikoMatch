using System.Collections.Generic;
using System.IO;
using CharacterMatch3.Core;
using CharacterMatch3.Input;
using CharacterMatch3.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CharacterMatch3.Editor
{
    public static class CharacterMatch3Setup
    {
        private static readonly string[] Folders =
        {
            "Art",
            "Audio",
            "Data",
            "Editor",
            "Effects",
            "Levels",
            "Materials",
            "Prefabs",
            "Scenes",
            "Scripts",
            "Scripts/Board",
            "Scripts/Core",
            "Scripts/Data",
            "Scripts/Goals",
            "Scripts/Input",
            "Scripts/Pieces",
            "Scripts/Save",
            "Scripts/Tutorial",
            "Scripts/UI",
            "Scripts/Utilities"
        };

        [MenuItem("Character Match-3/Setup Complete Game")]
        public static void SetupCompleteGame()
        {
            EnsureFolders();
            var catalog = CharacterCatalogGenerator.CreateOrUpdateCatalog(false);
            var scoring = CreateOrUpdateScoringConfig();
            var inputSettings = CreateOrUpdateInputSettings();
            var levels = LevelFactory.GenerateAllLevels(true);
            var library = LevelFactory.CreateOrUpdateLevelLibrary(levels);
            CreateMaterials();
            CreatePrefabs();
            CreateScenes(catalog, library, scoring, inputSettings);
            ConfigureBuildSettings();
            ConfigurePlayerSettings();
            ValidateAll(levels);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Character Match-3 setup complete. Use Boot.unity or LevelMap.unity to start playing.");
        }

        public static void RunHeadlessSetup()
        {
            SetupCompleteGame();
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CharacterMatch3"))
            {
                AssetDatabase.CreateFolder("Assets", "CharacterMatch3");
            }

            foreach (var folder in Folders)
            {
                EnsureFolder($"{CharacterMatch3Constants.RootPath}/{folder}");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static ScoringConfig CreateOrUpdateScoringConfig()
        {
            var scoring = AssetDatabase.LoadAssetAtPath<ScoringConfig>(CharacterMatch3Constants.ScoringPath);
            if (scoring == null)
            {
                scoring = ScriptableObject.CreateInstance<ScoringConfig>();
                AssetDatabase.CreateAsset(scoring, CharacterMatch3Constants.ScoringPath);
            }

            EditorUtility.SetDirty(scoring);
            return scoring;
        }

        private static Match3InputSettings CreateOrUpdateInputSettings()
        {
            var inputSettings = AssetDatabase.LoadAssetAtPath<Match3InputSettings>(CharacterMatch3Constants.InputSettingsPath);
            if (inputSettings == null)
            {
                inputSettings = ScriptableObject.CreateInstance<Match3InputSettings>();
                AssetDatabase.CreateAsset(inputSettings, CharacterMatch3Constants.InputSettingsPath);
            }

            EditorUtility.SetDirty(inputSettings);
            return inputSettings;
        }

        private static void CreateMaterials()
        {
            CreateMaterial("BoardCell", new Color(1f, 1f, 1f, 0.35f));
            CreateMaterial("SoftCover", new Color(0.55f, 0.9f, 0.95f, 0.65f));
            CreateMaterial("Crate", new Color(0.45f, 0.24f, 0.12f, 1f));
            CreateMaterial("RainbowOverlay", new Color(1f, 0.85f, 0.25f, 1f));
        }

        private static void CreateMaterial(string name, Color color)
        {
            var path = $"{CharacterMatch3Constants.RootPath}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
        }

        private static void CreatePrefabs()
        {
            CreateBoardCellPrefab();
            CreatePiecePrefab();
            CreateFloatingTextPrefab();
        }

        private static void CreateBoardCellPrefab()
        {
            var path = $"{CharacterMatch3Constants.RootPath}/Prefabs/BoardCell.prefab";
            var root = new GameObject("BoardCell", typeof(RectTransform), typeof(Image), typeof(CharacterMatch3.Board.BoardCellView));
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreatePiecePrefab()
        {
            var path = $"{CharacterMatch3Constants.RootPath}/Prefabs/PieceView.prefab";
            var root = new GameObject("PieceView", typeof(RectTransform), typeof(Image));
            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateFloatingTextPrefab()
        {
            var path = $"{CharacterMatch3Constants.RootPath}/Prefabs/FloatingText.prefab";
            var root = new GameObject("FloatingText", typeof(RectTransform), typeof(Text));
            var text = root.GetComponent<Text>();
            text.font = UIFactory.DefaultFont;
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateScenes(CharacterCatalog catalog, LevelLibrary library, ScoringConfig scoring, Match3InputSettings inputSettings)
        {
            CreateBootScene();
            CreateLevelMapScene(catalog, library);
            CreateGameplayScene(catalog, library, scoring, inputSettings);
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("GameBootstrap", typeof(GameBootstrap));
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, $"{CharacterMatch3Constants.RootPath}/Scenes/Boot.unity");
        }

        private static void CreateLevelMapScene(CharacterCatalog catalog, LevelLibrary library)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            var root = new GameObject("LevelMap", typeof(LevelMapUI));
            root.GetComponent<LevelMapUI>().Configure(library, catalog);
            EditorUtility.SetDirty(root);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, $"{CharacterMatch3Constants.RootPath}/Scenes/LevelMap.unity");
        }

        private static void CreateGameplayScene(CharacterCatalog catalog, LevelLibrary library, ScoringConfig scoring, Match3InputSettings inputSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            var root = new GameObject("GameSession", typeof(GameSession));
            root.GetComponent<GameSession>().Configure(catalog, library, scoring, inputSettings);
            EditorUtility.SetDirty(root);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, $"{CharacterMatch3Constants.RootPath}/Scenes/Gameplay.unity");
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.19f, 0.24f);
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void ConfigureBuildSettings()
        {
            var scenes = new[]
            {
                $"{CharacterMatch3Constants.RootPath}/Scenes/Boot.unity",
                $"{CharacterMatch3Constants.RootPath}/Scenes/LevelMap.unity",
                $"{CharacterMatch3Constants.RootPath}/Scenes/Gameplay.unity"
            };

            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (var scene in scenes)
            {
                buildScenes.Add(new EditorBuildSettingsScene(scene, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        private static void ValidateAll(List<LevelDefinition> levels)
        {
            var valid = 0;
            foreach (var level in levels)
            {
                var report = LevelValidator.Validate(level);
                if (report.IsValid)
                {
                    valid++;
                }
                else
                {
                    report.Log(level);
                }
            }

            Debug.Log($"Character Match-3 setup validation: {valid}/{levels.Count} levels passed.");
        }
    }
}
