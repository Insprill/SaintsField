using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using SaintsField.Interfaces;
using UnityEditor;
using UnityEngine;

namespace SaintsField.Editor.Drawers.EnumFlagsDrawers
{
    public partial class EnumFlagsAttributeDrawer
    {
        private bool _forceUnfold;

        private GUIContent _checkBoxCheckedContent;
        private GUIContent _checkBoxEmptyContent;
        private GUIContent _checkBoxIndeterminateContent;

        private GUIStyle _iconButtonStyle;
        private GUIStyle _miniButtonStyle;
        private GUIStyle _normalButtonStyle;

        private Texture2D _classicDropdownLeftGrayTexture2D;
        private Texture2D _classicDropdownGrayTexture2D;

        private struct BtnInfo
        {
            public GUIContent Label;
            public GUIStyle LabelStyle;
            public float LabelWidth;
            public Action Action;
            public bool Disabled;
            public bool Toggled;
        }

        private bool _initExpandState;

        private struct ImGuiInfo
        {
            public RichTextDrawer RichTextDrawer;
        }

        private static readonly Dictionary<string, ImGuiInfo> InspectingIds = new Dictionary<string, ImGuiInfo>();

        private static ImGuiInfo EnsureKey(SerializedProperty property, EnumFlagsAttribute enumFlagsAttribute)
        {
            string key = SerializedUtils.GetUniqueId(property);
            if (InspectingIds.TryGetValue(key, out ImGuiInfo found))
            {
                return found;
            }

            NoLongerInspectingWatch(property.serializedObject.targetObject, key, () =>
            {
                // ReSharper disable once InvertIf
                if (InspectingIds.TryGetValue(key, out ImGuiInfo info))
                {
                    info.RichTextDrawer.Dispose();
                    InspectingIds.Remove(key);
                }
            });

            if(enumFlagsAttribute.DefaultExpanded)
            {
                property.isExpanded = true;
            }

            return InspectingIds[key] = new ImGuiInfo
            {
                RichTextDrawer = new RichTextDrawer(),
            };
        }

        private bool EnsureImageResourcesLoaded()
        {
            if (_checkboxCheckedTexture2D != null)
            {
                return true;
            }

            LoadIcons();
            _classicDropdownLeftGrayTexture2D = Util.LoadResource<Texture2D>("classic-dropdown-left-gray.png");
            _classicDropdownGrayTexture2D = Util.LoadResource<Texture2D>("classic-dropdown-gray.png");

            return false;
        }

