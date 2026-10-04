using System;
using System.Collections.Generic;
using System.IO;
using Project.CameraModes;
using Project.InputAbstraction;
using Project.InputRebinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.GameFlow.Editor
{
    public static class GameFlowSceneScaffolder
    {
        public const string CatalogPath =
            "Assets/_Project/Content/GameFlow/GameSceneCatalog.asset";
        public const string BootstrapPath =
            "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
        public const string CorePath =
            "Assets/_Project/Scenes/Systems/Systems_Core.unity";
        public const string InputPath =
            "Assets/_Project/Scenes/Systems/Systems_Input.unity";
        public const string AudioPath =
            "Assets/_Project/Scenes/Systems/Systems_Audio.unity";
        public const string CameraPath =
            "Assets/_Project/Scenes/Systems/Systems_Camera.unity";
        public const string UiPath =
            "Assets/_Project/Scenes/Systems/Systems_UI.unity";
        public const string MainMenuPath =
            "Assets/_Project/Scenes/Flow/MainMenu.unity";
        public const string Level01Path =
            "Assets/_Project/Scenes/Levels/Level_01/Level_01.unity";
        public const string Level02Path =
            "Assets/_Project/Scenes/Levels/Level_02/Level_02.unity";
        public const string Level03Path =
            "Assets/_Project/Scenes/Levels/Level_03/Level_03.unity";
        public const string EndingPath =
            "Assets/_Project/Scenes/Flow/Ending.unity";

        public static readonly string[] PersistentScenePaths =
        {
            CorePath,
            InputPath,
            AudioPath,
            CameraPath,
            UiPath,
        };

        public static readonly string[] CanonicalBuildScenePaths =
        {
            BootstrapPath,
            CorePath,
            InputPath,
            AudioPath,
            CameraPath,
            UiPath,
            MainMenuPath,
            Level01Path,
            Level02Path,
            Level03Path,
            EndingPath,
        };

        public static void Generate()
        {
            EnsureFolderForAsset(CatalogPath);
            for (int index = 0; index < CanonicalBuildScenePaths.Length; index++)
            {
                EnsureFolderForAsset(CanonicalBuildScenePaths[index]);
            }

            GameSceneCatalog catalog = CreateOrUpdateCatalog();
            CreateSceneIfMissing(BootstrapPath, () => BuildBootstrap(catalog));
            CreateSceneIfMissing(CorePath, () => BuildSystemRoot(GameSystemSceneKind.Core));
            CreateSceneIfMissing(InputPath, BuildInputScene);
            CreateSceneIfMissing(AudioPath, BuildAudioScene);
            CreateSceneIfMissing(CameraPath, BuildCameraScene);
            CreateSceneIfMissing(UiPath, BuildUiScene);
            CreateSceneIfMissing(MainMenuPath, () => BuildFlowScene(GameFlowSceneId.MainMenu));
            CreateSceneIfMissing(Level01Path, () => BuildLevelScene(GameFlowSceneId.Level01));
            CreateSceneIfMissing(Level02Path, () => BuildLevelScene(GameFlowSceneId.Level02));
            CreateSceneIfMissing(Level03Path, () => BuildLevelScene(GameFlowSceneId.Level03));
            CreateSceneIfMissing(EndingPath, () => BuildFlowScene(GameFlowSceneId.Ending));

            EditorBuildSettings.scenes = Array.ConvertAll(
                CanonicalBuildScenePaths,
                path => new EditorBuildSettingsScene(path, true));
            catalog = AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"场景生成后无法重新载入 Scene Catalog：{CatalogPath}");
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameFlowEditorPlayBridge.ApplyStartScenePreference();

            if (!GameFlowProjectValidator.Validate(out string report))
            {
                throw new InvalidOperationException(report);
            }

            Debug.Log(
                "游戏流程场景骨架已就绪：Bootstrap + 5 个常驻系统场景 + " +
                "主菜单 + 3 个关卡 + 结束演出。\n" + report);
        }

        private static GameSceneCatalog CreateOrUpdateCatalog()
        {
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameSceneCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(
                BootstrapPath,
                PersistentScenePaths,
                new[]
                {
                    new GameFlowSceneEntry(GameFlowSceneId.MainMenu, MainMenuPath, false),
                    new GameFlowSceneEntry(GameFlowSceneId.Level01, Level01Path, true),
                    new GameFlowSceneEntry(GameFlowSceneId.Level02, Level02Path, true),
                    new GameFlowSceneEntry(GameFlowSceneId.Level03, Level03Path, true),
                    new GameFlowSceneEntry(GameFlowSceneId.Ending, EndingPath, false),
                });
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void BuildBootstrap(GameSceneCatalog catalog)
        {
            GameObject root = new GameObject("__Bootstrap");
            GameFlowController controller = root.AddComponent<GameFlowController>();
            controller.Configure(catalog, GameFlowSceneId.MainMenu);
        }

        private static GameSystemSceneRoot BuildSystemRoot(GameSystemSceneKind kind)
        {
            GameObject root = new GameObject($"__System_{kind}");
            GameSystemSceneRoot marker = root.AddComponent<GameSystemSceneRoot>();
            marker.Configure(kind);
            return marker;
        }

        private static void BuildInputScene()
        {
            GameSystemSceneRoot root = BuildSystemRoot(GameSystemSceneKind.Input);
            GameObject service = new GameObject("InputService");
            service.transform.SetParent(root.transform, false);
            InputService input = service.AddComponent<InputService>();
            input.ConfigurePersistence(false);
            service.AddComponent<InputBindingBootstrap>();
        }

        private static void BuildAudioScene()
        {
            GameSystemSceneRoot root = BuildSystemRoot(GameSystemSceneKind.Audio);
            GameObject service = new GameObject("AudioService");
            service.transform.SetParent(root.transform, false);
            GameAudioService audioService = service.AddComponent<GameAudioService>();

            AudioSource music = CreateAudioSource(service.transform, "Music", true);
            AudioSource soundEffects = CreateAudioSource(
                service.transform,
                "SoundEffects",
                false);
            audioService.Configure(music, soundEffects);
        }

        private static AudioSource CreateAudioSource(
            Transform parent,
            string objectName,
            bool loop)
        {
            GameObject sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static void BuildCameraScene()
        {
            GameSystemSceneRoot root = BuildSystemRoot(GameSystemSceneKind.Camera);
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1f, -10f);

            Camera output = cameraObject.AddComponent<Camera>();
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            cameraObject.AddComponent<AudioListener>();
            CameraControlManager manager = cameraObject.AddComponent<CameraControlManager>();
            manager.ConfigureOutput(output);
            CameraModeController mode = cameraObject.AddComponent<CameraModeController>();
            CameraFollowController follow =
                cameraObject.AddComponent<CameraFollowController>();
            follow.Configure(manager, mode, null);
            follow.SetRequestedMode(CameraViewMode.Side2D, 0f, true);
        }

        private static void BuildUiScene()
        {
            PlayableGameUiBuilder.BuildCurrentScene();
        }

        private static void BuildLegacyUiScene()
        {
            GameSystemSceneRoot root = BuildSystemRoot(GameSystemSceneKind.UI);

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(root.transform, false);

            GameObject canvasObject = new GameObject(
                "GameUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlatformUILayoutController),
                typeof(GameUiRouter));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject mainMenu = CreatePanel(
                canvasObject.transform,
                "MainMenuScreen",
                new Color(0.035f, 0.055f, 0.09f, 0.98f));
            CreateText(
                mainMenu.transform,
                "Title",
                "2026TapTap",
                new Vector2(0f, 250f),
                new Vector2(900f, 150f),
                72,
                font);
            CreateText(
                mainMenu.transform,
                "Subtitle",
                "选择关卡",
                new Vector2(0f, 135f),
                new Vector2(600f, 80f),
                34,
                font);
            Button level01 = CreateButton(mainMenu.transform, "Level01", "关卡 1", new Vector2(0f, 35f), font);
            Button level02 = CreateButton(mainMenu.transform, "Level02", "关卡 2", new Vector2(0f, -65f), font);
            Button level03 = CreateButton(mainMenu.transform, "Level03", "关卡 3", new Vector2(0f, -165f), font);

            GameObject gameplay = CreatePanel(
                canvasObject.transform,
                "GameplayHud",
                Color.clear,
                false);
            Button menu = CreateButton(gameplay.transform, "MainMenu", "主菜单", new Vector2(-810f, 475f), font, new Vector2(210f, 64f));
            Button reload = CreateButton(gameplay.transform, "Reload", "重开关卡", new Vector2(-580f, 475f), font, new Vector2(210f, 64f));
            Button ending = CreateButton(gameplay.transform, "Ending", "测试结束演出", new Vector2(785f, 475f), font, new Vector2(260f, 64f));

            GameObject endingScreen = CreatePanel(
                canvasObject.transform,
                "EndingScreen",
                new Color(0.02f, 0.025f, 0.04f, 0.98f));
            CreateText(
                endingScreen.transform,
                "Title",
                "演出结束",
                new Vector2(0f, 100f),
                new Vector2(900f, 150f),
                64,
                font);
            Button endingMenu = CreateButton(
                endingScreen.transform,
                "ReturnToMenu",
                "返回主菜单",
                new Vector2(0f, -70f),
                font,
                new Vector2(320f, 78f));

            GameObject loading = CreatePanel(
                canvasObject.transform,
                "LoadingScreen",
                new Color(0.015f, 0.02f, 0.03f, 1f));
            CreateText(
                loading.transform,
                "LoadingLabel",
                "加载中…",
                Vector2.zero,
                new Vector2(500f, 100f),
                42,
                font);

            GameUiRouter router = canvasObject.GetComponent<GameUiRouter>();
            router.Configure(
                mainMenu,
                gameplay,
                null,
                endingScreen,
                loading,
                level01,
                null,
                null,
                null,
                reload,
                menu,
                endingMenu,
                null);

            mainMenu.SetActive(false);
            gameplay.SetActive(false);
            endingScreen.SetActive(false);
            loading.SetActive(true);
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Color color,
            bool raycastTarget = true)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            SetFullScreen(panel.GetComponent<RectTransform>());
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Font font)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Font font,
            Vector2? size = null)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size ?? new Vector2(360f, 76f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.48f, 0.82f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.16f, 0.6f, 0.98f, 1f);
            colors.pressedColor = new Color(0.04f, 0.32f, 0.62f, 1f);
            button.colors = colors;
            Text text = CreateText(
                buttonObject.transform,
                "Label",
                label,
                Vector2.zero,
                rect.sizeDelta,
                30,
                font);
            SetFullScreen(text.rectTransform);
            return button;
        }

        private static void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildFlowScene(GameFlowSceneId id)
        {
            GameObject root = new GameObject($"__Flow_{id}");
            root.AddComponent<GameFlowSceneRoot>().Configure(id);
            new GameObject("SceneContent").transform.SetParent(root.transform, false);
        }

        private static void BuildLevelScene(GameFlowSceneId id)
        {
            GameObject root = new GameObject($"__Level_{id}");
            root.AddComponent<GameFlowSceneRoot>().Configure(id);
            GameObject content = new GameObject("LevelContent");
            content.transform.SetParent(root.transform, false);
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(root.transform, false);
            root.AddComponent<LevelSceneContext>().Configure(id, spawn.transform);
        }

        private static void CreateSceneIfMissing(string path, Action build)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            build();
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"无法保存场景：{path}");
            }
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
            {
                return;
            }

            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
