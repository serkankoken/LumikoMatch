using CharacterMatch3.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterMatch3.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string levelMapSceneName = "LevelMap";

        private void Awake()
        {
            RuntimeSceneUtility.EnsureMainCamera();
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            SaveManager.Load();

            if (AudioManager.Instance == null)
            {
                new GameObject("AudioManager").AddComponent<AudioManager>();
            }
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(levelMapSceneName))
            {
                SceneManager.LoadScene(levelMapSceneName);
            }
        }
    }
}
