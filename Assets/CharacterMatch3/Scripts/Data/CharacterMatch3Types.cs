using System;
using UnityEngine;

namespace CharacterMatch3
{
    public enum CharacterType
    {
        Cat = 0,
        Bunny = 1,
        Dino = 2,
        Penguin = 3,
        Bear = 4
    }

    public enum PieceKind
    {
        Normal = 0,
        Line = 1,
        Burst = 2,
        Rainbow = 3,
        Companion = 4
    }

    public enum LineOrientation
    {
        Horizontal = 0,
        Vertical = 1
    }

    public enum GoalType
    {
        CollectCharacter = 0,
        ClearSoftCover = 1,
        BreakCrates = 2,
        DropCompanions = 3,
        ReachScore = 4
    }

    public enum DifficultyLabel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    [Serializable]
    public struct BoardCoordinate : IEquatable<BoardCoordinate>
    {
        public int x;
        public int y;

        public BoardCoordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(BoardCoordinate other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public override string ToString()
        {
            return $"({x},{y})";
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public static bool operator ==(BoardCoordinate left, BoardCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BoardCoordinate left, BoardCoordinate right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct CellLayerData
    {
        public BoardCoordinate coordinate;
        [Min(1)] public int layers;

        public CellLayerData(int x, int y, int layers)
        {
            coordinate = new BoardCoordinate(x, y);
            this.layers = Mathf.Max(1, layers);
        }
    }

    [Serializable]
    public struct PiecePlacement
    {
        public BoardCoordinate coordinate;
        public PieceKind kind;
        public CharacterType character;
        public LineOrientation lineOrientation;

        public PiecePlacement(int x, int y, CharacterType character)
        {
            coordinate = new BoardCoordinate(x, y);
            kind = PieceKind.Normal;
            this.character = character;
            lineOrientation = LineOrientation.Horizontal;
        }

        public PiecePlacement(int x, int y, PieceKind kind, CharacterType character, LineOrientation lineOrientation)
        {
            coordinate = new BoardCoordinate(x, y);
            this.kind = kind;
            this.character = character;
            this.lineOrientation = lineOrientation;
        }
    }

    [Serializable]
    public struct LevelGoalData
    {
        public GoalType goalType;
        public CharacterType characterType;
        [Min(0)] public int amount;

        public LevelGoalData(GoalType goalType, int amount)
        {
            this.goalType = goalType;
            characterType = CharacterType.Cat;
            this.amount = Mathf.Max(0, amount);
        }

        public LevelGoalData(CharacterType characterType, int amount)
        {
            goalType = GoalType.CollectCharacter;
            this.characterType = characterType;
            this.amount = Mathf.Max(0, amount);
        }
    }

    [Serializable]
    public struct TutorialSwapData
    {
        public bool enabled;
        public BoardCoordinate from;
        public BoardCoordinate to;
    }

    public static class CharacterMatch3Constants
    {
        public const string RootPath = "Assets/CharacterMatch3";
        public const string CatalogPath = RootPath + "/Data/CharacterCatalog.asset";
        public const string ScoringPath = RootPath + "/Data/ScoringConfig.asset";
        public const string LevelLibraryPath = RootPath + "/Data/LevelLibrary.asset";
        public const string InputSettingsPath = RootPath + "/Data/Match3InputSettings.asset";
        public const int FirstLevel = 1;
        public const int LastLevel = 50;

        public static readonly CharacterType[] AllCharacters =
        {
            CharacterType.Cat,
            CharacterType.Bunny,
            CharacterType.Dino,
            CharacterType.Penguin,
            CharacterType.Bear
        };
    }
}
