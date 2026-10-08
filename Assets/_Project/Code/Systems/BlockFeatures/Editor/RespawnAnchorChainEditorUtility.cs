using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.BlockFeatures.Editor
{
    internal static class RespawnAnchorChainEditorUtility
    {
        internal static List<RespawnAnchorFeature> GetSceneAnchors(
            Scene scene)
        {
            RespawnAnchorFeature[] all =
                UnityEngine.Object.FindObjectsByType<
                    RespawnAnchorFeature>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var result = new List<RespawnAnchorFeature>();
            for (int index = 0; index < all.Length; index++)
            {
                RespawnAnchorFeature anchor = all[index];
                if (anchor != null &&
                    anchor.gameObject.scene == scene)
                {
                    result.Add(anchor);
                }
            }

            result.Sort(CompareSceneOrder);
            return result;
        }

        internal static bool TryConnect(
            RespawnAnchorFeature source,
            RespawnAnchorFeature target,
            out string message)
        {
            if (source == null || target == null)
            {
                message = "请选择两个重生锚。";
                return false;
            }

            if (source == target)
            {
                message = "同一个重生锚不能连接到自己。";
                return false;
            }

            if (source.gameObject.scene != target.gameObject.scene)
            {
                message = "只能连接同一个场景内的重生锚。";
                return false;
            }

            List<RespawnAnchorFeature> anchors =
                GetSceneAnchors(source.gameObject.scene);
            if (!anchors.Contains(source) ||
                !anchors.Contains(target))
            {
                message = "重生锚不在当前场景中。";
                return false;
            }

            EnsureIds(anchors);
            List<RespawnAnchorFeature> mainChain =
                RespawnAnchorChainRuntime.BuildMainChain(anchors);
            if (mainChain.Count > 0)
            {
                RespawnAnchorFeature tail =
                    mainChain[mainChain.Count - 1];
                if (source != tail)
                {
                    message = "只能从当前链表的最高点继续连接。";
                    return false;
                }

                if (mainChain.Contains(target) ||
                    !string.IsNullOrEmpty(target.NextAnchorId) ||
                    HasIncoming(target, anchors))
                {
                    message = "目标出生点已经属于另一条链表。请先右键截断已有连线。";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(source.NextAnchorId) ||
                     !string.IsNullOrEmpty(target.NextAnchorId) ||
                     HasIncoming(source, anchors) ||
                     HasIncoming(target, anchors))
            {
                message = "只能同时存在一条链表。请先清除其他连线。";
                return false;
            }

            if (WouldCreateCycle(source, target, anchors))
            {
                message = "该连接会形成循环，已拒绝。";
                return false;
            }

            BeginEdit("连接重生锚", anchors);
            EnsureIds(anchors);
            source.SetNextAnchorId(target.AnchorId);
            RebuildMetadata(anchors);
            EndEdit(source.gameObject.scene);
            message = "连接已保存。";
            return true;
        }

        internal static bool TryDisconnectIncoming(
            RespawnAnchorFeature target,
            out string message)
        {
            if (target == null)
            {
                message = "请选择重生锚。";
                return false;
            }

            List<RespawnAnchorFeature> anchors =
                GetSceneAnchors(target.gameObject.scene);
            List<RespawnAnchorFeature> before =
                new List<RespawnAnchorFeature>(anchors);
            BeginEdit("断开重生锚连线", anchors);
            EnsureIds(anchors);
            bool changed = false;
            for (int index = 0; index < before.Count; index++)
            {
                RespawnAnchorFeature anchor = before[index];
                if (anchor.NextAnchorId == target.AnchorId)
                {
                    anchor.SetNextAnchorId(string.Empty);
                    changed = true;
                }
            }

            if (!changed)
            {
                EndEdit(target.gameObject.scene);
                message = "该重生锚没有进入连线。";
                return false;
            }

            ClearForwardLinks(target, anchors);
            RebuildMetadata(anchors);
            EndEdit(target.gameObject.scene);
            message = "已截断入口，后续尾段已解除编号。";
            return true;
        }

        internal static void EnsureIdsAndPersist(Scene scene)
        {
            List<RespawnAnchorFeature> anchors =
                GetSceneAnchors(scene);
            bool changed = false;
            for (int index = 0; index < anchors.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(
                        anchors[index].AnchorId))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            BeginEdit("生成重生锚身份", anchors);
            EnsureIds(anchors);
            RebuildMetadata(anchors);
            EndEdit(scene);
        }

        internal static bool InitializeSceneOrder(
            Scene scene,
            out string message)
        {
            List<RespawnAnchorFeature> anchors =
                GetSceneAnchors(scene);
            if (anchors.Count == 0)
            {
                message = "当前场景没有重生锚。";
                return false;
            }

            BeginEdit("初始化重生锚链表", anchors);
            EnsureIds(anchors);
            for (int index = 0; index < anchors.Count; index++)
            {
                string nextAnchorId = index + 1 < anchors.Count
                    ? anchors[index + 1].AnchorId
                    : string.Empty;
                anchors[index].SetNextAnchorId(nextAnchorId);
            }

            RebuildMetadata(anchors);
            EndEdit(scene);
            message = "已按场景顺序初始化。";
            return true;
        }

        internal static bool ClearAll(
            Scene scene,
            out string message)
        {
            List<RespawnAnchorFeature> anchors =
                GetSceneAnchors(scene);
            if (anchors.Count == 0)
            {
                message = "当前场景没有重生锚。";
                return false;
            }

            BeginEdit("清空重生锚连接", anchors);
            EnsureIds(anchors);
            for (int index = 0; index < anchors.Count; index++)
            {
                anchors[index].SetNextAnchorId(string.Empty);
            }

            RebuildMetadata(anchors);
            EndEdit(scene);
            message = "已清空全部连接。";
            return true;
        }

        internal static void EnsureIds(
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            for (int index = 0; index < anchors.Count; index++)
            {
                anchors[index].EnsureRuntimeAnchorId();
            }
        }

        internal static void RebuildMetadata(
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            List<RespawnAnchorFeature> ordered =
                RespawnAnchorChainRuntime.BuildMainChain(anchors);
            var mainChain = new HashSet<RespawnAnchorFeature>();
            for (int index = 0; index < ordered.Count; index++)
            {
                mainChain.Add(ordered[index]);
            }

            if (mainChain.Count > 0)
            {
                for (int index = 0; index < anchors.Count; index++)
                {
                    RespawnAnchorFeature anchor = anchors[index];
                    if (!mainChain.Contains(anchor) &&
                        !string.IsNullOrEmpty(anchor.NextAnchorId))
                    {
                        anchor.SetNextAnchorId(string.Empty);
                    }
                }
            }

            IReadOnlyList<RespawnAnchorChainEntry> snapshot =
                RespawnAnchorChainRuntime.CreateSnapshot(ordered);
            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                int chainIndex = ordered.IndexOf(anchor);
                anchor.SetChainData(
                    anchor.NextAnchorId,
                    chainIndex,
                    chainIndex == 0,
                    snapshot);
                EditorUtility.SetDirty(anchor);
            }
        }

        private static bool WouldCreateCycle(
            RespawnAnchorFeature source,
            RespawnAnchorFeature target,
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            var byId = new Dictionary<string, RespawnAnchorFeature>(
                StringComparer.Ordinal);
            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (!string.IsNullOrEmpty(anchor.AnchorId))
                {
                    byId[anchor.AnchorId] = anchor;
                }
            }

            var visited = new HashSet<string>(
                StringComparer.Ordinal);
            string cursor = target.AnchorId;
            while (!string.IsNullOrEmpty(cursor) &&
                   visited.Add(cursor))
            {
                if (cursor == source.AnchorId)
                {
                    return true;
                }

                if (!byId.TryGetValue(
                        cursor,
                        out RespawnAnchorFeature anchor))
                {
                    return false;
                }

                cursor = anchor.NextAnchorId;
            }

            return !string.IsNullOrEmpty(cursor);
        }

        private static bool HasIncoming(
            RespawnAnchorFeature target,
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            if (target == null ||
                string.IsNullOrEmpty(target.AnchorId))
            {
                return false;
            }

            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (anchor != null &&
                    anchor.NextAnchorId == target.AnchorId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearForwardLinks(
            RespawnAnchorFeature start,
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            if (start == null)
            {
                return;
            }

            var byId = new Dictionary<string, RespawnAnchorFeature>(
                StringComparer.Ordinal);
            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (!string.IsNullOrEmpty(anchor.AnchorId))
                {
                    byId[anchor.AnchorId] = anchor;
                }
            }

            var visited = new HashSet<RespawnAnchorFeature>();
            RespawnAnchorFeature current = start;
            while (current != null && visited.Add(current))
            {
                string nextAnchorId = current.NextAnchorId;
                current.SetNextAnchorId(string.Empty);
                current = !string.IsNullOrEmpty(nextAnchorId) &&
                          byId.TryGetValue(
                              nextAnchorId,
                              out RespawnAnchorFeature next)
                    ? next
                    : null;
            }
        }

        private static void BeginEdit(
            string action,
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(action);
            var objects = new UnityEngine.Object[anchors.Count];
            for (int index = 0; index < anchors.Count; index++)
            {
                objects[index] = anchors[index];
            }

            Undo.RecordObjects(objects, action);
        }

        private static void EndEdit(
            Scene scene)
        {
            Undo.CollapseUndoOperations(
                Undo.GetCurrentGroup());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }

        private static int CompareSceneOrder(
            RespawnAnchorFeature left,
            RespawnAnchorFeature right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            Vector3 leftPosition = left.transform.position;
            Vector3 rightPosition = right.transform.position;
            int result = leftPosition.x.CompareTo(rightPosition.x);
            if (result != 0)
            {
                return result;
            }

            result = leftPosition.y.CompareTo(rightPosition.y);
            return result != 0
                ? result
                : left.GetInstanceID().CompareTo(
                    right.GetInstanceID());
        }
    }
}
