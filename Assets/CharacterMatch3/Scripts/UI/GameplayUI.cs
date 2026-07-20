using CharacterMatch3.Board;
using CharacterMatch3.Core;
using CharacterMatch3.Goals;
using CharacterMatch3.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterMatch3.UI
{
    public sealed class GameplayUI : MonoBehaviour
    {
        private GameSession session;
        private CharacterCatalog catalog;
        private Canvas canvas;
        private RectTransform safeRoot;
        private Image backgroundImage;
        private Text levelText;
        private Text movesText;
        private Text scoreText;
        private Text starText;
        private RectTransform goalsRoot;
        private GameObject startPanel;
        private GameObject pausePanel;
        private GameObject winPanel;
        private GameObject losePanel;
        private GameObject settingsPanel;

        public BoardView BoardView { get; private set; }

        public void Initialize(GameSession gameSession, CharacterCatalog characterCatalog)
        {
            session = gameSession;
            catalog = characterCatalog;
            EnsureBuilt();
            ApplyBackgroundTheme(gameSession != null ? gameSession.CurrentLevel : null);
        }

        public void BindGoalManager(GoalManager goalManager)
        {
            goalManager.GoalsChanged += () => RefreshGoals(goalManager);
            RefreshGoals(goalManager);
        }

        public void UpdateMoves(int moves)
        {
            if (movesText != null)
            {
                movesText.text = $"Moves\n{moves}";
            }
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score\n{score}";
            }
        }

        public void UpdateStars(LevelDefinition level, int movesRemaining)
        {
            if (starText == null || level == null)
            {
                return;
            }

            var stars = StarRating.GetStarsForRemainingMoves(level, movesRemaining);
            UpdateStars(stars);
        }

        public void UpdateStars(int stars)
        {
            if (starText == null)
            {
                return;
            }

            stars = Mathf.Clamp(stars, 0, 3);
            starText.text = $"Stars {stars}/3";
        }

        public void ShowStartPanel(LevelDefinition level)
        {
            EnsureBuilt();
            ApplyBackgroundTheme(level);
            HideAllPanels();
            levelText.text = $"Level {level.levelNumber}";
            var title = startPanel.transform.Find("Box/Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = $"{level.displayName}\n{level.difficultyLabel}";
            }

            startPanel.SetActive(true);
        }

        public void ShowPausePanel()
        {
            HideAllPanels();
            pausePanel.SetActive(true);
        }

        public void ShowSettingsPanel()
        {
            HideAllPanels();
            settingsPanel.SetActive(true);
        }

        public void ShowWinPanel(int score, int stars)
        {
            HideAllPanels();
            var title = winPanel.transform.Find("Box/Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = $"Level Complete\nScore {score}\nStars {stars}/3";
            }

            winPanel.SetActive(true);
        }

        public void ShowLosePanel(int score)
        {
            HideAllPanels();
            var title = losePanel.transform.Find("Box/Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = $"Try Again\nScore {score}";
            }

            losePanel.SetActive(true);
        }

        public void HideAllPanels()
        {
            startPanel?.SetActive(false);
            pausePanel?.SetActive(false);
            winPanel?.SetActive(false);
            losePanel?.SetActive(false);
            settingsPanel?.SetActive(false);
        }

        private void RefreshGoals(GoalManager goalManager)
        {
            EnsureBuilt();
            for (var i = goalsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(goalsRoot.GetChild(i).gameObject);
            }

            foreach (var goal in goalManager.ActiveGoals)
            {
                var item = UIFactory.CreatePanel($"Goal_{goal.goalType}", goalsRoot, new Color(1f, 1f, 1f, 0.28f));
                var layout = item.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 4, 4);
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.MiddleCenter;

                if (goal.goalType == GoalType.CollectCharacter)
                {
                    var icon = UIFactory.CreateImage("Icon", item.transform, catalog != null ? catalog.GetFallbackColor(goal.characterType) : Color.white);
                    icon.sprite = catalog != null ? catalog.GetSprite(goal.characterType) : null;
                    icon.preserveAspect = true;
                    icon.rectTransform.sizeDelta = new Vector2(52, 52);
                }

                var label = UIFactory.CreateText("Label", item.transform, $"{goal.DisplayName}: {goal.Remaining}", 24, TextAnchor.MiddleCenter, Color.white);
                label.rectTransform.sizeDelta = new Vector2(210, 58);
            }
        }

        private void EnsureBuilt()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UIFactory.CreateCanvas("GameplayCanvas");
            safeRoot = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaController)).GetComponent<RectTransform>();
            safeRoot.SetParent(canvas.transform, false);
            UIFactory.Stretch(safeRoot);

            backgroundImage = UIFactory.CreateImage("Background", safeRoot, GetFallbackBackgroundColor(null));
            UIFactory.Stretch(backgroundImage.rectTransform);
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;
            ApplyBackgroundTheme(session != null ? session.CurrentLevel : null);

            var boardBand = UIFactory.CreatePanel("BoardBand", backgroundImage.transform, new Color(0.07f, 0.29f, 0.36f, 0.24f));
            UIFactory.SetAnchored(boardBand.GetComponent<RectTransform>(), new Vector2(0f, 0.22f), new Vector2(1f, 0.78f), Vector2.zero, Vector2.zero);

            levelText = UIFactory.CreateText("LevelText", safeRoot, "Level", 34, TextAnchor.MiddleLeft, Color.white);
            UIFactory.SetAnchored(levelText.rectTransform, new Vector2(0.05f, 0.91f), new Vector2(0.36f, 0.98f), Vector2.zero, Vector2.zero);

            movesText = UIFactory.CreateText("MovesText", safeRoot, "Moves", 34, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchored(movesText.rectTransform, new Vector2(0.37f, 0.91f), new Vector2(0.62f, 0.98f), Vector2.zero, Vector2.zero);

            scoreText = UIFactory.CreateText("ScoreText", safeRoot, "Score", 28, TextAnchor.MiddleRight, Color.white);
            UIFactory.SetAnchored(scoreText.rectTransform, new Vector2(0.63f, 0.91f), new Vector2(0.9f, 0.98f), Vector2.zero, Vector2.zero);

            var pauseButton = UIFactory.CreateButton("PauseButton", safeRoot, "II", () => session.Pause());
            UIFactory.SetAnchored(pauseButton.GetComponent<RectTransform>(), new Vector2(0.91f, 0.92f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);

            starText = UIFactory.CreateText("StarText", safeRoot, "Stars 0/3", 26, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchored(starText.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.89f), Vector2.zero, Vector2.zero);

            goalsRoot = new GameObject("Goals", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            goalsRoot.SetParent(safeRoot, false);
            UIFactory.SetAnchored(goalsRoot, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.84f), Vector2.zero, Vector2.zero);
            var goalsLayout = goalsRoot.GetComponent<HorizontalLayoutGroup>();
            goalsLayout.spacing = 8;
            goalsLayout.childAlignment = TextAnchor.MiddleCenter;
            goalsLayout.childForceExpandWidth = false;

            var boardObject = new GameObject("BoardView", typeof(RectTransform), typeof(BoardView));
            boardObject.transform.SetParent(safeRoot, false);
            var boardRect = boardObject.GetComponent<RectTransform>();
            UIFactory.SetAnchored(boardRect, new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.74f), Vector2.zero, Vector2.zero);
            BoardView = boardObject.GetComponent<BoardView>();

            startPanel = CreateModalPanel("StartPanel", "Title", "Play", () => session.StartLevel());
            pausePanel = CreateModalPanel("PausePanel", "Paused", "Resume", () => session.Resume());
            AddPanelButton(pausePanel, "Restart", () => session.RestartLevel(), 1);
            AddPanelButton(pausePanel, "Settings", () => session.ShowSettings(), 2);
            AddPanelButton(pausePanel, "Exit", () => session.ExitToMap(), 3);

            winPanel = CreateModalPanel("WinPanel", "Level Complete", "Continue", () => session.PlayNextLevel());
            AddPanelButton(winPanel, "Replay", () => session.RestartLevel(), 1);
            AddPanelButton(winPanel, "Exit", () => session.ExitToMap(), 2);

            losePanel = CreateModalPanel("LosePanel", "Try Again", "Restart", () => session.RestartLevel());
            AddPanelButton(losePanel, "Exit", () => session.ExitToMap(), 1);

            settingsPanel = CreateModalPanel("SettingsPanel", "Settings", "Back", () => session.Resume());
            AddToggleButton(settingsPanel, "Sound", SaveManager.Data.soundEnabled, enabled => session.SetSoundEnabled(enabled), 1);
            AddToggleButton(settingsPanel, "Music", SaveManager.Data.musicEnabled, enabled => session.SetMusicEnabled(enabled), 2);
            AddToggleButton(settingsPanel, "Haptics", SaveManager.Data.hapticsEnabled, enabled => session.SetHapticsEnabled(enabled), 3);
            HideAllPanels();
        }

        private void ApplyBackgroundTheme(LevelDefinition level)
        {
            if (backgroundImage == null)
            {
                return;
            }

            var sprite = catalog != null ? catalog.GetGameplayBackgroundSprite(level != null ? level.backgroundThemeId : null) : null;
            backgroundImage.sprite = sprite;
            backgroundImage.color = sprite != null ? Color.white : GetFallbackBackgroundColor(level != null ? level.backgroundThemeId : null);
        }

        private static Color GetFallbackBackgroundColor(string themeId)
        {
            if (string.Equals(themeId, "beach", System.StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.25f, 0.71f, 0.83f);
            }

            if (string.Equals(themeId, "desert", System.StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.88f, 0.64f, 0.32f);
            }

            return new Color(0.38f, 0.68f, 0.42f);
        }

        private GameObject CreateModalPanel(string name, string title, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
        {
            var shade = UIFactory.CreatePanel(name, safeRoot, new Color(0f, 0f, 0f, 0.5f));
            var box = UIFactory.CreatePanel("Box", shade.transform, new Color(1f, 0.97f, 0.85f, 0.98f));
            UIFactory.SetAnchored(box.GetComponent<RectTransform>(), new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f), Vector2.zero, Vector2.zero);
            var titleText = UIFactory.CreateText("Title", box.transform, title, 42, TextAnchor.MiddleCenter, new Color(0.14f, 0.12f, 0.16f));
            UIFactory.SetAnchored(titleText.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
            AddPanelButton(shade, primaryLabel, primaryAction, 0);
            return shade;
        }

        private void AddPanelButton(GameObject panel, string label, UnityEngine.Events.UnityAction action, int index)
        {
            var button = UIFactory.CreateButton(label + "Button", panel.transform.Find("Box"), label, action);
            var yMax = 0.48f - index * 0.16f;
            var yMin = yMax - 0.11f;
            UIFactory.SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0.2f, yMin), new Vector2(0.8f, yMax), Vector2.zero, Vector2.zero);
        }

        private void AddToggleButton(GameObject panel, string label, bool initialValue, System.Action<bool> action, int index)
        {
            var state = initialValue;
            Button button = null;
            button = UIFactory.CreateButton(label + "Toggle", panel.transform.Find("Box"), $"{label}: {(state ? "On" : "Off")}", () =>
            {
                state = !state;
                button.GetComponentInChildren<Text>().text = $"{label}: {(state ? "On" : "Off")}";
                action?.Invoke(state);
            });
            var yMax = 0.48f - index * 0.16f;
            var yMin = yMax - 0.11f;
            UIFactory.SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0.2f, yMin), new Vector2(0.8f, yMax), Vector2.zero, Vector2.zero);
        }
    }
}
