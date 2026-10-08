using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    public sealed class RespawnAnchorChainWindow : EditorWindow
    {
        private static readonly Vector2 FixedWindowSize =
            new Vector2(920f, 620f);

        private RespawnAnchorFeature owner;
        private Scene scene;
        private RespawnAnchorGraphView graphView;
        private Label status;
        private Slider spacingSlider;
        private double nextLayoutSyncTime;

        public static void Open(RespawnAnchorFeature anchor)
        {
            if (anchor == null)
            {
                return;
            }

            RespawnAnchorChainWindow window =
                GetWindow<RespawnAnchorChainWindow>();
            window.titleContent =
                new GUIContent("重生点链表");
            window.owner = anchor;
            window.scene = anchor.gameObject.scene;
            window.minSize = FixedWindowSize;
            window.maxSize = FixedWindowSize;
            window.maximized = false;
            window.ShowUtility();
            window.Focus();
            window.Refresh();
        }

        private void OnEnable()
        {
            minSize = FixedWindowSize;
            maxSize = FixedWindowSize;
            maximized = false;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged -=
                OnHierarchyChanged;
            EditorApplication.hierarchyChanged +=
                OnHierarchyChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -=
                OnHierarchyChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void OnEditorUpdate()
        {
            if (graphView == null ||
                !scene.IsValid() ||
                EditorApplication.timeSinceStartup <
                nextLayoutSyncTime)
            {
                return;
            }

            nextLayoutSyncTime =
                EditorApplication.timeSinceStartup + .05d;
            graphView.RefreshLayoutFromWorldPositions();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            root.style.backgroundColor =
                EditorGUIUtility.isProSkin
                    ? new Color(.055f, .067f, .078f, 1f)
                    : new Color(.91f, .925f, .94f, 1f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6f;

            var title = new Label("重生点链表");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 12f;
            header.Add(title);

            var initializeButton = new Button(
                InitializeSceneOrder)
            {
                text = "按场景顺序初始化"
            };
            initializeButton.style.marginRight = 4f;
            header.Add(initializeButton);

            var clearButton = new Button(ClearAll)
            {
                text = "清空连接"
            };
            clearButton.style.marginRight = 4f;
            header.Add(clearButton);

            var refreshButton = new Button(Refresh)
            {
                text = "刷新"
            };
            refreshButton.style.marginRight = 8f;
            header.Add(refreshButton);

            spacingSlider = new Slider(
                "节点间距",
                .35f,
                2.5f)
            {
                value = 1f
            };
            spacingSlider.style.width = 180f;
            spacingSlider.style.marginRight = 8f;
            spacingSlider.RegisterValueChangedCallback(evt =>
            {
                graphView?.SetLayoutSpacing(evt.newValue);
            });
            header.Add(spacingSlider);

            var hint = new Label(
                "拖出圆点连接到目标端口 · 右键连线截断");
            hint.style.marginLeft = 10f;
            hint.style.marginRight = 10f;
            hint.style.opacity = .68f;
            hint.style.whiteSpace = WhiteSpace.NoWrap;
            header.Add(hint);

            status = new Label();
            status.style.flexGrow = 1f;
            status.style.marginLeft = 8f;
            status.style.whiteSpace = WhiteSpace.Normal;
            header.Add(status);
            root.Add(header);

            graphView = new RespawnAnchorGraphView();
            graphView.style.flexGrow = 1f;
            graphView.Changed += PersistScene;
            graphView.StatusChanged += SetStatus;
            root.Add(graphView);
        }

        private void Refresh()
        {
            if (graphView == null)
            {
                return;
            }

            if (!scene.IsValid() && owner != null)
            {
                scene = owner.gameObject.scene;
            }

            if (!scene.IsValid())
            {
                scene = SceneManager.GetActiveScene();
            }

            RespawnAnchorChainEditorUtility
                .EnsureIdsAndPersist(scene);
            graphView.Load(scene);
            spacingSlider?.SetValueWithoutNotify(
                graphView.LayoutSpacing);
            var anchors =
                RespawnAnchorChainEditorUtility
                    .GetSceneAnchors(scene);
            SetStatus($"已找到 {anchors.Count} 个重生锚。");
        }

        private void InitializeSceneOrder()
        {
            if (RespawnAnchorChainEditorUtility
                    .InitializeSceneOrder(
                        scene,
                        out string message))
            {
                SetStatus(message);
            }
            else
            {
                SetStatus(message);
            }

            Refresh();
        }

        private void ClearAll()
        {
            if (RespawnAnchorChainEditorUtility.ClearAll(
                    scene,
                    out string message))
            {
                SetStatus(message);
            }
            else
            {
                SetStatus(message);
            }

            Refresh();
        }

        private void PersistScene()
        {
            if (!scene.IsValid())
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }

        private void OnUndoRedo()
        {
            Refresh();
            PersistScene();
        }

        private void OnHierarchyChanged()
        {
            Refresh();
        }

        private void SetStatus(string message)
        {
            if (status != null)
            {
                status.text = message ?? string.Empty;
            }
        }
    }
}
