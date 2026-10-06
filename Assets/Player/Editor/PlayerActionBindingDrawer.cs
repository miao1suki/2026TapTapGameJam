using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomPropertyDrawer(typeof(PlayerActionBinding))]
    public sealed class PlayerActionBindingDrawer : PropertyDrawer
    {
        private static readonly InputActionId[] Actions =
            InputActionBindingPolicy.GetVisibleActions();

        public override VisualElement CreatePropertyGUI(
            SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.marginTop = 2;
            root.style.marginBottom = 2;

            SerializedProperty inputProperty =
                property.FindPropertyRelative("inputAction");
            SerializedProperty actionProperty =
                property.FindPropertyRelative("action");

            List<string> choices = new List<string>(Actions.Length);
            for (int index = 0; index < Actions.Length; index++)
            {
                choices.Add(GetLabel(Actions[index]));
            }

            int currentIndex = Array.IndexOf(
                Actions,
                (InputActionId)inputProperty.enumValueIndex);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            PopupField<string> inputPopup =
                new PopupField<string>("输入动作", choices, currentIndex);
            inputPopup.style.flexGrow = 1;
            inputPopup.style.minWidth = 140;
            inputPopup.RegisterValueChangedCallback(evt =>
            {
                int choiceIndex = choices.IndexOf(evt.newValue);
                if (choiceIndex < 0)
                {
                    return;
                }

                inputProperty.enumValueIndex =
                    (int)Actions[choiceIndex];
                inputProperty.serializedObject.ApplyModifiedProperties();
            });
            root.Add(inputPopup);

            PropertyField actionField =
                new PropertyField(actionProperty, "动作数据");
            actionField.style.flexGrow = 2;
            actionField.style.marginLeft = 4;
            root.Add(actionField);
            return root;
        }

        private static string GetLabel(InputActionId action)
        {
            return InputActionBindingPolicy.GetDisplayName(action);
        }
    }
}
