using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.LevelEditor.Editor;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in the silent isolation project. Opens real authoring windows, enters
// and leaves Play Mode, then writes a Library report; never saves a scene/map.
public static class EditorPlayModeRefreshAudit
{
    private const string Key = "EditorPlayModeRefreshAudit";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double deadline;
    private static PlanningEditorWindow planning;
    private static LevelEditorWindow placement;
    private static int Checks
    {
        get => SessionState.GetInt(Key + ".Checks", 0);
        set => SessionState.SetInt(Key + ".Checks", value);
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio"))
            throw new InvalidOperationException("Silent isolated project only");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start audit in Edit Mode");

        // A preceding profile may have left a workspace open with a legacy
        // absolute save path. Detach it before changing scenes or closing it.
        foreach (var existing in Resources.FindObjectsOfTypeAll<PlanningEditorWindow>())
        {
            Set(existing, "canvas", null);
            Set(existing, "document", null);
            Set(existing, "currentSavePath", string.Empty);
            Set(existing, "documentSavePending", false);
            existing.Close();
        }
        foreach (var existing in Resources.FindObjectsOfTypeAll<LevelEditorWindow>()) existing.Close();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Do not inherit the old global workspace or any real map's save path.
        string migration = "2026TapTap.Planning.LegacyAssigned." + Hash128.Compute(Application.dataPath);
        SessionState.SetBool(Key + ".HadMigration", EditorPrefs.HasKey(migration));
        SessionState.SetString(Key + ".PreviousMigration", EditorPrefs.GetString(migration, string.Empty));
        EditorPrefs.SetString(migration, Key);
        string oldSession = SessionState.GetString("2026TapTap.Planning.Session", string.Empty);
        SessionState.SetString(Key + ".PreviousSession", oldSession);
        string session = Guid.NewGuid().ToString("N");
        SessionState.SetString("2026TapTap.Planning.Session", session);
        string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(Application.dataPath) +
            "." + session + "." + SceneManager.GetActiveScene().handle;
        SessionState.SetString(Key + ".Scope", scope);
        EditorPrefs.SetString(scope + ".Document",
            new PlanningDocument { name = "Editor refresh audit" }.ToPersistentJson(true));
        EditorPrefs.SetString(scope + ".SavePath", string.Empty);
        EditorPrefs.SetBool(scope + ".AutoSync", false);
        Checks = 0;
        SessionState.SetInt(Key + ".Stage", 1);
        Resume();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetInt(Key + ".Stage", 0) == 0) return;
        deadline = EditorApplication.timeSinceStartup + 150d;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static object Get(object owner, string field) =>
        owner.GetType().GetField(field, Hidden).GetValue(owner);
    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static void InvokeTick(object owner) =>
        owner.GetType().GetMethod("Tick", Hidden).Invoke(owner, null);
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Checks++;
        Debug.Log("EDITOR PLAY REFRESH PASS: " + label);
    }

    private static void OpenWindows()
    {
        planning = EditorWindow.GetWindow<PlanningEditorWindow>();
        placement = EditorWindow.GetWindow<LevelEditorWindow>();
        // Batch mode does not always request the first UI Toolkit layout.
        if (Get(planning, "embeddedLevelEditor") == null) planning.CreateGUI();
        if (Get(placement, "panel") == null) placement.CreateGUI();
        Check(string.IsNullOrEmpty((string)Get(planning, "currentSavePath")),
            "audit workspace cannot reference any absolute shared/legacy save");
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup >= deadline)
        {
            Finish(new TimeoutException("Waiting for editor/play transition"));
            return;
        }
        if (EditorApplication.isCompiling) return;

        try
        {
            int stage = SessionState.GetInt(Key + ".Stage", 0);
            if (stage == 1)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                OpenWindows();
                VerifyEditRefresh("initial edit");
                LevelEditorState.EditMode = true;
                SurfaceTileEditorBridge.SetPainting(true);
                SessionState.SetInt(Key + ".Stage", 2);
                EditorApplication.EnterPlaymode();
            }
            else if (stage == 2)
            {
                if (!EditorApplication.isPlaying || Time.time < .3f) return;
                OpenWindows();
                VerifyPlayIsIdle();
                SessionState.SetInt(Key + ".Stage", 3);
                EditorApplication.ExitPlaymode();
            }
            else if (stage == 3)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                OpenWindows();
                VerifyEditRefresh("returned edit");
                Finish(null);
            }
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void VerifyEditRefresh(string phase)
    {
        Check(Get(planning, "embeddedLevelEditor") != null && Get(planning, "canvas") != null &&
            Get(placement, "panel") != null, phase + ": both real panels initialized");
        Check(planning.rootVisualElement.enabledSelf && placement.rootVisualElement.enabledSelf,
            phase + ": authoring controls enabled");

        Set(planning, "nextLevelEditorRefreshTime", 0d);
        Set(planning, "nextPreviewRefreshTime", 0d);
        Set(placement, "nextRefreshTime", 0d);
        InvokeTick(planning);
        InvokeTick(placement);
        Check((double)Get(planning, "nextLevelEditorRefreshTime") > 0d,
            phase + ": planning refresh resumes");
        Check((double)Get(planning, "nextPreviewRefreshTime") > 0d,
            phase + ": planning artwork preview resumes");
        Check((double)Get(placement, "nextRefreshTime") > 0d,
            phase + ": standalone placement refresh resumes");
    }

    private static void VerifyPlayIsIdle()
    {
        Check(Get(planning, "embeddedLevelEditor") != null && Get(placement, "panel") != null,
            "Play: initialized panels cannot mask missing guard");
        Check(!planning.rootVisualElement.enabledSelf && !placement.rootVisualElement.enabledSelf,
            "Play: authoring controls disabled even after domain reload");
        Check(!LevelEditorState.EditMode, "Play: authoring paint mode released");
        Check(!SurfaceTileEditorBridge.IsPainting, "Play: surface brush released");
        Set(planning, "nextLevelEditorRefreshTime", 0d);
        Set(planning, "nextPreviewRefreshTime", 0d);
        Set(placement, "nextRefreshTime", 0d);
        int roots = SceneManager.GetActiveScene().rootCount;
        string document = ((PlanningDocument)Get(planning, "document")).ToPersistentJson(true);

        // Force every interval to be due: the previous implementation advances
        // these deadlines and refreshes UI/previews even in hidden Play tabs.
        for (int i = 0; i < 512; i++)
        {
            InvokeTick(planning);
            InvokeTick(placement);
        }
        Check((double)Get(planning, "nextLevelEditorRefreshTime") == 0d,
            "Play: 512 planning ticks never enter authoring refresh");
        Check((double)Get(planning, "nextPreviewRefreshTime") == 0d,
            "Play: artwork preview refresh remains suspended");
        Check((double)Get(placement, "nextRefreshTime") == 0d,
            "Play: 512 standalone ticks never enter authoring refresh");
        Check(SceneManager.GetActiveScene().rootCount == roots &&
            ((PlanningDocument)Get(planning, "document")).ToPersistentJson(true) == document,
            "Play: repeated ticks leave scene roots and planning content untouched");
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseInt(Key + ".Stage");
        int checks = Checks;
        // Clear the transient document before Close so the audit itself cannot
        // invoke the window's normal save-on-close path.
        if (planning != null)
        {
            Set(planning, "canvas", null);
            Set(planning, "document", null);
            planning.Close();
        }
        if (placement != null) placement.Close();
        string scope = SessionState.GetString(Key + ".Scope", string.Empty);
        if (!string.IsNullOrEmpty(scope))
        {
            foreach (string suffix in new[] { ".Document", ".SavePath", ".AutoSync",
                ".Document.BeforeUnifiedGrid", ".View.Mode", ".View.Room", ".View.World.UnifiedGrid.X",
                ".View.World.UnifiedGrid.Y", ".View.World.UnifiedGrid.Zoom", ".View.Detail.X", ".View.Detail.Y",
                ".View.Detail.Zoom", ".View.Assembly.X", ".View.Assembly.Y", ".View.Assembly.Zoom" })
                EditorPrefs.DeleteKey(scope + suffix);
        }
        SessionState.SetString("2026TapTap.Planning.Session",
            SessionState.GetString(Key + ".PreviousSession", string.Empty));
        string migration = "2026TapTap.Planning.LegacyAssigned." + Hash128.Compute(Application.dataPath);
        if (SessionState.GetBool(Key + ".HadMigration", false))
            EditorPrefs.SetString(migration, SessionState.GetString(Key + ".PreviousMigration", string.Empty));
        else
            EditorPrefs.DeleteKey(migration);
        SessionState.EraseBool(Key + ".HadMigration");
        SessionState.EraseString(Key + ".PreviousMigration");
        SessionState.EraseString(Key + ".Scope");
        SessionState.EraseString(Key + ".PreviousSession");
        SessionState.EraseInt(Key + ".Checks");
        string report = error == null ? $"PASS {checks} editor/Play/resume checks" : "FAIL " + error;
        File.WriteAllText("Library/EditorPlayModeRefreshAudit.txt", report);
        if (error == null) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
