using UnityEngine;

namespace CharacterMatch3.Core
{
    public static class RuntimeSceneUtility
    {
        public static void EnsureMainCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            if (Object.FindFirstObjectByType<Camera>() != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.19f, 0.24f);
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            cameraObject.AddComponent<AudioListener>();
        }
    }
}
