﻿using System;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;
#if UNITY_2021_3_OR_NEWER
using UnityEngine.UIElements;
#endif
using Object = UnityEngine.Object;

namespace SaintsField.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(FindComponentAttribute))]
    public class FindComponentAttributeDrawer: SaintsPropertyDrawer
    {
        #region IMGUI
        private string _error = "";

        protected override float GetPostFieldWidth(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute) => 0;

        protected override bool DrawPostFieldImGui(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute,
            bool valueChanged)
        {
            (string error, Object result) = DoCheckComponent(property, saintsAttribute);
            if (error != "")
            {
                _error = error;
                return false;
            }

            if(result != null)
            {
                SetValueChanged(property);
            }
            return true;
        }

        protected override bool WillDrawBelow(SerializedProperty property,
            ISaintsAttribute saintsAttribute) => _error != "";

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label, float width, ISaintsAttribute saintsAttribute) => _error == ""? 0: ImGuiHelpBox.GetHeight(_error, width, EMessageType.Error);
        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute) => _error == ""? position: ImGuiHelpBox.Draw(position, _error, EMessageType.Error);
        #endregion

        private static (string error, Object result) DoCheckComponent(SerializedProperty property, ISaintsAttribute saintsAttribute)
        {
            if (property.objectReferenceValue != null)
            {
                return ("", null);
            }

            FindComponentAttribute findComponentAttribute = (FindComponentAttribute) saintsAttribute;
            Type fieldType = SerializedUtils.GetType(property);

            // Type type = findComponentAttribute.CompType ?? fieldType;

            Transform transform;
            switch (property.serializedObject.targetObject)
            {
                case Component component:
                    transform = component.transform;
                    break;
                case GameObject gameObject:
                    transform = gameObject.transform;
                    break;
                default:
                    return ("GetComponentInChildrenAttribute can only be used on Component or GameObject", null);
            }

            Object componentInChildren = null;
            foreach (string findPath in findComponentAttribute.Paths)
            {
                Transform findTarget = transform.Find(findPath);
                if (!findTarget)
                {
                    continue;
                }

                if(fieldType == typeof(GameObject))
                {
                    componentInChildren = findTarget.gameObject;
                    break;
                }

                if (fieldType == typeof(Transform))
                {
                    componentInChildren = findTarget;
                    break;
                }

                componentInChildren = findTarget.GetComponent(fieldType);
                if (componentInChildren != null)
                {
                    break;
                }
            }

            if (componentInChildren == null)
            {
                return ($"No {fieldType} found in paths: {string.Join(", ", findComponentAttribute.Paths)}", null);
            }

            property.objectReferenceValue = componentInChildren;

            return ("", componentInChildren);
        }

#if UNITY_2021_3_OR_NEWER

        #region UIToolkit

        private static string NamePlaceholder(SerializedProperty property, int index) =>
            $"{property.propertyPath}_{index}__FindComponent";

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property,
            ISaintsAttribute saintsAttribute, int index, VisualElement container, object parent)
        {
            return new HelpBox("", HelpBoxMessageType.Error)
            {
                style =
                {
                    display = DisplayStyle.None,
                },
                name = NamePlaceholder(property, index),
            };
        }

        protected override void OnAwakeUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            int index,
            VisualElement container, Action<object> onValueChangedCallback, object parent)
        {
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_FIND_COMPONENT
            Debug.Log($"FindComponent DrawPostFieldUIToolkit for {property.propertyPath}");
#endif
            (string error, Object result) = DoCheckComponent(property, saintsAttribute);
            HelpBox helpBox = container.Q<HelpBox>(NamePlaceholder(property, index));
            if (error != helpBox.text)
            {
                helpBox.style.display = error == "" ? DisplayStyle.None : DisplayStyle.Flex;
                helpBox.text = error;
            }

            // ReSharper disable once InvertIf
            if (result)
            {
                property.serializedObject.ApplyModifiedProperties();
                onValueChangedCallback.Invoke(result);
            }
        }
        #endregion

#endif
    }
}
