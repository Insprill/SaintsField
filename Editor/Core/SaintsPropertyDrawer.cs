﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SaintsField.Editor.Core
{
    // above
    // pre, label, field, post
    // below-
    public abstract partial class SaintsPropertyDrawer: PropertyDrawer, IDisposable
    {
        protected const int LabelLeftSpace = 4;
        protected const int LabelBaseWidth = 120;
        public const int IndentWidth = 15;
        public const float SingleLineHeight = 20f;
        // public const string EmptyRectLabel = "                ";

        // public static bool IsSubDrawer = false;
        private static readonly Dictionary<InsideSaintsFieldScoop.PropertyKey, int> SubDrawCounter = new Dictionary<InsideSaintsFieldScoop.PropertyKey, int>();
        private static readonly Dictionary<InsideSaintsFieldScoop.PropertyKey, int> SubGetHeightCounter = new Dictionary<InsideSaintsFieldScoop.PropertyKey, int>();

        private static readonly Dictionary<Type, IReadOnlyList<(bool isSaints, Type drawerType)>> PropertyAttributeToPropertyDrawers =
            new Dictionary<Type, IReadOnlyList<(bool isSaints, Type drawerType)>>();
#if UNITY_2022_1_OR_NEWER && SAINTSFIELD_IMGUI_DUPLICATE_DECORATOR_FIX
        private static IReadOnlyDictionary<Type, IReadOnlyList<Type>> _propertyAttributeToDecoratorDrawers =
            new Dictionary<Type, IReadOnlyList<Type>>();
#endif

        // [MenuItem("Saints/Debug")]
        // private static void SaintsDebug() => PropertyAttributeToPropertyDrawers.Clear();

        // private class SharedInfo
        // {
        //     public bool Changed;
        //     // public object ParentTarget;
        // }

        // private static readonly Dictionary<string, SharedInfo> PropertyPathToShared = new Dictionary<string, SharedInfo>();

        // private IReadOnlyList<ISaintsAttribute> _allSaintsAttributes;
        // private SaintsPropertyDrawer _labelDrawer;
        // private SaintsPropertyDrawer _fieldDrawer;

        // ReSharper disable once InconsistentNaming
        protected readonly string FieldControlName;

        private static double _sceneViewNotificationTime;
        private static bool _sceneViewNotificationListened;
        private static readonly HashSet<string> SceneViewNotifications = new HashSet<string>();

        private struct SaintsWithIndex : IEquatable<SaintsWithIndex>
        {
            public ISaintsAttribute SaintsAttribute;
            // ReSharper disable once NotAccessedField.Local
            public int Index;

            public bool Equals(SaintsWithIndex other)
            {
                return Equals(SaintsAttribute, other.SaintsAttribute) && Index == other.Index;
            }

            public override bool Equals(object obj)
            {
                return obj is SaintsWithIndex other && Equals(other);
            }

            public override int GetHashCode()
            {
                // return HashCode.Combine(SaintsAttribute, Index);
                return Util.CombineHashCode(SaintsAttribute, Index);
            }
        }

        private readonly Dictionary<SaintsWithIndex, SaintsPropertyDrawer> _cachedDrawer = new Dictionary<SaintsWithIndex, SaintsPropertyDrawer>();
        // private readonly Dictionary<Type, PropertyDrawer> _cachedOtherDrawer = new Dictionary<Type, PropertyDrawer>();
        // private readonly HashSet<Type> _usedDrawerTypes = new HashSet<Type>();
        // private readonly Dictionary<ISaintsAttribute, >
        // private struct UsedAttributeInfo
        // {
        //     public Type DrawerType;
        //     public ISaintsAttribute Attribute;
        // }

        // private readonly List<UsedAttributeInfo> _usedAttributes = new List<UsedAttributeInfo>();
        // private readonly Dictionary<SaintsWithIndex, SaintsPropertyDrawer> _usedAttributes = new Dictionary<SaintsWithIndex, SaintsPropertyDrawer>();

        // private static readonly FieldDrawerConfigAttribute DefaultFieldDrawerConfigAttribute =
        //     new FieldDrawerConfigAttribute(FieldDrawerConfigAttribute.FieldDrawType.Inline, 0);

        // private string _cachedPropPath;
#if UNITY_2022_1_OR_NEWER
        private static Assembly _unityEditorAssemble;
#endif

        // ReSharper disable once PublicConstructorInAbstractClass
        public SaintsPropertyDrawer()
        {
            // Selection.selectionChanged += OnSelectionChanged;
            // Debug.Log($"OnSaintsCreate Start: {SepTitleAttributeDrawer.drawCounter}/{fieldInfo}");
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
            Debug.Log($"new SaintsPropertyDrawer {this}");
#endif
            // if (IsSubDrawer)
            // {
            //     return;
            // }

            FieldControlName = Guid.NewGuid().ToString();

            // _usedAttributes.Clear();

            // _propertyAttributeToDrawers.Clear();

            EnsureAndGetTypeToDrawers();
        }

        private static void OnSceneViewNotification(SceneView sv)
        {
            if (SceneViewNotifications.Count == 0)
            {
                _sceneViewNotificationTime = EditorApplication.timeSinceStartup;
                return;
            }

            if(EditorApplication.timeSinceStartup - _sceneViewNotificationTime < 0.5f)
            {
                return;
            }

            _sceneViewNotificationTime = EditorApplication.timeSinceStartup;
            sv.ShowNotification(new GUIContent(string.Join("\n", SceneViewNotifications)));
            SceneViewNotifications.Clear();
            SceneView.RepaintAll();
        }

        protected static void EnqueueSceneViewNotification(string message)
        {
            if (!_sceneViewNotificationListened)
            {
                _sceneViewNotificationListened = true;
                _sceneViewNotificationTime = EditorApplication.timeSinceStartup;
                SceneView.duringSceneGui += OnSceneViewNotification;
            }

            SceneViewNotifications.Add(message);
        }

        public static IReadOnlyDictionary<Type, IReadOnlyList<(bool isSaints, Type drawerType)>> EnsureAndGetTypeToDrawers()
        {
            if (PropertyAttributeToPropertyDrawers.Count != 0)
            {
                return PropertyAttributeToPropertyDrawers;
            }

            Dictionary<Type, HashSet<Type>> attrToDrawers = new Dictionary<Type, HashSet<Type>>();
#if UNITY_2022_1_OR_NEWER
            Dictionary<Type, List<Type>> attrToDecoratorDrawers =
                new Dictionary<Type, List<Type>>();
#endif

            foreach (Assembly asb in AppDomain.CurrentDomain.GetAssemblies())
            {
#if UNITY_2022_1_OR_NEWER
                if (asb.GetName().Name == "UnityEditor")
                {
                    _unityEditorAssemble = asb;
                }
#endif

                Type[] allTypes = asb.GetTypes();

                // foreach (Type editorType in allTypes.Where(type => type.IsSubclassOf(typeof(UnityEditor.Editor))))
                // {
                //     foreach (CustomEditor editorTargetType in editorType.GetCustomAttributes<CustomEditor>(true))
                //     {
                //         Debug.Log($"{editorTargetType} -> {editorType}");
                //     }
                // }

                List<Type> allSubPropertyDrawers = allTypes
                    // .Where(type => type.IsSubclassOf(typeof(SaintsPropertyDrawer)))
                    .Where(type => type.IsSubclassOf(typeof(PropertyDrawer)))
                    .ToList();

                foreach (Type eachPropertyDrawer in allSubPropertyDrawers)
                {
                    foreach (Type attr in eachPropertyDrawer.GetCustomAttributes<CustomPropertyDrawer>(true)
                                 .Select(instance => typeof(CustomPropertyDrawer)
                                     .GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?.GetValue(instance))
                                 .Where(each => each != null))
                    {
                        if (!attrToDrawers.TryGetValue(attr, out HashSet<Type> attrList))
                        {
                            attrToDrawers[attr] = attrList = new HashSet<Type>();
                        }

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_CORE_DRAWER_INIT
                            Debug.Log($"Found drawer: {attr} -> {eachPropertyDrawer}");
#endif

                        attrList.Add(eachPropertyDrawer);
                    }
                }

#if UNITY_2022_1_OR_NEWER
                List<Type> allSubDecoratorDrawers = allTypes
                    .Where(type => type.IsSubclassOf(typeof(DecoratorDrawer)))
                    .ToList();

                foreach (Type eachDecoratorDrawer in allSubDecoratorDrawers)
                {
                    foreach (Type attr in eachDecoratorDrawer.GetCustomAttributes<CustomPropertyDrawer>(true)
                                 .Select(instance => typeof(CustomPropertyDrawer)
                                     .GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?.GetValue(instance))
                                 .Where(each => each != null))
                    {
                        if (!attrToDecoratorDrawers.TryGetValue(attr, out List<Type> attrList))
                        {
                            attrToDecoratorDrawers[attr] = attrList = new List<Type>();
                        }

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_CORE_DRAWER_INIT
                            Debug.Log($"Found dec drawer: {attr} -> {eachDecoratorDrawer}");
#endif

                        if(!attrList.Contains(eachDecoratorDrawer))
                        {
                            attrList.Add(eachDecoratorDrawer);
                        }
                    }
                }

#endif

            }

            foreach (KeyValuePair<Type, HashSet<Type>> kv in attrToDrawers)
            {
                PropertyAttributeToPropertyDrawers[kv.Key] = kv.Value
                    .Select(each => (each.IsSubclassOf(typeof(SaintsPropertyDrawer)), each))
                    .ToArray();
// #if EXT_INSPECTOR_LOG
//                     Debug.Log($"attr {kv.Key} has drawer(s) {string.Join(",", kv.Value)}");
// #endif
            }

#if UNITY_2022_1_OR_NEWER && SAINTSFIELD_IMGUI_DUPLICATE_DECORATOR_FIX
            _propertyAttributeToDecoratorDrawers = attrToDecoratorDrawers.ToDictionary(each => each.Key, each => (IReadOnlyList<Type>)each.Value);
#endif

            return PropertyAttributeToPropertyDrawers;
        }

        // ~SaintsPropertyDrawer()
        // {
        //     Debug.Log($"[{this}] Stop listening changed");
        //     Selection.selectionChanged -= OnSelectionChanged;
        // }
        //
        // private void OnSelectionChanged()
        // {
        //     Debug.Log($"selection changed: {string.Join(", ", Selection.objects.Select(each => each.ToString()))}");
        // }

        // ~SaintsPropertyDrawer()
        // {
        //     PropertyAttributeToDrawers.Clear();
        // }


        private float _labelFieldBasicHeight = EditorGUIUtility.singleLineHeight;

        protected virtual bool GetThisDecoratorVisibility(ShowIfAttribute targetAttribute, SerializedProperty property, FieldInfo info, object target)
        {
            return true;
        }


        private struct SaintsPropertyInfo
        {
            // ReSharper disable InconsistentNaming
            public SaintsPropertyDrawer Drawer;
            public ISaintsAttribute Attribute;
            public int Index;
            // ReSharper enable InconsistentNaming
        }

        private static bool hasOtherAttributeDrawer(MemberInfo fieldInfo)
        {
            // attributes can not be generic, so just check with the dictionary
            return fieldInfo.GetCustomAttributes()
                // ReSharper disable once UseNegatedPatternInIsExpression
                .Where(each => !(each is ISaintsAttribute))





                .Any(fieldAttribute => PropertyAttributeToPropertyDrawers.Keys.Any(checkType => checkType.IsInstanceOfType(fieldAttribute)));
        }

        private static Type FindOtherPropertyDrawer(FieldInfo fieldInfo)
        {
            List<Type> lookingForType = new List<Type>
            {
                fieldInfo.FieldType,
            };

            Type elementType = ReflectUtils.GetElementType(fieldInfo.FieldType);
            if (elementType != fieldInfo.FieldType)
            {
                lookingForType.Insert(0, elementType);
            }

            foreach (Type fieldType in lookingForType)
            {
                bool isGenericType = fieldType.IsGenericType;
    #if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
                Debug.Log($"FindOtherPropertyDrawer for {fieldType}, isGenericType={isGenericType}");
    #endif

                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (KeyValuePair<Type, IReadOnlyList<(bool isSaints, Type drawerType)>> propertyAttributeToPropertyDrawer in PropertyAttributeToPropertyDrawers)
                {
                    bool matched;
                    if (isGenericType)
                    {
                        Type genericType = fieldType.GetGenericTypeDefinition();
                        // maybe we only need the first one condition?
                        matched = propertyAttributeToPropertyDrawer.Key.IsAssignableFrom(fieldType) || genericType == propertyAttributeToPropertyDrawer.Key || genericType.IsSubclassOf(propertyAttributeToPropertyDrawer.Key);
                        // Debug.Log(fieldType.GetGenericTypeDefinition().IsSubclassOf(propertyAttributeToPropertyDrawer.Key));
                        // Debug.Log(fieldType.IsAssignableFrom(propertyAttributeToPropertyDrawer.Key));
                        // Debug.Log(propertyAttributeToPropertyDrawer.Key.IsAssignableFrom(fieldType));
                        // ReSharper disable once MergeIntoPattern
                        if (!matched && fieldType.BaseType != null && fieldType.BaseType.IsGenericType)
                        {
                            matched = propertyAttributeToPropertyDrawer.Key.IsAssignableFrom(fieldType.BaseType
                                .GetGenericTypeDefinition());
                        }
                    }
                    else
                    {
                        matched = propertyAttributeToPropertyDrawer.Key.IsAssignableFrom(fieldType);
                        if (!matched && propertyAttributeToPropertyDrawer.Key.IsGenericType)
                        {
                            matched = ReflectUtils.IsSubclassOfRawGeneric(propertyAttributeToPropertyDrawer.Key, fieldType);
                        }
                    }

// #if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
//                     Debug.Log($"fieldInfo.FieldType={fieldInfo.FieldType}, isGenericType={isGenericType}, GetGenericTypeDefinition={(isGenericType ? fieldInfo.FieldType.GetGenericTypeDefinition().ToString() : "")}, key={propertyAttributeToPropertyDrawer.Key}, {matched}");
// #endif
                    // ReSharper disable once InvertIf
                    if (matched)
                    {
                        Type foundDrawer = propertyAttributeToPropertyDrawer.Value.FirstOrDefault(each => !each.isSaints).drawerType;
    #if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
                        Debug.Log($"foundDrawer={foundDrawer} for {fieldType}");
    #endif
                        if(foundDrawer != null)
                        {
                            return foundDrawer;
                        }
                    }
                }

            }
            return null;
        }

        private static PropertyDrawer MakePropertyDrawer(Type foundDrawer, FieldInfo fieldInfo)
        {
            PropertyDrawer propertyDrawer;
            try
            {
                propertyDrawer = (PropertyDrawer)Activator.CreateInstance(foundDrawer);
            }
            catch (Exception)
            {
                return null;
            }

            FieldInfo field = foundDrawer.GetField("m_FieldInfo", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return null;
            }

            field.SetValue(propertyDrawer, fieldInfo);
            return propertyDrawer;
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private Type GetFirstSaintsDrawerType(Type attributeType)
        {
            // Debug.Log(attributeType);
            // Debug.Log(string.Join(",", _propertyAttributeToDrawers.Keys));

            if (!PropertyAttributeToPropertyDrawers.TryGetValue(attributeType,
                    out IReadOnlyList<(bool isSaints, Type drawerType)> eachDrawer))
            {
                return null;
            }
            // Debug.Log($"{attributeType}/{eachDrawer.Count}");

            (bool isSaints, Type drawerType) = eachDrawer.FirstOrDefault(each => each.isSaints);

            return isSaints ? drawerType : null;
        }

        private SaintsPropertyDrawer GetOrCreateSaintsDrawer(SaintsWithIndex saintsAttributeWithIndex)
        {
            if (_cachedDrawer.TryGetValue(saintsAttributeWithIndex, out SaintsPropertyDrawer drawer))
            {
                return drawer;
            }

            // Debug.Log($"create new drawer for {saintsAttributeWithIndex.SaintsAttribute}[{saintsAttributeWithIndex.Index}]");
            // Type drawerType = PropertyAttributeToDrawers[saintsAttributeWithIndex.SaintsAttribute.GetType()].First(each => each.isSaints).drawerType;
            return _cachedDrawer[saintsAttributeWithIndex] = GetOrCreateSaintsDrawerByAttr(saintsAttributeWithIndex.SaintsAttribute);
        }

        private static SaintsPropertyDrawer GetOrCreateSaintsDrawerByAttr(ISaintsAttribute saintsAttribute)
        {
            Type drawerType = PropertyAttributeToPropertyDrawers[saintsAttribute.GetType()].First(each => each.isSaints).drawerType;
            return (SaintsPropertyDrawer)Activator.CreateInstance(drawerType);
        }

        protected void DefaultDrawer(Rect position, SerializedProperty property, GUIContent label, FieldInfo info)
        {
            // // this works nice
            // MethodInfo defaultDraw = typeof(EditorGUI).GetMethod("DefaultPropertyField", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // defaultDraw!.Invoke(null, new object[] { position, property, label });

            // // not work when only my custom dec
            // // Getting the field type this way assumes that the property instance is not a managed reference (with a SerializeReference attribute); if it was, it should be retrieved in a different way:
            // Type fieldType = fieldInfo.FieldType;
            //
            // Type propertyDrawerType = (Type)Type.GetType("UnityEditor.ScriptAttributeUtility,UnityEditor")
            //     .GetMethod("GetDrawerTypeForType", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            //     .Invoke(null, new object[] { fieldType });
            //
            // PropertyDrawer propertyDrawer = null;
            // if (typeof(PropertyDrawer).IsAssignableFrom(propertyDrawerType))
            //     propertyDrawer = (PropertyDrawer)Activator.CreateInstance(propertyDrawerType);
            //
            // if (propertyDrawer != null)
            // {
            //     typeof(PropertyDrawer)
            //         .GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            //         .SetValue(propertyDrawer, fieldInfo);
            // }

            // // ... just a much simple way?
            // EditorGUI.PropertyField(position, property, label, true);

            // OK this should deal everything

            // this is no longer needed because PropertyField will handle this

//             IEnumerable<PropertyAttribute> allOtherAttributes = SerializedUtils
//                 .GetAttributesAndDirectParent<PropertyAttribute>(property)
//                 .attributes
//                 .Where(each => !(each is ISaintsAttribute));
//             foreach (PropertyAttribute propertyAttribute in allOtherAttributes)
//             {
//                 // ReSharper disable once InvertIf
//                 if(PropertyAttributeToDrawers.TryGetValue(propertyAttribute.GetType(), out IReadOnlyList<(bool isSaints, Type drawerType)> eachDrawer))
//                 {
//                     (bool _, Type drawerType) = eachDrawer.FirstOrDefault(each => !each.isSaints);
//                     // SaintsPropertyDrawer drawerInstance = GetOrCreateDrawerInfo(drawerType);
//                     // ReSharper disable once InvertIf
//                     if(drawerType != null)
//                     {
//                         if (!_cachedOtherDrawer.TryGetValue(drawerType, out PropertyDrawer drawerInstance))
//                         {
//                             _cachedOtherDrawer[drawerType] =
//                                 drawerInstance = (PropertyDrawer)Activator.CreateInstance(drawerType);
//                         }
//
//                         FieldInfo drawerFieldInfo = drawerType.GetField("m_Attribute", BindingFlags.NonPublic | BindingFlags.Instance);
//                         Debug.Assert(drawerFieldInfo != null);
//                         drawerFieldInfo.SetValue(drawerInstance, propertyAttribute);
//                         // drawerInstance.attribute = propertyAttribute;
//
//                         // UnityEditor.RangeDrawer
//                         // Debug.Log($"fallback drawerInstance={drawerInstance} for {propertyAttribute}");
// #if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
//                         Debug.Log($"drawerInstance {drawerInstance}={label?.text.Length}");
// #endif
//                         drawerInstance.OnGUI(position, property, label ?? GUIContent.none);
//                         // Debug.Log($"finished drawerInstance={drawerInstance}");
//                         return;
//                     }
//                 }
//             }

            // fallback to pure unity one (unity default attribute not included)
            // MethodInfo defaultDraw = typeof(EditorGUI).GetMethod("DefaultPropertyField", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // defaultDraw!.Invoke(null, new object[] { position, property, GUIContent.none });
#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
            Debug.Log($"use unity draw: {property.propertyType}");
#endif
            UnityDraw(position, property, label, info);

            // EditorGUI.PropertyField(position, property, GUIContent.none, true);
            // if (property.propertyType == SerializedPropertyType.Generic)
            // {
            //     EditorGUI.PropertyField(position, property, GUIContent.none, true);
            // }
            // else
            // {
            //     UnityDraw(position, property, GUIContent.none);
            // }
        }

        private static void UnityDraw(Rect position, SerializedProperty property, GUIContent label, FieldInfo fieldInfo)
        {
            // Wait... it works now?
            if (!hasOtherAttributeDrawer(fieldInfo))
            {
                Type drawerType = FindOtherPropertyDrawer(fieldInfo);
                if (drawerType != null)
                {
                    PropertyDrawer drawerInstance = MakePropertyDrawer(drawerType, fieldInfo);
                    if(drawerInstance != null)
                    {
                        // drawerInstance.GetPropertyHeight(property, label);
                        drawerInstance.OnGUI(position, property, label);
                        return;
                    }
                }
            }

            using (new InsideSaintsFieldScoop(SubDrawCounter, InsideSaintsFieldScoop.MakeKey(property)))
            {
                // this is no longer needed for no good reason. Need more investigation and testing
                // this code is used to prevent the decorator to be drawn everytime a fallback happens
                // the marco is not added by default
#if UNITY_2022_1_OR_NEWER && SAINTSFIELD_IMGUI_DUPLICATE_DECORATOR_FIX
                Type dec = fieldInfo.GetCustomAttributes<PropertyAttribute>(true)
                    .Select(propertyAttribute =>
                    {
                        // Debug.Log(propertyAttribute.GetType());
                        Type results = _propertyAttributeToDecoratorDrawers.TryGetValue(propertyAttribute.GetType(),
                            out IReadOnlyList<Type> eachDrawers)
                            ? eachDrawers[0]
                            : null;

                        // Debug.Log($"Found {results}");

                        return results;
                    })
                    .FirstOrDefault(each => each?.IsSubclassOf(typeof(DecoratorDrawer)) ?? false);

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_DRAW_PROCESS_CORE
                Debug.Log($"get dec {dec} for {property.propertyPath}");
#endif
                if (dec != null && ImGuiRemoveDecDraw(position, property, label))
                {
                    return;
                }
#endif

                try
                {
                    EditorGUI.PropertyField(position, property, label, true);
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError(e);
                }
                // Debug.Log($"UnityDraw done, isSub={isSubDrawer}");
            }
            // Debug.Log($"UnityDraw exit, isSub={isSubDrawer}");
        }

        public void Dispose()
        {
            OnDisposeIMGUI();
#if UNITY_2021_3_OR_NEWER
            OnDisposeUIToolkit();
#endif
        }
    }
}
