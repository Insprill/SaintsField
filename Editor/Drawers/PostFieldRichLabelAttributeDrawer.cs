﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaintsField.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(PostFieldRichLabelAttribute))]
    public class PostFieldRichLabelAttributeDrawer: SaintsPropertyDrawer
    {
        private readonly RichTextDrawer _richTextDrawer = new RichTextDrawer();

        ~PostFieldRichLabelAttributeDrawer()
        {
            _richTextDrawer.Dispose();
        }

        #region IMGUI

        private string _error = "";

        private IReadOnlyList<RichTextDrawer.RichTextChunk> _payloads;

        protected override float GetPostFieldWidth(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute)
        {
            PostFieldRichLabelAttribute targetAttribute = (PostFieldRichLabelAttribute)saintsAttribute;
            (string error, string xml) = RichTextDrawer.GetLabelXml(property, targetAttribute, GetParentTarget(property));

            _error = error;

            if (error != "" || string.IsNullOrEmpty(xml))
            {
                _payloads = null;
                return 0;
            }

            _payloads = RichTextDrawer.ParseRichXml(xml, label.text).ToArray();
            return _richTextDrawer.GetWidth(label, position.height, _payloads) + targetAttribute.Padding;
        }

        protected override bool DrawPostFieldImGui(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute, bool valueChanged)
        {
            if (_error != "")
            {
                return false;
            }

            if(_payloads == null || _payloads.Count == 0)
            {
                return false;
            }

            PostFieldRichLabelAttribute targetAttribute = (PostFieldRichLabelAttribute)saintsAttribute;

            Rect drawRect = new Rect(position)
            {
                x = position.x + targetAttribute.Padding,
                width = position.width - targetAttribute.Padding,
            };

            _richTextDrawer.DrawChunks(drawRect, label, _payloads);

            return true;
        }

        protected override bool WillDrawBelow(SerializedProperty property, ISaintsAttribute saintsAttribute)
        {
            return _error != "";
        }

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label, float width, ISaintsAttribute saintsAttribute)
        {
            return _error == "" ? 0 : ImGuiHelpBox.GetHeight(_error, width, MessageType.Error);
        }

        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute) =>
            _error == ""
                ? position
                : ImGuiHelpBox.Draw(position, _error, MessageType.Error);
        #endregion

        #region UIToolkit

        private static string NameRichLabel(SerializedProperty property, int index) => $"{property.propertyPath}_{index}__PostFieldRichLabel";
        private static string NameHelpBox(SerializedProperty property, int index) => $"{property.propertyPath}_{index}__PostFieldRichLabel_HelpBox";

        protected override VisualElement CreatePostFieldUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, object parent, Action<object> onChange)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = EditorGUIUtility.singleLineHeight,
                    marginLeft = LabelLeftSpace + ((PostFieldRichLabelAttribute)saintsAttribute).Padding,
                    unityTextAlign = TextAnchor.MiddleLeft,

                    flexShrink = 0,
                    flexGrow = 0,
                },
                name = NameRichLabel(property, index),
                pickingMode = PickingMode.Ignore,
                userData = "",
            };
        }

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, object parent)
        {
            return new HelpBox("", HelpBoxMessageType.Error)
            {
                name = NameHelpBox(property, index),
                userData = "",
                style =
                {
                    display = DisplayStyle.None,
                },
            };
        }

        protected override void OnUpdateUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            int index,
            VisualElement container, Action<object> onValueChangedCallback, object parent)
        {
            PostFieldRichLabelAttribute targetAttribute = (PostFieldRichLabelAttribute)saintsAttribute;
            (string error, string xml) = RichTextDrawer.GetLabelXml(property, targetAttribute, parent);

            HelpBox helpBox = container.Q<HelpBox>(NameHelpBox(property, index));
            string curError = (string)helpBox.userData;
            if (curError != error)
            {
                helpBox.text = error;
                helpBox.style.display = error == "" ? DisplayStyle.None : DisplayStyle.Flex;
            }

            VisualElement richLabel = container.Q<VisualElement>(NameRichLabel(property, index));
            string curXml = (string)richLabel.userData;
            // ReSharper disable once InvertIf
            if (curXml != xml)
            {
                richLabel.userData = xml;
                richLabel.Clear();
                // ReSharper disable once InvertIf
                if (xml != null)
                {
                    IReadOnlyList<RichTextDrawer.RichTextChunk> payloads = RichTextDrawer.ParseRichXml(xml, property.displayName).ToArray();
                    foreach (VisualElement richChunk in _richTextDrawer.DrawChunksUIToolKit(property.displayName, payloads))
                    {
                        richLabel.Add(richChunk);
                    }
                }
            }
        }

        #endregion
    }
}
