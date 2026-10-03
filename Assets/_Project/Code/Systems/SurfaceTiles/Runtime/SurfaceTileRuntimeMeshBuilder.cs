using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.SurfaceTiles
{
    internal static class SurfaceTileRuntimeMeshBuilder
    {
        private const float WorldSurfaceBias = 0.003f;
        private const float WorldSeamSafety = 0.001f;

        internal static Mesh Build(SurfaceTileBlock block)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            SurfaceTilePalette palette = block != null
                ? block.Palette
                : null;
            BoxCollider collider = block != null
                ? block.SurfaceCollider
                : null;
            if (block == null ||
                palette == null ||
                collider == null ||
                !palette.UsesSingleTexture())
            {
                return CreateMesh(vertices, normals, uvs, triangles);
            }

            List<SurfaceTilePlacement> placements = block.Placements
                .OrderBy(item => item.Layer)
                .ToList();
            for (int index = 0; index < placements.Count; index++)
            {
                SurfaceTilePlacement placement = placements[index];
                if (!palette.TryGet(
                        placement.TileId,
                        out SurfaceTilePalette.Entry tile) ||
                    tile.Sprite == null)
                {
                    continue;
                }

                Vector2Int grid = block.GetGridSize(placement.Face);
                Rect baseRect = SurfaceTileGeometry.GetPlacementRect(
                    tile,
                    placement.Cell,
                    placement.QuarterTurns,
                    placement.Anchor);
                if (!SurfaceTileGeometry.PlacementFitsGrid(baseRect, grid))
                {
                    continue;
                }

                Rect placementRect = SurfaceTileGeometry.GetPlacementRect(
                    tile,
                    placement);
                Rect spriteUv = ContentUv(
                    tile.Sprite,
                    tile.ContentRect);
                AddPlacementQuad(
                    block,
                    placement.Face,
                    placementRect,
                    grid,
                    spriteUv,
                    placement.QuarterTurns,
                    placement.FlipX,
                    placement.FlipY,
                    WorldSurfaceBias + index * 0.00005f,
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            return CreateMesh(vertices, normals, uvs, triangles);
        }

        private static Rect ContentUv(
            Sprite sprite,
            Rect contentRect)
        {
            Rect textureRect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect spriteUv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            return new Rect(
                spriteUv.x + contentRect.x * spriteUv.width,
                spriteUv.y + contentRect.y * spriteUv.height,
                contentRect.width * spriteUv.width,
                contentRect.height * spriteUv.height);
        }

        private static void AddPlacementQuad(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            Rect placementRect,
            Vector2Int grid,
            Rect uvRect,
            int turns,
            bool flipX,
            bool flipY,
            float worldBias,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            float u0 = placementRect.xMin / grid.x;
            float u1 = placementRect.xMax / grid.x;
            float v0 = placementRect.yMin / grid.y;
            float v1 = placementRect.yMax / grid.y;
            Vector2 seam = GetNormalizedSeamOverlap(
                block,
                face,
                worldBias);
            if (placementRect.xMin <= 0.0001f)
            {
                u0 -= seam.x;
            }

            if (placementRect.xMax >= grid.x - 0.0001f)
            {
                u1 += seam.x;
            }

            if (placementRect.yMin <= 0.0001f)
            {
                v0 -= seam.y;
            }

            if (placementRect.yMax >= grid.y - 0.0001f)
            {
                v1 += seam.y;
            }

            AddQuad(
                block,
                face,
                u0,
                v0,
                u1,
                v1,
                uvRect,
                turns,
                flipX,
                flipY,
                worldBias,
                vertices,
                normals,
                uvs,
                triangles);
        }

        private static Vector2 GetNormalizedSeamOverlap(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            float worldBias)
        {
            SurfaceTileFaceBasis basis =
                SurfaceTileGeometry.GetLocalBasis(
                    block.SurfaceCollider,
                    face);
            Vector3 scale = block.transform.lossyScale;
            float scaleU = Vector3.Magnitude(
                Vector3.Scale(basis.AxisU, scale));
            float scaleV = Vector3.Magnitude(
                Vector3.Scale(basis.AxisV, scale));
            float overlap = worldBias + WorldSeamSafety;
            return new Vector2(
                overlap /
                Mathf.Max(0.0001f, basis.Width * scaleU),
                overlap /
                Mathf.Max(0.0001f, basis.Height * scaleV));
        }

        private static void AddQuad(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            float u0,
            float v0,
            float u1,
            float v1,
            Rect uvRect,
            int turns,
            bool flipX,
            bool flipY,
            float worldBias,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            SurfaceTileFaceBasis basis =
                SurfaceTileGeometry.GetLocalBasis(
                    block.SurfaceCollider,
                    face);
            float axisScale = Vector3.Magnitude(
                Vector3.Scale(
                    basis.Normal,
                    block.transform.lossyScale));
            float localBias =
                worldBias / Mathf.Max(0.0001f, axisScale);
            Vector3[] positions =
            {
                basis.Point(u0, v0, localBias),
                basis.Point(u1, v0, localBias),
                basis.Point(u1, v1, localBias),
                basis.Point(u0, v1, localBias)
            };
            Vector2[] logicalUvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            bool forward = Vector3.Dot(
                Vector3.Cross(basis.AxisU, basis.AxisV),
                basis.Normal) > 0f;
            int[] order = forward
                ? new[] { 0, 1, 2, 3 }
                : new[] { 0, 3, 2, 1 };
            int start = vertices.Count;
            for (int index = 0; index < 4; index++)
            {
                int source = order[index];
                vertices.Add(positions[source]);
                normals.Add(basis.Normal);
                Vector2 tileUv = TransformTileUv(
                    logicalUvs[source],
                    turns,
                    flipX,
                    flipY);
                uvs.Add(new Vector2(
                    uvRect.x + tileUv.x * uvRect.width,
                    uvRect.y + tileUv.y * uvRect.height));
            }

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Vector2 TransformTileUv(
            Vector2 uv,
            int quarterTurns,
            bool flipX,
            bool flipY)
        {
            if (flipX)
            {
                uv.x = 1f - uv.x;
            }

            if (flipY)
            {
                uv.y = 1f - uv.y;
            }

            int turns = Mathf.Abs(quarterTurns) % 4;
            for (int index = 0; index < turns; index++)
            {
                uv = new Vector2(uv.y, 1f - uv.x);
            }

            return uv;
        }

        private static Mesh CreateMesh(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            Mesh mesh = new Mesh
            {
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
