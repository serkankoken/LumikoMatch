using System.IO;
using CharacterMatch3.Board;
using CharacterMatch3.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    public sealed class LevelToolsWindow : EditorWindow
    {
        [MenuItem("Character Match-3/Level Tools")]
        public static void Open()
        {
            GetWindow<LevelToolsWindow>("Level Tools");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Character Match-3 Level Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Create Character Catalog"))
            {
                CharacterCatalogGenerator.CreateOrUpdateCatalog(true);
            }

            if (GUILayout.Button("Generate Missing Level Assets"))
            {
                var levels = LevelFactory.GenerateAllLevels(false);
                LevelFactory.CreateOrUpdateLevelLibrary(levels);
            }

            if (GUILayout.Button("Setup Complete Game"))
            {
                CharacterMatch3Setup.SetupCompleteGame();
            }

            EditorGUILayout.Space();
            var selected = Selection.activeObject as LevelDefinition;
            EditorGUILayout.LabelField("Selected Level", selected != null ? selected.name : "None");

            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("Open Selected Level"))
                {
                    Selection.activeObject = selected;
                    EditorGUIUtility.PingObject(selected);
                }

                if (GUILayout.Button("Validate Selected Level"))
                {
                    LevelValidator.Validate(selected).Log(selected);
                }

                if (GUILayout.Button("Play Selected Level"))
                {
                    PlaySelectedLevel(selected);
                }

                if (GUILayout.Button("Generate Board Preview"))
                {
                    GeneratePreview(selected);
                }

                if (GUILayout.Button("Find Initial Matches"))
                {
                    var model = new BoardGenerator(selected).Generate();
                    Debug.Log($"Level {selected.levelNumber:000}: initial matches found = {MatchFinder.FindMatches(model).Count}", selected);
                }

                if (GUILayout.Button("Find Legal Moves"))
                {
                    var model = new BoardGenerator(selected).Generate();
                    Debug.Log($"Level {selected.levelNumber:000}: legal moves found = {MoveFinder.FindLegalMoves(model).Count}", selected);
                }

                if (GUILayout.Button("Check Companion Paths"))
                {
                    var report = LevelValidator.Validate(selected);
                    foreach (var error in report.Errors)
                    {
                        if (error.Contains("Companion"))
                        {
                            Debug.LogError(error, selected);
                        }
                    }
                }

                if (GUILayout.Button("Estimate Difficulty"))
                {
                    Debug.Log($"Level {selected.levelNumber:000} difficulty estimate: {LevelValidator.EstimateDifficulty(selected):0.0}", selected);
                }

                if (GUILayout.Button("Regenerate Level Seed"))
                {
                    Undo.RecordObject(selected, "Regenerate Character Match-3 Level Seed");
                    selected.randomSeed = Random.Range(1000, 999999);
                    EditorUtility.SetDirty(selected);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button("Validate All 50 Levels"))
            {
                ValidateAll();
            }
        }

        private static void ValidateAll()
        {
            var valid = 0;
            for (var number = CharacterMatch3Constants.FirstLevel; number <= CharacterMatch3Constants.LastLevel; number++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{CharacterMatch3Constants.RootPath}/Levels/Level_{number:000}.asset");
                var report = LevelValidator.Validate(level);
                report.Log(level);
                if (report.IsValid)
                {
                    valid++;
                }
            }

            Debug.Log($"Character Match-3 validation complete: {valid}/50 levels passed.");
        }

        private static void PlaySelectedLevel(LevelDefinition selected)
        {
            if (selected == null)
            {
                return;
            }

            GameState.SelectedLevelNumber = selected.levelNumber;
            var gameplayScene = $"{CharacterMatch3Constants.RootPath}/Scenes/Gameplay.unity";
            if (!File.Exists(gameplayScene))
            {
                CharacterMatch3Setup.SetupCompleteGame();
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(gameplayScene);
                EditorApplication.isPlaying = true;
            }
        }

        private static void GeneratePreview(LevelDefinition level)
        {
            var model = new BoardGenerator(level).Generate();
            var cellSize = 48;
            var texture = new Texture2D(level.boardWidth * cellSize, level.boardHeight * cellSize, TextureFormat.RGBA32, false);
            var colors = new[]
            {
                new Color(1f, 0.45f, 0.42f),
                new Color(0.55f, 0.73f, 1f),
                new Color(0.45f, 0.86f, 0.42f),
                new Color(0.45f, 0.9f, 1f),
                new Color(1f, 0.72f, 0.35f)
            };

            for (var y = 0; y < level.boardHeight; y++)
            {
                for (var x = 0; x < level.boardWidth; x++)
                {
                    var cell = model.GetCell(x, y);
                    var color = !cell.Active
                        ? new Color(0f, 0f, 0f, 0.2f)
                        : cell.CrateLayers > 0
                            ? new Color(0.45f, 0.25f, 0.14f)
                            : cell.Piece == null
                                ? Color.gray
                                : colors[(int)cell.Piece.Character];

                    for (var py = 0; py < cellSize; py++)
                    {
                        for (var px = 0; px < cellSize; px++)
                        {
                            texture.SetPixel(x * cellSize + px, y * cellSize + py, color);
                        }
                    }
                }
            }

            texture.Apply();
            var folder = $"{CharacterMatch3Constants.RootPath}/Art/Previews";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder($"{CharacterMatch3Constants.RootPath}/Art", "Previews");
            }

            var path = $"{folder}/Level_{level.levelNumber:000}_Preview.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            Debug.Log($"Saved board preview to {path}", level);
        }
    }
}
