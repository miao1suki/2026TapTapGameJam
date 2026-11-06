#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace Project.Migration
{
    /// <summary>Real Gaming unlock work and delayed presentation; isolated identity, no save writes.</summary>
    public static class Native2DColorPerformanceAudit
    {
        private const string Key = "Native2DColorPerformanceAudit";
        private static int frames, checks;
        private static readonly MethodInfo WaterTick = typeof(GameplayWaterPresentation).GetMethod(
            "LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            checks++; Debug.Log("NATIVE_2D_COLOR_PASS " + label);
        }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/Native2DValidation/"))
                throw new InvalidOperationException("Use the isolated Native2DValidation project.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            frames = checks = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 15) return;
            try
            {
                var service = ColorRuntimeService.Instance;
                service.ResetProgress();
                var sources = Object.FindObjectsByType<WaterVisualFeature>(FindObjectsSortMode.None);
                Check(sources.Length > 100, "real formal water sources loaded");
                var camera = Camera.main;
                Check(camera != null, "real gameplay camera exists");
                Vector3 near = sources[0].transform.position;
                camera.orthographic = true; camera.orthographicSize = 6;
                camera.transform.SetPositionAndRotation(new Vector3(near.x, near.y, -100), Quaternion.identity);
                var unlock = Stopwatch.StartNew(); service.Unlock("blue"); unlock.Stop();
                var all = Object.FindObjectsByType<ColorObject>(FindObjectsSortMode.None);
                Check(all.Where(c => c.BaseColorTypeId == "blue").All(c => c.IsActive),
                    "unseen blue objects receive gameplay unlock synchronously");
                var waters = Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsSortMode.None);
                var build = Stopwatch.StartNew(); foreach (var water in waters) WaterTick.Invoke(water, null); build.Stop();
                int built = waters.Count(w => w.MeshBuildCount > 0);
                Check(built > 0 && built < waters.Length / 4, "only nearby water builds meshes on unlock");
                var visible = waters.First(w => w.MeshBuildCount > 0);
                var distant = waters.Where(w => w.MeshBuildCount == 0).OrderByDescending(
                    w => (w.Volume.center - near).sqrMagnitude).First();
                int count = visible.MeshBuildCount;
                Vector3 original = distant.ownerPosition();
                distant.transform.parent.position += Vector3.right * .01f;
                Physics2D.SyncTransforms(); WaterTick.Invoke(distant, null); WaterTick.Invoke(visible, null);
                Check(visible.MeshBuildCount == count, "distant topology change leaves visible mesh intact");
                distant.transform.parent.position = original; Physics2D.SyncTransforms();
                WaterTick.Invoke(distant, null);
                Vector3 destination = distant.Volume.center;
                ColorAppearanceManager.Instance.Play("blue", true, 0f);
                camera.transform.position = new Vector3(destination.x, destination.y, -100);
                WaterTick.Invoke(distant, null);
                Check(distant.MeshBuildCount == 1, "entering view constructs deferred water immediately");
                WaterTick.Invoke(distant, null);
                Check(distant.MeshBuildCount == 1, "unchanged water does not rebuild on the next update");
                distant.enabled = false;
                distant.enabled = true;
                WaterTick.Invoke(distant, null);
                Check(distant.MeshBuildCount == 2,
                    "room re-enable reconstructs meshes removed during disable");
                var merged = distant.transform.parent.GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
                Collider2D volume = merged != null ? merged.Volumes.First(v => v != null && v.enabled) :
                    distant.transform.parent.GetComponent<Collider2D>();
                Vector3 ownerBeforeMove = distant.ownerPosition();
                distant.transform.parent.position += Vector3.up * 10000f;
                Physics2D.SyncTransforms();
                Bounds movedVolume = volume.bounds;
                Check(GameplayWaterPresentation.ContainsWater(distant.gameObject.scene, movedVolume.center, out float top) &&
                    top >= movedVolume.max.y - .001f,
                    "steam query sees moved water before presentation LateUpdate");
                distant.transform.parent.position = ownerBeforeMove; Physics2D.SyncTransforms();
                Check(service.IsUnlocked("blue") && !service.IsAbilityUnlocked("blue"),
                    "presentation deferral does not grant player ability or relock the scene");
                string report = $"waters={waters.Length}, initiallyBuilt={built}, unlockCpuMs={unlock.Elapsed.TotalMilliseconds:F3}, " +
                    $"waterBuildCpuMs={build.Elapsed.TotalMilliseconds:F3}, checks={checks}";
                Directory.CreateDirectory("Library/Native2DRegressions");
                File.WriteAllText("Library/Native2DRegressions/ColorPerformance.txt", report);
                Debug.Log("NATIVE_2D_COLOR_AUDIT_PASS " + report);
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception e) { SessionState.SetBool(Key, false); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static Vector3 ownerPosition(this GameplayWaterPresentation water) => water.transform.parent.position;
    }
}
#endif
