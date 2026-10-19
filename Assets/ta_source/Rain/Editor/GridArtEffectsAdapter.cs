using System;
using System.Collections.Generic;
using Project.ArtEffects;
using Project.Fireflies;
using Project.Fireflies.Editor;
using Project.GridArt.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.Rain.Editor
{
    /// <summary>Bridge lives in Assembly-CSharp-Editor, which may reference both TA and the reusable workbench asmdef.</summary>
    [InitializeOnLoad]
    public static class GridArtEffectsAdapter
    {
        static GridArtEffectsAdapter()
        {
            GridArtEffectsSource.Read = Read;
            GridArtEffectsSource.Create = Create;
            GridArtEffectsSource.BuildInspector = BuildInspector;
            Undo.undoRedoPerformed += GridArtEffectsSource.NotifyChanged;
        }

        public static IReadOnlyList<GridArtEffectRegion> Read(Scene scene)
        {
            var result = new List<GridArtEffectRegion>();
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return result;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RainSpriteField rain in root.GetComponentsInChildren<RainSpriteField>(true))
                {
                    if ((rain.hideFlags & HideFlags.DontSave) != 0) continue;
                    Color tint = rain.ColorId == "red" ? new Color(1f, .45f, .4f) :
                        rain.ColorId == "green" ? new Color(.45f, .9f, .4f) : new Color(.35f, .7f, 1f);
                    result.Add(new GridArtEffectRegion(rain, GridArtEffectKind.Rain, WorldBounds(rain.transform,
                        rain.AreaCells, rain.CellWorldSize), rain.gameObject.name, tint));
                }
                foreach (FireflyField field in root.GetComponentsInChildren<FireflyField>(true))
                {
                    if ((field.hideFlags & HideFlags.DontSave) != 0) continue;
                    result.Add(new GridArtEffectRegion(field, GridArtEffectKind.Fireflies, WorldBounds(field.transform,
                        field.AreaCells, field.CellWorldSize), field.gameObject.name, new Color(.9f, .9f, .4f)));
                }
            }
            return result;
        }

        public static Rect WorldBounds(Transform transform, Vector2 cells, float size)
        {
            Vector3 halfX = transform.rotation * Vector3.right * (cells.x * size * .5f);
            Vector3 halfY = transform.rotation * Vector3.up * (cells.y * size * .5f);
            Vector2 half = new Vector2(Mathf.Abs(halfX.x) + Mathf.Abs(halfY.x), Mathf.Abs(halfX.y) + Mathf.Abs(halfY.y));
            return new Rect((Vector2)transform.position - half, half * 2f);
        }

        public static Component Create(GridArtEffectSettings settings, RectInt cells, float cellSize)
        {
            if (settings == null || cells.width <= 0 || cells.height <= 0 || cells.width > 4096 || cells.height > 4096)
                throw new ArgumentException("请选择有效的区域范围（边长1～4096格）。");
            if (!float.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentException("关卡网格尺寸无效。");
            if (!RainRegionWindow.CanCreate()) throw new InvalidOperationException("请退出运行 / Prefab 模式，在普通Scene创建区域。");
            Rect world = GridArtEffectsSource.CellBounds(cells, cellSize);
            Vector2 center = world.center;
            Component result;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(settings.kind == GridArtEffectKind.Rain ? "工作台框画雨区" : "工作台框画萤火区");
            try
            {
                if (settings.kind == GridArtEffectKind.Rain)
                {
                    result = RainRegionWindow.CreateRegion((RainSpriteField.RainColor)Mathf.Clamp(settings.rainColor, 0, 2),
                        cells.size, Mathf.Clamp(settings.rainDensity, 0f, 100f), settings.rainSeed,
                        new Vector3(center.x, center.y, float.IsFinite(settings.rainDepth) ? settings.rainDepth : 1f));
                }
                else
                {
                    FireflyField field = FireflyRegionWindow.CreateRegion(cells.size, settings.fireflyCount,
                        settings.layoutSeed, settings.motionSeed, settings.pulseSeed, new Vector3(center.x, center.y, 0f));
                    Undo.RecordObject(field, "萤火可见深度");
                    field.SetRenderDepthCells(settings.fireflyDepthCells);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(field);
                    result = field;
                }
                // A same-root provider preserves the art map's grid without parenting the effect
                // under a generated gameplay root or stretching the individual pixel artwork.
                var grid = Undo.AddComponent<ArtEffectRegionGrid>(result.gameObject);
                Undo.RecordObject(grid, "美术区域网格尺寸");
                grid.Configure(cellSize); EditorUtility.SetDirty(grid);
                if (result is RainSpriteField rain) rain.RefreshPreview();
                if (result is FireflyField fireflies) fireflies.RefreshPreview();
                PrefabUtility.RecordPrefabInstancePropertyModifications(result);
                EditorUtility.SetDirty(result); EditorSceneManager.MarkSceneDirty(result.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            GridArtEffectsSource.NotifyChanged(); return result;
        }

        private static VisualElement BuildInspector(Component target, Action changed)
        {
            if (target is not RainSpriteField && target is not FireflyField) return null;
            var root = new VisualElement { name = "art-effect-inspector" };
            root.Add(new Label(target is RainSpriteField ? "当前雨区" : "当前萤火区域")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, marginBottom = 4 } });
            var name = new TextField("名称") { value = target.gameObject.name, isDelayed = true };
            name.RegisterValueChangedCallback(e =>
            { Undo.RecordObject(target.gameObject, "美术区域名称"); target.gameObject.name = e.newValue; Dirty(target); changed?.Invoke(); }); root.Add(name);
            var position = new Vector3Field("区域中心（世界）") { value = target.transform.position };
            position.RegisterValueChangedCallback(e =>
            {
                if (!float.IsFinite(e.newValue.x) || !float.IsFinite(e.newValue.y) || !float.IsFinite(e.newValue.z)) return;
                Undo.RecordObject(target.transform, "移动美术区域"); target.transform.position = e.newValue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform); Dirty(target); changed?.Invoke();
            }); root.Add(position);
            var data = new SerializedObject(target);
            if (target is RainSpriteField)
            {
                Field(root, data, "color", "雨滴颜色");
                Field(root, data, "areaWidth", "区域宽度（格）"); Field(root, data, "areaHeight", "区域高度（格）");
                Field(root, data, "density", "密度 / 格宽（最多512颗）"); Field(root, data, "fallSpeed", "下落速度（格/秒）");
                var advanced = new Foldout { text = "随机与动画", value = false }; root.Add(advanced);
                Field(advanced, data, "randomSeed", "固定种子"); Field(advanced, data, "randomness", "随机性");
                Field(advanced, data, "speedVariation", "速度差异"); Field(advanced, data, "frameRate", "四帧动画速度");
                Field(advanced, data, "reachBottomChance", "到底概率"); Field(advanced, data, "noiseStrength", "噪波强度");
            }
            else
            {
                Field(root, data, "areaSize", "活动范围（格）"); Field(root, data, "fireflyCount", "数量（最多128只）");
                Field(root, data, "renderDepthCells", "可见平面（格）"); Field(root, data, "flightSpeed", "游动速度（格/秒）");
                var glow = new Foldout { text = "微光、种子与补光", value = false }; root.Add(glow);
                Field(glow, data, "glowRadius", "光晕半径（格）"); Field(glow, data, "glowIntensity", "光晕强度");
                Field(glow, data, "haloColor", "光晕颜色"); Field(glow, data, "pixelColor", "硬点颜色");
                Field(glow, data, "layoutSeed", "布局种子"); Field(glow, data, "motionSeed", "游动种子"); Field(glow, data, "pulseSeed", "呼吸种子");
                Field(glow, data, "detectionRadius", "驱散半径（格）"); Field(glow, data, "realtimeLightCount", "实时灯（默认0，最多4）");
                glow.Add(new HelpBox("移动端建议关闭实时灯。硬点固定1美术像素，柔光不缩放硬点。", HelpBoxMessageType.Info));
            }
            root.TrackSerializedObjectValue(data, _ =>
            {
                if (target == null) return;
                if (target is RainSpriteField rain) rain.RefreshPreview();
                if (target is FireflyField field) field.RefreshPreview();
                Dirty(target); changed?.Invoke();
            });
            root.Bind(data); return root;
        }

        private static void Field(VisualElement parent, SerializedObject data, string property, string label)
        {
            SerializedProperty value = data.FindProperty(property);
            if (value != null) parent.Add(new PropertyField(value, label));
        }

        private static void Dirty(Component value)
        {
            EditorUtility.SetDirty(value); PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            EditorSceneManager.MarkSceneDirty(value.gameObject.scene); SceneView.RepaintAll();
            GridArtEffectsSource.NotifyChanged();
        }
    }

    [CustomEditor(typeof(ArtEffectRegionGrid))]
    public sealed class ArtEffectRegionGridEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("工作台创建区域时记录的关卡格尺寸。保持独立根，不以Transform缩放改变原像素。", HelpBoxMessageType.Info));
            var cell = new PropertyField(serializedObject.FindProperty("cellWorldSize"), "每格世界尺寸");
            cell.SetEnabled(false); root.Add(cell); root.Bind(serializedObject); return root;
        }
    }
}
