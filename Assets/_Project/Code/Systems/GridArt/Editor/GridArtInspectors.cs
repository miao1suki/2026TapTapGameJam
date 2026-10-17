using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    [CustomEditor(typeof(GridArtPalette))]
    public sealed class GridArtPaletteInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new ScrollView(); root.style.paddingLeft = root.style.paddingRight = 12;
            var palette = (GridArtPalette)target;
            root.Add(new HelpBox("地形自动连接边角；装饰按实际尺寸铺绘。素材编号保持稳定，不会因新增图片而改变。", HelpBoxMessageType.Info));
            var details = new VisualElement();
            void Show(int i)
            {
                details.Clear(); if (i < 0 || i >= palette.brushes.Count) return;
                var brush = palette.brushes[i];
                var foldout = new Foldout { text = brush.label, value = true };
                void Edit(System.Action a) { Undo.RecordObject(palette, "配置美术笔刷"); a(); EditorUtility.SetDirty(palette); }
                var name = new TextField("名称") { value = brush.label, isDelayed = true };
                name.RegisterValueChangedCallback(e => { Edit(() => brush.label = e.newValue); foldout.text = e.newValue; }); foldout.Add(name);
                var category = new TextField("分类") { value = brush.category, isDelayed = true };
                category.RegisterValueChangedCallback(e => Edit(() => brush.category = e.newValue)); foldout.Add(category);
                var auto = new Toggle("自动地形") { value = brush.autoTerrain }; foldout.Add(auto);
                var terrain = new VisualElement(); terrain.style.display = brush.autoTerrain ? DisplayStyle.Flex : DisplayStyle.None;
                var stamp = new VisualElement(); stamp.style.display = brush.autoTerrain ? DisplayStyle.None : DisplayStyle.Flex;
                auto.RegisterValueChangedCallback(e => { Edit(() => brush.autoTerrain = e.newValue); terrain.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None; stamp.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex; });
                var image = new ObjectField("图片") { objectType = typeof(Sprite), value = brush.sprite, allowSceneObjects = false };
                image.RegisterValueChangedCallback(e => { Edit(() => { brush.sprite = e.newValue as Sprite; brush.contentRect = new Rect(0,0,1,1); GridArtPixelLayout.RestoreNative(brush, out _); }); Show(i); }); stamp.Add(image);
                var size = new Vector2IntField("占格宽 / 高") { value = brush.Size };
                size.RegisterValueChangedCallback(e => Edit(() => brush.size = new Vector2Int(Mathf.Max(1,e.newValue.x),Mathf.Max(1,e.newValue.y)))); stamp.Add(size);
                foldout.Add(GridArtPixelSettings.Build(brush, Edit, () => Show(i)));
                var normal = new ObjectField("法线图集") { objectType = typeof(Texture2D), allowSceneObjects = false, value = brush.normalMap };
                normal.tooltip = "与颜色原图同尺寸、同排布，导入类型为法线贴图；仅在图层启用接收3D灯光时使用。";
                normal.RegisterValueChangedCallback(e => Edit(() => brush.normalMap = e.newValue as Texture2D)); foldout.Add(normal);
                string[] labels = { "左上角", "上边", "右上角", "左边", "中心", "右边", "左下角", "下边", "右下角", "内角：左下", "内角：右下", "内角：左上", "内角：右上" };
                for (int slot = 0; slot < labels.Length; slot++)
                {
                    int index = slot; var field = new ObjectField(labels[slot]) { objectType = typeof(Sprite), allowSceneObjects = false, value = brush.pieces != null && slot < brush.pieces.Length ? brush.pieces[slot] : null };
                    var feedback = new Label(); feedback.style.whiteSpace = WhiteSpace.Normal;
                    field.RegisterValueChangedCallback(e =>
                    {
                        if (!GridArtPixelLayout.IsRuleSprite(e.newValue as Sprite, brush.sourcePixelScale)) { field.SetValueWithoutNotify(e.previousValue); feedback.text = "自动边角需16×16原像素切片。"; return; }
                        feedback.text = "";
                        Edit(() => { if (brush.pieces == null || brush.pieces.Length != 13) System.Array.Resize(ref brush.pieces, 13); brush.pieces[index] = e.newValue as Sprite; });
                    }); terrain.Add(field); terrain.Add(feedback);
                }
                foldout.Add(terrain); foldout.Add(stamp); details.Add(foldout);
            }
            var choices = new System.Collections.Generic.List<string>();
            for (int i = 0; i < palette.brushes.Count; i++) choices.Add(i + " · " + palette.brushes[i].label);
            if (choices.Count > 0)
            {
                var picker = new PopupField<string>("编辑素材", choices, 0);
                picker.RegisterValueChangedCallback(e => Show(choices.IndexOf(e.newValue))); root.Add(picker); Show(0);
            }
            root.Add(details);
            root.Add(new Button(() => { Undo.RecordObject(palette,"新增美术笔刷"); palette.brushes.Add(new GridArtBrush()); EditorUtility.SetDirty(palette); root.Clear(); root.Add(CreateInspectorGUI()); }) { text = "新增笔刷" });
            return root;
        }
    }

    [CustomEditor(typeof(GridArtMap))]
    public sealed class GridArtMapInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement(); root.style.paddingLeft = root.style.paddingRight = 12;
            root.Add(new Label("关卡美术图"));
            var library = new ObjectField("素材库") { objectType = typeof(GridArtPalette), value = ((GridArtMap)target).palette };
            library.SetEnabled(false); root.Add(library);
            root.Add(new Label("图层、网格尺寸与绘图通过美术编辑器配置。"));
            root.Add(new Button(() => { GridArtWindow.Open(); GetWindow().SetMap((GridArtMap)target); }) { text = "打开美术图" });
            return root;
        }
        private static GridArtWindow GetWindow() => EditorWindow.GetWindow<GridArtWindow>();
    }

    [CustomEditor(typeof(GridArtScene))]
    public sealed class GridArtSceneInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement(); root.style.paddingLeft = root.style.paddingRight = 12;
            root.Add(new Label("美术网格与背景视差"));
            root.Add(new PropertyField(serializedObject.FindProperty("map"), "美术图"));
            root.Add(new PropertyField(serializedObject.FindProperty("targetCamera"), "跟随摄像机"));
            var pixels = new IntegerField("视差吸附像素/格") { value = ((GridArtScene)target).pixelsPerCell }; pixels.SetEnabled(false); root.Add(pixels);
            if (((GridArtScene)target).pixelsPerCell != Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell)
                root.Add(new Button(() => { Undo.RecordObject(target,"统一背景像素吸附"); ((GridArtScene)target).pixelsPerCell = Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell; EditorUtility.SetDirty(target); root.Clear(); root.Add(CreateInspectorGUI()); }) { text = "改用16像素网格吸附" });
            root.Add(new HelpBox("只移动背景层，不缩放图片。新背景按16源像素/格吸附；旧背景保留原值，确认后可统一。", HelpBoxMessageType.Info));
            root.Add(new Button(() => { var output = (GridArtScene)target; if (output.map != null) EditorWindow.GetWindow<GridArtWindow>("网格美术").SetMap(output.map); }) { text = "编辑美术图" });
            root.Add(new Button(() => ((GridArtScene)target).RebaseParallax()) { text = "重设视差参考点" });
            return root;
        }
    }
}
