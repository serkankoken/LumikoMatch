using UnityEditor;
using UnityEngine;

namespace CharacterMatch3.Editor
{
    [InitializeOnLoad]
    public static class CharacterMatch3AutoSetup
    {
        private const string SessionKey = "CharacterMatch3.AutoSetupAttempted";

        static CharacterMatch3AutoSetup()
        {
            EditorApplication.delayCall += TryRunOnceWhenReady;
        }

        private static void TryRunOnceWhenReady()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunOnceWhenReady;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<LevelLibrary>(CharacterMatch3Constants.LevelLibraryPath) != null &&
                AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharacterMatch3Constants.CatalogPath) != null)
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            Debug.Log("Character Match-3 generated assets are missing; running one-time setup.");
            CharacterMatch3Setup.SetupCompleteGame();
        }
    }
}
