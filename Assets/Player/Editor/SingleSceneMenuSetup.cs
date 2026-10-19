using System;
using Project.GameFlow;
using Project.StartMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Project.Player.Editor
{
    /// <summary>Explicit offline migration; never saves the formal level or a planning document.</summary>
    public static class SingleSceneMenuSetup
    {
        public const string PrefabPath = "Assets/_Project/Resources/UI/WorldMainMenu.prefab";
        private const string AuthoringPrefab = "Assets/_Project/Content/UI/MainMenuUI.prefab";

        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/"))
                throw new InvalidOperationException("只允许在隔离 FlowRetirementProbe 生成菜单，禁止保存团队当前内存场景。");
            GameObject root = PrefabUtility.LoadPrefabContents(AuthoringPrefab);
            try
            {
                StartMenuController menu = root.GetComponentInChildren<StartMenuController>(true);
                if (menu == null) throw new InvalidOperationException("MainMenuUI 缺少 StartMenuController。");
                menu.BuildView();
                menu.ApplyArtwork();
                menu.SetPresented(true);
                menu.SetFusionContent(1f);
                var data = new SerializedObject(menu);
                GameObject main = data.FindProperty("mainPanel").objectReferenceValue as GameObject;
                if (menu.Background != null) UnityEngine.Object.DestroyImmediate(menu.Background.gameObject);
                data.Update();
                data.FindProperty("background").objectReferenceValue = null;
                data.FindProperty("statusText").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (main != null)
                    foreach (string obsolete in new[] { "Title Placeholder", "Subtitle", "Status" })
                    {
                        Transform child = main.transform.Find(obsolete);
                        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                foreach (Camera device in root.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(device.gameObject);
                foreach (AudioListener device in root.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(device);
                foreach (EventSystem device in root.GetComponentsInChildren<EventSystem>(true)) UnityEngine.Object.DestroyImmediate(device.gameObject);
                if (menu.GetComponent<StartMenuFlowBridge>() == null) menu.gameObject.AddComponent<StartMenuFlowBridge>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                // Existing authors open this asset too: don't leave a second old-looking menu behind.
                PrefabUtility.SaveAsPrefabAsset(root, AuthoringPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // Compatibility file is new art even before Play. It is never a shipping scene.
            Scene compatibility = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), compatibility);
            var cameraRoot = new GameObject("Compatibility Preview Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraRoot.GetComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = new Color(.11f, .14f, .17f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.SaveScene(compatibility, GameFlowController.CompatibilityMenuScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SINGLE SCENE MENU SETUP PASS: new-art pure UI prefab and cleaned compatibility entry; Gaming and planning documents untouched.");
            EditorApplication.Exit(0);
        }
    }
}
