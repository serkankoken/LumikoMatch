using UnityEngine;

namespace CharacterMatch3
{
    [CreateAssetMenu(menuName = "Character Match-3/Scoring Config", fileName = "ScoringConfig")]
    public sealed class ScoringConfig : ScriptableObject
    {
        public int normalPieceRemoved = 60;
        public int fourMatchCreationBonus = 120;
        public int fiveMatchCreationBonus = 300;
        public int tOrLMatchBonus = 240;
        public int softCoverLayerRemoved = 80;
        public int crateLayerRemoved = 100;
        public int characterLockRemoved = 100;
        public int companionTokenDelivered = 1000;
        public int remainingMoveBonus = 500;
        public float cascadeMultiplierStep = 0.35f;

        public int ApplyCascadeMultiplier(int baseScore, int cascadeIndex)
        {
            var multiplier = 1f + Mathf.Max(0, cascadeIndex) * cascadeMultiplierStep;
            return Mathf.RoundToInt(baseScore * multiplier);
        }
    }
}
