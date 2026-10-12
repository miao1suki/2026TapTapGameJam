using UnityEngine;

namespace TaSource.LinkEffect
{
    /// <summary>Optional, side-effect-free preview for a nearby interaction.</summary>
    public interface ILinkEffectPreviewTarget
    {
        bool CanLink(GameObject actor, string colorId);
        Transform LinkAnchor { get; }
    }
}
