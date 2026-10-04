using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GameFlow.Editor
{
    public sealed class GameSceneNavigatorWindow : EditorWindow
    {
        private const string PlayerPrefabPath =
            "Assets/_Project/Content/Player/RuntimePlayer.prefab";
        private const string GoalMaterialPath =
            "Assets/_Project/Content/Materials/Prototype/Goal.mat";

        private readonly Color panelColor = new Color(0.09f, 0.12f, 0.17f, 1f);
        private EnumField levelIdField;
        private Label currentSceneLabel;

        public static void Open()
        {
            GetWindow<GameSceneNavigatorWindow>("场景导航");
        }

        public void CreateGUI()
        {
            minSize = new Vector2(520f, 640f);
            VisualElement root = rootVisualElement;
            root.style.backgroundColor = new Color(0.045f, 0.06f, 0.085f, 1f);
            root.style.paddingLeft = 12f;
            root.style.paddingRight = 12f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8f;
            Label title = new Label("2026TapTap · 场景导航");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.75f, 0.9f, 1f);
            title.style.flexGrow = 1f;
            header.Add(title);
            Button validate = new Button(GameFlowProjectValidator.ValidateFromMenu)
            {
                text = "检查配置",
            };
            header.Add(validate);
            root.Add(header);

            currentSceneLabel = new Label();
            currentSceneLabel.style.color = new Color(0.65f, 0.72f, 0.82f);
            currentSceneLabel.style.marginBottom = 8f;
            root.Add(currentSceneLabel);

            AddPlayLaunchPanel(root);

            ScrollView scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            root.Add(scroll);

            AddCategory(scroll, "启动入口", new[]
            {
                GameFlowSceneScaffolder.BootstrapPath,
            });
            AddCategory(scroll, "常驻系统", GameFlowSceneScaffolder.PersistentScenePaths);
            AddCategory(scroll, "流程界面", new[]
            {
                GameFlowSceneScaffolder.MainMenuPath,
                GameFlowSceneScaffolder.EndingPath,
            });
            AddCategory(scroll, "正式关卡", new[]
            {
                GameFlowSceneScaffolder.Level01Path,
                GameFlowSceneScaffolder.Level02Path,
                GameFlowSceneScaffolder.Level03Path,
            });
            AddCategory(scroll, "开发与测试", FindDevelopmentScenes());
            AddAuthoringPanel(scroll);

            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            UpdateCurrentSceneLabel();
        }

        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene _, Scene __)
        {
            UpdateCurrentSceneLabel();
        }

        private void AddCategory(
            VisualElement parent,
            string title,
            IEnumerable<string> paths)
        {
            Foldout foldout = new Foldout
            {
                text = title,
                value = title != "开发与测试",
            };
            StylePanel(foldout);
            bool any = false;
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    continue;
                }

                any = true;
                foldout.Add(CreateSceneRow(path));
            }

            if (!any)
            {
                Label empty = new Label("暂无场景");
                empty.style.color = Color.gray;
                foldout.Add(empty);
            }
            parent.Add(foldout);
        }

        private void AddPlayLaunchPanel(VisualElement parent)
        {
            VisualElement panel = new VisualElement();
            panel.style.backgroundColor = panelColor;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 8f;
            panel.style.paddingBottom = 8f;
            panel.style.marginBottom = 8f;
            panel.style.borderTopLeftRadius = 6f;
            panel.style.borderTopRightRadius = 6f;
            panel.style.borderBottomLeftRadius = 6f;
            panel.style.borderBottomRightRadius = 6f;

            Toggle formal = new Toggle("Play 时使用正式 Bootstrap 流程")
            {
                value = GameFlowEditorPlayBridge.UseFormalFlow,
            };
            formal.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(formal);

            Label help = new Label();
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.color = new Color(0.65f, 0.72f, 0.82f);
            help.style.marginTop = 4f;
            panel.Add(help);

            void RefreshHelp(bool useFormal)
            {
                help.text = useFormal
                    ? "正式流程：从 Bootstrap 启动，加载全部常驻系统，再进入当前打开的正式关卡；用于完整联调与打包前检查。"
                    : "直接调试：从当前场景启动。正式关卡仍会自动补齐系统、玩家和相机；普通测试场景保持原样。";
                panel.style.borderLeftColor = useFormal
                    ? new Color(0.2f, 0.72f, 1f)
                    : new Color(1f, 0.65f, 0.2f);
                panel.style.borderLeftWidth = 4f;
            }

            formal.RegisterValueChangedCallback(evt =>
            {
                GameFlowEditorPlayBridge.UseFormalFlow = evt.newValue;
                RefreshHelp(evt.newValue);
            });
            RefreshHelp(formal.value);
            parent.Add(panel);
        }

        private VisualElement CreateSceneRow(string path)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3f;
            row.style.marginBottom = 3f;

            Label name = new Label(Path.GetFileNameWithoutExtension(path));
            name.tooltip = path;
            name.style.flexGrow = 1f;
            name.style.color = new Color(0.88f, 0.91f, 0.96f);
            row.Add(name);

            Button open = new Button(() => OpenScene(path, OpenSceneMode.Single))
            {
                text = "打开",
                tooltip = "单独打开；有未保存场景时会先询问",
            };
            row.Add(open);
            Button additive = new Button(() => OpenScene(path, OpenSceneMode.Additive))
            {
                text = "+ 叠加",
                tooltip = "以 Additive 方式与当前场景同时打开",
            };
            row.Add(additive);
            Button ping = new Button(() =>
                EditorGUIUtility.PingObject(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
            {
                text = "定位",
            };
            row.Add(ping);
            return row;
        }

        private void AddAuthoringPanel(VisualElement parent)
        {
            Foldout foldout = new Foldout
            {
                text = "当前关卡 · 入口与出口",
                value = true,
            };
            StylePanel(foldout);

            Label help = new Label(
                "在正式关卡中使用。位置优先取当前选中物体，否则取 Scene 视图中心。" +
                "创建后仍可直接拖动。第三关终点默认进入结束演出。");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.color = new Color(0.65f, 0.72f, 0.82f);
            help.style.marginBottom = 6f;
            foldout.Add(help);

            levelIdField = new EnumField("关卡编号", GameFlowSceneId.Level01);
            foldout.Add(levelIdField);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            Button spawn = new Button(CreateOrSelectSpawn)
            {
                text = "创建 / 选中玩家出生点",
            };
            spawn.style.flexGrow = 1f;
            buttons.Add(spawn);
            Button goal = new Button(CreateGoal)
            {
                text = "创建关卡终点",
            };
            goal.style.flexGrow = 1f;
            buttons.Add(goal);
            foldout.Add(buttons);
            parent.Add(foldout);
        }

        private void CreateOrSelectSpawn()
        {
            if (!TryGetLevelContext(out LevelSceneContext context, out GameFlowSceneId id))
            {
                return;
            }

            PlayerSpawnPoint existing = FindInScene<PlayerSpawnPoint>(context.gameObject.scene);
            GameObject spawnObject;
            if (existing != null)
            {
                spawnObject = existing.gameObject;
            }
            else
            {
                spawnObject = new GameObject("PlayerSpawn");
                Undo.RegisterCreatedObjectUndo(spawnObject, "创建玩家出生点");
                spawnObject.transform.SetParent(FindContentRoot(context).transform, true);
                spawnObject.transform.position = ResolvePlacementPosition();
                spawnObject.AddComponent<PlayerSpawnPoint>();
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Undo.RecordObject(context, "配置玩家出生点");
            context.Configure(id, spawnObject.transform, prefab);
            EditorUtility.SetDirty(context);
            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            Selection.activeGameObject = spawnObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void CreateGoal()
        {
            if (!TryGetLevelContext(out LevelSceneContext context, out GameFlowSceneId id))
            {
                return;
            }

            GameObject goalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goalObject.name = id == GameFlowSceneId.Level03
                ? "LevelGoal_EndGame"
                : $"LevelGoal_To_{DefaultNext(id)}";
            Undo.RegisterCreatedObjectUndo(goalObject, "创建关卡终点");
            goalObject.transform.SetParent(FindContentRoot(context).transform, true);
            goalObject.transform.position = ResolvePlacementPosition() + Vector3.up;
            goalObject.transform.localScale = new Vector3(1.2f, 2.4f, 1.2f);
            BoxCollider collider = goalObject.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            LevelGoal goal = goalObject.AddComponent<LevelGoal>();
            bool endsGame = id == GameFlowSceneId.Level03;
            goal.Configure(DefaultNext(id), endsGame, 0.35f, goalObject.transform);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(GoalMaterialPath);
            if (material != null)
            {
                goalObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            Selection.activeGameObject = goalObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private bool TryGetLevelContext(
            out LevelSceneContext context,
            out GameFlowSceneId id)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            id = (GameFlowSceneId)levelIdField.value;
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);
            if (catalog != null && catalog.TryGetIdForPath(scene.path, out GameFlowSceneId inferred))
            {
                id = inferred;
                levelIdField.SetValueWithoutNotify(id);
            }

            if (id != GameFlowSceneId.Level01 &&
                id != GameFlowSceneId.Level02 &&
                id != GameFlowSceneId.Level03)
            {
                EditorUtility.DisplayDialog(
                    "不是正式关卡",
                    "请先打开 Level_01、Level_02 或 Level_03。",
                    "知道了");
                context = null;
                return false;
            }

            context = FindInScene<LevelSceneContext>(scene);
            if (context == null)
            {
                GameObject root = new GameObject($"__Level_{id}");
                Undo.RegisterCreatedObjectUndo(root, "创建关卡上下文");
                root.AddComponent<GameFlowSceneRoot>().Configure(id);
                context = root.AddComponent<LevelSceneContext>();
                context.Configure(id, null, AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            }
            return true;
        }

        private static GameObject FindContentRoot(LevelSceneContext context)
        {
            Transform child = context.transform.Find("LevelContent");
            if (child != null)
            {
                return child.gameObject;
            }

            GameObject content = new GameObject("LevelContent");
            Undo.RegisterCreatedObjectUndo(content, "创建关卡内容根节点");
            content.transform.SetParent(context.transform, false);
            return content;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }

        private static Vector3 ResolvePlacementPosition()
        {
            if (Selection.activeTransform != null &&
                Selection.activeTransform.gameObject.scene ==
                EditorSceneManager.GetActiveScene())
            {
                return Selection.activeTransform.position;
            }

            return SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
        }

        private static GameFlowSceneId DefaultNext(GameFlowSceneId id)
        {
            switch (id)
            {
                case GameFlowSceneId.Level01:
                    return GameFlowSceneId.Level02;
                case GameFlowSceneId.Level02:
                    return GameFlowSceneId.Level03;
                default:
                    return GameFlowSceneId.Ending;
            }
        }

        private static void OpenScene(string path, OpenSceneMode mode)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                EditorSceneManager.SetActiveScene(loaded);
                return;
            }

            if (mode == OpenSceneMode.Single &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, mode);
            EditorSceneManager.SetActiveScene(scene);
        }

        private static IEnumerable<string> FindDevelopmentScenes()
        {
            List<string> paths = new List<string>();
            AddScenesInFolder(paths, "Assets/_Project/Development");
            AddScenesInFolder(paths, "Assets/Scenes");
            AddScenesInFolder(paths, "Assets/GJ_Tools");
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void AddScenesInFolder(List<string> target, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { folder });
            for (int index = 0; index < guids.Length; index++)
            {
                target.Add(AssetDatabase.GUIDToAssetPath(guids[index]));
            }
        }

        private void UpdateCurrentSceneLabel()
        {
            if (currentSceneLabel == null)
            {
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            currentSceneLabel.text = string.IsNullOrEmpty(scene.path)
                ? "当前：未保存场景"
                : $"当前：{scene.name}  ·  {scene.path}";
        }

        private void StylePanel(VisualElement element)
        {
            element.style.backgroundColor = panelColor;
            element.style.borderTopLeftRadius = 7f;
            element.style.borderTopRightRadius = 7f;
            element.style.borderBottomLeftRadius = 7f;
            element.style.borderBottomRightRadius = 7f;
            element.style.paddingLeft = 9f;
            element.style.paddingRight = 9f;
            element.style.paddingTop = 6f;
            element.style.paddingBottom = 7f;
            element.style.marginBottom = 7f;
        }
    }
}
