using UnityEngine;

namespace Project.Planar
{
    /// <summary>Authored local geometry; works even when the native collider is disabled.</summary>
    public static class PlanarGeometry
    {
        public static Vector3 WorldPosition(this Rigidbody2D body)
            => new Vector3(body.position.x, body.position.y, body.transform.position.z);
        public static Bounds LocalBounds(Collider2D shape)
        {
            if (shape is BoxCollider2D box) return new Bounds(box.offset, box.size);
            if (shape is CapsuleCollider2D capsule) return new Bounds(capsule.offset, capsule.size);
            if (shape is CircleCollider2D circle) return new Bounds(circle.offset, Vector2.one * circle.radius * 2f);
            if (shape is PolygonCollider2D polygon)
            {
                var bounds = new Bounds(polygon.offset, Vector3.zero);
                bool first = true;
                for (int p = 0; p < polygon.pathCount; p++)
                    foreach (Vector2 point in polygon.GetPath(p))
                    {
                        Vector3 local = point + polygon.offset;
                        if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                        else bounds.Encapsulate(local);
                    }
                return bounds;
            }
            return new Bounds(shape.offset, Vector3.zero);
        }
        public static Vector3 ClosestWorldPoint(this Collider2D shape, Vector3 point)
        {
            Vector2 nearest = shape.ClosestPoint(point);
            return new Vector3(nearest.x, nearest.y, point.z);
        }
        public static Bounds AuthoredWorldBounds(Collider2D shape)
        {
            Bounds local = LocalBounds(shape);
            Matrix4x4 matrix = shape.transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(Vector3.right * local.extents.x);
            Vector3 y = matrix.MultiplyVector(Vector3.up * local.extents.y);
            return new Bounds(matrix.MultiplyPoint3x4(local.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), 0f) * 2f);
        }
    }
}
