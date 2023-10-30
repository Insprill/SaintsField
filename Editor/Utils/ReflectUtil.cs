﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ExtInspector.Editor.Utils
{
    public static class ReflectUil
    {
        public static FieldInfo GetField(object target, string fieldName)
        {
            return GetAllFields(target, f => f.Name.Equals(fieldName, StringComparison.Ordinal)).FirstOrDefault();
        }

        public static IEnumerable<FieldInfo> GetAllFields(object target, Func<FieldInfo, bool> predicate)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");
                yield break;
            }

            List<Type> types = GetSelfAndBaseTypes(target);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                IEnumerable<FieldInfo> fieldInfos = types[i]
                    .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (FieldInfo fieldInfo in fieldInfos)
                {
                    yield return fieldInfo;
                }
            }
        }

        public static List<Type> GetSelfAndBaseTypes(object target)
        {
            List<Type> types = new List<Type>
            {
                target.GetType(),
            };

            while (types.Last().BaseType != null)
            {
                types.Add(types.Last().BaseType);
            }

            types.Reverse();

            return types;
        }

        public static object GetTargetObjectWithProperty(SerializedProperty property)
        {
            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                string element = elements[i];
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();

            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(source);
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    return property.GetValue(source, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            if (GetValue_Imp(source, name) is not IEnumerable enumerable)
            {
                return null;
            }

            IEnumerator enumerator = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enumerator.MoveNext())
                {
                    return null;
                }
            }

            return enumerator.Current;
        }

        public enum GetPropType
        {
            NotFound,
            Field,
            Method,
        }

        public static (GetPropType getPropType, object fieldOrMethodInfo) GetProp(Type targetType, string fieldName)
        {
            const BindingFlags bindAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                                          BindingFlags.Public | BindingFlags.DeclaredOnly;

            FieldInfo fieldInfo = targetType.GetField(fieldName, bindAttr);
            if (fieldInfo == null)
            {
                fieldInfo = targetType.GetField($"<{fieldName}>k__BackingField", bindAttr);
            }

            if (fieldInfo != null)
            {
                return (GetPropType.Field, fieldInfo);
                // object value = findFieldInfo.GetValue(target);
                // buttonLabelXml = value == null ? string.Empty : value.ToString();
            }

            MethodInfo methodInfo = targetType.GetMethod(fieldName, bindAttr);

            // if (methodInfo == null)
            // {
            //     methodInfo = targetType.GetMethod($"<{aboveButtonAttribute.ButtonLabel}>k__BackingField",
            //         bindAttr);
            // }
            return methodInfo == null ? (GetPropType.NotFound, null) : (GetPropType.Method, methodInfo);

            //
            // _error = "";
            // ParameterInfo[] methodParams = methodInfo.GetParameters();
            // Debug.Assert(methodParams.All(p => p.IsOptional));
            // // Debug.Assert(methodInfo.ReturnType == typeof(string));
            // if (methodInfo.ReturnType != typeof(string))
            // {
            //     _error =
            //         $"Return type of callback method `{aboveButtonAttribute.ButtonLabel}` should be string";
            //     buttonLabelXml = aboveButtonAttribute.ButtonLabel;
            // }
            // else
            // {
            //     buttonLabelXml =
            //         (string)methodInfo.Invoke(target, methodParams.Select(p => p.DefaultValue).ToArray());
            // }
        }

    }
}
