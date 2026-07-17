using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3
{
    [CreateAssetMenu(menuName = "Character Match-3/Level Definition", fileName = "LevelDefinition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int levelNumber = 1;
        public string displayName = "Level 1";
        public DifficultyLabel difficultyLabel = DifficultyLabel.Easy;
        public string backgroundThemeId = "meadow";

        [Header("Board")]
        [Min(3)] public int boardWidth = 7;
        [Min(3)] public int boardHeight = 7;
        public bool[] activeCells = new bool[49];
        [Min(1)] public int moveLimit = 24;
        public List<CharacterType> availableCharacterTypes = new List<CharacterType>
        {
            CharacterType.Cat,
            CharacterType.Bunny,
            CharacterType.Dino,
            CharacterType.Penguin
        };
        public int randomSeed = 1001;
        public bool reshufflingAllowed = true;
        [Min(0)] public int maximumAutomaticReshuffleAttempts = 8;

        [Header("Goals")]
        public List<LevelGoalData> goals = new List<LevelGoalData>();
        public int oneStarScore = 3000;
        public int twoStarScore = 6000;
        public int threeStarScore = 9000;

        [Header("Board Elements")]
        public List<CellLayerData> softCoverPlacements = new List<CellLayerData>();
        public List<CellLayerData> cratePlacements = new List<CellLayerData>();
        public List<CellLayerData> lockPlacements = new List<CellLayerData>();
        public List<BoardCoordinate> companionTokenStartingPositions = new List<BoardCoordinate>();
        public List<BoardCoordinate> companionExitCells = new List<BoardCoordinate>();

        [Header("Pre-Placed Pieces")]
        public List<PiecePlacement> prePlacedNormalPieces = new List<PiecePlacement>();
        public List<PiecePlacement> prePlacedSpecialPieces = new List<PiecePlacement>();

        [Header("Tutorial")]
        [TextArea(2, 4)] public string tutorialInstructions;
        public TutorialSwapData tutorialForcedSwap;

        public int CellCount => boardWidth * boardHeight;

        public void EnsureDefaults()
        {
            boardWidth = Mathf.Max(3, boardWidth);
            boardHeight = Mathf.Max(3, boardHeight);
            moveLimit = Mathf.Max(1, moveLimit);

            if (activeCells == null || activeCells.Length != CellCount)
            {
                activeCells = new bool[CellCount];
                for (var i = 0; i < activeCells.Length; i++)
                {
                    activeCells[i] = true;
                }
            }

            if (availableCharacterTypes == null)
            {
                availableCharacterTypes = new List<CharacterType>();
            }

            if (availableCharacterTypes.Count == 0)
            {
                availableCharacterTypes.Add(CharacterType.Cat);
                availableCharacterTypes.Add(CharacterType.Bunny);
                availableCharacterTypes.Add(CharacterType.Dino);
                availableCharacterTypes.Add(CharacterType.Penguin);
            }

            goals ??= new List<LevelGoalData>();
            softCoverPlacements ??= new List<CellLayerData>();
            cratePlacements ??= new List<CellLayerData>();
            lockPlacements ??= new List<CellLayerData>();
            companionTokenStartingPositions ??= new List<BoardCoordinate>();
            companionExitCells ??= new List<BoardCoordinate>();
            prePlacedNormalPieces ??= new List<PiecePlacement>();
            prePlacedSpecialPieces ??= new List<PiecePlacement>();

            oneStarScore = Mathf.Max(0, oneStarScore);
            twoStarScore = Mathf.Max(oneStarScore + 1, twoStarScore);
            threeStarScore = Mathf.Max(twoStarScore + 1, threeStarScore);
        }

        public int GetIndex(int x, int y)
        {
            return y * boardWidth + x;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < boardWidth && y >= 0 && y < boardHeight;
        }

        public bool IsActive(int x, int y)
        {
            if (!IsInside(x, y) || activeCells == null)
            {
                return false;
            }

            var index = GetIndex(x, y);
            return index >= 0 && index < activeCells.Length && activeCells[index];
        }

        public int ActiveCellCount()
        {
            var count = 0;
            if (activeCells == null)
            {
                return count;
            }

            foreach (var active in activeCells)
            {
                if (active)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }
    }
}
