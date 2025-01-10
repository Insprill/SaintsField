using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.AutoRunner;
using SaintsField.Editor.Linq;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SaintsField.Editor.Drawers.XPathDrawers.GetByXPathDrawer
{
    public partial class GetByXPathAttributeDrawer: IAutoRunnerFixDrawer
    {
        // this will give array and array items; it needs to be processed separately
        public AutoRunnerFixerResult AutoRunFix(PropertyAttribute propertyAttribute,
            IReadOnlyList<PropertyAttribute> allAttributes,
            SerializedProperty property, MemberInfo memberInfo, object parent)
        {
            // ReSharper disable once UseNegatedPatternInIsExpression
            if(!(propertyAttribute is GetByXPathAttribute getByXPathAttribute))
            {
                return null;
            }

            GetByXPathAttribute[] getByXPathAttributes = allAttributes.OfType<GetByXPathAttribute>().ToArray();

            if (!ReferenceEquals(getByXPathAttribute, getByXPathAttributes[0]))
            {
                return null;
            }

            if(NothingSigner(getByXPathAttribute))
            {
                return null;
            }

            (string typeError, Type expectType, Type expectInterface) = GetExpectedTypeOfProp(property, memberInfo, parent);
            if (typeError != "")
            {
                return new AutoRunnerFixerResult
                {
                    Error = "",
                    ExecError = typeError,
                };
            }

            GetByXPathGenericCache target = new GetByXPathGenericCache
            {
                ImGuiRenderCount = 1,
                Error = "",
                ExpectedType = expectType,
                ExpectedInterface = expectInterface,
            };

            GetXPathValuesResult iterResults = GetXPathValues(
                getByXPathAttributes.SelectMany(each => each.XPathInfoAndList).ToArray(),
                target.ExpectedType,
                target.ExpectedInterface,
                property,
                memberInfo,
                parent);
            if(iterResults.XPathError != "")
            {
                return new AutoRunnerFixerResult
                {
                    Error = "",
                    ExecError = iterResults.XPathError,
                };
            }

            object[] expandedResults = iterResults.Results.ToArray();
            int resultsLength = expandedResults.Length;

            // Debug.Log(property.propertyPath);
            if (property.isArray)
            {
                if (property.arraySize == resultsLength)
                {
                    return null;
                }

                return new AutoRunnerFixerResult
                {
                    ExecError = "",
                    Error =
                        $"{property.displayName}({property.propertyPath}): Array size expected {expandedResults.Length}, got {property.arraySize}\nYou need to re-run the check after this is fixed",
                    CanFix = true,
                    Callback = () =>
                    {
                        property.arraySize = resultsLength;
                        property.serializedObject.ApplyModifiedProperties();
                    },
                };
            }

            // this is actually: isArrayItem
            bool isArray = SerializedUtils.PropertyPathIndex(property.propertyPath) != -1;
            if (isArray)
            {
                (SerializedProperty arrProperty, int _, string arrError) =
                    Util.GetArrayProperty(property, memberInfo, parent);
                if (arrError != "")
                {
                    return new AutoRunnerFixerResult
                    {
                        Error = "",
                        ExecError = arrError,
                    };
                }

                target.ArrayProperty = arrProperty;
            }

            // not directly array target
            foreach ((object targetResult, int index) in expandedResults.WithIndex())
            {
                SerializedProperty processingProperty = isArray
                    ? target.ArrayProperty.GetArrayElementAtIndex(index)
                    : property;
                int propertyCacheKey = isArray
                    ? index
                    : -1;

                (SerializedUtils.FieldOrProp fieldOrProp, object fieldParent) = SerializedUtils.GetFieldInfoAndDirectParent(processingProperty);

                PropertyCache propertyCache = target.IndexToPropertyCache[propertyCacheKey] = new PropertyCache
                {
                    Error = "",
                    // ReSharper disable once RedundantCast
                    MemberInfo = fieldOrProp.IsField? (MemberInfo)fieldOrProp.FieldInfo: fieldOrProp.PropertyInfo,
                    Parent = fieldParent,
                    SerializedProperty = processingProperty,
                };

                // propertyCache.SerializedProperty = processingProperty;

                (string originalValueError, int _, object originalValue) = Util.GetValue(processingProperty, propertyCache.MemberInfo, propertyCache.Parent);
                if (originalValueError != "")
                {
                    return new AutoRunnerFixerResult
                    {
                        Error = "",
                        ExecError = originalValueError,
                    };
                }

                propertyCache.OriginalValue = originalValue;
                bool fieldIsNull = Util.IsNull(originalValue);
                propertyCache.TargetValue = targetResult;
                bool targetIsNull = Util.IsNull(targetResult);
                propertyCache.TargetIsNull = targetIsNull;

                propertyCache.MisMatch = !Util.GetIsEqual(originalValue, targetResult);

                if(propertyCache.MisMatch)
                {
                    bool resign = getByXPathAttribute.AutoResignToValue && !targetIsNull;
                    if (!resign)
                    {
                        resign = getByXPathAttribute.AutoResignToNull && targetIsNull;
                    }
                    if (!resign)
                    {
                        resign = getByXPathAttribute.InitSign && fieldIsNull;
                    }

                    if (resign)
                    {
                        return new AutoRunnerFixerResult
                        {
                            Error = $"{property.displayName}({property.propertyPath}): Value mismatch {originalValue} -> {targetResult}",
                            ExecError = "",
                            CanFix = true,
                            Callback = () =>
                            {
                                DoSignPropertyCache(propertyCache);
                            },
                        };
                    }
                }
            }

            return null;
        }
    }
}
