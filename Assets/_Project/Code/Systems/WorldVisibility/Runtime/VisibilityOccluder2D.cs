using System.Collections.Generic;
using UnityEngine;

namespace Project.WorldVisibility
{
    /// <summary>Opt-in for non-planning walls/props. Disabled/trigger colliders never occlude.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class VisibilityOccluder2D : MonoBehaviour
    {
        internal static readonly List<VisibilityOccluder2D> Active = new List<VisibilityOccluder2D>();
        [SerializeField] public BoxCollider[] shapes = new BoxCollider[0];
        void Reset() => shapes = GetComponentsInChildren<BoxCollider>(true);
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); WorldVisibility2D.InvalidateAll(); }
        void OnDisable() { Active.Remove(this); WorldVisibility2D.InvalidateAll(); }
        void OnValidate() => WorldVisibility2D.InvalidateAll();
    }
}
