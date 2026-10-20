using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.ColorBlocks;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.BlockFeatures.Editor
{
    /// <summary>Isolated real Gaming Play Mode wiring/reset check, without authored asset writes.</summary>
    public static class GameplayWaterFormalBindingAudit
    {
        private const string Flag = "GameplayWaterFormalBindingAudit";
        private static int stage, waitFrame, checks, baselineWaters;
        private static double deadline;
        private static readonly StringBuilder details = new StringBuilder();

        public static void RunFormal()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("Use isolated FlowRetirementProbe.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            EditorApplication.delayCall += Begin;
        }
        private static void Begin()
        {
            stage = checks = 0; waitFrame = 4; details.Clear();
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Formal water binding audit timed out at stage " + stage);
                if (!EditorApplication.isPlaying || Time.frameCount < waitFrame) return;
                if (stage == 0)
                {
                    var service = ColorRuntimeService.Instance;
                    service.ResetProgress(); service.Unlock("blue");
                    stage = 1; waitFrame = Time.frameCount + 12; return;
                }
                if (stage == 1)
                {
                    baselineWaters = VerifyBindings("After blue unlock");
                    var manager = RoomLifecycleManager.ForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    Check(manager != null, "formal scene room lifecycle manager");
                    var regions = new HashSet<string>();
                    foreach (var water in Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        var member = water.GetComponentInParent<RoomResetObject>();
                        if (member != null && !string.IsNullOrEmpty(member.RegionId)) regions.Add(member.RegionId);
                    }
                    Check(regions.Count > 0, "water has authored room reset ownership");
                    // This is the same synchronous room restore used by R/respawn,
                    // applied to each formal water region rather than only the start room.
                    foreach (string region in regions) manager.ResetRegion(region);
                    details.AppendLine("R/respawn room reset regions=" + regions.Count);
                    stage = 2; waitFrame = Time.frameCount + 12; return;
                }
                int after = VerifyBindings("After R/respawn room restore");
                Check(after == baselineWaters, "reset preserves all formal water presentations: " + after);
                Check(ColorRuntimeService.Instance.IsUnlocked("blue"), "room restore preserves blue unlock");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static int VerifyBindings(string phase)
        {
            var waters = Object.FindObjectsByType<GameplayWaterPresentation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Check(waters.Length > 10, phase + ": real formal Gaming waters=" + waters.Length);
            int rear = 0, front = 0, surface = 0, body = 0, interior = 0;
            var slices = new List<Vector4>();
            foreach (var water in waters)
                foreach (var renderer in water.GetComponentsInChildren<MeshRenderer>(false))
                {
                    bool isRear = renderer.name == "Water Surface" || renderer.name == "Water Body" || renderer.name == "Water Interior";
                    bool isFront = renderer.name == "Water Refraction";
                    if (!isRear && !isFront) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    if (isFront)
                    {
                        front++; Check(block.GetFloat("_GridArtSliceClamp") == 0, phase + ": refraction does not enable tile clamp");
                        continue;
                    }
                    rear++;
                    if (renderer.name == "Water Surface") surface++;
                    if (renderer.name == "Water Body") body++;
                    if (renderer.name == "Water Interior") interior++;
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Check(mesh != null && mesh.vertexCount > 0 && mesh.vertexCount % 4 == 0, phase + ": rear has generated quad mesh");
                    mesh.GetUVs(1, slices);
                    Check(slices.Count == mesh.vertexCount, phase + ": each rear vertex has UV1 slice bounds");
                    Check(block.GetFloat("_GridArtSliceClamp") == 1, phase + ": rear enables shared color/mask clamp");
                    var uv = mesh.uv;
                    for (int first = 0; first < uv.Length; first += 4)
                    {
                        Vector2 min = uv[first], max = min;
                        for (int j = 1; j < 4; j++) { min = Vector2.Min(min, uv[first+j]); max = Vector2.Max(max, uv[first+j]); }
                        Vector4 expected = new Vector4(min.x, min.y, max.x, max.y);
                        bool valid = expected.z > expected.x && expected.w > expected.y;
                        for (int j = 0; j < 4; j++) valid &= (slices[first+j] - expected).sqrMagnitude < 1e-12f;
                        Check(valid, phase + ": UV1 bounds match unchanged per-quad UV0");
                    }
                    Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "2026TapTap/ColorVineReveal",
                        phase + ": rear retains standard shared water shader");
                }
            Check(surface > 0 && body > 0 && interior > 0 && front > 0, phase + ": all real surface/body/interior/refraction paths checked");
            details.AppendLine(phase + ": waters=" + waters.Length + ", rear=" + rear + ", front=" + front +
                ", surface=" + surface + ", body=" + body + ", interior=" + interior);
            return waters.Length;
        }

        private static void Check(bool okay, string message)
        {
            if (!okay) throw new InvalidOperationException(message);
            checks++;
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Flag, false);
            string result = (error == null ? "PASS " + checks + " formal water binding assertions" : error.ToString()) + "\n" + details;
            File.WriteAllText("Library/GameplayWaterFormalBindingAudit.txt", result); Debug.Log(result);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
