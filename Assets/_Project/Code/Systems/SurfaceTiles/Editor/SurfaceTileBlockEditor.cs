using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.SurfaceTiles.Editor
{
    [CustomEditor(typeof(SurfaceTileBlock))]
    public sealed class SurfaceTileBlockEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            var title = new Label("表面瓦片方块");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            root.Add(title);
            root.Add(new PropertyField(
                serializedObject.FindProperty("m_Script")));

            Foldout paint = CreateFoldout("绘制配置", true);
            paint.Add(new PropertyField(
                serializedObject.FindProperty("palette"),
                "瓦片库"));
            paint.Add(new PropertyField(
                serializedObject.FindProperty("cellSize"),
                "单格尺寸"));
            paint.Add(new PropertyField(
                serializedObject.FindProperty("transparentBase"),
                "隐藏基础渲染器"));
            root.Add(paint);

            Foldout placements = CreateFoldout("贴画记录", false);
            placements.Add(new PropertyField(
                serializedObject.FindProperty("placements"),
                "贴画列表"));
            root.Add(placements);

            SurfaceTileBlock block = (SurfaceTileBlock)target;
            Foldout status = CreateFoldout("烘焙状态", true);
            status.Add(CreateReadOnlyRow(
                "方块编号",
                () => block != null && !string.IsNullOrWhiteSpace(
                    block.BlockId)
                    ? block.BlockId
                    : "尚未生成"));
            status.Add(CreateReadOnlyRow(
                "网格尺寸",
                () => block != null
                    ? block.GetGridSize(SurfaceTileFace.Front)
                        .ToString()
                    : "无"));
            status.Add(CreateReadOnlyRow(
                "烘焙状态",
                () => block != null && block.BakeUpToDate
                    ? "已同步"
                    : "待烘焙"));
            root.Add(status);

            var help = new HelpBox(
                "贴画和烘焙由表面瓦片工具维护；这里用于检查单个方块的作者数据。",
                HelpBoxMessageType.Info);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            root.Bind(serializedObject);
            return root;
        }

        private static Foldout CreateFoldout(
            string title,
            bool expanded)
        {
            var foldout = new Foldout
            {
                text = title,
                value = expanded
            };
            foldout.style.marginTop = 6f;
            return foldout;
        }

        private static VisualElement CreateReadOnlyRow(
            string label,
            System.Func<string> value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;

            var name = new Label(label);
            name.style.width = 88f;
            name.style.opacity = .72f;
            row.Add(name);

            var content = new Label();
            content.style.flexGrow = 1f;
            content.style.whiteSpace = WhiteSpace.Normal;
            row.Add(content);

            System.Action refresh = () => content.text =
                value != null ? value() ?? "无" : "无";
            refresh();
            row.schedule.Execute(refresh).Every(150);
            return row;
        }
    }
}
