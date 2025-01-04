﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Linq;
using SaintsField.Editor.Utils;
using SaintsField.SaintsXPathParser;
using SaintsField.SaintsXPathParser.XPathAttribute;
using SaintsField.SaintsXPathParser.XPathFilter;
using SaintsField.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaintsField.Editor.Drawers.XPathDrawers.GetByXPathDrawer
{
    // Note about this drawer in UI Toolkit:
    // if the list/array is not drawn by a ListView, the 1st item the drawer will fail to deliver the correct message to the rest of the items
    // and if the list is drawn by a custom drawer, then the 1st item will not even get drawn
    // the correct way is to think is that there is no way to correctly store the cached data -> use a static
    // it's not granted that the 1st element will get drawn -> use any drawn element
    // because some config just update the data, some config will only update the UI, and some both -> the responsible drawer should only update the data, and any drawn element just fetch the cached data to update it's own UI accordingly

    // ATM: we just think the 1st element will always be drawn, and there is always a listView parent
    // and use the _selfChange to prevent loop calling on element dragging
    [CustomPropertyDrawer(typeof(GetByXPathAttribute))]
#if !SAINTSFIELD_OLD_GETTER
    [CustomPropertyDrawer(typeof(GetComponentAttribute))]
    [CustomPropertyDrawer(typeof(GetComponentInChildrenAttribute))]
    [CustomPropertyDrawer(typeof(GetComponentInParentAttribute))]
    [CustomPropertyDrawer(typeof(GetComponentInParentsAttribute))]
    [CustomPropertyDrawer(typeof(GetComponentInSceneAttribute))]
    [CustomPropertyDrawer(typeof(GetComponentByPathAttribute))]
    [CustomPropertyDrawer(typeof(GetPrefabWithComponentAttribute))]
    [CustomPropertyDrawer(typeof(GetScriptableObjectAttribute))]
    [CustomPropertyDrawer(typeof(FindComponentAttribute))]
#endif
    public partial class GetByXPathAttributeDrawer: SaintsPropertyDrawer
    {
        private class GetByPickerWindow : ObjectSelectWindow
        {
            private Action<Object> _onSelected;
            private EPick _editorPick;

            private IReadOnlyList<Object> _assetObjects;
            private IReadOnlyList<Object> _sceneObjects;

            public static void Open(Object curValue, EPick editorPick, IReadOnlyList<Object> assetObjects, IReadOnlyList<Object> sceneObjects, Action<Object> onSelected)
            {
                GetByPickerWindow thisWindow = CreateInstance<GetByPickerWindow>();
                thisWindow.titleContent = new GUIContent("Select");
                // thisWindow._expectedTypes = expectedTypes;
                thisWindow._assetObjects = assetObjects;
                thisWindow._sceneObjects = sceneObjects;
                thisWindow._onSelected = onSelected;
                thisWindow._editorPick = editorPick;
                thisWindow.SetDefaultActive(curValue);
                // Debug.Log($"call show selector window");
                thisWindow.ShowAuxWindow();
            }

            protected override bool AllowScene =>
                // Debug.Log(_editorPick);
                _editorPick.HasFlag(EPick.Scene);

            protected override bool AllowAssets =>
                // Debug.Log(_editorPick);
                _editorPick.HasFlag(EPick.Assets);

            protected override IEnumerable<ItemInfo> FetchAllAssets()
            {
                // HierarchyProperty property = new HierarchyProperty(HierarchyType.Assets, false);
                return _assetObjects.Select(each => new ItemInfo
                {
                    Object = each,
                    Label = each.name,
                    // Icon = property.icon,
                    InstanceID = each.GetInstanceID(),
                    GuiLabel = new GUIContent(each.name),
                });
            }

            protected override IEnumerable<ItemInfo> FetchAllSceneObject()
            {
                // HierarchyProperty property = new HierarchyProperty(HierarchyType.GameObjects, false);
                return _sceneObjects.Select(each => new ItemInfo
                {
                    Object = each,
                    Label = each.name,
                    // Icon = property.icon,
                    InstanceID = each.GetInstanceID(),
                    GuiLabel = new GUIContent(each.name),
                });
            }

            protected override string Error => "";

            protected override bool IsEqual(ItemInfo itemInfo, Object target)
            {
                return ReferenceEquals(itemInfo.Object, target);
            }

            protected override void OnSelect(ItemInfo itemInfo)
            {
                _onSelected(itemInfo.Object);
            }

            protected override bool FetchAllSceneObjectFilter(ItemInfo itemInfo) => true;

            protected override bool FetchAllAssetsFilter(ItemInfo itemInfo) => true;
        }


        private static void OpenPicker(SerializedProperty property, FieldInfo info, IReadOnlyList<GetByXPathAttribute> getByXPathAttributes, Type expectedType, Type interfaceType, Action<object> onValueChanged, object updatedParent)
        {
            (string getValueError, int _, object curValue) = Util.GetValue(property, info, updatedParent);

            // ReSharper disable once UseNegatedPatternInIsExpression
            if (!(curValue is Object) && curValue != null)
            {
                Debug.LogError($"targetValue is not Object: {curValue}");
                return;
            }

            Object curValueObj = (Object)curValue;

            if (getValueError != "")
            {
                Debug.LogError(getValueError);
                return;
            }

            GetXPathValuesResult r = GetXPathValues(getByXPathAttributes.SelectMany(each => each.XPathInfoAndList).ToArray(),
                expectedType, interfaceType, property, info, updatedParent);
            if (r.XPathError != "")
            {
                Debug.LogError(r.XPathError);
                return;
            }

            Object[] objResults = r.Results.OfType<Object>().ToArray();
            List<Object> assetObj = new List<Object>();
            List<Object> sceneObj = new List<Object>();
            foreach (Object objResult in objResults)
            {
                if (AssetDatabase.GetAssetPath(objResult) != "")
                {
                    assetObj.Add(objResult);
                }
                else
                {
                    sceneObj.Add(objResult);
                }
            }

            GetByPickerWindow.Open(curValueObj, EPick.Assets | EPick.Scene, assetObj, sceneObj, onValueChanged.Invoke);
        }

        // private class InitUserData
        // {
        //     public string Error;
        //     public SerializedProperty Property;
        //     public SerializedProperty TargetProperty;
        //     public FieldInfo Info;
        //     public MemberInfo MemberInfo;
        //     public Type ExpectType;
        //     public Type ExpectInterface;
        //
        //     public int DecoratorIndex;
        //
        //     public SerializedProperty ArrayProperty;
        //     public GetXPathValuesResult ArrayValues;
        //     public GetByXPathAttribute GetByXPathAttribute;
        //     public IReadOnlyList<GetByXPathAttribute> GetByXPathAttributes;
        //
        //     public double ImGuiLastTime;
        //
        //     public CheckFieldResult CheckFieldResult;
        // }

        private static (bool valid, object value) ValidateXPathResult(object each, Type expectType, Type expectInterface)
        {
            object result;
            // Debug.Log($"{each}");
            if (expectType.IsInstanceOfType(each))
            {
                result = each;
            }
            else if (each is Object uObject)
            {
                Object r = Util.GetTypeFromObj(uObject, expectType);

                if (r == null)
                {
                    return (false, null);
                }

                result = r;
            }
            else
            {
                return (false, null);
            }

            if (expectInterface == null)
            {
                return (true, result);
            }

            if (result is GameObject resultGo)
            {
                result = resultGo.transform;
            }
            if(result is Component resultComponent)
            {
                foreach (Component component in resultComponent.GetComponents<Component>())
                {
                    if (expectInterface.IsAssignableFrom(component.GetType()))
                    {
                        return (true, component);
                    }
                }
            }

            bool valid = expectInterface.IsAssignableFrom(result.GetType());
            return valid ? (true, result) : (false, null);
        }

        private static (string error, SerializedProperty targetProperty, MemberInfo targetMemberInfo, Type expectType, Type expectInterface) GetExpectedType(SerializedProperty property, FieldInfo info, object parent)
        {
            Type rawType = ReflectUtils.GetElementType(info.FieldType);
            MemberInfo targetMemberInfo = info;
            if (!typeof(IWrapProp).IsAssignableFrom(rawType))
            {
                return ("", property, targetMemberInfo, rawType, null);
            }

            (string error, int _, object value) = Util.GetValue(property, info, parent);
            if (error != "")
            {
                return (error, property, targetMemberInfo, rawType, null);
            }

            IWrapProp wrapProp = (IWrapProp) value;
            string prop = wrapProp.EditorPropertyName;
            Type expectedType;
            PropertyInfo wrapPropertyInfo = value.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (wrapPropertyInfo == null)
            {
                FieldInfo wrapFieldInfo = value.GetType().GetField(prop, BindingFlags.Public | BindingFlags.Instance);
                Debug.Assert(wrapFieldInfo != null);
                targetMemberInfo = wrapFieldInfo;
                expectedType = wrapFieldInfo.FieldType;
            }
            else
            {
                expectedType = wrapPropertyInfo.PropertyType;
                targetMemberInfo = wrapPropertyInfo;
            }

            SerializedProperty targetProperty = property.FindPropertyRelative(prop) ?? SerializedUtils.FindPropertyByAutoPropertyName(property, prop);

            Type expectedInterface = null;
            Type mostBaseType = ReflectUtils.GetMostBaseType(rawType);
            if(ReflectUtils.IsSubclassOfRawGeneric(typeof(SaintsInterface<,>), mostBaseType))
            {
                expectedInterface = mostBaseType.GetGenericArguments()[1];
            }

            return ("", targetProperty, targetMemberInfo, expectedType, expectedInterface);
        }

        private static (string error, Type expectType, Type expectInterface) GetExpectedTypeOfProp(
            SerializedProperty property, FieldInfo info, object parent)
        {
            Type rawType = ReflectUtils.GetElementType(info.FieldType);
            if (!typeof(IWrapProp).IsAssignableFrom(rawType))
            {
                return ("", rawType, null);
            }

            (string error, int _, object value) = Util.GetValue(property, info, parent);
            if (error != "")
            {
                return (error, rawType, null);
            }

            IWrapProp wrapProp = (IWrapProp) value;
            string prop = wrapProp.EditorPropertyName;
            Type expectedType;
            PropertyInfo wrapPropertyInfo = value.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (wrapPropertyInfo == null)
            {
                FieldInfo wrapFieldInfo = value.GetType().GetField(prop, BindingFlags.Public | BindingFlags.Instance);
                Debug.Assert(wrapFieldInfo != null);
                expectedType = wrapFieldInfo.FieldType;
            }
            else
            {
                expectedType = wrapPropertyInfo.PropertyType;
            }

            Type expectedInterface = null;
            Type mostBaseType = ReflectUtils.GetMostBaseType(rawType);
            if(ReflectUtils.IsSubclassOfRawGeneric(typeof(SaintsInterface<,>), mostBaseType))
            {
                expectedInterface = mostBaseType.GetGenericArguments()[1];
            }

            return ("", expectedType, expectedInterface);
        }

        private class CheckFieldResult
        {
            public string Error;
            public bool MisMatch;
            public object OriginalValue;
            public object TargetValue;
            public int Index;
        }

        private static CheckFieldResult CheckField(SerializedProperty property, FieldInfo info, object parent, object targetValue)
        {
            (string propError, int propIndex, object propValue) = Util.GetValue(property, info, parent);

            if (propError != "")
            {
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
                Debug.Log($"Error {property.propertyPath}: {propError}");
#endif
                return new CheckFieldResult
                {
                    Error = propError,
                    MisMatch = false,
                    OriginalValue = propValue,
                    TargetValue = targetValue,
                    Index = propIndex,
                };
            }

            if(propValue is IWrapProp wrapProp)
            {
                propValue = Util.GetWrapValue(wrapProp);
            }

            if (Util.GetIsEqual(propValue, targetValue))
            {
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
                Debug.Log($"Value Equal {property.propertyPath}: {propValue} == {targetValue}");
#endif
                return new CheckFieldResult
                {
                    Error = "",
                    MisMatch = false,
                    OriginalValue = propValue,
                    TargetValue = targetValue,
                    Index = propIndex,
                };
            }

#if SAINTSFIELD_DEBUG_GET_BY_XPATH
            Debug.Log($"Mismatch {property.propertyPath}: {propValue} != {targetValue}; {propValue is Object}/{targetValue is Object}");
#endif
            return new CheckFieldResult
            {
                Error = "",
                MisMatch = true,
                OriginalValue = propValue,
                TargetValue = targetValue,
                Index = propIndex,
            };
        }

                private static void UpdateImGuiSharedCache(GetByXPathGenericCache target, bool isFirstTime, SerializedProperty property, FieldInfo info)
        {
            target.UpdatedLastTime = EditorApplication.timeSinceStartup;

            target.Error = "";
            if (target.Parent == null || target.GetByXPathAttributes == null)
            {
                (GetByXPathAttribute[] attributes, object parent) = SerializedUtils.GetAttributesAndDirectParent<GetByXPathAttribute>(property);
                if(parent == null)
                {
                    target.Error = "Can not get parent for property";
                    return;
                }

                target.Parent = parent;
                target.GetByXPathAttributes = attributes;
            }

            bool isArray = SerializedUtils.PropertyPathIndex(property.propertyPath) != -1;

            if (isArray)
            {
                bool reSign = target.ArrayProperty == null;
                if (!reSign)
                {
                    try
                    {
                        int _ = target.ArrayProperty.arraySize;
                    }
                    catch (NullReferenceException)
                    {
                        reSign = true;
                    }
                    catch (ObjectDisposedException)
                    {
                        reSign = true;
                    }
                }
                if(reSign)
                {
                    (SerializedProperty arrProperty, int _, string arrError) =
                        Util.GetArrayProperty(property, info, target.Parent);
                    if (arrError != "")
                    {
                        target.Error = arrError;
                        return;
                    }

                    target.ArrayProperty = arrProperty;
                }
            }

            if (target.ExpectedType == null)
            {
                (string typeError, Type expectType, Type expectInterface) = GetExpectedTypeOfProp(property, info, target.Parent);
                if (typeError != "")
                {
                    target.Error = typeError;
                    return;
                }

                target.ExpectedType = expectType;
                target.ExpectedInterface = expectInterface;
            }

            GetXPathValuesResult iterResults = GetXPathValues(
                target.GetByXPathAttributes.SelectMany(each => each.XPathInfoAndList).ToArray(),
                target.ExpectedType,
                target.ExpectedInterface,
                property,
                info,
                target.Parent);

            object[] expandedResults = isArray
                ? iterResults.Results.ToArray()
                : new[] {iterResults.Results.FirstOrDefault()};

            if(isArray && target.ArrayProperty.arraySize != expandedResults.Length)
            {
                target.ArrayProperty.arraySize = expandedResults.Length;
                EnqueueSceneViewNotification($"Adjust array {target.ArrayProperty.displayName} to length {target.ArrayProperty.arraySize}");
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
                Debug.Log($"#GetByXPath# Raw: Adjust array {target.ArrayProperty.displayName} to length {target.ArrayProperty.arraySize}");
#endif
                target.ArrayProperty.serializedObject.ApplyModifiedProperties();
            }

            GetByXPathAttribute getByXPathAttribute = target.GetByXPathAttributes[0];

            foreach ((object targetResult, int index) in expandedResults.WithIndex())
            {
                SerializedProperty processingProperty = isArray
                    ? target.ArrayProperty.GetArrayElementAtIndex(index)
                    : property;
                int propertyCacheKey = isArray
                    ? index
                    : -1;
                if(!target.IndexToPropertyCache.TryGetValue(propertyCacheKey, out PropertyCache propertyCache))
                {
                    (SerializedUtils.FieldOrProp fieldOrProp, object fieldParent) = SerializedUtils.GetFieldInfoAndDirectParent(processingProperty);
                    target.IndexToPropertyCache[propertyCacheKey] = propertyCache = new PropertyCache
                    {
                        MemberInfo = fieldOrProp.IsField? fieldOrProp.FieldInfo: fieldOrProp.PropertyInfo,
                        Parent = fieldParent,
                        SerializedProperty = processingProperty,
                    };
                }

                (string originalValueError, int _, object originalValue) = Util.GetValue(processingProperty, propertyCache.MemberInfo, propertyCache.Parent);
                if (originalValueError != "")
                {
                    propertyCache.Error = originalValueError;
                    return;
                }

                propertyCache.OriginalValue = originalValue;
                bool fieldIsNull = Util.IsNull(originalValue);
                propertyCache.TargetValue = targetResult;
                bool targetIsNull = Util.IsNull(targetResult);
                propertyCache.TargetIsNull = targetIsNull;

                propertyCache.MisMatch = !Util.GetIsEqual(originalValue, targetResult);

                // Debug.Log($"#GetByXPath# mismatch={propertyCache.MisMatch}, {originalValue}, {targetResult}");

                if(propertyCache.MisMatch)
                {
                    bool resign = getByXPathAttribute.AutoResignToValue && !targetIsNull;
                    if (!resign)
                    {
                        resign = getByXPathAttribute.AutoResignToNull && targetIsNull;
                    }
                    if (!resign)
                    {
                        resign = isFirstTime && getByXPathAttribute.InitSign && fieldIsNull;
                    }

                    if (resign)
                    {
                        DoSignPropertyCache(propertyCache);
                    }
                }
            }
        }

        public static bool HelperGetArraySize(SerializedProperty arrayProperty, FieldInfo info)
        {
            // return false;
            if (EditorApplication.isPlaying)
            {
                return false;
            }

            if (arrayProperty.arraySize > 0)
            {
                return false;
            }
            string key = arrayProperty.propertyPath;

            (GetByXPathAttribute[] attributes, object parent) = SerializedUtils.GetAttributesAndDirectParent<GetByXPathAttribute>(arrayProperty);

            GetByXPathGenericCache target = new GetByXPathGenericCache
            {
                Error = "",
                GetByXPathAttributes = attributes,
                ArrayProperty = arrayProperty,
            };

            (string typeError, Type expectType, Type expectInterface) = GetExpectedTypeOfProp(arrayProperty, info, parent);
            if (typeError != "")
            {
                return false;
            }

            target.ExpectedType = expectType;
            target.ExpectedInterface = expectInterface;

            GetXPathValuesResult iterResults = GetXPathValues(
                target.GetByXPathAttributes.SelectMany(each => each.XPathInfoAndList).ToArray(),
                target.ExpectedType,
                target.ExpectedInterface,
                arrayProperty,
                null,
                parent);

            object[] expandedResults = iterResults.Results.ToArray();

            if (expandedResults.Length == 0)
            {
                return true;
            }

            arrayProperty.arraySize = expandedResults.Length;
            EnqueueSceneViewNotification($"Adjust array {arrayProperty.displayName} to length {arrayProperty.arraySize}");
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
            Debug.Log($"#GetByXPath# Helper: Adjust array {arrayProperty.displayName} to length {arrayProperty.arraySize}");
#endif
            arrayProperty.serializedObject.ApplyModifiedProperties();

            GetByXPathAttribute getByXPathAttribute = target.GetByXPathAttributes[0];

            if(getByXPathAttribute.InitSign)
            {
                foreach ((object targetResult, int propertyCacheKey) in expandedResults.WithIndex())
                {
                    SerializedProperty processingProperty =
                        target.ArrayProperty.GetArrayElementAtIndex(propertyCacheKey);

                    (SerializedUtils.FieldOrProp fieldOrProp, object fieldParent) =
                        SerializedUtils.GetFieldInfoAndDirectParent(processingProperty);
                    PropertyCache propertyCache
                        = target.IndexToPropertyCache[propertyCacheKey]
                            = new PropertyCache
                            {
                                MemberInfo = fieldOrProp.IsField ? fieldOrProp.FieldInfo : fieldOrProp.PropertyInfo,
                                Parent = fieldParent,
                                SerializedProperty = processingProperty,
                            };

                    propertyCache.OriginalValue = null;
                    propertyCache.TargetValue = targetResult;
                    bool targetIsNull = Util.IsNull(targetResult);
                    propertyCache.TargetIsNull = targetIsNull;

                    propertyCache.MisMatch = !targetIsNull;

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
                    Debug.Log($"#GetByXPath# Helper: Sign {propertyCache.SerializedProperty.propertyPath} from {propertyCache.OriginalValue} to {propertyCache.TargetValue}");
#endif
                    DoSignPropertyCache(propertyCache);
                }

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_GET_BY_XPATH
                Debug.Log($"#GetByXPath# Helper: Apply changes to {arrayProperty.serializedObject.targetObject}");
#endif

                arrayProperty.serializedObject.ApplyModifiedProperties();
            }


            ImGuiSharedCache[key] = target;

            return false;
        }

        private static string GetMismatchErrorMessage(object originalValue, object targetValue, bool targetValueIsNull)
        {
            return $"Expected {(targetValueIsNull? "null": targetValue)}, but got {(Util.IsNull(originalValue)? "null": originalValue)}";
        }
    }
}
