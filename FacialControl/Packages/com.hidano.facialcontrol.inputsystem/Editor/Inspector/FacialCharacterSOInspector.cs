using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.InputSystem.Editor.Inspector
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> 用の UI Toolkit カスタム Inspector（Phase 5.1 全面改修版）。
    /// AnimationClip を Source of Truth とする新 UX を提供する。
    /// </summary>
    /// <remarks>
    /// <para>Phase 5.1 (inspector-and-data-model-redesign) における主な変更点:</para>
    /// <list type="bullet">
    ///   <item>旧 BonePose / RendererPath 手入力 UI を物理削除（Req 4.1, 5.1）</item>
    ///   <item>旧 ExpressionBindingEntry.Category UI を物理削除（Req 7.1）</item>
    ///   <item>Expression 行に AnimationClip ObjectField を追加（Req 1.1）</item>
    ///   <item>Layer DropdownField + LayerOverrideMask MaskField を追加（Req 1.4, 3.3）</item>
    ///   <item>read-only RendererPath summary view を AnimationClip から派生（Req 4.3, 4.5）</item>
    ///   <item>validation エラー時の HelpBox + Save ボタン disabled（Req 1.6, 1.7, 3.5）</item>
    ///   <item>新規 Expression 作成時に GUID で Id を自動採番（Req 1.3）</item>
    ///   <item>AnimationClip 名から Name を派生（Req 1.2）</item>
    /// </list>
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterSO))]
    public sealed class FacialCharacterSOInspector : UnityEditor.Editor
    {
        // ====================================================================
        // VisualElement の name 定数 (テストの Q<>() と一致させる)
        // ====================================================================

        public const string InputFoldoutName = "facial-character-input-foldout";
        public const string ExpressionBindingsFoldoutName = "facial-character-expression-bindings-foldout";
        public const string AnalogBindingsFoldoutName = "facial-character-analog-bindings-foldout";
        public const string LayersFoldoutName = "facial-character-layers-foldout";
        public const string ExpressionsFoldoutName = "facial-character-expressions-foldout";
        public const string DebugFoldoutName = "facial-character-debug-foldout";

        public const string SaveButtonName = "facial-character-save-button";
        public const string ExpressionsValidationHelpName = "facial-character-expressions-validation";

        // Expression 行の内部要素名（テストの Q<>() で参照可能）
        public const string ExpressionRowIdLabelName = "expression-row-id-label";
        public const string ExpressionRowNameFieldName = "expression-row-name-field";
        public const string ExpressionRowClipFieldName = "expression-row-clip-field";
        public const string ExpressionRowLayerDropdownName = "expression-row-layer-dropdown";
        public const string ExpressionRowMaskFieldName = "expression-row-mask-field";
        public const string ExpressionRowRendererSummaryName = "expression-row-renderer-summary";
        public const string ExpressionRowValidationHelpName = "expression-row-validation-help";

        // ====================================================================
        // SerializedProperty
        // ====================================================================

        private SerializedProperty _inputActionAssetProperty;
        private SerializedProperty _actionMapNameProperty;
        private SerializedProperty _expressionBindingsProperty;
        private SerializedProperty _analogBindingsProperty;
        private SerializedProperty _layersProperty;
        private SerializedProperty _expressionsProperty;
        private SerializedProperty _schemaVersionProperty;

#if UNITY_EDITOR
        private SerializedProperty _referenceModelProperty;
#endif

        // ====================================================================
        // VisualElement キャッシュ
        // ====================================================================

        private DropdownField _actionMapDropdown;
        private Label _debugSchemaVersionLabel;
        private Label _debugLayerCountLabel;
        private Label _debugExpressionCountLabel;
        private Label _debugJsonPathLabel;
        private HelpBox _expressionsValidationHelp;
        private Button _saveButton;
        private ListView _expressionListView;

        // ====================================================================
        // 候補リスト
        // ====================================================================

        private readonly List<string> _actionNameChoices = new List<string>();
        private readonly List<string> _layerNameChoices = new List<string>();

        // AnimationClip のサンプラ（Phase 2.1 の interface を利用）
        private IExpressionAnimationClipSampler _sampler;

        // ====================================================================
        // Editor lifecycle
        // ====================================================================

        public override VisualElement CreateInspectorGUI()
        {
            ResolveSerializedProperties();
            _sampler = new AnimationClipExpressionSampler();

            // 既存 Expression のうち Id が空のエントリは GUID で自動採番（Req 1.3）
            EnsureExpressionGuidIds();

            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            BuildSaveButton(root);
            BuildInputSection(root);
            BuildExpressionBindingsSection(root);
            BuildAnalogBindingsSection(root);
            BuildLayersSection(root);
            BuildExpressionsSection(root);
            BuildDebugSection(root);

            RefreshLayerNameChoices();
            RefreshActionMapChoices();
            RefreshActionNameChoices();
            UpdateValidation();
            UpdateDebugLabels();

            return root;
        }

        private void ResolveSerializedProperties()
        {
            _inputActionAssetProperty = serializedObject.FindProperty("_inputActionAsset");
            _actionMapNameProperty = serializedObject.FindProperty("_actionMapName");
            _expressionBindingsProperty = serializedObject.FindProperty("_expressionBindings");
            _analogBindingsProperty = serializedObject.FindProperty("_analogBindings");

            // 抽象基底 FacialCharacterProfileSO 由来 (protected)
            _layersProperty = serializedObject.FindProperty("_layers");
            _expressionsProperty = serializedObject.FindProperty("_expressions");
            _schemaVersionProperty = serializedObject.FindProperty("_schemaVersion");

#if UNITY_EDITOR
            _referenceModelProperty = serializedObject.FindProperty("_referenceModel");
#endif
        }

        // ====================================================================
        // Save ボタン（validation エラー時に disabled）
        // ====================================================================

        private void BuildSaveButton(VisualElement root)
        {
            _saveButton = new Button(SaveAsset)
            {
                name = SaveButtonName,
                text = "保存 (Save)",
                tooltip = "FacialCharacterSO の編集内容を保存し、StreamingAssets/{name}/profile.json を更新します。",
            };
            _saveButton.AddToClassList(FacialControlStyles.ActionButton);
            _saveButton.style.marginBottom = 6;
            root.Add(_saveButton);
        }

        private void SaveAsset()
        {
            if (target == null)
            {
                return;
            }
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        // ====================================================================
        // Section 1: 入力 (Input)
        // ====================================================================

        private void BuildInputSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = InputFoldoutName,
                text = "入力 (Input)",
                value = true,
            };

            var help = new HelpBox(
                "ここに割り当てた .inputactions の Action 名を Expression にバインドします。"
                + " InputActionAsset と ActionMap 名を設定すると、下のキーバインディング欄で Action 名をドロップダウン選択できるようになります。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_inputActionAssetProperty != null)
            {
                var assetField = new ObjectField("InputActionAsset")
                {
                    objectType = typeof(InputActionAsset),
                    allowSceneObjects = false,
                    tooltip = "Project ウィンドウで作成した .inputactions をここに割り当てる。",
                };
                assetField.BindProperty(_inputActionAssetProperty);
                assetField.RegisterValueChangedCallback(_ =>
                {
                    RefreshActionMapChoices();
                    RefreshActionNameChoices();
                });
                foldout.Add(assetField);
            }

            if (_actionMapNameProperty != null)
            {
                _actionMapDropdown = new DropdownField("ActionMap 名")
                {
                    tooltip = "対象 ActionMap の名前。既定は \"Expression\"。",
                };
                _actionMapDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_actionMapNameProperty == null)
                    {
                        return;
                    }
                    serializedObject.Update();
                    _actionMapNameProperty.stringValue = evt.newValue ?? string.Empty;
                    serializedObject.ApplyModifiedProperties();
                    RefreshActionNameChoices();
                });
                foldout.Add(_actionMapDropdown);

                var rawField = new PropertyField(_actionMapNameProperty, "ActionMap 名 (手入力)");
                foldout.Add(rawField);
            }

            root.Add(foldout);
        }

        // ====================================================================
        // Section 2: キーバインディング (Action ↔ Expression)
        // ====================================================================

        private void BuildExpressionBindingsSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = ExpressionBindingsFoldoutName,
                text = "キーバインディング (Action ↔ Expression)",
                value = true,
            };

            var help = new HelpBox(
                "InputAction 名と Expression ID を 1 対 1 で結びます。"
                + " Keyboard / Controller の振分けは Action の binding path から自動推定されます (Req 7.1)。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_expressionBindingsProperty != null)
            {
                var listView = BuildArrayListView(
                    _expressionBindingsProperty,
                    itemHeight: 56f,
                    makeItem: () => CreateExpressionBindingRow(),
                    bindItem: (element, index) => BindExpressionBindingRow(element, index));
                foldout.Add(listView);
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
                tooltip = "InputActionAsset の対象 ActionMap 配下の Action 名。",
            };
            container.Add(actionDropdown);

            var expressionDropdown = new DropdownField("Expression ID")
            {
                name = "expressionDropdown",
                tooltip = "発火対象の Expression ID。",
            };
            container.Add(expressionDropdown);

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
        }

        // ====================================================================
        // Section 3: アナログバインディング
        // ====================================================================

        private void BuildAnalogBindingsSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = AnalogBindingsFoldoutName,
                text = "アナログバインディング (連続値 → BlendShape)",
                value = false,
            };

            var help = new HelpBox(
                "連続値 InputAction を BlendShape ウェイトへ写像します。"
                + " Phase 4.7 で 3 フィールドに簡素化済 (inputActionRef + targetIdentifier + targetAxis)。"
                + " deadzone / scale / offset / curve 等の値変換は InputActionAsset 側 processor チェーンで行います。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_analogBindingsProperty != null)
            {
                var listView = BuildArrayListView(
                    _analogBindingsProperty,
                    itemHeight: 92f,
                    makeItem: () => CreateAnalogBindingRow(),
                    bindItem: (element, index) => BindAnalogBindingRow(element, index));
                foldout.Add(listView);
            }

            root.Add(foldout);
        }

        private VisualElement CreateAnalogBindingRow()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 4;

            var inputActionRefField = new TextField("inputActionRef")
            {
                name = "inputActionRefField",
                tooltip = "対象 InputAction の参照識別子 (action 名または GUID)。",
            };
            container.Add(inputActionRefField);

            var targetIdentifierField = new TextField("targetIdentifier")
            {
                name = "targetIdentifierField",
                tooltip = "ターゲット BlendShape 名。",
            };
            container.Add(targetIdentifierField);

            var targetAxisField = new EnumField("targetAxis", AnalogTargetAxis.X)
            {
                name = "targetAxisField",
                tooltip = "ターゲットの軸 (BlendShape は X 既定)。",
            };
            container.Add(targetAxisField);

            return container;
        }

        private void BindAnalogBindingRow(VisualElement element, int index)
        {
            if (_analogBindingsProperty == null
                || index < 0
                || index >= _analogBindingsProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            var entryProp = _analogBindingsProperty.GetArrayElementAtIndex(index);
            var inputActionRefProp = entryProp.FindPropertyRelative("inputActionRef");
            var targetIdentifierProp = entryProp.FindPropertyRelative("targetIdentifier");
            var targetAxisProp = entryProp.FindPropertyRelative("targetAxis");

            var inputActionRefField = element.Q<TextField>("inputActionRefField");
            if (inputActionRefField != null && inputActionRefProp != null)
            {
                inputActionRefField.BindProperty(inputActionRefProp);
            }

            var targetIdentifierField = element.Q<TextField>("targetIdentifierField");
            if (targetIdentifierField != null && targetIdentifierProp != null)
            {
                targetIdentifierField.BindProperty(targetIdentifierProp);
            }

            var targetAxisField = element.Q<EnumField>("targetAxisField");
            if (targetAxisField != null && targetAxisProp != null)
            {
                targetAxisField.SetValueWithoutNotify((AnalogTargetAxis)targetAxisProp.enumValueIndex);
                targetAxisField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _analogBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("targetAxis");
                    if (prop != null)
                    {
                        prop.enumValueIndex = (int)(AnalogTargetAxis)evt.newValue;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }
        }

        // ====================================================================
        // Section 4: レイヤー (Layers)
        // ====================================================================

        private void BuildLayersSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = LayersFoldoutName,
                text = "レイヤー (Layers)",
                value = false,
            };

            var help = new HelpBox(
                "表情を重ねる優先順位レイヤーを定義します。"
                + " name はレイヤー識別子、priority は値が大きいほど後段で適用、exclusionMode は LastWins / Blend を選択。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_layersProperty != null)
            {
                var layersField = new PropertyField(_layersProperty, "Layers");
                layersField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    RefreshLayerNameChoices();
                    UpdateValidation();
                    RebuildExpressionListView();
                });
                foldout.Add(layersField);
            }

            root.Add(foldout);
        }

        // ====================================================================
        // Section 5: Expression
        // ====================================================================

        private void BuildExpressionsSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = ExpressionsFoldoutName,
                text = "Expression",
                value = true,
            };

            var help = new HelpBox(
                "AnimationClip を割り当てると、時刻 0 の BlendShape / Bone 値および AnimationEvent 経由の遷移メタデータが自動的に snapshot 化されます。"
                + " Id は新規追加時に GUID で自動採番されます。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

#if UNITY_EDITOR
            if (_referenceModelProperty != null)
            {
                var refModelField = new PropertyField(_referenceModelProperty, "参照モデル (Editor 専用)");
                refModelField.tooltip = "BlendShape 名 / bone 名取得用の参照モデル。";
                foldout.Add(refModelField);
            }
#endif

            _expressionsValidationHelp = new HelpBox(string.Empty, HelpBoxMessageType.Error)
            {
                name = ExpressionsValidationHelpName,
            };
            _expressionsValidationHelp.style.display = DisplayStyle.None;
            foldout.Add(_expressionsValidationHelp);

            if (_expressionsProperty != null)
            {
                _expressionListView = BuildArrayListView(
                    _expressionsProperty,
                    itemHeight: 280f,
                    makeItem: () => CreateExpressionRow(),
                    bindItem: (element, index) => BindExpressionRow(element, index));
                _expressionListView.itemsAdded += indices =>
                {
                    serializedObject.Update();
                    foreach (var addedIndex in indices)
                    {
                        if (addedIndex < 0 || addedIndex >= _expressionsProperty.arraySize)
                        {
                            continue;
                        }
                        AssignDefaultsForNewExpression(addedIndex);
                    }
                    serializedObject.ApplyModifiedProperties();
                    UpdateValidation();
                };
                foldout.Add(_expressionListView);
            }

            root.Add(foldout);
        }

        private VisualElement CreateExpressionRow()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 6;

            var idLabel = new Label
            {
                name = ExpressionRowIdLabelName,
                tooltip = "Expression Id (GUID)。新規追加時に自動採番されます。",
            };
            idLabel.AddToClassList(FacialControlStyles.InfoLabel);
            container.Add(idLabel);

            var nameField = new TextField("Name")
            {
                name = ExpressionRowNameFieldName,
                tooltip = "表情名。AnimationClip を割り当てるとファイル名 (拡張子なし) で自動入力されます。",
            };
            container.Add(nameField);

            var clipField = new ObjectField("AnimationClip")
            {
                name = ExpressionRowClipFieldName,
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
                tooltip = "時刻 0 の BlendShape / Bone 値と AnimationEvent メタデータをサンプリングするソース AnimationClip。",
            };
            container.Add(clipField);

            var layerDropdown = new DropdownField("Layer")
            {
                name = ExpressionRowLayerDropdownName,
                tooltip = "所属レイヤー名 (上の Layers セクションから選択)。",
            };
            container.Add(layerDropdown);

            var maskField = new MaskField("LayerOverrideMask")
            {
                name = ExpressionRowMaskFieldName,
                tooltip = "他レイヤーへのオーバーライド対象を multi-select で指定 (Req 3.3)。",
            };
            container.Add(maskField);

            var rendererSummary = new ListView
            {
                name = ExpressionRowRendererSummaryName,
                fixedItemHeight = 18f,
                showBorder = true,
                selectionType = SelectionType.None,
                showAddRemoveFooter = false,
                showFoldoutHeader = true,
                headerTitle = "RendererPaths (read-only)",
                reorderable = false,
            };
            rendererSummary.SetEnabled(false);
            rendererSummary.style.minHeight = 60f;
            container.Add(rendererSummary);

            var validationHelp = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                name = ExpressionRowValidationHelpName,
            };
            validationHelp.style.display = DisplayStyle.None;
            container.Add(validationHelp);

            return container;
        }

        private void BindExpressionRow(VisualElement element, int index)
        {
            if (_expressionsProperty == null
                || index < 0
                || index >= _expressionsProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(index);
            var idProp = entryProp.FindPropertyRelative("id");
            var nameProp = entryProp.FindPropertyRelative("name");
            var layerProp = entryProp.FindPropertyRelative("layer");
            var clipProp = entryProp.FindPropertyRelative("animationClip");
            var maskProp = entryProp.FindPropertyRelative("layerOverrideMask");

            // Id label (read-only). 空なら自動採番。
            if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
            {
                idProp.stringValue = Guid.NewGuid().ToString("N");
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            var idLabel = element.Q<Label>(ExpressionRowIdLabelName);
            if (idLabel != null)
            {
                idLabel.text = $"Id: {(idProp != null ? idProp.stringValue : string.Empty)}";
            }

            var nameField = element.Q<TextField>(ExpressionRowNameFieldName);
            if (nameField != null && nameProp != null)
            {
                nameField.SetValueWithoutNotify(nameProp.stringValue ?? string.Empty);
                nameField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("name");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                        UpdateValidation();
                    }
                });
            }

            var clipField = element.Q<ObjectField>(ExpressionRowClipFieldName);
            if (clipField != null && clipProp != null)
            {
                clipField.SetValueWithoutNotify(clipProp.objectReferenceValue);
                clipField.RegisterValueChangedCallback(evt =>
                {
                    OnClipChanged(index, evt.newValue as AnimationClip, element);
                });
                RefreshRendererSummary(element, clipProp.objectReferenceValue as AnimationClip);
            }

            var layerDropdown = element.Q<DropdownField>(ExpressionRowLayerDropdownName);
            if (layerDropdown != null && layerProp != null)
            {
                var choices = BuildSafeChoices(_layerNameChoices, layerProp.stringValue);
                layerDropdown.choices = choices;
                layerDropdown.SetValueWithoutNotify(layerProp.stringValue ?? string.Empty);
                layerDropdown.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("layer");
                    if (prop != null)
                    {
                        prop.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                        UpdateValidation();
                    }
                });
            }

            var maskField = element.Q<MaskField>(ExpressionRowMaskFieldName);
            if (maskField != null && maskProp != null)
            {
                var maskChoices = new List<string>(_layerNameChoices.Count);
                for (int i = 0; i < _layerNameChoices.Count; i++)
                {
                    maskChoices.Add(_layerNameChoices[i]);
                }
                maskField.choices = maskChoices;
                int maskValue = ReadMaskValueFromSerializedList(maskProp, _layerNameChoices);
                maskField.SetValueWithoutNotify(maskValue);
                maskField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("layerOverrideMask");
                    if (prop != null)
                    {
                        WriteMaskValueToSerializedList(prop, evt.newValue, _layerNameChoices);
                        serializedObject.ApplyModifiedProperties();
                        UpdateValidation();
                    }
                });
            }

            UpdateRowValidation(element, clipProp != null ? clipProp.objectReferenceValue as AnimationClip : null,
                maskProp);
        }

        private void OnClipChanged(int index, AnimationClip newClip, VisualElement rowElement)
        {
            if (_expressionsProperty == null
                || index < 0
                || index >= _expressionsProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(index);
            var clipProp = entryProp.FindPropertyRelative("animationClip");
            var nameProp = entryProp.FindPropertyRelative("name");

            if (clipProp != null)
            {
                clipProp.objectReferenceValue = newClip;
            }

            // AnimationClip 名から Name を派生（Req 1.2）
            if (newClip != null && nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = Path.GetFileNameWithoutExtension(newClip.name);
            }

            serializedObject.ApplyModifiedProperties();

            // RendererPath summary を refresh（Req 4.5）
            RefreshRendererSummary(rowElement, newClip);

            // Inspector 全体を再描画して name field 等にも反映
            if (rowElement != null)
            {
                var nameField = rowElement.Q<TextField>(ExpressionRowNameFieldName);
                if (nameField != null && nameProp != null)
                {
                    nameField.SetValueWithoutNotify(nameProp.stringValue ?? string.Empty);
                }
            }

            UpdateValidation();
        }

        private void RefreshRendererSummary(VisualElement rowElement, AnimationClip clip)
        {
            if (rowElement == null)
            {
                return;
            }
            var summary = rowElement.Q<ListView>(ExpressionRowRendererSummaryName);
            if (summary == null)
            {
                return;
            }

            List<string> rendererPaths = new List<string>();
            if (clip != null && _sampler != null)
            {
                try
                {
                    var clipSummary = _sampler.SampleSummary(clip);
                    if (clipSummary.RendererPaths != null)
                    {
                        for (int i = 0; i < clipSummary.RendererPaths.Count; i++)
                        {
                            rendererPaths.Add(clipSummary.RendererPaths[i]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"FacialCharacterSOInspector: SampleSummary に失敗しました: {ex.Message}");
                }
            }
            summary.itemsSource = rendererPaths;
            summary.makeItem = () => new Label();
            summary.bindItem = (el, i) =>
            {
                if (el is Label label && i >= 0 && i < rendererPaths.Count)
                {
                    label.text = rendererPaths[i];
                }
            };
            summary.Rebuild();

            if (clip == null)
            {
                summary.headerTitle = "RendererPaths (read-only) - AnimationClip 未割り当て";
            }
            else
            {
                summary.headerTitle = $"RendererPaths (read-only) - {rendererPaths.Count} 件";
            }
        }

        private void AssignDefaultsForNewExpression(int index)
        {
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(index);
            var idProp = entryProp.FindPropertyRelative("id");
            var nameProp = entryProp.FindPropertyRelative("name");
            var layerProp = entryProp.FindPropertyRelative("layer");

            if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
            {
                idProp.stringValue = Guid.NewGuid().ToString("N");
            }
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = "NewExpression";
            }
            if (layerProp != null && string.IsNullOrEmpty(layerProp.stringValue) && _layerNameChoices.Count > 0)
            {
                layerProp.stringValue = _layerNameChoices[0];
            }
        }

        private void RebuildExpressionListView()
        {
            if (_expressionListView != null)
            {
                _expressionListView.Rebuild();
            }
        }

        // ====================================================================
        // Section 6: デバッグ情報 (Debug)
        // ====================================================================

        private void BuildDebugSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = DebugFoldoutName,
                text = "デバッグ情報 (Debug)",
                value = false,
            };

            var help = new HelpBox(
                "現在 SO に保持されているサマリ情報 (読み取り専用)。"
                + " JSON の自動エクスポート先は StreamingAssets 配下の規約パスです。",
                HelpBoxMessageType.None);
            foldout.Add(help);

            _debugSchemaVersionLabel = new Label();
            _debugSchemaVersionLabel.AddToClassList(FacialControlStyles.InfoLabel);
            foldout.Add(_debugSchemaVersionLabel);

            _debugLayerCountLabel = new Label();
            _debugLayerCountLabel.AddToClassList(FacialControlStyles.InfoLabel);
            foldout.Add(_debugLayerCountLabel);

            _debugExpressionCountLabel = new Label();
            _debugExpressionCountLabel.AddToClassList(FacialControlStyles.InfoLabel);
            foldout.Add(_debugExpressionCountLabel);

            _debugJsonPathLabel = new Label();
            _debugJsonPathLabel.AddToClassList(FacialControlStyles.InfoLabel);
            _debugJsonPathLabel.style.whiteSpace = WhiteSpace.Normal;
            foldout.Add(_debugJsonPathLabel);

            root.Add(foldout);
        }

        private void UpdateDebugLabels()
        {
            var so = target as FacialCharacterSO;
            if (so == null)
            {
                return;
            }

            if (_debugSchemaVersionLabel != null)
            {
                _debugSchemaVersionLabel.text = $"schemaVersion: {(string.IsNullOrEmpty(so.SchemaVersion) ? "(未設定)" : so.SchemaVersion)}";
            }
            if (_debugLayerCountLabel != null)
            {
                _debugLayerCountLabel.text = $"レイヤー数: {(so.Layers != null ? so.Layers.Count : 0)}";
            }
            if (_debugExpressionCountLabel != null)
            {
                _debugExpressionCountLabel.text = $"Expression 数: {(so.Expressions != null ? so.Expressions.Count : 0)}";
            }
            if (_debugJsonPathLabel != null)
            {
                var assetName = so.CharacterAssetName;
                var path = string.IsNullOrEmpty(assetName)
                    ? "(SO 名未設定)"
                    : Path.Combine(
                        UnityEngine.Application.streamingAssetsPath,
                        FacialCharacterProfileSO.StreamingAssetsRootFolder,
                        assetName,
                        FacialCharacterProfileSO.ProfileJsonFileName);
                _debugJsonPathLabel.text = $"自動エクスポート先: {path}";
            }
        }

        // ====================================================================
        // Validation
        // ====================================================================

        private void UpdateValidation()
        {
            if (_expressionsProperty == null)
            {
                return;
            }

            serializedObject.Update();

            var errors = new List<string>();

            // 重複 Id 検出（Req 1.7）
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var idProp = elem.FindPropertyRelative("id");
                var id = idProp != null ? idProp.stringValue : string.Empty;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                if (!seenIds.Add(id))
                {
                    errors.Add($"Expression Id '{id}' が重複しています (Req 1.7)。");
                }
            }

            // AnimationClip null + zero LayerOverrideMask 検出
            int nullClipCount = 0;
            int zeroMaskCount = 0;
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var clipProp = elem.FindPropertyRelative("animationClip");
                var maskProp = elem.FindPropertyRelative("layerOverrideMask");
                if (clipProp != null && clipProp.objectReferenceValue == null)
                {
                    nullClipCount++;
                }
                if (maskProp != null && maskProp.isArray && maskProp.arraySize == 0)
                {
                    zeroMaskCount++;
                }
            }
            if (nullClipCount > 0)
            {
                errors.Add($"AnimationClip が未割当の Expression が {nullClipCount} 件あります (Req 1.6)。");
            }
            if (zeroMaskCount > 0)
            {
                errors.Add($"LayerOverrideMask が空 (zero) の Expression が {zeroMaskCount} 件あります (Req 3.5)。");
            }

            if (_expressionsValidationHelp != null)
            {
                if (errors.Count == 0)
                {
                    _expressionsValidationHelp.text = string.Empty;
                    _expressionsValidationHelp.style.display = DisplayStyle.None;
                }
                else
                {
                    _expressionsValidationHelp.text = string.Join("\n", errors);
                    _expressionsValidationHelp.style.display = DisplayStyle.Flex;
                }
            }

            if (_saveButton != null)
            {
                _saveButton.SetEnabled(errors.Count == 0);
            }

            // 各行の HelpBox 状態を更新
            if (_expressionListView != null)
            {
                _expressionListView.RefreshItems();
            }
        }

        private void UpdateRowValidation(VisualElement rowElement, AnimationClip clip, SerializedProperty maskProp)
        {
            if (rowElement == null)
            {
                return;
            }
            var help = rowElement.Q<HelpBox>(ExpressionRowValidationHelpName);
            if (help == null)
            {
                return;
            }
            var messages = new List<string>();
            if (clip == null)
            {
                messages.Add("AnimationClip が未割り当てです (Req 1.6)。");
            }
            bool maskIsZero = maskProp != null && maskProp.isArray && maskProp.arraySize == 0;
            if (maskIsZero)
            {
                messages.Add("LayerOverrideMask が空です (Req 3.5)。少なくとも 1 つのレイヤーを選択してください。");
            }
            if (messages.Count == 0)
            {
                help.text = string.Empty;
                help.style.display = DisplayStyle.None;
            }
            else
            {
                help.text = string.Join("\n", messages);
                help.style.display = DisplayStyle.Flex;
            }
        }

        // ====================================================================
        // 共通 ListView ビルダ
        // ====================================================================

        private ListView BuildArrayListView(
            SerializedProperty arrayProperty,
            float itemHeight,
            Func<VisualElement> makeItem,
            Action<VisualElement, int> bindItem)
        {
            var indexProxy = new List<int>();
            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                indexProxy.Add(i);
            }

            var listView = new ListView
            {
                fixedItemHeight = itemHeight,
                itemsSource = indexProxy,
                showAddRemoveFooter = true,
                showBorder = true,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                showFoldoutHeader = false,
                makeItem = makeItem,
                bindItem = bindItem,
            };
            listView.style.marginTop = 4;
            listView.style.minHeight = 80f;

            listView.itemsAdded += indices =>
            {
                serializedObject.Update();
                int addCount = 0;
                foreach (var _ in indices)
                {
                    addCount++;
                }
                arrayProperty.arraySize += addCount;
                serializedObject.ApplyModifiedProperties();
                RebuildIndexProxy(indexProxy, arrayProperty);
                listView.Rebuild();
            };

            listView.itemsRemoved += indices =>
            {
                serializedObject.Update();
                var sorted = new List<int>(indices);
                sorted.Sort();
                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    var removeIndex = sorted[i];
                    if (removeIndex >= 0 && removeIndex < arrayProperty.arraySize)
                    {
                        arrayProperty.DeleteArrayElementAtIndex(removeIndex);
                    }
                }
                serializedObject.ApplyModifiedProperties();
                RebuildIndexProxy(indexProxy, arrayProperty);
                listView.Rebuild();
            };

            return listView;
        }

        private static void RebuildIndexProxy(List<int> indexProxy, SerializedProperty arrayProperty)
        {
            indexProxy.Clear();
            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                indexProxy.Add(i);
            }
        }

        // ====================================================================
        // 候補キャッシュ更新
        // ====================================================================

        private void RefreshLayerNameChoices()
        {
            _layerNameChoices.Clear();
            if (_layersProperty == null)
            {
                return;
            }
            for (int i = 0; i < _layersProperty.arraySize; i++)
            {
                var elem = _layersProperty.GetArrayElementAtIndex(i);
                var nameProp = elem.FindPropertyRelative("name");
                var name = nameProp != null ? nameProp.stringValue : null;
                if (!string.IsNullOrEmpty(name))
                {
                    _layerNameChoices.Add(name);
                }
            }
        }

        private void RefreshActionMapChoices()
        {
            if (_actionMapDropdown == null || _inputActionAssetProperty == null)
            {
                return;
            }
            var asset = _inputActionAssetProperty.objectReferenceValue as InputActionAsset;
            var choices = new List<string>();
            if (asset != null)
            {
                foreach (var map in asset.actionMaps)
                {
                    if (!string.IsNullOrEmpty(map.name))
                    {
                        choices.Add(map.name);
                    }
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
            if (_inputActionAssetProperty == null || _actionMapNameProperty == null)
            {
                return;
            }
            var asset = _inputActionAssetProperty.objectReferenceValue as InputActionAsset;
            if (asset == null)
            {
                return;
            }
            var mapName = _actionMapNameProperty.stringValue;
            if (string.IsNullOrEmpty(mapName))
            {
                return;
            }
            var map = asset.FindActionMap(mapName);
            if (map == null)
            {
                return;
            }
            foreach (var action in map.actions)
            {
                if (!string.IsNullOrEmpty(action.name))
                {
                    _actionNameChoices.Add(action.name);
                }
            }
        }

        private List<string> CollectExpressionIds()
        {
            var ids = new List<string>();
            if (_expressionsProperty == null)
            {
                return ids;
            }
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var idProp = elem.FindPropertyRelative("id");
                if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                {
                    ids.Add(idProp.stringValue);
                }
            }
            return ids;
        }

        private static List<string> BuildSafeChoices(IReadOnlyList<string> baseChoices, string currentValue)
        {
            var result = new List<string>(baseChoices.Count + 2);
            result.Add(string.Empty);
            for (int i = 0; i < baseChoices.Count; i++)
            {
                if (!result.Contains(baseChoices[i]))
                {
                    result.Add(baseChoices[i]);
                }
            }
            if (!string.IsNullOrEmpty(currentValue) && !result.Contains(currentValue))
            {
                result.Add(currentValue);
            }
            return result;
        }

        // ====================================================================
        // Mask <-> List<string> 変換
        // ====================================================================

        /// <summary>
        /// LayerOverrideMask の永続化形式 (List&lt;string&gt; layerOverrideMask) から MaskField 値を読み取る。
        /// Layers の宣言順を bit position として用いる。
        /// </summary>
        private static int ReadMaskValueFromSerializedList(SerializedProperty listProp, IReadOnlyList<string> orderedLayerNames)
        {
            if (listProp == null || !listProp.isArray || orderedLayerNames == null || orderedLayerNames.Count == 0)
            {
                return 0;
            }
            int result = 0;
            int maxBits = orderedLayerNames.Count > 32 ? 32 : orderedLayerNames.Count;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);
                var name = elem.stringValue;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                for (int b = 0; b < maxBits; b++)
                {
                    if (string.Equals(orderedLayerNames[b], name, StringComparison.Ordinal))
                    {
                        result |= 1 << b;
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// MaskField 値 (int) を LayerOverrideMask 永続化形式 (List&lt;string&gt;) に書き戻す。
        /// </summary>
        private static void WriteMaskValueToSerializedList(SerializedProperty listProp, int maskValue, IReadOnlyList<string> orderedLayerNames)
        {
            if (listProp == null || !listProp.isArray)
            {
                return;
            }
            listProp.ClearArray();
            if (orderedLayerNames == null || orderedLayerNames.Count == 0 || maskValue == 0)
            {
                return;
            }
            int maxBits = orderedLayerNames.Count > 32 ? 32 : orderedLayerNames.Count;
            for (int b = 0; b < maxBits; b++)
            {
                if ((maskValue & (1 << b)) != 0)
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    var elem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                    elem.stringValue = orderedLayerNames[b];
                }
            }
        }
    }
}
