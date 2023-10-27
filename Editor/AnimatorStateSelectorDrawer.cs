﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExtInspector.Editor.Standalone;
using ExtInspector.Editor.Utils;
using ExtInspector.Standalone;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ExtInspector.Editor
{
    // [CustomPropertyDrawer(typeof(AnimState))]
    [CustomPropertyDrawer(typeof(AnimStateAttribute))]
    public class AnimatorStateSelectorDrawer : SaintsPropertyDrawer
    {
        private bool _onEnableChecked;
        private string _errorMsg = "";
        private bool _targetIsString = true;

        protected override float GetLabelFieldHeight(SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute)
        {
            return EditorStyles.popup.CalcHeight(new GUIContent("M"), EditorGUIUtility.currentViewWidth);
        }

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute)
        {
            float errorHeight = _errorMsg == "" ? 0 : HelpBox.GetHeight(_errorMsg);
            float subRowHeight = EditorGUIUtility.singleLineHeight * (_targetIsString ? 1 : 2);
            return errorHeight + subRowHeight;
        }

        protected override void DrawField(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute)
        {
            _targetIsString = property.propertyType == SerializedPropertyType.String;

            // AnimStateSelector animStateSelector = SerializedUtil.GetAttribute<AnimStateSelector>(property);
            // string animFieldName = animStateSelector?.AnimFieldName ?? "animator";
            AnimStateAttribute animStateAttribute = (AnimStateAttribute) saintsAttribute;
            string animFieldName = animStateAttribute.AnimFieldName ?? "animator";

            SerializedObject targetSer = property.serializedObject;
            SerializedProperty animProp = targetSer.FindProperty(animFieldName) ?? SerializedUtil.FindPropertyByAutoPropertyName(targetSer, animFieldName);

            // Debug.Log(property.type);
            if(animProp == null)
            {
                _errorMsg = $"Can't find Animator {animFieldName}";
                RenderErrorFallback(position, property);
                return;
            }

            Animator animator = (Animator)animProp.objectReferenceValue;
            if (!animator)
            {
                _errorMsg = $"Animator {animFieldName} is null";
                RenderErrorFallback(position, property);
                return;
            }

            AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;

            List<AnimState> animatorStates = new List<AnimState>();
            foreach ((AnimatorControllerLayer animatorControllerLayer, int layerIndex) in controller.layers.Select((each, index) => (each, index)))
            {
                animatorStates.AddRange(
                    animatorControllerLayer.stateMachine.states.Select(
                        childAnimatorState =>
                        {
                            AnimationClip clip = (AnimationClip)childAnimatorState.state.motion;
                            // float clipLength = clip ? clip.length : 0;
                            float speed = childAnimatorState.state.speed;
                            return new AnimState
                            {
                                layerIndex = layerIndex,
                                stateName = childAnimatorState.state.name,
                                stateNameHash = childAnimatorState.state.nameHash,
                                stateSpeed = speed,
                                animationClip = clip,
                            };
                        })
                );
            }

            if (animatorStates.Count == 0)
            {
                _errorMsg = $"Animator {animFieldName} has no states";
                RenderErrorFallback(position, property);
                return;
            }

            _errorMsg = "";

            SerializedProperty curLayerIndexProp = property.FindPropertyRelative("layerIndex");
            SerializedProperty curStateNameHashProp = property.FindPropertyRelative("stateNameHash");
            SerializedProperty curStateNameProp = property.FindPropertyRelative("stateName");
            SerializedProperty curStateSpeedProp = property.FindPropertyRelative("stateSpeed");
            SerializedProperty curAnimationClipProp = property.FindPropertyRelative("animationClip");

            string[] optionStrings = animatorStates.Select(each => each.stateName).ToArray();

            int curIndex;
            if (_targetIsString)
            {
                curIndex = Array.IndexOf(optionStrings, property.stringValue);
            }
            else
            {
                int curLayerIndex = curLayerIndexProp.intValue;
                int curStateNameHash = curStateNameHashProp.intValue;
                curIndex = animatorStates.FindIndex((each) =>
                    each.layerIndex == curLayerIndex && each.stateNameHash == curStateNameHash);
            }
            if (curIndex == -1)
            {
                curIndex = 0;
            }

            if (!_onEnableChecked)  // check whether external source changed, to avoid caching an old value
            {
                _onEnableChecked = true;
                AnimState animState = animatorStates[curIndex];
                if (_targetIsString)
                {
                    property.stringValue = animState.stateName;
                }
                else
                {
                    curLayerIndexProp.intValue = animState.layerIndex;
                    curStateNameHashProp.intValue = animState.stateNameHash;
                    curStateNameProp.stringValue = animState.stateName;
                    curStateSpeedProp.floatValue = animState.stateSpeed;
                    curAnimationClipProp.objectReferenceValue = animState.animationClip;
                }
            }

            // (Rect popupRect, Rect popupLeftRect) = RectUtils.SplitHeightRect(position, GetLabelFieldHeight(property, label, saintsAttribute));
            using EditorGUI.ChangeCheckScope popupChanged = new EditorGUI.ChangeCheckScope();
            curIndex = EditorGUI.Popup(
                position,
                label,
                curIndex,
                animatorStates.Select(each => new GUIContent(each.ToString())).ToArray(),
                EditorStyles.popup);

            // ReSharper disable once InvertIf
            if (popupChanged.changed)
            {
                AnimState animState = animatorStates[curIndex];
                if (_targetIsString)
                {
                    property.stringValue = animState.stateName;
                }
                else
                {
                    curLayerIndexProp.intValue = animState.layerIndex;
                    curStateNameHashProp.intValue = animState.stateNameHash;
                    curStateNameProp.stringValue = animState.stateName;
                    curStateSpeedProp.floatValue = animState.stateSpeed;
                    curAnimationClipProp.objectReferenceValue = animState.animationClip;
                }
            }

            // RenderSubRow(popupLeftRect, property);
        }

        private void RenderErrorFallback(Rect position, SerializedProperty property)
        {
            if (_targetIsString)
            {
                using EditorGUI.ChangeCheckScope onChange = new EditorGUI.ChangeCheckScope();
                string newContent = EditorGUI.TextField(position, GUIContent.none, property.stringValue);
                if (onChange.changed)
                {
                    property.stringValue = newContent;
                }

                return;
            }

            SerializedProperty curStateNameProp = property.FindPropertyRelative("stateName");
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUI.TextField(position, GUIContent.none, curStateNameProp.stringValue);
            }
            // _errorMsg = content;
            // Rect helpBoxLeftRect = HelpBox.Draw(position, _errorMsg, MessageType.Error);
            // string propertyString = PropertyToString(property);
            // (Rect popupRect, Rect popupLeftRect) = RectUtils.SplitHeightRect(helpBoxLeftRect, PopupHeight());
            // using (new EditorGUI.DisabledScope(true))
            // {
            //     EditorGUI.Popup(popupRect, label, 0, new[] { new GUIContent(propertyString) });
            // }
            //
            // RenderSubRow(popupLeftRect, property);
        }

        protected override bool WillDrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute)
        {
            return true;
        }

        protected override Rect DrawBelow(Rect position, SerializedProperty property,
            GUIContent label, ISaintsAttribute saintsAttribute)
        {
            if(_targetIsString)
            {
                (Rect valueRect, Rect leftRect) =
                    RectUtils.SplitHeightRect(position, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledGroupScope(true)) {
                    EditorGUI.TextField(valueRect, "┗Value", property.stringValue);
                }
                return leftRect;
            }

            SerializedProperty curStateSpeedProp = property.FindPropertyRelative("stateSpeed");
            SerializedProperty curAnimationClipProp = property.FindPropertyRelative("animationClip");
            // using (new EditorGUI.IndentLevelScope(1))
            using (new EditorGUI.DisabledScope(true))
            {
                (Rect speedRect, Rect speedLeftRect) =
                    RectUtils.SplitHeightRect(position, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(speedRect, curStateSpeedProp, new GUIContent( $"┣{curStateSpeedProp.displayName}"));

                (Rect clipRect, Rect leftRect) =
                    RectUtils.SplitHeightRect(speedLeftRect, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(clipRect, curAnimationClipProp, new GUIContent( $"┗{curAnimationClipProp.displayName}"));

                return leftRect;
            }
        }

        // private static void RenderSubRow(Rect position, SerializedProperty property)
        // {
        //     SerializedProperty curStateSpeedProp = property.FindPropertyRelative("stateSpeed");
        //     SerializedProperty curAnimationClipProp = property.FindPropertyRelative("animationClip");
        //     // using (new EditorGUI.IndentLevelScope(1))
        //     using (new EditorGUI.DisabledScope(true))
        //     {
        //         (Rect speedRect, Rect speedLeftRect) =
        //             RectUtils.SplitHeightRect(position, EditorGUIUtility.singleLineHeight);
        //         EditorGUI.PropertyField(speedRect, curStateSpeedProp, new GUIContent( $"┣{curStateSpeedProp.displayName}"));
        //
        //         (Rect clipRect, Rect leftRect) =
        //             RectUtils.SplitHeightRect(position, EditorGUIUtility.singleLineHeight);
        //         EditorGUI.PropertyField(clipRect, curAnimationClipProp, new GUIContent( $"┗{curAnimationClipProp.displayName}"));
        //     }
        // }

        // private static string PropertyToString(SerializedProperty property) =>
        //     new AnimState
        //     {
        //         layerIndex = property.FindPropertyRelative("layerIndex").intValue,
        //         stateName = property.FindPropertyRelative("stateName").stringValue,
        //         stateNameHash = property.FindPropertyRelative("stateNameHash").intValue,
        //         animationClip = (AnimationClip) property.FindPropertyRelative("animationClip").objectReferenceValue,
        //         stateSpeed = property.FindPropertyRelative("stateSpeed").floatValue,
        //     }.ToString();
    }
}
