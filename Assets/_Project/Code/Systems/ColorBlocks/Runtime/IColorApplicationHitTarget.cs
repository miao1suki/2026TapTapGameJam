using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Optional click context; ordinary contact ApplyColor calls remain supported.</summary>
    public interface IColorApplicationHitTarget : IColorApplicationTarget
    {
        bool ApplyColorAt(string colorId, GameObject actor, Vector3 hitWorldPoint);
    }
}
