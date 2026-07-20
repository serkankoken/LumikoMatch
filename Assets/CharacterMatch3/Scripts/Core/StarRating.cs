using UnityEngine;

namespace CharacterMatch3.Core
{
    public static class StarRating
    {
        private const float TwoStarRemainingMoveRatio = 0.25f;
        private const float ThreeStarRemainingMoveRatio = 0.5f;

        public static int GetStarsForRemainingMoves(LevelDefinition level, int movesRemaining)
        {
            if (level == null)
            {
                return 0;
            }

            var moveLimit = Mathf.Max(1, level.moveLimit);
            var clampedRemaining = Mathf.Clamp(movesRemaining, 0, moveLimit);

            if (clampedRemaining >= GetThreeStarMoveThreshold(level))
            {
                return 3;
            }

            if (clampedRemaining >= GetTwoStarMoveThreshold(level))
            {
                return 2;
            }

            return 1;
        }

        public static int GetTwoStarMoveThreshold(LevelDefinition level)
        {
            return GetRemainingMoveThreshold(level, TwoStarRemainingMoveRatio);
        }

        public static int GetThreeStarMoveThreshold(LevelDefinition level)
        {
            return GetRemainingMoveThreshold(level, ThreeStarRemainingMoveRatio);
        }

        private static int GetRemainingMoveThreshold(LevelDefinition level, float ratio)
        {
            var moveLimit = Mathf.Max(1, level != null ? level.moveLimit : 1);
            return Mathf.Clamp(Mathf.CeilToInt(moveLimit * ratio), 1, moveLimit);
        }
    }
}
