#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InputAbstraction;
using Project.Privacy;
using Project.StartMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Project.GameFlow.Tests
{
    public sealed class MobileLayoutPointerTests
    {
        private const string Key = "ShapeOfColor.TouchLayout.v1";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root, eventRoot;
        private Touchscreen screen;
        private bool hadLayout;
        private string previousLayout;
        private InputPlatformMode previousPlatform;
        private System.IDisposable previewLease;

        [UnityTest]
        public IEnumerator WorldMenuSettings_ReceivesTouch_AndRebindsRetainedButtonAndBridge()
        {
            hadLayout = PlayerPrefs.HasKey(Key); previousLayout = PlayerPrefs.GetString(Key);
            MobileControlLayout.Reset();
            var input = InputService.EnsureInstance(); previousPlatform = input.ConfiguredPlatformMode;
            typeof(InputService).GetField("platformMode", Private).SetValue(input, InputPlatformMode.Mobile);
            eventRoot = new GameObject("World Menu Layout Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventRoot.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            screen = InputSystem.AddDevice<Touchscreen>();
            previewLease = GameplayPresentationGate.Suppress();
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/WorldMainMenu.prefab"));
            var menu = root.GetComponentInChildren<StartMenuController>();
            Assert.IsNotNull(typeof(StartMenuController).GetField("uiSdfFont", Private).GetValue(menu),
                "WorldMainMenu must resolve its serialized Cubic_11 SDF asset; a missing font makes the layout entry silently unusable.");
            Assert.IsNotNull(StartMenuTypography.SdfFont, "The instantiated menu must configure shared typography before settings opens.");
            menu.SetPresented(true);
            menu.ShowSettings();
            yield return new WaitForSecondsRealtime(.25f);
            var settings = root.GetComponentInChildren<SettingsPanelController>(true);
            var entry = (RectTransform)settings.transform.Find("Category List/Touch Layout");
            Assert.IsNotNull(entry, "The main menu must have the same mobile entry as the pause menu.");
            yield return Tap(entry);
            Assert.IsTrue(MobileControlLayout.IsEditing, "Actual main-menu touch must open the layout, not only a direct Open() call.");
            var editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            var preview = editor.GetComponentInChildren<MobileGameplayControls>(true);
            Assert.IsTrue(preview.LayoutEditing && preview.Controls.gameObject.activeInHierarchy);
            Assert.That(preview.Controls.Cast<Transform>().All(x => x.gameObject.activeInHierarchy), Is.True);
            Assert.IsTrue(GameplayPresentationGate.IsSuppressed, "Opening layout cannot release the menu's gameplay preview lease.");
            yield return Tap((RectTransform)editor.transform.Find("工具栏/取消"));
            Assert.IsFalse(MobileControlLayout.IsEditing);
            Assert.IsFalse(PlayerPrefs.HasKey(Key));

            // Serialized/runtime-created views survive a Play-mode script reload, their
            // nonpersistent UnityEvent listeners and static bridge do not.
            entry.GetComponent<Button>().onClick.RemoveAllListeners();
            typeof(SettingsPanelController).GetField("TouchLayoutRequested", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
            var router = eventRoot.AddComponent<GameUiRouter>();
            router.enabled = false; router.enabled = true;
            menu.ShowMain(); menu.ShowSettings();
            yield return null;
            Assert.That(settings.transform.Find("Category List/Touch Layout"), Is.SameAs(entry));
            Assert.That(settings.transform.Find("Category List").Cast<Transform>().Count(t => t.name == "Touch Layout"), Is.EqualTo(1));
            yield return Tap(entry);
            Assert.IsTrue(MobileControlLayout.IsEditing, "Re-enabled settings must repair both a retained button and the static bridge.");
            editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            preview = editor.GetComponentInChildren<MobileGameplayControls>();
            var right = (RectTransform)preview.Controls.Find("右");
            yield return Tap(right);
            yield return Tap((RectTransform)editor.transform.Find("工具栏/放大"));
            Assert.That(right.sizeDelta.x, Is.EqualTo(134));
            yield return Tap((RectTransform)editor.transform.Find("工具栏/保存"));
            Assert.IsFalse(MobileControlLayout.IsEditing);
            Assert.IsTrue(PlayerPrefs.HasKey(Key));
            Assert.IsTrue(settings.gameObject.activeInHierarchy && menu.IsPresented);
            yield return Tap(entry);
            editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            preview = editor.GetComponentInChildren<MobileGameplayControls>();
            Assert.That(preview.Controls.Find("右").GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(134));
            yield return Tap((RectTransform)editor.transform.Find("工具栏/取消"));
        }

        [UnityTest]
        public IEnumerator OfflinePolicy_CanScrollAndClose_WithoutLeavingTheGame()
        {
            hadLayout = PlayerPrefs.HasKey(Key); previousLayout = PlayerPrefs.GetString(Key);
            previousPlatform = InputService.EnsureInstance().ConfiguredPlatformMode;
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
            screen = InputSystem.AddDevice<Touchscreen>();
            yield return new WaitForSecondsRealtime(.4f);
            root.GetComponentInChildren<GameUiRouter>(true).SetPaused(true, false);
            var font = AssetDatabase.FindAssets("Cubic_11 t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).First(f => f != null);
            AndroidPrivacyPolicy.ShowPolicy(font);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(AndroidPrivacyPolicy.IsShowing);
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "隐私政策");
            var scroll = canvas.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            StringAssert.Contains("如何联系我们", scroll.content.GetComponent<TextMeshProUGUI>().text);
            yield return Tap((RectTransform)canvas.transform.Find("返回"));
            Assert.IsFalse(AndroidPrivacyPolicy.IsShowing);
        }

        [UnityTest]
        public IEnumerator PausedSettings_ReceivesTouch_SelectDragResizeCancelAndSave()
        {
            hadLayout = PlayerPrefs.HasKey(Key); previousLayout = PlayerPrefs.GetString(Key);
            MobileControlLayout.Reset();
            var input = InputService.EnsureInstance(); previousPlatform = input.ConfiguredPlatformMode;
            typeof(InputService).GetField("platformMode", Private).SetValue(input, InputPlatformMode.Mobile);
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
            if (EventSystem.current == null)
            {
                eventRoot = new GameObject("Layout Test Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventRoot.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            screen = InputSystem.AddDevice<Touchscreen>();
            yield return new WaitForSecondsRealtime(.4f);
            var ui = root.GetComponentInChildren<GameUiRouter>(true);
            ui.SetPaused(true, false);
            yield return new WaitForSecondsRealtime(.7f);
            var esc = ui.GetComponentInChildren<EscMenuController>(true);
            var page = (GameObject)typeof(EscMenuController).GetField("settingsPage", Private).GetValue(esc);
            typeof(EscMenuController).GetMethod("OpenPage", Private).Invoke(esc, new object[] { page, Color.white });
            yield return new WaitForSecondsRealtime(.5f);
            var entry = (RectTransform)page.transform.Find("Category List/Touch Layout");
            Assert.IsNotNull(entry);
            yield return Tap(entry);
            Assert.IsTrue(MobileControlLayout.IsEditing, "Settings entry must actually open the modal.");
            var editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            Assert.That(editor.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(1200));
            var preview = editor.GetComponentInChildren<MobileGameplayControls>();
            var right = (RectTransform)preview.Controls.Find("右");
            AssertTopHit(right);
            yield return Tap(right);
            Assert.That(editor.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.StartsWith("右 ·")), Is.True);
            Assert.That(GameInput.ReadVector2(InputActionId.Move), Is.EqualTo(Vector2.zero), "Editing must not press gameplay movement.");
            yield return Tap((RectTransform)editor.transform.Find("工具栏/放大"));
            Assert.That(right.sizeDelta.x, Is.EqualTo(134));
            Vector2 from = RectTransformUtility.WorldToScreenPoint(null, right.position);
            Vector2 to = from + new Vector2(55, 35);
            Vector3 old = right.position;
            yield return Touch(from, TouchPhase.Began);
            yield return Touch(to, TouchPhase.Moved);
            yield return Touch(to, TouchPhase.Moved);
            yield return Touch(to, TouchPhase.Ended);
            Assert.That(Vector3.Distance(old, right.position), Is.GreaterThan(5), "Dragging must move the selected button.");
            yield return Tap((RectTransform)editor.transform.Find("工具栏/取消"));
            Assert.IsFalse(MobileControlLayout.IsEditing);
            Assert.IsFalse(PlayerPrefs.HasKey(Key), "Cancel cannot persist edits.");
            yield return Tap(entry);
            editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            Assert.IsNotNull(editor);
            preview = editor.GetComponentInChildren<MobileGameplayControls>();
            right = (RectTransform)preview.Controls.Find("右");
            yield return Tap(right);
            yield return Tap((RectTransform)editor.transform.Find("工具栏/放大"));
            yield return Tap((RectTransform)editor.transform.Find("工具栏/保存"));
            Assert.IsFalse(MobileControlLayout.IsEditing);
            Assert.IsTrue(PlayerPrefs.HasKey(Key));
            Assert.That(root.GetComponentInChildren<MobileGameplayControls>(true).Controls.Find("右").GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(134));
        }

        private void AssertTopHit(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), rect.name);
            Assert.That(hits[0].gameObject, Is.EqualTo(rect.gameObject), "Underlying pause/settings must not intercept " + rect.name);
        }
        private IEnumerator Tap(RectTransform rect)
        {
            AssertTopHit(rect);
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            yield return Touch(position, TouchPhase.Began);
            yield return Touch(position, TouchPhase.Ended);
        }
        private IEnumerator Touch(Vector2 position, TouchPhase phase)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 11, position = position, phase = phase });
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var modal = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            if (modal != null) Object.Destroy(modal.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.name == "隐私政策") Object.Destroy(canvas.gameObject);
            if (root != null) Object.Destroy(root);
            if (eventRoot != null) Object.Destroy(eventRoot);
            if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
            previewLease?.Dispose(); previewLease = null;
            if (InputService.Instance != null) typeof(InputService).GetField("platformMode", Private).SetValue(InputService.Instance, previousPlatform);
            Time.timeScale = 1; AudioListener.pause = false;
            if (hadLayout) PlayerPrefs.SetString(Key, previousLayout); else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            typeof(MobileControlLayout).GetField("data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            yield return null;
        }
    }
}
#endif
