using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.CameraModes.Editor
{
    internal sealed class CameraModePanel : VisualElement
    {
        private const string TransitionPreviewPreference =
            "2026TapTap.CameraModes.PreviewTransition";
        private static readonly Color Accent2D = new Color(0.12f, 0.55f, 0.95f, 1f);
        private static readonly Color Accent3D = new Color(0.54f, 0.34f, 0.92f, 1f);

        private readonly Color cardColor;
        private readonly Color subtleColor;
        private readonly Color idleButtonColor;
        private readonly ObjectField cameraField;
        private readonly ObjectField followTargetField;
        private readonly Button configureButton;
        private readonly Button side2DButton;
        private readonly Button perspective3DButton;
        private readonly Toggle previewTransitionToggle;
        private readonly Button locateButton;
        private readonly Label statusBadge;
        private readonly Label statusDetail;
        private readonly VisualElement settingsContent;

        private CameraModeController activeController;
        private SerializedObject boundController;
        private SerializedObject boundManager;

        public CameraModePanel()
        {
            bool darkTheme = EditorGUIUtility.isProSkin;
            cardColor = darkTheme
                ? new Color(0.16f, 0.17f, 0.19f, 0.96f)
                : new Color(0.91f, 0.92f, 0.94f, 0.98f);
            subtleColor = darkTheme
                ? new Color(0.22f, 0.23f, 0.26f, 1f)
                : new Color(0.82f, 0.83f, 0.86f, 1f);
            idleButtonColor = darkTheme
                ? new Color(0.25f, 0.26f, 0.29f, 1f)
                : new Color(0.76f, 0.77f, 0.8f, 1f);

            name = "camera-mode-panel";
            style.minWidth = 300f;
            style.maxWidth = 390f;
            style.paddingLeft = 10f;
            style.paddingRight = 10f;
            style.paddingTop = 10f;
            style.paddingBottom = 10f;

            Add(CreateHeader(out statusBadge));

            VisualElement bindingCard = CreateCard("场景绑定", "选择相机和它要跟随的对象");
            cameraField = CreateObjectField<Camera>("受控相机");
            cameraField.RegisterValueChangedCallback(evt =>
            {
                SetCamera(evt.newValue as Camera);
            });
            bindingCard.Add(cameraField);

            VisualElement cameraActions = CreateRow();
            cameraActions.Add(CreateSmallButton("使用 Main Camera", () => SetCamera(Camera.main)));
            cameraActions.Add(CreateSmallButton("读取当前选择", UseCurrentSelection));
            bindingCard.Add(cameraActions);

            followTargetField = CreateObjectField<Transform>("跟随目标");
            followTargetField.RegisterValueChangedCallback(_ => RefreshControls());
            bindingCard.Add(followTargetField);

            Button createTargetButton = CreateSmallButton("＋ 创建临时跟随点", CreateFollowTarget);
            createTargetButton.style.marginTop = 2f;
            bindingCard.Add(createTargetButton);

            configureButton = new Button(ConfigureController)
            {
                text = "创建 / 应用相机控制器",
                tooltip = "将当前相机和跟随目标写入 CameraModeController"
            };
            configureButton.style.height = 30f;
            configureButton.style.marginTop = 7f;
            configureButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            bindingCard.Add(configureButton);
            Add(bindingCard);

            VisualElement previewCard = CreateCard("视角预览", "可在编辑态直接检查切换动画和构图");
            VisualElement modeRow = CreateRow();
            side2DButton = CreateModeButton("2D  平台", "正交 · 平视", Accent2D, CameraViewMode.Side2D);
            perspective3DButton = CreateModeButton("3D  俯视", "透视 · 斜上方", Accent3D, CameraViewMode.Perspective3D);
            modeRow.Add(side2DButton);
            modeRow.Add(perspective3DButton);
            previewCard.Add(modeRow);

            previewTransitionToggle = new Toggle("切换预览时启用过渡")
            {
                value = EditorPrefs.GetBool(TransitionPreviewPreference, true),
                tooltip = "关闭后，编辑态点击模式按钮会立即跳到目标构图"
            };
            previewTransitionToggle.style.marginTop = 5f;
            previewTransitionToggle.RegisterValueChangedCallback(evt =>
                EditorPrefs.SetBool(TransitionPreviewPreference, evt.newValue));
            previewCard.Add(previewTransitionToggle);

            statusDetail = new Label();
            statusDetail.style.marginTop = 7f;
            statusDetail.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusDetail.style.fontSize = 11f;
            statusDetail.style.opacity = 0.72f;
            previewCard.Add(statusDetail);

            locateButton = CreateSmallButton("在 Inspector 中查看完整配置", LocateController);
            locateButton.style.marginTop = 4f;
            previewCard.Add(locateButton);
            Add(previewCard);

            Foldout settingsFoldout = new Foldout
            {
                text = "高级参数",
                value = false,
                tooltip = "调整 2D、3D、过渡和跟随参数"
            };
            settingsFoldout.style.marginTop = 5f;
            settingsFoldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsContent = new VisualElement();
            settingsContent.style.marginTop = 3f;
            settingsContent.style.paddingLeft = 3f;
            settingsFoldout.Add(settingsContent);
            Add(settingsFoldout);

            Label hint = new Label("提示：该面板只修改当前场景，不会改动渲染管线或 Build Settings。")
            {
                tooltip = "Scene View 右上角 Overlays 菜单可随时隐藏或重新显示此面板"
            };
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.fontSize = 10f;
            hint.style.opacity = 0.58f;
            hint.style.marginTop = 7f;
            Add(hint);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            schedule.Execute(RefreshStatus).Every(150);

            Camera initialCamera = ResolveCameraFromSelection();
            SetCamera(initialCamera != null ? initialCamera : Camera.main);
        }

        private VisualElement CreateHeader(out Label badge)
        {
            VisualElement header = CreateRow();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 5f;

            Image icon = new Image
            {
                image = EditorGUIUtility.IconContent("Camera Icon").image,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.style.width = 28f;
            icon.style.height = 28f;
            icon.style.marginRight = 7f;
            header.Add(icon);

            VisualElement titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1f;
            Label title = new Label("2D / 3D 相机");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleGroup.Add(title);
            Label subtitle = new Label("平台跳跃视角工具");
            subtitle.style.fontSize = 10f;
            subtitle.style.opacity = 0.62f;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);

            badge = new Label("未配置");
            badge.style.paddingLeft = 8f;
            badge.style.paddingRight = 8f;
            badge.style.paddingTop = 3f;
            badge.style.paddingBottom = 3f;
            badge.style.borderTopLeftRadius = 9f;
            badge.style.borderTopRightRadius = 9f;
            badge.style.borderBottomLeftRadius = 9f;
            badge.style.borderBottomRightRadius = 9f;
            badge.style.backgroundColor = subtleColor;
            badge.style.fontSize = 10f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(badge);
            return header;
        }

        private VisualElement CreateCard(string title, string description)
        {
            VisualElement card = new VisualElement();
            card.style.backgroundColor = cardColor;
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            card.style.paddingLeft = 9f;
            card.style.paddingRight = 9f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginTop = 5f;

            Label titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12f;
            card.Add(titleLabel);

            Label descriptionLabel = new Label(description);
            descriptionLabel.style.fontSize = 10f;
            descriptionLabel.style.opacity = 0.62f;
            descriptionLabel.style.marginBottom = 5f;
            card.Add(descriptionLabel);
            return card;
        }

        private static ObjectField CreateObjectField<T>(string label) where T : Object
        {
            ObjectField field = new ObjectField(label)
            {
                objectType = typeof(T),
                allowSceneObjects = true
            };
            field.style.marginTop = 2f;
            return field;
        }

        private static VisualElement CreateRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private Button CreateSmallButton(string text, System.Action action)
        {
            Button button = new Button(action) { text = text };
            button.style.flexGrow = 1f;
            button.style.height = 23f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }

        private Button CreateModeButton(
            string title,
            string subtitle,
            Color accent,
            CameraViewMode mode)
        {
            Button button = new Button(() => ChangeMode(mode));
            button.style.flexGrow = 1f;
            button.style.height = 48f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.borderBottomWidth = 3f;
            button.style.borderBottomColor = accent;

            VisualElement content = new VisualElement();
            content.pickingMode = PickingMode.Ignore;
            content.style.alignItems = Align.Center;
            Label titleLabel = new Label(title);
            titleLabel.pickingMode = PickingMode.Ignore;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            content.Add(titleLabel);
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.pickingMode = PickingMode.Ignore;
            subtitleLabel.style.fontSize = 9f;
            subtitleLabel.style.opacity = 0.66f;
            content.Add(subtitleLabel);
            button.Add(content);
            return button;
        }

        private void Subscribe()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshControls;
            EditorApplication.hierarchyChanged += RefreshControls;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void Unsubscribe()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshControls;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnSelectionChanged()
        {
            Camera selected = ResolveCameraFromSelection();
            if (selected != null)
            {
                SetCamera(selected);
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            RefreshControls();
        }

        private void UseCurrentSelection()
        {
            Camera selected = ResolveCameraFromSelection();
            if (selected != null)
            {
                SetCamera(selected);
                return;
            }

            if (Selection.activeTransform != null)
            {
                followTargetField.SetValueWithoutNotify(Selection.activeTransform);
                RefreshControls();
            }
        }

        private static Camera ResolveCameraFromSelection()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return null;
            }

            Camera camera = selectedObject.GetComponent<Camera>();
            if (camera != null)
            {
                return camera;
            }

            CameraModeController controller = selectedObject.GetComponent<CameraModeController>();
            return controller != null ? controller.ControlledCamera : null;
        }

        private void SetCamera(Camera camera)
        {
            cameraField.SetValueWithoutNotify(camera);
            CameraModeController controller = camera != null
                ? camera.GetComponent<CameraModeController>()
                : null;

            if (controller != activeController)
            {
                activeController = controller;
                followTargetField.SetValueWithoutNotify(
                    activeController != null ? activeController.FollowTarget : null);
                RebuildSettings();
            }

            RefreshControls();
        }

        private void RefreshControls()
        {
            Camera camera = cameraField.value as Camera;
            CameraModeController controller = camera != null
                ? camera.GetComponent<CameraModeController>()
                : null;
            if (controller != activeController)
            {
                activeController = controller;
                RebuildSettings();
            }

            configureButton.SetEnabled(camera != null && !Application.isPlaying);
            side2DButton.SetEnabled(activeController != null);
            perspective3DButton.SetEnabled(activeController != null);
            locateButton.SetEnabled(activeController != null);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (activeController == null)
            {
                statusBadge.text = "未配置";
                statusBadge.style.backgroundColor = subtleColor;
                statusBadge.style.color = StyleKeyword.Null;
                statusDetail.text = "先选择场景相机，再创建控制器";
                side2DButton.style.backgroundColor = idleButtonColor;
                perspective3DButton.style.backgroundColor = idleButtonColor;
                return;
            }

            if (Application.isPlaying && !activeController.HasControl)
            {
                statusBadge.text = "演出接管";
                statusBadge.style.backgroundColor = subtleColor;
                statusBadge.style.color = StyleKeyword.Null;
                string activeName = activeController.Manager != null
                    ? activeController.Manager.ActiveControlName
                    : null;
                statusDetail.text = string.IsNullOrEmpty(activeName)
                    ? "当前模式请求正在等待控制权"
                    : $"当前控制者：{activeName}；模式请求会在释放后生效";
                side2DButton.style.backgroundColor = idleButtonColor;
                perspective3DButton.style.backgroundColor = idleButtonColor;
                return;
            }

            CameraViewMode mode = activeController.IsTransitioning
                ? activeController.TargetMode
                : activeController.CurrentMode;
            bool side2D = mode == CameraViewMode.Side2D;
            statusBadge.text = activeController.IsTransitioning
                ? "切换中"
                : (side2D ? "2D 模式" : "3D 模式");
            statusBadge.style.backgroundColor = side2D ? Accent2D : Accent3D;
            statusBadge.style.color = Color.white;
            statusDetail.text = activeController.IsTransitioning
                ? $"正在前往 {(side2D ? "2D 平台" : "3D 俯视")} · {activeController.NormalizedTransitionTime:P0}"
                : $"当前稳定在 {(side2D ? "2D 正交平视" : "3D 透视俯视")}";
            side2DButton.style.backgroundColor = side2D ? Accent2D : idleButtonColor;
            perspective3DButton.style.backgroundColor = side2D ? idleButtonColor : Accent3D;
            side2DButton.style.color = side2D ? Color.white : StyleKeyword.Null;
            perspective3DButton.style.color = side2D ? StyleKeyword.Null : Color.white;
        }

        private void RebuildSettings()
        {
            settingsContent.Unbind();
            settingsContent.Clear();
            boundController = null;
            boundManager = null;

            if (activeController == null)
            {
                Label empty = new Label("创建控制器后可在这里直接调整构图参数。")
                {
                    tooltip = "高级参数会自动绑定到当前相机的 CameraModeController"
                };
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.opacity = 0.65f;
                settingsContent.Add(empty);
                return;
            }

            boundController = new SerializedObject(activeController);
            boundManager = activeController.Manager != null
                ? new SerializedObject(activeController.Manager)
                : null;
            AddBoundProperty(boundManager, "fallbackFocusPoint", "无目标时的观察点");
            AddBoundProperty(boundController, "side2D", "2D 平台视角");
            AddBoundProperty(boundController, "perspective3D", "3D 俯视视角");
            AddBoundProperty(boundController, "transition", "切换动画");
        }

        private void AddBoundProperty(
            SerializedObject serializedTarget,
            string propertyName,
            string label)
        {
            if (serializedTarget == null)
            {
                return;
            }

            SerializedProperty property = serializedTarget.FindProperty(propertyName);
            if (property != null)
            {
                PropertyField field = new PropertyField(property, label);
                field.style.marginTop = 2f;
                settingsContent.Add(field);
                field.BindProperty(property);
            }
        }

        private void CreateFollowTarget()
        {
            GameObject targetObject = new GameObject("CameraFollowTarget");
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Camera Follow Target");
            if (Selection.activeTransform != null)
            {
                targetObject.transform.position = Selection.activeTransform.position;
            }

            followTargetField.SetValueWithoutNotify(targetObject.transform);
            Selection.activeObject = targetObject;
            MarkSceneDirty(targetObject);
            RefreshControls();
        }

        private void ConfigureController()
        {
            Camera camera = cameraField.value as Camera;
            if (camera == null || Application.isPlaying)
            {
                return;
            }

            CameraModeController controller = camera.GetComponent<CameraModeController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CameraModeController>(camera.gameObject);
            }

            CameraControlManager manager = camera.GetComponent<CameraControlManager>();
            if (manager == null)
            {
                manager = Undo.AddComponent<CameraControlManager>(camera.gameObject);
            }

            Undo.RecordObject(controller, "Configure Camera Mode Controller");
            Undo.RecordObject(manager, "Configure Camera Control Manager");
            Undo.RecordObject(camera, "Configure Camera Mode Controller");
            Undo.RecordObject(camera.transform, "Configure Camera Mode Controller");
            controller.Configure(camera, followTargetField.value as Transform, true);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            MarkSceneDirty(camera.gameObject);

            activeController = controller;
            RebuildSettings();
            RefreshControls();
        }

        private void ChangeMode(CameraViewMode mode)
        {
            if (activeController == null)
            {
                return;
            }

            Camera camera = activeController.ControlledCamera;
            Undo.RecordObject(activeController, "Change Camera View Mode");
            if (camera != null)
            {
                Undo.RecordObject(camera, "Change Camera View Mode");
                Undo.RecordObject(camera.transform, "Change Camera View Mode");
            }

            if (Application.isPlaying)
            {
                CameraModeEditorRequests.RequestMode(
                    activeController,
                    mode,
                    false);
            }
            else if (previewTransitionToggle.value)
            {
                CameraModeEditPreviewDriver.StartTransition(activeController, mode);
                MarkSceneDirty(activeController.gameObject);
            }
            else
            {
                CameraModeEditorRequests.RequestMode(
                    activeController,
                    mode,
                    true);
                EditorUtility.SetDirty(activeController);
                if (camera != null)
                {
                    EditorUtility.SetDirty(camera);
                    EditorUtility.SetDirty(camera.transform);
                }

                MarkSceneDirty(activeController.gameObject);
            }

            RefreshStatus();
            SceneView.RepaintAll();
        }

        private void LocateController()
        {
            if (activeController == null)
            {
                return;
            }

            Selection.activeObject = activeController;
            EditorGUIUtility.PingObject(activeController);
        }

        private static void MarkSceneDirty(GameObject gameObject)
        {
            if (gameObject != null && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
