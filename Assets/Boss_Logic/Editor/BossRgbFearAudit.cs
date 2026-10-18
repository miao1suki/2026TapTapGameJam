using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.Player;
using TapTap.TA.RgbGlitch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.BossLogic.Editor
{
    /// <summary>Presentation-only regression of the real right-move encounter gates.</summary>
    public static class BossRgbFearAudit
    {
        private const string Flag = "BossRgbFearAudit.Running";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly StringBuilder report = new StringBuilder();
        private static double started;
        private static int checks;

        public static void Run()
        {
            string project = Application.dataPath.Replace('\\', '/');
            bool isolated = project.Contains("/Library/FlowRetirementProbe/Assets") ||
                project.Contains("/Library/TAIntegration-20261018/Assets");
            if (!isolated || Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Run only in the silent isolated integration/probe project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start the audit outside Play mode.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (EditorApplication.timeSinceStartup - started > 120d)
            { Finish(false, "Timeout entering Play mode"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            try
            {
                AudioListener.pause = true;
                AudioListener.volume = 0f;
                Physics.simulationMode = SimulationMode.Script;
                checks = 0;
                report.Clear();
                VerifyEncounter();
                passed = true;
            }
            catch (Exception error) { report.AppendLine(error.ToString()); Debug.LogException(error); }
            finally
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                objects.Clear();
                Physics.simulationMode = previous;
            }
            Finish(passed, (passed ? "PASS " : "FAIL ") + checks + "\n" + report);
        }

        private static void VerifyEncounter()
        {
            GameObject playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/Player/Player.prefab"), new Vector3(0f, 100f, 0f), Quaternion.identity);
            objects.Add(playerObject);
            PlayerController player = playerObject.GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            RgbGlitchSceneDriver driver = player.GetComponent<RgbGlitchSceneDriver>();
            player.ConfigureRespawn(PlayerRespawnMode.Standard);

            BossRightMoveExecutor first = Boss("First Boss");
            BossController controller = first.GetComponent<BossController>();
            BossRgbFearPresentation bridge = first.GetComponent<BossRgbFearPresentation>();
            Check(bridge != null, "existing right-move executor installs exactly one presentation bridge");
            Invoke(first, "FixedUpdate"); Tick(2f);
            Check(!first.IsMoving && !driver.BossFearActive && Clear(), "Boss presence before tracking gate does not enable fear");
            controller.SetTrackingEnabled(true);
            Set(first, "readyTime", Time.time + 10f);
            Invoke(first, "FixedUpdate"); Tick(2f);
            Check(!first.IsMoving && Clear(), "existing encounter start-delay gate is preserved");
            Set(first, "readyTime", Time.time - 1f);
            Invoke(first, "FixedUpdate"); Tick(2f);
            Check(first.IsMoving && driver.BossFearActive && Near(.4f), "actual right-move ownership starts mild Boss fear");

            player.EnterDeath();
            Invoke(bridge, "Update"); Invoke(driver, "Update"); Tick(2f);
            Check(Near(1f), "continued Boss movement does not override the player's full death layer");
            player.RequestRespawn(); Tick(2f); Invoke(driver, "Update");
            Check(Near(.4f), "respawn restores mild fear while the actual encounter remains active");

            BossRightMoveExecutor second = Boss("Second Boss");
            second.GetComponent<BossController>().SetTrackingEnabled(true);
            Invoke(second, "FixedUpdate"); Tick(2f);
            first.SetMoving(false); Tick(2f);
            Check(Near(.4f), "one stopping executor cannot clear another active encounter's fear");
            second.SetMoving(false); Tick(2f);
            Check(Clear() && !driver.BossFearActive, "last actual stop fades mild fear back to clear");

            first.SetMoving(true);
            Invoke(first, "FixedUpdate"); Tick(2f);
            player.EnterDeath();
            controller.SetTrackingEnabled(false);
            Invoke(first, "FixedUpdate"); Tick(2f);
            Check(Near(1f) && !driver.BossFearActive, "encounter stop fades only fear, never resets pending player death");
            player.RequestRespawn(); Tick(2f);
            Check(Clear(), "respawn after stopped encounter returns to normal clear view");

            controller.SetTrackingEnabled(true);
            Invoke(first, "FixedUpdate"); Tick(2f);
            first.enabled = false; Tick(2f);
            Check(Clear() && !driver.BossFearActive, "executor disable ends the encounter presentation");
            first.enabled = true;
            Invoke(first, "FixedUpdate"); Tick(2f);
            controller.Kill(); Tick(2f);
            Check(controller.IsDead && !first.IsMoving && Clear(), "actual Boss death stops motion and clears only fear");
        }

        private static BossRightMoveExecutor Boss(string name)
        {
            GameObject result = new GameObject(name);
            objects.Add(result);
            result.SetActive(false);
            result.AddComponent<BoxCollider>();
            result.AddComponent<Rigidbody>().useGravity = false;
            BossController controller = result.AddComponent<BossController>();
            controller.SetTrackingEnabled(false);
            BossRightMoveExecutor executor = result.AddComponent<BossRightMoveExecutor>();
            result.SetActive(true);
            return executor;
        }

        private static void Tick(float delta)
        {
            var effect = (RgbGlitchEffect)typeof(RgbGlitchEffect).GetField("instance",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (effect != null) Invoke(effect, "Tick", delta);
        }
        private static bool Near(float level) => Mathf.Abs(RgbGlitchEffect.Intensity - level) < .001f;
        private static bool Clear() => Near(0f);
        private static void Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, arguments);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Hidden).SetValue(target, value);
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(label);
            report.AppendLine("PASS " + label);
        }
        private static void Finish(bool passed, string text)
        {
            EditorApplication.update -= Wait;
            SessionState.EraseBool(Flag);
            File.WriteAllText("Library/BossRgbFearAudit.txt", text);
            Debug.Log(text);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
