using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using Project.Audio;
using Project.Mechanisms;
using Project.ContactCubes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Rooms.Editor
{
    internal static class GameplayResponsivenessVerification
    {
        private const string Key = "GameplayResponsivenessVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int frames;
        private static ClimbableVineFeature[] profileVines;
        public static void RunProfile()
        {
            SessionState.SetBool(Key + ".Profile", true); Run();
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            EditorSceneManager.OpenScene("Assets/Scenes/Pick.unity");
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frames == 2) ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            if (frames < 12) return;
            if (SessionState.GetBool(Key + ".Profile", false) && ProfileFrame()) return;
            EditorApplication.update -= Wait;
            var report = new StringBuilder();
            try
            {
                var player = Object.FindFirstObjectByType<PlayerController>();
                player.GetComponent<PlayerInputDriver>().enabled = false;
                var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
                var vine = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).First(v => v.IsAttached);
                report.AppendLine($"vines={Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).Length}, room members={Object.FindObjectsByType<RoomResetObject>(FindObjectsSortMode.None).Length}");
                var fixedUpdate = typeof(PlayerController).GetMethod("FixedUpdate", Private);
                var reattach = typeof(PlayerController).GetField("climbReattachUntil", Private);
                var ascent = typeof(PlayerController).GetField("protectBounceFromClimbing", Private);
                VerifyClimbHandoff(player, vine, fixedUpdate, reattach, ascent, report);
                VerifyCheckpoints(player, report);
                double max = 0; var watch = new Stopwatch();
                for (int i = 0; i < 30; i++)
                {
                    player.Motor.position = vine.transform.position + Vector3.left;
                    player.Motor.linearVelocity = Vector3.zero;
                    reattach.SetValue(player, 0f);
                    ascent.SetValue(player, false);
                    player.EnterClimb(vine); player.SetMoveInput(Vector2.right); player.RequestJump();
                    watch.Restart(); fixedUpdate.Invoke(player, null); watch.Stop();
                    max = Math.Max(max, watch.Elapsed.TotalMilliseconds);
                }
                report.AppendLine($"Wall kicks max CPU: {max:F3} ms (30 repeated)");
                foreach (var region in manager.Regions)
                {
                    watch.Restart(); manager.ResetRegion(region.id); watch.Stop();
                    report.AppendLine($"Reset {region.label}: {watch.Elapsed.TotalMilliseconds:F3} ms");
                }
                File.WriteAllText("Library/GameplayResponsiveness.txt", report.ToString());
                UnityEngine.Debug.Log(report.ToString());
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                File.WriteAllText("Library/GameplayResponsiveness.txt", report + "\nFAIL " + e);
                UnityEngine.Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
            }
        }

        private static void VerifyClimbHandoff(PlayerController player, ClimbableVineFeature vine,
            MethodInfo tick, FieldInfo reattach, FieldInfo ascent, StringBuilder report)
        {
            var ladder = new GameObject("Probe ladder").AddComponent<NeutralLadder>();
            var cube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab")).GetComponent<ContactCube>();
            foreach (Component source in new Component[] { vine, ladder, cube })
            {
                source.transform.position = new Vector3(-200f, 200f, 0f);
                player.Motor.position = source.transform.position + Vector3.left;
                player.transform.position = player.Motor.position;
                player.Motor.linearVelocity = Vector3.zero;
                ascent.SetValue(player, false); reattach.SetValue(player, 0f);
                player.EnterClimb(source); player.SetMoveInput(Vector2.right);
                if (!player.IsClimbing) throw new Exception("Climb entry failed: " + source.GetType().Name);
                player.RequestJump(); tick.Invoke(player, null);
                if (player.Motor.linearVelocity.y <= 0f) throw new Exception("Kick failed");
                reattach.SetValue(player, 0f); // The old timer has expired while still ascending.
                player.Motor.linearVelocity = new Vector3(-2f, 7f, 0f);
                if (player.IsClimbing) throw new Exception("Mid-ascent climb re-entry: " + source.GetType().Name);
                tick.Invoke(player, null);
                if (player.Motor.linearVelocity.y <= 6f) throw new Exception("Ascent was cut short");
                player.Motor.linearVelocity = Vector3.zero;
                tick.Invoke(player, null);
                if (!(bool)ascent.GetValue(player) && player.IsClimbing)
                    report.AppendLine("PASS kick/ascent/apex reattach: " + source.GetType().Name);
                else throw new Exception("Climb never re-enabled at apex");
                player.ExitClimb(source);
            }
            Object.Destroy(ladder.gameObject); Object.Destroy(cube.gameObject);
        }

        private static void VerifyCheckpoints(PlayerController player, StringBuilder report)
        {
            var first = new GameObject("Probe checkpoint A").AddComponent<RespawnAnchorFeature>();
            var second = new GameObject("Probe checkpoint B").AddComponent<RespawnAnchorFeature>();
            first.transform.position = second.transform.position = new Vector3(-400f, 100f, 0f);
            if (!first.ActivateForPlayer(player) || !first.IsActivated || !first.IsCurrent ||
                first.ActivateForPlayer(player)) throw new Exception("First checkpoint/deduplication");
            if (!second.ActivateForPlayer(player) || first.IsCurrent || !second.IsCurrent)
                throw new Exception("Switch checkpoint binding");
            if (!first.ActivateForPlayer(player) || !first.IsCurrent || second.IsCurrent)
                throw new Exception("Return to unlocked checkpoint");
            var catalog = AudioCatalog.LoadDefault();
            foreach (string cue in new[] { AudioCueIds.CheckpointUnlock, AudioCueIds.CheckpointSwitch })
                if (!catalog.TryGetCue(cue, out var definition) || !definition.HasClips || definition.Loop)
                    throw new Exception("Checkpoint audio catalog: " + cue);
            report.AppendLine("PASS checkpoint first activation/switch/revisit/deduplication and two one-shot cues");
            Object.Destroy(first.gameObject); Object.Destroy(second.gameObject);
        }

        private static bool ProfileFrame()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (frames == 12)
            {
                player.GetComponent<PlayerInputDriver>().enabled = false;
                profileVines = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).Where(v => v.IsAttached).ToArray();
                UnityEditorInternal.ProfilerDriver.ClearAllFrames();
                UnityEditorInternal.ProfilerDriver.enabled = true;
            }
            if (frames < 412)
            {
                int step = (frames - 12) % 40;
                if (step == 0)
                {
                    var vine = profileVines[(frames - 12) / 40 % profileVines.Length];
                    player.Motor.position = vine.transform.position + Vector3.left * 1.05f;
                    player.Motor.linearVelocity = Vector3.zero;
                    typeof(PlayerController).GetField("climbReattachUntil", Private).SetValue(player, 0f);
                    player.EnterClimb(vine);
                }
                player.SetMoveInput(Vector2.right);
                if (step == 8) player.RequestJump();
                return true;
            }
            UnityEditorInternal.ProfilerDriver.enabled = false;
            var samples = new Dictionary<string, double>();
            for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex; frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
            {
                using (var data = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!data.valid) continue;
                    for (int sample = 0; sample < data.sampleCount; sample++)
                    {
                        string name = data.GetSampleName(sample);
                        double ms = data.GetSampleTimeMs(sample);
                        if (ms < 1d) continue;
                        if (!samples.TryGetValue(name, out double old) || ms > old) samples[name] = ms;
                    }
                }
            }
            File.WriteAllLines("Library/GameplayProfile.txt", samples.OrderByDescending(p => p.Value).Take(70).Select(p => $"{p.Value:F3} ms {p.Key}"));
            SessionState.SetBool(Key + ".Profile", false);
            return false;
        }
    }
}
