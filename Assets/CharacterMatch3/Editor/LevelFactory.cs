using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    public static class LevelFactory
    {
        [MenuItem("Character Match-3/Generate Missing Level Assets")]
        public static void GenerateMissingLevelAssetsMenu()
        {
            GenerateAllLevels(false);
        }

        public static List<LevelDefinition> GenerateAllLevels(bool overwriteExisting)
        {
            CharacterMatch3Setup.EnsureFolders();
            var levels = new List<LevelDefinition>();

            for (var number = CharacterMatch3Constants.FirstLevel; number <= CharacterMatch3Constants.LastLevel; number++)
            {
                var path = $"{CharacterMatch3Constants.RootPath}/Levels/Level_{number:000}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, path);
                }

                if (overwriteExisting || string.IsNullOrEmpty(level.displayName) || level.levelNumber != number)
                {
                    Populate(level, number);
                    EditorUtility.SetDirty(level);
                }

                levels.Add(level);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return levels;
        }

        public static LevelLibrary CreateOrUpdateLevelLibrary(List<LevelDefinition> levels)
        {
            var library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(CharacterMatch3Constants.LevelLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<LevelLibrary>();
                AssetDatabase.CreateAsset(library, CharacterMatch3Constants.LevelLibraryPath);
            }

            library.SetLevels(levels);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static void Populate(LevelDefinition level, int number)
        {
            var hardCheckpoint = number is 20 or 25 or 30 or 35 or 40 or 45 or 49 or 50;
            var normalCheckpoint = number is 10 or 15 or 24 or 34 or 44 or 47 or 48;

            level.levelNumber = number;
            level.displayName = $"Level {number:000}";
            level.difficultyLabel = hardCheckpoint ? DifficultyLabel.Hard : normalCheckpoint ? DifficultyLabel.Normal : DifficultyLabel.Easy;
            level.backgroundThemeId = number switch
            {
                <= 6 => "meadow",
                <= 12 => "beach",
                _ => "desert"
            };

            var width = number < 31 ? 7 : number < 41 ? 8 : 9;
            var height = number < 21 ? 7 : number < 36 ? 8 : 9;
            SetBoard(level, width, height);
            ApplyMask(level, number);

            level.availableCharacterTypes.Clear();
            level.availableCharacterTypes.Add(CharacterType.Cat);
            level.availableCharacterTypes.Add(CharacterType.Bunny);
            level.availableCharacterTypes.Add(CharacterType.Dino);
            level.availableCharacterTypes.Add(CharacterType.Penguin);
            if (number >= 3)
            {
                level.availableCharacterTypes.Add(CharacterType.Bear);
            }

            level.randomSeed = 7000 + number * 37;
            level.moveLimit = hardCheckpoint ? 24 : normalCheckpoint ? 26 : 30;
            if (number <= 5)
            {
                level.moveLimit = 24;
            }

            level.reshufflingAllowed = true;
            level.maximumAutomaticReshuffleAttempts = 10;
            level.goals.Clear();
            level.softCoverPlacements.Clear();
            level.cratePlacements.Clear();
            level.lockPlacements.Clear();
            level.companionTokenStartingPositions.Clear();
            level.companionExitCells.Clear();
            level.prePlacedNormalPieces.Clear();
            level.prePlacedSpecialPieces.Clear();
            level.tutorialInstructions = string.Empty;
            level.tutorialForcedSwap = default;

            AddProgressionContent(level, number);

            var active = level.ActiveCellCount();
            level.oneStarScore = Mathf.RoundToInt(active * 55f + number * 220f);
            level.twoStarScore = Mathf.RoundToInt(level.oneStarScore * 1.8f);
            level.threeStarScore = Mathf.RoundToInt(level.oneStarScore * 2.75f);
            level.EnsureDefaults();
        }

        private static void AddProgressionContent(LevelDefinition level, int number)
        {
            if (number <= 5)
            {
                AddBasicLevel(level, number);
                return;
            }

            if (number <= 10)
            {
                AddSoftCoverLevel(level, number);
                return;
            }

            if (number <= 15)
            {
                AddCrateAndBurstLevel(level, number);
                return;
            }

            if (number <= 20)
            {
                AddMixedCoverLevel(level, number);
                return;
            }

            if (number <= 25)
            {
                AddCompanionLevel(level, number);
                return;
            }

            if (number <= 30)
            {
                AddLockLevel(level, number);
                return;
            }

            if (number <= 35)
            {
                AddIrregularLevel(level, number);
                return;
            }

            if (number <= 40)
            {
                AddSpecialComboLevel(level, number);
                return;
            }

            if (number <= 45)
            {
                AddMultiGoalLevel(level, number);
                return;
            }

            AddFinaleLevel(level, number);
        }

        private static void AddBasicLevel(LevelDefinition level, int number)
        {
            switch (number)
            {
                case 1:
                    level.goals.Add(new LevelGoalData(CharacterType.Cat, 8));
                    AddNormal(level, 2, 3, CharacterType.Cat);
                    AddNormal(level, 4, 3, CharacterType.Cat);
                    AddNormal(level, 3, 4, CharacterType.Cat);
                    AddTutorial(level, "Swap two neighbors to make three matching character heads.", 3, 4, 3, 3);
                    break;
                case 2:
                    level.goals.Add(new LevelGoalData(CharacterType.Cat, 10));
                    level.goals.Add(new LevelGoalData(CharacterType.Bunny, 10));
                    level.tutorialInstructions = "Cascades count too. A single move can collect more than one goal.";
                    break;
                case 3:
                    level.goals.Add(new LevelGoalData(CharacterType.Dino, 14));
                    AddNormal(level, 1, 3, CharacterType.Cat);
                    AddNormal(level, 2, 3, CharacterType.Cat);
                    AddNormal(level, 4, 3, CharacterType.Cat);
                    AddNormal(level, 3, 4, CharacterType.Cat);
                    AddTutorial(level, "Match four in a row to create a Line Piece.", 3, 4, 3, 3);
                    break;
                case 4:
                    level.goals.Add(new LevelGoalData(CharacterType.Dino, 12));
                    level.goals.Add(new LevelGoalData(CharacterType.Penguin, 12));
                    level.moveLimit = 26;
                    break;
                default:
                    level.goals.Add(new LevelGoalData(CharacterType.Bear, 12));
                    AddNormal(level, 0, 3, CharacterType.Bear);
                    AddNormal(level, 1, 3, CharacterType.Bear);
                    AddNormal(level, 3, 3, CharacterType.Bear);
                    AddNormal(level, 4, 3, CharacterType.Bear);
                    AddNormal(level, 2, 4, CharacterType.Bear);
                    AddTutorial(level, "Match five in a straight line to create a Rainbow Piece.", 2, 4, 2, 3);
                    break;
            }
        }

        private static void AddSoftCoverLevel(LevelDefinition level, int number)
        {
            var radius = number <= 7 ? 1 : 2;
            AddSoftCoverBox(level, level.boardWidth / 2, level.boardHeight / 2, radius, number >= 10 ? 2 : 1);
            if (number >= 8)
            {
                AddSoftCover(level, 1, 1, 1);
                AddSoftCover(level, level.boardWidth - 2, 1, 1);
                AddSoftCover(level, 1, level.boardHeight - 2, 1);
                AddSoftCover(level, level.boardWidth - 2, level.boardHeight - 2, 1);
            }

            level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
            if (number == 7)
            {
                level.goals.Add(new LevelGoalData(CharacterType.Cat, 12));
            }

            if (number == 6)
            {
                level.tutorialInstructions = "Soft Cover sits under pieces. Match or hit that cell to clear it.";
            }
        }

        private static void AddCrateAndBurstLevel(LevelDefinition level, int number)
        {
            if (number == 11)
            {
                AddCrateBox(level, 3, 3, 1, 1);
                level.tutorialInstructions = "Crates block cells. Break them by matching next to them or hitting them with specials.";
            }
            else if (number == 12)
            {
                AddNormal(level, 2, 3, CharacterType.Bunny);
                AddNormal(level, 4, 3, CharacterType.Bunny);
                AddNormal(level, 3, 2, CharacterType.Bunny);
                AddNormal(level, 3, 4, CharacterType.Bunny);
                AddNormal(level, 3, 5, CharacterType.Bunny);
                AddTutorial(level, "T and L matches create Burst Pieces.", 3, 5, 3, 3);
            }
            else if (number == 13)
            {
                for (var y = 1; y < level.boardHeight - 1; y++)
                {
                    AddCrate(level, level.boardWidth / 2, y, 1);
                }
            }
            else if (number == 14)
            {
                AddCrateBox(level, 3, 3, 1, 1);
                level.goals.Add(new LevelGoalData(CharacterType.Penguin, 14));
            }
            else
            {
                AddCrateBox(level, 3, 3, 1, 2);
                AddSpecial(level, 1, 3, PieceKind.Line, CharacterType.Cat, LineOrientation.Horizontal);
                AddSpecial(level, 5, 3, PieceKind.Burst, CharacterType.Bunny, LineOrientation.Horizontal);
            }

            level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
        }

        private static void AddMixedCoverLevel(LevelDefinition level, int number)
        {
            AddSoftCoverBox(level, 3, 3, 2, number >= 16 ? 2 : 1);
            if (number >= 18)
            {
                AddCrate(level, 2, 3, 1);
                AddCrate(level, 4, 3, 1);
                AddCrate(level, 3, 2, 1);
                AddCrate(level, 3, 4, 1);
            }

            level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
            if (level.cratePlacements.Count > 0)
            {
                level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
            }

            if (number == 19)
            {
                level.goals.Add(new LevelGoalData(CharacterType.Cat, 16));
                level.goals.Add(new LevelGoalData(CharacterType.Bear, 14));
            }
        }

        private static void AddCompanionLevel(LevelDefinition level, int number)
        {
            var x = level.boardWidth / 2;
            AddCompanion(level, x, level.boardHeight - 1, x, 0);
            if (number >= 22)
            {
                AddCrate(level, Mathf.Max(1, x - 1), 3, 1);
            }

            if (number >= 23)
            {
                AddCompanion(level, 1, level.boardHeight - 1, 1, 0);
                AddExit(level, level.boardWidth - 2, 0);
            }

            if (number >= 24)
            {
                level.goals.Add(new LevelGoalData(CharacterType.Dino, 16));
            }

            if (number >= 25)
            {
                AddSoftCoverBox(level, x, 2, 1, 1);
                AddCrate(level, x + 1, 4, 1);
                level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
                level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
            }

            level.goals.Add(new LevelGoalData(GoalType.DropCompanions, level.companionTokenStartingPositions.Count));
            if (number == 21)
            {
                level.tutorialInstructions = "Guide Companion Tokens to an EXIT cell at the bottom.";
            }
        }

        private static void AddLockLevel(LevelDefinition level, int number)
        {
            AddLock(level, 3, 3, number >= 30 ? 2 : 1);
            AddNormal(level, 3, 3, CharacterType.Cat);
            if (number >= 27)
            {
                AddLock(level, 2, 3, 1);
                AddLock(level, 4, 3, 1);
            }

            if (number >= 29)
            {
                AddCompanion(level, 1, level.boardHeight - 1, 1, 0);
            }

            if (number >= 30)
            {
                AddCrateBox(level, 4, 4, 1, 1);
                AddSoftCoverBox(level, 2, 2, 1, 1);
                level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
                level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
            }

            level.goals.Add(new LevelGoalData(CharacterType.Cat, 16));
            if (level.companionTokenStartingPositions.Count > 0)
            {
                level.goals.Add(new LevelGoalData(GoalType.DropCompanions, 1));
            }

            if (number == 26)
            {
                level.tutorialInstructions = "Character Locks hold a piece in place until matches or specials break the lock.";
            }
        }

        private static void AddIrregularLevel(LevelDefinition level, int number)
        {
            level.goals.Add(new LevelGoalData(CharacterType.Bunny, 18));
            if (number >= 34)
            {
                AddSoftCover(level, 1, 1, 2);
                AddSoftCover(level, level.boardWidth - 2, level.boardHeight - 2, 2);
                level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
            }

            if (number >= 35)
            {
                AddCrateBox(level, level.boardWidth / 2, level.boardHeight / 2, 1, 1);
                level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
            }
        }

        private static void AddSpecialComboLevel(LevelDefinition level, int number)
        {
            var cx = level.boardWidth / 2;
            var cy = level.boardHeight / 2;
            switch (number)
            {
                case 36:
                    AddSpecial(level, cx, cy, PieceKind.Line, CharacterType.Cat, LineOrientation.Horizontal);
                    AddSpecial(level, cx + 1, cy, PieceKind.Line, CharacterType.Bunny, LineOrientation.Vertical);
                    level.tutorialInstructions = "Swap two Line Pieces to clear a row and a column.";
                    break;
                case 37:
                    AddSpecial(level, cx, cy, PieceKind.Line, CharacterType.Cat, LineOrientation.Horizontal);
                    AddSpecial(level, cx + 1, cy, PieceKind.Burst, CharacterType.Bunny, LineOrientation.Horizontal);
                    level.tutorialInstructions = "Line plus Burst clears a wide cross.";
                    break;
                case 38:
                    AddSpecial(level, cx, cy, PieceKind.Burst, CharacterType.Cat, LineOrientation.Horizontal);
                    AddSpecial(level, cx + 1, cy, PieceKind.Burst, CharacterType.Bunny, LineOrientation.Horizontal);
                    break;
                case 39:
                    AddSpecial(level, cx, cy, PieceKind.Rainbow, CharacterType.Cat, LineOrientation.Horizontal);
                    AddNormal(level, cx + 1, cy, CharacterType.Dino);
                    level.tutorialInstructions = "Rainbow plus a character removes every piece of that character.";
                    break;
                default:
                    AddSpecial(level, cx, cy, PieceKind.Rainbow, CharacterType.Cat, LineOrientation.Horizontal);
                    AddSpecial(level, cx + 1, cy, PieceKind.Line, CharacterType.Dino, LineOrientation.Vertical);
                    AddCrateBox(level, cx, cy - 2, 1, 1);
                    level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
                    break;
            }

            level.goals.Add(new LevelGoalData(CharacterType.Dino, 18));
            if (number is 36 or 37 or 39)
            {
                AddTutorial(level, level.tutorialInstructions, cx, cy, cx + 1, cy);
            }
        }

        private static void AddMultiGoalLevel(LevelDefinition level, int number)
        {
            AddSoftCoverBox(level, level.boardWidth / 2, 3, 1, 1);
            AddCrate(level, 2, 4, 1);
            AddCrate(level, level.boardWidth - 3, 4, 1);

            switch (number)
            {
                case 41:
                    level.goals.Add(new LevelGoalData(CharacterType.Cat, 18));
                    level.goals.Add(new LevelGoalData(CharacterType.Penguin, 18));
                    level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
                    break;
                case 42:
                    AddCompanion(level, 1, level.boardHeight - 1, 1, 0);
                    level.goals.Add(new LevelGoalData(GoalType.DropCompanions, 1));
                    level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
                    break;
                case 43:
                    AddLock(level, 3, 5, 1);
                    AddNormal(level, 3, 5, CharacterType.Bear);
                    level.goals.Add(new LevelGoalData(CharacterType.Bear, 18));
                    level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
                    break;
                case 44:
                    level.goals.Add(new LevelGoalData(GoalType.ReachScore, level.twoStarScore));
                    level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
                    break;
                default:
                    AddCompanion(level, 1, level.boardHeight - 1, 1, 0);
                    AddLock(level, 3, 5, 1);
                    AddNormal(level, 3, 5, CharacterType.Bear);
                    level.goals.Add(new LevelGoalData(CharacterType.Cat, 18));
                    level.goals.Add(new LevelGoalData(GoalType.DropCompanions, 1));
                    level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
                    break;
            }
        }

        private static void AddFinaleLevel(LevelDefinition level, int number)
        {
            if (number == 46)
            {
                level.difficultyLabel = DifficultyLabel.Easy;
                level.moveLimit = 30;
                AddSpecial(level, 2, 4, PieceKind.Line, CharacterType.Cat, LineOrientation.Horizontal);
                AddSpecial(level, 5, 4, PieceKind.Burst, CharacterType.Bunny, LineOrientation.Horizontal);
                level.goals.Add(new LevelGoalData(CharacterType.Cat, 18));
                return;
            }

            AddSoftCoverBox(level, level.boardWidth / 2, level.boardHeight / 2, 2, number >= 49 ? 2 : 1);
            AddCrateBox(level, level.boardWidth / 2, level.boardHeight / 2, 1, number >= 49 ? 2 : 1);
            AddLock(level, 2, level.boardHeight - 3, 1);
            AddNormal(level, 2, level.boardHeight - 3, CharacterType.Penguin);
            AddCompanion(level, 1, level.boardHeight - 1, 1, 0);
            if (number >= 48)
            {
                AddCompanion(level, level.boardWidth - 2, level.boardHeight - 1, level.boardWidth - 2, 0);
            }

            if (number == 50)
            {
                level.displayName = "Level 050: Lantern Finale";
                AddSpecial(level, level.boardWidth / 2 - 1, level.boardHeight / 2 + 2, PieceKind.Rainbow, CharacterType.Cat, LineOrientation.Horizontal);
                AddSpecial(level, level.boardWidth / 2, level.boardHeight / 2 + 2, PieceKind.Burst, CharacterType.Bear, LineOrientation.Horizontal);
                level.moveLimit = 28;
                level.tutorialInstructions = "Finale: use the familiar tools together. No new rules, just sharper choices.";
            }

            level.goals.Add(new LevelGoalData(GoalType.ClearSoftCover, SumLayers(level.softCoverPlacements)));
            level.goals.Add(new LevelGoalData(GoalType.BreakCrates, SumLayers(level.cratePlacements)));
            level.goals.Add(new LevelGoalData(GoalType.DropCompanions, level.companionTokenStartingPositions.Count));
        }

        private static void SetBoard(LevelDefinition level, int width, int height)
        {
            level.boardWidth = width;
            level.boardHeight = height;
            level.activeCells = new bool[width * height];
            for (var i = 0; i < level.activeCells.Length; i++)
            {
                level.activeCells[i] = true;
            }
        }

        private static void ApplyMask(LevelDefinition level, int number)
        {
            if (number is 4 or 8 or 31)
            {
                Deactivate(level, 0, 0);
                Deactivate(level, level.boardWidth - 1, 0);
                Deactivate(level, 0, level.boardHeight - 1);
                Deactivate(level, level.boardWidth - 1, level.boardHeight - 1);
            }

            if (number is 20 or 32 or 40 or 50)
            {
                for (var y = 0; y < level.boardHeight; y++)
                {
                    if (y < level.boardHeight / 2 - 1 || y > level.boardHeight / 2 + 1)
                    {
                        Deactivate(level, level.boardWidth / 2, y);
                    }
                }
            }

            if (number is 33 or 34)
            {
                for (var y = 0; y < level.boardHeight; y++)
                {
                    for (var x = 0; x < level.boardWidth; x++)
                    {
                        if (x + y < 2 || x + y > level.boardWidth + level.boardHeight - 4)
                        {
                            Deactivate(level, x, y);
                        }
                    }
                }
            }

            if (number is 35 or 47 or 49)
            {
                Deactivate(level, 0, 1);
                Deactivate(level, 1, 0);
                Deactivate(level, level.boardWidth - 1, level.boardHeight - 2);
                Deactivate(level, level.boardWidth - 2, level.boardHeight - 1);
            }
        }

        private static void Deactivate(LevelDefinition level, int x, int y)
        {
            if (level.IsInside(x, y))
            {
                level.activeCells[level.GetIndex(x, y)] = false;
            }
        }

        private static void AddTutorial(LevelDefinition level, string text, int fromX, int fromY, int toX, int toY)
        {
            level.tutorialInstructions = text;
            level.tutorialForcedSwap = new TutorialSwapData
            {
                enabled = true,
                from = new BoardCoordinate(fromX, fromY),
                to = new BoardCoordinate(toX, toY)
            };
        }

        private static void AddNormal(LevelDefinition level, int x, int y, CharacterType character)
        {
            if (level.IsActive(x, y))
            {
                level.prePlacedNormalPieces.Add(new PiecePlacement(x, y, character));
            }
        }

        private static void AddSpecial(LevelDefinition level, int x, int y, PieceKind kind, CharacterType character, LineOrientation orientation)
        {
            if (level.IsActive(x, y))
            {
                level.prePlacedSpecialPieces.Add(new PiecePlacement(x, y, kind, character, orientation));
            }
        }

        private static void AddSoftCover(LevelDefinition level, int x, int y, int layers)
        {
            if (level.IsActive(x, y))
            {
                level.softCoverPlacements.Add(new CellLayerData(x, y, layers));
            }
        }

        private static void AddSoftCoverBox(LevelDefinition level, int cx, int cy, int radius, int layers)
        {
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    AddSoftCover(level, x, y, layers);
                }
            }
        }

        private static void AddCrate(LevelDefinition level, int x, int y, int layers)
        {
            if (level.IsActive(x, y))
            {
                level.cratePlacements.Add(new CellLayerData(x, y, layers));
            }
        }

        private static void AddCrateBox(LevelDefinition level, int cx, int cy, int radius, int layers)
        {
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    AddCrate(level, x, y, layers);
                }
            }
        }

        private static void AddLock(LevelDefinition level, int x, int y, int layers)
        {
            if (level.IsActive(x, y))
            {
                level.lockPlacements.Add(new CellLayerData(x, y, layers));
            }
        }

        private static void AddCompanion(LevelDefinition level, int startX, int startY, int exitX, int exitY)
        {
            if (level.IsActive(startX, startY))
            {
                level.companionTokenStartingPositions.Add(new BoardCoordinate(startX, startY));
            }

            AddExit(level, exitX, exitY);
        }

        private static void AddExit(LevelDefinition level, int x, int y)
        {
            if (level.IsActive(x, y) && !level.companionExitCells.Contains(new BoardCoordinate(x, y)))
            {
                level.companionExitCells.Add(new BoardCoordinate(x, y));
            }
        }

        private static int SumLayers(List<CellLayerData> placements)
        {
            var total = 0;
            foreach (var placement in placements)
            {
                total += Mathf.Max(0, placement.layers);
            }

            return total;
        }
    }
}
