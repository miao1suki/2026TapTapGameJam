using UnityEngine;

namespace Project.FlowMagma
{
    [CreateAssetMenu(menuName = "2026TapTap/反应视觉配置", fileName = "ReactionArtworkSettings")]
    public sealed class ReactionArtworkSettings : ScriptableObject
    {
        [InspectorName("流动岩浆视觉")] public GameObject magmaPrefab;
        [InspectorName("冷却蒸汽视觉")] public GameObject coolingSteamPrefab;
        private static ReactionArtworkSettings cached;
        public static ReactionArtworkSettings Load() => cached != null ? cached
            : cached = Resources.Load<ReactionArtworkSettings>("TAReactions/ReactionArtworkSettings");
    }
}
