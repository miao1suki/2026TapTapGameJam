using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.GameFlow
{
    /// <summary>Versioned normalized safe-area layout; no scene or prefab modifications.</summary>
    public static class MobileControlLayout
    {
        private const string Key = "ShapeOfColor.TouchLayout.v1";
        [Serializable] public sealed class Entry { public string name; public Vector2 position; public float size = 124; }
        [Serializable] private sealed class Data { public List<Entry> buttons = new List<Entry>(); }
        private static Data data;
        private static MobileControlLayoutEditor activeEditor;
        public static bool IsEditing => activeEditor != null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Project.StartMenu.SettingsPanelController.TouchLayoutRequested -= OpenFromSettings;
            Project.StartMenu.SettingsPanelController.TouchLayoutRequested += OpenFromSettings;
            data = null;
        }
        private static void OpenFromSettings(TMP_FontAsset font) => Open(font);
        private static Data Current
        {
            get
            {
                if (data != null) return data;
                try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")); }
                catch (ArgumentException) { }
                data ??= new Data(); data.buttons ??= new List<Entry>();
                return data;
            }
        }
        public static void Apply(RectTransform button, RectTransform area)
        {
            Entry entry = Current.buttons.Find(e => e != null && e.name == button.name);
            if (entry == null || !float.IsFinite(entry.position.x) || !float.IsFinite(entry.position.y)) return;
            button.anchorMin = button.anchorMax = new Vector2(Mathf.Clamp01(entry.position.x), Mathf.Clamp01(entry.position.y));
            button.anchoredPosition = Vector2.zero;
            float size = float.IsFinite(entry.size) ? Mathf.Clamp(entry.size, 80, 200) : 124;
            button.sizeDelta = Vector2.one * size;
            Clamp(button, area);
        }
        public static void Clamp(RectTransform button, RectTransform area)
        {
            Vector3 local = button.localPosition;
            Vector2 half = button.rect.size * .5f;
            local.x = Mathf.Clamp(local.x, area.rect.xMin + half.x, area.rect.xMax - half.x);
            local.y = Mathf.Clamp(local.y, area.rect.yMin + half.y, area.rect.yMax - half.y);
            button.localPosition = local;
        }
        public static void Save(RectTransform area)
        {
            data = new Data();
            foreach (RectTransform button in area)
            {
                Vector3 local = button.localPosition;
                data.buttons.Add(new Entry { name = button.name, size = button.sizeDelta.x,
                    position = new Vector2(Mathf.InverseLerp(area.rect.xMin, area.rect.xMax, local.x),
                        Mathf.InverseLerp(area.rect.yMin, area.rect.yMax, local.y)) });
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        public static void Reset()
        { data = new Data(); PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        public static void Open(TMP_FontAsset font)
        {
            if (IsEditing) return;
            var root = new GameObject("触控布局", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(MobileControlLayoutEditor));
            activeEditor = root.GetComponent<MobileControlLayoutEditor>(); activeEditor.Build(font);
        }
    }

    public sealed class MobileControlLayoutEditor : MonoBehaviour
    {
        // Modal above pause/settings (1000) and hints (1200), below scene transitions (32767).
        public const int SortingOrder = 30000;
        private MobileGameplayControls preview;
        private RectTransform selected;
        private TextMeshProUGUI title;
        private TMP_FontAsset font;
        private void Update()
        {
            if (GameInput.WasTriggeredThisFrame(InputActionId.Cancel)) Destroy(gameObject);
        }
        public void Build(TMP_FontAsset value)
        {
            font = value;
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = SortingOrder;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = new GameObject("背景", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(transform, false);
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.color = new Color(.025f, .045f, .06f, .97f);
            var hud = new GameObject("布局预览", typeof(RectTransform)); hud.SetActive(false); hud.transform.SetParent(transform, false);
            var rect = hud.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            preview = hud.AddComponent<MobileGameplayControls>(); preview.Configure(value, null); preview.LayoutEditing = true;
            hud.SetActive(true);
            // Configure is called after Awake; update labels with the actual project font.
            foreach (var label in preview.GetComponentsInChildren<TextMeshProUGUI>(true)) label.font = value;
            foreach (var input in preview.GetComponentsInChildren<VirtualInputButton>(true)) input.enabled = false;
            foreach (var input in preview.GetComponentsInChildren<VirtualDirectionButton>(true)) input.enabled = false;
            foreach (var feedback in preview.GetComponentsInChildren<TouchPressFeedback>(true)) feedback.enabled = false;
            foreach (RectTransform item in preview.Controls)
            {
                item.gameObject.SetActive(true);
                item.GetComponent<Button>().onClick.RemoveAllListeners();
                var drag = item.gameObject.AddComponent<MobileControlLayoutDrag>(); drag.Configure(this, item, preview.Controls);
            }
            var bar = new GameObject("工具栏", typeof(RectTransform), typeof(ScreenSafeArea)).GetComponent<RectTransform>();
            bar.SetParent(transform, false); bar.GetComponent<ScreenSafeArea>().Refresh();
            title = Label(bar, "拖动按键调整位置 · 点选后调整大小", new Vector2(0, -38), new Vector2(950, 45));
            ToolbarButton(bar, "缩小", -330, () => Resize(-10));
            ToolbarButton(bar, "放大", -165, () => Resize(10));
            ToolbarButton(bar, "恢复默认", 0, ResetLayout);
            ToolbarButton(bar, "取消", 165, () => Destroy(gameObject));
            ToolbarButton(bar, "保存", 330, Save);
            var system = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            if (system != null) system.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
        }
        private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size)
        {
            var label = new GameObject("文字", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false); label.font = font; label.fontSize = 26;
            label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1);
            label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size; return label;
        }
        private void ToolbarButton(Transform parent, string text, float x, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, -105); rect.sizeDelta = new Vector2(150, 64);
            obj.GetComponent<Image>().color = new Color(.12f, .28f, .4f, 1);
            var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(action);
            var label = Label(rect, text, Vector2.zero, rect.sizeDelta); label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        }
        public void Select(RectTransform item)
        {
            selected = item; title.text = $"{item.name.Replace("左跳跃", "左侧跳跃")} · {item.sizeDelta.x:0}";
            foreach (RectTransform button in preview.Controls)
                button.GetComponent<Image>().color = button == item ? new Color(.12f, .55f, .75f, .95f) : new Color(.07f, .13f, .19f, .85f);
        }
        private void Resize(float delta)
        {
            if (selected == null) return;
            selected.sizeDelta = Vector2.one * Mathf.Clamp(selected.sizeDelta.x + delta, 80, 200);
            MobileControlLayout.Clamp(selected, preview.Controls); Select(selected);
        }
        private void ResetLayout()
        {
            // Reset is a preview until Save, just like all other edits.
            foreach (var drag in preview.GetComponentsInChildren<MobileControlLayoutDrag>(true)) drag.RestoreDefault();
            selected = null; title.text = "已恢复默认布局 · 保存后生效";
        }
        private void Save()
        {
            MobileControlLayout.Save(preview.Controls);
            foreach (var hud in UnityEngine.Object.FindObjectsByType<MobileGameplayControls>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hud != preview) hud.ApplySavedLayout();
            Destroy(gameObject);
        }
    }

    public sealed class MobileControlLayoutDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private MobileControlLayoutEditor owner;
        private RectTransform item, area;
        private int pointer = int.MinValue;
        private Vector2 offset;
        private Vector2 defaultAnchor, defaultPosition;
        public void Configure(MobileControlLayoutEditor editor, RectTransform value, RectTransform bounds)
        { owner = editor; item = value; area = bounds; MobileGameplayControls.GetDefaultLayout(item.name, out defaultAnchor, out defaultPosition); }
        public void RestoreDefault()
        { item.anchorMin = item.anchorMax = defaultAnchor; item.anchoredPosition = defaultPosition; item.sizeDelta = Vector2.one * 124; MobileControlLayout.Clamp(item, area); }
        public void OnPointerDown(PointerEventData e) => owner.Select(item);
        public void OnBeginDrag(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId; RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.pressPosition, e.pressEventCamera, out var point);
            offset = (Vector2)item.localPosition - point;
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var point);
            item.localPosition = point + offset; MobileControlLayout.Clamp(item, area);
        }
        public void OnEndDrag(PointerEventData e) { if (e.pointerId == pointer) pointer = int.MinValue; }
    }
}
