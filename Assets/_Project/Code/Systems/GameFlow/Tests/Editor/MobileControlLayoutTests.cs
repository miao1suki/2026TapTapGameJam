using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow.Tests
{
    public sealed class MobileControlLayoutTests
    {
        private const string LayoutKey = "ShapeOfColor.TouchLayout.v1";
        private static readonly FieldInfo CachedData = typeof(MobileControlLayout)
            .GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        private bool hadLayout;
        private string savedLayout;
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            hadLayout = PlayerPrefs.HasKey(LayoutKey);
            savedLayout = PlayerPrefs.GetString(LayoutKey);
            MobileControlLayout.Reset();
            root = new GameObject("Mobile layout regression", typeof(RectTransform));
            root.SetActive(false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1280, 720);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (hadLayout) PlayerPrefs.SetString(LayoutKey, savedLayout);
            else PlayerPrefs.DeleteKey(LayoutKey);
            PlayerPrefs.Save();
            CachedData.SetValue(null, null);
        }

        [Test]
        public void BuiltHud_UsesTheSameDefaultsAsLayoutReset()
        {
            var hud = Build();
            Assert.That(hud.Controls.childCount, Is.EqualTo(15));
            foreach (RectTransform button in hud.Controls)
            {
                MobileGameplayControls.GetDefaultLayout(button.name, out var anchor, out var position);
                Assert.That(button.anchorMin, Is.EqualTo(anchor), button.name);
                Assert.That(button.anchorMax, Is.EqualTo(anchor), button.name);
                Assert.That(button.anchoredPosition, Is.EqualTo(position), button.name);
                Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(124, 124)), button.name);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1560, 720)]
        [TestCase(1600, 720)]
        [TestCase(1280, 960)]
        public void DownButton_LeavesClearTouchGapsAtLandscapeRatios(float width, float height)
        {
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
            var hud = Build();
            Rect down = TouchRect(hud.Controls, "下");
            Rect right = TouchRect(hud.Controls, "右");
            Rect jump = TouchRect(hud.Controls, "左跳跃");
            Assert.That(down.xMin - right.xMax, Is.EqualTo(28).Within(.01f));
            Assert.That(jump.yMin - down.yMax, Is.EqualTo(28).Within(.01f));
            Assert.That(down.Overlaps(right), Is.False);
            Assert.That(down.Overlaps(jump), Is.False);
        }

        [Test]
        public void PauseButton_IsInsideTheUpperLeftSafeArea()
        {
            var hud = Build();
            var pause = (RectTransform)hud.Controls.Find("暂停");
            Assert.That(pause.anchorMin, Is.EqualTo(Vector2.up));
            Assert.That(pause.anchorMax, Is.EqualTo(Vector2.up));
            Rect hit = TouchRect(hud.Controls, "暂停");
            Assert.That(hit.xMin - hud.Controls.rect.xMin, Is.EqualTo(14).Within(.01f));
            Assert.That(hud.Controls.rect.yMax - hit.yMax, Is.EqualTo(14).Within(.01f));
        }

        [Test]
        public void LayoutEditorReset_RestoresTheUpdatedDefaultPositions()
        {
            var hud = Build();
            foreach (string label in new[] { "右", "下", "暂停" })
            {
                var button = (RectTransform)hud.Controls.Find(label);
                var drag = button.gameObject.AddComponent<MobileControlLayoutDrag>();
                drag.Configure(null, button, hud.Controls);
                button.anchorMin = button.anchorMax = new Vector2(.5f, .5f);
                button.anchoredPosition = Vector2.zero;
                button.sizeDelta = Vector2.one * 180;
                drag.RestoreDefault();
                MobileGameplayControls.GetDefaultLayout(label, out var anchor, out var position);
                Assert.That(button.anchorMin, Is.EqualTo(anchor), label);
                Assert.That(button.anchorMax, Is.EqualTo(anchor), label);
                Assert.That(button.anchoredPosition, Is.EqualTo(position), label);
                Assert.That(button.sizeDelta.x, Is.EqualTo(124), label);
            }
        }

        [Test]
        public void SavedCustomPositions_ArePreservedWhenApplyingLayout()
        {
            var hud = Build();
            foreach (string label in new[] { "右", "下", "暂停" })
            {
                var button = (RectTransform)hud.Controls.Find(label);
                button.anchorMin = button.anchorMax = new Vector2(.5f, .5f);
                button.anchoredPosition = new Vector2(15, 25);
                button.sizeDelta = Vector2.one * 160;
            }
            MobileControlLayout.Save(hud.Controls);
            CachedData.SetValue(null, null);
            foreach (string label in new[] { "右", "下", "暂停" })
            {
                var button = (RectTransform)hud.Controls.Find(label);
                button.anchoredPosition = Vector2.zero;
                button.sizeDelta = Vector2.one * 124;
            }
            hud.ApplySavedLayout();
            foreach (string label in new[] { "右", "下", "暂停" })
            {
                var button = (RectTransform)hud.Controls.Find(label);
                Assert.That(button.localPosition.x, Is.EqualTo(15).Within(.01f), label);
                Assert.That(button.localPosition.y, Is.EqualTo(25).Within(.01f), label);
                Assert.That(button.sizeDelta.x, Is.EqualTo(160), label);
            }
        }

        private MobileGameplayControls Build()
        {
            var hud = root.AddComponent<MobileGameplayControls>();
            hud.BuildView();
            return hud;
        }

        private static Rect TouchRect(RectTransform parent, string name)
        {
            var button = (RectTransform)parent.Find(name);
            Vector4 padding = button.GetComponent<Image>().raycastPadding;
            var corners = new Vector3[4];
            button.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x + padding.x, min.y + padding.y,
                max.x - padding.z, max.y - padding.w);
        }
    }
}
