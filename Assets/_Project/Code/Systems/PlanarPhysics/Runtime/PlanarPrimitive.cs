using UnityEngine;

namespace Project.Planar
{
    /// <summary>Planar authoring geometry. Never creates a PhysX primitive or collider.</summary>
    public static class PlanarPrimitive
    {
        public enum Shape { Box, Disk, Capsule, Quad }
        private static Mesh quad;
        private static Mesh disk, capsule;
        public static GameObject Create(Shape shape)
        {
            if (!quad) quad = Resources.Load<Mesh>("Planar/UnitQuad");
            if (!quad) throw new System.InvalidOperationException("Missing native 2D unit quad asset");
            Mesh mesh = quad;
            if (shape == Shape.Disk)
            {
                if (!disk) disk = Resources.Load<Mesh>("Planar/UnitDisk");
                mesh = disk;
            }
            else if (shape == Shape.Capsule)
            {
                if (!capsule) capsule = Resources.Load<Mesh>("Planar/UnitCapsule");
                mesh = capsule;
            }
            if (!mesh) throw new System.InvalidOperationException("Missing native 2D round mesh asset");
            var go = new GameObject(shape.ToString());
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            switch (shape)
            {
                case Shape.Box: go.AddComponent<BoxCollider2D>(); break;
                case Shape.Disk: go.AddComponent<CircleCollider2D>(); break;
                case Shape.Capsule: go.AddComponent<CapsuleCollider2D>().size = new Vector2(1f, 2f); break;
            }
            return go;
        }
    }
}
