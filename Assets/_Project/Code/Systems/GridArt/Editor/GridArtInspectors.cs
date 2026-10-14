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
            root.Add(new HelpBox("地形使用九宫格及四张内拐角。装饰可设置多格尺寸；图片按占格铺满。", HelpBoxMessageType.Info));
            for (int i = 0; i < palette.brushes.Count; i++)
            {
                var brush = palette.brushes[i];
                var foldout = new Foldout { text = brush.label, value = i == 0 };
                void Edit(System.Action a) { Undo.RecordObject(palette, "配置美术笔刷"); a(); EditorUtility.SetDirty(palette); }
                var name = new TextField("名称") { value = brush.label, isDelayed = true };
                name.RegisterValueChangedCallback(e => { Edit(() => brush.label = e.newValue); foldout.text = e.newValue; }); foldout.Add(name);
                var auto = new Toggle("自动地形") { value = brush.autoTerrain }; foldout.Add(auto);
                var terrain = new VisualElement(); terrain.style.display = brush.autoTerrain ? DisplayStyle.Flex : DisplayStyle.None;
                var stamp = new VisualElement(); stamp.style.display = brush.autoTerrain ? DisplayStyle.None : DisplayStyle.Flex;
                auto.RegisterValueChangedCallback(e => { Edit(() => brush.autoTerrain = e.newValue); terrain.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None; stamp.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex; });
                var image = new ObjectField("图片") { objectType = typeof(Sprite), value = brush.sprite, allowSceneObjects = false };
                image.RegisterValueChangedCallback(e => Edit(() => brush.sprite = e.newValue as Sprite)); stamp.Add(image);
                var size = new Vector2IntField("占格宽 / 高") { value = brush.Size };
                size.RegisterValueChangedCallback(e => Edit(() => brush.size = new Vector2Int(Mathf.Max(1,e.newValue.x),Mathf.Max(1,e.newValue.y)))); stamp.Add(size);
                string[] labels = { "左上角", "上边", "右上角", "左边", "中心", "右边", "左下角", "下边", "右下角", "内角：左下", "内角：右下", "内角：左上", "内角：右上" };
                for (int slot = 0; slot < labels.Length; slot++)
                {
                    int index = slot; var field = new ObjectField(labels[slot]) { objectType = typeof(Sprite), allowSceneObjects = false, value = brush.pieces != null && slot < brush.pieces.Length ? brush.pieces[slot] : null };
                    field.RegisterValueChangedCallback(e => Edit(() => { if (brush.pieces == null || brush.pieces.Length != 13) System.Array.Resize(ref brush.pieces, 13); brush.pieces[index] = e.newValue as Sprite; })); terrain.Add(field);
                }
                foldout.Add(terrain); foldout.Add(stamp); root.Add(foldout);
            }
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
            root.Add(new PropertyField(serializedObject.FindProperty("palette"), "瓦片库"));
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
            root.Add(new PropertyField(serializedObject.FindProperty("pixelsPerCell"), "每格像素数"));
            root.Add(new HelpBox("仅移动背景层，不控制相机。每格像素数为零时关闭像素吸附。", HelpBoxMessageType.Info));
            root.Add(new Button(() => { var output = (GridArtScene)target; if (output.map != null) EditorWindow.GetWindow<GridArtWindow>("网格美术").SetMap(output.map); }) { text = "编辑美术图" });
            root.Add(new Button(() => ((GridArtScene)target).RebaseParallax()) { text = "重设视差参考点" });
            return root;
        }
    }
}
