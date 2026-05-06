using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.AutoExport;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Inspector.AdapterBindings;
using Hidano.FacialControl.Editor.Sampling;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// <see cref="FacialCharacterProfileSO"/> 系アセットの汎用 UI Toolkit カスタム Inspector。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 入力方式 (InputSystem / OSC / ARKit 等) には依存しない汎用 UI を提供する。
    /// Layers / Expressions / Reference Model / Debug 表示と自動保存（profile.json）を担当する。
    /// </para>
    /// <para>
    /// 派生クラスは <c>OnResolveDerivedSerializedProperties</c> / <c>OnBuildPreLayersSections</c> /
    /// <c>OnBuildAnalogExpressionInputSourceFields</c> / <c>FlushAutoExport</c> /
    /// <c>FindGazeConfigsProperty</c> / <c>ResolveAnalogSourceIdChoices</c> をオーバーライドして
    /// 入力源固有 UI（InputActionAsset 選択、ExpressionBindings、analog_bindings.json 出力など）を追加する。
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterProfileSO), editorForChildClasses: true)]
    public class FacialCharacterProfileSOInspector : UnityEditor.Editor
    {
        // ====================================================================
        // VisualElement の name 定数（汎用部分）
        // ====================================================================

        public const string LayersFoldoutName = "facial-character-layers-foldout";
        public const string GazeConfigsFoldoutName = "facial-character-gaze-configs-foldout";
        public const string DebugFoldoutName = "facial-character-debug-foldout";
        public const string AdapterBindingsFoldoutName = "facial-character-adapter-bindings-foldout";

        public const string SaveStatusBarName = "facial-character-save-status-bar";
        public const string SaveButtonName = "facial-character-save-button";
        public const string SaveStatusLabelName = "facial-character-save-status";
        public const string ExpressionsValidationHelpName = "facial-character-expressions-validation";

        public const string ReferenceModelFoldoutName = "facial-character-reference-model-foldout";

        public const string ExpressionRowIdLabelName = "expression-row-id-label";
        public const string ExpressionRowNameFieldName = "expression-row-name-field";
        public const string ExpressionRowKindDropdownName = "expression-row-kind-dropdown";
        public const string ExpressionRowClipFieldName = "expression-row-clip-field";
        public const string ExpressionRowRendererSummaryName = "expression-row-renderer-summary";
        public const string ExpressionRowValidationHelpName = "expression-row-validation-help";
        public const string ExpressionRowTransitionDurationFieldName = "expression-row-transition-duration-field";
        public const string ExpressionRowGazeLeftBonePathName = "expression-row-gaze-left-bone-path";
        public const string ExpressionRowGazeLeftInitRotName = "expression-row-gaze-left-init-rot";
        public const string ExpressionRowGazeRightBonePathName = "expression-row-gaze-right-bone-path";
        public const string ExpressionRowGazeRightInitRotName = "expression-row-gaze-right-init-rot";
        public const string ExpressionRowGazeLookLeftClipName = "expression-row-gaze-look-left-clip";
        public const string ExpressionRowGazeLookRightClipName = "expression-row-gaze-look-right-clip";
        public const string ExpressionRowGazeLookUpClipName = "expression-row-gaze-look-up-clip";
        public const string ExpressionRowGazeLookDownClipName = "expression-row-gaze-look-down-clip";
        public const string ExpressionRowGazeAutoAssignButtonName = "expression-row-gaze-auto-assign-button";
        public const string GazeConfigAddDropdownName = "gaze-config-add-dropdown";
        public const string GazeConfigBulkResolveButtonName = "gaze-config-bulk-resolve-button";
        public const string GazeConfigNoCandidatesLabel = "追加できる Analog Expression はありません";
        public const string GazeConfigRowName = "gaze-config-row";
        public const string GazeConfigExpressionNameLabelName = "gaze-config-expression-name";
        public const string GazeConfigLeftBonePathFieldName = "gaze-config-left-bone-path";
        public const string GazeConfigRightBonePathFieldName = "gaze-config-right-bone-path";
        public const string GazeConfigLookUpAngleFieldName = "gaze-config-look-up-angle";
        public const string GazeConfigLookDownAngleFieldName = "gaze-config-look-down-angle";
        public const string GazeConfigOuterYawAngleFieldName = "gaze-config-outer-yaw-angle";
        public const string GazeConfigInnerYawAngleFieldName = "gaze-config-inner-yaw-angle";
        public const string GazeConfigLookLeftClipFieldName = "gaze-config-look-left-clip";
        public const string GazeConfigLookRightClipFieldName = "gaze-config-look-right-clip";
        public const string GazeConfigLookUpClipFieldName = "gaze-config-look-up-clip";
        public const string GazeConfigLookDownClipFieldName = "gaze-config-look-down-clip";
        public const string GazeConfigAutoAssignButtonName = "gaze-config-auto-assign-button";
        public const string GazeConfigRemoveButtonName = "gaze-config-remove-button";

        // ====================================================================
        // 共通スタイル定数
        // ====================================================================

        protected const int HelpBoxFontSize = 12;
        protected const int SectionFoldoutFontSize = 13;

        // ====================================================================
        // SerializedProperty（汎用部分）
        // ====================================================================

        protected SerializedProperty _layersProperty;
        protected SerializedProperty _expressionsProperty;
        protected SerializedProperty _schemaVersionProperty;
        protected SerializedProperty _adapterBindingsProperty;

#if UNITY_EDITOR
        protected SerializedProperty _referenceModelProperty;
#endif

        /// <summary>
        /// 派生クラスが提供する <c>_gazeConfigs</c> SerializedProperty。
        /// 既定は <see cref="FindGazeConfigsProperty"/> の戻り値を保持する。null の場合は
        /// アナログ表情の bone/clip フィールドは表示されない。
        /// </summary>
        protected SerializedProperty _gazeConfigsProperty;

        // ====================================================================
        // VisualElement キャッシュ
        // ====================================================================

        private Label _debugSchemaVersionLabel;
        private Label _debugLayerCountLabel;
        private Label _debugExpressionCountLabel;
        private Label _debugJsonPathLabel;
        private HelpBox _expressionsValidationHelp;
        private Label _saveStatusLabel;
        private VisualElement _layersContainer;
        private VisualElement _gazeConfigsContainer;
        private Button _gazeConfigBulkResolveButton;

        // ====================================================================
        // 候補リスト
        // ====================================================================

        protected readonly List<string> _layerNameChoices = new List<string>();

        protected IExpressionAnimationClipSampler _sampler;
        private bool _autoSavePending;
#if UNITY_EDITOR
        private GameObject _lastReferenceModel;
#endif

        // ====================================================================
        // Editor lifecycle
        // ====================================================================

        public override VisualElement CreateInspectorGUI()
        {
            ResolveSerializedProperties();
            OnResolveDerivedSerializedProperties();
            _gazeConfigsProperty = FindGazeConfigsProperty();
            _sampler = new AnimationClipExpressionSampler();

            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            BuildSaveStatusBar(root);
            BuildReferenceModelSection(root);
            BuildLayersSection(root);
            BuildGazeConfigsSection(root);
            OnBuildPreLayersSections(root);
            BuildAdapterBindingsSection(root);
            BuildDebugSection(root);

            RefreshLayerNameChoices();
            UpdateValidation();
            UpdateDebugLabels();

            // 自動保存: SerializedObject の変更を監視
            root.TrackSerializedObjectValue(serializedObject, _ => OnSerializedObjectChanged());

#if UNITY_EDITOR
            TrackReferenceModelChanges(root);
#endif

            return root;
        }

        private void ResolveSerializedProperties()
        {
            _layersProperty = serializedObject.FindProperty("_layers");
            _expressionsProperty = serializedObject.FindProperty("_expressions");
            _schemaVersionProperty = serializedObject.FindProperty("_schemaVersion");
            _adapterBindingsProperty = serializedObject.FindProperty("_adapterBindings");

#if UNITY_EDITOR
            _referenceModelProperty = serializedObject.FindProperty("_referenceModel");
#endif
        }

        // ====================================================================
        // 派生クラス向け hook
        // ====================================================================

        /// <summary>派生クラスで追加のシリアライズプロパティを解決する。</summary>
        protected virtual void OnResolveDerivedSerializedProperties() { }

        /// <summary>Layers セクションの直前に追加するセクション群（入力源固有 UI）。</summary>
        protected virtual void OnBuildPreLayersSections(VisualElement root) { }

        /// <summary>
        /// アナログ表情行に追加する入力源固有フィールド（例: InputActionReference）。
        /// </summary>
        /// <param name="row">表情行のルート要素。</param>
        /// <param name="exprIndex"><see cref="_expressionsProperty"/> 配下のインデックス。</param>
        /// <param name="gazeConfigProperty">対応する <c>_gazeConfigs[index]</c> SerializedProperty。なければ null。</param>
        protected virtual void OnBuildAnalogExpressionInputSourceFields(
            VisualElement row, int exprIndex, SerializedProperty gazeConfigProperty)
        { }

        /// <summary>
        /// 自動保存時の永続化処理。既定では profile.json + AnimationClip サンプリングのみ。
        /// 派生は <c>base.FlushAutoExport(...)</c> を呼び出してから追加処理（例: analog_bindings.json）を行う。
        /// </summary>
        protected virtual void FlushAutoExport(
            FacialCharacterProfileSO so, IExpressionAnimationClipSampler sampler)
        {
            FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);
            FacialCharacterProfileExporter.ExportProfileJson(so);
        }

        /// <summary>
        /// <c>_gazeConfigs</c> SerializedProperty を派生から提供する。
        /// 既定は null（GazeBinding 機能を持たない SO 派生用）。
        /// 返す SerializedProperty は <c>List&lt;GazeBindingConfig&gt;</c> またはその派生型をシリアライズする。
        /// </summary>
        protected virtual SerializedProperty FindGazeConfigsProperty()
            => serializedObject.FindProperty("_gazeConfigs");

        /// <summary>
        /// アナログ表情の入力源 ID 候補（例: ActionName）。既定は空配列。
        /// </summary>
        protected virtual IReadOnlyList<string> ResolveAnalogSourceIdChoices()
            => Array.Empty<string>();

        // ====================================================================
        // 自動保存
        // ====================================================================

        private void BuildSaveStatusBar(VisualElement root)
        {
            var bar = new VisualElement
            {
                name = SaveStatusBarName,
            };
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 6;
            bar.style.paddingRight = 6;
            bar.style.paddingTop = 4;
            bar.style.paddingBottom = 4;
            bar.style.marginBottom = 6;
            bar.style.borderBottomColor = new StyleColor(new Color(0.28f, 0.28f, 0.28f, 0.45f));
            bar.style.borderBottomWidth = 1;

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
                    FlushAutoExport(profileSO, _sampler ?? new AnimationClipExpressionSampler());
                }
                AssetDatabase.SaveAssetIfDirty(target);

                if (_saveStatusLabel != null)
                {
                    _saveStatusLabel.text = $"保存しました ({DateTime.Now:HH:mm:ss})";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FacialCharacterProfileSOInspector: 自動保存に失敗しました: {ex.Message}");
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

        protected static HelpBox MakeHelpBox(string text, HelpBoxMessageType messageType = HelpBoxMessageType.Info)
        {
            var box = new HelpBox(text, messageType);
            box.style.fontSize = HelpBoxFontSize;
            box.style.marginTop = 2;
            box.style.marginBottom = 4;
            return box;
        }

        protected static Foldout MakeSectionFoldout(string name, string text, bool open)
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
        // Section: Adapter Bindings（[SerializeReference] AdapterBindingBase list）
        // ====================================================================

        private void BuildAdapterBindingsSection(VisualElement root)
        {
            if (_adapterBindingsProperty == null) return;

            var foldout = MakeSectionFoldout(AdapterBindingsFoldoutName, "Adapter Bindings", open: true);
            foldout.Add(MakeHelpBox(
                "入力源（OSC / Input System / ARKit など）の Adapter Binding を登録します。"
                + "Add ボタンから利用可能な binding を追加し、各 binding は SO 内に直接保存されます。"));

            var listView = new AdapterBindingsListView(_adapterBindingsProperty);
            listView.Bind(serializedObject);
            foldout.Add(listView);

            root.Add(foldout);
        }

        // ====================================================================
        // Section: GazeConfigs
        // ====================================================================

        private void BuildGazeConfigsSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(GazeConfigsFoldoutName, "GazeConfigs", open: true);

            _gazeConfigBulkResolveButton = new Button(ResolveAllGazeConfigsFromReferenceModel)
            {
                name = GazeConfigBulkResolveButtonName,
                text = "全 GazeConfig を参照モデルから再解決",
                tooltip = "全ての GazeConfig を現在の参照モデルから再解決し、既存値を上書きします。",
            };
            _gazeConfigBulkResolveButton.SetEnabled(HasReferenceModel());
            _gazeConfigBulkResolveButton.style.alignSelf = Align.FlexStart;
            _gazeConfigBulkResolveButton.style.marginBottom = 4;
            foldout.Add(_gazeConfigBulkResolveButton);

            _gazeConfigsContainer = new VisualElement();
            _gazeConfigsContainer.style.flexDirection = FlexDirection.Column;
            foldout.Add(_gazeConfigsContainer);

            RebuildGazeConfigsUI();

            root.Add(foldout);
        }

        private void RebuildGazeConfigsUI()
        {
            if (_gazeConfigsContainer == null) return;

            _gazeConfigsContainer.Clear();

            if (_gazeConfigsProperty == null)
            {
                _gazeConfigsContainer.Add(MakeHelpBox("GazeConfig の保存先が見つかりません。", HelpBoxMessageType.Warning));
                return;
            }

            serializedObject.Update();

            _gazeConfigsContainer.Add(BuildGazeConfigAddDropdown());

            for (int i = 0; i < _gazeConfigsProperty.arraySize; i++)
            {
                int configIndex = i;
                _gazeConfigsContainer.Add(BuildGazeConfigRow(configIndex));
            }
        }

        private VisualElement BuildGazeConfigAddDropdown()
        {
            var candidates = CollectAddableGazeConfigCandidates();
            if (candidates.Count == 0)
            {
                var disabledDropdown = new DropdownField("+ GazeConfig を追加")
                {
                    name = GazeConfigAddDropdownName,
                    choices = new List<string> { GazeConfigNoCandidatesLabel },
                };
                disabledDropdown.SetValueWithoutNotify(GazeConfigNoCandidatesLabel);
                disabledDropdown.SetEnabled(false);
                disabledDropdown.style.marginBottom = 6;
                return disabledDropdown;
            }

            var choices = new List<string>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                choices.Add(candidates[i].MenuLabel);
            }

            var dropdown = new DropdownField("+ GazeConfig を追加")
            {
                name = GazeConfigAddDropdownName,
                choices = choices,
            };
            dropdown.SetValueWithoutNotify(null);
            dropdown.index = -1;
            dropdown.style.marginBottom = 6;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int selectedIndex = choices.IndexOf(evt.newValue);
                if (selectedIndex < 0) return;

                AddGazeConfigFromCandidate(candidates[selectedIndex].ExpressionId);
            });
            return dropdown;
        }

        private VisualElement BuildGazeConfigRow(int configIndex)
        {
            var cfgProp = _gazeConfigsProperty.GetArrayElementAtIndex(configIndex);
            var expressionIdProp = cfgProp.FindPropertyRelative("expressionId");
            string expressionId = expressionIdProp != null ? expressionIdProp.stringValue : string.Empty;

            var row = new VisualElement
            {
                name = GazeConfigRowName,
                userData = expressionId,
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
            row.style.borderLeftWidth = 2;

            var expressionNameLabel = new Label(FindExpressionNameById(expressionId))
            {
                name = GazeConfigExpressionNameLabelName,
                tooltip = expressionId,
            };
            expressionNameLabel.style.minWidth = 120;
            expressionNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(expressionNameLabel);

            AddBoundTextField(row, cfgProp, "leftEyeBonePath", "左目", GazeConfigLeftBonePathFieldName, 120);
            AddBoundTextField(row, cfgProp, "rightEyeBonePath", "右目", GazeConfigRightBonePathFieldName, 120);

            AddBoundFloatField(row, cfgProp, "lookUpAngle", "上", GazeConfigLookUpAngleFieldName, 48);
            AddBoundFloatField(row, cfgProp, "lookDownAngle", "下", GazeConfigLookDownAngleFieldName, 48);
            AddBoundFloatField(row, cfgProp, "outerYawAngle", "外", GazeConfigOuterYawAngleFieldName, 48);
            AddBoundFloatField(row, cfgProp, "innerYawAngle", "内", GazeConfigInnerYawAngleFieldName, 48);

            AddBoundClipField(row, cfgProp, "lookLeftClip", "左Clip", GazeConfigLookLeftClipFieldName, 92);
            AddBoundClipField(row, cfgProp, "lookRightClip", "右Clip", GazeConfigLookRightClipFieldName, 92);
            AddBoundClipField(row, cfgProp, "lookUpClip", "上Clip", GazeConfigLookUpClipFieldName, 92);
            AddBoundClipField(row, cfgProp, "lookDownClip", "下Clip", GazeConfigLookDownClipFieldName, 92);

            var autoAssignButton = new Button(() => ResolveGazeConfigFromReferenceModel(configIndex))
            {
                name = GazeConfigAutoAssignButtonName,
                text = "参照モデルから自動設定",
                tooltip = "現在の参照モデルからこの GazeConfig を再解決し、既存値を上書きします。",
            };
            autoAssignButton.SetEnabled(HasReferenceModel());
            autoAssignButton.style.marginLeft = 4;
            row.Add(autoAssignButton);

            var removeButton = new Button(() => RemoveGazeConfigAt(configIndex))
            {
                name = GazeConfigRemoveButtonName,
                text = "削除",
            };
            removeButton.style.marginLeft = 4;
            row.Add(removeButton);

            return row;
        }

        private void AddBoundTextField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName,
            float width)
        {
            var prop = cfgProp.FindPropertyRelative(propertyName);
            if (prop == null) return;

            var field = new TextField(label)
            {
                name = elementName,
            };
            field.BindProperty(prop);
            field.style.width = width;
            field.style.marginLeft = 4;
            row.Add(field);
        }

        private void AddBoundFloatField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName,
            float width)
        {
            var prop = cfgProp.FindPropertyRelative(propertyName);
            if (prop == null) return;

            var field = new FloatField(label)
            {
                name = elementName,
            };
            field.BindProperty(prop);
            field.style.width = width;
            field.style.marginLeft = 4;
            row.Add(field);
        }

        private void AddBoundClipField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName,
            float width)
        {
            var prop = cfgProp.FindPropertyRelative(propertyName);
            if (prop == null) return;

            var field = new ObjectField(label)
            {
                name = elementName,
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            field.BindProperty(prop);
            field.style.width = width;
            field.style.marginLeft = 4;
            row.Add(field);
        }

        private List<GazeConfigCandidate> CollectAddableGazeConfigCandidates()
        {
            var candidates = new List<GazeConfigCandidate>();
            if (_expressionsProperty == null || _gazeConfigsProperty == null) return candidates;

            var configuredIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _gazeConfigsProperty.arraySize; i++)
            {
                var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(i);
                var idProp = cfg.FindPropertyRelative("expressionId");
                if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                {
                    configuredIds.Add(idProp.stringValue);
                }
            }

            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var expr = _expressionsProperty.GetArrayElementAtIndex(i);
                var kindProp = expr.FindPropertyRelative("kind");
                if (kindProp == null || (ExpressionKind)kindProp.enumValueIndex != ExpressionKind.Analog) continue;

                var idProp = expr.FindPropertyRelative("id");
                string expressionId = idProp != null ? idProp.stringValue : string.Empty;
                if (string.IsNullOrEmpty(expressionId) || configuredIds.Contains(expressionId)) continue;

                string expressionName = ReadStringProperty(expr, "name");
                candidates.Add(new GazeConfigCandidate(
                    expressionId,
                    BuildExpressionMenuLabel(expressionName, expressionId)));
            }

            return candidates;
        }

        private void AddGazeConfigFromCandidate(string expressionId)
        {
            if (string.IsNullOrEmpty(expressionId) || _gazeConfigsProperty == null) return;

            serializedObject.Update();
            if (FindGazeConfigIndexByExpressionId(expressionId) >= 0)
            {
                RebuildGazeConfigsUI();
                return;
            }

            int newIndex = _gazeConfigsProperty.arraySize;
            _gazeConfigsProperty.InsertArrayElementAtIndex(newIndex);
            var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(newIndex);
            ResetGazeConfigToDefaults(cfg);

            var idProp = cfg.FindPropertyRelative("expressionId");
            if (idProp != null) idProp.stringValue = expressionId;

            serializedObject.ApplyModifiedProperties();
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        private void RemoveGazeConfigAt(int configIndex)
        {
            if (_gazeConfigsProperty == null) return;

            serializedObject.Update();
            if (configIndex < 0 || configIndex >= _gazeConfigsProperty.arraySize) return;

            _gazeConfigsProperty.DeleteArrayElementAtIndex(configIndex);
            serializedObject.ApplyModifiedProperties();
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        private static void ResetGazeConfigToDefaults(SerializedProperty cfg)
        {
            SetString(cfg, "expressionId", string.Empty);
            SetString(cfg, "leftEyeBonePath", string.Empty);
            SetString(cfg, "rightEyeBonePath", string.Empty);
            SetVector3(cfg, "leftEyeInitialRotation", Vector3.zero);
            SetVector3(cfg, "rightEyeInitialRotation", Vector3.zero);
            SetVector3(cfg, "leftEyeYawAxisLocal", Vector3.up);
            SetVector3(cfg, "rightEyeYawAxisLocal", Vector3.up);
            SetVector3(cfg, "leftEyePitchAxisLocal", Vector3.right);
            SetVector3(cfg, "rightEyePitchAxisLocal", Vector3.right);
            SetFloat(cfg, "lookUpAngle", 15f);
            SetFloat(cfg, "lookDownAngle", 9f);
            SetFloat(cfg, "outerYawAngle", 15f);
            SetFloat(cfg, "innerYawAngle", 18f);
            SetObject(cfg, "lookLeftClip", null);
            SetObject(cfg, "lookRightClip", null);
            SetObject(cfg, "lookUpClip", null);
            SetObject(cfg, "lookDownClip", null);
            ClearArray(cfg, "lookLeftSamples");
            ClearArray(cfg, "lookRightSamples");
            ClearArray(cfg, "lookUpSamples");
            ClearArray(cfg, "lookDownSamples");
        }

        private string FindExpressionNameById(string expressionId)
        {
            if (_expressionsProperty == null || string.IsNullOrEmpty(expressionId)) return "(Expression 不明)";

            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var expr = _expressionsProperty.GetArrayElementAtIndex(i);
                var idProp = expr.FindPropertyRelative("id");
                if (idProp == null || !string.Equals(idProp.stringValue, expressionId, StringComparison.Ordinal)) continue;

                string expressionName = ReadStringProperty(expr, "name");
                return string.IsNullOrWhiteSpace(expressionName) ? expressionId : expressionName;
            }

            return $"(Expression 不明: {expressionId})";
        }

        private static string BuildExpressionMenuLabel(string expressionName, string expressionId)
        {
            if (string.IsNullOrWhiteSpace(expressionName)) return expressionId ?? string.Empty;
            return $"{expressionName} [{expressionId}]";
        }

        private static string ReadStringProperty(SerializedProperty owner, string propertyName)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            return prop != null ? prop.stringValue : string.Empty;
        }

        private static void SetString(SerializedProperty owner, string propertyName, string value)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetVector3(SerializedProperty owner, string propertyName, Vector3 value)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            if (prop != null) prop.vector3Value = value;
        }

        private static void SetFloat(SerializedProperty owner, string propertyName, float value)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetObject(SerializedProperty owner, string propertyName, UnityEngine.Object value)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void ClearArray(SerializedProperty owner, string propertyName)
        {
            var prop = owner.FindPropertyRelative(propertyName);
            if (prop != null && prop.isArray) prop.ClearArray();
        }

        private void ResolveGazeConfigFromReferenceModel(int configIndex)
        {
            if (!HasReferenceModel() || _gazeConfigsProperty == null) return;

            serializedObject.Update();
            if (configIndex < 0 || configIndex >= _gazeConfigsProperty.arraySize) return;

            AssignGazeConfigFromReferenceModel(
                _gazeConfigsProperty.GetArrayElementAtIndex(configIndex),
                resetRangesToDefaults: true);
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        private void ResolveAllGazeConfigsFromReferenceModel()
        {
            if (!HasReferenceModel() || _gazeConfigsProperty == null) return;

            serializedObject.Update();
            int configCount = _gazeConfigsProperty.arraySize;
            for (int i = 0; i < configCount; i++)
            {
                AssignGazeConfigFromReferenceModel(
                    _gazeConfigsProperty.GetArrayElementAtIndex(i),
                    resetRangesToDefaults: true);
                serializedObject.Update();
            }

            RebuildGazeConfigsUI();
            UpdateValidation();
        }

#if UNITY_EDITOR
        private void TrackReferenceModelChanges(VisualElement root)
        {
            if (root == null || _referenceModelProperty == null) return;

            _lastReferenceModel = GetReferenceModel();
            root.TrackPropertyValue(_referenceModelProperty, _ => OnReferenceModelPropertyChanged());
        }

        private void OnReferenceModelPropertyChanged()
        {
            var previousReferenceModel = _lastReferenceModel;
            var currentReferenceModel = GetReferenceModel();
            _lastReferenceModel = currentReferenceModel;

            UpdateGazeConfigResolveButtonStates();
            if (currentReferenceModel == null || currentReferenceModel == previousReferenceModel) return;

            AutoFillEmptyGazeConfigsFromReferenceModel();
        }

        private void AutoFillEmptyGazeConfigsFromReferenceModel()
        {
            if (!HasReferenceModel() || _gazeConfigsProperty == null) return;

            serializedObject.Update();
            int configCount = _gazeConfigsProperty.arraySize;
            for (int i = 0; i < configCount; i++)
            {
                var cfg = _gazeConfigsProperty.GetArrayElementAtIndex(i);
                var leftBonePathProp = cfg.FindPropertyRelative("leftEyeBonePath");
                var rightBonePathProp = cfg.FindPropertyRelative("rightEyeBonePath");
                if (!IsEmptyString(leftBonePathProp) || !IsEmptyString(rightBonePathProp)) continue;

                AssignGazeConfigFromReferenceModel(cfg, resetRangesToDefaults: false);
                serializedObject.Update();
            }

            RebuildGazeConfigsUI();
            UpdateValidation();
        }
#endif

        private void AssignGazeConfigFromReferenceModel(
            SerializedProperty cfg,
            bool resetRangesToDefaults)
        {
            if (cfg == null) return;

            AutoAssignGazeBonesFromReferenceModel(
                cfg.FindPropertyRelative("leftEyeBonePath"),
                cfg.FindPropertyRelative("leftEyeInitialRotation"),
                cfg.FindPropertyRelative("rightEyeBonePath"),
                cfg.FindPropertyRelative("rightEyeInitialRotation"),
                cfg.FindPropertyRelative("leftEyeYawAxisLocal"),
                cfg.FindPropertyRelative("leftEyePitchAxisLocal"),
                cfg.FindPropertyRelative("rightEyeYawAxisLocal"),
                cfg.FindPropertyRelative("rightEyePitchAxisLocal"));

            if (!resetRangesToDefaults) return;

            SetDefaultGazeConfigRanges(cfg);
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetDefaultGazeConfigRanges(SerializedProperty cfg)
        {
            SetFloat(cfg, "lookUpAngle", 15f);
            SetFloat(cfg, "lookDownAngle", 9f);
            SetFloat(cfg, "outerYawAngle", 15f);
            SetFloat(cfg, "innerYawAngle", 18f);
        }

        private void UpdateGazeConfigResolveButtonStates()
        {
            bool hasReferenceModel = HasReferenceModel();
            if (_gazeConfigBulkResolveButton != null)
            {
                _gazeConfigBulkResolveButton.SetEnabled(hasReferenceModel);
            }

            if (_gazeConfigsContainer == null) return;
            _gazeConfigsContainer.Query<Button>(GazeConfigAutoAssignButtonName).ForEach(
                button => button.SetEnabled(hasReferenceModel));
        }

        private bool HasReferenceModel()
        {
            return GetReferenceModel() != null;
        }

        private GameObject GetReferenceModel()
        {
#if UNITY_EDITOR
            return _referenceModelProperty != null
                ? _referenceModelProperty.objectReferenceValue as GameObject
                : null;
#else
            return null;
#endif
        }

        private static bool IsEmptyString(SerializedProperty prop)
        {
            return prop == null || string.IsNullOrEmpty(prop.stringValue);
        }

        private struct GazeConfigCandidate
        {
            public readonly string ExpressionId;
            public readonly string MenuLabel;

            public GazeConfigCandidate(string expressionId, string menuLabel)
            {
                ExpressionId = expressionId;
                MenuLabel = menuLabel;
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
            RebuildGazeConfigsUI();
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
            RebuildGazeConfigsUI();
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

            serializedObject.ApplyModifiedProperties();

            RebuildLayersUI();
            RebuildGazeConfigsUI();
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
                    if (newKind == ExpressionKind.Digital)
                    {
                        RemoveGazeConfigByExpressionId(idForCfg);
                    }
                    RebuildLayersUI();
                    RebuildGazeConfigsUI();
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

            // GazeConfig は専用セクションで opt-in 編集するため、Expression 行では共通 clip のみ表示する。
            BuildAnimationClipFields(row, exprIndex);

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
            // GazeConfig を派生で扱わない（_gazeConfigsProperty == null）の場合は何も出さない。
            if (_gazeConfigsProperty == null) return;

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
            var leftBonePathProp = cfgProp.FindPropertyRelative("leftEyeBonePath");
            var leftInitRotProp = cfgProp.FindPropertyRelative("leftEyeInitialRotation");
            var rightBonePathProp = cfgProp.FindPropertyRelative("rightEyeBonePath");
            var rightInitRotProp = cfgProp.FindPropertyRelative("rightEyeInitialRotation");
            var lookLeftClipProp = cfgProp.FindPropertyRelative("lookLeftClip");
            var lookRightClipProp = cfgProp.FindPropertyRelative("lookRightClip");
            var lookUpClipProp = cfgProp.FindPropertyRelative("lookUpClip");
            var lookDownClipProp = cfgProp.FindPropertyRelative("lookDownClip");

            // 派生クラスへ入力源固有フィールド (例: InputActionReference) の追加機会を提供する。
            OnBuildAnalogExpressionInputSourceFields(row, exprIndex, cfgProp);

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

            var leftYawAxisProp = cfgProp.FindPropertyRelative("leftEyeYawAxisLocal");
            var leftPitchAxisProp = cfgProp.FindPropertyRelative("leftEyePitchAxisLocal");
            var rightYawAxisProp = cfgProp.FindPropertyRelative("rightEyeYawAxisLocal");
            var rightPitchAxisProp = cfgProp.FindPropertyRelative("rightEyePitchAxisLocal");
            var outerYawProp = cfgProp.FindPropertyRelative("outerYawAngle");
            var innerYawProp = cfgProp.FindPropertyRelative("innerYawAngle");
            var lookUpAngleProp = cfgProp.FindPropertyRelative("lookUpAngle");
            var lookDownAngleProp = cfgProp.FindPropertyRelative("lookDownAngle");

            var autoAssignButton = new Button(() => AutoAssignGazeBonesFromReferenceModel(
                leftBonePathProp, leftInitRotProp,
                rightBonePathProp, rightInitRotProp,
                leftYawAxisProp, leftPitchAxisProp,
                rightYawAxisProp, rightPitchAxisProp))
            {
                name = ExpressionRowGazeAutoAssignButtonName,
                text = "参照モデルから自動設定",
                tooltip = "参照モデルの Animator から左右目ボーンを解決し、ボーン名と現在の localEulerAngles を初期回転として書き込みます。"
                    + " さらに、世界の上下/左右軸が各目ボーンの local 空間でどの方向に対応するかを算出し、yaw/pitch 軸として保存します。"
                    + " Humanoid Avatar が設定されている場合は LeftEye / RightEye マッピングを優先し、不在時は名前検索 (LeftEye / RightEye / *eye*) でフォールバックします。",
            };
            autoAssignButton.style.marginTop = 4;
            boneSection.Add(autoAssignButton);

            row.Add(boneSection);

            // ----- 可動範囲 (角度制限) -----
            var rangeSection = new Foldout
            {
                text = "可動範囲 (角度制限)",
                value = true,
            };
            rangeSection.style.marginTop = 4;
            rangeSection.Add(MakeHelpBox(
                "左右目共通の上下角度と、左右目それぞれの「外側 (鼻から離れる側)」「内側 (鼻に近づく側)」"
                + " の最大角度を指定します。例: 向かって左に視線を送るとき、向かって左の眼は外側、"
                + " 向かって右の眼は内側の値で動きます。"));
            if (lookUpAngleProp != null)
            {
                var f = new Slider("上方向の最大角度 (度)", 0f, 90f) { showInputField = true };
                f.BindProperty(lookUpAngleProp);
                rangeSection.Add(f);
            }
            if (lookDownAngleProp != null)
            {
                var f = new Slider("下方向の最大角度 (度)", 0f, 90f) { showInputField = true };
                f.BindProperty(lookDownAngleProp);
                rangeSection.Add(f);
            }
            if (outerYawProp != null)
            {
                var f = new Slider("外側 (左右) の最大角度 (度)", 0f, 90f) { showInputField = true };
                f.BindProperty(outerYawProp);
                rangeSection.Add(f);
            }
            if (innerYawProp != null)
            {
                var f = new Slider("内側 (左右) の最大角度 (度)", 0f, 90f) { showInputField = true };
                f.BindProperty(innerYawProp);
                rangeSection.Add(f);
            }
            row.Add(rangeSection);

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
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        // ====================================================================
        // GazeConfig ヘルパー
        // ====================================================================

        protected int FindGazeConfigIndexByExpressionId(string expressionId)
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

        protected bool HasGazeConfigForExpression(string expressionId)
        {
            return FindGazeConfigIndexByExpressionId(expressionId) >= 0;
        }

        protected void AppendGazeConfigForExpression(string expressionId)
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

        protected void RemoveGazeConfigByExpressionId(string expressionId)
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
            SerializedProperty rightInitRotProp,
            SerializedProperty leftYawAxisProp,
            SerializedProperty leftPitchAxisProp,
            SerializedProperty rightYawAxisProp,
            SerializedProperty rightPitchAxisProp)
        {
            if (_referenceModelProperty == null)
            {
                Debug.LogWarning("[FacialCharacterProfileSOInspector] 参照モデル property が解決できません。");
                return;
            }

            var referenceModel = _referenceModelProperty.objectReferenceValue as GameObject;
            if (referenceModel == null)
            {
                Debug.LogWarning(
                    "[FacialCharacterProfileSOInspector] 参照モデルが未割り当てです。"
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
                AssignParentLocalAxes(leftEye, leftYawAxisProp, leftPitchAxisProp);
                assigned++;
            }
            else
            {
                Debug.LogWarning("[FacialCharacterProfileSOInspector] 左目ボーンを参照モデルから解決できませんでした。Humanoid マッピング・命名規則を確認してください。");
            }

            if (rightEye != null)
            {
                if (rightBonePathProp != null) rightBonePathProp.stringValue = rightEye.name;
                if (rightInitRotProp != null) rightInitRotProp.vector3Value = rightEye.localEulerAngles;
                AssignParentLocalAxes(rightEye, rightYawAxisProp, rightPitchAxisProp);
                assigned++;
            }
            else
            {
                Debug.LogWarning("[FacialCharacterProfileSOInspector] 右目ボーンを参照モデルから解決できませんでした。Humanoid マッピング・命名規則を確認してください。");
            }

            if (assigned > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log(
                    $"[FacialCharacterProfileSOInspector] 参照モデル '{referenceModel.name}' から目ボーンを自動設定しました"
                    + $" (Left: {(leftEye != null ? leftEye.name : "(skip)")}, Right: {(rightEye != null ? rightEye.name : "(skip)")}).");
            }
        }

        private static void AssignParentLocalAxes(
            Transform eyeBone,
            SerializedProperty yawAxisProp,
            SerializedProperty pitchAxisProp)
        {
            if (eyeBone == null) return;

            Vector3 yawAxisLocal = Vector3.up;
            Vector3 pitchAxisLocal = Vector3.right;

            var parent = eyeBone.parent;
            if (parent != null)
            {
                yawAxisLocal = parent.InverseTransformDirection(Vector3.up);
                pitchAxisLocal = parent.InverseTransformDirection(Vector3.right);
            }

            if (yawAxisLocal.sqrMagnitude > 1e-8f) yawAxisLocal = yawAxisLocal.normalized;
            if (pitchAxisLocal.sqrMagnitude > 1e-8f) pitchAxisLocal = pitchAxisLocal.normalized;

            if (yawAxisProp != null) yawAxisProp.vector3Value = yawAxisLocal;
            if (pitchAxisProp != null) pitchAxisProp.vector3Value = pitchAxisLocal;
        }

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
            var foldout = MakeSectionFoldout(ReferenceModelFoldoutName, "参照モデル", open: false);
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
            var so = target as FacialCharacterProfileSO;
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
                // _gazeConfigsProperty を持たない派生 (GazeBinding 機能なし) では analog 個別検証は省略する。
                if (_gazeConfigsProperty != null)
                {
                    int cfgIndex = FindGazeConfigIndexByExpressionId(idProp != null ? idProp.stringValue : string.Empty);
                    if (cfgIndex < 0)
                    {
                        messages.Add("アナログ操作の設定が見つかりません。");
                    }
                    else
                    {
                        var cfgProp = _gazeConfigsProperty.GetArrayElementAtIndex(cfgIndex);
                        var analogMessages = ValidateAnalogExpression(cfgProp);
                        if (analogMessages != null) messages.AddRange(analogMessages);

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

        /// <summary>
        /// 派生クラスがアナログ表情に対する追加バリデーションメッセージを返すフック。
        /// 既定は空配列。例: InputActionReference 未割り当てチェックなど。
        /// </summary>
        protected virtual IReadOnlyList<string> ValidateAnalogExpression(SerializedProperty gazeConfigProperty)
            => Array.Empty<string>();

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
                    Debug.LogWarning($"FacialCharacterProfileSOInspector: SampleSummary に失敗: {ex.Message}");
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
        // 共通 ListView ビルダ（派生クラスからも利用）
        // ====================================================================

        protected ListView BuildArrayListView(
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

        // ====================================================================
        // 候補キャッシュ更新
        // ====================================================================

        protected void RefreshLayerNameChoices()
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

        protected List<string> CollectExpressionIds()
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

        protected static List<string> BuildSafeChoices(IReadOnlyList<string> baseChoices, string currentValue)
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

        protected static int ReadMaskValueFromSerializedList(SerializedProperty listProp, IReadOnlyList<string> orderedLayerNames)
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

        protected static void WriteMaskValueToSerializedList(SerializedProperty listProp, int maskValue, IReadOnlyList<string> orderedLayerNames)
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
        protected sealed class ExpressionClipObjectField : ObjectField
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
    }
}
