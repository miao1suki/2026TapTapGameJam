using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Interactions
{
    /// <summary>
    /// 一个可交互物体的定义资产。每个 Unity ScriptableObject 类型单独放在
    /// 文件中，避免 Unity 将普通图节点类误识别为脚本组件。
    /// </summary>
    [CreateAssetMenu(
        menuName = "2026TapTap/交互/物体交互定义",
        fileName = "InteractionObjectDefinition")]
    public sealed class InteractionObjectDefinition : ScriptableObject
    {
        [SerializeField] private string objectId = Guid.NewGuid().ToString("N");
        [SerializeField] private string displayName = "新物体";
        [SerializeField] private GameObject prefab;
        [SerializeField] private string baseColorTypeId;
        [SerializeField] private List<InteractionGraphNode> nodes =
            new List<InteractionGraphNode>();
        [SerializeField] private List<InteractionGraphEdge> edges =
            new List<InteractionGraphEdge>();

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public string BaseColorTypeId => baseColorTypeId;
        public IReadOnlyList<InteractionGraphNode> Nodes => nodes;
        public IReadOnlyList<InteractionGraphEdge> Edges => edges;

#if UNITY_EDITOR
        public List<InteractionGraphNode> EditableNodes => nodes;
        public List<InteractionGraphEdge> EditableEdges => edges;

        public void SetDisplayName(string value)
        {
            displayName = string.IsNullOrWhiteSpace(value) ? "新物体" : value;
        }

        public void SetPrefab(GameObject value)
        {
            prefab = value;
        }

        public void SetBaseColorTypeId(string value)
        {
            baseColorTypeId = value ?? string.Empty;
        }

        public void EnsureObjectId()
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                objectId = Guid.NewGuid().ToString("N");
            }
        }

        public void Configure(
            string valueDisplayName,
            GameObject valuePrefab,
            string valueBaseColorTypeId)
        {
            EnsureObjectId();
            displayName = string.IsNullOrWhiteSpace(valueDisplayName)
                ? "新物体"
                : valueDisplayName;
            prefab = valuePrefab;
            baseColorTypeId = valueBaseColorTypeId ?? string.Empty;
        }
#endif
    }
}
