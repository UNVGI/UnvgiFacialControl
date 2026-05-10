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
    /// <c>FlushAutoExport</c> をオーバーライドして入力源固有 UI や保存処理を追加する。
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterProfileSO), editorForChildClasses: true)]
    public class FacialCharacterProfileSOInspector : UnityEditor.Editor
    {
        // ====================================================================
        // VisualElement の name 定数（汎用部分）
        // ====================================================================

        public const string LayersFoldoutName = "facial-character-layers-foldout";
        public const string BaseExpressionFoldoutName = "facial-character-base-expression-foldout";
        public const string BaseExpressionClipFieldName = "facial-character-base-expression-clip-field";
        public const string BaseExpressionUnsetHelpName = "facial-character-base-expression-unset-help";
        public const string GazeConfigsFoldoutName = "facial-character-gaze-configs-foldout";
        public const string DebugFoldoutName = "facial-character-debug-foldout";
        public const string AdapterBindingsFoldoutName = "facial-character-adapter-bindings-foldout";

        public const string TabViewName = "facial-character-tabview";
        public const string TabExpressionsName = "facial-character-tab-expressions";
        public const string TabGazeName = "facial-character-tab-gaze";
        public const string TabAdapterBindingsName = "facial-character-tab-adapter-bindings";
        public const string TabDebugName = "facial-character-tab-debug";

        /// <summary>「目線」タブの表示文字列（参照モデル変更時にアスタリスクを付与する基準値）。</summary>
        public const string GazeTabBaseLabel = "目線";

        /// <summary>「目線」タブに付与する「未確認」マーカー（参照モデルが変わった直後に表示）。</summary>
        public const string GazeTabAttentionMarker = " *";

        public const string ReferenceModelDirectFieldName = "facial-character-reference-model-field";

        public const string SaveStatusBarName = "facial-character-save-status-bar";
        public const string SaveButtonName = "facial-character-save-button";
        public const string SaveStatusLabelName = "facial-character-save-status";
        public const string ExpressionsValidationHelpName = "facial-character-expressions-validation";
        public const string DebugExpressionIdMappingTitleName = "debug-expression-id-mapping-title";
        public const string DebugExpressionIdMappingName = "debug-expression-id-mapping";
        public const string DebugExpressionIdMappingRowName = "debug-expression-id-mapping-row";
        public const string DebugExpressionIdMappingNameCellName = "debug-expression-id-mapping-name";
        public const string DebugExpressionIdMappingExpressionIdCellName = "debug-expression-id-mapping-expression-id";
        public const string DebugExpressionIdMappingKindCellName = "debug-expression-id-mapping-kind";
        public const string DebugExpressionIdMappingLayerCellName = "debug-expression-id-mapping-layer";

        public const string ReferenceModelFoldoutName = "facial-character-reference-model-foldout";

        public const string ExpressionRowNameFieldName = "expression-row-name-field";
        public const string ExpressionRowIsGazeToggleName = "expression-row-is-gaze-toggle";
        public const string ExpressionRowClipFieldName = "expression-row-clip-field";
        public const string ExpressionRowRendererSummaryName = "expression-row-renderer-summary";
        public const string ExpressionRowValidationHelpName = "expression-row-validation-help";
        public const string ExpressionRowTransitionDurationFieldName = "expression-row-transition-duration-field";
        public const string GazeConfigAddDropdownName = "gaze-config-add-dropdown";
        public const string GazeConfigBulkResolveButtonName = "gaze-config-bulk-resolve-button";
        public const string GazeConfigNoCandidatesLabel = "追加できる目線操作の表情はありません";
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
        protected SerializedProperty _baseExpressionProperty;
        protected SerializedProperty _schemaVersionProperty;
        protected SerializedProperty _adapterBindingsProperty;

#if UNITY_EDITOR
        protected SerializedProperty _referenceModelProperty;
#endif

        /// <summary>SO ルート直下の <c>_gazeConfigs</c> SerializedProperty。</summary>
        protected SerializedProperty _rootGazeConfigsProperty;

        // ====================================================================
        // VisualElement キャッシュ
        // ====================================================================

        private Label _debugSchemaVersionLabel;
        private Label _debugLayerCountLabel;
        private Label _debugExpressionCountLabel;
        private Label _debugJsonPathLabel;
        private VisualElement _debugExpressionIdMappingContainer;
        private HelpBox _expressionsValidationHelp;
        private Label _saveStatusLabel;
        private VisualElement _layersContainer;
        private VisualElement _gazeConfigsContainer;
        private Tab _gazeTab;

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
            _rootGazeConfigsProperty = serializedObject.FindProperty("_gazeConfigs");
            _sampler = new AnimationClipExpressionSampler();

            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // BuildLayersSection は内部で MaskField の choices を _layerNameChoices から構築する。
            // そのため先に RefreshLayerNameChoices() を呼んで候補を確定させてから section を構築する。
            RefreshLayerNameChoices();

            BuildSaveStatusBar(root);

            // 参照モデルはタブの上に常時表示する（モデルタブを廃止してアクセス頻度を上げる）。
            BuildReferenceModelDirect(root);

            // 4 タブ構成: 表情 / 目線 / Adapter Bindings / Debug
            // タブ間で機能の住み分けを明示し、設定漏れを予防する。
            var tabView = new TabView { name = TabViewName };
            tabView.style.flexGrow = 1f;

            var expressionsTab = new Tab("表情") { name = TabExpressionsName };
            BuildLayersSection(expressionsTab.contentContainer);
            BuildBaseExpressionSection(expressionsTab.contentContainer);
            tabView.Add(expressionsTab);

            _gazeTab = new Tab(GazeTabBaseLabel) { name = TabGazeName };
            BuildGazeConfigsSection(_gazeTab.contentContainer);
            // 目線タブのヘッダーをクリックした瞬間にアスタリスクを消す（参照モデル変更後の確認注意）。
            // Tab 直下のクリックでは header subelement に届かないことがあるため、capture 段階で受ける。
            _gazeTab.RegisterCallback<PointerDownEvent>(_ => ClearGazeTabAttention(), TrickleDown.TrickleDown);
            tabView.Add(_gazeTab);

            var adapterTab = new Tab("Adapter Bindings") { name = TabAdapterBindingsName };
            OnBuildPreLayersSections(adapterTab.contentContainer);
            BuildAdapterBindingsSection(adapterTab.contentContainer);
            tabView.Add(adapterTab);

            var debugTab = new Tab("Debug") { name = TabDebugName };
            BuildDebugSection(debugTab.contentContainer);
            tabView.Add(debugTab);

            root.Add(tabView);

            UpdateValidation();
            UpdateDebugLabels();
            RebuildExpressionIdMapping();

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
            _baseExpressionProperty = serializedObject.FindProperty("_baseExpression");
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
        /// 自動保存時の永続化処理。既定では profile.json + AnimationClip サンプリングのみ。
        /// 派生は <c>base.FlushAutoExport(...)</c> を呼び出してから追加処理（例: analog_bindings.json）を行う。
        /// </summary>
        protected virtual void FlushAutoExport(
            FacialCharacterProfileSO so, IExpressionAnimationClipSampler sampler)
        {
            FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);
            FacialCharacterProfileExporter.ExportProfileJson(so);
        }

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
            RebuildExpressionIdMapping();
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
        // Section: ベース表情
        // ====================================================================

        private void BuildBaseExpressionSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(BaseExpressionFoldoutName, "ベース表情", open: true);

            if (_baseExpressionProperty == null)
            {
                foldout.Add(MakeHelpBox("ベース表情の保存先が見つかりません。", HelpBoxMessageType.Warning));
                root.Add(foldout);
                return;
            }

            var clipProp = _baseExpressionProperty.FindPropertyRelative("animationClip");
            if (clipProp == null)
            {
                foldout.Add(MakeHelpBox("ベース表情の AnimationClip 保存先が見つかりません。", HelpBoxMessageType.Warning));
                root.Add(foldout);
                return;
            }

            var clipField = new ObjectField("AnimationClip")
            {
                name = BaseExpressionClipFieldName,
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            clipField.BindProperty(clipProp);
            foldout.Add(clipField);

            var unsetHelp = MakeHelpBox(
                "ベース表情は、常時固定表情キャラや衣装固定 BlendShape など、"
                + "どのレイヤーも contribute しない BlendShape に残す初期値です。"
                + "AnimationClip を割り当てると自動保存時に cachedSnapshot が更新されます。");
            unsetHelp.name = BaseExpressionUnsetHelpName;
            SetBaseExpressionUnsetHelpVisibility(unsetHelp, clipProp.objectReferenceValue as AnimationClip);
            clipField.RegisterValueChangedCallback(evt =>
            {
                SetBaseExpressionUnsetHelpVisibility(unsetHelp, evt.newValue as AnimationClip);
            });
            foldout.Add(unsetHelp);

            root.Add(foldout);
        }

        private static void SetBaseExpressionUnsetHelpVisibility(HelpBox unsetHelp, AnimationClip clip)
        {
            if (unsetHelp == null) return;
            unsetHelp.style.display = clip == null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ====================================================================
        // Section: GazeConfigs
        // ====================================================================

        private void BuildGazeConfigsSection(VisualElement root)
        {
            var foldout = MakeSectionFoldout(GazeConfigsFoldoutName, "GazeConfigs", open: true);

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

            if (_rootGazeConfigsProperty == null)
            {
                _gazeConfigsContainer.Add(MakeHelpBox("GazeConfig の保存先が見つかりません。", HelpBoxMessageType.Warning));
                return;
            }

            serializedObject.Update();

            // GazeConfig はアナログ表情の追加に紐付けて自動生成する想定のため、
            // Inspector からの手動追加 dropdown は UI 上は表示しない（ユーザー要望 2026-05-09）。
            // ただし候補列挙ロジックや既存テストへの後方互換のため要素自体は構築しておく。
            VisualElement addDropdown = BuildGazeConfigAddDropdown();
            addDropdown.style.display = DisplayStyle.None;
            _gazeConfigsContainer.Add(addDropdown);

            for (int i = 0; i < _rootGazeConfigsProperty.arraySize; i++)
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
            var cfgProp = _rootGazeConfigsProperty.GetArrayElementAtIndex(configIndex);
            var expressionIdProp = cfgProp.FindPropertyRelative("expressionId");
            string expressionId = expressionIdProp != null ? expressionIdProp.stringValue : string.Empty;

            var row = new VisualElement
            {
                name = GazeConfigRowName,
                userData = expressionId,
            };
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 6;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.65f, 0.9f));
            row.style.borderLeftWidth = 2;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            string resolvedName = FindExpressionNameById(expressionId);
            string headerLabelText = string.IsNullOrEmpty(resolvedName)
                ? "Expression 名: <未設定>"
                : $"Expression 名: {resolvedName}";
            var expressionNameLabel = new Label(headerLabelText)
            {
                name = GazeConfigExpressionNameLabelName,
                tooltip = expressionId,
            };
            expressionNameLabel.style.flexGrow = 1f;
            expressionNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(expressionNameLabel);

            var autoAssignButton = new Button(() => ResolveGazeConfigFromReferenceModel(configIndex))
            {
                name = GazeConfigAutoAssignButtonName,
                text = "参照モデルから自動設定",
                tooltip = "現在の参照モデルからこの GazeConfig を再解決し、既存値を上書きします。",
            };
            autoAssignButton.SetEnabled(HasReferenceModel());
            autoAssignButton.style.marginLeft = 4;
            header.Add(autoAssignButton);

            var removeButton = new Button(() => RemoveGazeConfigAt(configIndex))
            {
                name = GazeConfigRemoveButtonName,
                text = "削除",
            };
            removeButton.style.marginLeft = 4;
            header.Add(removeButton);

            row.Add(header);

            AddBoundTextField(row, cfgProp, "leftEyeBonePath", "左目ボーン", GazeConfigLeftBonePathFieldName);
            AddBoundTextField(row, cfgProp, "rightEyeBonePath", "右目ボーン", GazeConfigRightBonePathFieldName);

            AddBoundFloatField(row, cfgProp, "lookUpAngle", "上方向角度", GazeConfigLookUpAngleFieldName);
            AddBoundFloatField(row, cfgProp, "lookDownAngle", "下方向角度", GazeConfigLookDownAngleFieldName);
            AddBoundFloatField(row, cfgProp, "outerYawAngle", "外側角度", GazeConfigOuterYawAngleFieldName);
            AddBoundFloatField(row, cfgProp, "innerYawAngle", "内側角度", GazeConfigInnerYawAngleFieldName);

            AddBoundClipField(row, cfgProp, "lookLeftClip", "左 Clip", GazeConfigLookLeftClipFieldName);
            AddBoundClipField(row, cfgProp, "lookRightClip", "右 Clip", GazeConfigLookRightClipFieldName);
            AddBoundClipField(row, cfgProp, "lookUpClip", "上 Clip", GazeConfigLookUpClipFieldName);
            AddBoundClipField(row, cfgProp, "lookDownClip", "下 Clip", GazeConfigLookDownClipFieldName);

            return row;
        }

        private void AddBoundTextField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName)
        {
            var prop = cfgProp.FindPropertyRelative(propertyName);
            if (prop == null) return;

            var field = new TextField(label)
            {
                name = elementName,
            };
            field.BindProperty(prop);
            row.Add(field);
        }

        private void AddBoundFloatField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName)
        {
            var prop = cfgProp.FindPropertyRelative(propertyName);
            if (prop == null) return;

            var field = new FloatField(label)
            {
                name = elementName,
            };
            field.BindProperty(prop);
            row.Add(field);
        }

        private void AddBoundClipField(
            VisualElement row,
            SerializedProperty cfgProp,
            string propertyName,
            string label,
            string elementName)
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
            row.Add(field);
        }

        private List<GazeConfigCandidate> CollectAddableGazeConfigCandidates()
        {
            var candidates = new List<GazeConfigCandidate>();
            if (_expressionsProperty == null || _rootGazeConfigsProperty == null) return candidates;

            var configuredIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _rootGazeConfigsProperty.arraySize; i++)
            {
                var cfg = _rootGazeConfigsProperty.GetArrayElementAtIndex(i);
                var idProp = cfg.FindPropertyRelative("expressionId");
                if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                {
                    configuredIds.Add(idProp.stringValue);
                }
            }

            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var expr = _expressionsProperty.GetArrayElementAtIndex(i);
                var isGazeProp = expr.FindPropertyRelative("isGaze");
                if (isGazeProp == null || !isGazeProp.boolValue) continue;

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
            if (string.IsNullOrEmpty(expressionId) || _rootGazeConfigsProperty == null) return;

            serializedObject.Update();
            if (FindRootGazeConfigIndex(expressionId) >= 0)
            {
                RebuildGazeConfigsUI();
                return;
            }

            int undoGroup = BeginUndoGroup("Add GazeConfig");
            int newIndex = _rootGazeConfigsProperty.arraySize;
            _rootGazeConfigsProperty.InsertArrayElementAtIndex(newIndex);
            var cfg = _rootGazeConfigsProperty.GetArrayElementAtIndex(newIndex);
            ResetGazeConfigToDefaults(cfg);

            var idProp = cfg.FindPropertyRelative("expressionId");
            if (idProp != null) idProp.stringValue = expressionId;

            ApplyModifiedPropertiesAndCollapseUndo(undoGroup);
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        private void RemoveGazeConfigAt(int configIndex)
        {
            if (_rootGazeConfigsProperty == null) return;

            serializedObject.Update();
            if (configIndex < 0 || configIndex >= _rootGazeConfigsProperty.arraySize) return;

            int undoGroup = BeginUndoGroup("Remove GazeConfig");
            ValidateGazeConfigDeletionTrigger(GazeConfigDeletionTrigger.ExplicitUserRemoval);
            _rootGazeConfigsProperty.DeleteArrayElementAtIndex(configIndex);
            ApplyModifiedPropertiesAndCollapseUndo(undoGroup);
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
            if (!HasReferenceModel() || _rootGazeConfigsProperty == null) return;

            serializedObject.Update();
            if (configIndex < 0 || configIndex >= _rootGazeConfigsProperty.arraySize) return;

            AssignGazeConfigFromReferenceModel(
                _rootGazeConfigsProperty.GetArrayElementAtIndex(configIndex),
                resetRangesToDefaults: true);
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

            // 参照モデルが切り替わった/設定されたら、目線タブ名にアスタリスクを付けて
            // 「ボーンパスを確認しろ」というユーザー注意を促す。
            // 自動入力はしない（前回値が残ると未設定との区別が付かなくなるため。
            // ユーザーが明示的に GazeConfig 行の「参照モデルから自動設定」ボタンで埋める想定）。
            if (currentReferenceModel != null && currentReferenceModel != previousReferenceModel)
            {
                MarkGazeTabNeedsAttention();
            }
        }
