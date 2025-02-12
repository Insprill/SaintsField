#if UNITY_2021_3_OR_NEWER && !SAINTSFIELD_UI_TOOLKIT_DISABLE
using System;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using SaintsField.Playa;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


namespace SaintsField.Editor.Playa.Renderer
{
    public partial class SerializedFieldBaseRenderer
    {
        private PropertyField _result;

        private class UserDataPayload
        {
            public string XML;
            public Label Label;
            public string FriendlyName;
            public RichTextDrawer RichTextDrawer;

            public bool TableHasSize;
        }

        private VisualElement _fieldElement;
        private bool _arraySizeCondition;
        private bool _richLabelCondition;
        private bool _tableCondition;

        private static string NameTableContainer(SerializedProperty property)
        {
            return $"saints-table-container-{property.propertyPath}";
        }

        protected override (VisualElement target, bool needUpdate) CreateTargetUIToolkit()
        {
            UserDataPayload userDataPayload = new UserDataPayload
            {
                FriendlyName = FieldWithInfo.SerializedProperty.displayName,
            };

            (VisualElement result, bool serializedUpdate) = CreateSerializedUIToolkit();

            if(result != null)
            {
                result.userData = userDataPayload;
            }

            OnArraySizeChangedAttribute onArraySizeChangedAttribute = FieldWithInfo.PlayaAttributes.OfType<OnArraySizeChangedAttribute>().FirstOrDefault();
            if (onArraySizeChangedAttribute != null)
            {
                OnArraySizeChangedUIToolkit(onArraySizeChangedAttribute.Callback, result, FieldWithInfo.SerializedProperty, (MemberInfo)FieldWithInfo.FieldInfo ?? FieldWithInfo.PropertyInfo);
            }

            // disable/enable/show/hide
            bool ifCondition = FieldWithInfo.PlayaAttributes.Count(each => each is PlayaShowIfAttribute
                                                                           // ReSharper disable once MergeIntoLogicalPattern
                                                                           || each is PlayaEnableIfAttribute
                                                                           // ReSharper disable once MergeIntoLogicalPattern
                                                                           || each is PlayaDisableIfAttribute) > 0;
            _arraySizeCondition = FieldWithInfo.PlayaAttributes.Any(each => each is IPlayaArraySizeAttribute);
            _richLabelCondition = FieldWithInfo.PlayaAttributes.Any(each => each is PlayaRichLabelAttribute);
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_SAINTS_EDITOR_SERIALIZED_FIELD_RENDERER
            Debug.Log(
                $"SerField: {FieldWithInfo.SerializedProperty.displayName}({FieldWithInfo.SerializedProperty.propertyPath}); if={ifCondition}; arraySize={_arraySizeCondition}, richLabel={_richLabelCondition}");
#endif

            bool needUpdate = serializedUpdate || ifCondition || _arraySizeCondition || _richLabelCondition || _tableCondition;

            return (_fieldElement = result, needUpdate);
        }

        protected abstract (VisualElement target, bool needUpdate) CreateSerializedUIToolkit();

        private static void OnArraySizeChangedUIToolkit(string callback, VisualElement result, SerializedProperty property, MemberInfo memberInfo)
        {
            if (!property.isArray)
            {
                Debug.LogWarning($"{property.propertyPath} is no an array/list");
                return;
            }

            int arraySize = property.arraySize;
            // don't use TrackPropertyValue because if you remove anything from list, it gives error
            // this is Unity's fault
            // result.TrackPropertyValue(property, p =>
            result.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                int newSize;
                try
                {
                    newSize = property.arraySize;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (NullReferenceException)
                {
                    return;
                }

                if (newSize == arraySize)
                {
                    return;
                }

                arraySize = newSize;
                InvokeArraySizeCallback(callback, property, memberInfo);
            });
        }

        protected override PreCheckResult OnUpdateUIToolKit(VisualElement root)
        // private void UIToolkitCheckUpdate(VisualElement result, bool ifCondition, bool arraySizeCondition, bool richLabelCondition, FieldInfo info, object parent)
        {
            PreCheckResult preCheckResult = base.OnUpdateUIToolKit(root);

            if(_arraySizeCondition)
            {

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_SAINTS_EDITOR_SERIALIZED_FIELD_RENDERER
                Debug.Log(
                    $"SerField: {FieldWithInfo.SerializedProperty.displayName}->{FieldWithInfo.SerializedProperty.propertyPath}; preCheckResult.ArraySize={preCheckResult.ArraySize}, curSize={FieldWithInfo.SerializedProperty.arraySize}");
#endif
                if (preCheckResult.ArraySize != -1 &&
                    ((preCheckResult.ArraySize == 0 && FieldWithInfo.SerializedProperty.arraySize > 0)
                     || (preCheckResult.ArraySize >= 1 && FieldWithInfo.SerializedProperty.arraySize == 0)))
                {
                    FieldWithInfo.SerializedProperty.arraySize = preCheckResult.ArraySize;
                    FieldWithInfo.SerializedProperty.serializedObject.ApplyModifiedProperties();
                }
            }

            if (_richLabelCondition)
            {
                string xml = preCheckResult.RichLabelXml;
                // Debug.Log(xml);
                UserDataPayload userDataPayload = (UserDataPayload) _fieldElement.userData;
                if (xml != userDataPayload.XML)
                {
                    // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
                    if (userDataPayload.RichTextDrawer == null)
                    {
                        userDataPayload.RichTextDrawer = new RichTextDrawer();
                    }
                    if(userDataPayload.Label == null)
                    {
                        UIToolkitUtils.WaitUntilThenDo(
                            _fieldElement,
                            () =>
                            {
                                Label label = _fieldElement.Q<Label>(className: "unity-label");
                                if (label == null)
                                {
                                    return (false, null);
                                }
                                return (true, label);
                            },
                            label =>
                            {
                                userDataPayload.Label = label;
                            }
                        );
                    }
                    else
                    {
                        userDataPayload.XML = xml;
                        UIToolkitUtils.SetLabel(userDataPayload.Label, RichTextDrawer.ParseRichXml(xml, userDataPayload.FriendlyName, GetMemberInfo(FieldWithInfo), FieldWithInfo.Target), userDataPayload.RichTextDrawer);
                    }
                }
            }

            return preCheckResult;
        }
    }
}
#endif
