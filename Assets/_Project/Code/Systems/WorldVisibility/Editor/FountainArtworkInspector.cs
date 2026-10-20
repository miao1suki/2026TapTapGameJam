using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.WorldVisibility.Editor
{
    [CustomEditor(typeof(FountainArtwork)),CanEditMultipleObjects]
    public sealed class FountainArtworkInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = VisibilityInspectorUI.Root("喷泉动画表现","仅负责八帧循环，不含碰撞、收集门槛、Boss或结局逻辑。82×66原生像素按16有效像素/格显示，完整留白不裁切。");
            var group = VisibilityInspectorUI.Group(root,"播放与尺寸");
            VisibilityInspectorUI.Field(group,serializedObject,"playing","播放动画");
            VisibilityInspectorUI.Field(group,serializedObject,"framesPerSecond","每秒帧数");
            VisibilityInspectorUI.Field(group,serializedObject,"cellWorldSize","单格世界尺寸");
            var frames = VisibilityInspectorUI.Group(root,"动画资源",false);
            VisibilityInspectorUI.Field(frames,serializedObject,"frames","完整序列帧"); return root;
        }
    }
}
