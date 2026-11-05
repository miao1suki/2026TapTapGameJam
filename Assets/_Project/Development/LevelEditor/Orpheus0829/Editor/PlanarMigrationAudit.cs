#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;

namespace Project.Migration
{
    /// <summary>Explicit inspection only; never saves a scene or touches player progress.</summary>
    public static class PlanarMigrationAudit
    {
        public static void BuildWindowsSmoke()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/Native2DValidation/"))
                throw new InvalidOperationException("Use the isolated Native2DValidation project.");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Playing/Gaming.unity" },
                locationPathName = "Builds/Native2DSmoke/Native2D.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            });
            if (result == null) throw new InvalidOperationException("Unity did not return a build report; inspect the build log.");
            File.WriteAllText("Library/PlanarMigration/WindowsBuild.txt", result.summary.result +
                ", errors=" + result.summary.totalErrors + ", warnings=" + result.summary.totalWarnings);
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/Native2DValidation/"))
                throw new InvalidOperationException("Use the isolated Native2DValidation project.");
            string output = Path.GetFullPath("Library/PlanarMigration");
            Directory.CreateDirectory(output);
            var lines = new List<string>();
            // The URP package still depends on the built-in spatial physics module.
            // It must not run a second simulation behind the native 2D gameplay.
            var physics = Type.GetType("UnityEngine.Physics, UnityEngine.PhysicsModule");
            string simulation = physics?.GetProperty("simulationMode")?.GetValue(null)?.ToString();
            if (simulation != "Script") throw new Exception("Spatial physics is still automatic: " + simulation);
            lines.Add("Spatial physics simulation: " + simulation);
            int flatMeshes = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets" }))
            {
                string meshPath = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (!mesh) continue;
                if (mesh.bounds.size.z > .001f) throw new Exception("Spatial mesh remains: " + meshPath);
                flatMeshes++;
            }
            lines.Add("Planar mesh assets: " + flatMeshes);
            var preview = EditorSceneManager.OpenScene("Assets/ta_source/star/StarPanoramaPreview.unity");
            bool wasDirty = preview.isDirty;
            var previewTarget = new RenderTexture(64, 64, 16);
            try
            {
                var previewCamera = Camera.main;
                if (!previewCamera) throw new Exception("Star preview camera missing.");
                previewCamera.targetTexture = previewTarget;
                previewCamera.Render();
                previewCamera.targetTexture = null;
                if (preview.isDirty != wasDirty) throw new Exception("2D star preview dirtied the author scene.");
                lines.Add("2D star render leaves author scene clean.");
            }
            finally { Object.DestroyImmediate(previewTarget); }
            foreach (string path in new[] {
                "Assets/_Project/Scenes/Playing/Gaming.unity", "Assets/_Project/Scenes/Boss.unity",
                "Assets/Scenes/R1/R1.unity", "Assets/Scenes/R2/R2.unity", "Assets/Scenes/R3/R3.unity",
                "Assets/Scenes/MRoom2.unity", "Assets/Scenes/MRoom3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int bodies = 0, shapes = 0, objects = 0, lights = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) != 0)
                            throw new Exception("Missing script: " + path + " / " + tr.name);
                        foreach (var component in tr.GetComponents<Component>())
                        {
                            if (!component) continue;
                            string type = component.GetType().FullName;
                            if (type == "UnityEngine.Rigidbody" || type == "UnityEngine.Light" ||
                                type == "UnityEngine.BoxCollider" || type == "UnityEngine.MeshCollider" ||
                                type == "UnityEngine.SphereCollider" || type == "UnityEngine.CapsuleCollider" ||
                                type == "UnityEngine.CharacterController")
                                throw new Exception("Unexpected spatial component: " + path + " / " + tr.name + " / " + type);
                            if (component is Collider2D) shapes++;
                            if (component is Rigidbody2D) bodies++;
                            if (component is Light2D) lights++;
                            if (component is Camera camera && !camera.orthographic)
                                throw new Exception("Perspective camera: " + path + " / " + tr.name);
                        }
                    }
                lines.Add(path + ": objects=" + objects + ", Collider2D=" + shapes + ", Rigidbody2D=" + bodies + ", Light2D=" + lights);
            }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            var main = Camera.main;
            if (!main) throw new Exception("Formal scene has no main camera.");
            Transform player = null;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour && behaviour.GetType().FullName == "Project.Player.PlayerController") { player = behaviour.transform; break; }
            if (!player) throw new Exception("Formal player missing.");
            main.transform.SetPositionAndRotation(new Vector3(player.position.x, player.position.y + 2, -30), Quaternion.identity);
            main.orthographicSize = 11;
            main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = Color.black;
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                main.targetTexture = target;
                main.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, "GamingNative2D.png"), image.EncodeToPNG());
                int magenta = 0, visible = 0;
                foreach (Color32 pixel in image.GetPixels32())
                {
                    if (pixel.r > 200 && pixel.b > 200 && pixel.g < 30) magenta++;
                    if (pixel.r + pixel.g + pixel.b > 60) visible++;
                }
                if (magenta > 1600 * 900 / 100) throw new Exception("Missing-shader magenta pixels: " + magenta);
                if (visible < 10000) throw new Exception("Unexpected blank formal render: " + visible);
                lines.Add("GPU render: visible=" + visible + ", magenta=" + magenta);
            }
            finally
            {
                main.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
            File.WriteAllLines(Path.Combine(output, "FormalSceneAudit.txt"), lines);
            Debug.Log("NATIVE_2D_FORMAL_AUDIT_PASS\n" + string.Join("\n", lines));
            EditorApplication.Exit(0);
        }
    }
}
#endif
