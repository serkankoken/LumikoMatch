namespace CharacterMatch3.Core
{
    public static class GameState
    {
        public static int SelectedLevelNumber = 1;
        public static int PendingMapProgressFrom;
        public static int PendingMapProgressTo;

        public static bool HasPendingMapProgression =>
            PendingMapProgressFrom > 0 &&
            PendingMapProgressTo > PendingMapProgressFrom;

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

        public static void ClearMapProgression()
        {
            PendingMapProgressFrom = 0;
            PendingMapProgressTo = 0;
        }
    }
}