#endif

        private void MarkGazeTabNeedsAttention()
        {
            if (_gazeTab == null) return;
            if (!string.Equals(_gazeTab.label, GazeTabBaseLabel + GazeTabAttentionMarker, StringComparison.Ordinal))
            {
                _gazeTab.label = GazeTabBaseLabel + GazeTabAttentionMarker;
            }
        }

        private void ClearGazeTabAttention()
        {
            if (_gazeTab == null) return;
            if (!string.Equals(_gazeTab.label, GazeTabBaseLabel, StringComparison.Ordinal))
            {
                _gazeTab.label = GazeTabBaseLabel;
            }
        }

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
                // 1 文字ごとに RebuildLayersUI を呼ぶとフォーカスが外れるため、
                // SerializedProperty の更新だけ value change で行い、候補リスト同期と
                // UI 再構築はフォーカスアウト時にまとめて実施する。
                nameField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var p = _layersProperty.GetArrayElementAtIndex(layerIndex).FindPropertyRelative("name");
                    if (p != null)
                    {
                        p.stringValue = evt.newValue ?? string.Empty;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
                nameField.RegisterCallback<BlurEvent>(_ =>
                {
                    // レイヤー名は他レイヤーの「上書き対象」候補にも影響するため全体を再構築する
                    RefreshLayerNameChoices();
                    RebuildLayersUI();
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

            // LayerOverrideMask: このレイヤーがアクティブな間に上書きする他レイヤー
            // 「排他モード」と一緒に管理する設定なので、入力源より上に配置する。
            var maskHelp = MakeHelpBox(
                "このレイヤーがアクティブな間、合成時に上書き対象とするレイヤーを選択します。"
                + "Nothing を選んだ場合はこのレイヤーは他レイヤーに何も上書きしません。");
            card.Add(maskHelp);

            var maskContainer = new VisualElement { name = $"layer-mask-container-{layerIndex}" };
            card.Add(maskContainer);
            RebuildLayerOverrideMaskFieldInto(maskContainer, layerIndex);

            if (inputSourcesProp != null)
            {
                var inputSourcesField = new PropertyField(inputSourcesProp, "入力源");
                inputSourcesField.Bind(serializedObject);
                card.Add(inputSourcesField);
            }

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

            var addExpressionButton = new Button(() => AddExpressionForLayer(layerName, isGaze: false))
            {
                text = "+ 表情を追加",
            };
            expressionAddRow.Add(addExpressionButton);

            var addGazeButton = new Button(() => AddExpressionForLayer(layerName, isGaze: true))
            {
                text = "+ 目線操作の表情を追加",
            };
            expressionAddRow.Add(addGazeButton);

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
            if (layerIndex < 0 || layerIndex >= _layersProperty.arraySize) return;

            int undoGroup = BeginUndoGroup("Remove Layer with Expressions");
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
                    if (idProp != null)
                    {
                        CleanupRootGazeConfigsForRemovedExpression(
                            idProp.stringValue,
                            GazeConfigDeletionTrigger.ExpressionDeletion);
                    }
                    _expressionsProperty.DeleteArrayElementAtIndex(i);
                }
            }

            _layersProperty.DeleteArrayElementAtIndex(layerIndex);
            ApplyModifiedPropertiesAndCollapseUndo(undoGroup);

            RefreshLayerNameChoices();
            RebuildLayersUI();
            RebuildGazeConfigsUI();
            UpdateValidation();
        }

        private void AddExpressionForLayer(string layerName, bool isGaze)
        {
            serializedObject.Update();
            int newIndex = _expressionsProperty.arraySize;
            _expressionsProperty.InsertArrayElementAtIndex(newIndex);
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(newIndex);

            var idProp = entryProp.FindPropertyRelative("id");
            var nameProp = entryProp.FindPropertyRelative("name");
            var layerProp = entryProp.FindPropertyRelative("layer");
            var isGazeProp = entryProp.FindPropertyRelative("isGaze");

            string newId = Guid.NewGuid().ToString("N");
            if (idProp != null) idProp.stringValue = newId;
            if (nameProp != null) nameProp.stringValue = isGaze ? "目線操作" : "新規表情";
            if (layerProp != null) layerProp.stringValue = layerName ?? string.Empty;
            if (isGazeProp != null) isGazeProp.boolValue = isGaze;

            serializedObject.ApplyModifiedProperties();

            RebuildLayersUI();
            RebuildGazeConfigsUI();
            RebuildExpressionIdMapping();
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
            var isGazeProp = entryProp.FindPropertyRelative("isGaze");
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

            // ヘッダー行: 削除ボタンのみ。「目線操作」トグルは AnimationClip スロット直下に移動した。
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.FlexEnd;

            bool currentIsGaze = isGazeProp != null && isGazeProp.boolValue;

            var removeButton = new Button(() => RemoveExpression(exprIndex))
            {
                text = "削除",
            };
            removeButton.style.marginLeft = 6;
            headerRow.Add(removeButton);

            row.Add(headerRow);

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
                        RebuildExpressionIdMapping();
                    }
                });
            }
            row.Add(nameField);

            // 遷移時間。目線操作では概念がないため非表示にする (データは互換目的で保持)。
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
                currentIsGaze ? DisplayStyle.None : DisplayStyle.Flex;
            row.Add(transitionDurationField);

            // GazeConfig は専用セクションで opt-in 編集するため、Expression 行では共通 clip のみ表示する。
            // BuildAnimationClipFields は AnimationClip スロットの直下に「目線操作」Toggle を配置する。
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

        private void ChangeExpressionIsGaze(int exprIndex, bool newIsGaze)
        {
            serializedObject.Update();
            if (exprIndex < 0 || exprIndex >= _expressionsProperty.arraySize) return;

            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var isGazeProp = entryProp.FindPropertyRelative("isGaze");
            if (isGazeProp == null) return;

            bool previousIsGaze = isGazeProp.boolValue;
            if (previousIsGaze == newIsGaze) return;

            int undoGroup = BeginUndoGroup("Change Expression IsGaze");
            isGazeProp.boolValue = newIsGaze;

            if (previousIsGaze && !newIsGaze)
            {
                var idProp = entryProp.FindPropertyRelative("id");
                CleanupRootGazeConfigsForRemovedExpression(
                    idProp != null ? idProp.stringValue : string.Empty,
                    GazeConfigDeletionTrigger.AnalogToNonAnalogKindTransition);
            }

            ApplyModifiedPropertiesAndCollapseUndo(undoGroup);
            RebuildLayersUI();
            RebuildGazeConfigsUI();
            RebuildExpressionIdMapping();
            UpdateValidation();
        }

        private void BuildAnimationClipFields(VisualElement row, int exprIndex)
        {
            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var clipProp = entryProp.FindPropertyRelative("animationClip");
            var isGazeProp = entryProp.FindPropertyRelative("isGaze");

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

            // AnimationClip スロットの直下に「目線操作」Toggle を配置する。
            // ON にすると GazeConfig（目線設定）駆動、OFF だと AnimationClip + 遷移時間。
            bool currentIsGaze = isGazeProp != null && isGazeProp.boolValue;
            var isGazeToggle = new Toggle("目線操作")
            {
                name = ExpressionRowIsGazeToggleName,
                tooltip = "ON にするとこの表情は GazeConfig（目線設定）で駆動されます。OFF の通常表情では AnimationClip と遷移時間が使われます。",
            };
            isGazeToggle.SetValueWithoutNotify(currentIsGaze);
            isGazeToggle.style.marginTop = 2;
            isGazeToggle.RegisterValueChangedCallback(evt =>
            {
                ChangeExpressionIsGaze(exprIndex, evt.newValue);
            });
            row.Add(isGazeToggle);

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

        private void RemoveExpression(int exprIndex)
        {
            serializedObject.Update();
            if (exprIndex < 0 || exprIndex >= _expressionsProperty.arraySize) return;

            var entryProp = _expressionsProperty.GetArrayElementAtIndex(exprIndex);
            var idProp = entryProp.FindPropertyRelative("id");
            string id = idProp != null ? idProp.stringValue : string.Empty;

            int undoGroup = BeginUndoGroup("Remove Expression with GazeConfig");
            CleanupRootGazeConfigsForRemovedExpression(id, GazeConfigDeletionTrigger.ExpressionDeletion);
            _expressionsProperty.DeleteArrayElementAtIndex(exprIndex);
            ApplyModifiedPropertiesAndCollapseUndo(undoGroup);

            RebuildLayersUI();
            RebuildGazeConfigsUI();
            RebuildExpressionIdMapping();
            UpdateValidation();
        }

        // ====================================================================
        // GazeConfig ヘルパー
        // ====================================================================

        private enum GazeConfigDeletionTrigger
        {
            ExplicitUserRemoval,
            ExpressionDeletion,
            AnalogToNonAnalogKindTransition,
        }

        private static int BeginUndoGroup(string groupName)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(groupName);
            return Undo.GetCurrentGroup();
        }

        private void ApplyModifiedPropertiesAndCollapseUndo(int undoGroup)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(undoGroup);
        }

        private int FindRootGazeConfigIndex(string expressionId)
        {
            if (_rootGazeConfigsProperty == null || string.IsNullOrEmpty(expressionId)) return -1;
            for (int i = 0; i < _rootGazeConfigsProperty.arraySize; i++)
            {
                var cfg = _rootGazeConfigsProperty.GetArrayElementAtIndex(i);
                var idP = cfg.FindPropertyRelative("expressionId");
                if (idP != null && string.Equals(idP.stringValue, expressionId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ValidateGazeConfigDeletionTrigger(GazeConfigDeletionTrigger trigger)
        {
            switch (trigger)
            {
                case GazeConfigDeletionTrigger.ExplicitUserRemoval:
                case GazeConfigDeletionTrigger.ExpressionDeletion:
                case GazeConfigDeletionTrigger.AnalogToNonAnalogKindTransition:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
            }
        }

        private int CleanupRootGazeConfigsForRemovedExpression(string expressionId, GazeConfigDeletionTrigger trigger)
        {
            ValidateGazeConfigDeletionTrigger(trigger);

            if (_rootGazeConfigsProperty == null || string.IsNullOrEmpty(expressionId)) return 0;

            int removedCount = 0;
            for (int i = _rootGazeConfigsProperty.arraySize - 1; i >= 0; i--)
            {
                var cfg = _rootGazeConfigsProperty.GetArrayElementAtIndex(i);
                var idP = cfg.FindPropertyRelative("expressionId");
                if (idP != null && string.Equals(idP.stringValue, expressionId, StringComparison.Ordinal))
                {
                    _rootGazeConfigsProperty.DeleteArrayElementAtIndex(i);
                    removedCount++;
                }
            }
            return removedCount;
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
        // Section: 参照モデル（タブ外、常時表示）
        // ====================================================================

        private void BuildReferenceModelDirect(VisualElement root)
        {
#if UNITY_EDITOR
            if (_referenceModelProperty == null) return;

            var refModelField = new PropertyField(_referenceModelProperty, "参照モデル")
            {
                name = ReferenceModelDirectFieldName,
                tooltip = "BlendShape 名やボーン名の取得元となるモデル。"
                    + "AnimationClip の RendererPath 検証にも利用されます。",
            };
            refModelField.style.marginBottom = 6;
            root.Add(refModelField);
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

            var mappingTitle = new Label("Expression ID マッピング")
            {
                name = DebugExpressionIdMappingTitleName,
            };
            mappingTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mappingTitle.style.marginTop = 6;
            foldout.Add(mappingTitle);

            _debugExpressionIdMappingContainer = new VisualElement
            {
                name = DebugExpressionIdMappingName,
            };
            _debugExpressionIdMappingContainer.style.flexDirection = FlexDirection.Column;
            foldout.Add(_debugExpressionIdMappingContainer);

            RebuildExpressionIdMapping();

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

        private void RebuildExpressionIdMapping()
        {
            if (_debugExpressionIdMappingContainer == null) return;

            _debugExpressionIdMappingContainer.Clear();
            _debugExpressionIdMappingContainer.Add(BuildExpressionIdMappingHeaderRow());

            if (_expressionsProperty == null) return;

            serializedObject.Update();
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var expr = _expressionsProperty.GetArrayElementAtIndex(i);
                _debugExpressionIdMappingContainer.Add(BuildExpressionIdMappingRow(expr));
            }
        }

        private VisualElement BuildExpressionIdMappingHeaderRow()
        {
            var row = BuildExpressionIdMappingRowContainer();
            row.Add(BuildExpressionIdMappingCell("name", null, bold: true));
            row.Add(BuildExpressionIdMappingCell("expressionId", null, bold: true));
            row.Add(BuildExpressionIdMappingCell("種別", null, bold: true));
            row.Add(BuildExpressionIdMappingCell("layer", null, bold: true));
            return row;
        }

        private VisualElement BuildExpressionIdMappingRow(SerializedProperty expr)
        {
            string kindLabel = "通常";
            var isGazeProp = expr.FindPropertyRelative("isGaze");
            if (isGazeProp != null && isGazeProp.boolValue)
            {
                kindLabel = "目線";
            }

            var row = BuildExpressionIdMappingRowContainer();
            row.Add(BuildExpressionIdMappingCell(
                ReadStringProperty(expr, "name"),
                DebugExpressionIdMappingNameCellName));
            row.Add(BuildExpressionIdMappingCell(
                ReadStringProperty(expr, "id"),
                DebugExpressionIdMappingExpressionIdCellName));
            row.Add(BuildExpressionIdMappingCell(kindLabel, DebugExpressionIdMappingKindCellName));
            row.Add(BuildExpressionIdMappingCell(
                ReadStringProperty(expr, "layer"),
                DebugExpressionIdMappingLayerCellName));
            return row;
        }

        private static VisualElement BuildExpressionIdMappingRowContainer()
        {
            var row = new VisualElement
            {
                name = DebugExpressionIdMappingRowName,
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            return row;
        }

        private static Label BuildExpressionIdMappingCell(string text, string name, bool bold = false)
        {
            var label = new Label(text ?? string.Empty)
            {
                name = name,
            };
            label.style.minWidth = 96;
            label.style.flexGrow = 1f;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            return label;
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

            // layerOverrideMask が空（= Nothing）はユーザーの明示的な選択として
            // 「このレイヤーは他レイヤーに何も上書きしない」と扱うため、警告にしない。

            // AnimationClip null タリー (通常表情のみ。目線操作は AnimationClip 任意)
            int nullClipCount = 0;
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var isGazeP = elem.FindPropertyRelative("isGaze");
                bool isGaze = isGazeP != null && isGazeP.boolValue;
                if (isGaze) continue;
                var clipP = elem.FindPropertyRelative("animationClip");
                if (clipP != null && clipP.objectReferenceValue == null) nullClipCount++;
            }
            if (nullClipCount > 0)
            {
                errors.Add($"AnimationClip が未割当の通常表情が {nullClipCount} 件あります。");
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
            var isGazeProp = entryProp.FindPropertyRelative("isGaze");
            var clipProp = entryProp.FindPropertyRelative("animationClip");
            var idProp = entryProp.FindPropertyRelative("id");

            bool isGaze = isGazeProp != null && isGazeProp.boolValue;

            var messages = new List<string>();
            if (!isGaze)
            {
                if (clipProp == null || clipProp.objectReferenceValue == null)
                {
                    messages.Add("AnimationClip が未割り当てです。");
                }
                var mismatchMessage = BuildRendererPathMismatchMessage(clipProp != null ? clipProp.objectReferenceValue as AnimationClip : null);
                if (!string.IsNullOrEmpty(mismatchMessage)) messages.Add(mismatchMessage);
            }
            else
            {
                // _rootGazeConfigsProperty を持たない派生 (GazeBinding 機能なし) では gaze 個別検証は省略する。
                if (_rootGazeConfigsProperty != null)
                {
                    int cfgIndex = FindRootGazeConfigIndex(idProp != null ? idProp.stringValue : string.Empty);
                    if (cfgIndex < 0)
                    {
                        messages.Add("目線操作の設定が見つかりません。");
                    }
                    else
                    {
                        var cfgProp = _rootGazeConfigsProperty.GetArrayElementAtIndex(cfgIndex);
                        var gazeMessages = ValidateGazeExpression(cfgProp);
                        if (gazeMessages != null) messages.AddRange(gazeMessages);

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
        /// 派生クラスが目線操作の表情に対する追加バリデーションメッセージを返すフック。
        /// 既定は空配列。例: InputActionReference 未割り当てチェックなど。
        /// </summary>
        protected virtual IReadOnlyList<string> ValidateGazeExpression(SerializedProperty gazeConfigProperty)
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
