using System.IO;
using System.Reflection;
using Project.Achievements;
using Project.BlockFeatures;
using Project.FastTravel;
using Project.GameFlow;
using Project.Player;
using Project.Saving;
using Project.WorldVisibility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Editor-only setup for the explicit manual-test copy; no production rule changes.</summary>
[InitializeOnLoad]
public static class MapVisibilityTestTools
{
    public const string ScenePath = "Assets/_Project/Development/MapVisibilityTest/MapVisibilityTest.unity";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool prepared, opened;
    static string oldRunDirectory;
    static AchievementCatalogSO testCatalog;
    static AchievementManager achievementManager;

    static MapVisibilityTestTools()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/2026TapTap/测试/打开地图传送与光照测试场景")]
    public static void OpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Prepare()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) return;
        prepared = true; opened = false;
        string root = Path.GetFullPath("Library/MapVisibilityManualTest");
        oldRunDirectory = RunSaveService.SaveDirectoryOverrideForTests;
        RunSaveService.SaveDirectoryOverrideForTests = Path.Combine(root, "Saves");
        if (RunSessionCoordinator.Instance != null) RunSessionCoordinator.Instance.enabled = false;

        // Use a transient catalog instance: never change the shared Resources asset.
        achievementManager = AchievementManager.Instance;
        if (Field(typeof(AchievementManager), "catalog").GetValue(achievementManager) is AchievementCatalogSO source)
        {
            testCatalog = Object.Instantiate(source);
            testCatalog.hideFlags = HideFlags.HideAndDontSave;
            Field(typeof(AchievementCatalogSO), "saveDirectoryOverride").SetValue(testCatalog, Path.Combine(root, "Achievements"));
            Field(typeof(AchievementManager), "catalog").SetValue(achievementManager, testCatalog);
            Field(typeof(AchievementManager), "saveService").SetValue(achievementManager, new AchievementSaveService(testCatalog));
        }
        foreach (var context in Object.FindObjectsByType<LevelSceneContext>(FindObjectsSortMode.None))
            if (context.gameObject.scene.path == ScenePath) context.Activate();
        foreach (var point in Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsSortMode.None))
            if (point.gameObject.scene.path == ScenePath && !point.IsBossArenaRespawnPoint) point.Activate();
        Debug.Log("[地图光照测试] 已开放地图及普通重生点。T开关地图；光照可在主相机组件调节。测试文件仅写入Library/MapVisibilityManualTest。");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !prepared) return;
        // Finish this test session before another scene can reuse its temporary progress.
        if (SceneManager.GetActiveScene().path != ScenePath) { EditorApplication.isPlaying = false; return; }
        foreach (var player in PlayerController.ActivePlayers)
        {
            if (!player || player.gameObject.scene.path != ScenePath) continue;
            var map = player.GetComponent<WorldTravelMap>();
            if (!map) continue;
            if (!opened && map.TryOpen()) opened = true;
            break;
        }
    }

    [MenuItem("Tools/2026TapTap/测试/地图测试：开关地图")]
    static void ToggleMap()
    {
        foreach (var player in PlayerController.ActivePlayers)
            if (player && player.gameObject.scene.path == ScenePath)
            {
                var map = player.GetComponent<WorldTravelMap>();
                if (map) { if (map.IsOpen) map.Close(); else map.TryOpen(); }
            }
    }

    [MenuItem("Tools/2026TapTap/测试/地图测试：开关光照")]
    static void ToggleLighting()
    {
        var camera = Camera.main;
        if (!camera || camera.gameObject.scene.path != ScenePath) return;
        var lighting = camera.GetComponent<WorldVisibility2D>();
        if (lighting) { lighting.enabled = !lighting.enabled; Selection.activeGameObject = camera.gameObject; }
    }

    [MenuItem("Tools/2026TapTap/测试/地图测试：开关地图", true)]
    [MenuItem("Tools/2026TapTap/测试/地图测试：开关光照", true)]
    static bool IsTesting() => EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath;

    static void OnPlayState(PlayModeStateChange state)
    {
        // Keep isolation until OnApplicationQuit/OnDestroy have saved to the test directory.
        if (state == PlayModeStateChange.EnteredEditMode) Restore();
    }

    static void Restore()
    {
        if (!prepared) return;
        prepared = false; opened = false;
        RunSaveService.SaveDirectoryOverrideForTests = oldRunDirectory;
        if (testCatalog) Object.DestroyImmediate(testCatalog);
        testCatalog = null; achievementManager = null;
    }

    static FieldInfo Field(System.Type type, string name) => type.GetField(name, Private)
        ?? throw new System.MissingFieldException(type.FullName, name);
}
