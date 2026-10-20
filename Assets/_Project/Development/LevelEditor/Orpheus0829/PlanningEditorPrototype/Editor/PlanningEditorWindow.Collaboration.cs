using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningEditorWindow
    {
        private PlanningDocument fragmentSource;
        private PlanningDocument fragmentOriginal;
        private bool fragmentCombineRooms;
        private Vector2Int fragmentAnchor;
        private bool fragmentIncludePlayer;
        private int fragmentDetached;
        private Label fragmentStatus;
        private Button fragmentApply;
        private VisualElement collaborationOverlay;
        private VisualElement fragmentImportSection, fragmentExportSection;

        private VisualElement BuildCollaborationOverlay()
        {
            collaborationOverlay = new VisualElement { name = "PlanningCollaborationOverlay" };
            collaborationOverlay.style.position = Position.Absolute;
            collaborationOverlay.style.top = 112f;
            collaborationOverlay.style.right = 20f;
            collaborationOverlay.style.bottom = 30f;
            collaborationOverlay.style.width = 400f;
            collaborationOverlay.style.maxWidth = Length.Percent(90);
            collaborationOverlay.style.backgroundColor = new Color(.09f, .105f, .135f);
            collaborationOverlay.style.borderLeftWidth = collaborationOverlay.style.borderRightWidth =
                collaborationOverlay.style.borderTopWidth = collaborationOverlay.style.borderBottomWidth = 1f;
            collaborationOverlay.style.borderLeftColor = collaborationOverlay.style.borderRightColor =
                collaborationOverlay.style.borderTopColor = collaborationOverlay.style.borderBottomColor = new Color(.3f, .4f, .55f);
            var title = Row();
            var label = new Label("关卡协作 · 导入与片段") { style = { flexGrow = 1f, fontSize = 15f, marginLeft = 10f, unityTextAlign = TextAnchor.MiddleLeft } };
            title.Add(label);
            title.Add(TopButton("关闭", CancelFragmentPreview));
            collaborationOverlay.Add(title);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "PlanningCollaborationScroll" };
            scroll.style.flexGrow = 1f;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.Add(BuildCollaborationTools());
            collaborationOverlay.Add(scroll);
            collaborationOverlay.style.display = DisplayStyle.None;
            return collaborationOverlay;
        }

        private void ShowCollaborationOverlay(bool importing)
        {
            if (collaborationOverlay == null) return;
            canvas?.CancelPointerInteraction();
            if (!importing)
            {
                fragmentOriginal = fragmentSource = null;
                RefreshImportPreview();
            }
            collaborationOverlay.style.display = DisplayStyle.Flex;
            collaborationOverlay.BringToFront();
            fragmentImportSection.style.display = importing ? DisplayStyle.Flex : DisplayStyle.None;
            fragmentExportSection.style.display = importing ? DisplayStyle.None : DisplayStyle.Flex;
            SetMode(PlanningCanvasMode.World);
        }

        private void BeginFragmentAreaExport()
        {
            collaborationOverlay.style.display = DisplayStyle.None;
            SetMode(PlanningCanvasMode.World);
            canvas.BeginAreaExport();
        }

        private VisualElement BuildCollaborationTools()
        {
            var group = new VisualElement();
            group.style.marginLeft = group.style.marginRight = 8;
            fragmentImportSection = new VisualElement();
            fragmentExportSection = new VisualElement();
            group.Add(fragmentImportSection); group.Add(fragmentExportSection);
            var importing = fragmentImportSection;
            var exporting = fragmentExportSection;
            importing.Add(new Label("导入到当前地图") { style = { marginTop = 8, fontSize = 14f } });
            importing.Add(new Label("选择存档 → 拖动置顶预览到目标位置 → 确认导入。左键拖预览，右键平移，滚轮缩放；Esc 取消。预览不会修改当前地图。") { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8f } });
            var input = new ObjectField("存档文件") { objectType = typeof(TextAsset), allowSceneObjects = false };
            input.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null) { fragmentOriginal = fragmentSource = null; RefreshImportPreview(); return; }
                LoadFragment(AssetDatabase.GetAssetPath(evt.newValue));
            });
            importing.Add(input);
            importing.Add(SmallRowButton("选择关卡存档 JSON…", () =>
            {
                string path = EditorUtility.OpenFilePanel("选择关卡存档", PlanningSaveStore.SaveFolder, "json");
                if (!string.IsNullOrEmpty(path)) LoadFragment(path);
            }));
            var anchor = new Vector2IntField("导入左上格") { tooltip = "世界图坐标；Y 向下。导入前显示位置预览，不覆盖已有房间。" };
            anchor.RegisterValueChangedCallback(evt => { fragmentAnchor = evt.newValue; RefreshImportPreview(); });
            importing.Add(anchor);
            canvas.ImportPreviewMoved += position =>
            {
                fragmentAnchor = position;
                anchor.SetValueWithoutNotify(position);
                RefreshImportPreview();
            };
            canvas.ImportPreviewCancelled += CancelFragmentPreview;
            var roomMode = new PopupField<string>("房间处理", new System.Collections.Generic.List<string>
                { "保留原房间划分", "合并为一个新房间" }, fragmentCombineRooms ? 1 : 0);
            roomMode.RegisterValueChangedCallback(evt =>
            {
                fragmentCombineRooms = roomMode.index == 1;
                if (fragmentOriginal != null)
                {
                    fragmentSource = PlanningFragmentUtility.PrepareImport(fragmentOriginal, fragmentCombineRooms);
                    RefreshImportPreview();
                }
            });
            importing.Add(roomMode);
            importing.Add(SmallRowButton("放到地图右侧并预览", () =>
            {
                fragmentAnchor = SuggestedFragmentAnchor(); anchor.SetValueWithoutNotify(fragmentAnchor); RefreshImportPreview(); canvas.FocusImportPreview();
            }));
            importing.Add(SmallRowButton("同时查看当前地图与预览", () => canvas.FocusImportPreview(true)));
            var player = new Toggle("使用导入的出生点");
            player.RegisterValueChangedCallback(evt => { fragmentIncludePlayer = evt.newValue; RefreshImportPreview(); });
            importing.Add(player);
            fragmentStatus = new Label("请选择关卡存档。");
            fragmentStatus.style.whiteSpace = WhiteSpace.Normal;
            fragmentStatus.style.marginTop = fragmentStatus.style.marginBottom = 8;
            importing.Add(fragmentStatus);
            fragmentApply = new Button(ApplyFragment) { text = "确认位置并导入" };
            fragmentApply.SetEnabled(false);
            importing.Add(fragmentApply);
            importing.Add(SmallRowButton("取消预览", CancelFragmentPreview));
            exporting.Add(new Label("导出地图片段") { style = { marginTop = 8, fontSize = 14f } });
            exporting.Add(new Label("可以导出整个房间 / 通道，也可以在世界图框选区域。原地图保持不变。") { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8f } });
            exporting.Add(SmallRowButton("导出当前选中的房间 / 通道…", () => ExportFragment(canvas.SelectedWorldIds.ToArray(), null)));
            exporting.Add(SmallRowButton("在地图上框选区域并导出…", BeginFragmentAreaExport));
            exporting.Add(SmallRowButton("导出选中的规划区域…", () =>
            {
                if (canvas.SelectedRegion == null) { EditorUtility.DisplayDialog("导出区域", "请先选择一个规划区域。", "确定"); return; }
                ExportFragment(null, canvas.SelectedRegion.Bounds);
            }));
            canvas.ExportAreaSelected -= OnExportArea;
            canvas.ExportAreaSelected += OnExportArea;
            group.Add(SmallRowButton("定位当前存档文件", () =>
            {
                string path = !string.IsNullOrEmpty(currentSavePath) ? currentSavePath : PlanningSharedSaveUtility.SceneSavePath(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) EditorUtility.RevealInFinder(path);
            }));
            return group;
        }

        private Vector2Int SuggestedFragmentAnchor()
        {
            return fragmentSource == null ? Vector2Int.zero : PlanningFragmentUtility.SuggestImportAnchor(document, fragmentSource);
        }

        private void CancelFragmentPreview()
        {
            canvas?.CancelPointerInteraction();
            fragmentOriginal = fragmentSource = null;
            rootVisualElement.Query<ObjectField>().ForEach(field => { if (field.label == "存档文件") field.SetValueWithoutNotify(null); });
            RefreshImportPreview();
            collaborationOverlay.style.display = DisplayStyle.None;
        }

        private void LoadFragment(string path)
        {
            try
            {
                fragmentOriginal = PlanningSaveStore.Read(path);
                fragmentSource = PlanningFragmentUtility.PrepareImport(fragmentOriginal, fragmentCombineRooms);
                ValidateFragmentPalette(fragmentSource);
                string assetPath = PlanningSharedSaveUtility.AssetPath(path);
                rootVisualElement.Query<ObjectField>().ForEach(field =>
                {
                    if (field.label == "存档文件") field.SetValueWithoutNotify(assetPath == null ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath));
                });
                fragmentAnchor = SuggestedFragmentAnchor();
                // Validate IDs and bindings once. Moving the preview does not clone a complete map.
                PlanningFragmentUtility.Import(document, fragmentSource, fragmentAnchor, false, out fragmentDetached);
                // Rebuild-free field update keeps a visible anchor consistent with the preview.
                rootVisualElement.Query<Vector2IntField>().ForEach(field => { if (field.label == "导入左上格") field.SetValueWithoutNotify(fragmentAnchor); });
                SetMode(PlanningCanvasMode.World);
                RefreshImportPreview();
                // Run after the view-mode restoration queued by SetMode.
                canvas.schedule.Execute(() => { if (fragmentSource != null) canvas.FocusImportPreview(); }).StartingIn(0);
            }
            catch (Exception exception) { fragmentOriginal = fragmentSource = null; RefreshImportPreview(); ShowSavePanelError("无法读取片段", exception); }
        }

        private void RefreshImportPreview()
        {
            if (fragmentApply == null || canvas == null) return;
            bool valid = false;
            string text = "请选择关卡存档。";
            if (fragmentSource != null)
            {
                try
                {
                    PlanningFragmentUtility.ValidatePlacement(document, fragmentSource, fragmentAnchor);
                    text = $"{fragmentSource.name}\n{fragmentSource.rooms.Count} 个房间 / 通道 · {PlanningFragmentUtility.Boxes(fragmentSource).Count()} 个物体";
                    if (fragmentDetached > 0) text += $"\n将解除 {fragmentDetached} 项片段外绑定。";
                    valid = true;
                }
                catch (Exception exception) { text = exception.Message; }
            }
            fragmentStatus.text = text;
            fragmentApply.SetEnabled(valid && !EditorApplication.isPlayingOrWillChangePlaymode);
            canvas.SetImportPreview(fragmentSource, fragmentAnchor, valid);
        }

        private void ApplyFragment()
        {
            if (fragmentSource == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var result = PlanningFragmentUtility.Import(document, fragmentSource, fragmentAnchor, fragmentIncludePlayer, out int detached);
                ValidateFragmentPalette(fragmentSource);
                if (detached > 0 && !EditorUtility.DisplayDialog("片段外绑定", $"将解除 {detached} 项指向片段外的绑定。继续导入？", "导入", "取消")) return;
                document = result;
                fragmentOriginal = fragmentSource = null;
                canvas.SetDocument(document);
                if (fragmentIncludePlayer && document.TryGetPlayerStartWorldPosition(out Vector3 spawn))
                    Project.LevelEditor.Editor.LevelEditorPlayerService.SetPlayerPosition(spawn * Project.LevelEditor.Editor.LevelEditorState.CellSize, false);
                OnCanvasDocumentChanged();
                OnCanvasSelectionChanged();
                RefreshImportPreview();
                collaborationOverlay.style.display = DisplayStyle.None;
            }
            catch (Exception exception) { ShowSavePanelError("无法导入片段", exception); }
        }

        private void OnExportArea(RectInt area) => ExportFragment(null, area);
        private static void ValidateFragmentPalette(PlanningDocument source)
        {
            PlanningFragmentUtility.ValidateDependencies(source);
        }
        private void ExportFragment(string[] owners, RectInt? area)
        {
            try
            {
                if (owners != null && owners.Length == 0) throw new InvalidDataException("请先选择要导出的房间或通道。");
                var fragment = PlanningFragmentUtility.Extract(document, owners, area, out int detached);
                if (detached > 0 && !EditorUtility.DisplayDialog("片段外绑定", $"导出时将解除 {detached} 项片段外绑定，当前地图不会改变。", "继续", "取消")) return;
                string path = EditorUtility.SaveFilePanel("导出关卡片段", PlanningSaveStore.SaveFolder, PlanningSaveStore.SanitizeFileName(document.name) + "_片段", "json");
                if (string.IsNullOrEmpty(path)) return;
                fragment.name = Path.GetFileNameWithoutExtension(path);
                PlanningSaveStore.Save(path, fragment);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception) { ShowSavePanelError("无法导出片段", exception); }
        }
    }
}
