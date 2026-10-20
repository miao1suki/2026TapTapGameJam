using System;
using System.Collections.Generic;
using System.Linq;
using Project;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Items;
using Project.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    internal static class ColorDebugTools
    {
        [MenuItem("Tools/2026TapTap/调试/颜色与收藏品调试")]
        private static void OpenWindow()
        {
            ColorDebugToolsWindow window =
                EditorWindow.GetWindow<
                    ColorDebugToolsWindow>();
            window.titleContent =
                new GUIContent("颜色与收藏品调试");
            window.minSize =
                new Vector2(480f, 700f);
            window.Show();
        }

        internal static PlayerController
            FindPlayerInActiveScene()
        {
            Scene activeScene =
                SceneManager.GetActiveScene();
            var players =
                ProjectDiscovery.FindAll<
                    PlayerController>();
            for (int index = 0;
                 index < players.Count;
                 index++)
            {
                PlayerController player = players[index];
                if (player != null &&
                    player.gameObject.scene == activeScene)
                {
                    return player;
                }
            }
            return null;
        }

        internal static bool UnlockAllPlayerAbilities(
            out string status)
        {
            if (!EditorApplication.isPlaying)
            {
                status = "请进入 Play Mode 后使用。";
                return false;
            }

            PlayerController player =
                FindPlayerInActiveScene();
            if (player == null)
            {
                status = "当前场景没有可用的标准玩家。";
                return false;
            }

            ColorRuntimeService service =
                ColorRuntimeService.Instance;
            service.SetAllUnlockedForCurrentSession(true);
            service.UnlockAllAbilitiesForCurrentSession();
            status =
                "已解锁三色场景颜色和三种玩家能力。";
            Debug.Log(
                "[ColorDebugTools] " +
                status +
                " 玩家：" +
                player.name,
                player);
            return true;
        }
    }

    internal sealed class ColorDebugToolsWindow :
        EditorWindow
    {
        private Label playModeLabel;
        private Label playerLabel;
        private Label debugMapSummaryLabel;
        private Label debugMapStatusLabel;
        private Label collectibleLabel;
        private IntegerField targetCountField;
        private Slider debugMapZoomSlider;
        private Label debugMapZoomLabel;
        private Label statusLabel;
        private Button unlockAbilitiesButton;
        private Button applyCountButton;
        private Button zeroButton;
        private Button maxButton;
        private Button refreshButton;
        private Button refreshMapButton;
        private ScrollView debugMapScroll;
        private VisualElement debugMapContent;
        private double nextRefreshTime;
        private bool built;

        private void OnEnable()
        {
            BuildIfNeeded();
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged +=
                HandlePlayModeChanged;
            Refresh();
            RefreshDebugMap();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -=
                HandlePlayModeChanged;
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

            var title = new Label(
                "颜色与收藏品调试");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            title.style.marginBottom = 8f;
            scroll.Add(title);

            playModeLabel = new Label();
            playModeLabel.style.marginBottom = 4f;
            scroll.Add(playModeLabel);

            playerLabel = new Label();
            playerLabel.style.marginBottom = 10f;
            scroll.Add(playerLabel);

            unlockAbilitiesButton = new Button(
                UnlockAbilities)
            {
                text = "解锁玩家六把钥匙能力"
            };
            unlockAbilitiesButton.style.height = 30f;
            unlockAbilitiesButton.style.marginBottom = 12f;
            scroll.Add(unlockAbilitiesButton);

            var debugMapTitle = new Label(
                "重生点与传送方块地图");
            debugMapTitle.style.fontSize = 14f;
            debugMapTitle.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            debugMapTitle.style.marginTop = 4f;
            debugMapTitle.style.marginBottom = 6f;
            scroll.Add(debugMapTitle);

            debugMapSummaryLabel = new Label();
            debugMapSummaryLabel.style.marginBottom = 6f;
            scroll.Add(debugMapSummaryLabel);

            var mapControls = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 8f
                }
            };
            scroll.Add(mapControls);

            debugMapZoomSlider = new Slider(
                "地图缩放",
                1f,
                4f)
            {
                value = 1.25f
            };
            debugMapZoomSlider.style.flexGrow = 1f;
            debugMapZoomSlider.style.minWidth = 220f;
            debugMapZoomSlider.RegisterValueChangedCallback(
                HandleDebugMapZoomChanged);
            mapControls.Add(debugMapZoomSlider);

            debugMapZoomLabel = new Label(
                "1.25×");
            debugMapZoomLabel.style.minWidth = 52f;
            debugMapZoomLabel.style.unityTextAlign =
                TextAnchor.MiddleRight;
            debugMapZoomLabel.style.marginLeft = 6f;
            mapControls.Add(debugMapZoomLabel);

            refreshMapButton = AddButton(
                mapControls,
                "刷新地图",
                RefreshDebugMap);
            var resetZoomButton = AddButton(
                mapControls,
                "重置缩放",
                ResetDebugMapZoom);
            resetZoomButton.style.marginLeft = 6f;

            debugMapScroll = new ScrollView(
                ScrollViewMode.VerticalAndHorizontal);
            debugMapScroll.style.height = 280f;
            debugMapScroll.style.backgroundColor =
                new Color(.10f, .11f, .12f);
            debugMapScroll.style.borderTopWidth = 1f;
            debugMapScroll.style.borderBottomWidth = 1f;
            debugMapScroll.style.borderLeftWidth = 1f;
            debugMapScroll.style.borderRightWidth = 1f;
            debugMapScroll.style.borderTopColor =
                new Color(.28f, .30f, .32f);
            debugMapScroll.style.borderBottomColor =
                new Color(.28f, .30f, .32f);
            debugMapScroll.style.borderLeftColor =
                new Color(.28f, .30f, .32f);
            debugMapScroll.style.borderRightColor =
                new Color(.28f, .30f, .32f);
            debugMapScroll.style.marginBottom = 6f;
            scroll.Add(debugMapScroll);

            debugMapContent = new VisualElement();
            debugMapContent.style.position =
                Position.Relative;
            debugMapScroll.Add(debugMapContent);

            debugMapStatusLabel = new Label();
            debugMapStatusLabel.style.whiteSpace =
                WhiteSpace.Normal;
            debugMapStatusLabel.style.marginBottom = 10f;
            debugMapStatusLabel.style.color =
                new Color(.65f, .75f, .82f);
            scroll.Add(debugMapStatusLabel);

            scroll.Add(new VisualElement
            {
                style =
                {
                    height = 1f,
                    backgroundColor =
                        new Color(.24f, .24f, .24f),
                    marginBottom = 12f
                }
            });

            var collectibleTitle = new Label(
                "当前运行收集品");
            collectibleTitle.style.fontSize = 14f;
            collectibleTitle.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            collectibleTitle.style.marginBottom = 6f;
            scroll.Add(collectibleTitle);

            collectibleLabel = new Label();
            collectibleLabel.style.whiteSpace =
                WhiteSpace.Normal;
            collectibleLabel.style.marginBottom = 10f;
            scroll.Add(collectibleLabel);

            targetCountField = new IntegerField(
                "目标已获得数量")
            {
                value = 0
            };
            targetCountField.style.marginBottom = 8f;
            scroll.Add(targetCountField);

            var countButtons = new VisualElement
            {
                style =
                {
                    flexDirection =
                        FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 10f
                }
            };
            scroll.Add(countButtons);

            applyCountButton = AddButton(
                countButtons,
                "应用数量",
                ApplyTargetCount);
            zeroButton = AddButton(
                countButtons,
                "设为 0",
                () => SetTargetCount(0));
            maxButton = AddButton(
                countButtons,
                "设为最大",
                SetTargetToMaximum);
            refreshButton = AddButton(
                countButtons,
                "刷新",
                Refresh);

            statusLabel = new Label();
            statusLabel.style.whiteSpace =
                WhiteSpace.Normal;
            statusLabel.style.marginTop = 6f;
            statusLabel.style.color =
                new Color(.65f, .75f, .82f);
            scroll.Add(statusLabel);
        }

        private static Button AddButton(
            VisualElement parent,
            string text,
            Action action)
        {
            var button = new Button(action)
            {
                text = text
            };
            button.style.marginRight = 6f;
            button.style.marginBottom = 6f;
            button.style.minWidth = 82f;
            button.style.height = 28f;
            parent.Add(button);
            return button;
        }

        private void Tick()
        {
            if (!EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup <
                nextRefreshTime)
            {
                return;
            }
            nextRefreshTime =
                EditorApplication.timeSinceStartup +
                .5d;
            Refresh();
        }

        private void HandlePlayModeChanged(
            PlayModeStateChange change)
        {
            nextRefreshTime = 0d;
            Refresh();
            RefreshDebugMap();
        }

        private void HandleDebugMapZoomChanged(
            ChangeEvent<float> change)
        {
            debugMapZoomLabel.text =
                change.newValue.ToString("0.##") +
                "×";
            RefreshDebugMap();
        }

        private void ResetDebugMapZoom()
        {
            debugMapZoomSlider.SetValueWithoutNotify(1f);
            debugMapZoomLabel.text = "1×";
            RefreshDebugMap();
        }

        private void RefreshDebugMap()
        {
            BuildIfNeeded();
            debugMapContent.Clear();
            debugMapStatusLabel.text = string.Empty;
            if (!EditorApplication.isPlaying)
            {
                debugMapSummaryLabel.text =
                    "重生点与传送方块：进入 Play Mode 后扫描";
                debugMapContent.style.width = 440f;
                debugMapContent.style.height = 280f;
                AddMapHint("仅 Play Mode 显示");
                return;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();
            List<RespawnAnchorFeature> respawnAnchors =
                ProjectDiscovery
                    .FindAll<RespawnAnchorFeature>(true)
                    .Where(anchor =>
                        anchor != null &&
                        anchor.gameObject.scene ==
                        activeScene)
                    .OrderBy(anchor =>
                        anchor.HasChainIndex
                            ? anchor.ChainIndex
                            : int.MaxValue)
                    .ThenBy(
                        anchor => anchor.AnchorId,
                        StringComparer.Ordinal)
                    .ToList();
            List<TeleportAnchorFeature> teleportAnchors =
                TeleportAnchorFeature
                    .RegisteredAnchors
                .Where(anchor =>
                    anchor != null &&
                    anchor.isActiveAndEnabled &&
                    anchor.gameObject.scene ==
                    activeScene)
                .OrderBy(
                    anchor => anchor.TeleportId,
                    StringComparer.Ordinal)
                .ToList();
            debugMapSummaryLabel.text =
                "重生点：" +
                respawnAnchors.Count +
                "    传送方块：" +
                teleportAnchors.Count;
            if (respawnAnchors.Count == 0 &&
                teleportAnchors.Count == 0)
            {
                debugMapContent.style.width = 440f;
                debugMapContent.style.height = 280f;
                AddMapHint(
                    "当前场景没有重生点或已注册传送方块");
                return;
            }

            var positions = new List<Vector3>(
                respawnAnchors.Count +
                teleportAnchors.Count);
            for (int index = 0;
                 index < respawnAnchors.Count;
                 index++)
            {
                positions.Add(
                    respawnAnchors[index]
                        .RespawnPosition);
            }
            for (int index = 0;
                 index < teleportAnchors.Count;
                 index++)
            {
                positions.Add(
                    teleportAnchors[index]
                        .transform.position);
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int index = 0;
                 index < positions.Count;
                 index++)
            {
                Vector3 position =
                    positions[index];
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float zoom = Mathf.Clamp(
                debugMapZoomSlider.value,
                1f,
                4f);
            const float baseWidth = 440f;
            const float baseHeight = 300f;
            const float padding = 58f;
            debugMapContent.style.width =
                baseWidth * zoom;
            debugMapContent.style.height =
                baseHeight * zoom;

            for (int index = 0;
                 index < respawnAnchors.Count;
                 index++)
            {
                RespawnAnchorFeature anchor =
                    respawnAnchors[index];
                Vector3 position =
                    anchor.RespawnPosition;
                RespawnAnchorFeature captured =
                    anchor;
                string label =
                    captured.HasChainIndex
                        ? captured.DisplayLabel
                        : "重生点 #" +
                          (index + 1);
                string state = !captured
                    .isActiveAndEnabled
                    ? "×"
                    : captured.IsActivated
                        ? "●"
                        : "○";
                string tooltip =
                    label +
                    "\nID：" +
                    captured.AnchorId +
                    "\n世界坐标：" +
                    position.x.ToString("0.##") +
                    ", " +
                    position.y.ToString("0.##") +
                    "\nBoss 重生点：" +
                    (captured
                        .IsBossArenaRespawnPoint
                        ? "是"
                        : "否") +
                    "\n已激活：" +
                    (captured.IsActivated
                        ? "是"
                        : "否") +
                    "\n当前绑定：" +
                    (captured.IsCurrent
                        ? "是"
                        : "否");
                AddDebugMapButton(
                    state + " " + label,
                    position,
                    minX,
                    maxX,
                    minY,
                    maxY,
                    rangeX,
                    rangeY,
                    zoom,
                    baseWidth,
                    baseHeight,
                    padding,
                    tooltip,
                    new Color(.16f, .38f, .28f),
                    () => TeleportToRespawn(
                        captured));
            }

            for (int index = 0;
                 index < teleportAnchors.Count;
                 index++)
            {
                TeleportAnchorFeature anchor =
                    teleportAnchors[index];
                Vector3 position =
                    anchor.transform.position;
                TeleportAnchorFeature captured =
                    anchor;
                string tooltip =
                    "传送方块：" +
                    captured.TeleportId +
                    "\n世界坐标：" +
                    position.x.ToString("0.##") +
                    ", " +
                    position.y.ToString("0.##");
                AddDebugMapButton(
                    "◆ " + captured.TeleportId,
                    position,
                    minX,
                    maxX,
                    minY,
                    maxY,
                    rangeX,
                    rangeY,
                    zoom,
                    baseWidth,
                    baseHeight,
                    padding,
                    tooltip,
                    new Color(.34f, .27f, .12f),
                    () => TeleportToAnchor(
                        captured.TeleportId));
            }
        }

        private void AddDebugMapButton(
            string text,
            Vector3 position,
            float minX,
            float maxX,
            float minY,
            float maxY,
            float rangeX,
            float rangeY,
            float zoom,
            float baseWidth,
            float baseHeight,
            float padding,
            string tooltip,
            Color backgroundColor,
            Action action)
        {
            float normalizedX =
                Mathf.Approximately(rangeX, 0f)
                    ? .5f
                    : Mathf.InverseLerp(
                        minX,
                        maxX,
                        position.x);
            float normalizedY =
                Mathf.Approximately(rangeY, 0f)
                    ? .5f
                    : Mathf.InverseLerp(
                        minY,
                        maxY,
                        position.y);
            const float buttonWidth = 112f;
            const float buttonHeight = 24f;
            var button = new Button(action)
            {
                text = text,
                tooltip = tooltip
            };
            button.style.position =
                Position.Absolute;
            button.style.left =
                (padding +
                 normalizedX *
                 (baseWidth - padding * 2f)) *
                zoom -
                buttonWidth * .5f;
            button.style.top =
                (padding +
                 (1f - normalizedY) *
                 (baseHeight - padding * 2f)) *
                zoom -
                buttonHeight * .5f;
            button.style.width = buttonWidth;
            button.style.height = buttonHeight;
            button.style.fontSize = 10f;
            button.style.backgroundColor =
                backgroundColor;
            button.style.color = Color.white;
            debugMapContent.Add(button);
        }

        private void AddMapHint(string text)
        {
            var hint = new Label(text);
            hint.style.position = Position.Absolute;
            hint.style.left = 0f;
            hint.style.right = 0f;
            hint.style.top = 0f;
            hint.style.bottom = 0f;
            hint.style.unityTextAlign =
                TextAnchor.MiddleCenter;
            hint.style.color =
                new Color(.60f, .66f, .70f);
            debugMapContent.Add(hint);
        }

        private void TeleportToRespawn(
            RespawnAnchorFeature anchor)
        {
            if (!EditorApplication.isPlaying)
            {
                debugMapStatusLabel.text =
                    "请进入 Play Mode 后使用。";
                return;
            }

            PlayerController player =
                ColorDebugTools
                    .FindPlayerInActiveScene();
            if (player == null)
            {
                debugMapStatusLabel.text =
                    "当前场景没有可用的标准玩家。";
                return;
            }
            if (anchor == null ||
                anchor.gameObject.scene !=
                player.gameObject.scene)
            {
                debugMapStatusLabel.text =
                    "目标重生点不在玩家所在场景。";
                return;
            }
            if (!anchor.isActiveAndEnabled)
            {
                debugMapStatusLabel.text =
                    "目标重生点未启用。";
                return;
            }
            if (!anchor.IsActivated)
            {
                anchor.ActivateForPlayer(player);
            }
            if (!anchor.IsRespawnPointAvailable)
            {
                debugMapStatusLabel.text =
                    "目标重生点无法激活。";
                return;
            }
            if (!player.TryRestoreLoadedRunPosition(
                    anchor,
                    false,
                    default))
            {
                debugMapStatusLabel.text =
                    "传送失败：" +
                    DebugRespawnLabel(anchor);
                return;
            }

            debugMapStatusLabel.text =
                "已走复活流程前往 " +
                DebugRespawnLabel(anchor) +
                "。";
        }

        private static string DebugRespawnLabel(
            RespawnAnchorFeature anchor)
        {
            return anchor.HasChainIndex
                ? anchor.DisplayLabel
                : "未连接重生点";
        }

        private void TeleportToAnchor(string anchorId)
        {
            if (!EditorApplication.isPlaying)
            {
                debugMapStatusLabel.text =
                    "请进入 Play Mode 后使用。";
                return;
            }

            PlayerController player =
                ColorDebugTools
                    .FindPlayerInActiveScene();
            if (player == null)
            {
                debugMapStatusLabel.text =
                    "当前场景没有可用的标准玩家。";
                return;
            }
            if (!TeleportAnchorFeature.TeleportObject(
                    player.gameObject,
                    anchorId))
            {
                debugMapStatusLabel.text =
                    "传送失败：" + anchorId;
                return;
            }

            debugMapStatusLabel.text =
                "已传送到传送方块 " +
                anchorId +
                "。";
        }

        private void Refresh()
        {
            BuildIfNeeded();
            bool playing =
                EditorApplication.isPlaying;
            PlayerController player = playing
                ? ColorDebugTools
                    .FindPlayerInActiveScene()
                : null;

            playModeLabel.text = playing
                ? "运行状态：Play Mode"
                : "运行状态：编辑模式，仅可查看";
            playerLabel.text = player != null
                ? "当前玩家：" + player.name
                : playing
                    ? "当前玩家：未找到"
                    : "当前玩家：进入 Play Mode 后检测";

            unlockAbilitiesButton.SetEnabled(
                playing && player != null);
            CollectibleProgressSnapshot progress =
                CollectibleProgressService
                    .GetWorldProgress();
            collectibleLabel.text =
                "当前已获得：" +
                progress.Collected +
                "\n最大可获得：" +
                progress.Total +
                "\n完成比例：" +
                (progress.Total > 0
                    ? progress.Ratio * 100f
                    : 0f)
                    .ToString("0.0") +
                "%";

            if (targetCountField != null &&
                targetCountField.focusController?
                    .focusedElement != targetCountField)
            {
                targetCountField.SetValueWithoutNotify(
                    Mathf.Clamp(
                        progress.Collected,
                        0,
                        progress.Total));
            }

            bool canEdit =
                playing && player != null;
            targetCountField.SetEnabled(canEdit);
            applyCountButton.SetEnabled(canEdit);
            zeroButton.SetEnabled(canEdit);
            maxButton.SetEnabled(canEdit);
            refreshButton.SetEnabled(true);
        }

        private void UnlockAbilities()
        {
            ColorDebugTools
                .UnlockAllPlayerAbilities(
                    out string status);
            statusLabel.text = status;
            Refresh();
        }

        private void ApplyTargetCount()
        {
            if (!EditorApplication.isPlaying)
            {
                statusLabel.text =
                    "请进入 Play Mode 后使用。";
                return;
            }
            if (!CollectibleProgressService
                    .TrySetWorldCollectedCountForDebug(
                        targetCountField.value,
                        out int applied,
                        out string error))
            {
                statusLabel.text = error;
                return;
            }

            statusLabel.text =
                "已把当前收集品数量设为 " +
                applied +
                "。";
            Refresh();
        }

        private void SetTargetCount(int value)
        {
            targetCountField.SetValueWithoutNotify(value);
            ApplyTargetCount();
        }

        private void SetTargetToMaximum()
        {
            CollectibleProgressSnapshot progress =
                CollectibleProgressService
                    .GetWorldProgress();
            targetCountField.SetValueWithoutNotify(
                progress.Total);
            ApplyTargetCount();
        }
    }
}
