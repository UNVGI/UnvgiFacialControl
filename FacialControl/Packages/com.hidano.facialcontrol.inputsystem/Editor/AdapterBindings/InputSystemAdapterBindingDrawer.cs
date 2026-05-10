using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.AdapterBindings.InputSystem;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
// UnityEngine.UIElements にも BindingMode 型があるため明示エイリアスで衝突を避ける。
using BindingMode = Hidano.FacialControl.InputSystem.Adapters.ScriptableObject.BindingMode;

namespace Hidano.FacialControl.InputSystem.Editor.AdapterBindings
{
    /// <summary>
    /// <see cref="InputSystemAdapterBinding"/> の inline UI を提供する UI Toolkit ベースの
    /// <see cref="PropertyDrawer"/>（Req 3.1, 3.2, 3.3, 6.5, 7.4, 7.5, 11.4、design.md `## Adapter PropertyDrawers`）。
    /// </summary>
    /// <remarks>
    /// preview.1 の破壊的変更により、Trigger / Gaze / Analog の 3 リストを単一の
    /// 「キーバインディング」リスト (<c>_expressionBindings</c>) に統合し、各行で
    /// <see cref="BindingMode"/> を選択する形に集約した。
    /// <c>[CustomPropertyDrawer(typeof(InputSystemAdapterBinding))]</c> を付与し、core の
    /// <see cref="UnityEditor.UIElements.PropertyField"/> から自動解決される（Req 3.3）。
    /// </remarks>
    [CustomPropertyDrawer(typeof(InputSystemAdapterBinding))]
    public sealed class InputSystemAdapterBindingDrawer : PropertyDrawer
    {
        private const string SlugFieldName = "Slug";
        private const string InputActionAssetFieldName = "_inputActionAsset";
        private const string ActionMapNameFieldName = "_actionMapName";
        private const string ExpressionBindingsFieldName = "_expressionBindings";

        public const string RootClassName = "facial-control-input-system-adapter-binding";
        public const string ActionMapDropdownName = "input-system-binding-action-map-dropdown";
        public const string ExpressionBindingsListName = "input-system-binding-expression-bindings-list";
        public const string BindingModeFieldName = "input-system-binding-mode-field";
        public const string ActionDropdownName = "input-system-binding-action-dropdown";
        public const string ExpressionDropdownName = "input-system-binding-expression-dropdown";
        public const string TriggerModeFieldName = "input-system-binding-trigger-mode-field";

        // 1 行の高さ (BindingMode + Action + Expression + TriggerMode)
        private const float RowHeight = 116f;

        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList(RootClassName);

            AddBoundField(root, property, SlugFieldName, "Slug");

            var assetField = AddInputActionAssetField(root, property);
            var actionMapDropdown = AddActionMapDropdown(root, property);

            var expressionIndexProxy = new List<int>();
            var expressionBindingsList = AddExpressionBindingsList(
                root, property, expressionIndexProxy);

            assetField?.RegisterValueChangedCallback(_ =>
            {
                RefreshActionMapChoices(actionMapDropdown, property);
                ReassignItemsSource(expressionBindingsList, property, ExpressionBindingsFieldName, expressionIndexProxy);
            });
            actionMapDropdown?.RegisterValueChangedCallback(_ =>
            {
                ReassignItemsSource(expressionBindingsList, property, ExpressionBindingsFieldName, expressionIndexProxy);
            });

            return root;
        }

        /// <summary>
        /// SerializedProperty の現在の array 状態から index proxy を再構築し、
        /// ListView の <see cref="BaseVerticalCollectionView.itemsSource"/> に再代入したうえで rows を refresh する。
        /// </summary>
        private static void ReassignItemsSource(
            ListView listView,
            SerializedProperty bindingProperty,
            string arrayFieldName,
            List<int> indexProxy)
        {
            if (listView == null) return;

            var arr = bindingProperty.FindPropertyRelative(arrayFieldName);
            RebuildIndexProxy(indexProxy, arr);
            listView.itemsSource = indexProxy;
            listView.RefreshItems();
        }

