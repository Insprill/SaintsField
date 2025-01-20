using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Drawers.AdvancedDropdownDrawer;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaintsField.Editor.Drawers.ShaderDrawers.ShaderParamDrawer
{
    public partial class ShaderParamAttributeDrawer
    {
        private static string DropdownButtonName(SerializedProperty property) => $"{property.propertyPath}__ShaderParam_DropdownButton";
        private static string HelpBoxName(SerializedProperty property) => $"{property.propertyPath}__ShaderParam_HelpBox";


        protected override VisualElement CreateFieldUIToolKit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            VisualElement container, FieldInfo info, object parent)
        {
            UIToolkitUtils.DropdownButtonField dropdownButton = UIToolkitUtils.MakeDropdownButtonUIToolkit(property.displayName);
            dropdownButton.name = DropdownButtonName(property);
            return dropdownButton;
        }

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, FieldInfo info, object parent)
        {
            return new HelpBox("", HelpBoxMessageType.Error)
            {
                style =
                {
                    display = DisplayStyle.None,
                    flexGrow = 1,
                },
                name = HelpBoxName(property),
            };
        }

        protected override void OnAwakeUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            IReadOnlyList<PropertyAttribute> allAttributes, VisualElement container, Action<object> onValueChangedCallback, FieldInfo info, object parent)
        {
            HelpBox helpBox = container.Q<HelpBox>(HelpBoxName(property));

            if (property.propertyType != SerializedPropertyType.String &&
                property.propertyType != SerializedPropertyType.Integer)
            {
                helpBox.text = $"{property.propertyType} is not supported";
                helpBox.style.display = DisplayStyle.Flex;
                return;
            }

            ShaderParamAttribute shaderParamAttribute = (ShaderParamAttribute) saintsAttribute;

            UpdateDisplay(container, shaderParamAttribute, property, info, parent);

            UIToolkitUtils.DropdownButtonField dropdownButton = container.Q<UIToolkitUtils.DropdownButtonField>(DropdownButtonName(property));
            dropdownButton.ButtonElement.clicked += () =>
            {
                (string error, Material material) = GetMaterial(shaderParamAttribute.TargetName, shaderParamAttribute.Index, property, info, parent);
                if (error != "")
                {
#if SAINTSFIELD_DEBUG
                    Debug.LogError(error);
#endif
                    UpdateDisplay(container, shaderParamAttribute, property, info, parent);
                    return;
                }

                ShaderInfo[] shaderInfos = GetShaderInfo(material).ToArray();
                (bool foundShaderInfo, ShaderInfo selectedShaderInfo) = GetSelectedShaderInfo(property, shaderInfos);
                AdvancedDropdownMetaInfo dropdownMetaInfo = GetMetaInfo(foundShaderInfo, selectedShaderInfo, shaderInfos, false);

                float maxHeight = Screen.currentResolution.height - dropdownButton.worldBound.y - dropdownButton.worldBound.height - 100;
                Rect worldBound = dropdownButton.worldBound;
                if (maxHeight < 100)
                {
                    worldBound.y -= 100 + worldBound.height;
                    maxHeight = 100;
                }

                UnityEditor.PopupWindow.Show(worldBound, new SaintsAdvancedDropdownUIToolkit(
                    dropdownMetaInfo,
                    dropdownButton.worldBound.width,
                    maxHeight,
                    false,
                    (_, curItem) =>
                    {
                        ShaderInfo shaderInfo = (ShaderInfo) curItem;
                        // ReSharper disable once ConvertIfStatementToSwitchStatement
                        if (property.propertyType == SerializedPropertyType.String)
                        {
                            property.stringValue = shaderInfo.PropertyName;
                            property.serializedObject.ApplyModifiedProperties();
                            onValueChangedCallback(shaderInfo.PropertyName);
                        }
                        else if (property.propertyType == SerializedPropertyType.Integer)
                        {
                            property.intValue = shaderInfo.PropertyID;
                            property.serializedObject.ApplyModifiedProperties();
                            onValueChangedCallback(shaderInfo.PropertyID);
                        }
                    }
                ));
            };
        }

        protected override void OnValueChanged(SerializedProperty property, ISaintsAttribute saintsAttribute, int index, VisualElement container,
            FieldInfo info, object parent, Action<object> onValueChangedCallback, object newValue)
        {
            UpdateDisplay(container, (ShaderParamAttribute) saintsAttribute, property, info, parent);
        }

        private static void UpdateDisplay(VisualElement container, ShaderParamAttribute shaderParamAttribute, SerializedProperty property, FieldInfo info, object parent)
        {
            UIToolkitUtils.DropdownButtonField dropdownButton = container.Q<UIToolkitUtils.DropdownButtonField>(DropdownButtonName(property));
            HelpBox helpBox = container.Q<HelpBox>(HelpBoxName(property));

            (string error, Material material) = GetMaterial(shaderParamAttribute.TargetName, shaderParamAttribute.Index, property, info, parent);
            if (error != "")
            {
                // dropdownButton.SetEnabled(false);

                // ReSharper disable once InvertIf
                if(helpBox.text != error)
                {
                    helpBox.text = error;
                    helpBox.style.display = DisplayStyle.Flex;
                }

                return;
            }

            (bool foundShaderInfo, ShaderInfo selectedShaderInfo) = GetSelectedShaderInfo(property, GetShaderInfo(material));

            if(!foundShaderInfo)
            {
                // dropdownButton.SetEnabled(true);
                string notFoundError;
                if (property.propertyType == SerializedPropertyType.String)
                {
                    string stringValue = property.stringValue;
                    if (string.IsNullOrEmpty(stringValue))
                    {
                        // ReSharper disable once InvertIf
                        if(helpBox.style.display != DisplayStyle.None)
                        {
                            helpBox.text = "";
                            helpBox.style.display = DisplayStyle.None;
                        }
                        return;
                    }

                    notFoundError = $"{stringValue} not found in shader";
                }
                else
                {
                    notFoundError = $"{property.intValue} not found in shader";
                }
                // ReSharper disable once InvertIf
                if(helpBox.text != notFoundError)
                {
                    helpBox.text = notFoundError;
                    helpBox.style.display = DisplayStyle.Flex;
                }
                return;
            }

            // dropdownButton.SetEnabled(true);
            dropdownButton.ButtonLabelElement.text = selectedShaderInfo.ToString();
        }
    }
}
