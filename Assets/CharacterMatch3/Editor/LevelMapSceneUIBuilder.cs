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
        private const string AutoRefreshSessionKey = "CharacterMatch3.LevelMapSceneUIBuilder.AutoRefreshOpenLevelMap";
        private const int PreviewLevelCount = 21;
        private const float PreviewNodeSize = 76f;
        private const float PreviewPlayerMarkerSize = 66f;

        private static readonly Vector2[] FallbackForestFactors =
        {
            new Vector2(0.47f, 0.88f),
            new Vector2(0.43f, 0.73f),
            new Vector2(0.54f, 0.64f),
            new Vector2(0.5f, 0.49f),
            new Vector2(0.42f, 0.36f),
            new Vector2(0.56f, 0.24f),
            new Vector2(0.51f, 0.11f)
        };

        private static readonly Vector2[] FallbackBeachFactors =
        {
            new Vector2(0.45f, 0.7f),
            new Vector2(0.54f, 0.56f),
            new Vector2(0.42f, 0.43f),
            new Vector2(0.52f, 0.31f),
            new Vector2(0.47f, 0.2f),
            new Vector2(0.4f, 0.11f),
            new Vector2(0.5f, 0.04f)
        };

        private static readonly Vector2[] FallbackDesertFactors =
        {
            new Vector2(0.49f, 0.96f),
            new Vector2(0.56f, 0.88f),
            new Vector2(0.48f, 0.8f),
            new Vector2(0.56f, 0.72f),
            new Vector2(0.47f, 0.64f),
            new Vector2(0.54f, 0.56f),
            new Vector2(0.49f, 0.48f)
        };

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
            Debug.Log("LevelMap scene editable UI is ready. Select LevelMapCanvas/SafeArea/Scene Editable Map Preview or Scene Editable Top Bar to adjust it in Scene view.");
        }

        [InitializeOnLoadMethod]
        private static void QueueAutoRefreshOpenLevelMapScene()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoRefreshSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoRefreshOpenLevelMapScene;
        }

        private static void TryAutoRefreshOpenLevelMapScene()
        {
            if (SessionState.GetBool(AutoRefreshSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoRefreshOpenLevelMapScene;
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != LevelMapScenePath)
            {
                return;
            }

            var maps = Object.FindObjectsByType<LevelMapUI>(FindObjectsSortMode.None);
            if (maps.Length == 0)
            {
                return;
            }

            foreach (var map in maps)
            {
                EnsureSceneEditableMapUI(map);
            }

            SessionState.SetBool(AutoRefreshSessionKey, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("LevelMap scene editable preview auto-refreshed. Select LevelMapCanvas/SafeArea/Scene Editable Map Preview in Hierarchy.");
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

            var mapPreviewRoot = GetOrCreateRectChild(safeRoot, "Scene Editable Map Preview");
            Stretch(mapPreviewRoot);
            BuildMapPreview(map, mapPreviewRoot);

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

            mapPreviewRoot.SetAsFirstSibling();
            topBarRoot.SetAsLastSibling();
            AssignReferences(map, canvas, safeRoot, mapPreviewRoot, topBarRoot, title, backButton, progressText);
            EnsureEventSystem();
            SetLayerRecursively(canvas.gameObject, LayerMask.NameToLayer("UI"));
        }

        private static void BuildMapPreview(LevelMapUI map, RectTransform previewRoot)
        {
            var serialized = new SerializedObject(map);
            var backgroundSprite = ReadSprite(serialized, "backgroundSprite") ??
                                   ReadSprite(serialized, "mapMeadowSprite") ??
                                   ReadSprite(serialized, "mapBeachSprite") ??
                                   ReadSprite(serialized, "mapDesertSprite");
            var completedSprite = ReadSprite(serialized, "completedNodeSprite");
            var currentSprite = ReadSprite(serialized, "currentNodeSprite");
            var lockedSprite = ReadSprite(serialized, "lockedNodeSprite");

            var background = GetOrCreateImage(previewRoot, "PreviewMapBackground", backgroundSprite != null ? Color.white : new Color(0.36f, 0.76f, 0.96f));
            background.sprite = backgroundSprite;
            background.preserveAspect = false;
            background.raycastTarget = false;
            Stretch(background.rectTransform);
            EditorUtility.SetDirty(background);

            var nodesRoot = GetOrCreateRectChild(previewRoot, "Preview Level Nodes");
            Stretch(nodesRoot);
            ClearChildren(nodesRoot);

            var forestFactors = ReadVector2Array(serialized, "forestLevelPositionFactors", FallbackForestFactors);
            var beachFactors = ReadVector2Array(serialized, "beachLevelPositionFactors", FallbackBeachFactors);
            var desertFactors = ReadVector2Array(serialized, "desertLevelPositionFactors", FallbackDesertFactors);
            var meadowPercent = ReadFloat(serialized, "meadowSlicePercent", 0.36f);
            var beachPercent = ReadFloat(serialized, "beachSlicePercent", 0.32f);
            var desertPercent = Mathf.Max(0.12f, 1f - meadowPercent - beachPercent);

            for (var levelNumber = CharacterMatch3Constants.FirstLevel; levelNumber <= Mathf.Min(PreviewLevelCount, CharacterMatch3Constants.LastLevel); levelNumber++)
            {
                var sprite = levelNumber == CharacterMatch3Constants.FirstLevel
                    ? currentSprite
                    : levelNumber <= 5
                        ? completedSprite
                        : lockedSprite;
                var node = GetOrCreateImage(nodesRoot, $"PreviewLevel_{levelNumber:000}", Color.white);
                node.sprite = sprite;
                node.preserveAspect = true;
                node.raycastTarget = false;

                var rect = node.rectTransform;
                rect.anchorMin = rect.anchorMax = GetPreviewLevelAnchor(levelNumber, forestFactors, beachFactors, desertFactors, meadowPercent, beachPercent, desertPercent);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * PreviewNodeSize;
                rect.anchoredPosition = Vector2.zero;

                var label = GetOrCreateText(rect, "Number", levelNumber.ToString(), 26, TextAnchor.MiddleCenter, Color.white);
                label.fontStyle = FontStyle.Bold;
                Stretch(label.rectTransform);
            }

            var marker = GetOrCreateText(previewRoot, "PreviewPlayerMarker", "B", 32, TextAnchor.MiddleCenter, new Color(0.35f, 0.18f, 0.05f));
            marker.fontStyle = FontStyle.Bold;
            marker.color = new Color(0.35f, 0.18f, 0.05f);
            var markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = GetPreviewLevelAnchor(CharacterMatch3Constants.FirstLevel, forestFactors, beachFactors, desertFactors, meadowPercent, beachPercent, desertPercent) + new Vector2(0f, 0.045f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = Vector2.one * PreviewPlayerMarkerSize;
            markerRect.anchoredPosition = Vector2.zero;
            marker.transform.SetAsLastSibling();

            EditorUtility.SetDirty(previewRoot);
        }

        private static Vector2 GetPreviewLevelAnchor(int levelNumber, Vector2[] forestFactors, Vector2[] beachFactors, Vector2[] desertFactors, float meadowPercent, float beachPercent, float desertPercent)
        {
            var zeroBased = Mathf.Max(0, levelNumber - CharacterMatch3Constants.FirstLevel);
            var section = zeroBased / 7;
            var localIndex = zeroBased % 7;
            var factors = section == 0 ? forestFactors : section == 1 ? beachFactors : desertFactors;
            var factor = factors[Mathf.Clamp(localIndex, 0, factors.Length - 1)];

            var bottom = section == 0 ? 0f : section == 1 ? meadowPercent : meadowPercent + beachPercent;
            var height = section == 0 ? meadowPercent : section == 1 ? beachPercent : desertPercent;
            var x = Mathf.Clamp01(factor.x);
            var y = Mathf.Clamp01(bottom + (1f - Mathf.Clamp01(factor.y)) * height);
            return new Vector2(x, y);
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

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static Sprite ReadSprite(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as Sprite : null;
        }

        private static float ReadFloat(SerializedObject serialized, string propertyName, float fallback)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.floatValue : fallback;
        }

        private static Vector2[] ReadVector2Array(SerializedObject serialized, string propertyName, Vector2[] fallback)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return fallback;
            }

            var values = new Vector2[property.arraySize];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = property.GetArrayElementAtIndex(i).vector2Value;
            }

            return values;
        }

        private static void AssignReferences(LevelMapUI map, Canvas canvas, RectTransform safeRoot, RectTransform mapPreviewRoot, RectTransform topBarRoot, Text title, Button backButton, Text progressText)
        {
            var serialized = new SerializedObject(map);
            SetObjectReference(serialized, "sceneCanvas", canvas);
            SetObjectReference(serialized, "sceneSafeRoot", safeRoot);
            SetObjectReference(serialized, "sceneMapPreviewRoot", mapPreviewRoot);
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
