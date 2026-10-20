using System;
using System.IO;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project.WorldVisibility.Editor
{
    public static class VisibilityPlayAudit
    {
        const string Flag = "Visibility.PlayAudit";
        static int stage, checks;
        static double deadline;
        static int firstFrame;
        static FountainArtwork fountain;
        static int initialFountainFrame;
        static WorldVisibility2D system;
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Flag,false)) return;
            EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup+70;
        }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').EndsWith("/Library/FlowRetirementProbe/Assets",StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only use the silent disposable project.");
            EditorSceneManager.OpenScene("Assets/Scenes/Pick.unity");
            var camera = Camera.main;
            system = camera.gameObject.AddComponent<WorldVisibility2D>(); system.targetCamera = camera; system.findPlayer = true;
            var player = GameObject.FindGameObjectWithTag("Player"); system.viewer = player.transform;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisibilitySetup.FountainPrefab));
            root.transform.position = player.transform.position+new Vector3(3,-1,0);
            var lamp = new GameObject("Play测试暖光").AddComponent<VisibilityLight2D>(); lamp.transform.position = root.transform.position+new Vector3(0,2,0);
            var canvas = new GameObject("测试屏幕覆盖UI",typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var ui = new GameObject("测试不受光照影响的色块",typeof(RectTransform),typeof(Image)); ui.transform.SetParent(canvas.transform,false);
            ui.GetComponent<Image>().color = Color.magenta;
            var rect = ui.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.sizeDelta = new Vector2(40,40); rect.anchoredPosition = new Vector2(8,-8);
            SessionState.SetBool(Flag,true); Resume(); EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string message) { checks++; if (!value) throw new Exception(message); }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play validation timed out");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (stage==0)
                {
                    if (Time.frameCount<10) return;
                    system = UnityEngine.Object.FindFirstObjectByType<WorldVisibility2D>(); fountain = UnityEngine.Object.FindFirstObjectByType<FountainArtwork>();
                    Check(system && fountain,"actual Play scene contains visibility and fountain");
                    Check(system.viewer && system.CachedSegments>0,"actual runtime registry supplies Pick walls");
                    Check(WorldVisibilityRendererFeature.LastExecutedFrame>=Time.frameCount-2,"actual Game camera executed visibility composition");
                    firstFrame = Time.frameCount; initialFountainFrame = fountain.CurrentFrame; stage = 1;
                }
                else if (stage==1 && Time.frameCount>firstFrame+8)
                {
                    if (fountain.CurrentFrame == initialFountainFrame && Time.frameCount<firstFrame+40) return;
                    Check(fountain.CurrentFrame != initialFountainFrame,"fountain advances real animation frames");
                    Check(Mathf.Abs(fountain.GetComponent<SpriteRenderer>().bounds.size.x-82f/16f)<.001f,"fountain stays at native width");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/Visibility_Pick_Play.png")); firstFrame = Time.frameCount; stage=2;
                }
                else if (stage==2 && Time.frameCount>firstFrame+4)
                {
                    string path="Library/Visibility_Pick_Play.png"; Check(File.Exists(path),"actual Game View screenshot saved");
                    var image = new Texture2D(2,2); image.LoadImage(File.ReadAllBytes(path));
                    var pixel = image.GetPixel(20,image.height-20);
                    Check(pixel.r>.9f && pixel.b>.9f && pixel.g<.1f,"overlay UI untouched by scene darkness"); UnityEngine.Object.Destroy(image);
                    File.WriteAllText("Library/VisibilityPlayAudit.txt",$"PASS {checks} actual Play assertions: runtime wall cache, camera render pass, fountain frame animation and native size, screen-overlay UI unmodified. No author scene saved; screenshot Visibility_Pick_Play.png.");
                    Finish(0);
                }
            }
            catch(Exception error) { Debug.LogException(error); File.WriteAllText("Library/VisibilityPlayAudit.txt","FAIL "+error); Finish(1); }
        }
        static void Finish(int exit)
        { SessionState.SetBool(Flag,false); EditorApplication.update-=Tick; EditorApplication.Exit(exit); }
    }
}
