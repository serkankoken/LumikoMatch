using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3.Save
{
    [Serializable]
    public sealed class CharacterMatch3SaveData
    {
        public int version = 1;
        public int highestUnlockedLevel = 1;
        public List<int> starsPerLevel = new List<int>();
        public List<int> bestScorePerLevel = new List<int>();
        public bool musicEnabled = true;
        public bool soundEnabled = true;
        public bool hapticsEnabled = true;
        public List<int> completedTutorialLevels = new List<int>();
    }

    public static class SaveManager
    {
        private const string SaveKey = "CharacterMatch3.Save.v1";
        private static CharacterMatch3SaveData data;

        public static CharacterMatch3SaveData Data
        {
            get
            {
                EnsureLoaded();
                return data;
            }
        }

        public static void Load()
        {
            var raw = PlayerPrefs.GetString(SaveKey, string.Empty);
            data = string.IsNullOrEmpty(raw)
                ? new CharacterMatch3SaveData()
                : JsonUtility.FromJson<CharacterMatch3SaveData>(raw);
            Normalize();
        }

        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static bool IsLevelUnlocked(int levelNumber)
        {
            EnsureLoaded();
            return levelNumber <= data.highestUnlockedLevel;
        }

        public static bool IsLevelCompleted(int levelNumber)
        {
            EnsureLoaded();
            EnsureLevelIndex(levelNumber);
            return data.starsPerLevel[levelNumber] > 0 || levelNumber < data.highestUnlockedLevel;
        }

        public static int GetStars(int levelNumber)
        {
            EnsureLoaded();
            EnsureLevelIndex(levelNumber);
            return data.starsPerLevel[levelNumber];
        }

        public static int GetBestScore(int levelNumber)
        {
            EnsureLoaded();
            EnsureLevelIndex(levelNumber);
            return data.bestScorePerLevel[levelNumber];
        }

        public static void RecordLevelWin(int levelNumber, int score, int stars)
        {
            EnsureLoaded();
            EnsureLevelIndex(levelNumber);
            data.starsPerLevel[levelNumber] = Mathf.Max(data.starsPerLevel[levelNumber], stars);
            data.bestScorePerLevel[levelNumber] = Mathf.Max(data.bestScorePerLevel[levelNumber], score);
            data.highestUnlockedLevel = Mathf.Max(data.highestUnlockedLevel, Mathf.Min(CharacterMatch3Constants.LastLevel, levelNumber + 1));
            Save();
        }

        public static bool IsTutorialCompleted(int levelNumber)
        {
            EnsureLoaded();
            return data.completedTutorialLevels.Contains(levelNumber);
        }

        public static void MarkTutorialCompleted(int levelNumber)
        {
            EnsureLoaded();
            if (!data.completedTutorialLevels.Contains(levelNumber))
            {
                data.completedTutorialLevels.Add(levelNumber);
                Save();
            }
        }

        public static void SetMusicEnabled(bool enabled)
        {
            EnsureLoaded();
            data.musicEnabled = enabled;
            Save();
        }

        public static void SetSoundEnabled(bool enabled)
        {
            EnsureLoaded();
            data.soundEnabled = enabled;
            Save();
        }

        public static void SetHapticsEnabled(bool enabled)
        {
            EnsureLoaded();
            data.hapticsEnabled = enabled;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (data == null)
            {
                Load();
            }
        }

        private static void Normalize()
        {
            data ??= new CharacterMatch3SaveData();
            data.version = Mathf.Max(1, data.version);
            data.highestUnlockedLevel = Mathf.Clamp(data.highestUnlockedLevel, 1, CharacterMatch3Constants.LastLevel);

            for (var i = data.starsPerLevel.Count; i <= CharacterMatch3Constants.LastLevel; i++)
            {
                data.starsPerLevel.Add(0);
            }

            for (var i = data.bestScorePerLevel.Count; i <= CharacterMatch3Constants.LastLevel; i++)
            {
                data.bestScorePerLevel.Add(0);
            }

            data.completedTutorialLevels ??= new List<int>();
        }

        private static void EnsureLevelIndex(int levelNumber)
        {
            Normalize();
            if (levelNumber < 0 || levelNumber >= data.starsPerLevel.Count)
            {
                Debug.LogWarning($"Character Match-3 save request for out-of-range level {levelNumber}.");
            }
        }
    }
}
