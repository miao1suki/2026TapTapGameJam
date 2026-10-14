using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.GridArt.Editor
{
    public readonly struct GridArtQuad
    {
        public readonly Rect bounds;
        public readonly Rect uv;
        public readonly Texture2D texture;
        public GridArtQuad(Rect area, Rect sample, Texture2D source) { bounds = area; uv = sample; texture = source; }
    }

    public static class GridArtBuilder
    {
        public static List<GridArtQuad> BuildQuads(GridArtLayer layer, GridArtPalette palette)
        {
            var result = new List<GridArtQuad>();
            if (palette == null) return result;
            var cells = new Dictionary<Vector2Int, int>();
            foreach (var cell in layer.cells) cells[cell.position] = cell.brush;
            foreach (var cell in layer.cells)
            {
                var brush = palette.Get(cell.brush);
                if (brush == null) continue;
                if (!brush.autoTerrain)
                {
                    Add(result, brush.sprite, new Rect(cell.position, brush.Size), null);
                    continue;
                }
                for (int q = 0; q < 4; q++)
                {
                    int dx = q % 2 == 0 ? -1 : 1, dy = q < 2 ? -1 : 1;
                    var kind = GridArtRules.Resolve(
                        GridArtRules.Same(cells, cell.position + new Vector2Int(dx, 0), cell.brush),
                        GridArtRules.Same(cells, cell.position + new Vector2Int(0, dy), cell.brush),
                        GridArtRules.Same(cells, cell.position + new Vector2Int(dx, dy), cell.brush));
                    var sprite = brush.GetPiece(GridArtRules.Slot(kind, q));
                    var quadrant = new Rect(q % 2 * .5f, q / 2 * .5f, .5f, .5f);
                    Add(result, sprite, new Rect((Vector2)cell.position + quadrant.position, quadrant.size), quadrant);
                }
            }
            return result;
        }

        private static void Add(List<GridArtQuad> result, Sprite sprite, Rect bounds, Rect? part)
        {
            if (sprite == null) return;
            if (sprite.packed) throw new InvalidOperationException("美术网格笔刷暂不支持已打包 Sprite，请使用独立原图切片。");
            Rect sample = sprite.rect;
            if (part.HasValue)
            {
                Rect p = part.Value;
                sample = new Rect(sample.position + Vector2.Scale(sample.size, p.position), Vector2.Scale(sample.size, p.size));
            }
            result.Add(new GridArtQuad(bounds,
                new Rect(sample.x / sprite.texture.width, sample.y / sprite.texture.height,
                    sample.width / sprite.texture.width, sample.height / sprite.texture.height), sprite.texture));
        }

        private sealed class Chunk
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public void Add(GridArtQuad q, float size)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3(q.bounds.xMin, q.bounds.yMin) * size);
                vertices.Add(new Vector3(q.bounds.xMin, q.bounds.yMax) * size);
                vertices.Add(new Vector3(q.bounds.xMax, q.bounds.yMax) * size);
                vertices.Add(new Vector3(q.bounds.xMax, q.bounds.yMin) * size);
                uv.Add(new Vector2(q.uv.xMin, q.uv.yMin)); uv.Add(new Vector2(q.uv.xMin, q.uv.yMax));
                uv.Add(new Vector2(q.uv.xMax, q.uv.yMax)); uv.Add(new Vector2(q.uv.xMax, q.uv.yMin));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        public static GridArtScene Apply(GridArtMap map, Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出运行模式后应用美术。");
            if (map == null || map.palette == null || !AssetDatabase.Contains(map))
                throw new InvalidOperationException("请先保存美术图并指定瓦片库。");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("请先打开目标场景。");
            var prepared = new List<List<GridArtQuad>>();
            int total = 0;
            foreach (var layer in map.layers)
            {
                total += layer.cells.Count;
                if (total > 100000) throw new InvalidOperationException("单张美术图上限为十万格，请拆分为多个区域。");
                prepared.Add(BuildQuads(layer, map.palette));
            }
            Shader shader = Shader.Find("2026TapTap/Grid Art/Unlit");
            if (shader == null) throw new InvalidOperationException("美术 Shader 尚未就绪。");
            string folder = "Assets/_Project/Art/Generated/GridArt/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map));
            EnsureFolder(folder);
            var bindings = new List<GridArtLayerBinding>();
            GridArtScene output = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<GridArtScene>(true))
                    if (candidate.map == map) output = candidate;
            if (output == null)
            {
                var root = new GameObject("美术网格 · " + map.name);
                SceneManager.MoveGameObjectToScene(root, scene);
                output = root.AddComponent<GridArtScene>();
                output.map = map;
            }
            // Replace only this output's generated layers, never gameplay or foreign children.
            for (int c = output.transform.childCount - 1; c >= 0; c--)
            {
                var child = output.transform.GetChild(c);
                if (child.name.StartsWith("美术层_", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            for (int l = 0; l < map.layers.Count; l++)
            {
                var layer = map.layers[l];
                var root = new GameObject("美术层_" + layer.id + " · " + layer.label);
                root.transform.SetParent(output.transform, false);
                root.transform.localPosition = new Vector3(0, 0, layer.depth);
                root.SetActive(layer.visible);
                bindings.Add(new GridArtLayerBinding { id = layer.id, root = root.transform,
                    restPosition = root.transform.localPosition, parallax = layer.parallax });
                var chunks = new Dictionary<(Texture2D, int, int), Chunk>();
                foreach (var q in prepared[l])
                {
                    var key = (q.texture, Mathf.FloorToInt(q.bounds.x / 32), Mathf.FloorToInt(q.bounds.y / 32));
                    if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = new Chunk();
                    chunk.Add(q, Mathf.Max(.01f, map.cellSize));
                }
                foreach (var pair in chunks)
                {
                    string textureId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key.Item1));
                    string name = layer.id + "_" + textureId + "_" + pair.Key.Item2 + "_" + pair.Key.Item3;
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/" + name + ".asset");
                    if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset"); }
                    mesh.Clear(); mesh.name = name; mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(pair.Value.vertices); mesh.SetUVs(0, pair.Value.uv);
                    mesh.SetTriangles(pair.Value.triangles, 0); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                    string materialPath = folder + "/" + layer.id + "_" + textureId + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                    material.SetTexture("_BaseMap", pair.Key.Item1); material.SetColor("_BaseColor", layer.tint);
                    EditorUtility.SetDirty(material);
                    var child = new GameObject("网格块 " + pair.Key.Item2 + "," + pair.Key.Item3);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.sortingOrder = l - 100;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
            }
            output.SetBindings(bindings.ToArray()); EditorUtility.SetDirty(output);
            map.bakedRevision = map.revision; EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map.palette);
            map.bakedPaletteHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.palette)).ToString();
            output.SetBuildSignature(map.revision, map.bakedPaletteHash);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            return output;
        }

        public static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
