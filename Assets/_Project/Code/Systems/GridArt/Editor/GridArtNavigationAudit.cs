using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Project.GridArt.Editor
{
    /// <summary>Attached UI navigation/shortcut audit; creates only disposable isolated fixtures.</summary>
    public static class GridArtNavigationAudit
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> Report = new();
        private static Func<Scene, GridArtLevelSnapshot> previousSource;
        private static GridArtWindow window;
        private static VisualElement canvas;
        private static GridArtLevelSnapshot snapshotA, snapshotB;
        private static string folder, sceneA, sceneB;
        private static int phase, checks;
        private static double started;
        private static TextField text;
        private static readonly Vector2 PanA = new(345f, -678f), PanB = new(-876f, 234f);

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("GridArtNavigationAudit only runs in stopped FlowRetirementProbe.");
            checks = phase = 0; Report.Clear(); started = EditorApplication.timeSinceStartup;
            previousSource = GridArtLevelSource.Read;
            folder = "Assets/__GridArtNavigationAudit_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            sceneA = folder + "/A.unity"; sceneB = folder + "/B.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), sceneA);
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), sceneB);
            snapshotA = Snapshot(new Rect(196, -92, 20, 12), new Rect(240, -80, 16, 18));
            snapshotB = Snapshot(new Rect(-220, 160, 30, 24), new Rect(-170, 190, 12, 10));
            GridArtLevelSource.Read = scene => scene.path == sceneA ? snapshotA : snapshotB;
            EditorSceneManager.OpenScene(sceneA, OpenSceneMode.Single);
            window = ScriptableObject.CreateInstance<GridArtWindow>(); window.position = new Rect(20,20,1280,800); window.Show();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static GridArtLevelSnapshot Snapshot(Rect first, Rect second)
        {
            var result = new GridArtLevelSnapshot { cellSize = 2f, currentRoomId = "room-a", label = "无美术图定位审计" };
            result.rooms.Add(new GridArtLevelRoom { id = "room-a", label = "当前房间", boundsWorld = first });
            result.rooms.Add(new GridArtLevelRoom { id = "room-b", label = "其他房间", boundsWorld = second });
            result.RecalculateBounds(); return result;
        }

        private static bool LaidOut()
        {
            if(window == null) return false;
            canvas = Read<VisualElement>(window,"canvas");
            return canvas != null && canvas.panel != null && canvas.contentRect.width > 300 && canvas.contentRect.height > 250;
        }

        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started > 90d) throw new TimeoutException("Navigation phase " + phase);
                if(!LaidOut()) return;
                if(phase == 0)
                {
                    Call(window,"RefreshLevelSource"); phase = 1; return;
                }
                if(phase == 1)
                {
                    Check(window.Map == null && snapshotA.groups.Count == 0, "fixture has room layout but no white-wall groups or art map");
                    Check(CenterIs(snapshotA.rooms[0].boundsWorld.center / 2f), "first source/layout frames the actual current room far from world origin");
                    var reference=canvas.Q<GridArtLevelReference>("art-level-reference");
                    Check(reference != null && reference.QuadCount >= 8 && canvas.IndexOf(reference)<canvas.IndexOf(canvas.Q("artwork-cache")),
                        "empty-art rooms remain visible as independent retained reference below art");
                    Check(window.rootVisualElement.Q<Toggle>("art-reference-visible").value &&
                        Mathf.Approximately(window.rootVisualElement.Q<Slider>("art-reference-opacity").value,.35f),
                        "reference defaults visible with 35% opacity without an art map");
                    Check(window.rootVisualElement.Q<PopupField<string>>("art-reference-room") != null &&
                        window.rootVisualElement.Q<Foldout>("art-effects-panel") != null,
                        "room locator and TA regions are reachable before creating an art map");
                    Check((bool)Call(canvas,"FrameRoom","room-b") && CenterIs(snapshotA.rooms[1].boundsWorld.center/2f),
                        "room locator frames selected room using world/art-cell conversion");
                    Check((bool)Call(canvas,"FrameLevel") && CenterIs(snapshotA.worldBounds.center/2f), "whole-level locator fits complete bounds");
                    Vector2 drawingPan = Read<Vector2>(canvas,"pan"); float drawingZoom = Read<float>(canvas,"zoom");
                    Call(window,"SetRoomOverview",true);
                    Check(Read<bool>(window,"roomOverview") && canvas.Q("artwork-cache").style.display.value == DisplayStyle.None,
                        "assembly overview hides covering artwork and is available without an art map");
                    Check(Read<System.Collections.IDictionary>(canvas,"roomLabels").Count == 2,
                        "overview retains one named label per room, not per grid cell");
                    Vector2 centerB = snapshotA.rooms[1].boundsWorld.center / 2f;
                    Vector2 click = new Vector2(Read<Vector2>(canvas,"pan").x + centerB.x * Read<float>(canvas,"zoom"),
                        Read<Vector2>(canvas,"pan").y - centerB.y * Read<float>(canvas,"zoom"));
                    Call(canvas,"SelectOverviewRoom",click,false);
                    Check(Read<string>(window,"selectedReferenceRoom") == "room-b", "overview click selects the correct world-XY room");
                    Call(window,"SetRoomOverview",false);
                    Check(Read<Vector2>(canvas,"pan") == drawingPan && Read<float>(canvas,"zoom") == drawingZoom,
                        "returning from overview restores exact drawing view");
                    Call(window,"SetRoomOverview",true);
                    Call(window,"OpenReferenceRoom");
                    Check(!Read<bool>(window,"roomOverview") && CenterIs(centerB), "enter-room action returns to drawing and frames chosen room");
                    int geometry = reference.GeometryRevision;
                    for(int i=0;i<100;i++) { Set(canvas,"pan",new Vector2(20+i,70-i)); Set(canvas,"zoom",1f+i*.05f); Call(canvas,"RepaintView"); }
                    Check(reference.GeometryRevision == geometry, "100 navigation changes retain reference geometry instead of regenerating it");
                    Toggle(window.rootVisualElement.Q<Toggle>("art-reference-visible"),false);
                    Slider opacity=window.rootVisualElement.Q<Slider>("art-reference-opacity"); opacity.value=.4f;
                    Check(reference.style.display.value == DisplayStyle.None && Mathf.Approximately(reference.style.opacity.value,.4f),
                        "reference show/opacity controls change only underlay presentation");
                    Toggle(window.rootVisualElement.Q<Toggle>("art-reference-visible"),true);
                    Set(canvas,"pan",PanA); Set(canvas,"zoom",11f); Call(canvas,"RememberView");
                    Call(window,"RefreshLevelSource");
                    Check(Read<Vector2>(canvas,"pan") == PanA && Mathf.Approximately(Read<float>(canvas,"zoom"),11f),
                        "source refresh does not reset a view the user already navigated");
                    text=new TextField("输入保护");window.rootVisualElement.Add(text);window.Focus();text.Focus();
                    phase=2;return;
                }
                if(phase == 2)
                {
                    Check(!GridArtWorkspaceShortcut.CanToggle(window), "N ignores focused UI Toolkit text/rename input");
                    canvas.Focus();
                    EditorGUIUtility.editingTextField=true;
                    try { Check(!GridArtWorkspaceShortcut.CanToggle(window), "N ignores Unity/IMGUI text editing"); }
                    finally { EditorGUIUtility.editingTextField=false; }
                    Set(canvas,"effectGesture",true);
                    Check(!GridArtWorkspaceShortcut.CanToggle(window), "N cannot discard an unfinished region stroke");
                    Set(canvas,"effectGesture",false);
                    text.RemoveFromHierarchy(); text=null;
                    EditorSceneManager.OpenScene(sceneB,OpenSceneMode.Single); Call(window,"RefreshLevelSource");
                    phase=3;return;
                }
                if(phase == 3)
                {
                    Report.Add("SCENE B state: " + ViewState());
                    Check(CenterIs(snapshotB.rooms[0].boundsWorld.center/2f), "a different scene with no art map frames its own room, not previous scene's origin/view");
                    Set(canvas,"pan",PanB);Set(canvas,"zoom",7f);Call(canvas,"RememberView");
                    EditorSceneManager.OpenScene(sceneA,OpenSceneMode.Single);Call(window,"RefreshLevelSource");
                    phase=4;return;
                }
                if(phase == 4)
                {
                    Check(Read<Vector2>(canvas,"pan")==PanA && Mathf.Approximately(Read<float>(canvas,"zoom"),11f),
                        "returning to the first scene restores its exact independent pan/zoom");
                    Check(window.rootVisualElement.Q<Toggle>("art-reference-visible").value &&
                        Mathf.Approximately(window.rootVisualElement.Q<Slider>("art-reference-opacity").value,.4f),
                        "reference preferences are restored per scene");
                    window.Focus();canvas.Focus();
                    Check(GridArtWorkspaceShortcut.CanToggle(window), "N remains available with canvas focus");
                    typeof(GridArtWorkspaceShortcut).GetMethod("Toggle",BindingFlags.Static|BindingFlags.NonPublic)
                        .Invoke(null,null);
                    Check(window == null, "N shortcut closes existing workbench without creating a second instance");
                    var browser = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser"));
                    browser.Focus();
                    Check(GridArtWorkspaceShortcut.CanToggle(browser), "N can reopen from Project without clicking Scene first");
                    Check(EditorApplication.ExecuteMenuItem("Tools/2026TapTap/关卡美术/切换窗口"), "global menu hotkey entry executes like planning M");
                    window=Resources.FindObjectsOfTypeAll<GridArtWindow>().Single();phase=5;return;
                }
                if(phase == 5)
                {
                    Call(window,"RefreshLevelSource");
                    Check(Read<Vector2>(canvas,"pan")==PanA && Mathf.Approximately(Read<float>(canvas,"zoom"),11f),
                        "N reopen preserves unfinished-view position and does not recenter on refresh");
                    Check(!SceneManager.GetActiveScene().isDirty, "reference navigation and shortcuts never dirty author scene data");
                    Finish(null);
                }
            }
            catch(Exception error) { Finish(error); }
        }

        private static bool CenterIs(Vector2 cells)
        {
            Vector2 pan=Read<Vector2>(canvas,"pan");float zoom=Read<float>(canvas,"zoom");Vector2 center=canvas.contentRect.center;
            return Vector2.Distance(new Vector2((center.x-pan.x)/zoom,(pan.y-center.y)/zoom),cells)<.01f;
        }
        private static string ViewState() => "scene=" + SceneManager.GetActiveScene().path +
            "; bound=" + Read<string>(window,"boundSceneKey") + "; view=" + Read<string>(canvas,"restoredViewKey") +
            "; initial=" + Read<bool>(canvas,"needsInitialFrame") + "; pan=" + Read<Vector2>(canvas,"pan") +
            "; zoom=" + Read<float>(canvas,"zoom") + "; viewport=" + canvas.contentRect +
            "; room=" + Read<string>(window,"selectedReferenceRoom");
        private static void Toggle(Toggle toggle,bool value) => toggle.value=value;
        private static object Call(object target,string method,params object[] args) =>
            target.GetType().GetMethod(method,Hidden|BindingFlags.Public).Invoke(target,args);
        private static T Read<T>(object target,string field) => (T)target.GetType().GetField(field,Hidden).GetValue(target);
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,Hidden).SetValue(target,value);
        private static void Check(bool condition,string message)
        { checks++; if(!condition) throw new InvalidOperationException("Check "+checks+": "+message);Report.Add("PASS "+checks+": "+message); }
        private static void Finish(Exception error)
        {
            EditorApplication.update-=Tick;
            if(window!=null) window.Close();
            GridArtLevelSource.Read=previousSource;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            string result=(error==null?"PASS ":"FAIL ")+checks+"\n"+string.Join("\n",Report)+"\n"+error;
            File.WriteAllText("Library/GridArtNavigationAudit.txt",result);
            if(error==null) Debug.Log(result);else Debug.LogError(result);
            EditorApplication.Exit(error==null?0:1);
        }
    }
}
