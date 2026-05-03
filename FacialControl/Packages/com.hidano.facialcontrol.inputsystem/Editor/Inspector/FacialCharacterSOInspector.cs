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
using Hidano.FacialControl.InputSystem.Editor.AutoExport;

namespace Hidano.FacialControl.InputSystem.Editor.Inspector
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> 用の UI Toolkit カスタム Inspector。
    /// </summary>
    /// <remarks>
    /// Layer-Expression ネスト + アナログ表情 (目線等) + 自動保存対応。
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterSO))]
    public sealed class FacialCharacterSOInspector : UnityEditor.Editor
    {
        // ====================================================================
        // VisualElement の name 定数
        // ====================================================================

        public const string InputFoldoutName = "facial-character-input-foldout";
        public const string ExpressionBindingsFoldoutName = "facial-character-expression-bindings-foldout";
        public const string LayersFoldoutName = "facial-character-layers-foldout";
        public const string DebugFoldoutName = "facial-character-debug-foldout";

        public const string SaveButtonName = "facial-character-save-button";
        public const string SaveStatusLabelName = "facial-character-save-status";
        public const string ExpressionsValidationHelpName = "facial-character-expressions-validation";

        public const string ExpressionRowIdLabelName = "expression-row-id-label";
        public const string ExpressionRowNameFieldName = "expression-row-name-field";
        public const string ExpressionRowKindDropdownName = "expression-row-kind-dropdown";
        public const string ExpressionRowClipFieldName = "expression-row-clip-field";
        public const string ExpressionRowRendererSummaryName = "expression-row-renderer-summary";
        public const string ExpressionRowValidationHelpName = "expression-row-validation-help";
        public const string ExpressionRowTransitionDurationFieldName = "expression-row-transition-duration-field";
        public const string ExpressionRowGazeActionFieldName = "expression-row-gaze-action-field";
        public const string ExpressionRowGazeLeftBonePathName = "expression-row-gaze-left-bone-path";
        public const string ExpressionRowGazeLeftInitRotName = "expression-row-gaze-left-init-rot";
        public const string ExpressionRowGazeRightBonePathName = "expression-row-gaze-right-bone-path";
        public const string ExpressionRowGazeRightInitRotName = "expression-row-gaze-right-init-rot";
        public const string ExpressionRowGazeLookLeftClipName = "expression-row-gaze-look-left-clip";
        public const string ExpressionRowGazeLookRightClipName = "expression-row-gaze-look-right-clip";
        public const string ExpressionRowGazeLookUpClipName = "expression-row-gaze-look-up-clip";
        public const string ExpressionRowGazeLookDownClipName = "expression-row-gaze-look-down-clip";
        public const string ExpressionRowGazeAutoAssignButtonName = "expression-row-gaze-auto-assign-button";

        // ====================================================================
        // SerializedProperty
        // ====================================================================

        private SerializedProperty _inputActionAssetProperty;
        private SerializedProperty _actionMapNameProperty;
        private SerializedProperty _expressionBindingsProperty;
        private SerializedProperty _layersProperty;
        private SerializedProperty _expressionsProperty;
        private SerializedProperty _gazeConfigsProperty;
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
        private Label _saveStatusLabel;
        private VisualElement _layersContainer;
        private ListView _expressionBindingsListView;

        // ====================================================================
        // 候補リスト
        // ====================================================================

        private readonly List<string> _actionNameChoices = new List<string>();
        private readonly List<string> _layerNameChoices = new List<string>();

        private IExpressionAnimationClipSampler _sampler;
        private bool _autoSavePending;

        // ====================================================================
        // 共通スタイル定数
        // ====================================================================

        private const int HelpBoxFontSize = 12;
        private const int SectionFoldoutFontSize = 13;

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

        public override VisualElement CreateInspectorGUI()
        {
            ResolveSerializedProperties();
            _sampler = new AnimationClipExpressionSampler();

            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            BuildSaveStatusBar(root);
            BuildInputSection(root);
            BuildExpressionBindingsSection(root);
            BuildLayersSection(root);
            BuildReferenceModelSection(root);
            BuildDebugSection(root);

            RefreshLayerNameChoices();
            RefreshActionMapChoices();
            RefreshActionNameChoices();
            UpdateValidation();
            UpdateDebugLabels();

            // 自動保存: SerializedObject の変更を監視
            root.TrackSerializedObjectValue(serializedObject, _ => OnSerializedObjectChanged());

            return root;
        }

        private void ResolveSerializedProperties()
        {
            _inputActionAssetProperty = serializedObject.FindProperty("_inputActionAsset");
            _actionMapNameProperty = serializedObject.FindProperty("_actionMapName");
            _expressionBindingsProperty = serializedObject.FindProperty("_expressionBindings");
            _layersProperty = serializedObject.FindProperty("_layers");
            _expressionsProperty = serializedObject.FindProperty("_expressions");
            _gazeConfigsProperty = serializedObject.FindProperty("_gazeConfigs");
            _schemaVersionProperty = serializedObject.FindProperty("_schemaVersion");

#if UNITY_EDITOR
            _referenceModelProperty = serializedObject.FindProperty("_referenceModel");
#endif
        }

        // ====================================================================
        // 自動保存
        // ====================================================================

        private void BuildSaveStatusBar(VisualElement root)
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            _saveStatusLabel = new Label("変更は自動的に保存されます")
            {
                name = SaveStatusLabelName,
            };
            _saveStatusLabel.style.flexGrow = 1f;
            _saveStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _saveStatusLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
            bar.Add(_saveStatusLabel);

            var forceExportButton = new Button(ForceSave)
            {
                name = SaveButtonName,
                text = "今すぐ書き出し",
                tooltip = "現在の設定を StreamingAssets/{SO名}/profile.json に即時エクスポートする。",
            };
            forceExportButton.AddToClassList(FacialControlStyles.ActionButton);
            bar.Add(forceExportButton);

            root.Add(bar);
        }

        private void OnSerializedObjectChanged()
        {
            if (target == null) return;

            EditorUtility.SetDirty(target);
            UpdateValidation();
            UpdateDebugLabels();
            RefreshLayerNameChoices();

            if (!_autoSavePending)
            {
                _autoSavePending = true;
                EditorApplication.delayCall += FlushAutoSave;
                if (_saveStatusLabel != null)
                {
                    _saveStatusLabel.text = "保存中…";
                }
            }
        }

        private void FlushAutoSave()
        {
            _autoSavePending = false;
            if (target == null) return;

            try
            {
                if (target is FacialCharacterProfileSO profileSO)
                {
                    FacialCharacterSOAutoExporter.SampleAnimationClipsIntoCachedSnapshots(
                        profileSO, _sampler ?? new AnimationClipExpressionSampler());
                    FacialCharacterSOAutoExporter.ExportToStreamingAssets(profileSO);
                }
                AssetDatabase.SaveAssetIfDirty(target);

                if (_saveStatusLabel != null)
                {
                    _saveStatusLabel.text = $"保存しました ({DateTime.Now:HH:mm:ss})";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FacialCharacterSOInspector: 自動保存に失敗しました: {ex.Message}");
                if (_saveStatusLabel != null)
                {
                    _saveStatusLabel.text = "保存エラー(コンソール参照)";
                }
            }
        }

        private void ForceSave()
        {
            serializedObject.ApplyModifiedProperties();
            FlushAutoSave();
            AssetDatabase.Refresh();
        }

        // ====================================================================
        // 共通ヘルパー: HelpBox / Foldout の見栄え調整
        // ====================================================================

        private static HelpBox MakeHelpBox(string text, HelpBoxMessageType messageType = HelpBoxMessageType.Info)
        {
            var box = new HelpBox(text, messageType);
            box.style.fontSize = HelpBoxFontSize;
            box.style.marginTop = 2;
            box.style.marginBottom = 4;
            return box;
        }

        private static Foldout MakeSectionFoldout(string name, string text, bool open)
        {
            var foldout = new Foldout
            {
                name = name,
                text = text,
                value = open,
            };
            foldout.style.unityFontStyleAndWeight = FontStyle.Normal;
            foldout.style.fontSize = SectionFoldoutFontSize;
            return foldout;
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
                    itemHeight: 56f,
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
        // Section: レイヤー (Layers + その配下の Expressions)
        // ====================================================================

        private void BuildLayersSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(LayersFoldoutName, "レイヤーと表情", open: true);

            foldout.Add(MakeHelpBox(
                "表情を重ねる優先順位レイヤーを定義します。"
                + "各レイヤーには所属する表情を登録し、レイヤー全体で他レイヤーへの上書き範囲を指定します。"));

            _expressionsValidationHelp = new HelpBox(string.Empty, HelpBoxMessageType.Error)
            {
                name = ExpressionsValidationHelpName,
            };
            _expressionsValidationHelp.style.fontSize = HelpBoxFontSize;
            _expressionsValidationHelp.style.display = DisplayStyle.None;
            foldout.Add(_expressionsValidationHelp);

            _layersContainer = new VisualElement();
            _layersContainer.style.flexDirection = FlexDirection.Column;
            foldout.Add(_layersContainer);

            var addLayerButton = new Button(AddLayer)
            {
                text = "+ レイヤーを追加",
            };
            addLayerButton.style.alignSelf = Align.FlexStart;
            addLayerButton.style.marginTop = 4;
            foldout.Add(addLayerButton);

            RebuildLayersUI();

            root.Add(foldout);
        }

        private void AddLayer()
        {
            serializedObject.Update();
            int newIndex = _layersProperty.arraySize;
            _layersProperty.InsertArrayElementAtIndex(newIndex);
            var layerProp = _layersProperty.GetArrayElementAtIndex(newIndex);
            var nameProp = layerProp.FindPropertyRelative("name");
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = "new-layer";
            }
            var priorityProp = layerProp.FindPropertyRelative("priority");
            if (priorityProp != null) priorityProp.intValue = newIndex;
            serializedObject.ApplyModifiedProperties();
            RefreshLayerNameChoices();
            RebuildLayersUI();
            UpdateValidation();
        }

        private void RebuildLayersUI()
        {
            if (_layersContainer == null || _layersProperty == null) return;
            _layersContainer.Clear();

            serializedObject.Update();
            int layerCount = _layersProperty.arraySize;
            for (int i = 0; i < layerCount; i++)
            {
                int layerIndex = i;
                var layerCard = BuildLayerCard(layerIndex);
                _layersContainer.Add(layerCard);
            }
        }

        private VisualElement BuildLayerCard(int layerIndex)
        {
            var layerProp = _layersProperty.GetArrayElementAtIndex(layerIndex);
            var nameProp = layerProp.FindPropertyRelative("name");
            var priorityProp = layerProp.FindPropertyRelative("priority");
            var exclusionModeProp = layerProp.FindPropertyRelative("exclusionMode");
            var inputSourcesProp = layerProp.FindPropertyRelative("inputSources");

            string layerName = nameProp != null ? nameProp.stringValue : $"layer{layerIndex}";

            var card = new Foldout
            {
                text = string.IsNullOrEmpty(layerName) ? $"レイヤー [{layerIndex}]" : layerName,
                value = true,
            };
            card.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.6f, 0.9f));
            card.style.borderLeftWidth = 3;
            card.style.paddingLeft = 6;
            card.style.marginBottom = 8;

            // 名前
            if (nameProp != null)
            {
                var nameField = new TextField("名前");
                nameField.SetValueWithoutNotify(nameProp.stringValue ?? string.Empty);
                nameField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var p = _layersProperty.GetArrayElementAtIndex(layerIndex).FindPropertyRelative("name");
                    if (p != null)
                    {
                        p.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                        // レイヤー名は他レイヤーの「上書き対象」候補にも影響するため全体を再構築する
                        RefreshLayerNameChoices();
                        RebuildLayersUI();
                    }
                });
                card.Add(nameField);
            }

            if (priorityProp != null)
            {
                var priorityField = new IntegerField("優先度");
                priorityField.SetValueWithoutNotify(priorityProp.intValue);
                priorityField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var p = _layersProperty.GetArrayElementAtIndex(layerIndex).FindPropertyRelative("priority");
                    if (p != null) { p.intValue = evt.newValue; serializedObject.ApplyModifiedProperties(); }
                });
                card.Add(priorityField);
            }

            if (exclusionModeProp != null)
            {
                var modeField = new EnumField("排他モード", (ExclusionMode)exclusionModeProp.enumValueIndex);
                modeField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var p = _layersProperty.GetArrayElementAtIndex(layerIndex).FindPropertyRelative("exclusionMode");
                    if (p != null) { p.enumValueIndex = (int)(ExclusionMode)evt.newValue; serializedObject.ApplyModifiedProperties(); }
                });
                card.Add(modeField);
            }

            if (inputSourcesProp != null)
            {
                var inputSourcesField = new PropertyField(inputSourcesProp, "入力源");
                inputSourcesField.Bind(serializedObject);
                card.Add(inputSourcesField);
            }

            // LayerOverrideMask: このレイヤーがアクティブな間に上書きする他レイヤー
            var maskHelp = MakeHelpBox(
                "このレイヤーがアクティブな間、合成時に上書き対象とするレイヤーを選択します。"
                + "通常は自レイヤーを必ず含めます。");
            card.Add(maskHelp);

            var maskContainer = new VisualElement { name = $"layer-mask-container-{layerIndex}" };
            card.Add(maskContainer);
            RebuildLayerOverrideMaskFieldInto(maskContainer, layerIndex);

            // 表情リスト
            var expressionHeader = new Label("表情");
            expressionHeader.style.unityFontStyleAndWeight = FontStyle.Normal;
            expressionHeader.style.marginTop = 8;
            card.Add(expressionHeader);

            var expressionsContainer = new VisualElement { name = $"layer-expressions-{layerIndex}" };
            card.Add(expressionsContainer);
            RebuildExpressionRowsForLayer(expressionsContainer, layerName);

            var expressionAddRow = new VisualElement();
            expressionAddRow.style.flexDirection = FlexDirection.Row;
            expressionAddRow.style.marginTop = 4;

            var addExpressionButton = new Button(() => AddExpressionForLayer(layerName, ExpressionKind.Digital))
            {
                text = "+ デジタル操作の表情を追加",
            };
            expressionAddRow.Add(addExpressionButton);

            var addAnalogButton = new Button(() => AddExpressionForLayer(layerName, ExpressionKind.Analog))
            {
                text = "+ アナログ操作の表情を追加",
            };
            expressionAddRow.Add(addAnalogButton);

            card.Add(expressionAddRow);

            // レイヤー削除
            var removeLayerButton = new Button(() => RemoveLayer(layerIndex))
            {
                text = "このレイヤーを削除",
            };
            removeLayerButton.style.alignSelf = Align.FlexEnd;
            removeLayerButton.style.marginTop = 8;
            card.Add(removeLayerButton);

            return card;
        }

        private void RebuildLayerOverrideMaskFieldInto(VisualElement container, int layerIndex)
        {
            var layerProp = _layersProperty.GetArrayElementAtIndex(layerIndex);
            var maskProp = layerProp.FindPropertyRelative("layerOverrideMask");
            if (maskProp == null) return;

            var maskField = new MaskField("上書きするレイヤー");
            var orderedNames = new List<string>(_layerNameChoices);
            maskField.choices = orderedNames;
            int maskValue = ReadMaskValueFromSerializedList(maskProp, orderedNames);
            maskField.SetValueWithoutNotify(maskValue);
            maskField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var p = _layersProperty.GetArrayElementAtIndex(layerIndex).FindPropertyRelative("layerOverrideMask");
                if (p != null)
                {
                    WriteMaskValueToSerializedList(p, evt.newValue, orderedNames);
                    serializedObject.ApplyModifiedProperties();
                    UpdateValidation();
                }
            });
            container.Add(maskField);
        }

        private void RemoveLayer(int layerIndex)
        {
            if (!EditorUtility.DisplayDialog(
                "レイヤーを削除", "このレイヤーと、所属する全表情を削除します。よろしいですか？", "削除", "キャンセル"))
            {
                return;
            }

            serializedObject.Update();
            string layerName = string.Empty;
            var layerProp = _layersProperty.GetArrayElementAtIndex(layerIndex);
            var nameProp = layerProp.FindPropertyRelative("name");
            if (nameProp != null) layerName = nameProp.stringValue ?? string.Empty;

            // 所属する表情を削除
            for (int i = _expressionsProperty.arraySize - 1; i >= 0; i--)
            {
                var exprElem = _expressionsProperty.GetArrayElementAtIndex(i);
                var exprLayerProp = exprElem.FindPropertyRelative("layer");
                if (exprLayerProp != null
                    && string.Equals(exprLayerProp.stringValue, layerName, StringComparison.Ordinal))
                {
                    var idProp = exprElem.FindPropertyRelative("id");
                    if (idProp != null) RemoveGazeConfigByExpressionId(idProp.stringValue);
                    _expressionsProperty.DeleteArrayElementAtIndex(i);
                }
            }

            _layersProperty.DeleteArrayElementAtIndex(layerIndex);
            serializedObject.ApplyModifiedProperties();

            RefreshLayerNameChoices();
            RebuildLayersUI();
            UpdateValidation();
        }

        private void AddExpressionForLayer(string layerName, ExpressionKind kind)
        {
            serializedObject.Update();
            int newIndex = _expressionsProperty.arraySize;
            _expressionsProperty.InsertArrayElementAtIndex(newIndex);
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(newIndex);

            var idProp = entryProp.FindPropertyRelative("id");
            var nameProp = entryProp.FindPropertyRelative("name");
            var layerProp = entryProp.FindPropertyRelative("layer");
            var kindProp = entryProp.FindPropertyRelative("kind");

            string newId = Guid.NewGuid().ToString("N");
            if (idProp != null) idProp.stringValue = newId;
            if (nameProp != null) nameProp.stringValue = kind == ExpressionKind.Analog ? "アナログ表情" : "新規表情";
            if (layerProp != null) layerProp.stringValue = layerName ?? string.Empty;
            if (kindProp != null) kindProp.enumValueIndex = (int)kind;

            // アナログ操作なら GazeExpressionConfig も自動追加
            if (kind == ExpressionKind.Analog)
            {
                int newCfgIndex = _gazeConfigsProperty.arraySize;
                _gazeConfigsProperty.InsertArrayElementAtIndex(newCfgIndex);
                var cfgProp = _gazeConfigsProperty.GetArrayElementAtIndex(newCfgIndex);
                var cfgExprIdProp = cfgProp.FindPropertyRelative("expressionId");
                if (cfgExprIdProp != null) cfgExprIdProp.stringValue = newId;
            }

            serializedObject.ApplyModifiedProperties();

            RebuildLayersUI();
            UpdateValidation();
        }

        private void RebuildExpressionRowsForLayer(VisualElement container, string layerName)
        {
            container.Clear();
            if (_expressionsProperty == null) return;

            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                int exprIndex = i;
                var exprProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
                var lp = exprProp.FindPropertyRelative("layer");
                if (lp == null) continue;
                if (!string.Equals(lp.stringValue, layerName, StringComparison.Ordinal)) continue;

                var row = BuildExpressionRow(exprIndex);
                container.Add(row);
            }
        }

        // ====================================================================
        // Expression 行
        // ====================================================================

        private VisualElement BuildExpressionRow(int exprIndex)
        {
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var idProp = entryProp.FindPropertyRelative("id");
            var nameProp = entryProp.FindPropertyRelative("name");
            var kindProp = entryProp.FindPropertyRelative("kind");
            var transitionDurationProp = entryProp.FindPropertyRelative("transitionDuration");

            // Id 自動採番
            if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
            {
                idProp.stringValue = Guid.NewGuid().ToString("N");
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 8;
            row.style.paddingLeft = 6;
            row.style.borderLeftColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            row.style.borderLeftWidth = 2;

            // ヘッダー行: kind dropdown + 削除ボタン
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            ExpressionKind currentKind = ExpressionKind.Digital;
            if (kindProp != null)
            {
                currentKind = (ExpressionKind)kindProp.enumValueIndex;
            }

            // EnumField そのままだと表示ラベルが "Digital" / "Analog" の英語になる。
            // ユーザー視点では「デジタル操作 / アナログ操作」のほうが意図が伝わるため
            // DropdownField で日本語ラベルを供給し、選択値を ExpressionKind に同期させる。
            var kindDropdown = new DropdownField("種別")
            {
                name = ExpressionRowKindDropdownName,
                choices = new List<string> { "デジタル操作", "アナログ操作" },
            };
            kindDropdown.SetValueWithoutNotify(currentKind == ExpressionKind.Analog ? "アナログ操作" : "デジタル操作");
            kindDropdown.style.flexGrow = 1f;
            kindDropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var p = _expressionsProperty.GetArrayElementAtIndex(exprIndex).FindPropertyRelative("kind");
                if (p != null)
                {
                    var newKind = string.Equals(evt.newValue, "アナログ操作", StringComparison.Ordinal)
                        ? ExpressionKind.Analog
                        : ExpressionKind.Digital;
                    p.enumValueIndex = (int)newKind;
                    serializedObject.ApplyModifiedProperties();

                    var idForCfg = idProp != null ? idProp.stringValue : string.Empty;
                    if (newKind == ExpressionKind.Analog && !HasGazeConfigForExpression(idForCfg))
                    {
                        AppendGazeConfigForExpression(idForCfg);
                    }
                    else if (newKind == ExpressionKind.Digital)
                    {
                        RemoveGazeConfigByExpressionId(idForCfg);
                    }
                    RebuildLayersUI();
                }
            });
            headerRow.Add(kindDropdown);

            var removeButton = new Button(() => RemoveExpression(exprIndex))
            {
                text = "削除",
            };
            removeButton.style.marginLeft = 6;
            headerRow.Add(removeButton);

            row.Add(headerRow);

            // ID label
            var idLabel = new Label
            {
                name = ExpressionRowIdLabelName,
                text = $"ID: {(idProp != null ? idProp.stringValue : string.Empty)}",
            };
            idLabel.AddToClassList(FacialControlStyles.InfoLabel);
            idLabel.style.fontSize = 10;
            idLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            row.Add(idLabel);

            // 名前
            var nameField = new TextField("名前")
            {
                name = ExpressionRowNameFieldName,
            };
            if (nameProp != null)
            {
                nameField.SetValueWithoutNotify(nameProp.stringValue ?? string.Empty);
                nameField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var p = _expressionsProperty.GetArrayElementAtIndex(exprIndex).FindPropertyRelative("name");
                    if (p != null)
                    {
                        p.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }
            row.Add(nameField);

            // 遷移時間。アナログ操作では概念がないため非表示にする (データは互換目的で保持)。
            var transitionDurationField = new Slider("遷移時間 (秒)", 0f, 1f)
            {
                name = ExpressionRowTransitionDurationFieldName,
                showInputField = true,
            };
            if (transitionDurationProp != null)
            {
                transitionDurationField.BindProperty(transitionDurationProp);
            }
            transitionDurationField.style.display =
                currentKind == ExpressionKind.Analog ? DisplayStyle.None : DisplayStyle.Flex;
            row.Add(transitionDurationField);

            // kind 別の専用 UI。AnimationClip 指定はデジタル/アナログ双方で使えるため
            // 共通で BuildAnimationClipFields を呼び、アナログのみ追加でボーン/BlendShape 設定を出す。
            BuildAnimationClipFields(row, exprIndex);
            if (currentKind == ExpressionKind.Analog)
            {
                BuildEyeLookFields(row, exprIndex, idProp != null ? idProp.stringValue : string.Empty);
            }

            // Validation
            var validationHelp = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                name = ExpressionRowValidationHelpName,
            };
            validationHelp.style.fontSize = HelpBoxFontSize;
            validationHelp.style.display = DisplayStyle.None;
            row.Add(validationHelp);

            UpdateRowValidation(row, exprIndex);

            return row;
        }

        private void BuildAnimationClipFields(VisualElement row, int exprIndex)
        {
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var clipProp = entryProp.FindPropertyRelative("animationClip");

            var clipField = new ExpressionClipObjectField
            {
                name = ExpressionRowClipFieldName,
                label = "AnimationClip",
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            if (clipProp != null)
            {
                clipField.BindProperty(clipProp);
                clipField.RegisterCallback<ChangeEvent<UnityEngine.Object>>(evt =>
                {
                    OnClipChanged(exprIndex, evt.newValue as AnimationClip, row);
                });
                clipField.OnValueAssigned = newValue =>
                {
                    OnClipChanged(exprIndex, newValue as AnimationClip, row);
                };
                clipField.RefreshDisplayLabel();
            }
            row.Add(clipField);

            var rendererSummary = new ListView
            {
                name = ExpressionRowRendererSummaryName,
                fixedItemHeight = 18f,
                showBorder = true,
                selectionType = SelectionType.None,
                showAddRemoveFooter = false,
                showFoldoutHeader = true,
                headerTitle = "影響する SkinnedMeshRenderer",
                reorderable = false,
            };
            rendererSummary.SetEnabled(false);
            rendererSummary.style.minHeight = 60f;
            row.Add(rendererSummary);

            RefreshRendererSummary(row, clipProp != null ? clipProp.objectReferenceValue as AnimationClip : null);
        }

        private void BuildEyeLookFields(VisualElement row, int exprIndex, string expressionId)
        {
            row.Add(MakeHelpBox(
                "Vector2 入力 (左スティック等) で両目を同時駆動します。"
                + "通常は目線ボーンの上下左右回転で制御し、必要に応じて BlendShape も併用できます。"));

            int cfgIndex = FindGazeConfigIndexByExpressionId(expressionId);
            if (cfgIndex < 0)
            {
                AppendGazeConfigForExpression(expressionId);
                cfgIndex = FindGazeConfigIndexByExpressionId(expressionId);
            }
            if (cfgIndex < 0) return;

            var cfgProp = _gazeConfigsProperty.GetArrayElementAtIndex(cfgIndex);
            var actionProp = cfgProp.FindPropertyRelative("inputAction");
            var leftBonePathProp = cfgProp.FindPropertyRelative("leftEyeBonePath");
            var leftInitRotProp = cfgProp.FindPropertyRelative("leftEyeInitialRotation");
            var rightBonePathProp = cfgProp.FindPropertyRelative("rightEyeBonePath");
            var rightInitRotProp = cfgProp.FindPropertyRelative("rightEyeInitialRotation");
            var lookLeftClipProp = cfgProp.FindPropertyRelative("lookLeftClip");
            var lookRightClipProp = cfgProp.FindPropertyRelative("lookRightClip");
            var lookUpClipProp = cfgProp.FindPropertyRelative("lookUpClip");
            var lookDownClipProp = cfgProp.FindPropertyRelative("lookDownClip");

            if (actionProp != null)
            {
                var actionField = new ObjectField("InputAction (Vector2)")
                {
                    name = ExpressionRowGazeActionFieldName,
                    objectType = typeof(InputActionReference),
                    allowSceneObjects = false,
                };
                actionField.BindProperty(actionProp);
                row.Add(actionField);
            }

            // ----- ボーン制御 (主) -----
            var boneSection = new Foldout
            {
                text = "目線ボーン (主)",
                value = true,
            };
            boneSection.style.marginTop = 4;
            boneSection.Add(MakeHelpBox(
                "Animator のルートからの相対パスで左右の目ボーンを指定します。"
                + "初期回転 (Euler 度) はアナログ入力 0 のときに保たれる姿勢で、入力値はこの値に加算されます。"));

            if (leftBonePathProp != null)
            {
                var f = new TextField("左目ボーンパス") { name = ExpressionRowGazeLeftBonePathName };
                f.BindProperty(leftBonePathProp);
                boneSection.Add(f);
            }
            if (leftInitRotProp != null)
            {
                var f = new Vector3Field("左目 初期回転 (Euler)") { name = ExpressionRowGazeLeftInitRotName };
                f.BindProperty(leftInitRotProp);
                boneSection.Add(f);
            }
            if (rightBonePathProp != null)
            {
                var f = new TextField("右目ボーンパス") { name = ExpressionRowGazeRightBonePathName };
                f.BindProperty(rightBonePathProp);
                boneSection.Add(f);
            }
            if (rightInitRotProp != null)
            {
                var f = new Vector3Field("右目 初期回転 (Euler)") { name = ExpressionRowGazeRightInitRotName };
                f.BindProperty(rightInitRotProp);
                boneSection.Add(f);
            }

            var autoAssignButton = new Button(() => AutoAssignGazeBonesFromReferenceModel(
                leftBonePathProp, leftInitRotProp, rightBonePathProp, rightInitRotProp))
            {
                name = ExpressionRowGazeAutoAssignButtonName,
                text = "参照モデルから自動設定",
                tooltip = "参照モデルの Animator から左右目ボーンを解決し、ボーン名と現在の localEulerAngles を初期回転として書き込みます。"
                    + " Humanoid Avatar が設定されている場合は LeftEye / RightEye マッピングを優先し、不在時は名前検索 (LeftEye / RightEye / *eye*) でフォールバックします。",
            };
            autoAssignButton.style.marginTop = 4;
            boneSection.Add(autoAssignButton);

            row.Add(boneSection);

            // ----- BlendShape 制御 (オプション、4 系統 clip) -----
            var blendSection = new Foldout
            {
                text = "目線 BlendShape (オプション)",
                value = false,
            };
            blendSection.style.marginTop = 4;
            blendSection.Add(MakeHelpBox(
                "BlendShape ベースで目線を表現するモデル向けのオプション設定です。"
                + " 4 系統 (LookLeft / LookRight / LookUp / LookDown) の AnimationClip を指定し、"
                + " Vector2 入力の +X / -X / +Y / -Y 方向に対応する BlendShape 状態を表現します。"
                + " clip 内の BlendShape curve の time=0 における値を keyframe weight として線形駆動します。"));
            blendSection.Add(BuildGazeClipField("LookLeft Clip (input.x < 0)", lookLeftClipProp, ExpressionRowGazeLookLeftClipName));
            blendSection.Add(BuildGazeClipField("LookRight Clip (input.x > 0)", lookRightClipProp, ExpressionRowGazeLookRightClipName));
            blendSection.Add(BuildGazeClipField("LookUp Clip (input.y > 0)", lookUpClipProp, ExpressionRowGazeLookUpClipName));
            blendSection.Add(BuildGazeClipField("LookDown Clip (input.y < 0)", lookDownClipProp, ExpressionRowGazeLookDownClipName));
            row.Add(blendSection);
        }

        private static ObjectField BuildGazeClipField(string label, SerializedProperty prop, string name)
        {
            var field = new ObjectField(label)
            {
                name = name,
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            if (prop != null) field.BindProperty(prop);
            return field;
        }

        private void RemoveExpression(int exprIndex)
        {
            serializedObject.Update();
            if (exprIndex < 0 || exprIndex >= _expressionsProperty.arraySize) return;

            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var idProp = entryProp.FindPropertyRelative("id");
            string id = idProp != null ? idProp.stringValue : string.Empty;

            RemoveGazeConfigByExpressionId(id);
            _expressionsProperty.DeleteArrayElementAtIndex(exprIndex);
            serializedObject.ApplyModifiedProperties();

            RebuildLayersUI();
            UpdateValidation();
        }

        // ====================================================================
        // GazeConfig ヘルパー
        // ====================================================================

        private int FindGazeConfigIndexByExpressionId(string expressionId)
        {
            if (_gazeConfigsProperty == null || string.IsNullOrEmpty(expressionId)) return -1;
            for (int i = 0; i < _gazeConfigsProperty.arraySize; i++)
            {
                var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(i);
                var idP = cfg.FindPropertyRelative("expressionId");
                if (idP != null && string.Equals(idP.stringValue, expressionId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool HasGazeConfigForExpression(string expressionId)
        {
            return FindGazeConfigIndexByExpressionId(expressionId) >= 0;
        }

        private void AppendGazeConfigForExpression(string expressionId)
        {
            if (_gazeConfigsProperty == null) return;
            serializedObject.Update();
            int newIndex = _gazeConfigsProperty.arraySize;
            _gazeConfigsProperty.InsertArrayElementAtIndex(newIndex);
            var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(newIndex);
            var idP = cfg.FindPropertyRelative("expressionId");
            if (idP != null) idP.stringValue = expressionId ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveGazeConfigByExpressionId(string expressionId)
        {
            if (_gazeConfigsProperty == null || string.IsNullOrEmpty(expressionId)) return;
            serializedObject.Update();
            for (int i = _gazeConfigsProperty.arraySize - 1; i >= 0; i--)
            {
                var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(i);
                var idP = cfg.FindPropertyRelative("expressionId");
                if (idP != null && string.Equals(idP.stringValue, expressionId, StringComparison.Ordinal))
                {
                    _gazeConfigsProperty.DeleteArrayElementAtIndex(i);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        // ====================================================================
        // 目線ボーン: 参照モデルからの自動設定
        // ====================================================================

        /// <summary>
        /// 参照モデル (<c>_referenceModel</c>) の Animator から左右目ボーンを解決し、
        /// 対応する SerializedProperty にボーン名と現在の localEulerAngles を書き込む。
        /// Humanoid Avatar 優先、不在時は名前検索でフォールバック。
        /// </summary>
        private void AutoAssignGazeBonesFromReferenceModel(
            SerializedProperty leftBonePathProp,
            SerializedProperty leftInitRotProp,
            SerializedProperty rightBonePathProp,
            SerializedProperty rightInitRotProp)
        {
            if (_referenceModelProperty == null)
            {
                Debug.LogWarning("[FacialCharacterSOInspector] 参照モデル property が解決できません。");
                return;
            }

            var referenceModel = _referenceModelProperty.objectReferenceValue as GameObject;
            if (referenceModel == null)
            {
                Debug.LogWarning(
                    "[FacialCharacterSOInspector] 参照モデルが未割り当てです。"
                    + " Inspector の「参照モデル」セクションで GameObject を割り当ててから再実行してください。");
                return;
            }

            var animator = referenceModel.GetComponentInChildren<Animator>(includeInactive: true);

            Transform leftEye = null;
            Transform rightEye = null;

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            }

            if (leftEye == null) leftEye = FindEyeTransformByName(referenceModel.transform, isLeft: true);
            if (rightEye == null) rightEye = FindEyeTransformByName(referenceModel.transform, isLeft: false);

            serializedObject.Update();

            int assigned = 0;
            if (leftEye != null)
            {
                if (leftBonePathProp != null) leftBonePathProp.stringValue = leftEye.name;
                if (leftInitRotProp != null) leftInitRotProp.vector3Value = leftEye.localEulerAngles;
                assigned++;
            }
            else
            {
                Debug.LogWarning("[FacialCharacterSOInspector] 左目ボーンを参照モデルから解決できませんでした。Humanoid マッピング・命名規則を確認してください。");
            }

            if (rightEye != null)
            {
                if (rightBonePathProp != null) rightBonePathProp.stringValue = rightEye.name;
                if (rightInitRotProp != null) rightInitRotProp.vector3Value = rightEye.localEulerAngles;
                assigned++;
            }
            else
            {
                Debug.LogWarning("[FacialCharacterSOInspector] 右目ボーンを参照モデルから解決できませんでした。Humanoid マッピング・命名規則を確認してください。");
            }

            if (assigned > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log(
                    $"[FacialCharacterSOInspector] 参照モデル '{referenceModel.name}' から目ボーンを自動設定しました"
                    + $" (Left: {(leftEye != null ? leftEye.name : "(skip)")}, Right: {(rightEye != null ? rightEye.name : "(skip)")}).");
            }
        }

        /// <summary>
        /// Humanoid マッピング不在時に名前で目ボーン Transform を検索する。
        /// 優先順: 完全一致 (LeftEye / RightEye) → side prefix (L_*Eye* / R_*Eye*) → side keyword (left*eye* / right*eye*)。
        /// 大小文字無視。
        /// </summary>
        private static Transform FindEyeTransformByName(Transform root, bool isLeft)
        {
            if (root == null) return null;
            var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);

            string exact = isLeft ? "LeftEye" : "RightEye";
            string sidePrefix = isLeft ? "l_" : "r_";
            string sideKeyword = isLeft ? "left" : "right";

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                if (string.Equals(t.name, exact, StringComparison.OrdinalIgnoreCase)) return t;
            }
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                if (t.name.StartsWith(sidePrefix, StringComparison.OrdinalIgnoreCase)
                    && t.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0) return t;
            }
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                var n = t.name;
                if (n.IndexOf(sideKeyword, StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0) return t;
            }
            return null;
        }

        // ====================================================================
        // Section: 参照モデル
        // ====================================================================

        private void BuildReferenceModelSection(VisualElement root)
        {
#if UNITY_EDITOR
            if (_referenceModelProperty == null) return;
            var foldout = MakeSectionFoldout("facial-character-reference-model-foldout", "参照モデル", open: false);
            foldout.Add(MakeHelpBox(
                "BlendShape 名やボーン名の取得元となるモデルを指定します。"
                + "AnimationClip の RendererPath 検証にも利用されます。"));
            var refModelField = new PropertyField(_referenceModelProperty, "参照モデル");
            foldout.Add(refModelField);
            root.Add(foldout);
#endif
        }

        // ====================================================================
        // Section: デバッグ情報
        // ====================================================================

        private void BuildDebugSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(DebugFoldoutName, "状態", open: false);

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
            if (so == null) return;

            if (_debugSchemaVersionLabel != null)
            {
                _debugSchemaVersionLabel.text = $"スキーマバージョン: {(string.IsNullOrEmpty(so.SchemaVersion) ? "(未設定)" : so.SchemaVersion)}";
            }
            if (_debugLayerCountLabel != null)
            {
                _debugLayerCountLabel.text = $"レイヤー数: {(so.Layers != null ? so.Layers.Count : 0)}";
            }
            if (_debugExpressionCountLabel != null)
            {
                _debugExpressionCountLabel.text = $"表情数: {(so.Expressions != null ? so.Expressions.Count : 0)}";
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
                _debugJsonPathLabel.text = $"JSON 出力先: {path}";
            }
        }

        // ====================================================================
        // Validation
        // ====================================================================

        private void UpdateValidation()
        {
            if (_expressionsProperty == null) return;
            serializedObject.Update();

            var errors = new List<string>();

            // 重複 Id 検出
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var idProp = elem.FindPropertyRelative("id");
                var id = idProp != null ? idProp.stringValue : string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                if (!seenIds.Add(id))
                {
                    errors.Add($"表情 ID '{id}' が重複しています。");
                }
            }

            // 各 Layer のマスク空チェック
            if (_layersProperty != null)
            {
                for (int i = 0; i < _layersProperty.arraySize; i++)
                {
                    var l = _layersProperty.GetArrayElementAtIndex(i);
                    var maskP = l.FindPropertyRelative("layerOverrideMask");
                    if (maskP != null && maskP.isArray && maskP.arraySize == 0)
                    {
                        var nameP = l.FindPropertyRelative("name");
                        var lname = nameP != null ? nameP.stringValue : $"layer{i}";
                        errors.Add($"レイヤー '{lname}' の上書き対象が未設定です。");
                    }
                }
            }

            // AnimationClip null タリー (kind=Digital のみ。Analog は AnimationClip 任意)
            int nullClipCount = 0;
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var kindP = elem.FindPropertyRelative("kind");
                int kindValue = kindP != null ? kindP.enumValueIndex : 0;
                if (kindValue != (int)ExpressionKind.Digital) continue;
                var clipP = elem.FindPropertyRelative("animationClip");
                if (clipP != null && clipP.objectReferenceValue == null) nullClipCount++;
            }
            if (nullClipCount > 0)
            {
                errors.Add($"AnimationClip が未割当のデジタル表情が {nullClipCount} 件あります。");
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

            // 各行 validation
            if (_layersContainer != null)
            {
                UpdateAllRowValidations();
            }
        }

        private void UpdateAllRowValidations()
        {
            for (int i = 0; i < _layersProperty.arraySize; i++)
            {
                var lp = _layersProperty.GetArrayElementAtIndex(i);
                var nameP = lp.FindPropertyRelative("name");
                if (nameP == null) continue;
                var layerName = nameP.stringValue ?? string.Empty;

                var container = _layersContainer.Q<VisualElement>($"layer-expressions-{i}");
                if (container == null) continue;

                int rowIdx = 0;
                for (int j = 0; j < _expressionsProperty.arraySize; j++)
                {
                    var ep = _expressionsProperty.GetArrayElementAtIndex(j);
                    var lP = ep.FindPropertyRelative("layer");
                    if (lP == null) continue;
                    if (!string.Equals(lP.stringValue, layerName, StringComparison.Ordinal)) continue;

                    if (rowIdx < container.childCount)
                    {
                        UpdateRowValidation(container[rowIdx], j);
                    }
                    rowIdx++;
                }
            }
        }

        private void UpdateRowValidation(VisualElement rowElement, int exprIndex)
        {
            if (rowElement == null) return;
            var help = rowElement.Q<HelpBox>(ExpressionRowValidationHelpName);
            if (help == null) return;

            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var kindProp = entryProp.FindPropertyRelative("kind");
            var clipProp = entryProp.FindPropertyRelative("animationClip");
            var idProp = entryProp.FindPropertyRelative("id");

            ExpressionKind kind = kindProp != null ? (ExpressionKind)kindProp.enumValueIndex : ExpressionKind.Digital;

            var messages = new List<string>();
            if (kind == ExpressionKind.Digital)
            {
                if (clipProp == null || clipProp.objectReferenceValue == null)
                {
                    messages.Add("AnimationClip が未割り当てです。");
                }
                var mismatchMessage = BuildRendererPathMismatchMessage(clipProp != null ? clipProp.objectReferenceValue as AnimationClip : null);
                if (!string.IsNullOrEmpty(mismatchMessage)) messages.Add(mismatchMessage);
            }
            else if (kind == ExpressionKind.Analog)
            {
                int cfgIndex = FindGazeConfigIndexByExpressionId(idProp != null ? idProp.stringValue : string.Empty);
                if (cfgIndex < 0)
                {
                    messages.Add("アナログ操作の設定が見つかりません。");
                }
                else
                {
                    var cfgProp = _gazeConfigsProperty.GetArrayElementAtIndex(cfgIndex);
                    var actionProp = cfgProp.FindPropertyRelative("inputAction");
                    if (actionProp == null || actionProp.objectReferenceValue == null)
                    {
                        messages.Add("InputAction (Vector2) が未割り当てです。");
                    }
                    bool anyBone = false;
                    foreach (var fname in new[] { "leftEyeBonePath", "rightEyeBonePath" })
                    {
                        var p = cfgProp.FindPropertyRelative(fname);
                        if (p != null && !string.IsNullOrWhiteSpace(p.stringValue)) { anyBone = true; break; }
                    }
                    bool anyBs = false;
                    foreach (var fname in new[] { "lookLeftClip", "lookRightClip", "lookUpClip", "lookDownClip" })
                    {
                        var p = cfgProp.FindPropertyRelative(fname);
                        if (p != null && p.objectReferenceValue != null) { anyBs = true; break; }
                    }
                    if (!anyBone && !anyBs)
                    {
                        messages.Add("目線ボーンまたは BlendShape のいずれかを 1 つ以上設定してください。");
                    }
                }
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

            if (clipProp != null) clipProp.objectReferenceValue = newClip;

            if (newClip != null && nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = ResolveAnimationClipFileName(newClip);
            }
            serializedObject.ApplyModifiedProperties();

            RefreshRendererSummary(rowElement, newClip);
            if (rowElement != null)
            {
                var nameField = rowElement.Q<TextField>(ExpressionRowNameFieldName);
                if (nameField != null && nameProp != null)
                {
                    nameField.SetValueWithoutNotify(nameProp.stringValue ?? string.Empty);
                }
                var clipField = rowElement.Q<ExpressionClipObjectField>(ExpressionRowClipFieldName);
                if (clipField != null) clipField.RefreshDisplayLabel();

                UpdateRowValidation(rowElement, index);
            }
        }

        private static string ResolveAnimationClipFileName(AnimationClip clip)
        {
            if (clip == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(path))
            {
                return Path.GetFileNameWithoutExtension(path);
            }
            return clip.name;
        }

        private void RefreshRendererSummary(VisualElement rowElement, AnimationClip clip)
        {
            if (rowElement == null) return;
            var summary = rowElement.Q<ListView>(ExpressionRowRendererSummaryName);
            if (summary == null) return;

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
                    Debug.LogWarning($"FacialCharacterSOInspector: SampleSummary に失敗: {ex.Message}");
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
                summary.headerTitle = "影響する SkinnedMeshRenderer (AnimationClip 未割当)";
            }
            else
            {
                summary.headerTitle = $"影響する SkinnedMeshRenderer ({rendererPaths.Count} 件)";
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
            for (int i = 0; i < arrayProperty.arraySize; i++) indexProxy.Add(i);

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
                foreach (var _ in indices) addCount++;
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
            for (int i = 0; i < arrayProperty.arraySize; i++) indexProxy.Add(i);
        }

        private void RebuildExpressionBindingsListView()
        {
            _expressionBindingsListView?.Rebuild();
        }

        // ====================================================================
        // 候補キャッシュ更新
        // ====================================================================

        private void RefreshLayerNameChoices()
        {
            _layerNameChoices.Clear();
            if (_layersProperty == null) return;
            for (int i = 0; i < _layersProperty.arraySize; i++)
            {
                var elem = _layersProperty.GetArrayElementAtIndex(i);
                var nameProp = elem.FindPropertyRelative("name");
                var name = nameProp != null ? nameProp.stringValue : null;
                if (!string.IsNullOrEmpty(name)) _layerNameChoices.Add(name);
            }
        }

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

        private List<string> CollectExpressionIds()
        {
            var ids = new List<string>();
            if (_expressionsProperty == null) return ids;
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
                if (!result.Contains(baseChoices[i])) result.Add(baseChoices[i]);
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

        private static int ReadMaskValueFromSerializedList(SerializedProperty listProp, IReadOnlyList<string> orderedLayerNames)
        {
            if (listProp == null || !listProp.isArray || orderedLayerNames == null || orderedLayerNames.Count == 0) return 0;
            int result = 0;
            int maxBits = orderedLayerNames.Count > 32 ? 32 : orderedLayerNames.Count;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);
                var name = elem.stringValue;
                if (string.IsNullOrEmpty(name)) continue;
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

        private static void WriteMaskValueToSerializedList(SerializedProperty listProp, int maskValue, IReadOnlyList<string> orderedLayerNames)
        {
            if (listProp == null || !listProp.isArray) return;
            listProp.ClearArray();
            if (orderedLayerNames == null || orderedLayerNames.Count == 0 || maskValue == 0) return;
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

        // ====================================================================
        // RendererPath mismatch 判定 (AnimationClip)
        // ====================================================================

        private string BuildRendererPathMismatchMessage(AnimationClip clip)
        {
            if (clip == null || _sampler == null) return null;
            var profileSO = target as FacialCharacterProfileSO;
            var referenceModel = profileSO != null ? profileSO.ReferenceModel : null;
            if (referenceModel == null) return null;

            HashSet<string> modelPaths;
            try { modelPaths = CollectReferenceModelRendererPaths(referenceModel); }
            catch (Exception) { return null; }
            if (modelPaths.Count == 0)
            {
                return $"参照モデル '{referenceModel.name}' に SkinnedMeshRenderer が見つかりません。";
            }

            List<string> rendererPaths;
            try
            {
                var summary = _sampler.SampleSummary(clip);
                rendererPaths = summary.RendererPaths != null ? new List<string>(summary.RendererPaths) : new List<string>();
            }
            catch (Exception) { return null; }

            var invalid = new List<string>();
            for (int i = 0; i < rendererPaths.Count; i++)
            {
                var path = rendererPaths[i] ?? string.Empty;
                if (!modelPaths.Contains(path)) invalid.Add(string.IsNullOrEmpty(path) ? "(ルート)" : path);
            }
            if (invalid.Count == 0) return null;

            return $"AnimationClip の RendererPath [{string.Join(", ", invalid)}] が参照モデル内の SkinnedMeshRenderer と一致しません。"
                + $" 参照モデル候補: [{string.Join(", ", modelPaths)}]";
        }

        private static HashSet<string> CollectReferenceModelRendererPaths(GameObject model)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (model == null) return result;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var smr = renderers[i];
                if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                var path = AnimationUtility.CalculateTransformPath(smr.transform, model.transform) ?? string.Empty;
                result.Add(path);
            }
            return result;
        }

        // ====================================================================
        // AnimationClip 表示用 ObjectField
        // ====================================================================

        /// <summary>
        /// AnimationClip 名ではなく、プロジェクトのファイル名（拡張子なし）を表示するカスタム ObjectField。
        /// AnimationClip を複製・編集した場合に object 名がファイル名と一致しなくなる Unity の挙動を回避する。
        /// </summary>
        private sealed class ExpressionClipObjectField : ObjectField
        {
            public Action<UnityEngine.Object> OnValueAssigned;

            public override void SetValueWithoutNotify(UnityEngine.Object newValue)
            {
                var previous = value;
                base.SetValueWithoutNotify(newValue);
                if (!ReferenceEquals(previous, newValue))
                {
                    MarkDirtyRepaint();
                    OnValueAssigned?.Invoke(newValue);
                }
                RefreshDisplayLabel();
            }

            public void RefreshDisplayLabel()
            {
                var clip = value as AnimationClip;
                var labelToSet = ResolveDisplayLabel(clip);
                if (string.IsNullOrEmpty(labelToSet)) return;

                // ObjectField 内部の表示テキスト要素を ".unity-object-field-display__label" で取得して上書き
                var displayLabel = this.Q<Label>(className: "unity-object-field-display__label");
                if (displayLabel != null)
                {
                    displayLabel.text = labelToSet;
                }
            }

            private static string ResolveDisplayLabel(AnimationClip clip)
            {
                if (clip == null) return string.Empty;
                var path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path)) return clip.name;
                var fileName = Path.GetFileNameWithoutExtension(path);
                return string.IsNullOrEmpty(fileName) ? clip.name : fileName;
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
