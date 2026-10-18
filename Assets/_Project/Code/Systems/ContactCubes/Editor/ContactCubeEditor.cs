using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ContactCubes.Editor
{
    [CustomEditor(typeof(CubeWaterJetArtwork))]
    public sealed class CubeWaterJetArtworkEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("接触水柱美术") { style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold } });
            var state = new Foldout { text = "只读运行状态", value = true };
            var label = new Label(); state.Add(label); root.Add(state);
            root.Add(new Label("参数在蓝色接触方块上设置；不产生水碰撞、不接管玩家。"));
            root.schedule.Execute(() => { if (target is CubeWaterJetArtwork art) label.text =
                $"气泡：{art.VisibleBubbleCount}/{CubeWaterJetArtwork.MaximumBubbles}　合批：{art.RendererCount}　流速：{art.BubbleSpeedCells:0.##} 格/秒"; }).Every(250);
            return root;
        }
    }
    [CustomEditor(typeof(ContactCubeMotion)), CanEditMultipleObjects]
    public sealed class ContactCubeMotionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var settings = new Foldout { text = "运动设置", value = true };
            settings.Add(new PropertyField(serializedObject.FindProperty("launchGravityGrace"), "水平喷出重力保护（秒）"));
            settings.Add(new PropertyField(serializedObject.FindProperty("settleVelocity"), "落稳竖直速度容差")); root.Add(settings);
            var state = new Label(); root.Add(state);
            root.schedule.Execute(() => { if (target is ContactCubeMotion motion) state.text = Application.isPlaying ? $"{(motion.IsSuspended ? "已挂起" : motion.IsSettled ? "已落稳" : "空中")}　{(motion.GravityProtected ? "重力保护" : "")}" : "未运行"; }).Every(250);
            return root;
        }
    }
    [CustomEditor(typeof(HorizontalCubeVine)), CanEditMultipleObjects]
    public sealed class HorizontalCubeVineEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var settings = new Foldout { text = "横向攀附", value = true };
            settings.Add(new PropertyField(serializedObject.FindProperty("traverseSpeed"), "横向攀附速度"));
            settings.Add(new PropertyField(serializedObject.FindProperty("contactMargin"), "上下接触容差（格）")); root.Add(settings);
            return root;
        }
    }
    [CustomEditor(typeof(CubeWaterJet))]
    public sealed class CubeWaterJetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("接触反应水柱") { style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new Label("参数由蓝色接触方块管理；TA水片与预填充气泡仅负责外观。"));
            var status = new Label(); root.Add(status);
            root.schedule.Execute(() => { if (target is CubeWaterJet jet) status.text = Application.isPlaying ?
                $"玩家推动：{jet.PlayerPushSpeed:0.##} 世界单位/秒　方向：{jet.Direction}" : "未运行"; }).Every(250);
            return root;
        }
    }

    [CustomEditor(typeof(ContactCube)), CanEditMultipleObjects]
    public sealed class ContactCubeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("接触反应方块") { style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold } });
            var identity = new Foldout { text = "物体配置", value = true };
            Add(identity, "color", "固定类型"); Add(identity, "shape", "实体碰撞箱"); Add(identity, "model", "模型"); root.Add(identity);
            var growth = new Foldout { text = "长度与接触", value = true };
            Add(growth, "initialLength", "初始长度（格）"); Add(growth, "maximumLength", "最大长度（格）");
            Add(growth, "stepSeconds", "变化间隔（秒）"); Add(growth, "contactToleranceCells", "接触容差（格）"); root.Add(growth);
            var jet = new Foldout { text = "水柱", value = false };
            Add(jet, "jetLengthCells", "射程（格）"); Add(jet, "jetWidthCells", "宽度（格）");
            Add(jet, "jetSpeed", "方块推动速度（格/秒）"); Add(jet,"jetPlayerPushMultiplier","玩家推动倍率");
            Add(jet,"jetBubbleDensity","气泡密度（排/格）"); Add(jet,"jetBubbleSpeed","气泡速度（格/秒）"); root.Add(jet);
            var debug = new Foldout { text = "运行状态", value = true };
            var label = new Label(); debug.Add(label); root.Add(debug);
            root.schedule.Execute(() => { if (target is ContactCube cube) label.text = Application.isPlaying ? $"状态：{StateName(cube.State)}　长度：{cube.Length} 格" : "未运行"; }).Every(250);
            return root;
        }
        private static string StateName(CubeReactionState state) => state == CubeReactionState.Growing ? "生长" : state == CubeReactionState.Shrinking ? "缩短" : state == CubeReactionState.Locked ? "锁长" : "静止";
        private void Add(VisualElement parent, string field, string label) => parent.Add(new PropertyField(serializedObject.FindProperty(field), label));
    }
}
