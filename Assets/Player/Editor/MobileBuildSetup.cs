using System;
using System.IO;
using System.Linq;
using Project.GameFlow;
using Project.InputAbstraction;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MobileBuildSetup
{
    public const string AndroidApplicationId =
        "com.miao1suki.shapeofcolor";
    public const string MenuScene = "Assets/_Project/Scenes/Start/MainMenu.unity";
    public const string GameScene = "Assets/_Project/Scenes/Playing/Gaming.unity";
    public const string UiPrefab = "Assets/_Project/Content/UI/SharedGameUI.prefab";
    public static void Prepare()
    {
        var root = PrefabUtility.LoadPrefabContents(UiPrefab);
        try
        {
            foreach (var old in root.GetComponentsInChildren<PlatformUILayoutController>(true))
            {
                foreach (var mobile in old.MobileOnlyObjects) if (mobile != null) mobile.SetActive(false);
                old.SetVisibilityGroups(old.DesktopOnlyObjects.ToArray(), Array.Empty<GameObject>());
            }
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                if (canvas.name == "GameUI") SafeContent(canvas);
            }
            var mobileRoot = root.transform.Find("Mobile Gameplay Canvas");
            if (mobileRoot == null)
            {
                var obj = new GameObject("Mobile Gameplay Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileGameplayControls));
                obj.transform.SetParent(root.transform, false);
                var canvas = obj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
                var scaler = obj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var controls = obj.GetComponent<MobileGameplayControls>();
                var font = AssetDatabase.FindAssets("Cubic_11 t:TMP_FontAsset")
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).First(f => f != null);
                controls.Configure(font, root.GetComponentInChildren<GameUiRouter>(true));
                controls.BuildView();
            }
            PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var menu = EditorSceneManager.OpenScene(MenuScene);
        var controller = UnityEngine.Object.FindFirstObjectByType<Project.StartMenu.StartMenuController>();
        if (controller != null)
        {
            Project.StartMenu.StartMenuController.ConfigureCanvasScaler(controller.Canvas);
            SafeContent(controller.Canvas);
        }
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();
    }
    static void SafeContent(Canvas canvas)
    {
        if (canvas == null || canvas.transform.Find("Safe Content") != null) return;
        var children = canvas.transform.Cast<Transform>().ToArray();
        var rect = new GameObject("Safe Content", typeof(RectTransform), typeof(ScreenSafeArea)).GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.GetComponent<ScreenSafeArea>().Refresh();
        foreach (var child in children)
        {
            // Full-bleed artwork remains outside the inset content region.
            if (child.name == "Background" || child.name == "Pause Backdrop") continue;
            child.SetParent(rect, false);
        }
    }
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows/形之彩.exe");
    public static void BuildAndroid() => Build(BuildTarget.Android, "Android/形之彩.apk");
    static void Build(BuildTarget target, string suffix)
    {
        string outputRoot = Environment.GetEnvironmentVariable("TAPTAP_TEST_BUILD_ROOT");
        if (string.IsNullOrEmpty(outputRoot)) throw new Exception("TAPTAP_TEST_BUILD_ROOT must specify the output directory.");
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
            throw new Exception("Failed to switch build target: " + target);
        PlayerSettings.productName = "形之彩";
        Project.Player.Editor.MobileArtworkSetup.ApplyOfficialIcons();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        if (target == BuildTarget.Android)
        {
            Project.GameFlow.Editor.AndroidGraphicsCompatibilityBuildGuard.Configure();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android,
                AndroidApplicationId);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
        }
        string output = Path.Combine(outputRoot, suffix);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { GameScene }, target = target,
            locationPathName = output, options = BuildOptions.None
        });
        File.WriteAllText(Path.Combine(outputRoot, target + "-report.txt"),
            $"{report.summary.result}\n{output}\nbytes={report.summary.totalSize}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\nscenes={GameScene}\n");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + target);
        if (target == BuildTarget.StandaloneWindows64)
            ArchiveWindows(Path.GetDirectoryName(output), outputRoot);
    }

    static void ArchiveWindows(string playerDirectory, string outputRoot)
    {
        string sevenZip = Environment.GetEnvironmentVariable("TAPTAP_7ZIP");
        if (string.IsNullOrEmpty(sevenZip))
            sevenZip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
        if (!File.Exists(sevenZip))
            throw new FileNotFoundException("Windows player succeeded; install 7-Zip or set TAPTAP_7ZIP to create the upload archive.", sevenZip);
        string archive = Path.GetFullPath(Path.Combine(outputRoot, "形之彩.7z"));
        string pending = Path.GetFullPath(Path.Combine(outputRoot, "形之彩.pending.7z"));
        if (File.Exists(pending))
            throw new IOException("A pending archive already exists; inspect it before retrying: " + pending);
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = sevenZip,
            WorkingDirectory = Path.GetFullPath(playerDirectory),
            Arguments = "a -t7z -mx=5 \"" + pending + "\" .\\* -xr!*DoNotShip* -xr!*DontShip*",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception("7-Zip failed: " + process.ExitCode);
        }
        if (File.Exists(archive)) File.Replace(pending, archive, archive + ".previous");
        else File.Move(pending, archive);
        Debug.Log("Windows upload archive (player files directly at root): " + archive);
    }
}
