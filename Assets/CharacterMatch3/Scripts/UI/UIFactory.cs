using System.Collections;
using CharacterMatch3.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CharacterMatch3.UI
{
    public static class UIFactory
    {
        private static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (cachedFont == null)
                    {
                        cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                }

                return cachedFont;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        public static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        public static Text CreateText(string name, Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var label = textObject.GetComponent<Text>();
            label.text = text;
            label.font = DefaultFont;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = color;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size / 2);
            label.resizeTextMaxSize = size;
            return label;
        }

        public static Button CreateButton(string name, Transform parent, string label, UnityAction callback)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(1f, 0.83f, 0.32f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = image.color,
                highlightedColor = Color.Lerp(image.color, Color.white, 0.18f),
                pressedColor = Color.Lerp(image.color, Color.black, 0.18f),
                selectedColor = Color.Lerp(image.color, Color.white, 0.08f),
                disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.72f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            if (callback != null)
            {
                button.onClick.AddListener(callback);
            }

            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.24f, 0.15f, 0.05f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var text = CreateText("Label", buttonObject.transform, label, 34, TextAnchor.MiddleCenter, new Color(0.15f, 0.13f, 0.1f));
            Stretch(text.rectTransform);
            text.fontStyle = FontStyle.Bold;
            var textShadow = text.gameObject.AddComponent<Shadow>();
            textShadow.effectColor = new Color(1f, 1f, 1f, 0.36f);
            textShadow.effectDistance = new Vector2(0f, 1f);

            buttonObject.AddComponent<ButtonPressAnimator>();
            return button;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        public static void SetAnchored(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        public static void EnsureEventSystem()
        {
            var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems.Length > 0)
            {
                var keep = UnityEngine.EventSystems.EventSystem.current != null
                    ? UnityEngine.EventSystems.EventSystem.current
                    : eventSystems[0];

                foreach (var candidateEventSystem in eventSystems)
                {
                    if (candidateEventSystem != null && candidateEventSystem != keep)
                    {
                        Object.Destroy(candidateEventSystem.gameObject);
                    }
                }

                return;
            }

            var createdEventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Object.DontDestroyOnLoad(createdEventSystem);
        }
    }

    internal sealed class ButtonPressAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private RectTransform rectTransform;
        private Coroutine animationRoutine;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateTo(0.94f, 0.06f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateTo(1f, 0.1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(1f, 0.1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HapticsManager.Light();
            AnimateTo(1.04f, 0.08f, 1f, 0.1f);
        }

        private void AnimateTo(float scale, float duration)
        {
            AnimateTo(scale, duration, scale, 0f);
        }

        private void AnimateTo(float firstScale, float firstDuration, float secondScale, float secondDuration)
        {
            if (!isActiveAndEnabled || rectTransform == null)
            {
                return;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateScale(firstScale, firstDuration, secondScale, secondDuration));
        }

        private IEnumerator AnimateScale(float firstScale, float firstDuration, float secondScale, float secondDuration)
        {
            yield return AnimateScaleStep(firstScale, firstDuration);
            if (secondDuration > 0f)
            {
                yield return AnimateScaleStep(secondScale, secondDuration);
            }

            animationRoutine = null;
        }

        private IEnumerator AnimateScaleStep(float targetScale, float duration)
        {
            var start = rectTransform.localScale;
            var end = baseScale * targetScale;
            if (duration <= 0f)
            {
                rectTransform.localScale = end;
                yield break;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = t * t * (3f - 2f * t);
                rectTransform.localScale = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }

            rectTransform.localScale = end;
        }
    }
}
