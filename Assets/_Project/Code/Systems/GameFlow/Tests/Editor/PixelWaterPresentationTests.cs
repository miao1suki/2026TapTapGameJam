using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class PixelWaterPresentationTests
    {
        private const string Settings = "Assets/_Project/Resources/ColorBlocks/WaterPresentation.asset";

        [Test]
        public void RuntimeSettingsHaveAllThreeTaTilesAndRevealMaterials()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(Settings);
            Assert.NotNull(asset);
            var serialized = new SerializedObject(asset);
            var tiles = serialized.FindProperty("tiles");
            var materials = serialized.FindProperty("tileMaterials");
            Assert.AreEqual(3, tiles.arraySize);
            Assert.AreEqual(3, materials.arraySize);
            for (int i = 0; i < 3; i++)
            {
                var sprite = tiles.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                var material = materials.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                Assert.NotNull(sprite); Assert.NotNull(material);
                Assert.AreEqual(16, sprite.rect.width); Assert.AreEqual(17, sprite.rect.height);
                Assert.AreEqual(sprite.texture, material.mainTexture);
                Assert.True(material.HasProperty("_ColorRevealOpacity"));
            }
            var filter = serialized.FindProperty("filterMaterial").objectReferenceValue as Material;
            Assert.NotNull(filter); Assert.True(filter.HasProperty("_ColorRevealOpacity"));
            Assert.NotNull(serialized.FindProperty("bubblePrefab").objectReferenceValue);
            Assert.NotNull(serialized.FindProperty("bubbleMaterial").objectReferenceValue);
        }

        [TestCase("Up", 0)]
        [TestCase("Left", 1)]
        [TestCase("Right", 2)]
        [TestCase("Down", 3)]
        public void BubbleDirectionKeepsSerializedValues(string name, int value)
        {
            Type type = Type.GetType("Project.WaterBubbles.DirectionalWaterBubbleStream+FlowDirection, Project.WaterBubbles.Runtime");
            Assert.NotNull(type);
            Assert.AreEqual(value, Convert.ToInt32(Enum.Parse(type, name)));
        }

        [TestCase("PC")]
        [TestCase("Mobile")]
        public void BothRenderersIncludeWaterRefractionAndHsv(string platform)
        {
            var renderer = AssetDatabase.LoadMainAssetAtPath($"Assets/Settings/{platform}_Renderer.asset");
            Assert.NotNull(renderer);
            var features = new SerializedObject(renderer).FindProperty("m_RendererFeatures");
            bool water = false, hsv = false;
            for (int i = 0; i < features.arraySize; i++)
            {
                var feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feature == null) continue;
                if (feature.GetType().Name == "WaterUnderlayRendererFeature") water = true;
                if (feature.GetType().Name == "SelectiveHsvRendererFeature") hsv = true;
            }
            Assert.True(water); Assert.True(hsv);
        }
    }
}