        private void ImGuiLoadResources()
        {
            if (EnsureImageResourcesLoaded())
            {
                return;
            }

            // ImGuiEnsureDispose(property.serializedObject.targetObject);

            _checkBoxCheckedContent = new GUIContent(_checkboxCheckedTexture2D);
            _checkBoxEmptyContent = new GUIContent(_checkboxEmptyTexture2D);
            _checkBoxIndeterminateContent = new GUIContent(_checkboxIndeterminateTexture2D);

            const int padding = 2;

            _iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(padding, padding, padding, padding),
                richText = true,
            };
            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                richText = true,
            };
            _normalButtonStyle = new GUIStyle(_miniButtonStyle)
            {
                alignment = TextAnchor.MiddleLeft,
            };
        }

        protected override float GetFieldHeight(SerializedProperty property, GUIContent label,
            float width,
            ISaintsAttribute saintsAttribute,
            FieldInfo info,
            bool hasLabelWidth, object parent)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        protected override void DrawField(Rect position, SerializedProperty property, GUIContent label,
            ISaintsAttribute saintsAttribute, ISaintsAttribute[] allAttributes, OnGUIPayload onGUIPayload,
            FieldInfo info, object parent)
        {
            ImGuiLoadResources();

            EnumFlagsAttribute enumFlagsAttribute = (EnumFlagsAttribute)saintsAttribute;
            ImGuiInfo cachedInfo = EnsureKey(property, enumFlagsAttribute);

            bool isExpanded = property.isExpanded;

            EnumFlagsMetaInfo metaInfo = EnumFlagsUtil.GetMetaInfo(property, info);

            Rect drawRect = position;
            if (!string.IsNullOrEmpty(label.text))
            {
                Rect afterLabelRect = EditorGUI.PrefixLabel(drawRect, label);
                Rect labelRect = new Rect(drawRect)
                {
                    width = afterLabelRect.x - drawRect.x,
                };

                // EditorGUI.DrawRect(labelRect, Color.red);
                if(Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    property.isExpanded = !property.isExpanded;
                }

                drawRect = afterLabelRect;

            }

            (Rect preFouldoutRect, Rect foldoutRect) = RectUtils.SplitWidthRect(drawRect, drawRect.width - EditorGUIUtility.singleLineHeight);
            drawRect = preFouldoutRect;

            int curValue = property.intValue;

            if (isExpanded)  // just draw the toggle all/none buttons
            {
                GUI.DrawTexture(foldoutRect, _classicDropdownGrayTexture2D);

                (Rect toggleAllButtonRect, Rect afterToggleAllButtonRect) = RectUtils.SplitWidthRect(drawRect, EditorGUIUtility.singleLineHeight);
                using(new EditorGUI.DisabledScope(EnumFlagsUtil.IsOn(curValue, metaInfo.AllCheckedInt)))
                {
                    if (GUI.Button(toggleAllButtonRect, _checkboxCheckedTexture2D, _iconButtonStyle))
                    {
                        property.intValue = metaInfo.AllCheckedInt;
                        onGUIPayload.SetValue(metaInfo.AllCheckedInt);
                    }
                }
                (Rect emptyButtonRect, Rect afterEmptyButtonRect) = RectUtils.SplitWidthRect(afterToggleAllButtonRect, EditorGUIUtility.singleLineHeight);
                using(new EditorGUI.DisabledScope(curValue == 0))
                {
                    if (GUI.Button(emptyButtonRect, _checkboxEmptyTexture2D, _iconButtonStyle))
                    {
                        property.intValue = 0;
                        onGUIPayload.SetValue(0);
                    }
                }

                if (GUI.Button(new Rect(afterEmptyButtonRect)
                    {
                        width = afterEmptyButtonRect.width + EditorGUIUtility.singleLineHeight,
                    }, GUIContent.none, GUIStyle.none))
                {
                    property.isExpanded = !property.isExpanded;
                }

                return;
            }

            if (GUI.Button(foldoutRect, _classicDropdownLeftGrayTexture2D, GUIStyle.none))
            {
                property.isExpanded = !property.isExpanded;
            }

            (Rect toggleButtonRect, Rect afterToggleButtonRect) = RectUtils.SplitWidthRect(drawRect, EditorGUIUtility.singleLineHeight);

            Texture2D toggleTexture;

            if (curValue == 0)
            {
                toggleTexture = _checkboxEmptyTexture2D;
            }
            else if (EnumFlagsUtil.IsOn(curValue, metaInfo.AllCheckedInt))
            {
                toggleTexture = _checkboxCheckedTexture2D;
            }
            else
            {
                toggleTexture = _checkboxIndeterminateTexture2D;
            }

            if (GUI.Button(toggleButtonRect, toggleTexture, _iconButtonStyle))
            {
                if (curValue == 0)
                {
                    property.intValue = metaInfo.AllCheckedInt;
                    onGUIPayload.SetValue(metaInfo.AllCheckedInt);
                }
                else
                {
                    property.intValue = 0;
                    onGUIPayload.SetValue(0);
                }
            }

            // EditorGUI.DrawRect(afterToggleButtonRect, Color.cyan);

            Rect accToggleBitFieldButtonRect = afterToggleButtonRect;
            // ReSharper disable once UseDeconstruction
            foreach (KeyValuePair<int, EnumFlagsUtil.EnumDisplayInfo> kv in metaInfo.BitValueToName.Where(each => each.Key != 0 && each.Key != metaInfo.AllCheckedInt))
            {
                int bit = kv.Key;

                bool isOn = EnumFlagsUtil.IsOn(curValue, bit);

                EnumFlagsUtil.EnumDisplayInfo displayInfo = kv.Value;
                if (displayInfo.HasRichName)
                {
                    RichTextDrawer.RichTextChunk[] richChunks = RichTextDrawer.ParseRichXml(displayInfo.RichName, property.displayName, property, info, parent).ToArray();
                    float useWidth = cachedInfo.RichTextDrawer.GetWidth(label, position.height, richChunks);
                    Rect drawRichRect;
                    bool breakOut = false;
                    if (useWidth >= accToggleBitFieldButtonRect.width)
                    {
                        drawRichRect = accToggleBitFieldButtonRect;
                        breakOut = true;
                    }
                    else
                    {
                        (Rect thisUseRichRect, Rect leftAccRect) = RectUtils.SplitWidthRect(accToggleBitFieldButtonRect, useWidth);
                        drawRichRect = thisUseRichRect;
                        accToggleBitFieldButtonRect = leftAccRect;
                    }

                    using (EditorGUIBackgroundColor.ToggleButton(isOn))
                    {
                        if (GUI.Button(drawRichRect, GUIContent.none, _miniButtonStyle))
                        {
                            int newValue = EnumFlagsUtil.ToggleBit(curValue, bit);
                            if (newValue != curValue)
                            {
                                property.intValue = newValue;
                                onGUIPayload.SetValue(newValue);
                            }
                        }
                    }
                    cachedInfo.RichTextDrawer.DrawChunks(drawRichRect, label, richChunks);

                    if (breakOut)
                    {
                        break;
                    }
                }
                else
                {
                    string bitButtonText = displayInfo.Name;
                    float useWidth = _miniButtonStyle.CalcSize(new GUIContent(bitButtonText)).x;
                    Rect drawBitRect;
                    bool breakOut = false;
                    if (useWidth >= accToggleBitFieldButtonRect.width)
                    {
                        drawBitRect = accToggleBitFieldButtonRect;
                        breakOut = true;
                    }
                    else
                    {
                        (Rect thisUseBitRect, Rect leftAccRect) = RectUtils.SplitWidthRect(accToggleBitFieldButtonRect, useWidth);
                        drawBitRect = thisUseBitRect;
                        accToggleBitFieldButtonRect = leftAccRect;
                    }

                    // Debug.Log($"{bit}/{curValue}: {isOn}");

                    using (EditorGUIBackgroundColor.ToggleButton(isOn))
                    {
                        if (GUI.Button(drawBitRect, bitButtonText, _miniButtonStyle))
                        {
                            int newValue = EnumFlagsUtil.ToggleBit(curValue, bit);
                            if (newValue != curValue)
                            {
                                // Debug.Log($"{curValue} -> {newValue}");
                                property.intValue = newValue;
                                onGUIPayload.SetValue(newValue);
                            }
                        }
                    }

                    // Debug.Log($"{bitButtonText}: {breakOut}");
                    if (breakOut)
                    {
                        break;
                    }
                }
            }
        }

        protected override bool WillDrawBelow(SerializedProperty property, ISaintsAttribute saintsAttribute, int index, FieldInfo info,
            object parent)
        {
            return property.isExpanded;
        }

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label, float width, ISaintsAttribute saintsAttribute,
            int index, FieldInfo info, object parent)
        {
            // Debug.Log($"Calc width {width}");
            if (!property.isExpanded)
            {
                return 0;
            }

            if(width - 1 <= Mathf.Epsilon)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            // return EditorGUIUtility.singleLineHeight * 4;


            ImGuiLoadResources();
            ImGuiInfo cachedInfo = EnsureKey(property, (EnumFlagsAttribute)saintsAttribute);
            EnumFlagsMetaInfo metaInfo = EnumFlagsUtil.GetMetaInfo(property, info);
            // Debug.Log("CALC START ----------");
            return GetFlexButtons(width, metaInfo, label, cachedInfo, property.displayName, property, info, parent)
                .Select(each => each.YOffset)
                .DefaultIfEmpty(0)
                .Max() + EditorGUIUtility.singleLineHeight;
        }

        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute,
            int index, ISaintsAttribute[] allAttributes, OnGUIPayload onGuiPayload, FieldInfo info, object parent)
        {
            // return position;
            // Debug.Log(position.width);
            if (position.width - 1 <= Mathf.Epsilon)
            {
                return RectUtils.SplitHeightRect(position, EditorGUIUtility.singleLineHeight).leftRect;
            }

            // Debug.Log($"Draw width={position.width}");

            ImGuiLoadResources();

            EnumFlagsMetaInfo metaInfo = EnumFlagsUtil.GetMetaInfo(property, info);
            ImGuiInfo cachedInfo = EnsureKey(property, (EnumFlagsAttribute)saintsAttribute);

            int curValue = property.intValue;

            float yAcc = 0;

            // Debug.Log("DRAW START ----------");

            foreach (FlexButton flexButton in GetFlexButtons(position.width, metaInfo, label, cachedInfo, property.displayName, property, info, parent))
            {
                yAcc = flexButton.YOffset;

                int curBit = flexButton.Bit;
                bool isOn = EnumFlagsUtil.IsOn(curValue, curBit);
                Rect buttonRect = new Rect
                {
                    x = position.x + flexButton.X,
                    y = position.y + flexButton.YOffset,
                    width = flexButton.Width,
                    height = EditorGUIUtility.singleLineHeight,
                };

                // Debug.Log($"draw {flexButton.NonRichText} @ {flexButton.X}, {flexButton.YOffset} at {buttonRect}/{position}");
                // EditorGUI.DrawRect(buttonRect, Color.blue);

                using (EditorGUIBackgroundColor.ToggleButton(isOn))
                {
                    if (GUI.Button(buttonRect, flexButton.IsRichText? GUIContent.none: new GUIContent(flexButton.NonRichText), _miniButtonStyle))
                    {
                        int newValue = EnumFlagsUtil.ToggleBit(curValue, curBit);
                        if (newValue != curValue)
                        {
                            property.intValue = newValue;
                            onGuiPayload.SetValue(newValue);
                        }
                    }

                    if (flexButton.IsRichText)
                    {
                        cachedInfo.RichTextDrawer.DrawChunks(buttonRect, label, flexButton.RichTextChunks);
                    }
                }
            }

            // Debug.Log("DRAW END ----------");

            return RectUtils.SplitHeightRect(position, yAcc + EditorGUIUtility.singleLineHeight).leftRect;
        }

        private struct FlexButton
        {
            public int Bit;
            public float X;
            public float YOffset;
            public float Width;
            public bool IsRichText;
            public IReadOnlyList<RichTextDrawer.RichTextChunk> RichTextChunks;
            public string NonRichText;
            // public Action OnClick;
        }

        private IEnumerable<FlexButton> GetFlexButtons(float width, EnumFlagsMetaInfo metaInfo, GUIContent guiContent, ImGuiInfo cachedInfo, string displayName, SerializedProperty property, FieldInfo info, object parent)
        {
            float xOffset = 0;
            float yOffset = 0;
            // ReSharper disable once UseDeconstruction
            foreach (KeyValuePair<int, EnumFlagsUtil.EnumDisplayInfo> kv in metaInfo.BitValueToName.Where(each => each.Key != 0 && each.Key != metaInfo.AllCheckedInt))
            {
                int bit = kv.Key;
                EnumFlagsUtil.EnumDisplayInfo displayInfo = kv.Value;

                float useWidth;
                RichTextDrawer.RichTextChunk[] chunks = null;

                if (displayInfo.HasRichName)
                {
                    chunks = RichTextDrawer.ParseRichXml(displayInfo.RichName, displayName, property, info, parent).ToArray();
                    useWidth = cachedInfo.RichTextDrawer.GetWidth(guiContent, EditorGUIUtility.singleLineHeight, chunks);
                }
                else
                {
                    useWidth = _miniButtonStyle.CalcSize(new GUIContent(displayInfo.Name)).x;
                }

                float leftWidth = width - xOffset;
                if (leftWidth < useWidth)  // new line
                {
                    // Debug.Log($"N {displayInfo.Name} width={width}, xOffset={xOffset}, useWidth={useWidth}, leftWidth={leftWidth}");
                    xOffset = useWidth;
                    yOffset += EditorGUIUtility.singleLineHeight;
                    yield return new FlexButton
                    {
                        Bit = bit,
                        X = 0,
                        YOffset = yOffset,
                        Width = useWidth,
                        IsRichText = displayInfo.HasRichName,
                        RichTextChunks = chunks,
                        NonRichText = displayInfo.Name,
                    };
                }
                else
                {
                    // Debug.Log($"K {displayInfo.Name} width={width}, xOffset={xOffset}, useWidth={useWidth}, leftWidth={leftWidth}");
                    yield return new FlexButton
                    {
                        Bit = bit,
                        X = xOffset,
                        YOffset = yOffset,
                        Width = useWidth,
                        IsRichText = displayInfo.HasRichName,
                        RichTextChunks = chunks,
                        NonRichText = displayInfo.Name,
                    };
                    xOffset += useWidth;
                }
            }
        }

        // private void CheckAutoExpand(float positionWidth, SerializedProperty property, FieldInfo info, EnumFlagsAttribute enumFlagsAttribute)
        // {
        //     if (positionWidth - 1 <= Mathf.Epsilon)  // layout event will give this to negative... wait for repaint to do correct calculate
        //     {
        //         return;
        //     }
        //
        //     _forceUnfold = false;
        //
        //     if (property.isExpanded)
        //     {
        //         return;
        //     }
        //
        //     if (!enumFlagsAttribute.AutoExpand)
        //     {
        //         return;
        //     }
        //
        //     (int, string)[] allValues = Enum.GetValues(info.FieldType)
        //         .Cast<object>()
        //         .Select(each => ((int)each, each.ToString())).ToArray();
        //
        //     int allCheckedInt = allValues.Select(each => each.Item1).Aggregate(0, (acc, value) => acc | value);
        //     IEnumerable<string> stringValues = allValues
        //         .Where(each => each.Item1 != 0 && each.Item1 != allCheckedInt)
        //         .Select(each => each.Item2);
        //
        //     float totalBtnWidth = EditorGUIUtility.singleLineHeight + stringValues.Sum(each => _miniButtonStyle.CalcSize(new GUIContent(each)).x);
        //
        //     _forceUnfold = totalBtnWidth > positionWidth;
        //     // Debug.Log($"totalBtnWidth = {totalBtnWidth}, positionWidth = {positionWidth}, _forceUnfold = {_forceUnfold}, event={Event.current.type}");
        // }
        //
        // private static void BtnRender(Rect position, IReadOnlyList<BtnInfo> btnInfos)
        // {
        //     float eachX = position.x;
        //     foreach (BtnInfo btnInfo in btnInfos)
        //     {
        //         Rect btnRect = new Rect(position)
        //         {
        //             x = eachX,
        //             width = btnInfo.LabelWidth,
        //         };
        //         using(new EditorGUI.DisabledScope(btnInfo.Disabled))
        //         using (EditorGUIBackgroundColor.ToggleButton(btnInfo.Toggled))
        //         {
        //             if (GUI.Button(btnRect, btnInfo.Label, btnInfo.LabelStyle))
        //             {
        //                 btnInfo.Action.Invoke();
        //             }
        //         }
        //
        //         eachX += btnInfo.LabelWidth;
        //     }
        // }
    }
}
