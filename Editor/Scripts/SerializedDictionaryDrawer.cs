﻿using AYellowpaper.SerializedCollections.Editor.Data;
using AYellowpaper.SerializedCollections.Editor.States;
using AYellowpaper.SerializedCollections.Populators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace AYellowpaper.SerializedCollections.Editor
{
    [CustomPropertyDrawer(typeof(SerializedDictionary<,>))]
    public class SerializedDictionaryDrawer : PropertyDrawer
    {
        public const string KeyName = nameof(SerializedKeyValuePair<int, int>.Key);
        public const string ValueName = nameof(SerializedKeyValuePair<int, int>.Value);
        public const string SerializedListName = nameof(SerializedDictionary<int, int>._serializedList);
        public const string LookupTableName = nameof(SerializedDictionary<int, int>.LookupTable);

        private const int TopHeaderClipHeight = 20;
        private const int TopHeaderHeight = 19;
        private const int SearchHeaderHeight = 20;
        private const int KeyValueHeaderHeight = 18;
        private const bool KeyFlag = true;
        private const bool ValueFlag = false;
        private static readonly Color BorderColor = new Color(36 / 255f, 36 / 255f, 36 / 255f);
        private static readonly List<int> NoEntriesList = new List<int>();

        private Dictionary<string, DictionaryDrawer> _arrayData = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!_arrayData.ContainsKey(property.propertyPath))
                _arrayData.Add(property.propertyPath, new DictionaryDrawer(fieldInfo));
            _arrayData[property.propertyPath].OnGUI(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!_arrayData.ContainsKey(property.propertyPath))
                _arrayData.Add(property.propertyPath, new DictionaryDrawer(fieldInfo));
            return _arrayData[property.propertyPath].GetPropertyHeight(property, label);
        }

        public class DictionaryDrawer
        {
            private FieldInfo _fieldInfo;
            private bool _initialized = false;
            internal ReorderableList ReorderableList { get; private set; }
            private ReorderableList _unexpandedList;
            private SingleEditingData _singleEditing;
            private Type _entryType;
            private FieldInfo _keyFieldInfo;
            private GUIContent _label;
            private Rect _totalRect;
            private GUIStyle _keyValueStyle;
            private SerializedDictionaryAttribute _dictionaryAttribute;
            private PropertyData _propertyData;
            private bool _propertyListSettingsInitialized = false;
            private List<int> _pagedIndices;
            private PagingElement _pagingElement;
            private int _lastListSize = -1;
            private IReadOnlyList<KeysGeneratorData> _populators;
            private Action _queuedAction;
            private SearchField _searchField;
            private GUIContent _detailsContent;
            private bool _showSearchBar = false;
            private ListState _activeState;

            internal SerializedProperty ListProperty { get; private set; }
            internal string SearchText { get; private set; } = string.Empty;
            internal SearchListState SearchState { get; private set; }
            internal DefaultListState DefaultState { get; private set; }

            private class SingleEditingData
            {
                public bool IsValid => BackingList != null;
                public IList BackingList;
                public ILookupTable LookupTable;

                public void Invalidate()
                {
                    BackingList = null;
                    LookupTable = null;
                }
            }

            public DictionaryDrawer(FieldInfo fieldInfo)
            {
                _fieldInfo = fieldInfo;
            }

            public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                InitializeIfNeeded(property);

                _totalRect = position;
                _label = new GUIContent(label);

                EditorGUI.BeginChangeCheck();
                DoList(position);
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
                ProcessState();
                ProcessQueuedAction();
            }

            private void DoList(Rect position)
            {
                if (ListProperty.isExpanded)
                    ReorderableList.DoList(position);
                else
                {
                    using (new GUI.ClipScope(new Rect(0, position.y, position.width + position.x, TopHeaderClipHeight)))
                    {
                        _unexpandedList.DoList(position.WithY(0));
                    }
                }
            }

            private void ProcessState()
            {
                var newState = _activeState.OnUpdate();
                if (newState != null && newState != _activeState)
                {
                    _activeState.OnExit();
                    _activeState = newState;
                    newState.OnEnter();
                }
            }

            private void ProcessQueuedAction()
            {
                if (_queuedAction != null)
                {
                    _queuedAction();
                    _queuedAction = null;
                }
            }

            public float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                InitializeIfNeeded(property);

                if (!ListProperty.isExpanded)
                    return TopHeaderClipHeight;

                return ReorderableList.GetHeight();
            }

            private SerializedProperty GetElementProperty(SerializedProperty property, bool fieldFlag)
            {
                return property.FindPropertyRelative(fieldFlag == KeyFlag ? KeyName : ValueName);
            }

            internal static float CalculateHeightOfElement(SerializedProperty property, bool drawKeyAsList, bool drawValueAsList)
            {
                SerializedProperty keyProperty = property.FindPropertyRelative(KeyName);
                SerializedProperty valueProperty = property.FindPropertyRelative(ValueName);
                return Mathf.Max(SCEditorUtility.CalculateHeight(keyProperty, drawKeyAsList), SCEditorUtility.CalculateHeight(valueProperty, drawValueAsList));
            }

            private void InitializeIfNeeded(SerializedProperty property)
            {
                ListProperty = property.FindPropertyRelative(SerializedListName);

                if (!_initialized)
                {
                    _initialized = true;
                    _keyValueStyle = new GUIStyle(EditorStyles.toolbarButton);
                    _keyValueStyle.padding = new RectOffset(0, 0, 0, 0);
                    _keyValueStyle.border = new RectOffset(0, 0, 0, 0);
                    _keyValueStyle.alignment = TextAnchor.MiddleCenter;

                    DefaultState = new DefaultListState(this);
                    SearchState = new SearchListState(this);
                    _activeState = DefaultState;

                    _dictionaryAttribute = _fieldInfo.GetCustomAttribute<SerializedDictionaryAttribute>();

                    _propertyData = SCEditorUtility.GetPropertyData(ListProperty);
                    _propertyData.GetElementData(SCEditorUtility.KeyFlag).Settings.DisplayName = _dictionaryAttribute?.KeyName ?? "Key";
                    _propertyData.GetElementData(SCEditorUtility.ValueFlag).Settings.DisplayName = _dictionaryAttribute?.ValueName ?? "Value";
                    SavePropertyData();

                    _pagingElement = new PagingElement();
                    _pagedIndices = new List<int>();
                    UpdatePaging();

                    ReorderableList = MakeList();
                    _unexpandedList = MakeUnexpandedList();
                    _searchField = new SearchField();

                    var listField = _fieldInfo.FieldType.GetField(SerializedListName, BindingFlags.Instance | BindingFlags.NonPublic);
                    _entryType = listField.FieldType.GetGenericArguments()[0];
                    _keyFieldInfo = _entryType.GetField(KeyName);

                    _singleEditing = new SingleEditingData();

                    _populators = KeysGeneratorCache.GetPopulatorsForType(_keyFieldInfo.FieldType);
                }

                void InitializeSettings(bool fieldFlag)
                {
                    var genericArgs = _fieldInfo.FieldType.GetGenericArguments();
                    var firstProperty = ListProperty.GetArrayElementAtIndex(0);
                    var keySettings = CreateDisplaySettings(GetElementProperty(firstProperty, fieldFlag), genericArgs[fieldFlag == SCEditorUtility.KeyFlag ? 0 : 1]);
                    var settings = _propertyData.GetElementData(fieldFlag).Settings;
                    settings.DisplayType = keySettings.displayType;
                    settings.HasListDrawerToggle = keySettings.canToggleListDrawer;
                }

                if (!_propertyListSettingsInitialized && ListProperty.minArraySize > 0)
                {
                    _propertyListSettingsInitialized = true;
                    InitializeSettings(SCEditorUtility.KeyFlag);
                    InitializeSettings(SCEditorUtility.ValueFlag);
                    SavePropertyData();
                }

                // TODO: Is there a better solution to check for Revert/delete/add?
                if (_lastListSize != _activeState.ListSize)
                {
                    _lastListSize = _activeState.ListSize;
                    UpdateSingleEditing();
                    UpdatePaging();
                }

                ToggleSearchBar(_propertyData.AlwaysShowSearch ? true : SCEditorUtility.ShouldShowSearch(_pagingElement.PageCount));
            }

            private void SavePropertyData()
            {
                SCEditorUtility.SavePropertyData(ListProperty, _propertyData);
            }

            private void UpdateSingleEditing()
            {
                if (ListProperty.serializedObject.isEditingMultipleObjects && _singleEditing.IsValid)
                    _singleEditing.Invalidate();
                else if (!ListProperty.serializedObject.isEditingMultipleObjects && !_singleEditing.IsValid)
                {
                    var dictionary = SCEditorUtility.GetParent(ListProperty, ListProperty.serializedObject.targetObject);
                    _singleEditing.BackingList = GetBackingList(dictionary);
                    _singleEditing.LookupTable = GetLookupTable(dictionary);
                }
            }

            private ILookupTable GetLookupTable(object dictionary)
            {
                var propInfo = dictionary.GetType().GetProperty(LookupTableName, BindingFlags.Instance | BindingFlags.NonPublic);
                return (ILookupTable)propInfo.GetValue(dictionary);
            }

            private IList GetBackingList(object dictionary)
            {
                var listField = _fieldInfo.FieldType.GetField(SerializedListName, BindingFlags.Instance | BindingFlags.NonPublic);
                return (IList)listField.GetValue(dictionary);
            }

            private void UpdatePaging()
            {
                var elementsPerPage = EditorUserSettings.Get().ElementsPerPage;
                _pagingElement.PageCount = Mathf.Max(1, Mathf.CeilToInt((float)_activeState.ListSize / elementsPerPage));

                _pagedIndices.Clear();
                _pagedIndices.Capacity = Mathf.Max(elementsPerPage, _pagedIndices.Capacity);

                int startIndex = (_pagingElement.Page - 1) * elementsPerPage;
                int endIndex = Mathf.Min(startIndex + elementsPerPage, _activeState.ListSize);
                for (int i = startIndex; i < endIndex; i++)
                    _pagedIndices.Add(i);

                string detailsString = _pagingElement.PageCount > 1
                    ? $"{_pagedIndices[0] + 1}..{_pagedIndices.Last() + 1} / {_activeState.ListSize} Elements"
                    : (_activeState.ListSize + " " + (_pagedIndices.Count == 1 ? "Element" : "Elements"));
                _detailsContent = new GUIContent(detailsString);
            }

            private ReorderableList MakeList()
            {
                var list = new ReorderableList(_pagedIndices, typeof(int), true, true, true, true);
                list.onAddCallback += OnAdd;
                list.onRemoveCallback += OnRemove;
                list.onReorderCallbackWithDetails += OnReorder;
                list.drawElementCallback += OnDrawElement;
                list.elementHeightCallback += OnGetElementHeight;
                list.drawHeaderCallback += OnDrawHeader;
                list.drawNoneElementCallback += OnDrawNone;
                return list;
            }

            private void ToggleSearchBar(bool flag)
            {
                _showSearchBar = flag;
                ReorderableList.headerHeight = TopHeaderClipHeight + KeyValueHeaderHeight + (_showSearchBar ? SearchHeaderHeight : 0);
                if (!_showSearchBar)
                {
                    if (_searchField.HasFocus())
                        GUI.FocusControl(null);
                    SearchText = string.Empty;
                }
            }

            private void OnDrawNone(Rect rect)
            {
                EditorGUI.LabelField(rect, EditorGUIUtility.TrTextContent(_activeState.NoElementsText));
            }

            private ReorderableList MakeUnexpandedList()
            {
                var list = new ReorderableList(NoEntriesList, typeof(int));
                list.drawHeaderCallback = DrawUnexpandedHeader;
                return list;
            }

            private (DisplayType displayType, bool canToggleListDrawer) CreateDisplaySettings(SerializedProperty property, Type type)
            {
                bool hasCustomEditor = SCEditorUtility.HasDrawerForType(type);
                bool isGenericWithChildren = property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren;
                bool isArray = property.isArray && property.propertyType != SerializedPropertyType.String;
                bool canToggleListDrawer = isArray || (isGenericWithChildren && hasCustomEditor);
                DisplayType displayType = DisplayType.PropertyNoLabel;
                if (canToggleListDrawer)
                    displayType = DisplayType.Property;
                else if (!isArray && isGenericWithChildren && !hasCustomEditor)
                    displayType = DisplayType.List;
                return (displayType, canToggleListDrawer);
            }

            private void DrawUnexpandedHeader(Rect rect)
            {
                EditorGUI.BeginProperty(rect, _label, ListProperty);
                ListProperty.isExpanded = EditorGUI.Foldout(rect.WithX(rect.x - 5), ListProperty.isExpanded, _label, true);

                var detailsStyle = EditorStyles.miniLabel;
                var detailsRect = rect.AppendRight(0).AppendLeft(detailsStyle.CalcSize(_detailsContent).x);
                GUI.Label(detailsRect, _detailsContent, detailsStyle);

                EditorGUI.EndProperty();
            }

            private void DoPaging(Rect rect)
            {
                EditorGUI.BeginChangeCheck();
                _pagingElement.OnGUI(rect);
                if (EditorGUI.EndChangeCheck())
                {
                    ReorderableList.ClearSelection();
                    UpdatePaging();
                }
            }

            private void OnDrawHeader(Rect rect)
            {
                Rect topRect = rect.WithHeight(TopHeaderHeight);
                Rect adjustedTopRect = topRect.WithXAndWidth(_totalRect.x + 1, _totalRect.width - 1);

                DoMainHeader(adjustedTopRect.CutLeft(topRect.x - adjustedTopRect.x));
                if (_showSearchBar)
                {
                    adjustedTopRect = adjustedTopRect.AppendDown(SearchHeaderHeight);
                    DoSearch(adjustedTopRect);
                }
                DoKeyValueRect(adjustedTopRect.AppendDown(KeyValueHeaderHeight));
            }

            private void DoMainHeader(Rect rect)
            {
                Rect lastTopRect = rect.AppendRight(0).WithHeight(EditorGUIUtility.singleLineHeight);

                lastTopRect = lastTopRect.AppendLeft(20);
                DoOptionsButton(lastTopRect);
                lastTopRect = lastTopRect.AppendLeft(5);

                if (_pagingElement.PageCount > 1)
                {
                    lastTopRect = lastTopRect.AppendLeft(_pagingElement.GetDesiredWidth());
                    DoPaging(lastTopRect);
                }

                var detailsStyle = EditorStyles.miniLabel;
                lastTopRect = lastTopRect.AppendLeft(detailsStyle.CalcSize(_detailsContent).x, 5);
                GUI.Label(lastTopRect, _detailsContent, detailsStyle);

                if (!_singleEditing.IsValid)
                {
                    lastTopRect = lastTopRect.AppendLeft(lastTopRect.height + 5);
                    var guicontent = EditorGUIUtility.TrIconContent(EditorGUIUtility.Load("d_console.infoicon") as Texture, "Conflict checking, duplicate key removal and populators not supported in multi object editing mode.");
                    GUI.Label(lastTopRect, guicontent);
                }

                EditorGUI.BeginProperty(rect, _label, ListProperty);
                ListProperty.isExpanded = EditorGUI.Foldout(rect.WithXAndWidth(rect.x - 5, lastTopRect.x - rect.x), ListProperty.isExpanded, _label, true);
                EditorGUI.EndProperty();
            }

            private void DoOptionsButton(Rect rect)
            {
                if (GUI.Button(rect, EditorGUIUtility.IconContent("pane options@2x"), EditorStyles.iconButton))
                {
                    OpenStuff(rect);
                    //var gm = new GenericMenu();
                    //SCEditorUtility.AddGenericMenuItem(gm, false, true, new GUIContent("Clear"), () => QueueAction(() => ListProperty.ClearArray()));
                    //SCEditorUtility.AddGenericMenuItem(gm, false, _singleEditing.IsValid, new GUIContent("Remove Conflicts"), () => QueueAction(RemoveConflicts));
                    //SCEditorUtility.AddGenericMenuItem(gm, false, true, new GUIContent("Bulk Edit..."), () => OpenStuff(rect));
                    ////foreach (var populatorData in _populators)
                    ////{
                    ////    SCEditorUtility.AddGenericMenuItem(gm, false, _singleEditing.IsValid, new GUIContent(populatorData.Name), OnPopulatorDataSelected, populatorData.PopulatorType);
                    ////}
                    //gm.AddSeparator(string.Empty);
                    //SCEditorUtility.AddGenericMenuItem(gm, _propertyData.AlwaysShowSearch, true, new GUIContent("Always Show Search"), ToggleAlwaysShowSearchPropertyData);
                    //gm.AddItem(new GUIContent("Preferences..."), false, () => SettingsService.OpenUserPreferences(EditorUserSettingsProvider.PreferencesPath));
                    //gm.DropDown(rect);
                }
            }

            private void OpenStuff(Rect rect)
            {
                var screenRect = GUIUtility.GUIToScreenRect(rect);
                var window = ScriptableObject.CreateInstance<KeysGeneratorSelectorWindow>();
                window.Initialize(_populators);
                window.ShowAsDropDown(screenRect, new Vector2(400, 200));
                window.OnApply += ApplyPopulatorQueued;
            }

            private void ToggleAlwaysShowSearchPropertyData()
            {
                _propertyData.AlwaysShowSearch = !_propertyData.AlwaysShowSearch;
                SavePropertyData();
            }

            private void DoKeyValueRect(Rect rect)
            {
                float width = EditorGUIUtility.labelWidth + 22;
                Rect leftRect = rect.WithWidth(width);
                Rect rightRect = leftRect.AppendRight(rect.width - width);

                if (Event.current.type == EventType.Repaint && _propertyData != null)
                {
                    _keyValueStyle.Draw(leftRect, EditorGUIUtility.TrTextContent(_propertyData.GetElementData(KeyFlag).Settings.DisplayName), false, false, false, false);
                    _keyValueStyle.Draw(rightRect, EditorGUIUtility.TrTextContent(_propertyData.GetElementData(ValueFlag).Settings.DisplayName), false, false, false, false);
                }

                if (ListProperty.minArraySize > 0)
                {
                    DoDisplayTypeToggle(leftRect, KeyFlag);
                    DoDisplayTypeToggle(rightRect, ValueFlag);
                }

                EditorGUI.DrawRect(rect.AppendDown(1, -1), BorderColor);
            }

            private void DoSearch(Rect rect)
            {
                EditorGUI.DrawRect(rect.AppendLeft(1), BorderColor);
                EditorGUI.DrawRect(rect.AppendRight(1, -1), BorderColor);
                EditorGUI.DrawRect(rect.AppendDown(1, -1), BorderColor);

                SearchText = _searchField.OnToolbarGUI(rect.CutTop(2).CutHorizontal(6), SearchText);
            }

            private void QueueAction(Action action)
            {
                _queuedAction = action;
            }

            private void ApplyPopulatorQueued(KeysGenerator populator, ModificationType modificationType)
            {
                var array = populator.GetElements(_keyFieldInfo.FieldType).OfType<object>().ToArray();
                EditorApplication.delayCall += () => ApplyPopulator(array, modificationType);
                //QueueAction(() => ApplyPopulator(array, modificationType));
            }

            private void ApplyPopulator(IEnumerable<object> elements, ModificationType modificationType)
            {
                object entry = Activator.CreateInstance(_entryType);

                foreach (var targetObject in ListProperty.serializedObject.targetObjects)
                {
                    Debug.Log("going in");
                    Undo.RecordObject(targetObject, "Populate");
                    var dictionary = SCEditorUtility.GetParent(ListProperty, targetObject);
                    var lookupTable = GetLookupTable(dictionary);
                    var list = GetBackingList(dictionary);

                    if (modificationType == ModificationType.Add)
                    {
                        foreach (var key in elements)
                        {
                            var occurences = lookupTable.GetOccurences(key);
                            if (occurences.Count > 0)
                                continue;
                            _keyFieldInfo.SetValue(entry, key);
                            Debug.Log("adding");
                            list.Add(entry);
                        }
                    }
                    else if (modificationType == ModificationType.Remove)
                    {
                        foreach (var existingKey in lookupTable.Keys)
                        {
                            Debug.Log("removing");
                            list.Remove(existingKey);
                        }
                    }
                    else if (modificationType == ModificationType.Confine)
                    {
                        var keysToRemove = lookupTable.Keys.OfType<object>().ToHashSet();
                        foreach (var key in elements)
                            keysToRemove.Remove(key);

                        foreach (var keyToRemove in keysToRemove)
                            list.Remove(keyToRemove);
                    }


                    // TODO: This is only done because OnAfterDeserialize doesn't fire. Not really obvious why this has to be called manually here
                    lookupTable.RecalculateOccurences();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
                }

                ListProperty.serializedObject.Update();
            }

            private void RemoveConflicts()
            {
                foreach (var targetObject in ListProperty.serializedObject.targetObjects)
                {
                    Undo.RecordObject(targetObject, "Populate");
                    var dictionary = SCEditorUtility.GetParent(ListProperty, targetObject);
                    var lookupTable = GetLookupTable(dictionary);
                    var list = GetBackingList(dictionary);

                    List<int> duplicateIndices = new List<int>();

                    foreach (var key in lookupTable.Keys)
                    {
                        var occurences = lookupTable.GetOccurences(key);
                        for (int i = 1; i < occurences.Count; i++)
                            duplicateIndices.Add(occurences[i]);
                    }

                    foreach (var indexToRemove in duplicateIndices.OrderByDescending(x => x))
                    {
                        list.RemoveAt(indexToRemove);
                    }

                    // TODO: This is only done because OnAfterDeserialize doesn't fire. Not really obvious why this has to be called manually here
                    lookupTable.RecalculateOccurences();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
                }
            }

            private void DoDisplayTypeToggle(Rect contentRect, bool fieldFlag)
            {
                var displayData = _propertyData.GetElementData(fieldFlag);

                if (displayData.Settings.HasListDrawerToggle)
                {
                    Rect rightRectToggle = new Rect(contentRect);
                    rightRectToggle.x += rightRectToggle.width - 18;
                    rightRectToggle.width = 18;
                    EditorGUI.BeginChangeCheck();
                    bool newValue = GUI.Toggle(rightRectToggle, displayData.IsListToggleActive, "", EditorStyles.toolbarButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        displayData.IsListToggleActive = newValue;
                        SavePropertyData();
                    }
                }
            }

            private float OnGetElementHeight(int index)
            {
                int actualIndex = _pagedIndices[index];
                var element = _activeState.GetPropertyAtIndex(actualIndex);
                return CalculateHeightOfElement(element, _propertyData.GetElementData(KeyFlag).EffectiveDisplayType == DisplayType.List ? true : false, _propertyData.GetElementData(ValueFlag).EffectiveDisplayType == DisplayType.List ? true : false);
            }

            private void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
            {
                const int lineLeftSpace = 2;
                const int lineWidth = 1;
                const int lineRightSpace = 12;
                const int totalSpace = lineLeftSpace + lineWidth + lineRightSpace;

                int actualIndex = _pagedIndices[index];

                SerializedProperty kvp = _activeState.GetPropertyAtIndex(actualIndex);
                Rect keyRect = rect.WithSize(EditorGUIUtility.labelWidth - lineLeftSpace, EditorGUIUtility.singleLineHeight);
                Rect lineRect = keyRect.WithXAndWidth(keyRect.x + keyRect.width + lineLeftSpace, lineWidth).WithHeight(rect.height);
                Rect valueRect = keyRect.AppendRight(rect.width - keyRect.width - totalSpace, totalSpace);

                var keyProperty = kvp.FindPropertyRelative(KeyName);
                var valueProperty = kvp.FindPropertyRelative(ValueName);

                Color prevColor = GUI.color;
                if (_singleEditing.IsValid)
                {
                    //Debug.Log(actualIndex + " " + _singleEditing.BackingList.Count + " " + _activeState.ListSize + " " + ListProperty.propertyPath);
                    var keyObject = _keyFieldInfo.GetValue(_singleEditing.BackingList[actualIndex]);
                    var occurences = _singleEditing.LookupTable.GetOccurences(keyObject);
                    if (occurences.Count > 1)
                    {
                        GUI.color = occurences[0] == actualIndex ? Color.yellow : Color.red;
                    }
                    if (!SerializedCollectionsUtility.IsValidKey(keyObject))
                    {
                        GUI.color = Color.red;
                    }
                }

                var keyDisplayData = _propertyData.GetElementData(KeyFlag);
                DrawGroupedElement(keyRect, 20, keyProperty, keyDisplayData.EffectiveDisplayType);

                EditorGUI.DrawRect(lineRect, new Color(36 / 255f, 36 / 255f, 36 / 255f));
                GUI.color = prevColor;

                var valueDisplayData = _propertyData.GetElementData(ValueFlag);
                DrawGroupedElement(valueRect, lineRightSpace, valueProperty, valueDisplayData.EffectiveDisplayType);
            }

            private void DrawGroupedElement(Rect rect, int spaceForProperty, SerializedProperty property, DisplayType displayType)
            {
                using (new LabelWidth(rect.width * 0.4f))
                {
                    float height = SCEditorUtility.CalculateHeight(property.Copy(), displayType);
                    Rect groupRect = rect.CutLeft(-spaceForProperty).WithHeight(height);
                    GUI.BeginGroup(groupRect);

                    Rect elementRect = new Rect(spaceForProperty, 0, rect.width, height);
                    _activeState.DrawElement(elementRect, property, displayType);

                    DrawInvisibleProperty(rect.WithWidth(spaceForProperty), property);

                    GUI.EndGroup();
                }
            }

            internal static void DrawInvisibleProperty(Rect rect, SerializedProperty property)
            {
                const int propertyOffset = 5;

                GUI.BeginClip(rect.CutLeft(-propertyOffset));
                EditorGUI.BeginProperty(rect, GUIContent.none, property);
                EditorGUI.EndProperty();
                GUI.EndClip();
            }

            internal static void DrawElement(Rect rect, SerializedProperty property, DisplayType displayType, Action<SerializedProperty> BeforeDrawingCallback = null, Action<SerializedProperty> AfterDrawingCallback = null)
            {
                switch (displayType)
                {
                    case DisplayType.Property:
                        BeforeDrawingCallback?.Invoke(property);
                        EditorGUI.PropertyField(rect, property, true);
                        AfterDrawingCallback?.Invoke(property);
                        break;
                    case DisplayType.PropertyNoLabel:
                        BeforeDrawingCallback?.Invoke(property);
                        EditorGUI.PropertyField(rect, property, GUIContent.none, true);
                        AfterDrawingCallback?.Invoke(property);
                        break;
                    case DisplayType.List:
                        Rect childRect = rect.WithHeight(0);
                        foreach (SerializedProperty prop in SCEditorUtility.GetChildren(property.Copy()))
                        {
                            childRect = childRect.AppendDown(EditorGUI.GetPropertyHeight(prop, true));
                            BeforeDrawingCallback?.Invoke(prop);
                            EditorGUI.PropertyField(childRect, prop, true);
                            AfterDrawingCallback?.Invoke(prop);
                        }
                        break;
                    default:
                        break;
                }
            }

            private void OnAdd(ReorderableList list)
            {
                int targetIndex = list.selectedIndices.Count > 0 && list.selectedIndices[0] >= 0 ? list.selectedIndices[0] : 0;
                int actualTargetIndex = targetIndex < _pagedIndices.Count ? _pagedIndices[targetIndex] : 0;
                _activeState.InserElementAt(actualTargetIndex);
            }

            private void OnReorder(ReorderableList list, int oldIndex, int newIndex)
            {
                UpdatePaging();
                ListProperty.MoveArrayElement(_pagedIndices[oldIndex], _pagedIndices[newIndex]);
            }

            private void OnRemove(ReorderableList list)
            {
                _activeState.RemoveElementAt(_pagedIndices[list.index]);
                UpdatePaging();
                //int actualIndex = _pagedIndices[list.index];
                //ListProperty.DeleteArrayElementAtIndex(actualIndex);
                //UpdatePaging();
                //if (actualIndex >= ListProperty.minArraySize)
                //    list.index = _pagedIndices.Count - 1;
            }
        }
    }
}