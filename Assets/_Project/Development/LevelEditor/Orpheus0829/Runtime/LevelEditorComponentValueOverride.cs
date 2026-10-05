using System;
using UnityEngine;

namespace Project.LevelEditor
{
    [Serializable]
    public sealed class LevelEditorComponentValueOverride
    {
        [SerializeField] private string componentPath;
        [SerializeField] private string componentTypeName;
        [SerializeField] private int componentIndex;
        [SerializeField] private string propertyPath;
        [SerializeField] private string valueType;
        [SerializeField] private string value;
        [SerializeField] private UnityEngine.Object objectReferenceValue;

        public string ComponentPath => componentPath;
        public string ComponentTypeName => componentTypeName;
        public int ComponentIndex => componentIndex;
        public string PropertyPath => propertyPath;
        public string ValueType => valueType;
        public string Value => value;
        public UnityEngine.Object ObjectReferenceValue =>
            objectReferenceValue;

        public LevelEditorComponentValueOverride Clone()
        {
            return new LevelEditorComponentValueOverride
            {
                componentPath = componentPath,
                componentTypeName = componentTypeName,
                componentIndex = componentIndex,
                propertyPath = propertyPath,
                valueType = valueType,
                value = value,
                objectReferenceValue = objectReferenceValue
            };
        }

        public void Configure(
            string valueComponentPath,
            string valueComponentTypeName,
            int valueComponentIndex,
            string valuePropertyPath,
            string valueTypeName,
            string valueText,
            UnityEngine.Object valueObject)
        {
            componentPath = valueComponentPath;
            componentTypeName = valueComponentTypeName;
            componentIndex = valueComponentIndex;
            propertyPath = valuePropertyPath;
            valueType = valueTypeName;
            value = valueText;
            objectReferenceValue = valueObject;
        }
    }
}
