using CharacterMatch3.Core;
using CharacterMatch3.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CharacterMatch3.Editor
{
    public static class LevelMapSceneUIBuilder
    {
        private const string LevelMapScenePath = CharacterMatch3Constants.RootPath + "/Scenes/LevelMap.unity";

        [MenuItem("Character Match-3/Map Tools/Create Scene Editable Map UI")]
        public static void CreateSceneEditableMapUi()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(LevelMapScenePath, OpenSceneMode.Single);
            var maps = Object.FindObjectsByType<LevelMapUI>(FindObjectsSortMode.None);
            if (maps.Length == 0)
            {
                Debug.LogWarning($"No LevelMapUI found in {LevelMapScenePath}.");
                return;
            }

            foreach (var map in maps)
            {
                EnsureSceneEditableMapUI(map);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LevelMap scene editable UI is ready. Select LevelMapCanvas/SafeArea/Scene Editable Top Bar to adjust it in Scene view.");
        }

        internal static void EnsureSceneEditableMapUI(LevelMapUI map)
        {
            if (map == null)
            {
                return;
            }

            var canvas = GetOrCreateCanvas();
            var safeRoot = GetOrCreateRectChild(canvas.transform, "SafeArea");
            EnsureComponent<SafeAreaController>(safeRoot.gameObject);
            Stretch(safeRoot);

            var topBarRoot = GetOrCreateRectChild(safeRoot, "Scene Editable Top Bar");
            Stretch(topBarRoot);

            var titlePlate = GetOrCreateImage(topBarRoot, "TitlePlate", new Color(1f, 0.9f, 0.66f, 0.96f));
            SetAnchored(titlePlate.rectTransform, new Vector2(0.12f, 0.89f), new Vector2(0.88f, 0.975f), Vector2.zero, Vector2.zero);
            EnsureShadow(titlePlate.gameObject, new Color(0.18f, 0.1f, 0.04f, 0.24f), new Vector2(0f, -5f));

            var title = GetOrCreateText(titlePlate.transform, "Title", "Lumiko Map", 52, TextAnchor.MiddleCenter, new Color(0.35f, 0.15f, 0.04f));
            title.fontStyle = FontStyle.Bold;
            Stretch(title.rectTransform);

            var backButton = GetOrCreateButton(topBarRoot, "BackButton", "<", new Color(0.23f, 0.66f, 0.94f, 0.98f), Color.white);
            SetAnchored(backButton.GetComponent<RectTransform>(), new Vector2(0.035f, 0.91f), new Vector2(0.13f, 0.97f), Vector2.zero, Vector2.zero);

            var progressPanel = GetOrCreateImage(topBarRoot, "LevelProgress", new Color(1f, 0.86f, 0.48f, 0.96f));
            SetAnchored(progressPanel.rectTransform, new Vector2(0.69f, 0.82f), new Vector2(0.96f, 0.875f), Vector2.zero, Vector2.zero);

            var progressText = GetOrCreateText(progressPanel.transform, "ProgressText", $"Level 1/{CharacterMatch3Constants.LastLevel}", 27, TextAnchor.MiddleCenter, new Color(0.35f, 0.18f, 0.04f));
            progressText.fontStyle = FontStyle.Bold;
            Stretch(progressText.rectTransform);

            topBarRoot.SetAsLastSibling();
            AssignReferences(map, canvas, safeRoot, topBarRoot, title, backButton, progressText);
            EnsureEventSystem();
            SetLayerRecursively(canvas.gameObject, LayerMask.NameToLayer("UI"));
        }

        private static Canvas GetOrCreateCanvas()
        {
            var canvasObject = GameObject.Find("LevelMapCanvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("LevelMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            var canvas = EnsureComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = EnsureComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureComponent<GraphicRaycaster>(canvasObject);
            return canvas;
        }

        private static RectTransform GetOrCreateRectChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return EnsureComponent<RectTransform>(existing.gameObject);
            }

            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Image GetOrCreateImage(Transform parent, string name, Color color)
        {
            var rect = GetOrCreateRectChild(parent, name);
            var image = EnsureComponent<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            return image;
        }

        private static Button GetOrCreateButton(Transform parent, string name, string label, Color color, Color textColor)
        {
            var rect = GetOrCreateRectChild(parent, name);
            var image = EnsureComponent<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;

            var button = EnsureComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = color,
                highlightedColor = Color.Lerp(color, Color.white, 0.18f),
                pressedColor = Color.Lerp(color, Color.black, 0.18f),
                selectedColor = Color.Lerp(color, Color.white, 0.08f),
                disabledColor = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.58f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            EnsureShadow(rect.gameObject, new Color(0.24f, 0.15f, 0.05f, 0.28f), new Vector2(0f, -5f));
            var text = GetOrCreateText(rect, "Label", label, 34, TextAnchor.MiddleCenter, textColor);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(button);
            return button;
        }

        private static Text GetOrCreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Color color)
        {
            var rect = GetOrCreateRectChild(parent, name);
            var text = EnsureComponent<Text>(rect.gameObject);
            text.text = value;
            text.font = UIFactory.DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void EnsureShadow(GameObject target, Color color, Vector2 distance)
        {
            var shadow = EnsureComponent<Shadow>(target);
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            EditorUtility.SetDirty(shadow);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = target.AddComponent<T>();
            EditorUtility.SetDirty(target);
            return component;
        }

        private static void AssignReferences(LevelMapUI map, Canvas canvas, RectTransform safeRoot, RectTransform topBarRoot, Text title, Button backButton, Text progressText)
        {
            var serialized = new SerializedObject(map);
            SetObjectReference(serialized, "sceneCanvas", canvas);
            SetObjectReference(serialized, "sceneSafeRoot", safeRoot);
            SetObjectReference(serialized, "sceneTopBarRoot", topBarRoot);
            SetObjectReference(serialized, "sceneTitleText", title);
            SetObjectReference(serialized, "sceneBackButton", backButton);
            SetObjectReference(serialized, "sceneProgressText", progressText);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
        }

        private static void SetObjectReference(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void Stretch(RectTransform rectTransform)
        {
            SetAnchored(rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetAnchored(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.localScale = Vector3.one;
            EditorUtility.SetDirty(rectTransform);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
