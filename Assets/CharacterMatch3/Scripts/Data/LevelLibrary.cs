using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3
{
    [CreateAssetMenu(menuName = "Character Match-3/Level Library", fileName = "LevelLibrary")]
    public sealed class LevelLibrary : ScriptableObject
    {
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels
        {
            get
            {
                EnsureList();
                return levels;
            }
        }

        public void SetLevels(IEnumerable<LevelDefinition> definitions)
        {
            EnsureList();
            levels.Clear();
            levels.AddRange(definitions);
            levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
        }

        public LevelDefinition GetLevel(int levelNumber)
        {
            EnsureList();
            return levels.Find(level => level != null && level.levelNumber == levelNumber);
        }

        public int HighestLevelNumber()
        {
            EnsureList();
            var highest = 0;
            foreach (var level in levels)
            {
                if (level != null && level.levelNumber > highest)
                {
                    highest = level.levelNumber;
                }
            }

            return highest;
        }

        private void EnsureList()
        {
            levels ??= new List<LevelDefinition>();
        }
    }
}
