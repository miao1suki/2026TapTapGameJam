using System.Collections.Generic;
using UnityEngine;

namespace Project.WorldVisibility
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class VisibilityLight2D : MonoBehaviour
    {
        internal static readonly List<VisibilityLight2D> Active = new List<VisibilityLight2D>();
        [SerializeField, Min(.1f)] public float radiusCells = 6;
        [SerializeField, ColorUsage(false)] public Color tint = new Color(1f,.83f,.6f);
        [SerializeField, Range(0,2)] public float intensity = 1;
        internal readonly VisibilityGeometry.Polygon polygon = new VisibilityGeometry.Polygon();
        internal Vector3 lastPosition = new Vector3(float.PositiveInfinity,0,0);
        internal int lastRevision = -1;
        internal float lastRadius, lastIntensity;
        internal Color lastTint;
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); lastRevision = -1; }
        void OnDisable() { Active.Remove(this); polygon.Dispose(); }
        void OnDrawGizmosSelected() { Gizmos.color = tint; Gizmos.DrawWireSphere(transform.position,radiusCells); }
    }
}
