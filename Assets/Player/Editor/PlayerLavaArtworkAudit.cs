using System;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.FlowMagma;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Real camera ordering and presentation-only native-pixel offset checks.</summary>
    public static class PlayerLavaArtworkAudit
    {
        private const string Flag = "PlayerLavaArtworkAudit";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int phase, frames, checks;
        private static double deadline;
        private static Camera camera;
        private static RenderTexture target;
        private static FlowMagmaVisual magma;
        private static Color32[] reference;
        private static bool perspective;
        private static string report;

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use silent isolated probe only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = frames = checks = 0;
            deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Phase " + phase);
                if (++frames < 8) return;
                frames = 0;
                if (phase == 0)
                {
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    var lava = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab"));
                    lava.transform.position = Vector3.zero;
                    var cube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab"));
                    cube.transform.position = Vector3.zero;
                    cube.GetComponent<ContactCubeMotion>().SuspendMotion(true);
                    camera = new GameObject("Artwork real URP camera").AddComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.04f, .08f, .12f, 1);
                    camera.allowHDR = camera.allowMSAA = false;
                    target = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
                    target.Create(); camera.targetTexture = target;
                    SetCamera(false);
                    VerifyPlayerPixels();
                    phase++;
                }
                else if (phase == 1)
                {
                    ColorAppearanceManager.Instance.Play("red", true, 0);
                    magma = Object.FindFirstObjectByType<FlowMagmaVisual>();
                    Check(magma != null && magma.GetComponent<MeshRenderer>().sortingOrder < 0,
                        "Magma body sorts below block sprites");
                    Check(magma.transform.position.z > -.5f, "Magma plane is behind the opaque front face");
                    SetMagmaVisible(false); phase++;
                }
                else if (phase == 2)
                {
                    reference = Capture("cube-only-" + (perspective ? "3d" : "2d"));
                    SetMagmaVisible(true); phase++;
                }
                else if (phase == 3)
                {
                    var actual = Capture("cube-over-magma-" + (perspective ? "3d" : "2d"));
                    int matched = 0, different = 0;
                    // Interior pixels are fully covered by the real block artwork.
                    // Outside it, magma and sparks are deliberately allowed to remain visible.
                    for (int y = 138; y < 182; y++)
                        for (int x = 138; x < 182; x++)
                        {
                            var a = reference[y * 320 + x]; var b = actual[y * 320 + x];
                            if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 5) different++;
                            else matched++;
                        }
                    Check(matched > 1900 && different < 10,
                        $"Real {(perspective ? "perspective" : "orthographic")} camera: magma cannot paint over cube ({different} changed pixels)");
                    if (!perspective)
                    {
                        perspective = true; SetCamera(true); SetMagmaVisible(false); phase = 2;
                    }
                    else Finish(null);
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void VerifyPlayerPixels()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/Player/Player.prefab"), new Vector3(20, 0, 0), Quaternion.identity);
            var player = root.GetComponent<PlayerController>(); player.enabled = false;
            root.GetComponent<PlayerInputDriver>().enabled = false;
            root.GetComponent<Rigidbody>().isKinematic = true;
            var presentation = root.GetComponent<PlayerSpritePresentation>();
            var offset = typeof(PlayerSpritePresentation).GetField("additionalVerticalOffsetPixels", Hidden);
            var size = typeof(PlayerSpritePresentation).GetField("cellWorldSize", Hidden);
            var refresh = typeof(PlayerSpritePresentation).GetMethod("Refresh", Hidden);
            var capsule = root.GetComponent<CapsuleCollider>();
            var colliderCenter = capsule.center; float radius = capsule.radius, height = capsule.height;
            foreach (float cell in new[] { .5f, 1f, 2f })
            {
                size.SetValue(presentation, cell); offset.SetValue(presentation, 0); refresh.Invoke(presentation, new object[] { 0f });
                Vector3 before = presentation.Visual.transform.position, scale = presentation.Visual.transform.localScale;
                offset.SetValue(presentation, -2); refresh.Invoke(presentation, new object[] { 0f });
                Check(Mathf.Abs(presentation.Visual.transform.position.y - before.y + cell * 2f / 16f) < .00001f,
                    "Exactly two effective art pixels down; grid size=" + cell);
                Check(presentation.Visual.transform.localScale == scale && capsule.center == colliderCenter &&
                    capsule.radius == radius && capsule.height == height, "Offset never resizes art or moves physics");
            }
        }

        private static void SetCamera(bool usePerspective)
        {
            camera.orthographic = !usePerspective; camera.orthographicSize = 1.5f;
            camera.fieldOfView = 17.06153f;
            camera.transform.position = usePerspective ? new Vector3(1.5f, 0, -10) : new Vector3(0, 0, -10);
            camera.transform.LookAt(Vector3.zero);
        }
        private static void SetMagmaVisible(bool visible)
        {
            foreach (var renderer in magma.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = visible;
        }
        private static Color32[] Capture(string name)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(320, 320, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); image.Apply();
                Directory.CreateDirectory("Library/PlayerLavaArtwork");
                File.WriteAllBytes("Library/PlayerLavaArtwork/" + name + ".png", image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Check(bool value, string message)
        {
            checks++; report += message + "\n";
            if (!value) throw new InvalidOperationException(message);
        }
        private static void Finish(Exception exception)
        {
            report += exception == null ? $"PASS {checks} artwork checks\n" : "FAIL " + exception;
            File.WriteAllText("Library/PlayerLavaArtworkResult.txt", report);
            if (exception != null) Debug.LogException(exception); else Debug.Log(report);
            SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
            EditorApplication.Exit(exception == null ? 0 : 1);
        }
    }
}
