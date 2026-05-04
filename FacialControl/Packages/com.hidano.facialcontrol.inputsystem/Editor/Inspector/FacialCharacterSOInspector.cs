using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Inspector;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem.Editor.AutoExport;

namespace Hidano.FacialControl.InputSystem.Editor.Inspector
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> 用の UI Toolkit カスタム Inspector。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 汎用 UI（Layers / Expressions / 自動保存 / Reference Model / Debug）は
    /// core パッケージの <see cref="FacialCharacterProfileSOInspector"/> に集約され、
    /// 本クラスは InputSystem 固有の UI（InputActionAsset 選択、Expression Bindings、
    /// Gaze の <see cref="InputActionReference"/> フィールド）と
    /// <c>analog_bindings.json</c> の追加エクスポートのみを担う。
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterSO))]
    public class FacialCharacterSOInspector : FacialCharacterProfileSOInspector
    {
        // ====================================================================
        // VisualElement の name 定数（InputSystem 固有部分）
        // ====================================================================

        public const string InputFoldoutName = "facial-character-input-foldout";
        public const string ExpressionBindingsFoldoutName = "facial-character-expression-bindings-foldout";

        public const string ExpressionRowGazeActionFieldName = "expression-row-gaze-action-field";

        // ====================================================================
        // SerializedProperty（InputSystem 固有部分）
        // ====================================================================

        private SerializedProperty _inputActionAssetProperty;
        private SerializedProperty _actionMapNameProperty;
        private SerializedProperty _expressionBindingsProperty;

        // ====================================================================
        // VisualElement キャッシュ
        // ====================================================================

        private DropdownField _actionMapDropdown;
        private ListView _expressionBindingsListView;

        // ====================================================================
        // 候補リスト
        // ====================================================================

        private readonly List<string> _actionNameChoices = new List<string>();

        // ====================================================================
        // Editor lifecycle
        // ====================================================================

        private static readonly List<FacialCharacterSOInspector> _activeInspectors =
            new List<FacialCharacterSOInspector>();

        private void OnEnable()
        {
            if (!_activeInspectors.Contains(this))
            {
                _activeInspectors.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeInspectors.Remove(this);
        }

        private string GetCurrentInputActionAssetPath()
        {
            if (_inputActionAssetProperty == null) return null;
            var asset = _inputActionAssetProperty.objectReferenceValue;
            return asset == null ? null : AssetDatabase.GetAssetPath(asset);
        }

        // ====================================================================
        // 派生 hook 実装
        // ====================================================================

        protected override void OnResolveDerivedSerializedProperties()
        {
            _inputActionAssetProperty = serializedObject.FindProperty("_inputActionAsset");
            _actionMapNameProperty = serializedObject.FindProperty("_actionMapName");
            _expressionBindingsProperty = serializedObject.FindProperty("_expressionBindings");
        }

        protected override SerializedProperty FindGazeConfigsProperty()
        {
            return serializedObject.FindProperty("_gazeConfigs");
        }

        protected override IReadOnlyList<string> ResolveAnalogSourceIdChoices()
        {
            return _actionNameChoices;
        }

        protected override void OnBuildPreLayersSections(VisualElement root)
        {
            BuildInputSection(root);
            BuildExpressionBindingsSection(root);
            RefreshActionMapChoices();
            RefreshActionNameChoices();
        }

        protected override void OnBuildAnalogExpressionInputSourceFields(
            VisualElement row, int exprIndex, SerializedProperty gazeConfigProperty)
        {
            if (gazeConfigProperty == null) return;
            var actionProp = gazeConfigProperty.FindPropertyRelative("inputAction");
            if (actionProp == null) return;

            var actionField = new ObjectField("InputAction (Vector2)")
            {
                name = ExpressionRowGazeActionFieldName,
                objectType = typeof(InputActionReference),
                allowSceneObjects = false,
            };
            actionField.BindProperty(actionProp);
            row.Add(actionField);
        }

        protected override IReadOnlyList<string> ValidateAnalogExpression(SerializedProperty gazeConfigProperty)
        {
            if (gazeConfigProperty == null) return Array.Empty<string>();
            var actionProp = gazeConfigProperty.FindPropertyRelative("inputAction");
            if (actionProp == null || actionProp.objectReferenceValue == null)
            {
                return new[] { "InputAction (Vector2) が未割り当てです。" };
            }
            return Array.Empty<string>();
        }

        protected override void FlushAutoExport(
            FacialCharacterProfileSO so, IExpressionAnimationClipSampler sampler)
        {
            // core 側の汎用処理 (profile.json + AnimationClip サンプリング) を実行。
            base.FlushAutoExport(so, sampler);

            // InputSystem 固有: Gaze 4 系統 clip サンプリング + analog_bindings.json 出力。
            FacialCharacterSOAutoExporter.SampleGazeClipsIntoConfigs(so);
            FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);
        }

        // ====================================================================
        // Section: 入力
        // ====================================================================

        private void BuildInputSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(InputFoldoutName, "入力", open: true);

            foldout.Add(MakeHelpBox(
                "ここに割り当てた .inputactions の Action 名を、表情のキー操作にバインドします。"
                + "InputActionAsset を変更すると、下のキーバインディング欄の候補が即時更新されます。"));

            if (_inputActionAssetProperty != null)
            {
                var assetField = new ObjectField("InputActionAsset")
                {
                    objectType = typeof(InputActionAsset),
                    allowSceneObjects = false,
                };
                assetField.BindProperty(_inputActionAssetProperty);
                assetField.RegisterValueChangedCallback(_ =>
                {
                    RefreshActionMapChoices();
                    RefreshActionNameChoices();
                    RebuildExpressionBindingsListView();
                });
                foldout.Add(assetField);
            }

            if (_actionMapNameProperty != null)
            {
                _actionMapDropdown = new DropdownField("ActionMap 名");
                _actionMapDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_actionMapNameProperty == null) return;
                    serializedObject.Update();
                    _actionMapNameProperty.stringValue = evt.newValue ?? string.Empty;
                    serializedObject.ApplyModifiedProperties();
                    RefreshActionNameChoices();
                    RebuildExpressionBindingsListView();
                });
                foldout.Add(_actionMapDropdown);
            }

            root.Add(foldout);
        }

        // ====================================================================
        // Section: キーバインディング
        // ====================================================================

        private void BuildExpressionBindingsSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(ExpressionBindingsFoldoutName, "キーバインディング", open: true);

            foldout.Add(MakeHelpBox(
                "Action 名と表情 ID を 1 対 1 で結びつけます。"
                + "Keyboard / Controller の振分けは Action の binding path から自動推定されます。"));

            if (_expressionBindingsProperty != null)
            {
                _expressionBindingsListView = BuildArrayListView(
                    _expressionBindingsProperty,
                    itemHeight: 84f,
                    makeItem: () => CreateExpressionBindingRow(),
                    bindItem: (element, index) => BindExpressionBindingRow(element, index));
                foldout.Add(_expressionBindingsListView);
            }

            root.Add(foldout);
        }

        private VisualElement CreateExpressionBindingRow()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 4;

            var actionDropdown = new DropdownField("Action 名")
            {
                name = "actionDropdown",
            };
            container.Add(actionDropdown);

            var expressionDropdown = new DropdownField("表情 ID")
            {
                name = "expressionDropdown",
            };
            container.Add(expressionDropdown);

            var triggerModeField = new EnumField("動作モード", TriggerMode.Hold)
            {
                name = "triggerModeField",
                tooltip = "Hold: 押している間だけ ON。Toggle: 押すたびに ON/OFF が切替わる。",
            };
            container.Add(triggerModeField);

            return container;
        }

        private void BindExpressionBindingRow(VisualElement element, int index)
        {
            if (_expressionBindingsProperty == null
                || index < 0
                || index >= _expressionBindingsProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            var entryProp = _expressionBindingsProperty.GetArrayElementAtIndex(index);
            var actionNameProp = entryProp.FindPropertyRelative("actionName");
            var expressionIdProp = entryProp.FindPropertyRelative("expressionId");
            var triggerModeProp = entryProp.FindPropertyRelative("triggerMode");

            var actionDropdown = element.Q<DropdownField>("actionDropdown");
            if (actionDropdown != null && actionNameProp != null)
            {
                var choices = BuildSafeChoices(_actionNameChoices, actionNameProp.stringValue);
                actionDropdown.choices = choices;
                actionDropdown.SetValueWithoutNotify(actionNameProp.stringValue ?? string.Empty);
                actionDropdown.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("actionName");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            var expressionDropdown = element.Q<DropdownField>("expressionDropdown");
            if (expressionDropdown != null && expressionIdProp != null)
            {
                var ids = CollectExpressionIds();
                var choices = BuildSafeChoices(ids, expressionIdProp.stringValue);
                expressionDropdown.choices = choices;
                expressionDropdown.SetValueWithoutNotify(expressionIdProp.stringValue ?? string.Empty);
                expressionDropdown.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("expressionId");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            var triggerModeField = element.Q<EnumField>("triggerModeField");
            if (triggerModeField != null && triggerModeProp != null)
            {
                var current = (TriggerMode)triggerModeProp.enumValueIndex;
                triggerModeField.SetValueWithoutNotify(current);
                triggerModeField.RegisterValueChangedCallback(evt =>
                {
                    if (!(evt.newValue is TriggerMode mode))
                    {
                        return;
                    }
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("triggerMode");
                    if (prop != null)
                    {
                        prop.enumValueIndex = (int)mode;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }
        }

        private void RebuildExpressionBindingsListView()
        {
            _expressionBindingsListView?.Rebuild();
        }

        // ====================================================================
        // 候補キャッシュ更新
        // ====================================================================

        private void RefreshActionMapChoices()
        {
            if (_actionMapDropdown == null || _inputActionAssetProperty == null) return;
            var asset = _inputActionAssetProperty.objectReferenceValue as InputActionAsset;
            var choices = new List<string>();
            if (asset != null)
            {
                foreach (var map in asset.actionMaps)
                {
                    if (!string.IsNullOrEmpty(map.name)) choices.Add(map.name);
                }
            }
            _actionMapDropdown.choices = choices;

            var current = _actionMapNameProperty != null ? _actionMapNameProperty.stringValue : string.Empty;
            if (!string.IsNullOrEmpty(current) && choices.Contains(current))
            {
                _actionMapDropdown.SetValueWithoutNotify(current);
            }
            else if (choices.Count > 0)
            {
                _actionMapDropdown.SetValueWithoutNotify(choices[0]);
            }
            else
            {
                _actionMapDropdown.SetValueWithoutNotify(string.Empty);
            }
        }

        private void RefreshActionNameChoices()
        {
            _actionNameChoices.Clear();
            if (_inputActionAssetProperty == null || _actionMapNameProperty == null) return;
            var asset = _inputActionAssetProperty.objectReferenceValue as InputActionAsset;
            if (asset == null) return;
            var mapName = _actionMapNameProperty.stringValue;
            if (string.IsNullOrEmpty(mapName)) return;
            var map = asset.FindActionMap(mapName);
            if (map == null) return;
            foreach (var action in map.actions)
            {
                if (!string.IsNullOrEmpty(action.name)) _actionNameChoices.Add(action.name);
            }
        }

        // ====================================================================
        // AssetPostprocessor: InputActionAsset 編集追従
        // ====================================================================

        private void RefreshActionChoicesFromExternalEdit()
        {
            try { serializedObject.Update(); } catch (Exception) { return; }
            RefreshActionMapChoices();
            RefreshActionNameChoices();
            RebuildExpressionBindingsListView();
        }

        private sealed class InputActionAssetChangeWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (_activeInspectors.Count == 0) return;
                if (importedAssets == null || importedAssets.Length == 0) return;

                var importedInputActionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    var path = importedAssets[i];
                    if (string.IsNullOrEmpty(path)) continue;
                    if (path.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                    {
                        importedInputActionPaths.Add(path);
                    }
                }
                if (importedInputActionPaths.Count == 0) return;

                var snapshot = new List<FacialCharacterSOInspector>(_activeInspectors);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var inspector = snapshot[i];
                    if (inspector == null) continue;
                    var currentPath = inspector.GetCurrentInputActionAssetPath();
                    if (string.IsNullOrEmpty(currentPath)) continue;
                    if (importedInputActionPaths.Contains(currentPath))
                    {
                        inspector.RefreshActionChoicesFromExternalEdit();
                    }
                }
            }
        }
    }
}
