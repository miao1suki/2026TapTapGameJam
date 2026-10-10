using System.Collections.Generic;
using Project;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningEditorWindow
    {
        private VisualElement regionList;

        private VisualElement BuildLeftRail()
        {
            var rail = new ScrollView(ScrollViewMode.Vertical);
            rail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rail.style.width = leftRailWidth; rail.style.minWidth = 110; rail.style.maxWidth = 420;
            rail.style.flexGrow = 1; rail.style.flexShrink = 0;
            rail.style.backgroundColor = new Color(.09f, .105f, .135f);
            rail.Add(SectionTitle("房间列表")); roomList = ScrollList(220); rail.Add(roomList);
            rail.Add(SectionTitle("通道列表")); connectorList = ScrollList(180); rail.Add(connectorList);
            rail.Add(SectionTitle("区域规划")); regionList = ScrollList(130); rail.Add(regionList);
            return rail;
        }

        private VisualElement BuildRegionTools()
        {
            var section = new Foldout { text = "区域规划", value = true };
            section.style.marginLeft = section.style.marginRight = 6;
            var annotation = new TextField("注释") { value = canvas.RegionAnnotation, isDelayed = true };
            annotation.RegisterValueChangedCallback(evt => canvas.RegionAnnotation = evt.newValue);
            var automatic = new Toggle("自动配色") { value = canvas.AutomaticRegionColor };
            var color = new ColorField("区域颜色") { value = canvas.RegionColor, showAlpha = false };
            color.SetEnabled(!automatic.value);
            color.RegisterValueChangedCallback(evt => canvas.RegionColor = evt.newValue);
            automatic.RegisterValueChangedCallback(evt => { canvas.AutomaticRegionColor = evt.newValue; color.SetEnabled(!evt.newValue); });
            section.Add(annotation); section.Add(automatic); section.Add(color);
            section.Add(TopButton("框选规划区域", () => { canvas.SetMapTool(PlanningMapTool.Region); RefreshToolStyles(); }));
            var note = new Label("框选创建区域。选区域列表后拖动移动，可在选中项中编辑或删除；不生成场景物体。");
            note.style.whiteSpace = WhiteSpace.Normal; note.style.color = new Color(.65f, .72f, .8f);
            section.Add(note);
            return section;
        }

        private void RefreshRegionList()
        {
            if (regionList == null || document == null) return;
            regionList.Clear();
            foreach (var region in document.regions)
            {
                var button = new Button { text = string.IsNullOrWhiteSpace(region.annotation) ? "未命名区域" : region.annotation };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = button.style.marginRight = 6; button.style.height = 24;
                button.style.backgroundColor = canvas.IsRegionSelected(region.id) ? new Color(.24f, .35f, .5f) : new Color(.12f, .15f, .19f);
                button.RegisterCallback<ClickEvent>(evt =>
                {
                    if (mode != PlanningCanvasMode.World) SetMode(PlanningCanvasMode.World);
                    canvas.SelectRegion(region.id, true, evt.ctrlKey || evt.commandKey || evt.shiftKey);
                    selectionFoldout.value = true;
                });
                regionList.Add(button);
            }
        }

        private void BuildSelectedRegionInspector()
        {
            var region = canvas.SelectedRegion;
            if (region == null) return;
            var annotation = new TextField("区域注释") { value = region.annotation, isDelayed = true };
            var color = new ColorField("区域颜色") { value = ColorUtility.TryParseHtmlString(region.colorHex, out var parsed) ? parsed : Color.cyan, showAlpha = false };
            annotation.RegisterValueChangedCallback(evt => canvas.UpdateSelectedRegion(evt.newValue, color.value));
            color.RegisterValueChangedCallback(evt => canvas.UpdateSelectedRegion(annotation.value, evt.newValue));
            inspector.Add(annotation); inspector.Add(color);
            inspector.Add(new Label($"范围：{region.width} × {region.height} 世界格"));
            inspector.Add(TopButton("删除区域", () => canvas.DeleteWorldSelection()));
        }

        private void RemoveOwnedSceneItems(IReadOnlyList<string> boxIds)
        {
            if (boxIds == null || boxIds.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var ids = new HashSet<string>(boxIds);
            var active = SceneManager.GetActiveScene();
            var delete = new List<GameObject>();
            foreach (var block in ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true))
            {
                if (block == null || block.gameObject.scene != active || !ids.Contains(block.PlanningBoxId)) continue;
                // Only planning-owned instances under the generated root are in scope.
                if (block.transform.root.name != "__PlanningMapGenerated") continue;
                delete.Add(block.gameObject);
            }
            bool changed = false;
            foreach (var target in delete)
            {
                if (target == null) continue;
                Object.DestroyImmediate(target); changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(active);
                sceneHasBeenApplied = true; syncSceneHandle = active.handle;
                CaptureSceneBindings(); RememberBindings(); SceneView.RepaintAll();
            }
        }

        private void RemoveOwnerSceneContainers(IReadOnlyList<PlanningRoom> owners)
        {
            if (owners == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var owner in owners)
            {
                var container = PlanningSceneBuilder.FindRoomContainer(owner);
                var ids = new List<string>(); foreach (var box in owner.boxes) ids.Add(box.id);
                RemoveOwnedSceneItems(ids);
                if (container != null && container.scene == SceneManager.GetActiveScene() && container.transform.childCount == 0)
                {
                    Object.DestroyImmediate(container);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }
    }
}
