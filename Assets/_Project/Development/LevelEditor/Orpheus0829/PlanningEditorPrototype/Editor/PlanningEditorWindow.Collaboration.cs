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
        private Vector2Int fragmentAnchor;
        private bool fragmentIncludePlayer;
        private int fragmentDetached;
        private Label fragmentStatus;
        private Button fragmentApply;

        private VisualElement BuildCollaborationTools()
        {
            var group = new Foldout { text = "协作与片段", value = false };
            group.style.marginLeft = group.style.marginRight = 8;
            group.Add(new Label("导入到当前地图") { style = { marginTop = 8 } });
            var input = new ObjectField("存档文件") { objectType = typeof(TextAsset), allowSceneObjects = false };
            input.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null) { fragmentSource = null; RefreshImportPreview(); return; }
                LoadFragment(AssetDatabase.GetAssetPath(evt.newValue));
            });
            group.Add(input);
            group.Add(SmallRowButton("浏览 JSON…", () =>
            {
                string path = EditorUtility.OpenFilePanel("选择关卡存档", PlanningSaveStore.SaveFolder, "json");
                if (!string.IsNullOrEmpty(path)) LoadFragment(path);
            }));
            var anchor = new Vector2IntField("导入左上格") { tooltip = "世界图坐标；Y 向下。导入前显示位置预览，不覆盖已有房间。" };
            anchor.RegisterValueChangedCallback(evt => { fragmentAnchor = evt.newValue; RefreshImportPreview(); });
            group.Add(anchor);
            group.Add(SmallRowButton("放到地图右侧", () =>
            {
                fragmentAnchor = SuggestedFragmentAnchor(); anchor.SetValueWithoutNotify(fragmentAnchor); RefreshImportPreview(); canvas.FocusImportPreview();
            }));
            var player = new Toggle("使用导入的出生点");
            player.RegisterValueChangedCallback(evt => { fragmentIncludePlayer = evt.newValue; RefreshImportPreview(); });
            group.Add(player);
            fragmentStatus = new Label("请选择关卡存档。");
            fragmentStatus.style.whiteSpace = WhiteSpace.Normal;
            fragmentStatus.style.marginTop = fragmentStatus.style.marginBottom = 8;
            group.Add(fragmentStatus);
            fragmentApply = new Button(ApplyFragment) { text = "导入当前地图" };
            fragmentApply.SetEnabled(false);
            group.Add(fragmentApply);
            group.Add(SmallRowButton("取消预览", () => { fragmentSource = null; input.SetValueWithoutNotify(null); RefreshImportPreview(); }));
            group.Add(new Label("导出地图片段") { style = { marginTop = 12 } });
            group.Add(SmallRowButton("导出选中房间 / 通道…", () => ExportFragment(canvas.SelectedWorldIds.ToArray(), null)));
            group.Add(SmallRowButton("框选区域导出…", () => { SetMode(PlanningCanvasMode.World); canvas.BeginAreaExport(); }));
            group.Add(SmallRowButton("导出选中规划区域…", () =>
            {
                if (canvas.SelectedRegion == null) { EditorUtility.DisplayDialog("导出区域", "请先选择一个规划区域。", "确定"); return; }
                ExportFragment(null, canvas.SelectedRegion.Bounds);
            }));
            canvas.ExportAreaSelected -= OnExportArea;
            canvas.ExportAreaSelected += OnExportArea;
            group.Add(SmallRowButton("定位当前存档", () =>
            {
                string path = !string.IsNullOrEmpty(currentSavePath) ? currentSavePath : PlanningSharedSaveUtility.SceneSavePath(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) EditorUtility.RevealInFinder(path);
            }));
            return group;
        }

        private Vector2Int SuggestedFragmentAnchor()
        {
            if (document.rooms.Count == 0) return Vector2Int.zero;
            var bounds = PlanningFragmentUtility.Bounds(document);
            return new Vector2Int(bounds.xMax + 2, bounds.y);
        }

        private void LoadFragment(string path)
        {
            try
            {
                fragmentSource = PlanningSaveStore.Read(path);
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
                RefreshImportPreview(); canvas.FocusImportPreview();
            }
            catch (Exception exception) { fragmentSource = null; RefreshImportPreview(); ShowSavePanelError("无法读取片段", exception); }
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
                fragmentSource = null;
                canvas.SetDocument(document);
                if (fragmentIncludePlayer && document.TryGetPlayerStartWorldPosition(out Vector3 spawn))
                    Project.LevelEditor.Editor.LevelEditorPlayerService.SetPlayerPosition(spawn * Project.LevelEditor.Editor.LevelEditorState.CellSize, false);
                OnCanvasDocumentChanged();
                OnCanvasSelectionChanged();
                RefreshImportPreview();
            }
            catch (Exception exception) { ShowSavePanelError("无法导入片段", exception); }
        }

        private void OnExportArea(RectInt area) => ExportFragment(null, area);
        private static void ValidateFragmentPalette(PlanningDocument source)
        {
            var palette = Project.LevelEditor.Editor.LevelEditorPaletteService.GetOrCreate();
            foreach (var box in PlanningFragmentUtility.Boxes(source))
            {
                if (box.type == PlanningDetailType.Prop)
                {
                    if (box.propEntryId == PlanningDoorUtility.ButtonEntryId) continue;
                    var entry = palette.PropEntries.FirstOrDefault(p => p.EntryId == box.propEntryId) ??
                        palette.PropEntries.FirstOrDefault(p => p.DisplayName == box.propEntryName);
                    if (entry?.Prefab == null) throw new InvalidDataException("缺少道具资源：" + (box.propEntryName ?? box.propEntryId) + "。请先同步道具栏目及预制体。");
                }
                else if ((!string.IsNullOrEmpty(box.paletteEntryId) || !string.IsNullOrEmpty(box.paletteEntryName)) &&
                    !palette.Entries.Any(e => e.EntryId == box.paletteEntryId || e.DisplayName == box.paletteEntryName))
                    throw new InvalidDataException("缺少方块栏目：" + (box.paletteEntryName ?? box.paletteEntryId) + "。请先同步栏目资源。");
            }
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
