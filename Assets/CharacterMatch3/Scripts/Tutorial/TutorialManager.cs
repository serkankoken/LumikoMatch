using CharacterMatch3.Board;
using CharacterMatch3.Save;
using CharacterMatch3.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterMatch3.Tutorial
{
    public sealed class TutorialManager : MonoBehaviour
    {
        private GameObject overlay;
        private Text instructionText;
        private LevelDefinition activeLevel;
        private BoardController activeBoard;

        public void BeginIfNeeded(LevelDefinition level, BoardController board)
        {
            activeLevel = level;
            activeBoard = board;

            if (string.IsNullOrWhiteSpace(level.tutorialInstructions) || SaveManager.IsTutorialCompleted(level.levelNumber))
            {
                Hide();
                board.SwapAllowedByTutorial = null;
                return;
            }

            EnsureOverlay();
            instructionText.text = level.tutorialInstructions;
            overlay.SetActive(true);

            board.SwapAllowedByTutorial = (from, to) =>
            {
                if (!level.tutorialForcedSwap.enabled)
                {
                    return true;
                }

                var forcedA = level.tutorialForcedSwap.from;
                var forcedB = level.tutorialForcedSwap.to;
                return (from == forcedA && to == forcedB) || (from == forcedB && to == forcedA);
            };
        }

        public void MarkCurrentTutorialComplete()
        {
            if (activeLevel == null)
            {
                return;
            }

            SaveManager.MarkTutorialCompleted(activeLevel.levelNumber);
            if (activeBoard != null)
            {
                activeBoard.SwapAllowedByTutorial = null;
            }

            Hide();
        }

        private void Hide()
        {
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }

        private void EnsureOverlay()
        {
            if (overlay != null)
            {
                return;
            }

            var canvas = UIFactory.CreateCanvas("TutorialCanvas");
            overlay = UIFactory.CreatePanel("TutorialOverlay", canvas.transform, new Color(0f, 0f, 0f, 0.42f));
            overlay.GetComponent<Image>().raycastTarget = false;

            var panel = UIFactory.CreatePanel("InstructionBand", overlay.transform, new Color(1f, 0.97f, 0.83f, 0.96f));
            UIFactory.SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);
            panel.GetComponent<Image>().raycastTarget = false;

            instructionText = UIFactory.CreateText("InstructionText", panel.transform, string.Empty, 34, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.14f));
            UIFactory.SetAnchored(instructionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 12), new Vector2(-24, -12));
            instructionText.raycastTarget = false;

            var hand = UIFactory.CreateText("HandPointer", overlay.transform, "DRAG", 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchored(hand.rectTransform, new Vector2(0.42f, 0.29f), new Vector2(0.58f, 0.35f), Vector2.zero, Vector2.zero);
            hand.raycastTarget = false;

            var skipButton = UIFactory.CreateButton("SkipTutorialButton", overlay.transform, "Skip", MarkCurrentTutorialComplete);
            UIFactory.SetAnchored(skipButton.GetComponent<RectTransform>(), new Vector2(0.78f, 0.225f), new Vector2(0.92f, 0.285f), Vector2.zero, Vector2.zero);
        }
    }
}
