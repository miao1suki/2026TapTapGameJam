using UnityEngine;

namespace Project.Mechanisms
{
    [DisallowMultipleComponent]
    public sealed class DoorHint : MonoBehaviour
    {
        [SerializeField, InspectorName("所属门")] private DoorController owner;
        [SerializeField, InspectorName("提示素材")] private DoorHintStyle style;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform fallback;
        private MaterialPropertyBlock properties;
        public int Total { get; private set; }
        public int Pressed { get; private set; }
        public DoorController Owner => owner;
        public void Bind(DoorController value) { owner = value; Refresh(); }
        public void Refresh()
        {
            if (owner == null) return;
            Total = owner.Buttons.Count;
            Pressed = owner.PressedCount;
            if (style == null) style = Resources.Load<DoorHintStyle>("DoorHintStyle");
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            var sprite = style != null ? style.Resolve(Total, Pressed) : null;
            spriteRenderer.sprite = sprite;
            spriteRenderer.enabled = sprite != null;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            if (sprite != null)
                spriteRenderer.transform.localScale = Vector3.one * (.8f / Mathf.Max(.01f, sprite.bounds.size.x));
            else spriteRenderer.transform.localScale = Vector3.one;
            if (fallback == null)
            {
                fallback = new GameObject("孔位占位").transform;
                fallback.SetParent(transform, false);
                for (int index = 0; index < 3; index++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dot.name = "孔位 " + (index + 1);
                    dot.transform.SetParent(fallback, false);
                    dot.transform.localScale = new Vector3(.18f, .18f, .08f);
                    var collider = dot.GetComponent<Collider>();
                    collider.enabled = false;
                    if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                }
            }
            fallback.gameObject.SetActive(sprite == null && Total > 0);
            properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < fallback.childCount; index++)
            {
                var dot = fallback.GetChild(index);
                dot.gameObject.SetActive(index < Mathf.Min(Total, 3));
                dot.localPosition = new Vector3((index - (Mathf.Min(Total, 3) - 1) * .5f) * .25f, 0, 0);
                var color = index < Pressed ? new Color(.25f, .9f, .45f) : new Color(.22f, .22f, .22f);
                properties.Clear(); properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
                dot.GetComponent<Renderer>().SetPropertyBlock(properties);
            }
        }
    }
}
