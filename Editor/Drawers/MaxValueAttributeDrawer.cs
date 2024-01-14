﻿using System;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaintsField.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(MaxValueAttribute))]
    public class MaxValueAttributeDrawer : SaintsPropertyDrawer
    {
        #region UIToolkit
        private string _error = "";

        protected override bool DrawPostFieldImGui(Rect position, SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute, bool valueChanged)
        {
            if (!valueChanged)
            {
                return true;
            }

            object parentTarget = GetParentTarget(property);

            MaxValueAttribute minValueAttribute = (MaxValueAttribute)saintsAttribute;
            (string error, float valueLimit) = GetLimitFloat(minValueAttribute, parentTarget);

            _error = error;

            if (_error != "")
            {
                return true;
            }

            if (property.propertyType == SerializedPropertyType.Float)
            {
                float curValue = property.floatValue;

                if (valueLimit < curValue)
                {
                    property.floatValue = valueLimit;
                    SetValueChanged(property);
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int curValue = property.intValue;

                if (valueLimit < curValue)
                {
                    property.intValue = (int)valueLimit;
                    SetValueChanged(property);
                }
            }
            return true;
        }

        protected override bool WillDrawBelow(SerializedProperty property, ISaintsAttribute saintsAttribute) => _error != "";

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label, float width, ISaintsAttribute saintsAttribute) => _error == "" ? 0 : ImGuiHelpBox.GetHeight(_error, width, MessageType.Error);

        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute) => _error == "" ? position : ImGuiHelpBox.Draw(position, _error, MessageType.Error);
        #endregion

        private static (string error, float valueLimit) GetLimitFloat(MaxValueAttribute maxValueAttribute, object parentTarget)
        {
            return maxValueAttribute.ValueCallback == null
                ? ("", maxValueAttribute.Value)
                : Util.GetCallbackFloat(parentTarget, maxValueAttribute.ValueCallback);
        }

                #region UIToolkit

        private static string NameHelpBox(SerializedProperty property, int index) =>
            $"{property.propertyPath}_{index}__MaxValue_HelpBox";

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, object parent)
        {
            return new HelpBox("", HelpBoxMessageType.Error)
            {
                name = NameHelpBox(property, index),
                style =
                {
                    display = DisplayStyle.None,
                },
            };
        }

        protected override void OnUpdateUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            int index,
            VisualElement container, Action<object> onValueChangedCallback, object parent)
        {
            HelpBox helpBox = container.Q<HelpBox>(NameHelpBox(property, index));
            MaxValueAttribute minValueAttribute = (MaxValueAttribute)saintsAttribute;
            (string error, float valueLimit) = GetLimitFloat(minValueAttribute, parent);

            if(helpBox.text != error)
            {
                helpBox.style.display = error == "" ? DisplayStyle.None : DisplayStyle.Flex;
                helpBox.text = error;
            }

            if (error != "")
            {
                return;
            }

            if(property.propertyType == SerializedPropertyType.Float && property.floatValue > valueLimit)
            {
                property.floatValue = valueLimit;
                property.serializedObject.ApplyModifiedProperties();
                onValueChangedCallback.Invoke(valueLimit);
            }
            else if(property.propertyType == SerializedPropertyType.Integer && property.intValue > (int)valueLimit)
            {
                property.intValue = (int)valueLimit;
                property.serializedObject.ApplyModifiedProperties();
                onValueChangedCallback.Invoke((int)valueLimit);
            }
        }

        #endregion
    }
}
