using System.Collections;
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
        private Image movesChipImage;
        private Image scoreChipImage;
        private Image starPlateImage;
        private RectTransform goalsRoot;
        private GameObject startPanel;
        private GameObject pausePanel;
        private GameObject winPanel;
        private GameObject losePanel;
        private GameObject settingsPanel;
        private RectTransform celebrationRoot;
        private CanvasGroup celebrationGroup;
        private Coroutine modalAnimationRoutine;
        private Coroutine celebrationRoutine;
        private int celebrationToken;
        private int lastMovesValue = int.MinValue;
        private int lastScoreValue = int.MinValue;
        private int lastStarsValue = int.MinValue;

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
                if (lastMovesValue != int.MinValue && lastMovesValue != moves)
                {
                    StartCoroutine(PulseRect(movesText.transform.parent as RectTransform, 1.07f, 0.18f));
                }

                lastMovesValue = moves;
                if (movesChipImage != null)
                {
                    movesChipImage.color = moves <= 5
                        ? new Color(0.95f, 0.34f, 0.24f, 0.96f)
                        : moves <= 10
                            ? new Color(1f, 0.65f, 0.26f, 0.95f)
                            : new Color(0.18f, 0.72f, 0.58f, 0.94f);
                }
            }
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score\n{score}";
                if (lastScoreValue != int.MinValue && lastScoreValue != score)
                {
                    StartCoroutine(PulseRect(scoreChipImage != null ? scoreChipImage.rectTransform : scoreText.transform.parent as RectTransform, 1.06f, 0.16f));
                }

                lastScoreValue = score;
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
            if (lastStarsValue != int.MinValue && lastStarsValue != stars)
            {
                StartCoroutine(PulseRect(starPlateImage != null ? starPlateImage.rectTransform : starText.rectTransform, 1.08f, 0.18f));
                HapticsManager.Light();
            }

            lastStarsValue = stars;
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

            ShowPanel(startPanel);
        }

        public void ShowPausePanel()
        {
            ShowPanel(pausePanel);
        }

        public void ShowSettingsPanel()
        {
            ShowPanel(settingsPanel);
        }

        public void ShowWinPanel(int score, int stars)
        {
            HideAllPanels();
            var title = winPanel.transform.Find("Box/Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = $"Level Complete\nScore {score}\nStars {stars}/3";
            }

            ShowPanel(winPanel);
            PlayWinCelebration(stars);
        }

        public void ShowLosePanel(int score)
        {
            HideAllPanels();
            var title = losePanel.transform.Find("Box/Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = $"Try Again\nScore {score}";
            }

            ShowPanel(losePanel);
        }

        public void HideAllPanels()
        {
            if (modalAnimationRoutine != null)
            {
                StopCoroutine(modalAnimationRoutine);
                modalAnimationRoutine = null;
            }

            startPanel?.SetActive(false);
            pausePanel?.SetActive(false);
            winPanel?.SetActive(false);
            losePanel?.SetActive(false);
            settingsPanel?.SetActive(false);
            StopCelebration();
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
                var item = UIFactory.CreatePanel($"Goal_{goal.goalType}", goalsRoot, goal.IsComplete ? new Color(0.48f, 0.9f, 0.56f, 0.9f) : new Color(1f, 0.96f, 0.78f, 0.92f));
                var itemShadow = item.AddComponent<Shadow>();
                itemShadow.effectColor = new Color(0.08f, 0.08f, 0.08f, 0.2f);
                itemShadow.effectDistance = new Vector2(0f, -3f);
                var itemLayoutElement = item.AddComponent<LayoutElement>();
                itemLayoutElement.minHeight = 64f;
                itemLayoutElement.preferredHeight = 64f;
                itemLayoutElement.minWidth = goal.goalType == GoalType.CollectCharacter ? 244f : 204f;
                itemLayoutElement.preferredWidth = itemLayoutElement.minWidth;

                var layout = item.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(12, 14, 5, 5);
                layout.spacing = 10;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                if (goal.goalType == GoalType.CollectCharacter)
                {
                    var icon = UIFactory.CreateImage("Icon", item.transform, catalog != null ? catalog.GetFallbackColor(goal.characterType) : Color.white);
                    icon.sprite = catalog != null ? catalog.GetSprite(goal.characterType) : null;
                    icon.preserveAspect = true;
                    icon.rectTransform.sizeDelta = new Vector2(52, 52);
                    icon.raycastTarget = false;
                }

                var labelText = goal.IsComplete ? $"{goal.DisplayName}\nDone" : $"{goal.DisplayName}\n{goal.Remaining} left";
                var label = UIFactory.CreateText("Label", item.transform, labelText, 24, TextAnchor.MiddleCenter, new Color(0.2f, 0.14f, 0.08f));
                label.fontStyle = FontStyle.Bold;
                label.rectTransform.sizeDelta = new Vector2(goal.goalType == GoalType.CollectCharacter ? 160 : 172, 58);
                label.raycastTarget = false;
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

            var hudRibbon = UIFactory.CreatePanel("HudRibbon", safeRoot, new Color(0.04f, 0.07f, 0.09f, 0.16f));
            hudRibbon.GetComponent<Image>().raycastTarget = false;
            UIFactory.SetAnchored(hudRibbon.GetComponent<RectTransform>(), new Vector2(0f, 0.895f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            levelText = CreateHudChip("LevelChip", "Level", new Vector2(0.04f, 0.915f), new Vector2(0.31f, 0.982f), new Color(0.24f, 0.58f, 0.92f, 0.94f), 30);

            movesText = CreateHudChip("MovesChip", "Moves", new Vector2(0.33f, 0.915f), new Vector2(0.58f, 0.982f), new Color(0.18f, 0.72f, 0.58f, 0.94f), 30);
            movesChipImage = movesText.transform.parent.GetComponent<Image>();

            scoreText = CreateHudChip("ScoreChip", "Score", new Vector2(0.6f, 0.915f), new Vector2(0.88f, 0.982f), new Color(1f, 0.76f, 0.28f, 0.94f), 26);
            scoreChipImage = scoreText.transform.parent.GetComponent<Image>();

            var pauseButton = UIFactory.CreateButton("PauseButton", safeRoot, "||", () => session.Pause());
            UIFactory.SetAnchored(pauseButton.GetComponent<RectTransform>(), new Vector2(0.905f, 0.918f), new Vector2(0.975f, 0.98f), Vector2.zero, Vector2.zero);
            StyleButton(pauseButton, new Color(1f, 0.6f, 0.32f), new Color(0.18f, 0.08f, 0.03f));

            var starPlate = UIFactory.CreatePanel("StarPlate", safeRoot, new Color(0.14f, 0.12f, 0.18f, 0.62f));
            starPlateImage = starPlate.GetComponent<Image>();
            starPlate.GetComponent<Image>().raycastTarget = false;
            UIFactory.SetAnchored(starPlate.GetComponent<RectTransform>(), new Vector2(0.22f, 0.846f), new Vector2(0.78f, 0.895f), Vector2.zero, Vector2.zero);
            starText = UIFactory.CreateText("StarText", starPlate.transform, "Stars 0/3", 26, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.4f));
            starText.fontStyle = FontStyle.Bold;
            starText.raycastTarget = false;
            UIFactory.Stretch(starText.rectTransform);

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
            CreateCelebrationRoot();
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
            var shade = UIFactory.CreatePanel(name, safeRoot, new Color(0.02f, 0.03f, 0.06f, 0.58f));
            var group = shade.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            var box = UIFactory.CreatePanel("Box", shade.transform, new Color(1f, 0.97f, 0.86f, 0.98f));
            UIFactory.SetAnchored(box.GetComponent<RectTransform>(), new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.74f), Vector2.zero, Vector2.zero);
            var boxShadow = box.AddComponent<Shadow>();
            boxShadow.effectColor = new Color(0.12f, 0.08f, 0.04f, 0.34f);
            boxShadow.effectDistance = new Vector2(0f, -10f);

            var trim = UIFactory.CreatePanel("Trim", box.transform, new Color(0.28f, 0.64f, 0.92f, 0.92f));
            trim.GetComponent<Image>().raycastTarget = false;
            UIFactory.SetAnchored(trim.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one, Vector2.zero, Vector2.zero);

            var titleText = UIFactory.CreateText("Title", box.transform, title, 42, TextAnchor.MiddleCenter, new Color(0.16f, 0.1f, 0.14f));
            titleText.fontStyle = FontStyle.Bold;
            UIFactory.SetAnchored(titleText.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
            AddPanelButton(shade, primaryLabel, primaryAction, 0);
            return shade;
        }

        private void AddPanelButton(GameObject panel, string label, UnityEngine.Events.UnityAction action, int index)
        {
            var button = UIFactory.CreateButton(label + "Button", panel.transform.Find("Box"), label, action);
            var yMax = 0.58f - index * 0.13f;
            var yMin = yMax - 0.105f;
            UIFactory.SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0.18f, yMin), new Vector2(0.82f, yMax), Vector2.zero, Vector2.zero);
            var isExit = string.Equals(label, "Exit", System.StringComparison.OrdinalIgnoreCase);
            var color = index == 0
                ? new Color(0.34f, 0.78f, 0.46f)
                : isExit
                    ? new Color(0.92f, 0.34f, 0.32f)
                    : new Color(1f, 0.78f, 0.3f);
            StyleButton(button, color, isExit ? Color.white : new Color(0.18f, 0.11f, 0.04f));
        }

        private void AddToggleButton(GameObject panel, string label, bool initialValue, System.Action<bool> action, int index)
        {
            var state = initialValue;
            Button button = null;
            button = UIFactory.CreateButton(label + "Toggle", panel.transform.Find("Box"), $"{label}: {(state ? "On" : "Off")}", () =>
            {
                state = !state;
                button.GetComponentInChildren<Text>().text = $"{label}: {(state ? "On" : "Off")}";
                StyleButton(button, state ? new Color(0.34f, 0.78f, 0.46f) : new Color(0.62f, 0.62f, 0.68f), state ? new Color(0.12f, 0.13f, 0.08f) : Color.white);
                action?.Invoke(state);
            });
            var yMax = 0.58f - index * 0.13f;
            var yMin = yMax - 0.105f;
            UIFactory.SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0.18f, yMin), new Vector2(0.82f, yMax), Vector2.zero, Vector2.zero);
            StyleButton(button, state ? new Color(0.34f, 0.78f, 0.46f) : new Color(0.62f, 0.62f, 0.68f), state ? new Color(0.12f, 0.13f, 0.08f) : Color.white);
        }

        private Text CreateHudChip(string name, string text, Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize)
        {
            var chip = UIFactory.CreatePanel(name, safeRoot, color);
            chip.GetComponent<Image>().raycastTarget = false;
            UIFactory.SetAnchored(chip.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var shadow = chip.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.04f, 0.04f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var label = UIFactory.CreateText("Label", chip.transform, text, fontSize, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            UIFactory.SetAnchored(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            return label;
        }

        private static void StyleButton(Button button, Color color, Color textColor)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.08f);
            colors.disabledColor = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.58f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = textColor;
                label.fontStyle = FontStyle.Bold;
            }
        }

        private void ShowPanel(GameObject panel)
        {
            HideAllPanels();
            if (panel == null)
            {
                return;
            }

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            var group = panel.GetComponent<CanvasGroup>();
            var box = panel.transform.Find("Box") as RectTransform;
            modalAnimationRoutine = StartCoroutine(AnimatePanelIn(group, box));
        }

        private IEnumerator AnimatePanelIn(CanvasGroup group, RectTransform box)
        {
            if (group != null)
            {
                group.alpha = 0f;
            }

            if (box != null)
            {
                box.localScale = Vector3.one * 0.84f;
            }

            const float duration = 0.26f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(t);
                if (group != null)
                {
                    group.alpha = Mathf.Lerp(0f, 1f, EaseOutCubic(t));
                }

                if (box != null)
                {
                    box.localScale = Vector3.one * Mathf.LerpUnclamped(0.84f, 1f, eased);
                }

                yield return null;
            }

            if (group != null)
            {
                group.alpha = 1f;
            }

            if (box != null)
            {
                box.localScale = Vector3.one;
            }

            modalAnimationRoutine = null;
        }

        private static IEnumerator PulseRect(RectTransform rectTransform, float peakScale, float duration)
        {
            if (rectTransform == null)
            {
                yield break;
            }

            var baseScale = rectTransform.localScale;
            var halfDuration = duration * 0.5f;
            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / halfDuration);
                rectTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * peakScale, EaseOutCubic(t));
                yield return null;
            }

            for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / halfDuration);
                rectTransform.localScale = Vector3.LerpUnclamped(baseScale * peakScale, baseScale, EaseInOut(t));
                yield return null;
            }

            rectTransform.localScale = baseScale;
        }

        private void CreateCelebrationRoot()
        {
            if (celebrationRoot != null)
            {
                return;
            }

            celebrationRoot = new GameObject("CelebrationLayer", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            celebrationRoot.SetParent(safeRoot, false);
            UIFactory.Stretch(celebrationRoot);
            celebrationGroup = celebrationRoot.GetComponent<CanvasGroup>();
            celebrationGroup.blocksRaycasts = false;
            celebrationGroup.interactable = false;
            celebrationRoot.gameObject.SetActive(false);
        }

        private void PlayWinCelebration(int stars)
        {
            CreateCelebrationRoot();
            if (celebrationRoutine != null)
            {
                StopCoroutine(celebrationRoutine);
            }

            celebrationToken++;
            celebrationRoutine = StartCoroutine(AnimateWinCelebration(Mathf.Clamp(stars, 1, 3)));
        }

        private void StopCelebration()
        {
            celebrationToken++;
            if (celebrationRoutine != null)
            {
                StopCoroutine(celebrationRoutine);
                celebrationRoutine = null;
            }

            if (celebrationRoot == null)
            {
                return;
            }

            for (var i = celebrationRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(celebrationRoot.GetChild(i).gameObject);
            }

            celebrationRoot.gameObject.SetActive(false);
        }

        private IEnumerator AnimateWinCelebration(int stars)
        {
            var token = celebrationToken;
            for (var i = celebrationRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(celebrationRoot.GetChild(i).gameObject);
            }

            celebrationRoot.gameObject.SetActive(true);
            celebrationRoot.SetAsLastSibling();

            celebrationGroup.alpha = 1f;
            SpawnCelebrationRays(token);

            var random = new System.Random(1300 + stars * 97);
            var count = 52 + stars * 18;
            for (var i = 0; i < count; i++)
            {
                StartCoroutine(AnimateConfettiPiece(random, i, token));
            }

            yield return new WaitForSecondsRealtime(1.45f);
            if (token != celebrationToken)
            {
                yield break;
            }

            for (var elapsed = 0f; elapsed < 0.4f; elapsed += Time.unscaledDeltaTime)
            {
                if (token != celebrationToken)
                {
                    yield break;
                }

                celebrationGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
                yield return null;
            }

            if (token == celebrationToken)
            {
                for (var i = celebrationRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(celebrationRoot.GetChild(i).gameObject);
                }

                celebrationRoot.gameObject.SetActive(false);
                celebrationRoutine = null;
            }
        }

        private void SpawnCelebrationRays(int token)
        {
            var rayRoot = new GameObject("Rays", typeof(RectTransform));
            rayRoot.transform.SetParent(celebrationRoot, false);
            var rootRect = rayRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = new Vector2(0f, 110f);

            for (var i = 0; i < 14; i++)
            {
                var ray = UIFactory.CreateImage($"Ray_{i:00}", rayRoot.transform, new Color(1f, 0.88f, 0.28f, 0.14f));
                ray.raycastTarget = false;
                var rect = ray.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(28f, 1040f);
                rect.localRotation = Quaternion.Euler(0f, 0f, i * (360f / 14f));
            }

            StartCoroutine(AnimateRayRoot(rootRect, token));
        }

        private IEnumerator AnimateRayRoot(RectTransform root, int token)
        {
            const float duration = 1.1f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (token != celebrationToken || root == null)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                root.localRotation = Quaternion.Euler(0f, 0f, 26f * t);
                root.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.08f, EaseOutCubic(t));
                yield return null;
            }
        }

        private IEnumerator AnimateConfettiPiece(System.Random random, int index, int token)
        {
            var particle = UIFactory.CreateImage($"Confetti_{index:00}", celebrationRoot, GetConfettiColor(index));
            particle.raycastTarget = false;
            var rect = particle.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(12f + index % 3 * 6f, 18f + index % 4 * 5f);

            var width = safeRoot != null && safeRoot.rect.width > 1f ? safeRoot.rect.width : 1080f;
            var height = safeRoot != null && safeRoot.rect.height > 1f ? safeRoot.rect.height : 1920f;
            var startX = (float)(random.NextDouble() * width - width * 0.5f);
            var startY = height * 0.5f + (float)(random.NextDouble() * 180f);
            var drift = (float)(random.NextDouble() * 340f - 170f);
            var endY = -height * 0.5f - 120f - (index % 6) * 18f;
            var delay = (float)(random.NextDouble() * 0.22f);
            var duration = 0.85f + (float)(random.NextDouble() * 0.55f);
            var spin = (float)(random.NextDouble() * 420f + 180f) * (index % 2 == 0 ? 1f : -1f);

            rect.anchoredPosition = new Vector2(startX, startY);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (token != celebrationToken || particle == null)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var x = startX + drift * EaseInOut(t) + Mathf.Sin((t * 5f + index) * Mathf.PI) * 22f;
                var y = Mathf.Lerp(startY, endY, EaseInCubic(t));
                rect.anchoredPosition = new Vector2(x, y);
                rect.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                rect.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.78f, t);
                var color = particle.color;
                color.a = t < 0.8f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f);
                particle.color = color;
                yield return null;
            }

            if (particle != null)
            {
                Destroy(particle.gameObject);
            }
        }

        private static Color GetConfettiColor(int index)
        {
            switch (index % 6)
            {
                case 0:
                    return new Color(1f, 0.4f, 0.38f, 1f);
                case 1:
                    return new Color(0.28f, 0.72f, 1f, 1f);
                case 2:
                    return new Color(1f, 0.86f, 0.22f, 1f);
                case 3:
                    return new Color(0.34f, 0.9f, 0.52f, 1f);
                case 4:
                    return new Color(0.78f, 0.52f, 1f, 1f);
                default:
                    return new Color(1f, 0.58f, 0.24f, 1f);
            }
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            var shifted = t - 1f;
            return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
        }
    }
}
