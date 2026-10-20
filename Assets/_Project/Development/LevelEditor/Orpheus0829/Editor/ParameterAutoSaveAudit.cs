using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    public static class ParameterAutoSaveAudit
    {
        static int count;
        static Type T => typeof(LevelEditorPropComponentEditorWindow);
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Check(bool value, string message)
        { if (!value) throw new Exception(message); count++; }
        static LevelEditorPropComponentEditorWindow Window =>
            EditorWindow.GetWindow<LevelEditorPropComponentEditorWindow>();
        static void Set(object window, string key, object value) => T.GetField(key, Flags).SetValue(window, value);
        static object Call(object window, string key, params object[] args) => T.GetMethod(key, Flags).Invoke(window, args);
        static List<LevelEditorComponentValueOverride> Draft(object window) =>
            (List<LevelEditorComponentValueOverride>)T.GetField("draftOverrides", Flags).GetValue(window);
        static LevelEditorComponentValueOverride Value()
        {
            var v = new LevelEditorComponentValueOverride();
            v.Configure("", "UnityEngine.Rigidbody", 0, "m_Mass", "Float", "2", null);
            return v;
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated probe only");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                bool dirty = scene.isDirty;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/Player/Player.prefab");
                Check(prefab != null, "Test source exists");
                int saves = 0;
                List<LevelEditorComponentValueOverride> saved = null;
                Action<List<LevelEditorComponentValueOverride>> save = v => { saves++; saved = v; };
                LevelEditorPropComponentEditorWindow.Open(prefab, null, save);
                Check(Draft(Window).Count == 0, "Empty draft");
                Window.Close();
                Check(saves == 0, "Unchanged close cannot save");
                Check(scene.isDirty == dirty, "Unchanged close cannot dirty scene");

                LevelEditorPropComponentEditorWindow.Open(prefab, null, save);
                Draft(Window).Add(Value());
                var original = Draft(Window)[0];
                Window.Close();
                Check(saves == 1, "Changed close saves exactly once");
                Check(saved.Count == 1, "Saved value retained");
                Check(!ReferenceEquals(saved[0], original), "Saved item is clone");
                LevelEditorPropComponentEditorWindow.Open(prefab, saved, save);
                Check(Draft(Window).Count == 1, "Reopen saved override");
                Check(saved.Count == 1, "Reopen cannot clear owner list");
                Draft(Window).Add(Value());
                Call(Window, "DiscardAndClose");
                Check(saves == 1, "Discard never saves");

                LevelEditorPropComponentEditorWindow.Open(prefab, null, save);
                Draft(Window).Add(Value());
                Check(LevelEditorPropComponentEditorWindow.SaveOpenSession(), "Switch flush succeeds");
                Check(saves == 2, "Switch flush once");
                Check(LevelEditorPropComponentEditorWindow.SaveOpenSession(), "Repeat flush allowed");
                Check(saves == 2, "Repeat flush no duplicate");
                Window.Close();
                Check(saves == 2, "Close after flush no duplicate");

                int widths = 0;
                LevelEditorPropComponentEditorWindow.Open(prefab, null, save,
                    platformWidth: 3, applyPlatformWidth: width => { widths++; return width < 1 ? "宽度无效" : null; });
                Set(Window, "draftPlatformWidth", 0);
                Check(!(bool)Call(Window, "TrySaveDraft", true), "Invalid width rejected");
                Check(saves == 2, "Invalid width cannot save overrides");
                Check(widths == 1, "Width validation called");
                Set(Window, "draftPlatformWidth", 1);
                Check((bool)Call(Window, "TrySaveDraft", true), "Valid width accepted");
                Check(saves == 3 && widths == 2, "Width and override commit once");
                Window.Close();
                Check(saves == 3 && widths == 2, "Close after width save no duplicate");
                Check(scene.isDirty == dirty, "Fixture has no unintended scene mutations");
                string canvas = File.ReadAllText("Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor/PlanningCanvas.cs");
                Check(canvas.Contains("part.platformMountId = item.box.platformMountId"), "Merged platform mount propagation retained");
                Check(canvas.Contains("importedMount.Platform"), "Merged platform mount import retained");
                Check(canvas.Contains("rightClickParameters = false"), "Right-click state cleanup exists");
                File.WriteAllText("Library/ParameterAutoSaveAudit.txt", "PASS " + count + " assertions; compile and real EditorWindow close/save/discard/switch/width validation; no Scene saved.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                File.WriteAllText("Library/ParameterAutoSaveAudit.txt", "FAIL " + e);
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
