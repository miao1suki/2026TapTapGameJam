using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.Rain.Editor
{
    public sealed class RainRegionWindow : EditorWindow
    {
        private const string Directory = "Assets/ta_source/Rain/";

        [SerializeField] private RainSpriteField.RainColor rainColor = RainSpriteField.RainColor.Blue;
        [SerializeField] private Vector2 areaCells = new Vector2(8, 6);
        [SerializeField] private Vector3 position = new Vector3(0, 0, 1);
        [SerializeField] private float density = 18;
        [SerializeField] private int seed = 37;
        private Button create;
        private HelpBox unavailable;

        [MenuItem("Tools/2026TapTap/关卡美术/创建雨区")]
        public static void Open()
        {
            var window = GetWindow<RainRegionWindow>("创建雨区");
            window.minSize = new Vector2(340, 360);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            root.Add(new Label("创建像素雨区")
            {
                style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
            });
            var body = new ScrollView();
            body.style.flexGrow = 1;
            root.Add(body);
            body.Add(new HelpBox("独立美术对象，无碰撞与颜色交互。颜色随场景 HSV 恢复，不影响机关。", HelpBoxMessageType.Info));
            var data = new SerializedObject(this);
            body.Add(new PropertyField(data.FindProperty(nameof(rainColor)), "雨滴颜色"));
            body.Add(new PropertyField(data.FindProperty(nameof(areaCells)), "雨区范围（格）"));
            body.Add(new PropertyField(data.FindProperty(nameof(position)), "中心位置（世界坐标）"));
            var center = new Button(() =>
            {
                SceneView view = SceneView.lastActiveSceneView;
                if (view == null) return;
                data.Update();
                data.FindProperty(nameof(position)).vector3Value = new Vector3(view.pivot.x, view.pivot.y, position.z);
                data.ApplyModifiedProperties();
            }) { text = "使用 Scene 视图中心" };
            body.Add(center);
            var advanced = new Foldout { text = "分布参数", value = false };
            advanced.Add(new PropertyField(data.FindProperty(nameof(density)), "密度（每格宽度）"));
            advanced.Add(new PropertyField(data.FindProperty(nameof(seed)), "固定随机种子"));
            body.Add(advanced);
            body.Add(new HelpBox("雨区保存在 Scene，不写入关卡存档。协作时提交场景与对应 planning 存档。创建后在 Inspector 调整下落、随机性及范围。", HelpBoxMessageType.Info));
            unavailable = new HelpBox("请退出运行模式，并切换到可编辑的普通场景。", HelpBoxMessageType.Warning);
            body.Add(unavailable);
            create = new Button(() =>
            {
                if (!CanCreate()) return;
                data.ApplyModifiedProperties();
                RainSpriteField rain = CreateRegion(rainColor, areaCells, density, seed, position);
                Selection.activeGameObject = rain.gameObject;
                SceneView.FrameLastActiveSceneView();
            }) { text = "在当前场景创建雨区" };
            create.style.marginTop = 12;
            create.style.minHeight = 30;
            create.style.backgroundColor = new Color(.16f, .38f, .65f);
            create.style.color = Color.white;
            root.Add(create);
            root.Bind(data);
            UpdateAvailability();
        }

        public static bool CanCreate()
        {
            Scene scene = SceneManager.GetActiveScene();
            return !EditorApplication.isPlayingOrWillChangePlaymode && scene.IsValid() && scene.isLoaded &&
                !EditorSceneManager.IsPreviewScene(scene) && PrefabStageUtility.GetCurrentPrefabStage() == null;
        }

        public static RainSpriteField CreateRegion(RainSpriteField.RainColor color, Vector2 area,
            float dropsPerCell, int authorSeed, Vector3 center)
        {
            if (!CanCreate()) throw new InvalidOperationException("雨区只能在非运行状态的普通场景中创建。");
            string suffix = color == RainSpriteField.RainColor.Red ? "Red" :
                color == RainSpriteField.RainColor.Green ? "Green" : "Blue";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "Rain_" + suffix + ".prefab");
            if (prefab == null) throw new InvalidOperationException("雨区预制体缺失：Rain_" + suffix);
            Scene scene = SceneManager.GetActiveScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "创建雨区");
            instance.name = color == RainSpriteField.RainColor.Red ? "红色雨区" :
                color == RainSpriteField.RainColor.Green ? "绿色雨区" : "蓝色雨区";
            instance.transform.position = center;
            RainSpriteField rain = instance.GetComponent<RainSpriteField>();
            rain.Configure(color, area, dropsPerCell, authorSeed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rain);
            EditorUtility.SetDirty(rain);
            EditorSceneManager.MarkSceneDirty(scene);
            return rain;
        }

        private void OnPlayModeChanged(PlayModeStateChange _) => UpdateAvailability();
        private void OnSceneChanged(Scene _, Scene __) => UpdateAvailability();
        private void UpdateAvailability()
        {
            if (create == null || unavailable == null) return;
            bool available = CanCreate();
            create.SetEnabled(available);
            unavailable.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
