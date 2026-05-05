using System;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Inspector.AdapterBindings
{
    /// <summary>
    /// <see cref="FacialCharacterProfileSO._adapterBindings"/>（<c>[SerializeReference]</c>
    /// <see cref="System.Collections.Generic.List{T}"/> of <see cref="AdapterBindingBase"/>）を
    /// 編集する UI Toolkit reorderable list。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Req 1.4 / 2.4-2.7 / 3.3 / 3.5 / 3.6 / 7.1 / 12.2 / 12.3 を満たす。Add ドロップダウン →
    /// <see cref="System.Activator.CreateInstance(System.Type)"/> + slug auto-populate、
    /// Remove で dirty 化、Reorder、null 要素の <see cref="MissingAdapterPlaceholderElement"/>、
    /// 同 SO 内 slug 重複時の error class + summary banner、PropertyDrawer 例外時の
    /// per-element fallback element を提供する。
    /// </para>
    /// <para>
    /// 通常時は <see cref="UnityEditor.UIElements.PropertyField"/> を委譲し、Adapter package が
    /// 提供する <c>[CustomPropertyDrawer]</c> を core 側で参照せずに自動解決する（Req 3.1, 3.3）。
    /// PropertyDrawer の <see cref="UnityEditor.PropertyDrawer.CreatePropertyGUI"/> 例外は
    /// 行単位の fallback element で吸収し、他 row の描画継続を保証する（Req 3.6）。
    /// </para>
    /// </remarks>
    public sealed class AdapterBindingsListView : VisualElement
    {
        public const string ErrorRowClassName = "facial-control-error";
        public const string FallbackRowClassName = "facial-control-fallback-row";
        public const string SummaryBannerName = "facial-control-adapter-bindings-summary-banner";
        public const string RowClassName = "facial-control-adapter-binding-row";
        public const string RootClassName = "facial-control-adapter-bindings-list-view";

        private const string AdapterBindingsFieldName = "_adapterBindings";

        private readonly SerializedProperty _listProperty;
        private readonly VisualElement _summaryBanner;
        private readonly VisualElement _rowsContainer;
        private readonly ListView _listView;
        private AdvancedDropdownState _addDropdownState;

        public AdapterBindingsListView(SerializedProperty listProperty)
        {
            _listProperty = listProperty ?? throw new ArgumentNullException(nameof(listProperty));
            if (!_listProperty.isArray)
            {
                throw new ArgumentException(
                    "listProperty must be an array property bound to _adapterBindings.",
                    nameof(listProperty));
            }

            AddToClassList(RootClassName);

            _summaryBanner = new VisualElement { name = SummaryBannerName };
            _summaryBanner.style.display = DisplayStyle.None;
            Add(_summaryBanner);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.Add(new Label("Adapter Bindings"));
            var addButton = new Button(OpenAddDropdown) { text = "+ Add" };
            header.Add(addButton);
            Add(header);

            _rowsContainer = new VisualElement { name = "facial-control-adapter-bindings-rows" };
            Add(_rowsContainer);

            _listView = new ListView
            {
                bindingPath = AdapterBindingsFieldName,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showAddRemoveFooter = false,
                makeItem = () => new VisualElement(),
                bindItem = (element, index) => { },
            };
            _listView.itemIndexChanged += OnListViewIndexChanged;
            _listView.style.display = DisplayStyle.None;
            Add(_listView);

            Rebuild();
        }

        // ------------------------------------------------------------------
        // Public API（テスト + Inspector 統合（task 5.6）から呼び出される）
        // ------------------------------------------------------------------

        /// <summary>
        /// <see cref="AdapterBindingDiscovery"/> 経由で取得した descriptor から具象を生成し
        /// list に追加する。slug は <see cref="AdapterSlug.FromDisplayName(string)"/> で
        /// auto-populate される（Req 2.5, 12.2）。
        /// </summary>
        public void AddBindingFromDescriptor(AdapterBindingDescriptor descriptor)
        {
            if (descriptor.Type == null)
            {
                throw new ArgumentException("descriptor.Type must be non-null.", nameof(descriptor));
            }

            var instance = (AdapterBindingBase)Activator.CreateInstance(descriptor.Type);
            instance.Slug = AdapterSlug.FromDisplayName(descriptor.OriginalDisplayName).Value;

            var list = ResolveUnderlyingList();
            if (list == null) return;
            list.Add(instance);

            CommitMutation();
        }

        /// <summary>
        /// 指定 index の binding を list から削除し、SO を dirty 化する（Req 2.6）。
        /// </summary>
        public void RemoveBindingAt(int index)
        {
            var list = ResolveUnderlyingList();
            if (list == null) return;
            if (index < 0 || index >= list.Count) return;

            list.RemoveAt(index);
            CommitMutation();
        }

        /// <summary>
        /// <paramref name="fromIndex"/> の要素を <paramref name="toIndex"/> に移動する（Reorder）。
        /// </summary>
        public void MoveBinding(int fromIndex, int toIndex)
        {
            var list = ResolveUnderlyingList();
            if (list == null) return;
            if (fromIndex < 0 || fromIndex >= list.Count) return;

            if (toIndex < 0) toIndex = 0;
            if (toIndex >= list.Count) toIndex = list.Count - 1;
            if (fromIndex == toIndex) return;

            var item = list[fromIndex];
            list.RemoveAt(fromIndex);

            int insertIndex = toIndex;
            if (insertIndex > list.Count) insertIndex = list.Count;
            list.Insert(insertIndex, item);

            CommitMutation();
        }

        // ------------------------------------------------------------------
        // 内部: rebuild / row 構築 / duplicate 検出
        // ------------------------------------------------------------------

        private void Rebuild()
        {
            _rowsContainer.Clear();

            int count = _listProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                var row = BuildRow(i);
                _rowsContainer.Add(row);
            }

            ApplyDuplicateSlugMarkers();
        }

        private VisualElement BuildRow(int index)
        {
            var row = new VisualElement { name = $"adapter-binding-row-{index}" };
            row.AddToClassList(RowClassName);

            var prop = _listProperty.GetArrayElementAtIndex(index);
            object value = prop.managedReferenceValue;

            if (value == null)
            {
                int capturedIndex = index;
                var placeholder = new MissingAdapterPlaceholderElement(
                    prop.managedReferenceFullTypename,
                    () => RemoveBindingAt(capturedIndex));
                row.Add(placeholder);
                return row;
            }

            var bindingType = value.GetType();
            var drawer = TryCreateCustomDrawer(bindingType);

            if (drawer != null)
            {
                try
                {
                    var element = drawer.CreatePropertyGUI(prop);
                    if (element == null)
                    {
                        row.Add(new PropertyField(prop));
                    }
                    else
                    {
                        row.Add(element);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[FacialControl] PropertyDrawer for '{bindingType.FullName}' threw {ex.GetType().Name}: " +
                        $"{ex.Message}. A fallback element is shown for this row (Req 3.6).");
                    row.Add(BuildFallbackElement(bindingType, index));
                    return row;
                }
            }
            else
            {
                row.Add(new PropertyField(prop));
            }

            int rowIndexForRemove = index;
            row.Add(new Button(() => RemoveBindingAt(rowIndexForRemove)) { text = "Remove" });

            return row;
        }

        private VisualElement BuildFallbackElement(Type bindingType, int index)
        {
            var fallback = new VisualElement { name = $"adapter-binding-fallback-{index}" };
            fallback.AddToClassList(FallbackRowClassName);
            fallback.Add(new Label(
                $"PropertyDrawer threw for '{bindingType?.FullName ?? "<unknown>"}'. See console for details."));
            int capturedIndex = index;
            fallback.Add(new Button(() => RemoveBindingAt(capturedIndex)) { text = "Remove" });
            return fallback;
        }

        private void ApplyDuplicateSlugMarkers()
        {
            var list = ResolveUnderlyingList();
            if (list == null)
            {
                _summaryBanner.Clear();
                _summaryBanner.style.display = DisplayStyle.None;
                return;
            }

            var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null || string.IsNullOrEmpty(item.Slug)) continue;
                slugCounts.TryGetValue(item.Slug, out int count);
                slugCounts[item.Slug] = count + 1;
            }

            bool anyDuplicate = false;
            foreach (var c in slugCounts.Values)
            {
                if (c > 1) { anyDuplicate = true; break; }
            }

            if (anyDuplicate)
            {
                _summaryBanner.Clear();
                _summaryBanner.Add(new Label(
                    "[FacialControl] Duplicate adapter binding slugs detected. Save will be blocked until resolved (Req 12.3)."));
                _summaryBanner.style.display = DisplayStyle.Flex;
            }
            else
            {
                _summaryBanner.Clear();
                _summaryBanner.style.display = DisplayStyle.None;
            }

            int rowCount = _rowsContainer.childCount;
            for (int i = 0; i < list.Count && i < rowCount; i++)
            {
                var item = list[i];
                bool isDup = item != null
                    && !string.IsNullOrEmpty(item.Slug)
                    && slugCounts.TryGetValue(item.Slug, out int c2)
                    && c2 > 1;

                var row = _rowsContainer[i];
                if (isDup)
                {
                    if (!row.ClassListContains(ErrorRowClassName))
                    {
                        row.AddToClassList(ErrorRowClassName);
                    }
                }
                else
                {
                    row.RemoveFromClassList(ErrorRowClassName);
                }
            }
        }

        // ------------------------------------------------------------------
        // SO 同期ヘルパー
        // ------------------------------------------------------------------

        private void CommitMutation()
        {
            var so = _listProperty.serializedObject;
            if (so.targetObject != null)
            {
                EditorUtility.SetDirty(so.targetObject);
            }
            so.Update();
            Rebuild();
        }

        private List<AdapterBindingBase> ResolveUnderlyingList()
        {
            var target = _listProperty.serializedObject.targetObject;
            if (target == null) return null;

            var t = target.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(
                    AdapterBindingsFieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field.GetValue(target) as List<AdapterBindingBase>;
                }
                t = t.BaseType;
            }
            return null;
        }

        private void OnListViewIndexChanged(int fromIndex, int toIndex)
        {
            // ListView 由来の reorder を underlying list に反映してから rebuild する。
            // ListView は既に itemsSource を並び替えた後にコールバックするため、
            // ここでは MoveBinding を再呼び出さず CommitMutation のみ行う。
            CommitMutation();
        }

        private void OpenAddDropdown()
        {
            if (_addDropdownState == null)
            {
                _addDropdownState = new AdvancedDropdownState();
            }
            var descriptors = AdapterBindingDiscovery.GetDescriptors();
            var dropdown = new AdapterBindingAddDropdown(_addDropdownState, descriptors, AddBindingFromDescriptor);
            dropdown.Show(worldBound);
        }

        // ------------------------------------------------------------------
        // PropertyDrawer 解決（reflection で CustomPropertyDrawer の m_Type を読む）
        // ------------------------------------------------------------------

        private static FieldInfo _customPropertyDrawerTargetTypeField;

        private static PropertyDrawer TryCreateCustomDrawer(Type targetType)
        {
            if (targetType == null) return null;

            var typeField = _customPropertyDrawerTargetTypeField ??=
                typeof(CustomPropertyDrawer).GetField(
                    "m_Type",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (typeField == null) return null;

            var drawerTypes = TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>();
            for (int i = 0; i < drawerTypes.Count; i++)
            {
                var drawerType = drawerTypes[i];
                if (drawerType == null) continue;
                if (drawerType.IsAbstract) continue;
                if (drawerType.IsGenericTypeDefinition) continue;
                if (!typeof(PropertyDrawer).IsAssignableFrom(drawerType)) continue;

                var attrs = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), inherit: true);
                for (int j = 0; j < attrs.Length; j++)
                {
                    if (!(attrs[j] is CustomPropertyDrawer cpd)) continue;
                    var t = typeField.GetValue(cpd) as Type;
                    if (t == targetType)
                    {
                        try
                        {
                            return (PropertyDrawer)Activator.CreateInstance(drawerType);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }
            return null;
        }
    }
}
