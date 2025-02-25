using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Linq;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace SaintsField.Editor.Drawers.SaintsDictionary
{
    public partial class SaintsDictionaryDrawer
    {
        private class SaintsDictionaryTable : TreeView
        {
            private readonly SerializedProperty _property;
            private readonly SerializedProperty _keysProp;
            private readonly SerializedProperty _valuesProp;
            private List<int> _itemIndexToPropertyIndex;
            private readonly FieldInfo _keysField;
            private readonly FieldInfo _info;
            private readonly object _parent;

            public readonly UnityEvent<IReadOnlyList<(int, int)>> IndexSwapEvent = new UnityEvent<IReadOnlyList<(int, int)>>();

            public SaintsDictionaryTable(
                TreeViewState state, MultiColumnHeader multiColumnHeader,
                SerializedProperty property,
                SerializedProperty keysProp, SerializedProperty valuesProp,
                FieldInfo keysField, FieldInfo info, object parent
                ) : base(state, multiColumnHeader)
            {
                // Custom setup
                rowHeight = 20;
                // columnIndexForTreeFoldouts = 2;
                showAlternatingRowBackgrounds = true;
                showBorder = true;
                // customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f;
                // extraSpaceBeforeIconAndLabel = kToggleWidth;
                // multiColumnHeader.sortingChanged += OnSortingChanged;
                // ArrayProp = arrayProp;
                // _headerToPropNames = headerToPropNames;
                // _elementType = elementType;

                _property = property;
                _keysProp = keysProp;
                _valuesProp = valuesProp;
                _keysField = keysField;

                _info = info;
                _parent = parent;
                // Reload();
            }

            public void SetItemIndexToPropertyIndex(IReadOnlyList<int> itemIndexToPropertyIndex)
            {
                _itemIndexToPropertyIndex = itemIndexToPropertyIndex.ToList();
                Reload();
            }

            // private void OnSortingChanged(MultiColumnHeader header)
            // {
            //     Debug.Log(header);
            // }

            protected override TreeViewItem BuildRoot()
            {
                TreeViewItem root = new TreeViewItem {id = -1, depth = -1, displayName = "Root"};

                List<TreeViewItem> allItems = _itemIndexToPropertyIndex.Select(index => new TreeViewItem { id = index, depth = 0, displayName = $"{index}" }).ToList();

                // Utility method that initializes the TreeViewItem.children and .parent for all items.
                SetupParentsAndChildrenFromDepths(root, allItems);

                // Return root of the tree
                return root;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                TreeViewItem item = args.item;
                for (int i = 0; i < args.GetNumVisibleColumns(); i++)
                {
                    CellGUI(args.GetCellRect(i), item, args.GetColumn(i));
                    // CellGUI(args.GetCellRect(i), item, (args.GetColumn(i) as MultiColumnHeaderState.Column).headerContent.text, ref args);
                }
            }

            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                int index = item.id;
                return new[] { _keysProp, _valuesProp }
                    .Select(each => each.GetArrayElementAtIndex(index))
                    .Where(each => each != null)
                    .Select(each => EditorGUI.GetPropertyHeight(each, GUIContent.none, true))
                    .Max();
            }

            private void CellGUI(Rect getCellRect, TreeViewItem item, int getColumn)
            {
                int index = item.id;
                bool isKeyColumn = getColumn == 0;
                SerializedProperty listProp = isKeyColumn ? _keysProp : _valuesProp;
                SerializedProperty itemProp = listProp.GetArrayElementAtIndex(index);

                bool conflicted = false;
                if(isKeyColumn)
                {
                    (string curFieldError, int _, object curFieldVaue) = Util.GetValue(_property, _info, _parent);
                    if (curFieldError == "")
                    {
                        IEnumerable allKeyList = _keysField.GetValue(curFieldVaue) as IEnumerable;
                        Debug.Assert(allKeyList != null, $"key list {_keysField.Name} is null");
                        (object value, int index)[] indexedValue = allKeyList.Cast<object>().WithIndex().ToArray();
                        object thisKey = indexedValue[index].value;
                        // Debug.Log($"checking with {thisKey}");
                        foreach ((object existKey, int _) in indexedValue.Where(each => each.index != index))
                        {
                            // Debug.Log($"{existKey}/{thisKey}");
                            // ReSharper disable once InvertIf
                            if (Util.GetIsEqual(existKey, thisKey))
                            {
                                conflicted = true;
                                break;
                            }
                        }
                    }
                }

                // ReSharper disable once ConvertToUsingDeclaration
                using(EditorGUI.ChangeCheckScope changed = new EditorGUI.ChangeCheckScope())
                using(new GUIColorScoop(conflicted? WarningColor: GUI.color))
                {
                    EditorGUI.PropertyField(getCellRect, itemProp, GUIContent.none);
                    if (conflicted && changed.changed)
                    {
                        Reload();
                    }
                }
            }

            private int[] _draggedPropertyIndexes = Array.Empty<int>();

            protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
            {
                if (args.performDrop && _draggedPropertyIndexes.Length > 0)
                {
                    List<(int, int)> propIndexFromTo = new List<(int, int)>();
                    foreach (int index in _draggedPropertyIndexes)
                    {
                        // the last will give index+1, wtf...
                        int useIndex = Mathf.Min(args.insertAtIndex, _itemIndexToPropertyIndex.Count - 1);
                        int toIndex = _itemIndexToPropertyIndex[useIndex];
                        // Debug.Log($"{index} <-> {toIndex}");
                        propIndexFromTo.Add((index, toIndex));
                    }

                    // foreach ((int fromIndex, int toIndex) in propIndexFromTo)
                    // {
                    //     _keysProp.MoveArrayElement(fromIndex, toIndex);
                    //     _valuesProp.MoveArrayElement(fromIndex, toIndex);
                    //     int fromIndexList = _itemIndexToPropertyIndex.IndexOf(fromIndex);
                    //     int toIndexList = _itemIndexToPropertyIndex.IndexOf(toIndex);
                    //
                    //     (_itemIndexToPropertyIndex[fromIndexList], _itemIndexToPropertyIndex[toIndexList]) =
                    //         (_itemIndexToPropertyIndex[toIndexList], _itemIndexToPropertyIndex[fromIndexList]);
                    // }

                    _draggedPropertyIndexes = Array.Empty<int>();
                    IndexSwapEvent.Invoke(propIndexFromTo);
                    // EditorApplication.delayCall += OnDestroy;
                    // _property.serializedObject.ApplyModifiedProperties();
                    // Reload();
                }

                // return DragAndDropVisualMode.None;
                return DragAndDropVisualMode.Move;
            }

            protected override bool CanStartDrag(CanStartDragArgs args)
            {
                _draggedPropertyIndexes = args.draggedItemIDs.OrderByDescending(each => each).ToArray();
                return true;
            }

            protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.StartDrag("SaintsDictionary");
            }

            public IReadOnlyList<int> GetSelectedIndex() =>
                GetSelection().Select(each => _itemIndexToPropertyIndex[each]).ToArray();
        }

        protected override bool UseCreateFieldIMGUI => true;

        private class SaintsDictionaryInfoIMGUI
        {
            public string Key;
            public string Error;

            public string KeySearch;
            public string ValueSearch;
            public int NumberOfItemsPerPage;
            public int PageIndex;
            public int TotalPage;
            public int TotalSize;

            public SaintsDictionaryTable SaintsDictionaryTable;
            public SerializedProperty KeysProp;
            public SerializedProperty ValuesProp;
        }

        private static readonly Dictionary<string, SaintsDictionaryInfoIMGUI> SaintsDictionaryInfoIMGUICache = new Dictionary<string, SaintsDictionaryInfoIMGUI>();

        private SaintsDictionaryInfoIMGUI EnsureKey(float width, SerializedProperty property, SaintsDictionaryAttribute saintsDictionaryAttribute, FieldInfo info, object parent)
        {
            string key = SerializedUtils.GetUniqueId(property);
            if (!SaintsDictionaryInfoIMGUICache.TryGetValue(key, out SaintsDictionaryInfoIMGUI value))
            {
                int arrayIndex = SerializedUtils.PropertyPathIndex(property.propertyPath);

                Type rawType = arrayIndex == -1 ? info.FieldType : info.FieldType.GetElementType();
                Debug.Assert(rawType != null, $"Failed to get element type from {property.propertyPath}");
                // Debug.Log(info.FieldType);
                (string propKeysName, string propValuesName) = GetKeysValuesPropName(rawType);
                SerializedProperty keysProp = property.FindPropertyRelative(propKeysName) ?? SerializedUtils.FindPropertyByAutoPropertyName(property, propKeysName);
                SerializedProperty valuesProp = property.FindPropertyRelative(propValuesName) ?? SerializedUtils.FindPropertyByAutoPropertyName(property, propValuesName);

                FieldInfo keysField = rawType.GetField(propKeysName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance  | BindingFlags.FlattenHierarchy);
                Debug.Assert(keysField != null, $"Failed to get keys field from {property.propertyPath}");

                float useWidth = Mathf.Max(width / 2 - 25, 25);

                SaintsDictionaryTable saintsDictionaryTable = new SaintsDictionaryTable(
                    new TreeViewState(),
                    new MultiColumnHeader(new MultiColumnHeaderState(new[]
                    {
                        new MultiColumnHeaderState.Column
                        {
                            headerContent = new GUIContent(GetKeyLabel(saintsDictionaryAttribute)),
                            width = useWidth,
                        },
                        new MultiColumnHeaderState.Column
                        {
                            headerContent = new GUIContent(GetValueLabel(saintsDictionaryAttribute)),
                            width = useWidth,
                        },
                    })),
                    property, keysProp, valuesProp, keysField, info, parent);

                // saintsDictionaryTable.SetItemIndexToPropertyIndex(Enumerable.Range(0, keysProp.arraySize).ToArray());

                SaintsDictionaryInfoIMGUICache[key] = value = new SaintsDictionaryInfoIMGUI
                {
                    Error = "",
                    Key = key,

                    SaintsDictionaryTable = saintsDictionaryTable,
                    KeysProp = keysProp,
                    ValuesProp = valuesProp,
                    TotalSize = keysProp.arraySize,

                    NumberOfItemsPerPage = saintsDictionaryAttribute?.NumberOfItemsPerPage ?? -1,
                };

                NoLongerInspectingWatch(property.serializedObject.targetObject, key, () =>
                {
                    SaintsDictionaryInfoIMGUICache.Remove(key);
                });

                BindIndexSwapEvent(value);
                RefreshListIMGUI(value);
            }
            else
            {
                try
                {
                    string _ = value.KeysProp.propertyPath;
                }
                catch (NullReferenceException)
                {
                    SaintsDictionaryInfoIMGUICache.Remove(key);
                    return EnsureKey(width, property, saintsDictionaryAttribute, info, parent);
                }
                catch (ObjectDisposedException)
                {
                    SaintsDictionaryInfoIMGUICache.Remove(key);
                    return EnsureKey(width, property, saintsDictionaryAttribute, info, parent);
                }

                // ReSharper disable once InvertIf
                if(value.TotalSize != value.KeysProp.arraySize)
                {
                    value.TotalSize = value.KeysProp.arraySize;
                    RefreshListIMGUI(value);
                }
            }

            return value;
        }

        private static void BindIndexSwapEvent(SaintsDictionaryInfoIMGUI cachedInfo)
        {
            UnityEvent<IReadOnlyList<(int, int)>> swapEvent = cachedInfo.SaintsDictionaryTable.IndexSwapEvent;
            swapEvent.AddListener(swapIndexes =>
            {
                foreach ((int fromIndex, int toIndex) in swapIndexes)
                {
                    cachedInfo.KeysProp.MoveArrayElement(fromIndex, toIndex);
                    cachedInfo.ValuesProp.MoveArrayElement(fromIndex, toIndex);
                }

                cachedInfo.KeysProp.serializedObject.ApplyModifiedProperties();
                RefreshListIMGUI(cachedInfo);
            });

        }

        protected override float GetFieldHeight(SerializedProperty property, GUIContent label, float width,
            ISaintsAttribute saintsAttribute, FieldInfo info,
            bool hasLabelWidth, object parent)
        {
            SaintsDictionaryAttribute saintsDictionaryAttribute = saintsAttribute as SaintsDictionaryAttribute;
            bool searchable = saintsDictionaryAttribute?.Searchable ?? true;

            return property.isExpanded
                ? EnsureKey(width, property, (SaintsDictionaryAttribute) saintsAttribute, info, parent).SaintsDictionaryTable.totalHeight
                    + SingleLineHeight * 2 + (searchable? SingleLineHeight: 0)
                : SingleLineHeight;
        }

        private static Texture2D _leftIcon;
        private static Texture2D _rightIcon;
        private static GUIStyle _iconButtonStyle;

        protected override void DrawField(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute,
            IReadOnlyList<PropertyAttribute> allAttributes, OnGUIPayload onGUIPayload, FieldInfo info, object parent)
        {
            SaintsDictionaryAttribute saintsDictionaryAttribute = saintsAttribute as SaintsDictionaryAttribute;
            bool searchable = saintsDictionaryAttribute?.Searchable ?? true;

            SaintsDictionaryInfoIMGUI cachedInfo = EnsureKey(position.width, property, saintsDictionaryAttribute, info,
                parent);
            try
            {
                string _ = cachedInfo.KeysProp.propertyPath;
                string __ = cachedInfo.ValuesProp.propertyPath;
            }
            catch (NullReferenceException)
            {
#if SAINTSFIELD_DEBUG
                Debug.LogError("Property disposed");
#endif
                SaintsDictionaryInfoIMGUICache.Remove(cachedInfo.Key);
                return;
            }
            catch (ObjectDisposedException)
            {
#if SAINTSFIELD_DEBUG
                Debug.LogError("Property disposed");
#endif
                SaintsDictionaryInfoIMGUICache.Remove(cachedInfo.Key);
                return;
            }

            // Debug.Log(cachedInfo.TotalPage);

            (Rect foldoutRawRect, Rect foldoutLeftRect) = RectUtils.SplitHeightRect(position, SingleLineHeight);
            Rect foldoutRect = ShrinkRect(foldoutRawRect); property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label);

            Rect totalRect = RectUtils.SplitWidthRect(foldoutRect, foldoutRect.width - 50).leftRect;
            using (EditorGUI.ChangeCheckScope totalChanged = new EditorGUI.ChangeCheckScope())
            {
                int size = EditorGUI.IntField(totalRect, GUIContent.none, cachedInfo.KeysProp.arraySize);
                if(totalChanged.changed)
                {
                    int newSize = Mathf.Max(size, 0);
                    if (newSize >= cachedInfo.KeysProp.arraySize)
                    {
                        if (IncreaseArraySize(newSize, cachedInfo.KeysProp, cachedInfo.ValuesProp))
                        {
                            property.serializedObject.ApplyModifiedProperties();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        IReadOnlyList<int> deleteIndexes = Enumerable.Range(newSize, cachedInfo.KeysProp.arraySize - newSize)
                            .Reverse()
                            .ToList();
                        DecreaseArraySize(deleteIndexes, cachedInfo.KeysProp, cachedInfo.ValuesProp);
                        property.serializedObject.ApplyModifiedProperties();
                    }

                    // RefreshList(cachedInfo);
                    return;
                }
            }

            if(!property.isExpanded)
            {
                return;
            }

            GUI.Box(foldoutLeftRect, GUIContent.none, EditorStyles.helpBox);

            Rect searchLeftRect = foldoutLeftRect;

            if(searchable)
            {
                (Rect searchRect, Rect searchLeftRawRect) = RectUtils.SplitHeightRect(foldoutLeftRect, SingleLineHeight);
                searchLeftRect = searchLeftRawRect;
                (Rect keySearchRawRect, Rect valueSearchRawRect) =
                    RectUtils.SplitWidthRect(searchRect, searchRect.width / 2);
                Rect keySearchRect = ShrinkRect(keySearchRawRect);
                using (EditorGUI.ChangeCheckScope keySearchChanged = new EditorGUI.ChangeCheckScope())
                {
                    string keySearchNew = EditorGUI.DelayedTextField(new Rect(keySearchRect)
                    {
                        width = keySearchRect.width - 2,
                    }, GUIContent.none, cachedInfo.KeySearch);
                    if (keySearchChanged.changed && cachedInfo.KeySearch != keySearchNew)
                    {
                        cachedInfo.KeySearch = keySearchNew;
                        RefreshListIMGUI(cachedInfo);
                        return;
                    }
                }

                EditorGUI.LabelField(new Rect(keySearchRect)
                {
                    width = keySearchRect.width - 6,
                }, "Key Search", new GUIStyle("label")
                {
                    alignment = TextAnchor.MiddleRight, normal =
                    {
                        textColor = Color.gray,
                    },
                    fontStyle = FontStyle.Italic
                });

                Rect valueSearchRect = ShrinkRect(valueSearchRawRect);
                using (EditorGUI.ChangeCheckScope valueSearchChanged = new EditorGUI.ChangeCheckScope())
                {
                    string valueSearchNew =
                        EditorGUI.DelayedTextField(valueSearchRect, GUIContent.none, cachedInfo.ValueSearch);
                    if (valueSearchChanged.changed && cachedInfo.ValueSearch != valueSearchNew)
                    {
                        cachedInfo.ValueSearch = valueSearchNew;
                        RefreshListIMGUI(cachedInfo);
                        return;
                    }
                }

                EditorGUI.LabelField(new Rect(valueSearchRect)
                {
                    width = valueSearchRect.width - 6,
                }, "Value Search", new GUIStyle("label")
                {
                    alignment = TextAnchor.MiddleRight, normal =
                    {
                        textColor = Color.gray,
                    },
                    fontStyle = FontStyle.Italic
                });
            }

            (Rect tableRect, Rect bottomRawRect) = RectUtils.SplitHeightRect(searchLeftRect, searchLeftRect.height - SingleLineHeight);

            cachedInfo.SaintsDictionaryTable.OnGUI(tableRect);

            Rect bottomRect = ShrinkRect(bottomRawRect);

            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (_iconButtonStyle == null)
            {
                _iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                };
            }

            // paging
            (Rect numberOfItemsPerPageRect, Rect bottomAfterItemRect) = RectUtils.SplitWidthRect(bottomRect, SingleLineHeight * 2);
            if((saintsDictionaryAttribute?.NumberOfItemsPerPage ?? 0) > 0)
            {
                using (EditorGUI.ChangeCheckScope numberOfItemsPerPageChanged = new EditorGUI.ChangeCheckScope())
                {
                    int numberOfItemsPerPage = EditorGUI.DelayedIntField(ShrinkRect(numberOfItemsPerPageRect),
                        cachedInfo.NumberOfItemsPerPage);
                    if (numberOfItemsPerPageChanged.changed)
                    {
                        cachedInfo.NumberOfItemsPerPage = numberOfItemsPerPage;
                        RefreshListIMGUI(cachedInfo);
                        return;
                    }
                }

                const string totalString = "/Page";
                float totalStringWidth = GUI.skin.label.CalcSize(new GUIContent(totalString)).x;
                (Rect totalStringRect, Rect bottomAfterTotalRect) =
                    RectUtils.SplitWidthRect(bottomAfterItemRect, totalStringWidth);
                EditorGUI.LabelField(totalStringRect, totalString);

                (Rect _, Rect bottomStartPagingRect) = RectUtils.SplitWidthRect(bottomAfterTotalRect, 5);
                (Rect prePageButtonRect, Rect bottomAfterPrePageRect) =
                    RectUtils.SplitWidthRect(bottomStartPagingRect, SingleLineHeight - 2);
                if (_leftIcon == null)
                {
                    _leftIcon = Util.LoadResource<Texture2D>("classic-dropdown-left.png");
                }

                using (new EditorGUI.DisabledScope(cachedInfo.PageIndex <= 0))
                {
                    if (GUI.Button(prePageButtonRect, _leftIcon, _iconButtonStyle))
                    {
                        int newIndex = Mathf.Max(0, cachedInfo.PageIndex - 1);
                        if (newIndex != cachedInfo.PageIndex)
                        {
                            cachedInfo.PageIndex = newIndex;
                            RefreshListIMGUI(cachedInfo);
                            return;
                        }
                    }
                }

                (Rect pageRect, Rect bottomAfterPageRect) = RectUtils.SplitWidthRect(bottomAfterPrePageRect, 40);
                using (EditorGUI.ChangeCheckScope pageChanged = new EditorGUI.ChangeCheckScope())
                {
                    int page = EditorGUI.DelayedIntField(pageRect, GUIContent.none, cachedInfo.PageIndex + 1);
                    if (pageChanged.changed)
                    {
                        int newIndex = Mathf.Clamp(page - 1, 0, cachedInfo.TotalPage - 1);
                        if (newIndex != cachedInfo.PageIndex)
                        {
                            cachedInfo.PageIndex = newIndex;
                            RefreshListIMGUI(cachedInfo);
                            return;
                        }
                    }
                }

                string totalPageString = $"/ {cachedInfo.TotalPage}";
                // Debug.Log(totalPageString);
                float totalPageStringWidth = GUI.skin.label.CalcSize(new GUIContent(totalPageString)).x;
                (Rect totalPageRect, Rect bottomAfterTotalPageRect) =
                    RectUtils.SplitWidthRect(bottomAfterPageRect, totalPageStringWidth);
                EditorGUI.LabelField(totalPageRect, totalPageString);

                (Rect nextPageRect, Rect _) =
                    RectUtils.SplitWidthRect(bottomAfterTotalPageRect, SingleLineHeight - 2);
                if (_rightIcon == null)
                {
                    _rightIcon = Util.LoadResource<Texture2D>("classic-dropdown-right.png");
                }

                using (new EditorGUI.DisabledScope(cachedInfo.PageIndex + 1 >= cachedInfo.TotalPage))
                {
                    if (GUI.Button(nextPageRect, _rightIcon, _iconButtonStyle))
                    {
                        int newIndex = Mathf.Min(cachedInfo.PageIndex + 1, cachedInfo.TotalPage - 1);
                        if (newIndex != cachedInfo.PageIndex)
                        {
                            cachedInfo.PageIndex = newIndex;
                            RefreshListIMGUI(cachedInfo);
                            return;
                        }
                    }
                }
            }

            // add/remove
            (Rect bottomPreRemoveRect, Rect removeButtonRect) = RectUtils.SplitWidthRect(bottomRect, bottomRect.width - SingleLineHeight - 2);
            if (GUI.Button(removeButtonRect, "-", _iconButtonStyle))
            {
                List<int> selected = cachedInfo.SaintsDictionaryTable.GetSelectedIndex()
                    .OrderByDescending(each => each)
                    .ToList();

                if (selected.Count == 0)
                {
                    int curSize = cachedInfo.KeysProp.arraySize;
                    // Debug.Log($"curSize={curSize}");
                    if (curSize == 0)
                    {
                        return;
                    }
                    selected.Add(curSize - 1);
                }

                // Debug.Log($"delete {keysProp.propertyPath}/{keysProp.arraySize} key at {string.Join(",", selected)}");

                DecreaseArraySize(selected, cachedInfo.KeysProp, cachedInfo.ValuesProp);
                property.serializedObject.ApplyModifiedProperties();
                return;
            }
            (Rect _, Rect addButtonRect) = RectUtils.SplitWidthRect(bottomPreRemoveRect, bottomPreRemoveRect.width - SingleLineHeight - 2);
            // ReSharper disable once InvertIf
            if (GUI.Button(addButtonRect, "+", _iconButtonStyle))
            {
                IncreaseArraySize(cachedInfo.KeysProp.arraySize + 1, cachedInfo.KeysProp, cachedInfo.ValuesProp);
                property.serializedObject.ApplyModifiedProperties();
                // ReSharper disable once RedundantJumpStatement
                return;
            }
        }

        private static void RefreshListIMGUI(SaintsDictionaryInfoIMGUI cachedInfo)
        {
            List<int> fullList = Enumerable.Range(0, cachedInfo.KeysProp.arraySize).ToList();
            List<int> refreshedHitTargetIndexes = new List<int>(fullList);

            if (!string.IsNullOrEmpty(cachedInfo.KeySearch) || !string.IsNullOrEmpty(cachedInfo.ValueSearch))
            {
                refreshedHitTargetIndexes = Search(cachedInfo.KeysProp, cachedInfo.ValuesProp, cachedInfo.KeySearch, cachedInfo.ValueSearch);
            }

            if (cachedInfo.NumberOfItemsPerPage > 0)
            {
                int startIndex = cachedInfo.PageIndex * cachedInfo.NumberOfItemsPerPage;
                if(startIndex > refreshedHitTargetIndexes.Count)
                {
                    cachedInfo.PageIndex = 0;
                    startIndex = 0;
                }
                int endIndex = Mathf.Min((cachedInfo.PageIndex + 1) * cachedInfo.NumberOfItemsPerPage, refreshedHitTargetIndexes.Count);
                int totalPage = Mathf.Max(1, Mathf.CeilToInt(refreshedHitTargetIndexes.Count / (float)cachedInfo.NumberOfItemsPerPage));
                cachedInfo.TotalPage = totalPage;

                refreshedHitTargetIndexes = refreshedHitTargetIndexes.GetRange(startIndex, endIndex - startIndex);

            }

            cachedInfo.SaintsDictionaryTable.SetItemIndexToPropertyIndex(refreshedHitTargetIndexes);
        }

        private static Rect ShrinkRect(Rect rect) => new Rect(rect)
        {
            y = rect.y + 1,
            height = rect.height - 2,
        };
    }
}
