using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GridArt.Editor
{
    public static class GridArtGeometry
    {
        public static HashSet<Vector2Int> Cells(IEnumerable<Bounds> bounds, float size, int limit = 100000)
        {
            var result = new HashSet<Vector2Int>(); size = Mathf.Max(.01f, size);
            foreach (var b in bounds)
            {
                int left = Mathf.CeilToInt(b.min.x / size - .5f - .0001f);
                int bottom = Mathf.CeilToInt(b.min.y / size - .5f - .0001f);
                int right = Mathf.FloorToInt(b.max.x / size - .5f - .0001f);
                int top = Mathf.FloorToInt(b.max.y / size - .5f - .0001f);
                if ((long)(right - left + 1) * (top - bottom + 1) > limit) throw new InvalidOperationException("选区超过十万格，请分区铺绘。");
                for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
                { result.Add(new Vector2Int(x, y)); if (result.Count > limit) throw new InvalidOperationException("选区超过十万格，请分区铺绘。"); }
            }
            return result;
        }

        public static HashSet<Vector2Int> SelectedCells(float size, Scene scene)
        {
            var bounds = new List<Bounds>(); var seen = new HashSet<BoxCollider>();
            foreach (var selected in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(selected) || selected.scene != scene) continue;
                foreach (var box in selected.GetComponentsInChildren<BoxCollider>())
                    if (!box.isTrigger && box.enabled && seen.Add(box)) bounds.Add(box.bounds);
            }
            return Cells(bounds, size);
        }
    }
}
