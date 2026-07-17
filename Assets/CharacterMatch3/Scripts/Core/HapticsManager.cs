using UnityEngine;

namespace CharacterMatch3.Core
{
    public static class HapticsManager
    {
        public static void Light()
        {
            if (CharacterMatch3.Save.SaveManager.Data.hapticsEnabled)
            {
                Handheld.Vibrate();
            }
        }
    }
}
