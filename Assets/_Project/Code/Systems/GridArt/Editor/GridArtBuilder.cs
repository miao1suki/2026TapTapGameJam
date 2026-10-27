using System;
using System.Collections.Generic;
using System.Linq;
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
        public readonly Texture2D normal;
        public readonly string colorId;
        public readonly int quarterTurns;
        public Vector2 UV(int corner) => GridArtRotation.SampleUV(uv,corner,quarterTurns);
        public GridArtQuad(Rect area, Rect sample, Texture2D source, Texture2D normalMap = null, string color = "", int rotation = 0)
        { bounds = area; uv = sample; texture = source; normal = normalMap; colorId = color; quarterTurns = GridArtRotation.Normalize(rotation); }
    }

    public static class GridArtBuilder
    {
        public static List<GridArtQuad> BuildQuads(GridArtLayer layer, GridArtPalette palette) =>
            BuildQuads(layer, palette, null);

        private static List<GridArtQuad> BuildQuads(GridArtLayer layer, GridArtPalette palette,
            HashSet<Vector2Int> chunkFilter)
        {
            var result = new List<GridArtQuad>();
            if (palette == null) return result;
            var cells = new Dictionary<Vector2Int, int>();
            foreach (var cell in layer.cells) cells[cell.position] = cell.brush;
            foreach (var cell in layer.cells)
            {
                if (chunkFilter != null && !chunkFilter.Contains(ChunkCoordinate(cell.position))) continue;
                var brush = palette.Get(cell.brush);
                if (brush == null) continue;
                string colorId = string.IsNullOrEmpty(layer.colorId) ? brush.colorId : layer.colorId == "none" ? "" : layer.colorId;
                if (!brush.autoTerrain)
                {
                    Add(result, brush.sprite, new Rect(cell.position, GridArtRotation.Size(brush.DisplaySize, cell.quarterTurns)), brush.contentRect, brush.normalMap, colorId, cell.quarterTurns);
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
                    Add(result, sprite, new Rect((Vector2)cell.position + quadrant.position, quadrant.size), quadrant, brush.normalMap, colorId);
                }
            }
            return result;
        }

        private static Vector2Int ChunkCoordinate(Vector2Int cell) => new Vector2Int(
            Mathf.FloorToInt(cell.x / 32f), Mathf.FloorToInt(cell.y / 32f));

        private static void Add(List<GridArtQuad> result, Sprite sprite, Rect bounds, Rect? part, Texture2D normal, string colorId, int rotation = 0)
        {
            if (sprite == null) return;
            if (sprite.packed) throw new InvalidOperationException("美术网格笔刷暂不支持已打包 Sprite，请使用独立原图切片。");
            if (normal != null && (normal.width != sprite.texture.width || normal.height != sprite.texture.height))
                throw new InvalidOperationException("法线图必须与原图集尺寸及切片位置一致：" + sprite.name);
            Rect sample = sprite.rect;
            if (part.HasValue)
            {
                Rect p = part.Value;
                sample = new Rect(sample.position + Vector2.Scale(sample.size, p.position), Vector2.Scale(sample.size, p.size));
            }
            result.Add(new GridArtQuad(bounds,
                new Rect(sample.x / sprite.texture.width, sample.y / sprite.texture.height,
                    sample.width / sprite.texture.width, sample.height / sprite.texture.height), sprite.texture, normal, colorId, rotation));
        }

        public static int ColorLayer(string colorId)
        {
            return GridArtColorSource.UnityLayer(colorId);
        }

        private sealed class Chunk
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Vector4> sampleBounds = new List<Vector4>();
            public readonly List<int> triangles = new List<int>();
            public bool Matches(Mesh mesh)
            {
                if (mesh == null || mesh.subMeshCount == 0 || mesh.vertexCount != vertices.Count || mesh.GetIndexCount(0) != (uint)triangles.Count) return false;
                mesh.GetVertices(compareVertices); mesh.GetUVs(0, compareUv);
                mesh.GetUVs(1, compareBounds);
                return compareVertices.SequenceEqual(vertices) && compareUv.SequenceEqual(uv) && compareBounds.SequenceEqual(sampleBounds);
            }
            public void Add(GridArtQuad q, float size)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3(q.bounds.xMin, q.bounds.yMin) * size);
                vertices.Add(new Vector3(q.bounds.xMin, q.bounds.yMax) * size);
                vertices.Add(new Vector3(q.bounds.xMax, q.bounds.yMax) * size);
                vertices.Add(new Vector3(q.bounds.xMax, q.bounds.yMin) * size);
                uv.Add(q.UV(0)); uv.Add(q.UV(1)); uv.Add(q.UV(2)); uv.Add(q.UV(3));
                var rect = new Vector4(q.uv.xMin, q.uv.yMin, q.uv.xMax, q.uv.yMax);
                for (int corner = 0; corner < 4; corner++) sampleBounds.Add(rect);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }
        private static readonly List<Vector3> compareVertices = new List<Vector3>();
        private static readonly List<Vector2> compareUv = new List<Vector2>();
        private static readonly List<Vector4> compareBounds = new List<Vector4>();
        private static void Record(UnityEngine.Object target, bool enabled) { if (enabled) Undo.RecordObject(target, "应用关卡美术"); }
        private static void Created(UnityEngine.Object target, bool enabled) { if (enabled) Undo.RegisterCreatedObjectUndo(target, "应用关卡美术"); }
        private static void Destroy(UnityEngine.Object target, bool enabled) { if (enabled) Undo.DestroyObjectImmediate(target); else UnityEngine.Object.DestroyImmediate(target); }

        public static GridArtScene Apply(GridArtMap map, Scene scene, bool recordUndo = true, bool saveMap = true)
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出运行模式后应用美术。");
            if (map == null || map.palette == null || !AssetDatabase.Contains(map))
                throw new InvalidOperationException("请先保存美术图并指定瓦片库。");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("请先打开目标场景。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("请退出Prefab模式，在关卡场景中应用美术。");
            string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            if (string.IsNullOrEmpty(sceneGuid)) throw new InvalidOperationException("请先保存当前场景，再应用美术。");
            if (!string.IsNullOrEmpty(map.ownerSceneGuid) && map.ownerSceneGuid != sceneGuid)
                throw new InvalidOperationException("这张美术图属于其他场景。请先创建独立副本，避免改动另一关。");
            GridArtScene output = FindOutput(map, scene);
            if (output != null && (PrefabUtility.IsPartOfPrefabInstance(output) || output.transform.position.sqrMagnitude > .0001f ||
                Quaternion.Angle(output.transform.rotation, Quaternion.identity) > .01f || (output.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f))
                throw new InvalidOperationException("美术输出根节点必须保持世界原点、无旋转且缩放为1，并独立于共享预制体。");
            string paletteHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.palette)).ToString();
            if (output != null && output.IsBuiltFor(map.revision, paletteHash)) return output;
            var prepared = new List<List<GridArtQuad>>();
            var colorLayers = new Dictionary<string,int>(StringComparer.Ordinal);
            int total = 0;
            foreach (var layer in map.layers)
            {
                total += layer.cells.Count;
                if (total > 100000) throw new InvalidOperationException("单张美术图上限为十万格，请拆分为多个区域。");
                prepared.Add(BuildQuads(layer, map.palette));
                foreach (var quad in prepared[prepared.Count-1])
                {
                    string id = quad.colorId ?? "";
                    if (!colorLayers.ContainsKey(id)) colorLayers[id] = ColorLayer(id);
                }
            }
            Shader shader = Shader.Find("2026TapTap/Grid Art/Unlit");
            if (shader == null) throw new InvalidOperationException("美术 Shader 尚未就绪。");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (map.layers.Exists(l => l.lighting3D) && litShader == null) throw new InvalidOperationException("当前管线缺少3D受光Shader。");
            string folder = "Assets/_Project/Art/Generated/GridArt/" + sceneGuid + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map));
            EnsureFolder(folder);
            var bindings = new List<GridArtLayerBinding>();
            var colorBindings = new List<GridArtColorBinding>();
            if (output == null)
            {
                var root = new GameObject("美术网格 · " + map.name);
                SceneManager.MoveGameObjectToScene(root, scene);
                output = root.AddComponent<GridArtScene>();
                output.map = map;
                Created(root, recordUndo);
            }
            Record(output, recordUndo); Record(map, recordUndo);
            // Replace only this output's generated layers, never gameplay or foreign children.
            for (int c = output.transform.childCount - 1; c >= 0; c--)
            {
                var child = output.transform.GetChild(c);
                if (child.name.StartsWith("美术层_", StringComparison.Ordinal) &&
                    (recordUndo || !map.layers.Exists(layer => child.name.StartsWith("美术层_"+layer.id+" · ", StringComparison.Ordinal)))) Destroy(child.gameObject, recordUndo);
            }
            for (int l = 0; l < map.layers.Count; l++)
            {
                var layer = map.layers[l];
                string layerName = "美术层_" + layer.id + " · " + layer.label;
                GameObject root = null;
                if (!recordUndo) for (int i = 0; i < output.transform.childCount; i++)
                    if (output.transform.GetChild(i).name.StartsWith("美术层_"+layer.id+" · ", StringComparison.Ordinal)) root = output.transform.GetChild(i).gameObject;
                if (root == null) { root = new GameObject(layerName); root.transform.SetParent(output.transform, false); Created(root, recordUndo); }
                root.name = layerName;
                root.transform.localPosition = new Vector3(0, 0, layer.depth);
                root.SetActive(layer.visible);
                bindings.Add(new GridArtLayerBinding { id = layer.id, root = root.transform,
                    restPosition = root.transform.localPosition, parallax = layer.parallax });
                var chunks = new Dictionary<(Texture2D, int, int, Texture2D, string), Chunk>();
                foreach (var q in prepared[l])
                {
                    var key = (q.texture, Mathf.FloorToInt(q.bounds.x / 32), Mathf.FloorToInt(q.bounds.y / 32), q.normal, q.colorId ?? "");
                    if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = new Chunk();
                    chunk.Add(q, Mathf.Max(.01f, map.cellSize));
                }
                var used = new HashSet<string>();
                foreach (var pair in chunks)
                {
                    string textureId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key.Item1));
                    string normalId = pair.Key.Item4 != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key.Item4)) : "flat";
                    int colorLayer = colorLayers[pair.Key.Item5];
                    string name = layer.id + "_" + textureId + "_" + normalId + "_" + pair.Key.Item2 + "_" + pair.Key.Item3 + (colorLayer == 0 ? "" : "_c"+Hash128.Compute(pair.Key.Item5));
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/" + name + ".asset");
                    if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset"); }
                    if (!pair.Value.Matches(mesh))
                    {
                        Record(mesh, recordUndo);
                        mesh.Clear(); mesh.name = name; mesh.indexFormat = IndexFormat.UInt32;
                        mesh.SetVertices(pair.Value.vertices); mesh.SetUVs(0, pair.Value.uv);
                        mesh.SetUVs(1, pair.Value.sampleBounds);
                        mesh.SetTriangles(pair.Value.triangles, 0); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                        mesh.RecalculateNormals(); mesh.RecalculateTangents();
                        AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                    string materialPath = folder + "/" + layer.id + "_" + textureId + "_" + normalId + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                    bool materialChanged = material.shader != (layer.lighting3D ? litShader : shader) || material.GetTexture("_BaseMap") != pair.Key.Item1 ||
                        material.GetColor("_BaseColor") != layer.tint || layer.lighting3D && material.GetTexture("_BumpMap") != pair.Key.Item4;
                    if (materialChanged || string.IsNullOrEmpty(material.name))
                    {
                    Record(material, recordUndo);
                    material.shader = layer.lighting3D ? litShader : shader;
                    material.shaderKeywords = Array.Empty<string>(); material.renderQueue = -1;
                    material.SetTexture("_BaseMap", pair.Key.Item1); material.SetColor("_BaseColor", layer.tint);
                    if (layer.lighting3D)
                    {
                        material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                        material.SetFloat("_Cull", 0); material.SetFloat("_Smoothness", 0);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.SetTexture("_BumpMap", pair.Key.Item4);
                        if (pair.Key.Item4 != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
                        material.renderQueue = 3000;
                    }
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    }
                    string childName = "网格块 " + name; used.Add(childName);
                    var existing = root.transform.Find(childName);
                    var child = existing != null ? existing.gameObject : new GameObject(childName);
                    if (existing == null) { child.transform.SetParent(root.transform, false); child.AddComponent<MeshFilter>(); child.AddComponent<MeshRenderer>(); }
                    child.layer = colorLayer;
                    child.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    if (colorLayer != 0) colorBindings.Add(new GridArtColorBinding { renderer=renderer,alphaTexture=pair.Key.Item1 });
                    renderer.sortingOrder = layer.sortingOrder == int.MinValue ? l - 100 : layer.sortingOrder;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    if (!used.Contains(root.transform.GetChild(i).name)) Destroy(root.transform.GetChild(i).gameObject, recordUndo);
            }
            output.SetBindings(bindings.ToArray()); EditorUtility.SetDirty(output);
            output.SetColorBindings(colorBindings.ToArray());
            map.ownerSceneGuid = sceneGuid;
            map.bakedRevision = map.revision; EditorUtility.SetDirty(map);
            if (saveMap) AssetDatabase.SaveAssetIfDirty(map.palette);
            map.bakedPaletteHash = paletteHash;
            output.SetBuildSignature(map.revision, map.bakedPaletteHash);
            if (saveMap) AssetDatabase.SaveAssetIfDirty(map); EditorSceneManager.MarkSceneDirty(scene);
            return output;
        }

        /// <summary>
        /// Rebuilds only chunks touched by a SceneView stroke. It falls back to the full builder
        /// when the previous generated signature is unavailable, so legacy scenes remain safe.
        /// </summary>
        public static GridArtScene ApplyAffected(GridArtMap map, Scene scene, GridArtLayer layer,
            IReadOnlyCollection<Vector2Int> changedAnchors, int previousRevision, bool saveMap = false)
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出运行模式后应用美术。");
            if (map == null || map.palette == null || !AssetDatabase.Contains(map))
                throw new InvalidOperationException("请先保存美术图并指定瓦片库。");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("请先打开目标场景。");
            if (layer == null || !map.layers.Contains(layer) || changedAnchors == null || changedAnchors.Count == 0)
                return Apply(map, scene, false, saveMap);

            string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            string paletteHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.palette)).ToString();
            GridArtScene output = FindOutput(map, scene);
            if (output == null || !output.IsBuiltFor(previousRevision, paletteHash))
                return Apply(map, scene, false, saveMap);

            var affected = new HashSet<Vector2Int>();
            foreach (Vector2Int anchor in changedAnchors)
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        affected.Add(ChunkCoordinate(anchor + new Vector2Int(x, y)));

            List<GridArtQuad> quads = BuildQuads(layer, map.palette, affected);
            var chunks = new Dictionary<(Texture2D, int, int, Texture2D, string), Chunk>();
            var colorLayers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (GridArtQuad quad in quads)
            {
                string colorId = quad.colorId ?? string.Empty;
                if (!colorLayers.ContainsKey(colorId)) colorLayers[colorId] = ColorLayer(colorId);
                Vector2Int coordinate = ChunkCoordinate(Vector2Int.FloorToInt(quad.bounds.position));
                var key = (quad.texture, coordinate.x, coordinate.y, quad.normal, colorId);
                if (!chunks.TryGetValue(key, out Chunk chunk)) chunks[key] = chunk = new Chunk();
                chunk.Add(quad, Mathf.Max(.01f, map.cellSize));
            }

            Transform layerRoot = null;
            string layerPrefix = "美术层_" + layer.id + " · ";
            for (int i = 0; i < output.transform.childCount; i++)
                if (output.transform.GetChild(i).name.StartsWith(layerPrefix, StringComparison.Ordinal))
                { layerRoot = output.transform.GetChild(i); break; }
            if (layerRoot == null) return Apply(map, scene, false, saveMap);

            Shader shader = Shader.Find("2026TapTap/Grid Art/Unlit");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || layer.lighting3D && litShader == null)
                throw new InvalidOperationException("当前管线缺少 GridArt 所需 Shader。");
            string folder = "Assets/_Project/Art/Generated/GridArt/" + sceneGuid + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map));
            EnsureFolder(folder);
            var used = new HashSet<string>(StringComparer.Ordinal);

            foreach (KeyValuePair<(Texture2D, int, int, Texture2D, string), Chunk> pair in chunks)
            {
                string textureId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key.Item1));
                string normalId = pair.Key.Item4 != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Key.Item4)) : "flat";
                int colorLayer = colorLayers[pair.Key.Item5];
                string name = layer.id + "_" + textureId + "_" + normalId + "_" + pair.Key.Item2 + "_" + pair.Key.Item3 +
                    (colorLayer == 0 ? string.Empty : "_c" + Hash128.Compute(pair.Key.Item5));
                string meshPath = folder + "/" + name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
                if (!pair.Value.Matches(mesh))
                {
                    mesh.Clear(); mesh.name = name; mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(pair.Value.vertices); mesh.SetUVs(0, pair.Value.uv); mesh.SetUVs(1, pair.Value.sampleBounds);
                    mesh.SetTriangles(pair.Value.triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
                    EditorUtility.SetDirty(mesh);
                    if (saveMap) AssetDatabase.SaveAssetIfDirty(mesh);
                }

                string materialPath = folder + "/" + layer.id + "_" + textureId + "_" + normalId + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                bool materialChanged = material.shader != (layer.lighting3D ? litShader : shader) ||
                    material.GetTexture("_BaseMap") != pair.Key.Item1 || material.GetColor("_BaseColor") != layer.tint ||
                    layer.lighting3D && material.GetTexture("_BumpMap") != pair.Key.Item4;
                if (materialChanged || string.IsNullOrEmpty(material.name))
                {
                    material.shader = layer.lighting3D ? litShader : shader; material.shaderKeywords = Array.Empty<string>(); material.renderQueue = -1;
                    material.SetTexture("_BaseMap", pair.Key.Item1); material.SetColor("_BaseColor", layer.tint);
                    if (layer.lighting3D)
                    {
                        material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                        material.SetFloat("_Cull", 0); material.SetFloat("_Smoothness", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.SetTexture("_BumpMap", pair.Key.Item4);
                        if (pair.Key.Item4 != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
                        material.renderQueue = 3000;
                    }
                    EditorUtility.SetDirty(material);
                    if (saveMap) AssetDatabase.SaveAssetIfDirty(material);
                }

                string childName = "网格块 " + name; used.Add(childName);
                Transform existing = layerRoot.Find(childName);
                GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
                if (existing == null) { child.transform.SetParent(layerRoot, false); child.AddComponent<MeshFilter>(); child.AddComponent<MeshRenderer>(); }
                child.layer = colorLayer; child.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.sortingOrder = layer.sortingOrder == int.MinValue ? map.layers.IndexOf(layer) - 100 : layer.sortingOrder;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }

            for (int i = layerRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = layerRoot.GetChild(i);
                if (TryReadChunkCoordinate(child.name, layer.id, out Vector2Int coordinate) && affected.Contains(coordinate) && !used.Contains(child.name))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            RefreshBindings(output, map);
            map.ownerSceneGuid = sceneGuid; map.bakedRevision = map.revision; map.bakedPaletteHash = paletteHash;
            output.SetBuildSignature(map.revision, paletteHash);
            EditorUtility.SetDirty(map); EditorUtility.SetDirty(output); EditorSceneManager.MarkSceneDirty(scene);
            if (saveMap) AssetDatabase.SaveAssetIfDirty(map);
            return output;
        }

        private static bool TryReadChunkCoordinate(string childName, string layerId, out Vector2Int coordinate)
        {
            coordinate = default;
            string prefix = "网格块 " + layerId + "_";
            if (string.IsNullOrEmpty(childName) || !childName.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string[] parts = childName.Substring(prefix.Length).Split('_');
            if (parts.Length < 4 || !int.TryParse(parts[2], out int x) || !int.TryParse(parts[3], out int y)) return false;
            coordinate = new Vector2Int(x, y);
            return true;
        }

        private static void RefreshBindings(GridArtScene output, GridArtMap map)
        {
            var bindings = new List<GridArtLayerBinding>();
            var colors = new List<GridArtColorBinding>();
            foreach (GridArtLayer layer in map.layers)
            {
                Transform root = null;
                string prefix = "美术层_" + layer.id + " · ";
                for (int i = 0; i < output.transform.childCount; i++)
                    if (output.transform.GetChild(i).name.StartsWith(prefix, StringComparison.Ordinal)) { root = output.transform.GetChild(i); break; }
                if (root == null) continue;
                bindings.Add(new GridArtLayerBinding { id = layer.id, root = root, restPosition = new Vector3(0, 0, layer.depth), parallax = layer.parallax });
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer == null || renderer.gameObject.layer == 0) continue;
                    Texture2D alpha = renderer.sharedMaterial != null ? renderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D : null;
                    colors.Add(new GridArtColorBinding { renderer = renderer, alphaTexture = alpha });
                }
            }
            output.SetBindings(bindings.ToArray()); output.SetColorBindings(colors.ToArray());
        }

        private static GridArtScene FindOutput(GridArtMap map, Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<GridArtScene>(true))
                    if (candidate.map == map) return candidate;
            return null;
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
