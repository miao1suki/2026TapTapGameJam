using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.Fireflies.Editor
{
    public sealed class FireflyRegionWindow : EditorWindow
    {
        public const string PrefabPath = "Assets/ta_source/firefly/FireflyField.prefab";
        [SerializeField] private Vector2 areaCells = new Vector2(3, 2);
        [SerializeField] private int count = 22;
        [SerializeField] private Vector3 centerPosition = Vector3.zero;
        [SerializeField] private int layoutSeed = 107;
        [SerializeField] private int motionSeed = 293;
        [SerializeField] private int pulseSeed = 521;
        private Button create;
        private HelpBox unavailable;

        [MenuItem("Tools/2026TapTap/关卡美术/创建萤火虫区域")]
        public static void Open()
        {
            var window = GetWindow<FireflyRegionWindow>("创建萤火虫区域");
            window.minSize = new Vector2(340, 350);
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
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            root.Add(new Label("创建萤火虫区域")
            {
                style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
            });
            var body = new ScrollView();
            body.style.flexGrow = 1;
            root.Add(body);
            body.Add(new HelpBox("中性美术区域，无碰撞与玩法交互；硬光点为 1/16 格，实时补光默认关闭。", HelpBoxMessageType.Info));
            var data = new SerializedObject(this);
            body.Add(new PropertyField(data.FindProperty(nameof(areaCells)), "活动范围（格）"));
            body.Add(new PropertyField(data.FindProperty(nameof(count)), "萤火虫数量"));
            body.Add(new PropertyField(data.FindProperty(nameof(centerPosition)), "中心位置（世界坐标）"));
            body.Add(new Button(() =>
            {
                SceneView view = SceneView.lastActiveSceneView;
                if (view == null) return;
                data.Update();
                data.FindProperty(nameof(centerPosition)).vector3Value = new Vector3(view.pivot.x, view.pivot.y, centerPosition.z);
                data.ApplyModifiedProperties();
            }) { text = "使用 Scene 视图中心" });
            var seeds = new Foldout { text = "固定随机种子", value = false };
            seeds.Add(new PropertyField(data.FindProperty(nameof(layoutSeed)), "布局种子"));
            seeds.Add(new PropertyField(data.FindProperty(nameof(motionSeed)), "游动种子"));
            seeds.Add(new PropertyField(data.FindProperty(nameof(pulseSeed)), "呼吸种子"));
            body.Add(seeds);
            body.Add(new HelpBox("独立保存于 Scene，不写入 planning。交付关卡时仍需同时提交场景与规划存档。创建后在 Inspector 调整游动、驱散、光晕及补光。", HelpBoxMessageType.Info));
            unavailable = new HelpBox("请退出运行与 Prefab 编辑模式，切换到普通场景，并确认萤火虫预制体存在。", HelpBoxMessageType.Warning);
            body.Add(unavailable);
            create = new Button(() =>
            {
                if (!CanCreate()) return;
                data.ApplyModifiedProperties();
                FireflyField field = CreateRegion(areaCells, count, layoutSeed, motionSeed, pulseSeed, centerPosition);
                Selection.activeGameObject = field.gameObject;
                SceneView.FrameLastActiveSceneView();
            }) { text = "在当前场景创建萤火虫区域" };
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
                !EditorSceneManager.IsPreviewScene(scene) && PrefabStageUtility.GetCurrentPrefabStage() == null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        }

        public static FireflyField CreateRegion(Vector2 cells, int number, int layout, int motion, int pulse, Vector3 center)
        {
            if (!CanCreate()) throw new InvalidOperationException("萤火虫区域只能在非运行状态的普通场景中创建，且必须有对应预制体。");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "创建萤火虫区域");
            instance.name = "萤火虫区域";
            instance.transform.position = center;
            var field = instance.GetComponent<FireflyField>();
            field.Configure(cells, number, layout, motion, pulse);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(field);
            EditorUtility.SetDirty(field);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            return field;
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
