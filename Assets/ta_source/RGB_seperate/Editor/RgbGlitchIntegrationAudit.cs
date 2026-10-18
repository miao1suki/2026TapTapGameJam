using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TapTap.TA.RgbGlitch.Editor
{
    /// <summary>Silent isolated-project lifecycle regression; does not claim GPU visual verification.</summary>
    public static class RgbGlitchIntegrationAudit
    {
        private const string Flag = "RgbGlitchIntegrationAudit.Running";
        private const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
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
            {
                Finish(false, "Timed out entering Play mode.");
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            float previousTimeScale = Time.timeScale;
            bool passed = false;
            try
            {
                AudioListener.pause = true;
                AudioListener.volume = 0f;
                Physics.simulationMode = SimulationMode.Script;
                checks = 0;
                report.Clear();
                VerifyLifecycle();
                passed = true;
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                objects.Clear();
                RgbGlitchEffect.ResetEffect();
                RgbGlitchEffect.UseRendererDefaults();
                Time.timeScale = previousTimeScale;
                Physics.simulationMode = previous;
            }
            Finish(passed, (passed ? "PASS " : "FAIL ") + checks + "\n" + report);
        }

        private static void VerifyLifecycle()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Check(source != null, "standard Player prefab exists");
            Check(source.GetComponents<RgbGlitchSceneDriver>().Length == 1, "exactly one RGB lifecycle driver on standard Player");
            GameObject fixture = Object.Instantiate(source, new Vector3(0f, 100f, 0f), Quaternion.identity);
            objects.Add(fixture);
            PlayerController player = fixture.GetComponent<PlayerController>();
            PlayerHealth health = fixture.GetComponent<PlayerHealth>();
            RgbGlitchSceneDriver driver = fixture.GetComponent<RgbGlitchSceneDriver>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            Check(new SerializedObject(driver).FindProperty("targetPlayer").objectReferenceValue == player,
                "prefab driver directly references its own PlayerController");
            Check(driver.OwnsEffect && !driver.BossFearActive && Nearly(RgbGlitchEffect.Intensity, 0f),
                "normal gameplay starts clear, with one player owner and no automatic Boss fear");

            player.EnterDeath();
            Check(player.IsDead && Nearly(RgbGlitchEffect.Intensity, 1f), "ordinary/fall death enables full RGB layer");
            Tick(5f);
            Check(Nearly(RgbGlitchEffect.Intensity, 1f), "death layer holds until actual respawn completion");

            GameObject legacy = new GameObject("Optional legacy RGB rig");
            objects.Add(legacy);
            RgbGlitchSceneDriver secondary = legacy.AddComponent<RgbGlitchSceneDriver>();
            Check(!secondary.OwnsEffect && driver.OwnsEffect, "scene rig cannot steal standard-player ownership");
            secondary.enabled = false;
            Check(Nearly(RgbGlitchEffect.Intensity, 1f), "disabling passive rig does not clear another owner's death layer");
            GameObject previewObject = new GameObject("Standalone preview must stay passive");
            objects.Add(previewObject);
            RgbGlitchPreview preview = previewObject.AddComponent<RgbGlitchPreview>();
            Invoke(preview, "Update");
            preview.enabled = false;
            Check(Nearly(RgbGlitchEffect.Intensity, 1f), "preview update/disable cannot overwrite gameplay owner");

            // Exercise the real slow-respawn callbacks without starting a second scene-loader overlay.
            Set(player, "respawnInProgress", true);
            Invoke(player, "PerformRespawn");
            Invoke(driver, "Update");
            Tick(5f);
            Check(!player.IsDead && player.IsRespawning && Nearly(RgbGlitchEffect.Intensity, 1f),
                "camera relocation/health revival beneath loading cover does not start RGB recovery");
            Invoke(player, "FinishRespawnPresentation", true);
            Check(!player.IsRespawning, "real completion callback closes slow-respawn lifecycle");
            Tick(2f);
            Check(Nearly(RgbGlitchEffect.Intensity, 0f), "RespawnCompleted fades death to normal clear baseline");

            driver.PlayDeath();
            player.ConfigureRespawn(PlayerRespawnMode.Standard);
            player.RequestRespawn();
            Tick(2f);
            Check(!player.IsDead && Nearly(RgbGlitchEffect.Intensity, 0f), "alive R reset also clears death presentation");
            Set(player, "invulnerableUntil", -1f);
            health.TakeDamage(health.MaxHealth);
            Check(player.IsDead && Nearly(RgbGlitchEffect.Intensity, 1f), "PlayerHealth.Died path is bound immediately");
            player.RequestRespawn();
            Tick(2f);

            Check(RgbGlitchEffect.BeginBossFear(0f), "explicit Boss entry finds the active lifecycle owner");
            Invoke(driver, "Update");
            Check(driver.BossFearActive && Nearly(RgbGlitchEffect.Intensity, .4f), "Boss fear starts only on explicit request");
            CheckParameters(.28f, 8f, .0035f, 6f, .55f, "Boss uses TA mild preset");
            player.EnterDeath();
            CheckParameters(.65f, 15f, .018f, 10f, .85f, "Boss death temporarily uses strong death preset");
            player.RequestRespawn();
            Tick(.6f);
            Invoke(driver, "Update");
            CheckParameters(.65f, 15f, .018f, 10f, .85f, "strong tuning remains throughout death recovery");
            Time.timeScale = 0f;
            Tick(2f);
            Invoke(driver, "Update");
            Check(Nearly(RgbGlitchEffect.Intensity, .4f), "death fade returns to retained Boss baseline");
            CheckParameters(.28f, 8f, .0035f, 6f, .55f, "mild Boss tuning resumes after death layer fades");
            RgbGlitchEffect.EndBossFear(.8f);
            Tick(1f);
            Invoke(driver, "Update");
            Check(!driver.BossFearActive && Nearly(RgbGlitchEffect.Intensity, 0f), "explicit Boss exit restores clear gameplay");
            Time.timeScale = 1f;

            driver.enabled = false;
            secondary.enabled = true;
            Check(secondary.OwnsEffect, "standalone TA rig still works when no player driver owns the pass");
            driver.enabled = true;
            Check(driver.OwnsEffect && !secondary.OwnsEffect, "standard player preempts an earlier optional rig");
            driver.PlayDeath();
            secondary.enabled = false;
            Check(Nearly(RgbGlitchEffect.Intensity, 1f), "preempted rig cleanup cannot reset player presentation");
            driver.enabled = false;
            Check(RgbGlitchSceneDriver.Active == null && Nearly(RgbGlitchEffect.Intensity, 0f),
                "owner disable/scene exit immediately clears all presentation layers");
            Check(!TryParameters(out _, out _), "owner exit removes global renderer tuning override");
            Set(player, "invulnerableUntil", -1f);
            health.TakeDamage(health.MaxHealth);
            Check(Nearly(RgbGlitchEffect.Intensity, 0f), "disabled driver has removed its death subscriptions");
            Check(!RgbGlitchEffect.BeginBossFear(0f), "Boss API does not create an unowned hidden rig");
        }

        private static void Tick(float delta)
        {
            var effect = (RgbGlitchEffect)typeof(RgbGlitchEffect).GetField("instance", HiddenStatic).GetValue(null);
            if (effect != null) Invoke(effect, "Tick", delta);
        }

        private static bool TryParameters(out Vector4 parameters, out float jitter)
        {
            object[] values = { default(Vector4), 0f };
            bool result = (bool)typeof(RgbGlitchEffect).GetMethod("TryGetParameters", HiddenStatic).Invoke(null, values);
            parameters = (Vector4)values[0];
            jitter = (float)values[1];
            return result;
        }

        private static void CheckParameters(float dark, float rate, float shake, float separation, float jitter, string label)
        {
            bool exists = TryParameters(out Vector4 value, out float actualJitter);
            Check(exists && Nearly(value.x, dark) && Nearly(value.y, rate) && Nearly(value.z, shake) &&
                Nearly(value.w, separation) && Nearly(actualJitter, jitter), label);
        }

        private static bool Nearly(float actual, float expected) => Mathf.Abs(actual - expected) < .001f;
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
            File.WriteAllText("Library/RgbGlitchIntegrationAudit.txt", text);
            Debug.Log(text);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
