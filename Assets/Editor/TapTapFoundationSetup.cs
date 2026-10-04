using System.IO;
using Project.Player;
using Project.GameFlow;
using Project.GameFlow.Editor;
using Project.Achievements;
using Project.InputAbstraction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates clean foundation content; never imports legacy test scenes or art.</summary>
public static class TapTapFoundationSetup
{
    public static void Generate()
    {
        GameFlowSceneScaffolder.Generate();
        const string path = "Assets/_Project/Content/Player/RuntimePlayer.prefab";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.Refresh();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "RuntimePlayer"; player.tag = "Player";
            player.AddComponent<PlayerController>();
            prefab = PrefabUtility.SaveAsPrefabAsset(player, path);
            Object.DestroyImmediate(player);
        }
        string[] paths = { GameFlowSceneScaffolder.Level01Path, GameFlowSceneScaffolder.Level02Path, GameFlowSceneScaffolder.Level03Path };
        for (int i = 0; i < paths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(paths[i]);
            var context = Object.FindFirstObjectByType<LevelSceneContext>();
            context.SetPlayerPrefab(prefab);
            context.PlayerSpawn.position = new Vector3(-6, 1.2f, 0);
            if (GameObject.Find("FoundationTestGeometry") == null)
            {
                var root = new GameObject("FoundationTestGeometry");
                Cube("Ground", root.transform, new Vector3(0, -.5f, 0), new Vector3(20, 1, 4));
                Cube("Step", root.transform, new Vector3(0, .35f, 0), new Vector3(2, .7f, 2));
                var goal = Cube("LevelGoal", root.transform, new Vector3(7, 1, 0), new Vector3(1, 2, 2));
                goal.GetComponent<BoxCollider>().isTrigger = true;
                goal.AddComponent<LevelGoal>().Configure(i == 0 ? GameFlowSceneId.Level02 : i == 1 ? GameFlowSceneId.Level03 : GameFlowSceneId.Ending, i == 2);
                var light = new GameObject("LevelLight").AddComponent<Light>();
                light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            }
            EditorSceneManager.SaveScene(scene);
        }
        Scene core = EditorSceneManager.OpenScene(GameFlowSceneScaffolder.CorePath);
        if (!Object.FindFirstObjectByType<AchievementManager>()) new GameObject("AchievementService").AddComponent<AchievementManager>();
        if (!Object.FindFirstObjectByType<AchievementSignalBridge>()) new GameObject("AchievementSignalBridge").AddComponent<AchievementSignalBridge>();
        EditorSceneManager.SaveScene(core);
        Scene uiScene = EditorSceneManager.OpenScene(GameFlowSceneScaffolder.UiPath);
        var router = Object.FindFirstObjectByType<GameUiRouter>();
        Transform hud = router.transform.Find("GameplayHud");
        if (hud.Find("PlatformInputUI") == null)
        {
            EditorApplication.ExecuteMenuItem("GameObject/2026TapTap/Input/创建双平台输入 UI");
            GameObject touch = GameObject.Find("PlatformInputUI");
            touch.transform.SetParent(hud, false);
            Transform lookPad = touch.transform.Find("MobileUI/LookJoystick");
            if (lookPad != null) Object.DestroyImmediate(lookPad.gameObject);
            var layout = touch.GetComponent<PlatformUILayoutController>();
            layout.ReplaceLayoutTargets(touch.GetComponentsInChildren<RectTransform>(true));
            layout.CaptureLayout(InputPlatformMode.Desktop); layout.CaptureLayout(InputPlatformMode.Mobile);
        }
        if (hud.Find("PauseButton") == null)
        {
            var button = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(hud, false);
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-80, -35); rect.sizeDelta = new Vector2(110, 45);
            button.GetComponent<Image>().color = new Color(.12f, .18f, .28f, .85f);
            UnityEventTools.AddPersistentListener(button.GetComponent<Button>().onClick, router.TogglePause);
            var caption = new GameObject("Label", typeof(RectTransform), typeof(Text)); caption.transform.SetParent(button.transform, false);
            var textRect = caption.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = caption.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "暂停"; text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        }
        EditorSceneManager.SaveScene(uiScene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(GameFlowSceneScaffolder.BootstrapPath);
        Debug.Log("2026TapTap foundation initialized. Legacy gameplay/art excluded.");
    }
    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
        cube.transform.SetParent(parent); cube.transform.position = position; cube.transform.localScale = scale;
        return cube;
    }
    public static void ValidateBuild()
    {
        var result = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Validation/2026TapTap.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Foundation build failed");
    }
}
