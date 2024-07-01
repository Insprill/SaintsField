﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        protected override float GetPostFieldWidth(Rect position, SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute, FieldInfo info, object parent) => 0;

        protected override bool DrawPostFieldImGui(Rect position, SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute,
            int index,
            OnGUIPayload onGUIPayload, FieldInfo info, object parent)
        {
            (string error, Object result) = DoCheckComponent(property, saintsAttribute, info, parent);
            if (error != "")
            {
                _error = error;
                return false;
            }

            if(result != null)
            {
                onGUIPayload.SetValue(result);
            }
            return true;
        }

        protected override bool WillDrawBelow(SerializedProperty property,
            ISaintsAttribute saintsAttribute, FieldInfo info, object parent) => _error != "";

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label, float width,
            ISaintsAttribute saintsAttribute, FieldInfo info, object parent) => _error == ""? 0: ImGuiHelpBox.GetHeight(_error, width, EMessageType.Error);
        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute, FieldInfo info, object parent) => _error == ""? position: ImGuiHelpBox.Draw(position, _error, EMessageType.Error);
        #endregion

        private static (string error, Object result) DoCheckComponent(SerializedProperty property, ISaintsAttribute saintsAttribute, FieldInfo info, object parent)
        {
            SerializedProperty targetProperty = property;
            Type fieldType = info.FieldType;
            Type interfaceType = null;
            if (property.propertyType == SerializedPropertyType.Generic)
            {
                object propertyValue = SerializedUtils.GetValue(property, info, parent);

                if (propertyValue is IWrapProp wrapProp)
                {
                    Type mostBaseType = SaintsInterfaceDrawer.GetMostBaseType(wrapProp.GetType());
                    if (mostBaseType.IsGenericType && mostBaseType.GetGenericTypeDefinition() == typeof(SaintsInterface<,>))
                    {
                        IReadOnlyList<Type> genericArguments = mostBaseType.GetGenericArguments();
                        if (genericArguments.Count == 2)
                        {
                            interfaceType = genericArguments[1];
                        }
                    }
                    targetProperty = property.FindPropertyRelative(wrapProp.EditorPropertyName) ??
                                     SerializedUtils.FindPropertyByAutoPropertyName(property,
                                         wrapProp.EditorPropertyName);

                    if(targetProperty == null)
                    {
                        return ($"{wrapProp.EditorPropertyName} not found in {property.propertyPath}", null);
                    }

                    SerializedUtils.FieldOrProp wrapFieldOrProp = Util.GetWrapProp(wrapProp);
                    fieldType = wrapFieldOrProp.IsField
                        ? wrapFieldOrProp.FieldInfo.FieldType
                        : wrapFieldOrProp.PropertyInfo.PropertyType;
                    if (interfaceType != null && fieldType != typeof(Component) && !fieldType.IsSubclassOf(typeof(Component)) && typeof(Component).IsSubclassOf(fieldType))
                    {
                        fieldType = typeof(Component);
                    }
                }
            }

            if (targetProperty.objectReferenceValue != null)
            {
                return ("", null);
            }

            FindComponentAttribute findComponentAttribute = (FindComponentAttribute) saintsAttribute;

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

                componentInChildren = interfaceType == null
                    ? findTarget.GetComponent(fieldType)
                    : findTarget.GetComponents(fieldType).FirstOrDefault(interfaceType.IsInstanceOfType);

                if (componentInChildren != null)
                {
                    break;
                }
            }

            if (componentInChildren == null)
            {
                return ($"No {fieldType} found in paths: {string.Join(", ", findComponentAttribute.Paths)}", null);
            }

            targetProperty.objectReferenceValue = componentInChildren;

            return ("", componentInChildren);
        }

#if UNITY_2021_3_OR_NEWER

        #region UIToolkit

        private static string NamePlaceholder(SerializedProperty property, int index) =>
            $"{property.propertyPath}_{index}__FindComponent";

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property,
            ISaintsAttribute saintsAttribute, int index, VisualElement container, FieldInfo info, object parent)
        {
            HelpBox helpBox = new HelpBox("", HelpBoxMessageType.Error)
            {
                style =
                {
                    display = DisplayStyle.None,
                },
                name = NamePlaceholder(property, index),
            };
            helpBox.AddToClassList(ClassAllowDisable);
            return helpBox;
        }

        protected override void OnAwakeUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            int index,
            VisualElement container, Action<object> onValueChangedCallback, FieldInfo info, object parent)
        {
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_FIND_COMPONENT
            Debug.Log($"FindComponent DrawPostFieldUIToolkit for {property.propertyPath}");
#endif
            (string error, Object result) = DoCheckComponent(property, saintsAttribute, info, parent);
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
