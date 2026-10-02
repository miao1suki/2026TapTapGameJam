using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Project.CameraModes.Editor
{
    [Overlay(typeof(SceneView), "2D / 3D 相机", true)]
    public sealed class CameraModeSceneOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            return new CameraModePanel();
        }
    }
}
