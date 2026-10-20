using System;
using System.Collections;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.BossLogic;
using Project.CameraModes;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.Items;
using Project.Player;
using Project.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.FastTravel
{
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class WorldTravelMap : MonoBehaviour, ICameraControlSource, ICameraLensOverrideSource
    {
        private sealed class Label
        {
            public RectTransform Rect;
            public Vector2 Position;
            public string Region;
            public string Name;
            public TMP_Text Text;
        }
        private readonly List<Label> labels = new List<Label>();
        private readonly List<Label> markers = new List<Label>();
        private readonly List<RespawnAnchorFeature> checkpoints = new List<RespawnAnchorFeature>();
        private readonly List<Rect> occupiedLabels = new List<Rect>();
        private PlayerController player;
        private RoomLifecycleManager rooms;
        private TravelMapSettings settings;
        private GameObject canvasRoot, panel, entry;
        private RectTransform safeArea, viewport, playerMarker;
        private TravelMapGraphic map;
        private TMP_Text summary, status;
        private IDisposable playerLock, presentationLock;
        private CameraControlManager cameraManager;
        private GameFlowController boundFlow;
        private CameraControlHandle cameraHandle;
        private CameraState travelState;
        private Coroutine travel;
        private RespawnAnchorFeature travellingTo;
        private bool opened, dirty;
        private bool unlockDirty = true, cachedUnlocked;
        private float cachedRequiredRatio = -1f;
        private bool ownsCursor, previousCursorVisible;
        private string selectedRegion;
        private Rect lastSafeArea;
        private Vector2 lastViewportSize;
        public bool IsOpen => opened;
        public bool IsTravelling => travel != null;
        public string CameraControlName => "重生点传送";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrap()
        {
            PlayerController.PlayerActivated -= Install;
            PlayerController.PlayerActivated += Install;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallExisting()
        {
            foreach (var item in PlayerController.ActivePlayers) Install(item);
        }
        private static void Install(PlayerController item)
        {
            if (item != null && item.GetComponent<WorldTravelMap>() == null) item.gameObject.AddComponent<WorldTravelMap>();
        }
        private void Awake()
        {
            player = GetComponent<PlayerController>();
            settings = Resources.Load<TravelMapSettings>("UI/TravelMapSettings");
        }
        private void OnEnable()
        {
            CollectibleProgressService.Changed += MarkDirty;
            BossTravelProgress.Changed += MarkDirty;
            BindFlow();
            MarkDirty();
        }
        private void OnDisable()
        {
            CollectibleProgressService.Changed -= MarkDirty;
            BossTravelProgress.Changed -= MarkDirty;
            if (boundFlow != null) boundFlow.TransitionStarted -= OnTransitionStarted;
            boundFlow = null;
            Close();
            if (canvasRoot != null) Destroy(canvasRoot);
            canvasRoot = null;
        }
        private void MarkDirty() { dirty = true; unlockDirty = true; }
        private void BindFlow()
        {
            var flow = GameFlowController.Instance;
            if (ReferenceEquals(boundFlow, flow)) return;
            if (boundFlow != null) boundFlow.TransitionStarted -= OnTransitionStarted;
            boundFlow = flow;
            if (boundFlow != null) boundFlow.TransitionStarted += OnTransitionStarted;
        }
        // Release synchronously, before the menu captures Rigidbody state. An
        // Update-delayed release can make its temporary kinematic flag permanent.
        private void OnTransitionStarted(GameFlowSceneId target) => Close();
        private void OnApplicationPause(bool paused) { if (paused) Close(); }
        private bool IsGameplay()
        {
            var flow = GameFlowController.Instance;
            return flow == null || (!flow.IsTransitioning && flow.ActiveSceneId == GameFlowSceneId.Gaming);
        }
        public bool IsUnlocked
        {
            get
            {
                float required = settings != null ? settings.RequiredRatio : .05f;
                if (!unlockDirty && cachedRequiredRatio == required) return cachedUnlocked;
                BossTravelState boss = BossTravelProgress.GetState(gameObject.scene);
                var progress = CollectibleProgressService.GetWorldProgress();
                cachedUnlocked = boss.HasEnteredBoss && boss.HasDefeatedBoss && progress.MeetsRatio(required);
                cachedRequiredRatio = required; unlockDirty = false;
                return cachedUnlocked;
            }
        }
        public bool CanUseHere
        {
            get
            {
                rooms = rooms != null ? rooms : RoomLifecycleManager.ForScene(gameObject.scene);
                BossTravelState boss = BossTravelProgress.GetState(gameObject.scene);
                if (player == null || !player.isActiveAndEnabled || !IsUnlocked || boss.IsBossFightActive || rooms == null || BossTravelProgress.IsBossScene(gameObject.scene)) return false;
                string current = rooms.FindRegion(player.transform.position);
                return !string.IsNullOrEmpty(current) && current != boss.BossRegionId;
            }
        }
        private void Update()
        {
            BindFlow();
            if (player == null || settings == null || settings.Font == null) { Close(); return; }
            bool normal = player.isActiveAndEnabled && IsGameplay() && !player.IsDead && !player.IsRespawning && Time.timeScale > 0f;
            if ((opened || travel != null) && (!normal || !CanUseHere ||
                travel != null && (!cameraHandle.HasControl || !IsDestinationAllowed(travellingTo)))) Close();
            if (travel == null && GameInput.WasTriggeredThisFrame(InputActionId.WorldMap))
            {
                if (opened) Close(); else TryOpen();
            }
            if (opened && GameInput.WasTriggeredThisFrame(InputActionId.Cancel))
            {
                GameInput.ConsumeActionThisFrame(InputActionId.Pause);
                Close();
            }
            // A map entry also makes the feature reachable on touch devices.
            bool entryVisible = normal && !opened && travel == null && !player.IsControlLocked &&
                !GameplayPresentationGate.IsSuppressed && CanUseHere;
            if (entryVisible && canvasRoot == null) BuildUi();
            if (entry != null && entry.activeSelf != entryVisible) entry.SetActive(entryVisible);
            if (canvasRoot != null && lastSafeArea != Screen.safeArea) ApplySafeArea();
            if (opened)
            {
                if (lastViewportSize != viewport.rect.size) { lastViewportSize = viewport.rect.size; map.Fit(); }
                if (dirty) { RefreshProgress(); dirty = false; }
                playerMarker.anchoredPosition = map.Project(GridPoint(player.transform.position));
            }
        }
        public bool TryOpen()
        {
            BindFlow();
            if (opened || travel != null || player == null || !player.isActiveAndEnabled || player.IsDead || player.IsControlLocked ||
                !IsGameplay() || Time.timeScale <= 0f || GameplayPresentationGate.IsSuppressed || !CanUseHere ||
                settings == null || settings.Font == null) return false;
            BuildUi();
            opened = true;
            playerLock = player.AcquireTravelSafetyLock(this);
            // Only interaction is suppressed; visuals/world continue and no global timescale is changed.
            presentationLock = GameplayPresentationGate.Suppress();
            panel.SetActive(true); entry.SetActive(false);
            ApplySafeArea(); Canvas.ForceUpdateCanvases();
            BuildMapContent();
            previousCursorVisible = Cursor.visible; ownsCursor = true; DesktopCursorPolicy.Apply(true);
            return true;
        }
        public void Close()
        {
            opened = false;
            if (panel != null) panel.SetActive(false);
            // Every exit shares the same idempotent lease cleanup. Do not rely
            // on Unity disposing nested coroutine finally blocks on disable.
            Coroutine active = travel; travel = null; travellingTo = null;
            if (active != null) StopCoroutine(active);
            ReleaseLeases();
        }
        private void ReleaseLeases()
        {
            if (cameraHandle.IsValid) cameraHandle.Release(CameraTransition.Ease(.25f));
            cameraHandle = default;
            presentationLock?.Dispose(); presentationLock = null;
            playerLock?.Dispose(); playerLock = null;
            if (ownsCursor) { ownsCursor = false; DesktopCursorPolicy.Apply(previousCursorVisible); }
        }
        private Vector2 GridPoint(Vector3 position)
        {
            Vector3 p = rooms.transform.InverseTransformPoint(position) / rooms.CellSize;
            return new Vector2(p.x, p.y);
        }
        private bool IsDestinationAllowed(RespawnAnchorFeature point)
        {
            if (point == null || !point.IsRespawnPointAvailable || point.IsBossArenaRespawnPoint || point.gameObject.scene != gameObject.scene) return false;
            string region = rooms.FindRegion(point.transform.position);
            string bossRoom = BossTravelProgress.GetState(gameObject.scene).BossRegionId;
            return !string.IsNullOrEmpty(region) && region != bossRoom;
        }
        public bool TryTravel(RespawnAnchorFeature point)
        {
            if (!opened || travel != null || !CanUseHere || !IsDestinationAllowed(point)) return false;
            cameraManager = FindSceneCameraManager();
            if (cameraManager == null || cameraManager.gameObject.scene != gameObject.scene ||
                cameraManager.ActivePriority >= CameraControlPriorities.Cutscene)
            { status.text = "当前演出尚未结束，请稍后再试"; return false; }
            travellingTo = point;
            travel = StartCoroutine(Travel(point));
            return true;
        }
        private CameraControlManager FindSceneCameraManager()
        {
            CameraControlManager fallback = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var manager in root.GetComponentsInChildren<CameraControlManager>(true))
                {
                    if (!manager.isActiveAndEnabled || manager.OutputCamera == null || !manager.OutputCamera.isActiveAndEnabled) continue;
                    if (manager.OutputCamera.CompareTag("MainCamera")) return manager;
                    fallback ??= manager;
                }
            return fallback;
        }
        private IEnumerator Travel(RespawnAnchorFeature point)
        {
            travelState = cameraManager.CurrentState;
            CameraState source = travelState;
            cameraHandle = cameraManager.RequestControl(this, CameraControlPriorities.Cutscene,
                CameraInterruptionPolicy.AllowHigherPriority, CameraTransition.Immediate);
            panel.SetActive(false); opened = false;
            try
            {
                CameraState wide = source;
                wide.orthographicSize *= 1.4f;
                yield return AnimateCamera(source, wide, settings.ZoomOutSeconds);
                if (!CanUseHere || !IsDestinationAllowed(point) || !cameraHandle.HasControl) yield break;
                Vector3 delta = point.RespawnPosition - player.transform.position;
                CameraState arrival = wide; arrival.position += delta;
                yield return AnimateCamera(wide, arrival, settings.TravelSeconds);
                if (!CanUseHere || !IsDestinationAllowed(point) || !cameraHandle.HasControl) yield break;
                if (!player.TryTravelToCheckpoint(point))
                {
                    yield return AnimateCamera(arrival, source, settings.ZoomInSeconds);
                    yield break;
                }
                CameraState final = source; final.position += player.transform.position - (point.RespawnPosition - delta);
                yield return AnimateCamera(arrival, final, settings.ZoomInSeconds);
            }
            finally
            {
                // The gameplay source follows the relocated player; keep all camera writes in its manager.
                ReleaseLeases(); travel = null; travellingTo = null;
            }
        }
        private IEnumerator AnimateCamera(CameraState from, CameraState to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (!cameraHandle.HasControl || !CanUseHere || !IsDestinationAllowed(travellingTo)) yield break;
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                travelState = to;
                travelState.position = Vector3.Lerp(from.position, to.position, blend);
                travelState.orthographicSize = Mathf.Lerp(from.orthographicSize, to.orthographicSize, blend);
                yield return null;
            }
            travelState = to;
        }
        public bool TryGetCameraState(in CameraControlContext context, out CameraState state) { state = travelState; return isActiveAndEnabled; }
        public bool TryGetLensOverride(out float size, out float fov) { size = travelState.orthographicSize; fov = travelState.fieldOfView; return cameraHandle.IsValid; }

        private void BuildMapContent()
        {
            foreach (var label in labels) if (label.Rect != null) Destroy(label.Rect.gameObject);
            foreach (var marker in markers) if (marker.Rect != null) Destroy(marker.Rect.gameObject);
            labels.Clear(); markers.Clear(); checkpoints.Clear();
            map.Configure(rooms.Regions);
            foreach (RoomRegion room in rooms.Regions)
            {
                if (room == null || room.corridor || room.cells == null || room.cells.Count == 0) continue;
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
                foreach (var cell in room.cells) { min = Vector2.Min(min, new Vector2(cell.x, -cell.y - 1)); max = Vector2.Max(max, new Vector2(cell.x + 1, -cell.y)); }
                TMP_Text text = Text("房间收集进度", viewport, "", 17, TextAlignmentOptions.Center);
                text.rectTransform.sizeDelta = new Vector2(190, 55);
                labels.Add(new Label { Rect = text.rectTransform, Text = text, Region = room.id,
                    Name = string.IsNullOrWhiteSpace(room.label) ? "房间" : room.label, Position = (min + max) * .5f });
            }
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                checkpoints.AddRange(root.GetComponentsInChildren<RespawnAnchorFeature>(true));
            foreach (var point in checkpoints)
            {
                // Locked points are visible but never interactable. Boss points are not shown at all.
                if (point == null || point.IsBossArenaRespawnPoint) continue;
                string region = rooms.FindRegion(point.transform.position);
                if (string.IsNullOrEmpty(region) || region == BossTravelProgress.GetState(gameObject.scene).BossRegionId) continue;
                RespawnAnchorFeature selected = point;
                Button button = Button("重生点", viewport, point.IsActivated ? "◆" : "◇", () => TryTravel(selected));
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);
                button.interactable = IsDestinationAllowed(point);
                button.GetComponent<Image>().color = point.IsCurrent ? new Color(.15f, .6f, .8f) : new Color(.17f, .35f, .4f);
                markers.Add(new Label { Rect = button.GetComponent<RectTransform>(), Position = GridPoint(point.transform.position) });
            }
            playerMarker.SetAsLastSibling();
            RefreshProgress(); PositionLabels();
        }
        private void RefreshProgress()
        {
            var total = CollectibleProgressService.GetWorldProgress();
            summary.text = $"世界地图   ·   收集品 {total.Collected}/{total.Total}（{total.Ratio:P0}）";
            foreach (Label label in labels)
            {
                var p = CollectibleProgressService.GetRegionProgress(gameObject.scene, label.Region);
                label.Text.text = label.Name + "\n" + (p.Total == 0 ? "无收集品" : p.IsComplete ? "收集完成 ✓" : $"还剩 {p.Remaining} 个");
            }
            string hint = GameInput.PlatformMode == InputPlatformMode.Mobile
                ? "单指拖动 · 双指或 ± 缩放 · 点击已解锁重生点传送"
                : "拖动移动 · 滚轮或 ± 缩放 · 点击已解锁重生点传送 · T / Esc 关闭";
            if (!string.IsNullOrEmpty(selectedRegion))
            {
                var p = CollectibleProgressService.GetRegionProgress(gameObject.scene, selectedRegion);
                Label room = labels.Find(item => item.Region == selectedRegion);
                hint = (room != null ? room.Name : "房间") + " · " + (p.Total == 0 ? "无收集品" : p.IsComplete ? "收集完成" : $"还剩 {p.Remaining} 个") + "\n" + hint;
            }
            status.text = hint;
        }
        private void PositionLabels()
        {
            if (map == null || viewport == null) return;
            occupiedLabels.Clear(); Rect visible = viewport.rect;
            // Checkpoint buttons take priority over room captions. Move crowded
            // buttons only by a few UI pixels; never replace their world position.
            foreach (var item in markers)
            {
                Vector2 position = map.Project(item.Position);
                bool onScreen = visible.Contains(position); item.Rect.gameObject.SetActive(onScreen);
                if (!onScreen) continue;
                Vector2 chosen = new Vector2(Mathf.Clamp(position.x, visible.xMin + 24f, visible.xMax - 24f),
                    Mathf.Clamp(position.y, visible.yMin + 24f, visible.yMax - 24f));
                for (int attempt = 0; attempt < 17; attempt++)
                {
                    Vector2 candidate = chosen;
                    if (attempt > 0)
                    {
                        float angle = ((attempt - 1) % 8) * Mathf.PI / 4f;
                        candidate = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (attempt <= 8 ? 48f : 96f);
                    }
                    Rect bounds = new Rect(candidate - Vector2.one * 24f, Vector2.one * 48f);
                    if (visible.Contains(bounds.min) && visible.Contains(bounds.max) && !OverlapsLabel(bounds)) { chosen = candidate; break; }
                }
                item.Rect.anchoredPosition = chosen;
                occupiedLabels.Add(new Rect(chosen - Vector2.one * 24f, Vector2.one * 48f));
            }
            foreach (var item in labels)
            {
                Vector2 position = map.Project(item.Position);
                bool show = map.Scale >= .9f && visible.Contains(position);
                Rect bounds = new Rect(position - item.Rect.sizeDelta * .5f, item.Rect.sizeDelta);
                show &= !OverlapsLabel(bounds) && visible.Contains(bounds.min) && visible.Contains(bounds.max);
                item.Rect.anchoredPosition = position; item.Rect.gameObject.SetActive(show);
                if (show) occupiedLabels.Add(bounds);
            }
        }
        private bool OverlapsLabel(Rect bounds)
        { foreach (Rect occupied in occupiedLabels) if (occupied.Overlaps(bounds)) return true; return false; }
        private void SelectRoom(Vector2 position)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(position.x), -Mathf.FloorToInt(position.y) - 1);
            selectedRegion = null;
            foreach (var room in rooms.Regions)
                if (room != null && room.cells != null && room.cells.Contains(cell)) { selectedRegion = room.id; break; }
            RefreshProgress();
        }
        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        private void BuildUi()
        {
            if (canvasRoot != null) return;
            canvasRoot = new GameObject("WorldTravelMapUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasRoot, gameObject.scene);
            Canvas canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            safeArea = Rect("安全区域", canvasRoot.transform); Stretch(safeArea);
            entry = Button("地图入口", safeArea, "地图 T", () => TryOpen()).gameObject;
            if (GameInput.PlatformMode == InputPlatformMode.Mobile) entry.GetComponentInChildren<TMP_Text>().text = "地图";
            var entryRect = (RectTransform)entry.transform; entryRect.anchorMin = entryRect.anchorMax = new Vector2(.5f, 1f); entryRect.anchoredPosition = new Vector2(0f, -40f); entryRect.sizeDelta = new Vector2(120, 44);
            panel = Rect("地图面板", safeArea).gameObject; Stretch((RectTransform)panel.transform);
            panel.AddComponent<Image>().color = new Color(.035f, .055f, .075f, .97f);
            summary = Text("标题", panel.transform, "世界地图", 26, TextAlignmentOptions.Left);
            summary.rectTransform.anchorMin = new Vector2(0, 1); summary.rectTransform.anchorMax = Vector2.one;
            summary.rectTransform.offsetMin = new Vector2(24, -65); summary.rectTransform.offsetMax = new Vector2(-140, -15);
            summary.overflowMode = TextOverflowModes.Ellipsis;
            var close = Button("关闭", panel.transform, "关闭", Close);
            Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-65, -40), new Vector2(100, 44));
            viewport = Rect("地图视口", panel.transform); Stretch(viewport); viewport.offsetMin = new Vector2(24, 92); viewport.offsetMax = new Vector2(-24, -78);
            viewport.gameObject.AddComponent<RectMask2D>();
            map = viewport.gameObject.AddComponent<TravelMapGraphic>(); map.color = Color.white; map.ViewChanged += PositionLabels; map.PointClicked += SelectRoom;
            playerMarker = Text("玩家位置", viewport, "●", 24, TextAlignmentOptions.Center).rectTransform;
            playerMarker.GetComponent<TMP_Text>().color = new Color(1f, .85f, .34f); playerMarker.sizeDelta = new Vector2(28, 28);
            status = Text("操作提示", panel.transform, "", 17, TextAlignmentOptions.Left);
            status.rectTransform.anchorMin = Vector2.zero; status.rectTransform.anchorMax = new Vector2(1, 0);
            status.rectTransform.offsetMin = new Vector2(24, 10); status.rectTransform.offsetMax = new Vector2(-300, 68);
            status.overflowMode = TextOverflowModes.Ellipsis;
            var minus = Button("缩小", panel.transform, "−", () => map.Zoom(1 / 1.3f, Vector2.zero));
            var plus = Button("放大", panel.transform, "+", () => map.Zoom(1.3f, Vector2.zero));
            var fit = Button("总览", panel.transform, "总览", () => map.Fit());
            Place((RectTransform)minus.transform, new Vector2(1, 0), new Vector2(-230, 38), new Vector2(54, 46));
            Place((RectTransform)plus.transform, new Vector2(1, 0), new Vector2(-164, 38), new Vector2(54, 46));
            Place((RectTransform)fit.transform, new Vector2(1, 0), new Vector2(-70, 38), new Vector2(110, 46));
            ApplySafeArea(); panel.SetActive(false);
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return (RectTransform)obj.transform;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private TMP_Text Text(string name, Transform parent, string value, int size, TextAlignmentOptions alignment)
        {
            var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = settings.Font; text.text = value; text.fontSize = size; text.alignment = alignment; text.raycastTarget = false;
            text.color = new Color(.91f, .94f, .96f); text.enableWordWrapping = false;
            return text;
        }
        private Button Button(string name, Transform parent, string title, Action clicked)
        {
            RectTransform rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.16f, .23f, .29f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => clicked());
            TMP_Text label = Text("文字", rect, title, 20, TextAlignmentOptions.Center); Stretch(label.rectTransform);
            return button;
        }
    }
}
