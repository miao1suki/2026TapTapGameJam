using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    // Fit cards to the viewport, not ScrollView's unconstrained content width.
    public sealed class GridArtBrushGrid : VisualElement
    {
        private float lastWidth=-1;
        public static int Columns(float width) => Mathf.Max(1,Mathf.FloorToInt(width/94f));
        // Leave two UI points beyond the 3+3 margins: at 150% scale Unity rounds
        // those margins to 3.333 each, otherwise the last card unexpectedly wraps.
        public static float CardWidth(float width) => Mathf.Max(1,Mathf.Floor(width/Columns(width))-8f);
        public GridArtBrushGrid()
        {
            AddToClassList("art-brush-grid");
            style.width=Length.Percent(100); style.minWidth=0;
            RegisterCallback<GeometryChangedEvent>(_ => Reflow());
        }
        public void Reflow(bool force=false)
        {
            float width=contentRect.width;
            if (!float.IsFinite(width) || width<=0 || (!force && Mathf.Abs(width-lastWidth)<.5f)) return;
            lastWidth=width;
            foreach (var card in Children()) if (card.ClassListContains("art-brush"))
            {
                card.style.width=CardWidth(width); card.style.minWidth=0;
                card.style.flexGrow=0; card.style.flexShrink=0;
            }
        }
    }
}
