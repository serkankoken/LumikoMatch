using UnityEngine;

namespace CharacterMatch3.Core
{
    public static class HapticsManager
    {
        private static float lastHapticTime = -10f;

        public static void Light()
        {
            TryVibrate(0.12f);
        }

        public static void Medium()
        {
            TryVibrate(0.22f);
        }

        public static void Heavy()
        {
            TryVibrate(0.34f);
        }

        private static void TryVibrate(float cooldown)
        {
            if (!CharacterMatch3.Save.SaveManager.Data.hapticsEnabled)
            {
                return;
            }

            if (Time.unscaledTime - lastHapticTime < cooldown)
            {
                return;
            }

            lastHapticTime = Time.unscaledTime;
            try
            {
                Handheld.Vibrate();
            }
            catch
            {
                // Some editor/device combinations expose Handheld.Vibrate but do not support it at runtime.
            }
        }
    }
}
