using CharacterMatch3.Core;
using CharacterMatch3.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CharacterMatch3.UI
{
    public sealed class LevelMapUI : MonoBehaviour
    {
        [SerializeField] private LevelLibrary levelLibrary;
        [SerializeField] private CharacterCatalog characterCatalog;

        private Canvas canvas;
        private RectTransform contentRoot;

        private void Start()
        {
            RuntimeSceneUtility.EnsureMainCamera();
            Screen.orientation = ScreenOrientation.Portrait;
            SaveManager.Load();
            Build();
        }

        public void Configure(LevelLibrary library, CharacterCatalog catalog)
        {
            levelLibrary = library;
            characterCatalog = catalog;
        }

        private void Build()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UIFactory.CreateCanvas("LevelMapCanvas");
            var safeRoot = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaController)).GetComponent<RectTransform>();
            safeRoot.SetParent(canvas.transform, false);
            UIFactory.Stretch(safeRoot);

            UIFactory.CreatePanel("Background", safeRoot, new Color(0.22f, 0.68f, 0.58f));

            var title = UIFactory.CreateText("Title", safeRoot, "Lumiko Match", 52, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchored(title.rectTransform, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero);

            var scrollObject = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            scrollObject.transform.SetParent(safeRoot, false);
            UIFactory.SetAnchored(scrollObject.GetComponent<RectTransform>(), new Vector2(0f, 0.04f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            scrollObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            scrollObject.GetComponent<Mask>().showMaskGraphic = false;

            contentRoot = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRoot.SetParent(scrollObject.transform, false);
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.sizeDelta = new Vector2(0, 2600);

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = contentRoot;
            scroll.horizontal = false;
            scroll.vertical = true;

            CreateLevelNodes();
        }

        private void CreateLevelNodes()
        {
            for (var levelNumber = CharacterMatch3Constants.FirstLevel; levelNumber <= CharacterMatch3Constants.LastLevel; levelNumber++)
            {
                var unlocked = SaveManager.IsLevelUnlocked(levelNumber);
                var stars = SaveManager.GetStars(levelNumber);
                var node = UIFactory.CreateButton($"Level_{levelNumber:000}", contentRoot, unlocked ? levelNumber.ToString() : "LOCK", () => { });
                var nodeRect = node.GetComponent<RectTransform>();
                nodeRect.sizeDelta = new Vector2(150, 150);

                var row = (levelNumber - 1) / 5;
                var column = (levelNumber - 1) % 5;
                var xOffset = column * 180f + (row % 2 == 0 ? 100f : 170f);
                var yOffset = -120f - row * 245f;
                nodeRect.anchorMin = new Vector2(0f, 1f);
                nodeRect.anchorMax = new Vector2(0f, 1f);
                nodeRect.anchoredPosition = new Vector2(xOffset, yOffset);

                var image = node.GetComponent<Image>();
                image.color = unlocked
                    ? levelNumber == SaveManager.Data.highestUnlockedLevel
                        ? new Color(1f, 0.93f, 0.25f)
                        : new Color(1f, 0.78f, 0.36f)
                    : new Color(0.32f, 0.42f, 0.45f);

                var starLabel = UIFactory.CreateText("Stars", node.transform, unlocked ? new string('*', stars) : string.Empty, 24, TextAnchor.LowerCenter, new Color(0.2f, 0.14f, 0.05f));
                UIFactory.Stretch(starLabel.rectTransform);

                var captured = levelNumber;
                node.onClick.RemoveAllListeners();
                node.onClick.AddListener(() =>
                {
                    if (!SaveManager.IsLevelUnlocked(captured))
                    {
                        return;
                    }

                    AudioManager.Instance?.PlayButton();
                    GameState.SelectedLevelNumber = captured;
                    SceneManager.LoadScene("Gameplay");
                });
            }
        }
    }
}
