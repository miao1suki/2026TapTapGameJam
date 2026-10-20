using System;
using Project.BlockFeatures;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;

namespace Project.BossLogic.Editor
{
    internal static class CinematicArtworkCloneAudit
    {
        [MenuItem(
            "Tools/2026TapTap/调试/验证演出物品最新贴图")]
        private static void Run()
        {
            try
            {
                VerifyKey("red", MechanismArtworkKind.RedKey);
                VerifyKey("blue", MechanismArtworkKind.BlueKey);
                VerifyKey("green", MechanismArtworkKind.GreenKey);
                VerifyCollectible();
                Debug.Log(
                    "[CinematicArtworkCloneAudit] PASS：红/蓝/绿钥匙和收集品的演出副本均使用当前最新 Sprite。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void VerifyKey(
            string color,
            MechanismArtworkKind kind)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ColorBlocks/Prefabs/ColorKey_" +
                    color +
                    ".prefab");
            VerifyClone(
                prefab,
                kind,
                "ColorKey_" + color);
        }

        private static void VerifyCollectible()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/WorldItems/Collectible.prefab");
            VerifyClone(
                prefab,
                MechanismArtworkKind.Collectible,
                "Collectible");
        }

        private static void VerifyClone(
            GameObject prefab,
            MechanismArtworkKind kind,
            string label)
        {
            Require(prefab != null, label + " 预制体缺失");
            GameObject instance =
                UnityEngine.Object.Instantiate(prefab);
            try
            {
                SpriteRenderer latest =
                    MechanismArtworkPresentation
                        .PrepareCinematicClone(
                            instance,
                            "BossTop",
                            32760);
                Sprite expected =
                    ObjectArtworkLibrary.Load()
                        .FirstFrame(kind);
                Require(latest != null, label + " 未生成最新美术 Renderer");
                Require(
                    latest.sprite == expected,
                    label + " 未使用 ObjectArtworkLibrary 最新帧");
                Require(
                    latest.sortingOrder == 32760,
                    label + " 演出排序未应用");
                foreach (Renderer renderer in
                         instance.GetComponentsInChildren<
                             Renderer>(true))
                {
                    Require(
                        renderer == latest ||
                        !renderer.enabled,
                        label + " 仍显示旧占位 Renderer");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    instance);
            }
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    message);
            }
        }
    }
}
