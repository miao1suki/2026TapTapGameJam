using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Project.CameraModes;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.GameFlow
{
    /// <summary>A presentation lease over the real level, not a second game or camera writer.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldMenuPresentation : MonoBehaviour, ICameraControlSource, ICameraLensOverrideSource
    {
        [SerializeField, Min(.1f), InspectorName("进入游戏运镜秒数")] private float enterSeconds = 1.35f;
        [SerializeField, Min(.1f), InspectorName("回到菜单运镜秒数")] private float returnSeconds = 1.15f;
        [SerializeField, Range(.25f, 1f), InspectorName("菜单近景缩放比例")] private float closeupScale = .58f;
        [SerializeField, InspectorName("菜单中玩家屏幕位置")] private Vector2 playerViewport = new Vector2(.72f, .28f);
        private readonly List<SceneLease> scenes = new List<SceneLease>();
        private IDisposable interactionLease;
        private CameraControlHandle cameraLease;
        private CameraControlManager cameraManager;
        private CameraState menuState;
        private Scene activeWorld;
        private GameObject frameHoldRoot;
        private RenderTexture heldFrame;
        private RawImage frameHoldImage;
        private bool frameCapturePending;
        private AudioListener handoffListener;
        public bool IsPreviewing => interactionLease != null;
        public Scene ActiveWorld => activeWorld;
        public int FrameHandoffCount { get; private set; }
        public string CameraControlName => "开始菜单真实场景近景";

        private sealed class ToggleState
        {
            public Behaviour target;
            public bool enabled;
            public void Restore() { if (target != null) target.enabled = enabled; }
        }
        private sealed class ColliderState
        {
            public Collider target;
            public bool enabled;
            public void Restore() { if (target != null) target.enabled = enabled; }
        }
        private sealed class SceneLease : IDisposable
        {
            public Scene scene;
            public readonly List<ToggleState> interactions = new List<ToggleState>();
            public readonly List<ToggleState> cameras = new List<ToggleState>();
            public readonly List<ColliderState> colliders = new List<ColliderState>();
            public IDisposable playerLock;
            public Rigidbody body;
            public bool bodyKinematic;
            public void Dispose()
            {
                // Restore the physical participant before its input. Never clear somebody else's lock.
                foreach (var item in colliders) item.Restore();
                if (body != null) body.isKinematic = bodyKinematic;
                foreach (var item in interactions) item.Restore();
                playerLock?.Dispose();
                playerLock = null;
            }
        }

        public void BeginPreview()
        {
            interactionLease ??= GameplayPresentationGate.Suppress();
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public IEnumerator HoldCurrentFrame()
        {
            ClearFrameHold();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || Screen.width < 1 || Screen.height < 1)
                yield break;
            heldFrame = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32)
                { name = "菜单关卡重载单帧交接", filterMode = FilterMode.Point };
            heldFrame.Create();
            frameCapturePending = true;
            UnityEngine.Rendering.RenderPipelineManager.endFrameRendering += CaptureRenderedFrame;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (frameCapturePending && Time.realtimeSinceStartup < deadline) yield return null;
            UnityEngine.Rendering.RenderPipelineManager.endFrameRendering -= CaptureRenderedFrame;
            if (frameCapturePending)
            {
                // A minimized/non-rendering window must not deadlock the game flow.
                ClearFrameHold();
                yield break;
            }
            frameHoldRoot = new GameObject("World Menu Frame Handoff", typeof(RectTransform), typeof(Canvas));
            frameHoldRoot.transform.SetParent(transform, false);
            Canvas canvas = frameHoldRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // Below the live menu, above the world; never captures input.
            frameHoldImage = StartMenuUiFactory.CreateRect("Previous World", frameHoldRoot.transform).gameObject.AddComponent<RawImage>();
            StartMenuUiFactory.Stretch(frameHoldImage.rectTransform);
            frameHoldImage.texture = heldFrame;
            frameHoldImage.raycastTarget = false;
            FrameHandoffCount++;
        }

        public IEnumerator ReleaseFrameHold()
        {
            if (frameHoldImage != null)
            {
                RawImage image = frameHoldImage;
                yield return DOTween.To(() => image.color.a, alpha => image.color = new Color(1f, 1f, 1f, alpha), 0f, .24f)
                    .SetUpdate(true).SetTarget(this).WaitForCompletion();
            }
            ClearFrameHold();
        }

        private void CaptureRenderedFrame(UnityEngine.Rendering.ScriptableRenderContext context, Camera[] cameras)
        {
            if (!frameCapturePending || heldFrame == null) return;
            bool worldRendered = false;
            foreach (Camera camera in cameras)
                if (camera != null && camera.cameraType == CameraType.Game && camera.isActiveAndEnabled) { worldRendered = true; break; }
            if (!worldRendered) return;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(heldFrame);
            frameCapturePending = false;
        }

        private void ClearFrameHold()
        {
            UnityEngine.Rendering.RenderPipelineManager.endFrameRendering -= CaptureRenderedFrame;
            frameCapturePending = false;
            if (frameHoldRoot != null) Destroy(frameHoldRoot);
            frameHoldRoot = null;
            frameHoldImage = null;
            if (heldFrame != null) { heldFrame.Release(); Destroy(heldFrame); }
            heldFrame = null;
        }

        public void DisableMenuDevices(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera item in root.GetComponentsInChildren<Camera>(true)) item.enabled = false;
                foreach (AudioListener item in root.GetComponentsInChildren<AudioListener>(true)) item.enabled = false;
                foreach (EventSystem item in root.GetComponentsInChildren<EventSystem>(true)) item.enabled = false;
            }
            EnsureHandoffListener();
        }

        /// <summary>Hold one listener while the old world unloads, without duplicating its registries.</summary>
        public void SuspendWorldDevices(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                    foreach (EventSystem system in root.GetComponentsInChildren<EventSystem>(true)) system.enabled = false;
                }
            EnsureHandoffListener();
        }

        private void EnsureHandoffListener()
        {
            // This runs only on scene handoffs, never as a per-frame discovery scan.
            foreach (AudioListener listener in ProjectDiscovery.FindAll<AudioListener>())
                if (listener != handoffListener && listener.isActiveAndEnabled) return;
            if (handoffListener == null)
            {
                var root = new GameObject("__WorldMenuAudioHandoff");
                root.transform.SetParent(transform, false);
                handoffListener = root.AddComponent<AudioListener>();
            }
            handoffListener.enabled = true;
        }

        private void ReleaseHandoffListener()
        {
            if (handoffListener != null) handoffListener.enabled = false;
        }

        /// <summary>Called from sceneLoaded, before the first physics step or gameplay Update.</summary>
        public void StageWorld(Scene scene)
        {
            if (!IsPreviewing || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var existing in scenes) if (existing.scene.handle == scene.handle) return;
            var lease = new SceneLease { scene = scene };
            scenes.Add(lease);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera item in root.GetComponentsInChildren<Camera>(true)) Disable(item, lease.cameras);
                foreach (AudioListener item in root.GetComponentsInChildren<AudioListener>(true)) Disable(item, lease.cameras);
                foreach (Canvas item in root.GetComponentsInChildren<Canvas>(true))
                    if (item.renderMode != RenderMode.WorldSpace) Disable(item, lease.interactions);
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!candidate.CompareTag("Player")) continue;
                    foreach (MonoBehaviour component in candidate.GetComponents<MonoBehaviour>())
                    {
                        if (component == null) continue;
                        // Player lives in the default assembly; the control-lock lease is resolved once,
                        // without reversing the dependency from the flow assembly to gameplay.
                        MethodInfo acquire = component.GetType().GetMethod("AcquireControlLock", new[] { typeof(object) });
                        if (acquire != null && lease.playerLock == null)
                            lease.playerLock = acquire.Invoke(component, new object[] { this }) as IDisposable;
                        string type = component.GetType().Name;
                        if (type == "PlayerInputDriver" || type == "GreenHookAbility" || type == "PlayerColorWheel")
                            Disable(component, lease.interactions);
                    }
                    lease.body = candidate.GetComponent<Rigidbody>();
                    if (lease.body != null)
                    {
                        lease.bodyKinematic = lease.body.isKinematic;
                        if (!lease.body.isKinematic) { lease.body.linearVelocity = Vector3.zero; lease.body.angularVelocity = Vector3.zero; }
                        lease.body.isKinematic = true;
                    }
                    foreach (Collider collider in candidate.GetComponentsInChildren<Collider>(true))
                    {
                        lease.colliders.Add(new ColliderState { target = collider, enabled = collider.enabled });
                        collider.enabled = false;
                    }
                }
            }
            EnsureHandoffListener();
        }

        public void UseWorld(Scene scene, StartMenuController menu)
        {
            StageWorld(scene);
            cameraLease.Release(CameraTransition.Immediate);
            cameraLease = default;
            ReleaseHandoffListener();
            foreach (var lease in scenes)
            {
                bool chosen = lease.scene.handle == scene.handle;
                foreach (var item in lease.cameras)
                    if (item.target != null) item.target.enabled = chosen && item.enabled;
            }
            activeWorld = scene;
            cameraManager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                cameraManager = root.GetComponentInChildren<CameraControlManager>(true);
                if (cameraManager != null) break;
            }
            if (cameraManager == null) throw new InvalidOperationException("Gaming 缺少 CameraControlManager，不能展示融合菜单。");
            ResolveMenuCamera();
            cameraLease = cameraManager.RequestControl(this, CameraControlPriorities.Cutscene + 100,
                CameraInterruptionPolicy.BlockAll, CameraTransition.Immediate);
            cameraManager.ForceTakeControl(cameraLease, CameraTransition.Immediate);
            menu.SetBackgroundVisible(false);
            menu.SetBackgroundScrollEnabled(false);
            menu.SetPresented(true, false);
            menu.SetFusionContent(1f);
        }

        private void ResolveMenuCamera()
        {
            menuState = cameraManager.CurrentState;
            var mode = cameraManager.GetComponent<CameraModeController>();
            if (mode != null && mode.TryGetCameraState(new CameraControlContext(cameraManager.FocusPoint, 0f), out var gameplay))
                menuState = gameplay;
            menuState.projection = CameraProjectionMode.Orthographic;
            menuState.orthographicSize = Mathf.Max(.5f, menuState.orthographicSize * closeupScale);
            float aspect = cameraManager.OutputCamera != null ? cameraManager.OutputCamera.aspect : 16f / 9f;
            Vector3 focus = cameraManager.FocusTarget != null ? cameraManager.FocusTarget.position : cameraManager.FocusPoint;
            menuState.position.x = focus.x - (Mathf.Clamp01(playerViewport.x) * 2f - 1f) * menuState.orthographicSize * aspect;
            menuState.position.y = focus.y - (Mathf.Clamp01(playerViewport.y) * 2f - 1f) * menuState.orthographicSize;
        }

        public IEnumerator EnterGameplay(StartMenuController menu)
        {
            menu.SetPresented(true, false);
            Tween ui = DOTween.To(() => 1f, value => menu.SetFusionContent(value), 0f, enterSeconds * .65f)
                .SetEase(Ease.InCubic).SetUpdate(true).SetTarget(this);
            cameraLease.Release(CameraTransition.Ease(enterSeconds));
            cameraLease = default;
            yield return new WaitForSecondsRealtime(enterSeconds);
            ui.Complete();
            menu.SetPresented(false);
            EndPreview();
        }

        public IEnumerator ReturnToMenu(StartMenuController menu)
        {
            BeginPreview();
            FreezeActiveWorld();
            ResolveMenuCamera();
            cameraLease = cameraManager.RequestControl(this, CameraControlPriorities.Cutscene + 100,
                CameraInterruptionPolicy.BlockAll, CameraTransition.Ease(returnSeconds));
            cameraManager.ForceTakeControl(cameraLease, CameraTransition.Ease(returnSeconds));
            menu.ShowMain();
            menu.SetPresented(true, false);
            menu.SetFusionContent(0f);
            yield return DOTween.To(() => 0f, value => menu.SetFusionContent(value), 1f, returnSeconds)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this).WaitForCompletion();
            menu.SetPresented(true);
        }

        public void FreezeActiveWorld()
        {
            if (!activeWorld.IsValid() || !activeWorld.isLoaded) return;
            StageWorld(activeWorld);
            ReleaseHandoffListener();
            foreach (var lease in scenes)
                if (lease.scene.handle == activeWorld.handle)
                    foreach (var device in lease.cameras) device.Restore();
        }

        public void EndPreview()
        {
            foreach (var lease in scenes) lease.Dispose();
            scenes.Clear();
            interactionLease?.Dispose();
            interactionLease = null;
        }

        internal void CollectStagedWorlds(List<Scene> result)
        {
            foreach (var lease in scenes)
                if (lease.scene.IsValid() && lease.scene.isLoaded && !result.Contains(lease.scene)) result.Add(lease.scene);
            if (activeWorld.IsValid() && activeWorld.isLoaded && !result.Contains(activeWorld)) result.Add(activeWorld);
        }

        internal void RecoverMenu(StartMenuController menu)
        {
            DOTween.Kill(this);
            cameraLease.Release(CameraTransition.Immediate);
            cameraLease = default;
            cameraManager = null;
            activeWorld = default;
            ClearFrameHold();
            EndPreview();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            foreach (StartupCameraGuard guard in GetComponentsInChildren<StartupCameraGuard>(true))
            {
                Camera camera = guard.GetComponent<Camera>();
                if (camera != null) camera.enabled = false;
                Destroy(guard.gameObject);
            }
            if (menu == null)
            {
                // No menu asset exists to restore, but never retain a stale screenshot,
                // shared gate, frozen player, camera lease or transition flag.
                EnsureHandoffListener();
                return;
            }
            Camera selectedCamera = null;
            AudioListener selectedListener = null;
            EventSystem selectedEvents = null;
            foreach (GameObject root in menu.gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;
                    if (selectedCamera == null && camera.gameObject.activeInHierarchy) selectedCamera = camera;
                }
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    listener.enabled = false;
                    if (selectedListener == null && listener.gameObject.activeInHierarchy) selectedListener = listener;
                }
                foreach (EventSystem events in root.GetComponentsInChildren<EventSystem>(true))
                {
                    events.enabled = false;
                    if (selectedEvents == null && events.gameObject.activeInHierarchy) selectedEvents = events;
                }
            }
            if (selectedCamera != null) selectedCamera.enabled = true;
            if (selectedEvents != null) selectedEvents.enabled = true;
            if (selectedListener != null)
            {
                ReleaseHandoffListener();
                selectedListener.enabled = true;
            }
            else EnsureHandoffListener();
            menu.ShowMain();
            menu.SetBackgroundVisible(false);
            menu.SetFusionContent(1f);
            menu.SetPresented(true);
        }

        private static void Disable(Behaviour item, List<ToggleState> states)
        {
            states.Add(new ToggleState { target = item, enabled = item.enabled });
            item.enabled = false;
        }

        public bool TryGetCameraState(in CameraControlContext context, out CameraState state) { state = menuState; return true; }
        public bool TryGetLensOverride(out float size, out float fov) { size = menuState.orthographicSize; fov = menuState.fieldOfView; return true; }
        private void OnDestroy()
        {
            DOTween.Kill(this);
            ClearFrameHold();
            cameraLease.Release(CameraTransition.Immediate);
            EndPreview();
        }
    }
}
