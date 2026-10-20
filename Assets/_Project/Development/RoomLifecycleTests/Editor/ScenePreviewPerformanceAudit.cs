using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Project.BlockFeatures.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Disposable edit-mode selection/culling regression; never opens the user's Scene.</summary>
public static class ScenePreviewPerformanceAudit
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Run only in the disposable FlowRetirementProbe project.");
        var report = new StringBuilder(); int checks = 0, exit = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++; report.AppendLine("PASS " + message);
        }
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArtworkScenePreview.Clear();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/Blue_ContactCube.prefab");
            Check(prefab != null, "standard blue artwork exists");
            var camera = new GameObject("Audit camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -20); camera.orthographic = true;
            camera.orthographicSize = 40; camera.aspect = 1.6f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var owners = new GameObject[1024];
            for (int i = 0; i < owners.Length; i++)
            {
                owners[i] = Object.Instantiate(prefab);
                owners[i].transform.position = new Vector3(i % 32 - 16, i / 32 - 16, 0);
            }
            // Deliberately do not SyncTransforms: authoring preview must use the
            // current Transform, not force a full physics-scene synchronization.
            ArtworkScenePreview.RefreshOpenScenes();
            Check(ArtworkScenePreview.CachedOwnerCount >= owners.Length, "1024 independent artwork owners cached without merging");
            Check(ArtworkScenePreview.TryGetFirstMesh(owners[0], out var firstMesh, out _), "first owner has actual preview geometry");
            report.AppendLine($"First owner position={owners[0].transform.position}, local preview bounds={firstMesh.bounds}, collider={owners[0].GetComponentInChildren<Collider>().bounds}, active={owners[0].activeInHierarchy}, hidden={SceneVisibilityManager.instance.IsHidden(owners[0])}, stage={UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().Contains(owners[0])}");
            var method = typeof(ArtworkScenePreview).GetMethod("PickOwner", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Camera), typeof(Ray) }, null);
            var pick = (Func<Camera, Ray, GameObject>)Delegate.CreateDelegate(typeof(Func<Camera, Ray, GameObject>), method);
            var planeField = typeof(ArtworkScenePreview).GetField("planes", BindingFlags.NonPublic | BindingFlags.Static);
            GeometryUtility.CalculateFrustumPlanes(camera, (Plane[])planeField.GetValue(null));
            for (int i = 0; i < owners.Length; i += 127)
            {
                Ray ray = new Ray(camera.transform.position + new Vector3(owners[i].transform.position.x, owners[i].transform.position.y, 0), Vector3.forward);
                Check(pick(camera, ray) == owners[i], "ray picking keeps exact owner " + i);
            }
            var center = owners[528];
            Ray centerRay = new Ray(new Vector3(0, 0, -20), Vector3.forward);
            Check(pick(camera, centerRay) == center, "center ray selects displayed artwork");
            SceneVisibilityManager.instance.Hide(center, false);
            Check(pick(camera, centerRay) == null, "Scene hidden owner is not pickable");
            SceneVisibilityManager.instance.Show(center, false);
            Check(pick(camera, new Ray(new Vector3(50, 30, -20), Vector3.forward)) == null, "empty-space pick stays empty");
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) pick(camera, centerRay);
            watch.Stop();
            report.AppendLine($"1024 unmerged owners, 200 center-ray selection scans: {watch.Elapsed.TotalMilliseconds:F2} ms total / {watch.Elapsed.TotalMilliseconds / 200:F3} ms each (CPU only, not Scene total frame time).");
            Plane[] unit = { new(Vector3.right, 1), new(Vector3.left, 1), new(Vector3.up, 1), new(Vector3.down, 1), new(Vector3.forward, 1), new(Vector3.back, 1) };
            Vector3 a = new(-100000, 0, 0), b = new(100000, 0, 0);
            Check(ScenePreviewCulling.ClipLine(unit, ref a, ref b) && Mathf.Abs(a.x + 1) < .02f && Mathf.Abs(b.x - 1) < .02f,
                "long route crosses viewport with both endpoints offscreen and is clipped");
            a = new(-100, 2, 0); b = new(100, 2, 0);
            Check(!ScenePreviewCulling.ClipLine(unit, ref a, ref b), "offscreen route produces no dotted geometry");
            a = new(0, 0, -100); b = new(0, 0, 100);
            Check(ScenePreviewCulling.ClipLine(unit, ref a, ref b) && a.z >= -1.001f && b.z <= 1.001f, "near/far clipping is bounded too");
            report.AppendLine($"PASS {checks} assertions. No rendered-frame/FPS claim; actual Scene panning still needs interactive acceptance.");
        }
        catch (Exception error) { exit = 1; report.AppendLine(error.ToString()); UnityEngine.Debug.LogException(error); }
        finally
        {
            ArtworkScenePreview.Clear();
            File.WriteAllText("Library/ScenePreviewPerformanceAudit.txt", report.ToString());
            UnityEngine.Debug.Log(report.ToString()); EditorApplication.Exit(exit);
        }
    }
}
