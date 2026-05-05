using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.AdapterBindings.InputSystem;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.InputSystem.Editor.AdapterBindings
{
    /// <summary>
    /// <see cref="InputSystemAdapterBinding"/> の inline UI を提供する UI Toolkit ベースの
    /// <see cref="PropertyDrawer"/>（Req 3.1, 3.2, 3.3, 6.5, 7.4, 7.5, 11.4、design.md `## Adapter PropertyDrawers`）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[CustomPropertyDrawer(typeof(InputSystemAdapterBinding))]</c> を付与し、core の
    /// <see cref="UnityEditor.UIElements.PropertyField"/> から自動解決される（Req 3.3）。
    /// core パッケージ側で本 drawer を登録しないこと（Req 3.1）。
    /// </para>
    /// <para>
    /// 編集対象 UI:
    /// <list type="bullet">
    /// <item><description>Slug（<c>AdapterBindingBase</c> の public field）</description></item>
    /// <item><description>InputActionAsset（<c>_inputActionAsset</c>）</description></item>
    /// <item><description>ActionMap 名（<c>_actionMapName</c>、Asset の ActionMap 一覧から候補補完）</description></item>
    /// <item><description>Expression Bindings リスト（<c>_expressionBindings</c>、Action 名・Expression ID・TriggerMode の 3 列）</description></item>
    /// <item><description>Gaze Configs リスト（<c>_gazeConfigs</c>、Unity デフォルトの <see cref="UnityEditor.UIElements.PropertyField"/> 描画）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 新規 IMGUI panel は導入しない（Req 11.4）。
    /// </para>
    /// </remarks>
    [CustomPropertyDrawer(typeof(InputSystemAdapterBinding))]
    public sealed class InputSystemAdapterBindingDrawer : PropertyDrawer
    {
        private const string SlugFieldName = "Slug";
        private const string InputActionAssetFieldName = "_inputActionAsset";
        private const string ActionMapNameFieldName = "_actionMapName";
        private const string ExpressionBindingsFieldName = "_expressionBindings";
        private const string GazeConfigsFieldName = "_gazeConfigs";

        public const string RootClassName = "facial-control-input-system-adapter-binding";
        public const string ActionMapDropdownName = "input-system-binding-action-map-dropdown";
        public const string ExpressionBindingsListName = "input-system-binding-expression-bindings-list";
        public const string ActionDropdownName = "input-system-binding-action-dropdown";
        public const string ExpressionDropdownName = "input-system-binding-expression-dropdown";
        public const string TriggerModeFieldName = "input-system-binding-trigger-mode-field";

        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList(RootClassName);

            AddBoundField(root, property, SlugFieldName, "Slug");

            var assetField = AddInputActionAssetField(root, property);
            var actionMapDropdown = AddActionMapDropdown(root, property);
            var expressionBindingsList = AddExpressionBindingsList(root, property);

            // _inputActionAsset 変更時に ActionMap / Action 候補と list rows を再構築する。
            assetField?.RegisterValueChangedCallback(_ =>
            {
                RefreshActionMapChoices(actionMapDropdown, property);
                expressionBindingsList?.Rebuild();
            });
            actionMapDropdown?.RegisterValueChangedCallback(_ =>
            {
                expressionBindingsList?.Rebuild();
            });

            AddBoundField(root, property, GazeConfigsFieldName, "Gaze Configs");

            return root;
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

        private ListView AddExpressionBindingsList(VisualElement root, SerializedProperty property)
        {
            var listProp = property.FindPropertyRelative(ExpressionBindingsFieldName);
            if (listProp == null)
            {
                root.Add(new Label($"<missing field: {ExpressionBindingsFieldName}>"));
                return null;
            }

            var indexProxy = new List<int>();
            RebuildIndexProxy(indexProxy, listProp);

            var listView = new ListView
            {
                name = ExpressionBindingsListName,
                fixedItemHeight = 84f,
                itemsSource = indexProxy,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = true,
                headerTitle = "キーバインディング",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                makeItem = CreateExpressionBindingRow,
                bindItem = (element, index) => BindExpressionBindingRow(element, index, property),
            };
            listView.style.marginTop = 4;
            listView.style.minHeight = 80f;

            listView.itemsAdded += indices =>
            {
                var so = property.serializedObject;
                so.Update();
                var arr = property.FindPropertyRelative(ExpressionBindingsFieldName);
                if (arr == null) return;
                int addCount = 0;
                foreach (var _ in indices) addCount++;
                arr.arraySize += addCount;
                so.ApplyModifiedProperties();
                RebuildIndexProxy(indexProxy, arr);
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
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 4;

            container.Add(new DropdownField("Action 名")
            {
                name = ActionDropdownName,
            });
            container.Add(new DropdownField("表情 ID")
            {
                name = ExpressionDropdownName,
            });
            container.Add(new EnumField("動作モード", TriggerMode.Hold)
            {
                name = TriggerModeFieldName,
                tooltip = "Hold: 押している間だけ ON。Toggle: 押すたびに ON/OFF が切替わる。",
            });
            return container;
        }

        private static void BindExpressionBindingRow(
            VisualElement element, int index, SerializedProperty bindingProperty)
        {
            var listProp = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
            if (listProp == null || index < 0 || index >= listProp.arraySize)
            {
                return;
            }

            var serializedObject = bindingProperty.serializedObject;
            serializedObject.Update();

            var entryProp = listProp.GetArrayElementAtIndex(index);
            var actionNameProp = entryProp.FindPropertyRelative("actionName");
            var expressionIdProp = entryProp.FindPropertyRelative("expressionId");
            var triggerModeProp = entryProp.FindPropertyRelative("triggerMode");

            var actionDropdown = element.Q<DropdownField>(ActionDropdownName);
            if (actionDropdown != null && actionNameProp != null)
            {
                var actionChoices = CollectActionNames(bindingProperty);
                var safeChoices = BuildSafeChoices(actionChoices, actionNameProp.stringValue);
                actionDropdown.choices = safeChoices;
                actionDropdown.SetValueWithoutNotify(actionNameProp.stringValue ?? string.Empty);
                actionDropdown.RegisterValueChangedCallback(evt =>
                {
                    var so = bindingProperty.serializedObject;
                    so.Update();
                    var list = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
                    if (list == null || index >= list.arraySize) return;
                    var prop = list.GetArrayElementAtIndex(index).FindPropertyRelative("actionName");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        so.ApplyModifiedProperties();
                    }
                });
            }

            var expressionDropdown = element.Q<DropdownField>(ExpressionDropdownName);
            if (expressionDropdown != null && expressionIdProp != null)
            {
                var expressionChoices = CollectExpressionIds(bindingProperty);
                var safeChoices = BuildSafeChoices(expressionChoices, expressionIdProp.stringValue);
                expressionDropdown.choices = safeChoices;
                expressionDropdown.SetValueWithoutNotify(expressionIdProp.stringValue ?? string.Empty);
                expressionDropdown.RegisterValueChangedCallback(evt =>
                {
                    var so = bindingProperty.serializedObject;
                    so.Update();
                    var list = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
                    if (list == null || index >= list.arraySize) return;
                    var prop = list.GetArrayElementAtIndex(index).FindPropertyRelative("expressionId");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        so.ApplyModifiedProperties();
                    }
                });
            }

            var triggerModeField = element.Q<EnumField>(TriggerModeFieldName);
            if (triggerModeField != null && triggerModeProp != null)
            {
                var current = (TriggerMode)triggerModeProp.enumValueIndex;
                triggerModeField.SetValueWithoutNotify(current);
                triggerModeField.RegisterValueChangedCallback(evt =>
                {
                    if (!(evt.newValue is TriggerMode mode)) return;
                    var so = bindingProperty.serializedObject;
                    so.Update();
                    var list = bindingProperty.FindPropertyRelative(ExpressionBindingsFieldName);
                    if (list == null || index >= list.arraySize) return;
                    var prop = list.GetArrayElementAtIndex(index).FindPropertyRelative("triggerMode");
                    if (prop != null)
                    {
                        prop.enumValueIndex = (int)mode;
                        so.ApplyModifiedProperties();
                    }
                });
            }
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
