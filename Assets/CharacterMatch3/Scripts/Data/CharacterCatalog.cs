using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3
{
    [Serializable]
    public sealed class CharacterSpriteEntry
    {
        public CharacterType characterType;
        public Sprite sprite;
        public Color fallbackColor = Color.white;
    }

    [CreateAssetMenu(menuName = "Character Match-3/Character Catalog", fileName = "CharacterCatalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private List<CharacterSpriteEntry> entries = new List<CharacterSpriteEntry>();

        public IReadOnlyList<CharacterSpriteEntry> Entries => entries;

        public void EnsureDefaultEntries()
        {
            var colors = new[]
            {
                new Color(1f, 0.54f, 0.48f),
                new Color(0.66f, 0.78f, 1f),
                new Color(0.54f, 0.88f, 0.5f),
                new Color(0.52f, 0.9f, 1f),
                new Color(1f, 0.78f, 0.42f)
            };

            for (var i = 0; i < CharacterMatch3Constants.AllCharacters.Length; i++)
            {
                var type = CharacterMatch3Constants.AllCharacters[i];
                if (entries.Exists(entry => entry.characterType == type))
                {
                    continue;
                }

                entries.Add(new CharacterSpriteEntry
                {
                    characterType = type,
                    fallbackColor = colors[i]
                });
            }

            entries.Sort((a, b) => a.characterType.CompareTo(b.characterType));
        }

        public Sprite GetSprite(CharacterType characterType)
        {
            var entry = entries.Find(candidate => candidate.characterType == characterType);
            return entry != null ? entry.sprite : null;
        }

        public void SetSprite(CharacterType characterType, Sprite sprite)
        {
            EnsureDefaultEntries();
            var entry = entries.Find(candidate => candidate.characterType == characterType);
            if (entry != null)
            {
                entry.sprite = sprite;
            }
        }

        public Color GetFallbackColor(CharacterType characterType)
        {
            var entry = entries.Find(candidate => candidate.characterType == characterType);
            return entry != null ? entry.fallbackColor : Color.white;
        }

        public int AssignedSpriteCount()
        {
            var count = 0;
            foreach (var entry in entries)
            {
                if (entry.sprite != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
