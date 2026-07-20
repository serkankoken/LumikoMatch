namespace CharacterMatch3.Core
{
    public static class GameState
    {
        public static int SelectedLevelNumber = 1;
        public static int PendingMapProgressFrom;
        public static int PendingMapProgressTo;
        public static int PendingMapStarRevealLevel;
        public static int PendingMapStarRevealCount;

        public static bool HasPendingMapProgression =>
            PendingMapProgressFrom > 0 &&
            PendingMapProgressTo > PendingMapProgressFrom;

        public static bool HasPendingMapStarReveal =>
            PendingMapStarRevealLevel > 0 &&
            PendingMapStarRevealCount > 0;

        public static void QueueMapProgression(int fromLevel, int toLevel)
        {
            if (fromLevel <= 0 || toLevel <= fromLevel)
            {
                ClearMapProgression();
                return;
            }

            PendingMapProgressFrom = fromLevel;
            PendingMapProgressTo = toLevel;
            SelectedLevelNumber = toLevel;
        }

        public static void QueueMapStarReveal(int levelNumber, int stars)
        {
            if (levelNumber <= 0 || stars <= 0)
            {
                ClearMapStarReveal();
                return;
            }

            PendingMapStarRevealLevel = levelNumber;
            PendingMapStarRevealCount = stars > 3 ? 3 : stars;
        }

        public static void ClearMapProgression()
        {
            PendingMapProgressFrom = 0;
            PendingMapProgressTo = 0;
        }

        public static void ClearMapStarReveal()
        {
            PendingMapStarRevealLevel = 0;
            PendingMapStarRevealCount = 0;
        }
    }
}
