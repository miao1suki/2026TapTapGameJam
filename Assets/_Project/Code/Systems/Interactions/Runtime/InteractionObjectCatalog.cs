using System.Collections.Generic;
using UnityEngine;

namespace Project.Interactions
{
    /// <summary>
    /// 项目中所有可交互物体定义的目录。
    /// </summary>
    [CreateAssetMenu(
        menuName = "2026TapTap/交互/物体交互目录",
        fileName = "InteractionObjectCatalog")]
    public sealed class InteractionObjectCatalog : ScriptableObject
    {
        [SerializeField] private List<InteractionObjectDefinition> objects =
            new List<InteractionObjectDefinition>();

        public IReadOnlyList<InteractionObjectDefinition> Objects => objects;

        public InteractionObjectDefinition Find(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            return objects.Find(item =>
                item != null && item.ObjectId == objectId);
        }

#if UNITY_EDITOR
        public List<InteractionObjectDefinition> EditableObjects => objects;
#endif
    }
}
