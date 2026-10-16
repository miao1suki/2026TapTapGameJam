using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Player
{
    // One pooled mesh, no runtime Canvas, collider or per-dash GameObjects.
    internal sealed class CarryPlacementPreview : IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] vertices = new Vector3[512];
        private readonly int[] indices = new int[768];
        public CarryPlacementPreview(Transform owner)
        {
            root = new GameObject("Carry placement preview", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(owner, false);
            mesh = new Mesh { name = "Carry placement dashes" };
            mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Cull", 0f);
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            for (int i = 0; i < 128; i++)
            {
                int v = i * 4, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
        }
        public void Hide() { if (root != null) root.SetActive(false); }
        public void Show(Bounds bounds, bool valid, float cell)
        {
            root.SetActive(true);
            material.SetColor("_BaseColor", valid ? new Color(.4f, 1f, .8f) : new Color(1f, .15f, .15f));
            int count = 0;
            Vector3 min = bounds.min, max = bounds.max;
            float z = min.z - cell * .04f;
            Edge(new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z), cell, ref count);
            Edge(new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z), cell, ref count);
            Edge(new Vector3(max.x, max.y, z), new Vector3(min.x, max.y, z), cell, ref count);
            Edge(new Vector3(min.x, max.y, z), new Vector3(min.x, min.y, z), cell, ref count);
            mesh.Clear();
            mesh.SetVertices(vertices, 0, count * 4);
            mesh.SetTriangles(indices, 0, count * 6, 0);
            mesh.RecalculateBounds();
        }
        private void Edge(Vector3 a, Vector3 b, float cell, ref int count)
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / (cell * .19f)), 2, 32);
            Vector3 normal = Vector3.Cross((b - a).normalized, Vector3.forward) * (cell * .014f);
            for (int i = 0; i < steps; i++)
            {
                Vector3 start = Vector3.Lerp(a, b, (i + .12f) / steps);
                Vector3 end = Vector3.Lerp(a, b, (i + .72f) / steps);
                int v = count++ * 4;
                vertices[v] = root.transform.InverseTransformPoint(start - normal);
                vertices[v + 1] = root.transform.InverseTransformPoint(start + normal);
                vertices[v + 2] = root.transform.InverseTransformPoint(end + normal);
                vertices[v + 3] = root.transform.InverseTransformPoint(end - normal);
            }
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
        }
    }
}