        // ----------------------------------------------------------------
        // Sections
        // ----------------------------------------------------------------

        private static ObjectField AddInputActionAssetField(VisualElement root, SerializedProperty property)
        {
            var assetProp = property.FindPropertyRelative(InputActionAssetFieldName);
            if (assetProp == null)
            {
                root.Add(new Label($"<missing field: {InputActionAssetFieldName}>"));
                return null;
            }

            var field = new ObjectField("InputActionAsset")
            {
                objectType = typeof(InputActionAsset),
                allowSceneObjects = false,
            };
            field.BindProperty(assetProp);
            root.Add(field);
            return field;
        }

        private static DropdownField AddActionMapDropdown(VisualElement root, SerializedProperty property)
        {
            var mapNameProp = property.FindPropertyRelative(ActionMapNameFieldName);
            if (mapNameProp == null)
            {
                root.Add(new Label($"<missing field: {ActionMapNameFieldName}>"));
                return null;
            }

            var dropdown = new DropdownField("ActionMap 名")
            {
                name = ActionMapDropdownName,
            };
            RefreshActionMapChoices(dropdown, property);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var so = property.serializedObject;
                so.Update();
                var prop = property.FindPropertyRelative(ActionMapNameFieldName);
                if (prop != null)
                {
                    prop.stringValue = evt.newValue ?? string.Empty;
                    so.ApplyModifiedProperties();
                }
            });
            root.Add(dropdown);
            return dropdown;
        }

        private ListView AddExpressionBindingsList(
            VisualElement root, SerializedProperty property, List<int> indexProxy)
        {
            var listProp = property.FindPropertyRelative(ExpressionBindingsFieldName);
            if (listProp == null)
            {
                root.Add(new Label($"<missing field: {ExpressionBindingsFieldName}>"));
                return null;
            }

            RebuildIndexProxy(indexProxy, listProp);

            // NOTE: itemsSource は SetViewController の "後" に設定する。
            // UnityCsReference の BaseVerticalCollectionView.SetViewController は
            // 旧 controller を Dispose して新 controller を SetView する実装で、
            // ListView.itemsSource ゲッターは viewController.itemsSource を直接返すため、
            // 初期化子で itemsSource を渡すと差し替え後に null 化されてしまい、
            // Add ボタンで EnsureItemSourceCanBeResized が例外を投げる。
            var listView = new ListView
            {
                name = ExpressionBindingsListName,
                fixedItemHeight = RowHeight,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = true,
                headerTitle = "キーバインディング",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                makeItem = CreateExpressionBindingRow,
                bindItem = (element, index) => BindExpressionBindingRow(element, index, property),
                // unbindItem: 行が再利用される際に古い RegisterValueChangedCallback を持ち越さないように
                // 子要素を毎回破棄する。bindItem 側は element.Clear() 済み前提で組み立てる。
                unbindItem = (element, _) => element.Clear(),
            };
            listView.style.marginTop = 4;
            listView.style.minHeight = 120f;
            listView.SetViewController(new SafeListViewController());
            listView.itemsSource = indexProxy;

            listView.itemsAdded += indices =>
            {
                var so = property.serializedObject;
                so.Update();
                var arr = property.FindPropertyRelative(ExpressionBindingsFieldName);
                if (arr == null) return;
                int oldSize = arr.arraySize;
                int addCount = 0;
                foreach (var _ in indices) addCount++;
                arr.arraySize += addCount;
                // 新規エントリは Normal モード + 既定 Hold で初期化する (追加直後の意味のあるデフォルト)。
                for (int i = oldSize; i < arr.arraySize; i++)
                {
                    var entry = arr.GetArrayElementAtIndex(i);
                    var modeProp = entry.FindPropertyRelative("bindingMode");
                    if (modeProp != null) modeProp.enumValueIndex = (int)BindingMode.Normal;
                    var actionProp = entry.FindPropertyRelative("actionName");
                    if (actionProp != null) actionProp.stringValue = string.Empty;
                    var exprProp = entry.FindPropertyRelative("expressionId");
                    if (exprProp != null) exprProp.stringValue = string.Empty;
                    var triggerProp = entry.FindPropertyRelative("triggerMode");
                    if (triggerProp != null) triggerProp.enumValueIndex = (int)TriggerMode.Hold;
                }
                so.ApplyModifiedProperties();
                RebuildIndexProxy(indexProxy, arr);
                listView.ClearSelection();
                listView.Rebuild();
            };
            listView.itemsRemoved += indices =>
            {
                var so = property.serializedObject;
                so.Update();
                var arr = property.FindPropertyRelative(ExpressionBindingsFieldName);
                if (arr == null) return;
                var sorted = new List<int>(indices);
                sorted.Sort();
                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    var removeIndex = sorted[i];
                    if (removeIndex >= 0 && removeIndex < arr.arraySize)
                    {
                        arr.DeleteArrayElementAtIndex(removeIndex);
                    }
                }
                so.ApplyModifiedProperties();
                RebuildIndexProxy(indexProxy, arr);
                listView.ClearSelection();
                listView.Rebuild();
            };

            root.Add(listView);
            return listView;
        }

        private static void RebuildIndexProxy(List<int> indexProxy, SerializedProperty arrayProperty)
        {
            indexProxy.Clear();
            if (arrayProperty == null || !arrayProperty.isArray) return;
            for (int i = 0; i < arrayProperty.arraySize; i++) indexProxy.Add(i);
        }

        // ----------------------------------------------------------------
        // Expression bindings row
        // ----------------------------------------------------------------

        private static VisualElement CreateExpressionBindingRow()
        {
            // makeItem は空の container だけを返し、内部要素は BindExpressionBindingRow で毎回再構築する。
            // 旧実装のように子要素を makeItem で作って bindItem で再 bind する形だと、再 bind の度に
            // RegisterValueChangedCallback がスタックして同 evt で複数回 ApplyModifiedProperties が走り、
            // SerializedProperty / arraySize が想定外に変動する → 別 row の bindItem が範囲外 index で
            // GetArrayElementAtIndex を呼んで ArgumentOutOfRangeException、という連鎖を起こしていた。
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 4;
            return container;
        }

        private static void BindExpressionBindingRow(
            VisualElement element, int index, SerializedProperty bindingProperty)
        {
            // unbindItem だけでは itemsRemoved → bindItem 直行のケースで element に旧子要素が残る場合があるため、
            // 防御的に bindItem 先頭でも Clear する。これにより RegisterValueChangedCallback の二重登録を防ぐ。
            element.Clear();

            var listProp = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
            if (listProp == null || index < 0 || index >= listProp.arraySize)
            {
                // index が範囲外 (削除直後にレンダリングがズレた等) の場合は何も bind しない。
                return;
            }

            var serializedObject = bindingProperty.serializedObject;
            serializedObject.Update();

            var entryProp = listProp.GetArrayElementAtIndex(index);
            var bindingModeProp = entryProp.FindPropertyRelative("bindingMode");
            var actionNameProp = entryProp.FindPropertyRelative("actionName");
            var expressionIdProp = entryProp.FindPropertyRelative("expressionId");
            var triggerModeProp = entryProp.FindPropertyRelative("triggerMode");

            // BindingMode field
            var bindingModeField = new EnumField("動作モード", BindingMode.Normal)
            {
                name = BindingModeFieldName,
                tooltip = "Normal: 通常キー押下による Expression。"
                    + " Gaze: Vector2 入力で目線駆動。"
                    + " Analog: Scalar 連続値で expression weight を 0..1 駆動。",
            };
            if (bindingModeProp != null)
            {
                bindingModeField.BindProperty(bindingModeProp);
            }
            element.Add(bindingModeField);

            // Action dropdown
            var actionDropdown = new DropdownField("Action 名")
            {
                name = ActionDropdownName,
            };
            string actionNameValue = actionNameProp != null ? actionNameProp.stringValue ?? string.Empty : string.Empty;
            var actionChoices = CollectActionNames(bindingProperty);
            actionDropdown.choices = BuildSafeChoices(actionChoices, actionNameValue);
            actionDropdown.SetValueWithoutNotify(actionNameValue);
            actionDropdown.RegisterValueChangedCallback(evt =>
            {
                var so = bindingProperty.serializedObject;
                so.Update();
                var list = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
                if (list == null || index < 0 || index >= list.arraySize) return;
                var prop = list.GetArrayElementAtIndex(index).FindPropertyRelative("actionName");
                if (prop != null)
                {
                    prop.stringValue = evt.newValue ?? string.Empty;
                    so.ApplyModifiedProperties();
                }
            });
            element.Add(actionDropdown);

            // Expression dropdown
            var expressionDropdown = new DropdownField("表情 ID")
            {
                name = ExpressionDropdownName,
            };
            string expressionIdValue = expressionIdProp != null ? expressionIdProp.stringValue ?? string.Empty : string.Empty;
            var expressionChoices = CollectExpressionIds(bindingProperty);
            expressionDropdown.choices = BuildSafeChoices(expressionChoices, expressionIdValue);
            expressionDropdown.SetValueWithoutNotify(expressionIdValue);
            // 内部値は expressionId（hash 文字列）のままで保存し、表示だけ Expression.name に変換する。
            expressionDropdown.formatListItemCallback =
                id => FormatExpressionDropdownLabel(bindingProperty, id);
            expressionDropdown.formatSelectedValueCallback =
                id => FormatExpressionDropdownLabel(bindingProperty, id);
            expressionDropdown.RegisterValueChangedCallback(evt =>
            {
                var so = bindingProperty.serializedObject;
                so.Update();
                var list = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
                if (list == null || index < 0 || index >= list.arraySize) return;
                var prop = list.GetArrayElementAtIndex(index).FindPropertyRelative("expressionId");
                if (prop != null)
                {
                    prop.stringValue = evt.newValue ?? string.Empty;
                    so.ApplyModifiedProperties();
                }
            });
            element.Add(expressionDropdown);

            // TriggerMode field
            var triggerModeField = new EnumField("トリガモード", TriggerMode.Hold)
            {
                name = TriggerModeFieldName,
                tooltip = "Normal モード時のみ有効。"
                    + " Hold: 押している間だけ ON。Toggle: 押すたびに ON/OFF が切替わる。",
            };
            if (triggerModeProp != null)
            {
                triggerModeField.BindProperty(triggerModeProp);
            }
            BindingMode currentMode = bindingModeProp != null
                ? (BindingMode)bindingModeProp.enumValueIndex
                : BindingMode.Normal;
            UpdateTriggerModeVisibility(triggerModeField, currentMode);
            bindingModeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is BindingMode mode)
                {
                    UpdateTriggerModeVisibility(triggerModeField, mode);
                }
            });
            element.Add(triggerModeField);
        }

        private static void UpdateTriggerModeVisibility(EnumField triggerModeField, BindingMode mode)
        {
            if (triggerModeField == null) return;
            triggerModeField.style.display = mode == BindingMode.Normal
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static void AddBoundField(
            VisualElement root, SerializedProperty property, string relativePath, string label)
        {
            var child = property.FindPropertyRelative(relativePath);
            if (child == null)
            {
                // 該当フィールドが見つからない場合でも他フィールドの描画を止めない（Req 3.6 と同思想）。
                root.Add(new Label($"<missing field: {relativePath}>"));
                return;
            }

            var field = new PropertyField(child, label);
            root.Add(field);
        }

        private static void RefreshActionMapChoices(DropdownField dropdown, SerializedProperty property)
        {
            if (dropdown == null) return;
            var choices = new List<string>();

            var assetProp = property.FindPropertyRelative(InputActionAssetFieldName);
            var asset = assetProp != null ? assetProp.objectReferenceValue as InputActionAsset : null;
            if (asset != null)
            {
                foreach (var map in asset.actionMaps)
                {
                    if (!string.IsNullOrEmpty(map.name)) choices.Add(map.name);
                }
            }
            dropdown.choices = choices;

            var mapNameProp = property.FindPropertyRelative(ActionMapNameFieldName);
            var current = mapNameProp != null ? mapNameProp.stringValue : string.Empty;
            if (!string.IsNullOrEmpty(current) && choices.Contains(current))
            {
                dropdown.SetValueWithoutNotify(current);
            }
            else if (choices.Count > 0)
            {
                dropdown.SetValueWithoutNotify(choices[0]);
            }
            else
            {
                dropdown.SetValueWithoutNotify(string.Empty);
            }
        }

        private static List<string> CollectActionNames(SerializedProperty bindingProperty)
        {
            var result = new List<string>();
            var assetProp = bindingProperty.FindPropertyRelative(InputActionAssetFieldName);
            var asset = assetProp != null ? assetProp.objectReferenceValue as InputActionAsset : null;
            if (asset == null) return result;

            var mapNameProp = bindingProperty.FindPropertyRelative(ActionMapNameFieldName);
            var mapName = mapNameProp != null ? mapNameProp.stringValue : null;
            if (string.IsNullOrEmpty(mapName)) return result;

            var map = asset.FindActionMap(mapName);
            if (map == null) return result;

            foreach (var action in map.actions)
            {
                if (!string.IsNullOrEmpty(action.name)) result.Add(action.name);
            }
            return result;
        }

        private static List<string> CollectExpressionIds(SerializedProperty bindingProperty)
        {
            var result = new List<string>();
            var so = bindingProperty.serializedObject;
            if (!(so.targetObject is FacialCharacterProfileSO profileSo))
            {
                return result;
            }
            var expressions = profileSo.Expressions;
            if (expressions == null) return result;
            for (int i = 0; i < expressions.Count; i++)
            {
                var id = expressions[i] != null ? expressions[i].id : null;
                if (!string.IsNullOrEmpty(id)) result.Add(id);
            }
            return result;
        }

        /// <summary>
        /// dropdown の表示ラベルを「表情名 (なければ id 短縮)」に整形する。
        /// 内部に保存される値は expressionId（hash 文字列）のままで触らない。
        /// </summary>
        private static string FormatExpressionDropdownLabel(
            SerializedProperty bindingProperty, string expressionId)
        {
            if (string.IsNullOrEmpty(expressionId))
            {
                return string.Empty;
            }

            var so = bindingProperty.serializedObject;
            if (!(so.targetObject is FacialCharacterProfileSO profileSo))
            {
                return expressionId;
            }

            var expressions = profileSo.Expressions;
            if (expressions == null)
            {
                return expressionId;
            }

            for (int i = 0; i < expressions.Count; i++)
            {
                var e = expressions[i];
                if (e == null) continue;
                if (!string.Equals(e.id, expressionId, StringComparison.Ordinal)) continue;

                if (!string.IsNullOrEmpty(e.name))
                {
                    return e.name;
                }
                break;
            }

            // 名前が空 / 候補から外れた id は判別性のため短縮表示。
            return expressionId.Length <= 8 ? expressionId : expressionId.Substring(0, 8) + "…";
        }

        private static List<string> BuildSafeChoices(IReadOnlyList<string> baseChoices, string currentValue)
        {
            var result = new List<string>(baseChoices.Count + 2) { string.Empty };
            for (int i = 0; i < baseChoices.Count; i++)
            {
                if (!result.Contains(baseChoices[i])) result.Add(baseChoices[i]);
            }
            if (!string.IsNullOrEmpty(currentValue) && !result.Contains(currentValue))
            {
                result.Add(currentValue);
            }
            return result;
        }
    }
}
