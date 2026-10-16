using System;
using System.Collections;
using System.Reflection;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.Rooms;
using Project.StartMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Player.Editor
{
    /// <summary>Exercise the real player and shared transition in an unsaved isolated Gaming scene.</summary>
    public static class RespawnTransitionVerification
    {
        private const string Key = "RespawnTransitionVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int frames, checks;
        private static bool running;
        private static double started;
        private static PlayerController player;
        private static GameSceneLoader loader;

        public static void Run()
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets"))
                throw new Exception("Isolated verification only");
            EditorSceneManager.OpenScene(MobileBuildSetup.GameScene);
            SessionState.SetBool(Key, true);
            Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            frames = checks = 0; running = false; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 180) { Finish(false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 25 || running) return;
            running = true;
            player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Check(player != null, "Gaming contains its standard player");
            player.GetComponent<PlayerInputDriver>().enabled = false;
            foreach (var manager in UnityEngine.Object.FindObjectsByType<RoomLifecycleManager>(FindObjectsSortMode.None))
                typeof(RoomLifecycleManager).GetField("autoRespawnOnDeath", Private).SetValue(manager, false);
            loader = GameSceneLoader.Instance;
            loader.StartCoroutine(Verify());
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) { Debug.LogError("RESPAWN FAIL " + message); Finish(false); throw new Exception(message); }
            checks++; Debug.Log("RESPAWN PASS " + message);
        }

        private static IEnumerator WaitPhase(RespawnTransitionPhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (loader.RespawnPhase != phase && Time.realtimeSinceStartup < deadline) yield return null;
            Check(loader.RespawnPhase == phase, "reached " + phase);
        }

        private static IEnumerator WaitFinished()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (player.IsRespawning && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!player.IsRespawning && !loader.IsRespawnTransitioning, "transition finished within deadline");
        }

        private static void CheckOverlayClear()
        {
            var overlay = loader.TransitionOverlay;
            Check(!overlay.transform.Find("Black Reveal").gameObject.activeSelf &&
                  !overlay.transform.Find("Background Copy").gameObject.activeSelf &&
                  !overlay.transform.Find("Loading Text").gameObject.activeSelf &&
                  !overlay.GetComponent<CanvasGroup>().blocksRaycasts, "overlay and input blocker are cleared");
        }

        private static IEnumerator Verify()
        {
            Check(player.RespawnMode == PlayerRespawnMode.Standard, "standard prefab preserves ordinary respawn by default");
            player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, -1f);
            Check(player.RespawnLoadingSeconds == 0, "negative hold clamps to zero");
            player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, float.NaN);
            Check(player.RespawnLoadingSeconds == 2, "invalid hold has finite safe fallback");
            player.ConfigureRespawn(PlayerRespawnMode.Standard);
            int completed = 0, relocated = 0;
            player.RespawnCompleted += () => completed++;
            player.CameraFollowRelocated += () => relocated++;
            player.RequestRespawn();
            Check(!player.IsRespawning && !player.IsDead && completed == 1 && relocated == 1,
                "ordinary respawn remains synchronous with one relocation/completion");
            ColorRuntimeService.Instance.Unlock("red");
            ColorRuntimeService.Instance.UnlockAbility("blue");
            Vector3 spawn = player.Motor.position;
            player.Motor.position = player.transform.position = spawn + Vector3.right * 8;
            Physics.SyncTransforms();
            player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, .35f);
            IDisposable external = player.AcquireControlLock("another cutscene");
            int externalDepth = player.ControlLockDepth;
            Vector3 before = player.Motor.position;
            player.RequestRespawn();
            Check(player.IsRespawning && player.IsControlLocked && player.IsInvulnerable,
                "slow respawn acquires its own control and damage protection");
            Check(player.ControlLockDepth == externalDepth + 1 && player.Motor.position == before,
                "slow request does not relocate before cover");
            for (int i = 0; i < 5; i++) player.RequestRespawn();
            Check(player.ControlLockDepth == externalDepth + 1 && completed == 1,
                "repeated R requests do not duplicate locks or reset the transition");
            var router = UnityEngine.Object.FindFirstObjectByType<GameUiRouter>();
            router.SetPaused(true, false, true);
            Check(!router.IsPaused && Time.timeScale > 0f, "pause cannot interrupt active respawn");
            yield return new WaitForSecondsRealtime(.15f);
            Check(Vector3.Distance(player.Motor.position, before) < .001f && relocated == 1,
                "motor remains frozen while the iris is still closing");
            yield return WaitPhase(RespawnTransitionPhase.Loading);
            float loadingAt = Time.realtimeSinceStartup;
            Check(relocated == 2 && completed == 1 && !player.IsDead,
                "relocation occurs once under loading, completion waits for reveal");
            player.QueuePlatformDelta(Vector3.right * 10);
            Check((Vector3)typeof(PlayerController).GetField("pendingPlatformDelta", Private).GetValue(player) == Vector3.zero,
                "covered respawn cannot accumulate moving-platform displacement");
            var overlay = loader.TransitionOverlay;
            var background = overlay.transform.Find("Background Copy").GetComponent<RawImage>();
            var text = overlay.transform.Find("Loading Text").GetComponent<TextMeshProUGUI>();
            var black = overlay.transform.Find("Black Reveal").GetComponent<RawImage>();
            Check(background.gameObject.activeSelf && background.texture != null &&
                  background.texture == GameUiArtwork.Load().menuBackground,
                "direct Gaming launch uses the actual menu artwork as loading background");
            Check(background.color.a == 1f && background.rectTransform.rect.width > 0f &&
                  Mathf.Abs(background.rectTransform.anchoredPosition.x) < .01f,
                "loading background is opaque, full-sized and centered after entering");
            Check(text.gameObject.activeSelf && text.font.name.Contains("Cubic"), "loading reuses the configured pixel font");
            Check(text.GetComponent<CanvasGroup>().alpha == 1f &&
                  background.transform.GetSiblingIndex() > black.transform.GetSiblingIndex() &&
                  text.transform.GetSiblingIndex() > background.transform.GetSiblingIndex(),
                "loading text and picture are ordered above the closed black mask");
            Check(black.gameObject.activeSelf && black.material.GetFloat("_Radius") < 0,
                "closed black iris remains behind the loading picture");
            Check(ColorRuntimeService.Instance.IsUnlocked("red") && ColorRuntimeService.Instance.IsAbilityUnlocked("blue"),
                "scene color and player ability progress survive slow respawn");
            player.ConfigureRespawn(PlayerRespawnMode.Standard);
            Check(player.IsRespawning, "changing the next mode does not abort active presentation");
            yield return WaitPhase(RespawnTransitionPhase.BackgroundLeaving);
            Check(Time.realtimeSinceStartup - loadingAt >= .34f, "configured hold is honored independently of animation duration");
            yield return WaitFinished();
            Check(completed == 2 && relocated == 2, "slow respawn completes exactly once after reveal");
            Check(player.ControlLockDepth == externalDepth && player.IsControlLocked,
                "finishing preserves another system's control lock");
            Check(player.InvulnerabilityRemaining > 1.8f, "respawn invulnerability starts after presentation");
            external.Dispose();
            Check(!player.IsControlLocked, "own and external locks release independently");
            CheckOverlayClear();

            player.EnterDeath();
            Check(player.IsDead && !player.CanAutomaticallyRespawn, "normal death animation still gates automatic respawn");
            yield return new WaitForSeconds(1.2f);
            Check(player.CanAutomaticallyRespawn, "automatic respawn becomes available after the death animation");
            player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, 0);
            player.RequestRespawn();
            Check(!player.CanAutomaticallyRespawn, "automatic death loop cannot restart active transition");
            yield return WaitFinished();
            Check(!player.IsDead && completed == 3, "death and live R both use the configured slow mode");

            foreach (var phase in new[] { RespawnTransitionPhase.Covering, RespawnTransitionPhase.Loading,
                         RespawnTransitionPhase.Revealing })
            {
                player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, .1f);
                player.RequestRespawn();
                yield return WaitPhase(phase);
                loader.CancelRespawnTransition(player.transform);
                Check(!player.IsRespawning && !player.IsControlLocked, "cancellation releases owner lock at " + phase);
                CheckOverlayClear();
                yield return new WaitForSecondsRealtime(.15f);
                Check(!loader.IsRespawnTransitioning, "cancelled tween cannot resume at " + phase);
            }

            player.RequestRespawn();
            player.enabled = false;
            Check(!player.IsRespawning && !loader.IsRespawnTransitioning, "disabling the controller cancels its persistent-loader routine");
            CheckOverlayClear();
            player.enabled = true;

            player.RequestRespawn();
            yield return WaitPhase(RespawnTransitionPhase.Loading);
            yield return loader.PrepareMenuReturnTransition(player.transform);
            Check(!player.IsRespawning && !loader.IsRespawnTransitioning && !player.IsControlLocked,
                "menu return preempts respawn and releases the old player lock");
            Check(background.gameObject.activeSelf && text.gameObject.activeSelf,
                "menu transition retains ownership of the same overlay");
            yield return new WaitForSecondsRealtime(.4f);
            Check(background.gameObject.activeSelf, "cancelled respawn does not hide the menu loading background later");
            overlay.ResetPresentation();

            var temporaryOwner = new GameObject("Temporary transition owner");
            bool orphanDone = false;
            Check(loader.TryStartRespawnTransition(temporaryOwner.transform, 1, null, ok => orphanDone = !ok),
                "loader accepts an explicit presentation owner");
            Check(!loader.TryStartRespawnTransition(player.transform, 1, null, null), "second presentation owner cannot steal the overlay");
            loader.CancelRespawnTransition(player.transform);
            Check(loader.IsRespawnTransitioning, "different owner cannot cancel a presentation");
            temporaryOwner.SetActive(false);
            yield return null; yield return null;
            Check(orphanDone && !loader.IsRespawnTransitioning, "inactive orphan owner is cleaned up without a controller callback");
            CheckOverlayClear();
            temporaryOwner.SetActive(true);

            bool unscaledDone = false;
            Time.timeScale = 0;
            Check(loader.TryStartRespawnTransition(temporaryOwner.transform, .1f, null, ok => unscaledDone = ok),
                "presentation can be scheduled with scaled gameplay time stopped");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!unscaledDone && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = 1;
            Check(unscaledDone, "iris, background, hold and reverse all use unscaled time");
            CheckOverlayClear();
            UnityEngine.Object.Destroy(temporaryOwner);
            player.ConfigureRespawn(PlayerRespawnMode.Standard);
            Finish(true);
        }

        private static void Finish(bool ok)
        {
            Time.timeScale = 1;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            if (ok) Debug.Log("RESPAWN TRANSITION VERIFICATION PASSED " + checks);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
