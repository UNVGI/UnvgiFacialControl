using System;
using System.Collections.Generic;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.LipSync.Editor.Inspector
{
    public sealed class PhonemeEntryListView : VisualElement
    {
        public enum EntryKind
        {
            BlendShape,
            AnimationClip,
        }

        public const string RootClassName = "facial-control-lipsync-phoneme-entry-list-view";
        public const string ListViewName = "ulipsync-phoneme-entry-list";
        public const string RowClassName = "ulipsync-phoneme-entry-row";
        public const string EntryTypeSelectorName = "ulipsync-phoneme-entry-type-selector";
        public const string BlendShapeWarningName = "ulipsync-phoneme-entry-blend-shape-warning";
        public const string BlendShapeLabel = "BlendShape 形式";
        public const string AnimationClipLabel = "AnimationClip 形式";

        private const string PhonemeEntriesFieldName = "_phonemeEntries";
        private const string PhonemeIdFieldName = nameof(PhonemeEntryBase.PhonemeId);
        private const string MaxWeightFieldName = nameof(PhonemeEntryBase.MaxWeight);
        private const string BlendShapeNameFieldName = nameof(BlendShapePhonemeEntry.BlendShapeName);
        private const string ClipFieldName = nameof(AnimationClipPhonemeEntry.Clip);

        private static readonly List<string> EntryTypeChoices = new List<string>
        {
            BlendShapeLabel,
            AnimationClipLabel,
        };

        private readonly SerializedProperty _listProperty;
        private readonly List<int> _indexProxy = new List<int>();
        private readonly ListView _listView;

        public PhonemeEntryListView(SerializedProperty listProperty)
        {
            _listProperty = listProperty ?? throw new ArgumentNullException(nameof(listProperty));
            if (!_listProperty.isArray)
            {
                throw new ArgumentException(
                    "listProperty must be an array property bound to _phonemeEntries.",
                    nameof(listProperty));
            }

            AddToClassList(RootClassName);

            RebuildIndexProxy(_indexProxy, GetListProperty());
            _listView = new ListView
            {
                name = ListViewName,
                itemsSource = _indexProxy,
                fixedItemHeight = 132f,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = true,
                headerTitle = "音素エントリ",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                unbindItem = (element, _) => element.Clear(),
            };
            _listView.style.marginTop = 4;
            _listView.style.minHeight = 96f;
            _listView.overridingAddButtonBehavior = (_, button) => OpenAddMenu(button);
            _listView.itemsAdded += OnItemsAdded;
            _listView.itemsRemoved += OnItemsRemoved;
            _listView.itemIndexChanged += OnItemIndexChanged;
            Add(_listView);
        }

        public void AddEntry(EntryKind kind)
        {
            AddEntry(kind, GetListProperty().arraySize);
        }

        public void RemoveEntryAt(int index)
        {
            SerializedProperty list = GetListProperty();
            if (index < 0 || index >= list.arraySize)
            {
                return;
            }

            SerializedObject serializedObject = list.serializedObject;
            serializedObject.Update();
            list = GetListProperty();
            list.DeleteArrayElementAtIndex(index);
            ApplyModifiedPropertiesAndRefresh(serializedObject);
        }

        public void MoveEntry(int fromIndex, int toIndex)
        {
            SerializedProperty list = GetListProperty();
            if (fromIndex < 0 || fromIndex >= list.arraySize)
            {
                return;
            }

            if (toIndex < 0)
            {
                toIndex = 0;
            }
            if (toIndex >= list.arraySize)
            {
                toIndex = list.arraySize - 1;
            }
            if (fromIndex == toIndex)
            {
                return;
            }

            SerializedObject serializedObject = list.serializedObject;
            serializedObject.Update();
            list = GetListProperty();
            list.MoveArrayElement(fromIndex, toIndex);
            ApplyModifiedPropertiesAndRefresh(serializedObject);
        }

        public void SetEntryKind(int index, EntryKind kind)
        {
            SerializedProperty list = GetListProperty();
            if (index < 0 || index >= list.arraySize)
            {
                return;
            }

            SerializedObject serializedObject = list.serializedObject;
            serializedObject.Update();
            list = GetListProperty();

            SerializedProperty entryProperty = list.GetArrayElementAtIndex(index);
            EntryKind currentKind = ResolveEntryKind(entryProperty);
            if (entryProperty.managedReferenceValue != null && currentKind == kind)
            {
                return;
            }

            var previous = entryProperty.managedReferenceValue as PhonemeEntryBase;
            PhonemeEntryBase replacement = CreateEntry(kind);
            CopyCommonFields(previous, replacement);

            entryProperty.managedReferenceValue = replacement;
            ApplyModifiedPropertiesAndRefresh(serializedObject);
        }

        public VisualElement CreateBoundRowForIndex(int index)
        {
            var row = MakeRow();
            BindRow(row, index);
            return row;
        }

        private void AddEntry(EntryKind kind, int index)
        {
            SerializedProperty list = GetListProperty();
            if (index < 0 || index > list.arraySize)
            {
                index = list.arraySize;
            }

            SerializedObject serializedObject = list.serializedObject;
            serializedObject.Update();
            list = GetListProperty();

            list.InsertArrayElementAtIndex(index);
            SerializedProperty entryProperty = list.GetArrayElementAtIndex(index);
            entryProperty.managedReferenceValue = CreateEntry(kind);
            ApplyModifiedPropertiesAndRefresh(serializedObject);
        }

        private void OpenAddMenu(Button anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent(BlendShapeLabel),
                false,
                () => AddEntry(EntryKind.BlendShape));
            menu.AddItem(
                new GUIContent(AnimationClipLabel),
                false,
                () => AddEntry(EntryKind.AnimationClip));
            menu.DropDown(anchor != null ? anchor.worldBound : _listView.worldBound);
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 4;
            return row;
        }

        private void BindRow(VisualElement row, int visualIndex)
        {
            row.Clear();

            SerializedProperty list = GetListProperty();
            int propertyIndex = ResolvePropertyIndex(visualIndex);
            if (propertyIndex < 0 || propertyIndex >= list.arraySize)
            {
                row.Add(new Label("<missing phoneme entry>"));
                return;
            }

            SerializedProperty entryProperty = list.GetArrayElementAtIndex(propertyIndex);
            EntryKind kind = ResolveEntryKind(entryProperty);
            row.Add(CreateTypeSelector(propertyIndex, kind));

            if (entryProperty.managedReferenceValue == null)
            {
                row.Add(new HelpBox("音素エントリの型が未設定です。", HelpBoxMessageType.Warning));
                return;
            }

            AddCommonFields(row, entryProperty);

            if (kind == EntryKind.BlendShape)
            {
                AddBlendShapeFields(row, entryProperty);
            }
            else
            {
                AddAnimationClipFields(row, entryProperty);
            }
        }

        private DropdownField CreateTypeSelector(int propertyIndex, EntryKind kind)
        {
            string currentLabel = ToLabel(kind);
            var selector = new DropdownField("形式")
            {
                name = EntryTypeSelectorName,
                choices = EntryTypeChoices,
            };
            selector.SetValueWithoutNotify(currentLabel);
            selector.RegisterValueChangedCallback(evt =>
            {
                if (TryParseKind(evt.newValue, out EntryKind selectedKind))
                {
                    SetEntryKind(propertyIndex, selectedKind);
                }
            });
            return selector;
        }

        private static void AddCommonFields(VisualElement row, SerializedProperty entryProperty)
        {
            AddTextField(row, entryProperty, PhonemeIdFieldName, "音素 ID");
            AddFloatField(row, entryProperty, MaxWeightFieldName, "Max Weight");
        }

        private static void AddBlendShapeFields(VisualElement row, SerializedProperty entryProperty)
        {
            SerializedProperty blendShapeNameProperty =
                entryProperty.FindPropertyRelative(BlendShapeNameFieldName);
            if (blendShapeNameProperty == null)
            {
                row.Add(new Label($"<missing field: {BlendShapeNameFieldName}>"));
                return;
            }

            var field = new TextField("BlendShape 名");
            field.BindProperty(blendShapeNameProperty);
            row.Add(field);

            var warning = new HelpBox("BlendShape 名未設定", HelpBoxMessageType.Warning)
            {
                name = BlendShapeWarningName,
            };
            ApplyBlendShapeWarningVisibility(warning, blendShapeNameProperty.stringValue);
            field.RegisterValueChangedCallback(evt =>
            {
                ApplyBlendShapeWarningVisibility(warning, evt.newValue);
            });
            row.Add(warning);
        }

        private static void AddAnimationClipFields(VisualElement row, SerializedProperty entryProperty)
        {
            SerializedProperty clipProperty = entryProperty.FindPropertyRelative(ClipFieldName);
            if (clipProperty == null)
            {
                row.Add(new Label($"<missing field: {ClipFieldName}>"));
                return;
            }

            var field = new ObjectField("AnimationClip")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            field.BindProperty(clipProperty);
            row.Add(field);
        }

        private static void AddTextField(
            VisualElement row,
            SerializedProperty parent,
            string relativePath,
            string label)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            if (property == null)
            {
                row.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            var field = new TextField(label);
            field.BindProperty(property);
            row.Add(field);
        }

        private static void AddFloatField(
            VisualElement row,
            SerializedProperty parent,
            string relativePath,
            string label)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            if (property == null)
            {
                row.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            var field = new FloatField(label);
            field.BindProperty(property);
            row.Add(field);
        }

        private void OnItemsAdded(IEnumerable<int> indices)
        {
            foreach (int index in indices)
            {
                AddEntry(EntryKind.BlendShape, index);
                break;
            }
        }

        private void OnItemsRemoved(IEnumerable<int> indices)
        {
            var sorted = new List<int>(indices);
            sorted.Sort();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                int visualIndex = sorted[i];
                RemoveEntryAt(visualIndex);
            }
        }

        private void OnItemIndexChanged(int fromIndex, int toIndex)
        {
            MoveEntry(fromIndex, toIndex);
        }

        private void ApplyModifiedPropertiesAndRefresh(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            if (serializedObject.targetObject != null)
            {
                EditorUtility.SetDirty(serializedObject.targetObject);
            }

            serializedObject.Update();
            RebuildIndexProxy(_indexProxy, GetListProperty());
            _listView?.Rebuild();
        }

        private SerializedProperty GetListProperty()
        {
            return _listProperty.serializedObject.FindProperty(_listProperty.propertyPath) ?? _listProperty;
        }

        private int ResolvePropertyIndex(int visualIndex)
        {
            if (visualIndex >= 0 && visualIndex < _indexProxy.Count)
            {
                return _indexProxy[visualIndex];
            }

            return visualIndex;
        }

        private static void RebuildIndexProxy(List<int> indexProxy, SerializedProperty listProperty)
        {
            indexProxy.Clear();
            if (listProperty == null || !listProperty.isArray)
            {
                return;
            }

            for (int i = 0; i < listProperty.arraySize; i++)
            {
                indexProxy.Add(i);
            }
        }

        private static PhonemeEntryBase CreateEntry(EntryKind kind)
        {
            return kind == EntryKind.AnimationClip
                ? (PhonemeEntryBase)new AnimationClipPhonemeEntry()
                : new BlendShapePhonemeEntry();
        }

        private static void CopyCommonFields(PhonemeEntryBase source, PhonemeEntryBase destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.PhonemeId = source.PhonemeId;
            destination.MaxWeight = source.MaxWeight;
        }

        private static EntryKind ResolveEntryKind(SerializedProperty entryProperty)
        {
            object value = entryProperty.managedReferenceValue;
            if (value is AnimationClipPhonemeEntry)
            {
                return EntryKind.AnimationClip;
            }

            if (value is BlendShapePhonemeEntry)
            {
                return EntryKind.BlendShape;
            }

            string fullTypename = entryProperty.managedReferenceFullTypename ?? string.Empty;
            if (fullTypename.Contains(typeof(AnimationClipPhonemeEntry).FullName))
            {
                return EntryKind.AnimationClip;
            }

            return EntryKind.BlendShape;
        }

        private static string ToLabel(EntryKind kind)
        {
            return kind == EntryKind.AnimationClip ? AnimationClipLabel : BlendShapeLabel;
        }

        private static bool TryParseKind(string label, out EntryKind kind)
        {
            if (string.Equals(label, AnimationClipLabel, StringComparison.Ordinal))
            {
                kind = EntryKind.AnimationClip;
                return true;
            }

            if (string.Equals(label, BlendShapeLabel, StringComparison.Ordinal))
            {
                kind = EntryKind.BlendShape;
                return true;
            }

            kind = EntryKind.BlendShape;
            return false;
        }

        private static void ApplyBlendShapeWarningVisibility(HelpBox warning, string blendShapeName)
        {
            warning.style.display = string.IsNullOrEmpty(blendShapeName)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
