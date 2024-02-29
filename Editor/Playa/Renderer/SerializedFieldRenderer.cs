﻿using UnityEditor;
using UnityEngine;
#if UNITY_2022_2_OR_NEWER && !SAINTSFIELD_UI_TOOLKIT_DISABLE
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif

namespace SaintsField.Editor.Playa.Renderer
{
    public class SerializedFieldRenderer: AbsRenderer
    {
        public SerializedFieldRenderer(SerializedObject serializedObject, SaintsFieldWithInfo fieldWithInfo, bool tryFixUIToolkit=false) : base(serializedObject, fieldWithInfo, tryFixUIToolkit)
        {
        }

#if UNITY_2022_2_OR_NEWER && !SAINTSFIELD_UI_TOOLKIT_DISABLE

        private PropertyField _result;

        public override VisualElement CreateVisualElement()
        {
            PropertyField result = new PropertyField(SerializedObject.FindProperty(FieldWithInfo.FieldInfo.Name))
            {
                style =
                {
                    flexGrow = 1,
                },
            };

            // ReSharper disable once InvertIf
            if(TryFixUIToolkit && FieldWithInfo.FieldInfo.GetCustomAttributes(typeof(ISaintsAttribute), true).Length == 0)
            {
                // Debug.Log($"{fieldWithInfo.fieldInfo.Name} {arr.Length}");
                _result = result;
                _result.RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            }
            return result;
        }

        private void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            // Debug.Log("OnGeometryChangedEvent");
            Label label = _result.Q<Label>(className: "unity-label");
            if (label == null)
            {
                return;
            }

            // Utils.Util.FixLabelWidthLoopUIToolkit(label);
            _result.UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            Utils.UIToolkitUtils.FixLabelWidthLoopUIToolkit(label);
            _result = null;
        }

#endif
        public override void Render()
        {
            SerializedProperty property = SerializedObject.FindProperty(FieldWithInfo.FieldInfo.Name);
            EditorGUILayout.PropertyField(property, GUILayout.ExpandWidth(true));
        }

        public override string ToString() => $"Ser<{FieldWithInfo.FieldInfo.Name}>";
    }
}
