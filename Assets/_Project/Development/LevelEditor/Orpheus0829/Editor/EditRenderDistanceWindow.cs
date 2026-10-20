using System;
using System.Collections.Generic;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Project.LevelEditor.Editor
{
    internal static class EditorAuthoringPerformanceState
    {
        private static double lastPlanningTickMs;
        private static double lastLevelEditorTickMs;
        private static double lastScenePreviewDrawMs;
        private static double lastScenePreviewPickMs;
        private static double lastFrameGapMs;
        private static double lastRoomCacheMs;
        private static double lastVisibilityApplyMs;
        private static double lastPlanningCanvasDrawMs;
        private static double lastPlanningCanvasWorldMs;
        private static double lastPlanningCanvasAssemblyMs;
        private static double lastPlanningCanvasDetailMs;
        private static double lastPlanningCanvasPreviewMs;
        private static double lastPlanningCanvasLabelsMs;
        private static double lastPlanningCanvasOverlayMs;

        public static bool ReducedRefreshEnabled
        {
            get;
            set;
        }

        public static double LastPlanningTickMs =>
            lastPlanningTickMs;
        public static double LastLevelEditorTickMs =>
            lastLevelEditorTickMs;
        public static double LastScenePreviewDrawMs =>
            lastScenePreviewDrawMs;
        public static double LastScenePreviewPickMs =>
            lastScenePreviewPickMs;
        public static double LastFrameGapMs =>
            lastFrameGapMs;
        public static double LastRoomCacheMs =>
            lastRoomCacheMs;
        public static double LastVisibilityApplyMs =>
            lastVisibilityApplyMs;
        public static double LastPlanningCanvasDrawMs =>
            lastPlanningCanvasDrawMs;
        public static double LastPlanningCanvasWorldMs =>
            lastPlanningCanvasWorldMs;
        public static double LastPlanningCanvasAssemblyMs =>
            lastPlanningCanvasAssemblyMs;
        public static double LastPlanningCanvasDetailMs =>
            lastPlanningCanvasDetailMs;
        public static double LastPlanningCanvasPreviewMs =>
            lastPlanningCanvasPreviewMs;
        public static double LastPlanningCanvasLabelsMs =>
            lastPlanningCanvasLabelsMs;
        public static double LastPlanningCanvasOverlayMs =>
            lastPlanningCanvasOverlayMs;

        public static void RecordPlanningTick(
            double milliseconds)
        {
            lastPlanningTickMs = milliseconds;
        }

        public static void RecordLevelEditorTick(
            double milliseconds)
        {
            lastLevelEditorTickMs = milliseconds;
        }

        public static void RecordScenePreviewDraw(
            double milliseconds)
        {
            lastScenePreviewDrawMs = milliseconds;
        }

        public static void RecordScenePreviewPick(
            double milliseconds)
        {
            lastScenePreviewPickMs = milliseconds;
        }

        public static void RecordFrameGap(
            double milliseconds)
        {
            lastFrameGapMs = milliseconds;
        }

        public static void RecordRoomCache(
            double milliseconds)
        {
            lastRoomCacheMs = milliseconds;
        }

        public static void RecordVisibilityApply(
            double milliseconds)
        {
            lastVisibilityApplyMs = milliseconds;
        }

        public static void RecordPlanningCanvasDraw(
            double milliseconds)
        {
            lastPlanningCanvasDrawMs = milliseconds;
        }

        public static void RecordPlanningCanvasWorld(
            double milliseconds)
        {
            lastPlanningCanvasWorldMs = milliseconds;
        }

        public static void RecordPlanningCanvasAssembly(
            double milliseconds)
        {
            lastPlanningCanvasAssemblyMs = milliseconds;
        }

        public static void RecordPlanningCanvasDetail(
            double milliseconds)
        {
            lastPlanningCanvasDetailMs = milliseconds;
        }

        public static void RecordPlanningCanvasPreview(
            double milliseconds)
        {
            lastPlanningCanvasPreviewMs = milliseconds;
        }

        public static void RecordPlanningCanvasLabels(
            double milliseconds)
        {
            lastPlanningCanvasLabelsMs = milliseconds;
        }

        public static void RecordPlanningCanvasOverlay(
            double milliseconds)
        {
            lastPlanningCanvasOverlayMs = milliseconds;
        }

        public static double ElapsedMilliseconds(
            long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() -
                    startTimestamp) *
                   1000d /
                   Stopwatch.Frequency;
        }
    }

    internal sealed class EditRenderDistanceWindow :
        EditorWindow
    {
        private const string MapRootName =
            "__PlanningMapGenerated";
        private const string RoomRootPrefix =
            "__PlanningRoom_";
        private const int DefaultRadiusCells = 80;
        private const int MinimumRadiusCells = 10;
        private const int MaximumRadiusCells = 400;
        private const double RefreshInterval = .15d;

        private sealed class RoomEntry
        {
            public GameObject Root;
            public Bounds Bounds;
            public Bounds[] Samples;
        }

        private static readonly string PreferencePrefix =
            "2026TapTap.EditRenderDistance." +
            Hash128.Compute(Application.dataPath);
        private static EditRenderDistanceWindow active;

        private readonly List<RoomEntry> rooms =
            new List<RoomEntry>();
        private readonly HashSet<GameObject> hiddenByTool =
            new HashSet<GameObject>();
        private readonly Queue<GameObject> pendingShow =
            new Queue<GameObject>();
        private readonly Queue<GameObject> pendingHide =
            new Queue<GameObject>();
        private readonly HashSet<GameObject> pendingShowSet =
            new HashSet<GameObject>();
        private readonly HashSet<GameObject> pendingHideSet =
            new HashSet<GameObject>();
        private Toggle enabledToggle;
        private Toggle reducedRefreshToggle;
        private SliderInt radiusSlider;
        private Label radiusValueLabel;
        private Label statusLabel;
        private GameObject currentMapRoot;
        private bool enabled;
        private bool reducePlanningRefresh = true;
        private bool cacheDirty = true;
        private bool applyPending;
        private bool built;
        private int radiusCells = DefaultRadiusCells;
        private int cachedSceneHandle;
        private double nextRefreshTime;
        private double nextDiagnosticLogTime;
        private double nextStatusRefreshTime;
        private double lastEditorUpdateTime;
        private Vector3 lastPivot = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);

        private static float CellSize =>
            Mathf.Max(.05f, LevelEditorState.CellSize);

        [MenuItem(
            "Tools/2026TapTap/编辑器性能/编辑渲染距离")]
        private static void Open()
        {
            EditRenderDistanceWindow window =
                GetWindow<
                    EditRenderDistanceWindow>();
            window.titleContent =
                new GUIContent("编辑渲染距离");
            window.minSize =
                new Vector2(380f, 250f);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void RegisterFailsafe()
        {
            AssemblyReloadEvents.beforeAssemblyReload -=
                RestoreBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload +=
                RestoreBeforeReload;
            EditorApplication.quitting -=
                RestoreBeforeQuit;
            EditorApplication.quitting +=
                RestoreBeforeQuit;
        }

        private static void RestoreBeforeReload()
        {
            active?.RestoreVisibility();
        }

        private static void RestoreBeforeQuit()
        {
            active?.RestoreVisibility();
        }

        private void OnEnable()
        {
            active = this;
            enabled = EditorPrefs.GetBool(
                PreferencePrefix + ".Enabled",
                false);
            radiusCells = Mathf.Clamp(
                EditorPrefs.GetInt(
                    PreferencePrefix + ".Radius",
                    DefaultRadiusCells),
                MinimumRadiusCells,
                MaximumRadiusCells);
            reducePlanningRefresh =
                EditorPrefs.GetBool(
                    PreferencePrefix +
                    ".ReducePlanningRefresh",
                    true);
            BuildIfNeeded();
            EditorApplication.update += Tick;
            EditorApplication.hierarchyChanged +=
                MarkCacheDirty;
            EditorApplication.playModeStateChanged +=
                HandlePlayModeChanged;
            EditorSceneManager.sceneSaving +=
                HandleSceneSaving;
            EditorSceneManager.sceneOpened +=
                HandleSceneOpened;
            Selection.selectionChanged +=
                MarkApplyPending;
            cacheDirty = true;
            applyPending = enabled;
            RefreshControls();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.hierarchyChanged -=
                MarkCacheDirty;
            EditorApplication.playModeStateChanged -=
                HandlePlayModeChanged;
            EditorSceneManager.sceneSaving -=
                HandleSceneSaving;
            EditorSceneManager.sceneOpened -=
                HandleSceneOpened;
            Selection.selectionChanged -=
                MarkApplyPending;
            SavePreferences();
            RestoreVisibility();
            EditorAuthoringPerformanceState
                .ReducedRefreshEnabled = false;
            if (active == this)
            {
                active = null;
            }
        }

        private void BuildIfNeeded()
        {
            if (built)
            {
                return;
            }
            built = true;
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);

            var title = new Label("编辑渲染距离");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            title.style.marginBottom = 8f;
            scroll.Add(title);

            enabledToggle = new Toggle(
                "启用编辑渲染距离")
            {
                value = enabled
            };
            enabledToggle.style.marginBottom = 8f;
            enabledToggle.RegisterValueChangedCallback(
                HandleEnabledChanged);
            scroll.Add(enabledToggle);

            reducedRefreshToggle = new Toggle(
                "同时降低规划窗口刷新频率")
            {
                value = reducePlanningRefresh
            };
            reducedRefreshToggle.style.marginBottom = 8f;
            reducedRefreshToggle.RegisterValueChangedCallback(
                HandleReducedRefreshChanged);
            scroll.Add(reducedRefreshToggle);

            var radiusRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };
            scroll.Add(radiusRow);

            radiusSlider = new SliderInt(
                "可见半径（格）",
                MinimumRadiusCells,
                MaximumRadiusCells)
            {
                value = radiusCells
            };
            radiusSlider.style.flexGrow = 1f;
            radiusSlider.style.minWidth = 220f;
            radiusSlider.RegisterValueChangedCallback(
                HandleRadiusChanged);
            radiusRow.Add(radiusSlider);

            radiusValueLabel = new Label(
                radiusCells + " 格");
            radiusValueLabel.style.minWidth = 54f;
            radiusValueLabel.style.marginLeft = 6f;
            radiusValueLabel.style.unityTextAlign =
                TextAnchor.MiddleRight;
            radiusRow.Add(radiusValueLabel);

            var buttons = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 2f,
                    marginBottom = 10f
                }
            };
            scroll.Add(buttons);

            var refreshButton = new Button(
                RefreshNow)
            {
                text = "刷新房间缓存"
            };
            refreshButton.style.height = 28f;
            refreshButton.style.minWidth = 120f;
            refreshButton.style.marginRight = 6f;
            refreshButton.style.marginBottom = 6f;
            buttons.Add(refreshButton);

            var closeButton = new Button(
                DisableAndRestore)
            {
                text = "关闭并恢复全部"
            };
            closeButton.style.height = 28f;
            closeButton.style.minWidth = 130f;
            closeButton.style.marginBottom = 6f;
            buttons.Add(closeButton);

            var separator = new VisualElement();
            separator.style.height = 1f;
            separator.style.backgroundColor =
                new Color(.24f, .24f, .24f);
            separator.style.marginBottom = 10f;
            scroll.Add(separator);

            statusLabel = new Label();
            statusLabel.style.whiteSpace =
                WhiteSpace.Normal;
            statusLabel.style.color =
                new Color(.68f, .76f, .84f);
            scroll.Add(statusLabel);

            RefreshStatus();
            SyncReducedRefreshState();
        }

        private void HandleEnabledChanged(
            ChangeEvent<bool> change)
        {
            enabled = change.newValue;
            SavePreferences();
            if (enabled)
            {
                cacheDirty = true;
                applyPending = true;
            }
            else
            {
                RestoreVisibility();
            }
            SyncReducedRefreshState();
            RefreshStatus();
        }

        private void HandleReducedRefreshChanged(
            ChangeEvent<bool> change)
        {
            reducePlanningRefresh =
                change.newValue;
            SavePreferences();
            SyncReducedRefreshState();
            RefreshStatus();
        }

        private void HandleRadiusChanged(
            ChangeEvent<int> change)
        {
            radiusCells = Mathf.Clamp(
                change.newValue,
                MinimumRadiusCells,
                MaximumRadiusCells);
            if (radiusValueLabel != null)
            {
                radiusValueLabel.text =
                    radiusCells + " 格";
            }
            SavePreferences();
            applyPending = enabled;
            RefreshStatus();
        }

        private void Tick()
        {
            double now =
                EditorApplication.timeSinceStartup;
            if (lastEditorUpdateTime > 0d)
            {
                EditorAuthoringPerformanceState
                    .RecordFrameGap(
                        (now - lastEditorUpdateTime) *
                        1000d);
            }
            lastEditorUpdateTime = now;
            if (!enabled ||
                EditorApplication
                    .isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorAuthoringPerformanceState
                    .LastFrameGapMs > 60d &&
                now >= nextDiagnosticLogTime)
            {
                nextDiagnosticLogTime = now + 2d;
                Debug.LogWarning(
                    "[EditPerf] 更新间隔 " +
                    EditorAuthoringPerformanceState
                        .LastFrameGapMs
                        .ToString("0.0") +
                    " ms | 规划 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningTickMs
                        .ToString("0.0") +
                    " ms | 关卡 " +
                    EditorAuthoringPerformanceState
                        .LastLevelEditorTickMs
                        .ToString("0.0") +
                    " ms | 预览绘制 " +
                    EditorAuthoringPerformanceState
                        .LastScenePreviewDrawMs
                        .ToString("0.0") +
                    " ms | 预览拾取 " +
                    EditorAuthoringPerformanceState
                        .LastScenePreviewPickMs
                        .ToString("0.0") +
                    " ms | 房间缓存 " +
                    EditorAuthoringPerformanceState
                        .LastRoomCacheMs
                        .ToString("0.0") +
                    " ms | 可见性应用 " +
                    EditorAuthoringPerformanceState
                        .LastVisibilityApplyMs
                        .ToString("0.0") +
                    " ms | 画布 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasDrawMs
                        .ToString("0.0") +
                    " ms（世界 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasWorldMs
                        .ToString("0.0") +
                    " / 装配 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasAssemblyMs
                        .ToString("0.0") +
                    " / 详情 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasDetailMs
                        .ToString("0.0") +
                    "） | 预览刷新 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasPreviewMs
                        .ToString("0.0") +
                    " ms | 区域标签 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasLabelsMs
                        .ToString("0.0") +
                    " ms | 玩家标记 " +
                    EditorAuthoringPerformanceState
                        .LastPlanningCanvasOverlayMs
                        .ToString("0.0") +
                    " ms");
            }
            if (now >= nextStatusRefreshTime)
            {
                nextStatusRefreshTime = now + .5d;
                RefreshStatus();
            }
            if (now < nextRefreshTime)
            {
                return;
            }
            nextRefreshTime = now + RefreshInterval;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                RestoreVisibility();
                RefreshStatus("当前没有可编辑场景。");
                return;
            }

            SceneView sceneView =
                SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                RefreshStatus("请打开 Scene 视图。");
                return;
            }

            if (cacheDirty ||
                cachedSceneHandle != scene.handle)
            {
                RebuildRoomCache(scene);
                cacheDirty = false;
                applyPending = true;
            }

            Vector3 pivot = sceneView.pivot;
            if (applyPending ||
                (pivot - lastPivot).sqrMagnitude >
                CellSize * CellSize * .04f)
            {
                ApplyVisibility(pivot);
                lastPivot = pivot;
                applyPending = false;
            }
            ProcessVisibilityQueue();
        }

        private void RebuildRoomCache(Scene scene)
        {
            long rebuildStart =
                Stopwatch.GetTimestamp();
            if (cachedSceneHandle != 0 &&
                cachedSceneHandle != scene.handle)
            {
                RestoreVisibility();
            }
            rooms.Clear();
            currentMapRoot = FindMapRoot(scene);
            cachedSceneHandle = scene.handle;
            if (currentMapRoot == null)
            {
                RestoreVisibility();
                EditorAuthoringPerformanceState
                    .RecordRoomCache(
                        EditorAuthoringPerformanceState
                            .ElapsedMilliseconds(
                                rebuildStart));
                RefreshStatus(
                    "未找到 " +
                    MapRootName +
                    "，当前场景不做裁剪。");
                return;
            }

            Transform root = currentMapRoot.transform;
            for (int index = 0;
                 index < root.childCount;
                 index++)
            {
                Transform child = root.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                CalculateBounds(
                    child,
                    out Bounds bounds,
                    out Bounds[] samples);
                rooms.Add(new RoomEntry
                {
                    Root = child.gameObject,
                    Bounds = bounds,
                    Samples = samples
                });
            }

            if (rooms.Count == 0)
            {
                RestoreVisibility();
                EditorAuthoringPerformanceState
                    .RecordRoomCache(
                        EditorAuthoringPerformanceState
                            .ElapsedMilliseconds(
                                rebuildStart));
                RefreshStatus(
                    "规划根节点下没有房间容器。");
            }
            EditorAuthoringPerformanceState
                .RecordRoomCache(
                    EditorAuthoringPerformanceState
                        .ElapsedMilliseconds(
                            rebuildStart));
        }

        private void ApplyVisibility(Vector3 pivot)
        {
            if (currentMapRoot == null ||
                rooms.Count == 0)
            {
                return;
            }

            long applyStart =
                Stopwatch.GetTimestamp();
            SceneVisibilityManager visibility =
                SceneVisibilityManager.instance;
            RoomEntry current = FindCurrentRoom(pivot);
            GameObject selectedRoot =
                FindSelectedRoomRoot();
            float radius = radiusCells * CellSize;
            float radiusSquared = radius * radius;
            int visibleCount = 0;
            bool changed = false;
            for (int index = 0;
                 index < rooms.Count;
                 index++)
            {
                RoomEntry room = rooms[index];
                if (room.Root == null)
                {
                    continue;
                }

                bool visible =
                    room == current ||
                    room.Root == selectedRoot ||
                    IsWithinRadius(
                        room,
                        pivot,
                        radiusSquared);
                if (visible)
                {
                    visibleCount++;
                    CancelQueuedHide(room.Root);
                    if (hiddenByTool.Contains(room.Root))
                    {
                        QueueShow(room.Root);
                        changed = true;
                    }
                    continue;
                }

                CancelQueuedShow(room.Root);
                if (hiddenByTool.Contains(room.Root) ||
                    pendingHideSet.Contains(room.Root) ||
                    visibility.IsHidden(
                        room.Root,
                        true))
                {
                    continue;
                }

                QueueHide(room.Root);
                changed = true;
            }

            if (changed)
            {
                SceneView.RepaintAll();
            }
            RefreshStatus(
                null,
                visibleCount);
            EditorAuthoringPerformanceState
                .RecordVisibilityApply(
                    EditorAuthoringPerformanceState
                        .ElapsedMilliseconds(
                            applyStart));
        }

        private RoomEntry FindCurrentRoom(
            Vector3 pivot)
        {
            RoomEntry containing = null;
            float containingVolume =
                float.PositiveInfinity;
            RoomEntry nearest = null;
            float nearestDistance =
                float.PositiveInfinity;

            for (int index = 0;
                 index < rooms.Count;
                 index++)
            {
                RoomEntry room = rooms[index];
                float distance =
                    room.Bounds.SqrDistance(pivot);
                if (distance <= .0001f)
                {
                    Vector3 size = room.Bounds.size;
                    float volume =
                        size.x * size.y * size.z;
                    if (volume < containingVolume)
                    {
                        containing = room;
                        containingVolume = volume;
                    }
                }
                if (distance < nearestDistance)
                {
                    nearest = room;
                    nearestDistance = distance;
                }
            }

            return containing ?? nearest;
        }

        private GameObject FindSelectedRoomRoot()
        {
            Transform selected = Selection.activeTransform;
            if (selected == null ||
                currentMapRoot == null)
            {
                return null;
            }

            Transform parent =
                currentMapRoot.transform;
            while (selected != null &&
                   selected.parent != parent)
            {
                selected = selected.parent;
            }
            return selected != null
                ? selected.gameObject
                : null;
        }

        private static bool IsWithinRadius(
            RoomEntry room,
            Vector3 pivot,
            float radiusSquared)
        {
            if (room.Samples == null ||
                room.Samples.Length == 0)
            {
                return room.Bounds.SqrDistance(pivot) <=
                       radiusSquared;
            }

            for (int index = 0;
                 index < room.Samples.Length;
                 index++)
            {
                if (room.Samples[index].SqrDistance(pivot) <=
                    radiusSquared)
                {
                    return true;
                }
            }
            return false;
        }

        private static void CalculateBounds(
            Transform root,
            out Bounds bounds,
            out Bounds[] samples)
        {
            bool found = false;
            bounds = default;
            var blockSamples = new List<Bounds>();

            LevelEditorPlacedBlock[] blocks =
                root.GetComponentsInChildren<
                    LevelEditorPlacedBlock>(true);
            for (int index = 0;
                 index < blocks.Length;
                 index++)
            {
                LevelEditorPlacedBlock block =
                    blocks[index];
                if (block == null)
                {
                    continue;
                }
                float cell = block.CellWorldSize;
                Vector2Int cells = block.SizeCells;
                Vector3 scale =
                    block.transform.lossyScale;
                Vector3 size = new Vector3(
                    Mathf.Abs(cells.x * cell * scale.x),
                    Mathf.Abs(cells.y * cell * scale.y),
                    Mathf.Max(
                        Mathf.Abs(cell * scale.z),
                        CellSize));
                Bounds blockBounds = new Bounds(
                    block.transform.position,
                    size);
                blockSamples.Add(blockBounds);
                if (!found)
                {
                    bounds = blockBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        blockBounds);
                }
            }
            if (found)
            {
                bounds.Expand(CellSize * 2f);
                samples = blockSamples.ToArray();
                return;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<
                    Renderer>(true);
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            bounds = found
                ? bounds
                : new Bounds(
                    root.position,
                    Vector3.one * CellSize);
            samples = found
                ? new[] { bounds }
                : Array.Empty<Bounds>();
        }

        private static GameObject FindMapRoot(
            Scene scene)
        {
            GameObject[] roots =
                scene.GetRootGameObjects();
            for (int index = 0;
                 index < roots.Length;
                 index++)
            {
                GameObject root = roots[index];
                if (root != null &&
                    root.name == MapRootName)
                {
                    return root;
                }
            }
            return null;
        }

        private void RestoreVisibility()
        {
            pendingShow.Clear();
            pendingHide.Clear();
            pendingShowSet.Clear();
            pendingHideSet.Clear();
            if (hiddenByTool.Count == 0)
            {
                return;
            }

            SceneVisibilityManager visibility =
                SceneVisibilityManager.instance;
            foreach (GameObject root in hiddenByTool)
            {
                if (root != null)
                {
                    visibility.Show(root, true);
                }
            }
            hiddenByTool.Clear();
            SceneView.RepaintAll();
        }

        private void QueueShow(GameObject root)
        {
            if (root == null ||
                !pendingShowSet.Add(root))
            {
                return;
            }
            pendingShow.Enqueue(root);
        }

        private void QueueHide(GameObject root)
        {
            if (root == null ||
                !pendingHideSet.Add(root))
            {
                return;
            }
            pendingHide.Enqueue(root);
        }

        private void CancelQueuedShow(GameObject root)
        {
            pendingShowSet.Remove(root);
        }

        private void CancelQueuedHide(GameObject root)
        {
            pendingHideSet.Remove(root);
        }

        private void ProcessVisibilityQueue()
        {
            if (pendingShow.Count == 0 &&
                pendingHide.Count == 0)
            {
                return;
            }

            SceneVisibilityManager visibility =
                SceneVisibilityManager.instance;
            bool changed = false;
            for (int count = 0;
                 count < 1;
                 count++)
            {
                bool processed = false;
                while (pendingShow.Count > 0)
                {
                    GameObject root =
                        pendingShow.Dequeue();
                    if (!pendingShowSet.Remove(root))
                    {
                        continue;
                    }
                    if (root != null)
                    {
                        visibility.Show(root, true);
                        hiddenByTool.Remove(root);
                        changed = true;
                    }
                    processed = true;
                    break;
                }
                if (processed)
                {
                    continue;
                }

                while (pendingHide.Count > 0)
                {
                    GameObject root =
                        pendingHide.Dequeue();
                    if (!pendingHideSet.Remove(root))
                    {
                        continue;
                    }
                    if (root != null &&
                        !hiddenByTool.Contains(root))
                    {
                        visibility.Hide(root, true);
                        hiddenByTool.Add(root);
                        changed = true;
                    }
                    processed = true;
                    break;
                }
            }
            if (changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void DisableAndRestore()
        {
            enabled = false;
            enabledToggle?.SetValueWithoutNotify(false);
            SavePreferences();
            RestoreVisibility();
            RefreshStatus();
        }

        private void RefreshNow()
        {
            cacheDirty = true;
            applyPending = enabled;
            nextRefreshTime = 0d;
            if (!enabled)
            {
                RestoreVisibility();
            }
            RefreshStatus();
        }

        private void MarkCacheDirty()
        {
            cacheDirty = true;
            applyPending = enabled;
        }

        private void MarkApplyPending()
        {
            applyPending = enabled;
        }

        private void HandlePlayModeChanged(
            PlayModeStateChange state)
        {
            if (state ==
                    PlayModeStateChange.ExitingEditMode ||
                state ==
                    PlayModeStateChange.EnteredPlayMode)
            {
                RestoreVisibility();
                EditorAuthoringPerformanceState
                    .ReducedRefreshEnabled = false;
                return;
            }
            if (state ==
                PlayModeStateChange.EnteredEditMode)
            {
                cacheDirty = true;
                applyPending = enabled;
                nextRefreshTime = 0d;
                SyncReducedRefreshState();
            }
        }

        private void HandleSceneSaving(
            Scene scene,
            string path)
        {
            RestoreVisibility();
            if (enabled)
            {
                EditorApplication.delayCall += () =>
                {
                    cacheDirty = true;
                    applyPending = true;
                };
            }
        }

        private void HandleSceneOpened(
            Scene scene,
            OpenSceneMode mode)
        {
            cacheDirty = true;
            applyPending = enabled;
        }

        private void SavePreferences()
        {
            EditorPrefs.SetBool(
                PreferencePrefix + ".Enabled",
                enabled);
            EditorPrefs.SetInt(
                PreferencePrefix + ".Radius",
                radiusCells);
            EditorPrefs.SetBool(
                PreferencePrefix +
                ".ReducePlanningRefresh",
                reducePlanningRefresh);
        }

        private void RefreshControls()
        {
            enabledToggle?.SetValueWithoutNotify(
                enabled);
            reducedRefreshToggle?
                .SetValueWithoutNotify(
                    reducePlanningRefresh);
            radiusSlider?.SetValueWithoutNotify(
                radiusCells);
            if (radiusValueLabel != null)
            {
                radiusValueLabel.text =
                    radiusCells + " 格";
            }
            RefreshStatus();
        }

        private void SyncReducedRefreshState()
        {
            EditorAuthoringPerformanceState
                .ReducedRefreshEnabled =
                enabled &&
                reducePlanningRefresh &&
                !EditorApplication
                    .isPlayingOrWillChangePlaymode;
        }

        private void RefreshStatus(
            string overrideText = null,
            int visibleCount = -1)
        {
            if (statusLabel == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(overrideText))
            {
                statusLabel.text =
                    overrideText;
                return;
            }
            if (!enabled)
            {
                statusLabel.text =
                    "未启用。全部规划房间保持可见。";
                return;
            }
            if (rooms.Count == 0)
            {
                statusLabel.text =
                    "等待扫描规划房间。";
                return;
            }

            int visible = visibleCount >= 0
                ? visibleCount
                : Mathf.Max(
                    0,
                    rooms.Count -
                    hiddenByTool.Count);
            statusLabel.text =
                "半径：" +
                radiusCells +
                " 格   可见房间：" +
                visible +
                " / " +
                rooms.Count +
                "   隐藏房间：" +
                (rooms.Count - visible) +
                "   规划刷新：" +
                (reducePlanningRefresh
                    ? "轻量"
                    : "标准") +
                "\n诊断：更新间隔 " +
                EditorAuthoringPerformanceState
                    .LastFrameGapMs
                    .ToString("0.0") +
                "ms / 规划 " +
                EditorAuthoringPerformanceState
                    .LastPlanningTickMs
                    .ToString("0.0") +
                "ms / 关卡 " +
                EditorAuthoringPerformanceState
                    .LastLevelEditorTickMs
                    .ToString("0.0") +
                "ms / 预览 " +
                EditorAuthoringPerformanceState
                    .LastScenePreviewDrawMs
                    .ToString("0.0") +
                "ms / 缓存 " +
                EditorAuthoringPerformanceState
                    .LastRoomCacheMs
                    .ToString("0.0") +
                "ms / 应用 " +
                EditorAuthoringPerformanceState
                    .LastVisibilityApplyMs
                    .ToString("0.0") +
                "ms\n画布：" +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasDrawMs
                    .ToString("0.0") +
                "ms（世界 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasWorldMs
                    .ToString("0.0") +
                " / 装配 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasAssemblyMs
                    .ToString("0.0") +
                " / 详情 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasDetailMs
                    .ToString("0.0") +
                "） / 预览 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasPreviewMs
                    .ToString("0.0") +
                "ms / 标签 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasLabelsMs
                    .ToString("0.0") +
                "ms / 玩家 " +
                EditorAuthoringPerformanceState
                    .LastPlanningCanvasOverlayMs
                    .ToString("0.0") +
                "ms";
        }
    }
}
