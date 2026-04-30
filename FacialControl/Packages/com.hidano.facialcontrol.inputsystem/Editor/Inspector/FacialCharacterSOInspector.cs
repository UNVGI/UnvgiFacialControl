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
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.InputSystem.Editor.Inspector
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> 用の UI Toolkit カスタム Inspector。
    /// ユーザー要件「SO の Inspector を見れば設定方法が分かる」を満たすため、
    /// 7 つのセクション (Foldout) に短い HelpBox を添えて 1 アセットで完結する編集 UI を提供する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 旧 <c>FacialProfileSOInspector</c> / <c>InputBindingProfileSOEditor</c> /
    /// <c>AnalogInputBindingProfileSOEditor</c> の機能を 1 Inspector に集約し、
    /// 長文 README に頼らずとも単独で表情設定を完了させられることを目指す。
    /// </para>
    /// <para>
    /// Expression セクションは preview 段階では既存 <see cref="Tools.ExpressionCreatorWindow"/>
    /// を呼び出すボタンを置く方針 (Task 5 範囲。完全な内蔵は将来の拡張)。
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(FacialCharacterSO))]
    public sealed class FacialCharacterSOInspector : UnityEditor.Editor
    {
        // ====================================================================
        // VisualElement の name 定数 (テストの Q<>() と一致させる)
        // ====================================================================

        /// <summary>入力 (Input) セクションの Foldout 名。</summary>
        public const string InputFoldoutName = "facial-character-input-foldout";

        /// <summary>キーバインディング (Action ↔ Expression) セクションの Foldout 名。</summary>
        public const string ExpressionBindingsFoldoutName = "facial-character-expression-bindings-foldout";

        /// <summary>アナログバインディングセクションの Foldout 名。</summary>
        public const string AnalogBindingsFoldoutName = "facial-character-analog-bindings-foldout";

        /// <summary>レイヤー (Layers) セクションの Foldout 名。</summary>
        public const string LayersFoldoutName = "facial-character-layers-foldout";

        /// <summary>Expression セクションの Foldout 名。</summary>
        public const string ExpressionsFoldoutName = "facial-character-expressions-foldout";

        /// <summary>BonePose セクションの Foldout 名。</summary>
        public const string BonePosesFoldoutName = "facial-character-boneposes-foldout";

        /// <summary>デバッグ情報セクションの Foldout 名。</summary>
        public const string DebugFoldoutName = "facial-character-debug-foldout";

        // ====================================================================
        // SerializedProperty
        // ====================================================================

        private SerializedProperty _inputActionAssetProperty;
        private SerializedProperty _actionMapNameProperty;
        private SerializedProperty _expressionBindingsProperty;
        private SerializedProperty _analogBindingsProperty;
        private SerializedProperty _layersProperty;
        private SerializedProperty _expressionsProperty;
        private SerializedProperty _rendererPathsProperty;
        private SerializedProperty _bonePosesProperty;
        private SerializedProperty _schemaVersionProperty;

#if UNITY_EDITOR
        private SerializedProperty _referenceModelProperty;
#endif

        // ====================================================================
        // VisualElement キャッシュ (再構築時の参照用)
        // ====================================================================

        private DropdownField _actionMapDropdown;
        private Label _debugSchemaVersionLabel;
        private Label _debugLayerCountLabel;
        private Label _debugExpressionCountLabel;
        private Label _debugJsonPathLabel;

        // ====================================================================
        // 候補リスト (ドロップダウン用キャッシュ)
        // ====================================================================

        private readonly List<string> _actionNameChoices = new List<string>();
        private readonly List<string> _expressionIdChoices = new List<string>();
        private readonly List<string> _expressionDisplayChoices = new List<string>();
        private readonly List<string> _layerNameChoices = new List<string>();
        private readonly List<string> _blendShapeNameChoices = new List<string>();
        private readonly List<string> _boneNameChoices = new List<string>();

        // ====================================================================
        // Editor lifecycle
        // ====================================================================

        public override VisualElement CreateInspectorGUI()
        {
            ResolveSerializedProperties();

            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // セクション 1〜7 を順に構築
            BuildInputSection(root);
            BuildExpressionBindingsSection(root);
            BuildAnalogBindingsSection(root);
            BuildLayersSection(root);
            BuildExpressionsSection(root);
            BuildBonePosesSection(root);
            BuildDebugSection(root);

            // 初回描画用に候補キャッシュを構築
            RefreshAllChoiceCaches();

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
            _rendererPathsProperty = serializedObject.FindProperty("_rendererPaths");
            _bonePosesProperty = serializedObject.FindProperty("_bonePoses");
            _schemaVersionProperty = serializedObject.FindProperty("_schemaVersion");

#if UNITY_EDITOR
            _referenceModelProperty = serializedObject.FindProperty("_referenceModel");
#endif
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
            foldout.AddToClassList("fc-section-foldout");

            var help = new HelpBox(
                "ここに割り当てた .inputactions の Action 名を Expression にバインドします。"
                + "InputActionAsset と ActionMap 名を設定すると、下のキーバインディング欄で Action 名をドロップダウン選択できるようになります。",
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

                // フォールバック手入力フィールド (ActionMap が解決できない場合の互換)
                var rawField = new PropertyField(_actionMapNameProperty, "ActionMap 名 (手入力)");
                rawField.AddToClassList("fc-fallback-field");
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
                + "Action 名は上の InputActionAsset / ActionMap、Expression ID は下の Expression 一覧から候補を選べます。"
                + "Category は Controller (ゲームパッド) と Keyboard (キーボード) を指定してください。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_expressionBindingsProperty != null)
            {
                var listView = BuildArrayListView(
                    _expressionBindingsProperty,
                    itemHeight: 88f,
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
                tooltip = "発火対象の Expression ID。Expression セクションで定義したものから選択。",
            };
            container.Add(expressionDropdown);

            var categoryField = new EnumField("Category", InputSourceCategory.Controller)
            {
                name = "categoryField",
                tooltip = "入力源カテゴリ。Controller=ゲームパッド系、Keyboard=キーボード系。",
            };
            container.Add(categoryField);

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
            var categoryProp = entryProp.FindPropertyRelative("category");

            // Action 名ドロップダウン
            var actionDropdown = element.Q<DropdownField>("actionDropdown");
            if (actionDropdown != null)
            {
                var choices = BuildSafeChoices(_actionNameChoices, actionNameProp.stringValue);
                actionDropdown.choices = choices;
                actionDropdown.SetValueWithoutNotify(string.IsNullOrEmpty(actionNameProp.stringValue)
                    ? string.Empty
                    : actionNameProp.stringValue);
                actionDropdown.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("actionName");
                    prop.stringValue = evt.newValue ?? string.Empty;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            // Expression ID ドロップダウン
            var expressionDropdown = element.Q<DropdownField>("expressionDropdown");
            if (expressionDropdown != null)
            {
                var choices = BuildSafeChoices(_expressionIdChoices, expressionIdProp.stringValue);
                expressionDropdown.choices = choices;
                expressionDropdown.SetValueWithoutNotify(string.IsNullOrEmpty(expressionIdProp.stringValue)
                    ? string.Empty
                    : expressionIdProp.stringValue);
                expressionDropdown.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("expressionId");
                    prop.stringValue = evt.newValue ?? string.Empty;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            // Category EnumField
            var categoryField = element.Q<EnumField>("categoryField");
            if (categoryField != null)
            {
                categoryField.SetValueWithoutNotify((InputSourceCategory)categoryProp.enumValueIndex);
                categoryField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _expressionBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("category");
                    prop.enumValueIndex = (int)(InputSourceCategory)evt.newValue;
                    serializedObject.ApplyModifiedProperties();
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
                text = "アナログバインディング (連続値 → BlendShape / BonePose)",
                value = false,
            };

            var help = new HelpBox(
                "右スティック等の連続値入力源を BlendShape ウェイトまたはボーンの Euler 軸へ写像します。"
                + "sourceId は 'x-' プレフィックスのカスタム ID か予約 ID (analog-blendshape.* / analog-bonepose.*) を指定。"
                + "BlendShape ターゲットでは targetIdentifier に BlendShape 名、BonePose ターゲットではボーン名を入れてください。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_analogBindingsProperty != null)
            {
                var listView = BuildArrayListView(
                    _analogBindingsProperty,
                    itemHeight: 200f,
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
            container.style.marginBottom = 6;

            var sourceIdField = new TextField("sourceId")
            {
                name = "sourceIdField",
                tooltip = "入力源 ID。x- プレフィックスのカスタム ID または予約 ID。",
            };
            container.Add(sourceIdField);

            var sourceAxisField = new IntegerField("sourceAxis")
            {
                name = "sourceAxisField",
                tooltip = "入力源の軸番号 (scalar=0、Vector2 では X=0/Y=1)。",
            };
            container.Add(sourceAxisField);

            var targetKindField = new EnumField("targetKind", AnalogBindingTargetKind.BlendShape)
            {
                name = "targetKindField",
                tooltip = "ターゲット種別。BlendShape か BonePose を選択。",
            };
            container.Add(targetKindField);

            var targetIdentifierDropdown = new DropdownField("targetIdentifier")
            {
                name = "targetIdentifierDropdown",
                tooltip = "ターゲット名。BlendShape または bone 名を選択。",
            };
            container.Add(targetIdentifierDropdown);

            var targetIdentifierFallback = new TextField("targetIdentifier (手入力)")
            {
                name = "targetIdentifierFallback",
                tooltip = "候補に無い名前を直接入力する場合に使用。",
            };
            targetIdentifierFallback.AddToClassList("fc-fallback-field");
            container.Add(targetIdentifierFallback);

            var targetAxisField = new EnumField("targetAxis", AnalogTargetAxis.X)
            {
                name = "targetAxisField",
                tooltip = "BonePose ターゲット時の Euler 軸 (X/Y/Z)。BlendShape ターゲット時は無視。",
            };
            container.Add(targetAxisField);

            var mappingField = new PropertyField()
            {
                name = "mappingField",
                tooltip = "マッピング関数 (dead-zone → scale → offset → curve → invert → clamp)。",
            };
            container.Add(mappingField);

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
            var sourceIdProp = entryProp.FindPropertyRelative("sourceId");
            var sourceAxisProp = entryProp.FindPropertyRelative("sourceAxis");
            var targetKindProp = entryProp.FindPropertyRelative("targetKind");
            var targetIdentifierProp = entryProp.FindPropertyRelative("targetIdentifier");
            var targetAxisProp = entryProp.FindPropertyRelative("targetAxis");
            var mappingProp = entryProp.FindPropertyRelative("mapping");

            // sourceId TextField
            var sourceIdField = element.Q<TextField>("sourceIdField");
            if (sourceIdField != null)
            {
                sourceIdField.BindProperty(sourceIdProp);
            }

            // sourceAxis IntegerField
            var sourceAxisField = element.Q<IntegerField>("sourceAxisField");
            if (sourceAxisField != null)
            {
                sourceAxisField.BindProperty(sourceAxisProp);
            }

            // targetKind EnumField
            var targetKindField = element.Q<EnumField>("targetKindField");
            if (targetKindField != null)
            {
                targetKindField.SetValueWithoutNotify((AnalogBindingTargetKind)targetKindProp.enumValueIndex);
                targetKindField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _analogBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("targetKind");
                    prop.enumValueIndex = (int)(AnalogBindingTargetKind)evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                    // 候補リストを切り替えるため再 bind
                    RefreshTargetIdentifierDropdownForRow(element, index);
                });
            }

            // targetIdentifier ドロップダウン (BlendShape 名 / bone 名から)
            RefreshTargetIdentifierDropdownForRow(element, index);

            // targetIdentifier フォールバック手入力
            var fallbackField = element.Q<TextField>("targetIdentifierFallback");
            if (fallbackField != null)
            {
                fallbackField.BindProperty(targetIdentifierProp);
            }

            // targetAxis EnumField
            var targetAxisField = element.Q<EnumField>("targetAxisField");
            if (targetAxisField != null)
            {
                targetAxisField.SetValueWithoutNotify((AnalogTargetAxis)targetAxisProp.enumValueIndex);
                targetAxisField.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    var prop = _analogBindingsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("targetAxis");
                    prop.enumValueIndex = (int)(AnalogTargetAxis)evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            // mapping PropertyField
            var mappingField = element.Q<PropertyField>("mappingField");
            if (mappingField != null && mappingProp != null)
            {
                mappingField.BindProperty(mappingProp);
            }
        }

        private void RefreshTargetIdentifierDropdownForRow(VisualElement element, int index)
        {
            if (_analogBindingsProperty == null
                || index < 0
                || index >= _analogBindingsProperty.arraySize)
            {
                return;
            }

            var entryProp = _analogBindingsProperty.GetArrayElementAtIndex(index);
            var targetKindProp = entryProp.FindPropertyRelative("targetKind");
            var targetIdentifierProp = entryProp.FindPropertyRelative("targetIdentifier");

            var dropdown = element.Q<DropdownField>("targetIdentifierDropdown");
            if (dropdown == null)
            {
                return;
            }

            var kind = (AnalogBindingTargetKind)targetKindProp.enumValueIndex;
            var sourceList = kind == AnalogBindingTargetKind.BlendShape
                ? _blendShapeNameChoices
                : _boneNameChoices;

            var choices = BuildSafeChoices(sourceList, targetIdentifierProp.stringValue);
            dropdown.choices = choices;
            dropdown.SetValueWithoutNotify(string.IsNullOrEmpty(targetIdentifierProp.stringValue)
                ? string.Empty
                : targetIdentifierProp.stringValue);

            // 既存の callback を解除するため、UIElements の SetValueWithoutNotify では
            // 重複登録を防ぎたいが、UI Toolkit では同 callback の再登録は問題にならない。
            dropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var prop = _analogBindingsProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("targetIdentifier");
                prop.stringValue = evt.newValue ?? string.Empty;
                serializedObject.ApplyModifiedProperties();
            });
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
                + "name はレイヤー識別子、priority は値が大きいほど後段で適用、exclusionMode は LastWins (後勝ち) / Blend (ブレンド) を選択。"
                + "inputSources にはこのレイヤーで動作する入力源 (controller-expr / keyboard-expr / lipsync 等) を 1 件以上設定してください。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

            if (_layersProperty != null)
            {
                // 標準 PropertyField でリスト編集 (Unity Inspector 既定のリスト UI を再利用)。
                // 1 件 1 件の中の inputSources も標準 UI で展開可能。
                var layersField = new PropertyField(_layersProperty, "Layers");
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
                value = false,
            };

            var help = new HelpBox(
                "表情 1 件ずつの定義です。id は キーバインディング欄の Expression ID として参照されます。"
                + "BlendShape 値は 0〜1 (内部で 0〜100 にスケール)、TransitionCurve はプリセットまたは Custom キーフレームを指定。"
                + "BlendShape スライダーをプレビューしながら作成したい場合は下のボタンから Expression Creator ウィンドウを開いてください。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

#if UNITY_EDITOR
            if (_referenceModelProperty != null)
            {
                var refModelField = new PropertyField(_referenceModelProperty, "参照モデル (Editor 専用)");
                refModelField.tooltip = "BlendShape 名 / bone 名取得用の参照モデル。Prefab 推奨。";
                refModelField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshReferenceModelChoices());
                foldout.Add(refModelField);

                var refModelHelp = new HelpBox(
                    "参照モデルを下記のフィールドに設定すると、BlendShape 名と bone 名のドロップダウン候補が表示されます。",
                    HelpBoxMessageType.None);
                foldout.Add(refModelHelp);
            }
#endif

            if (_expressionsProperty != null)
            {
                var expressionsField = new PropertyField(_expressionsProperty, "Expressions");
                expressionsField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshExpressionChoices());
                foldout.Add(expressionsField);
            }

            // Expression Creator ウィンドウを開くボタン
            var openCreatorButton = new Button(OpenExpressionCreatorWindow)
            {
                name = "openExpressionCreatorButton",
                text = "Expression Creator を開く",
                tooltip = "BlendShape スライダーをプレビューしながら Expression を作成する Editor ウィンドウを開きます。",
            };
            openCreatorButton.AddToClassList(FacialControlStyles.ActionButton);
            openCreatorButton.style.marginTop = 4;
            foldout.Add(openCreatorButton);

            // RendererPaths は Expression セクション末尾に置く (BlendShape/Renderer 関連の編集対象)
            if (_rendererPathsProperty != null)
            {
                var rendererHelp = new HelpBox(
                    "SkinnedMeshRenderer のヒエラルキーパス (モデルルート相対)。"
                    + "空のままでも Runtime は Active なモデル全体から自動検索するため通常は無設定で問題ありません。",
                    HelpBoxMessageType.None);
                rendererHelp.style.marginTop = 8;
                foldout.Add(rendererHelp);

                var rendererField = new PropertyField(_rendererPathsProperty, "Renderer Paths");
                foldout.Add(rendererField);
            }

            root.Add(foldout);
        }

        private static void OpenExpressionCreatorWindow()
        {
            // 既存のメニュー項目から開く (型直接参照は core パッケージへの依存になるため避ける)。
            EditorApplication.ExecuteMenuItem("FacialControl/Expression 作成");
        }

        // ====================================================================
        // Section 6: BonePose
        // ====================================================================

        private void BuildBonePosesSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                name = BonePosesFoldoutName,
                text = "BonePose",
                value = false,
            };

            var help = new HelpBox(
                "Expression 適用時に上書きするボーンの Euler 角 (顔相対) を定義します。"
                + "boneName は Animator 階層上の Transform 名と一致させてください。",
                HelpBoxMessageType.Info);
            foldout.Add(help);

#if UNITY_EDITOR
            // 参照モデル未設定時の案内
            if (_referenceModelProperty != null)
            {
                var noRefHelp = new HelpBox(
                    "参照モデルを Expression セクション内のフィールドに設定すると、boneName のドロップダウン候補が表示されます。",
                    HelpBoxMessageType.None);
                noRefHelp.name = "bonePoseNoRefModelHelp";
                noRefHelp.style.display = (_referenceModelProperty.objectReferenceValue == null)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                foldout.Add(noRefHelp);
            }
#endif

            if (_bonePosesProperty != null)
            {
                // BonePose の標準 PropertyField (内部の entries / boneName / eulerXYZ も既定 UI で編集可能)
                var bonePosesField = new PropertyField(_bonePosesProperty, "Bone Poses");
                foldout.Add(bonePosesField);
            }

            root.Add(foldout);
        }

        // ====================================================================
        // Section 7: デバッグ情報 (Debug)
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
                + "JSON の自動エクスポート先は StreamingAssets 配下の規約パスです。",
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

            UpdateDebugLabels();

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
        // 共通: 標準パターンの ListView ビルダ
        // ====================================================================

        /// <summary>
        /// SerializedProperty (配列) を編集する <see cref="ListView"/> を生成する。
        /// itemsSource は arraySize ベースで作成し、+ / - / リオーダー操作は ListView 既定 UI を使用する。
        /// </summary>
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

            // + ボタン: arraySize を増やしたあと proxy / Rebuild
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

            // - ボタン: ListView から渡された indices を降順で SerializedProperty 配列から消す
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
        // 候補キャッシュの更新
        // ====================================================================

        private void RefreshAllChoiceCaches()
        {
            RefreshActionMapChoices();
            RefreshActionNameChoices();
            RefreshExpressionChoices();
            RefreshLayerChoices();
            RefreshReferenceModelChoices();
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

        private void RefreshExpressionChoices()
        {
            _expressionIdChoices.Clear();
            _expressionDisplayChoices.Clear();
            if (_expressionsProperty == null)
            {
                UpdateDebugLabels();
                return;
            }
            for (int i = 0; i < _expressionsProperty.arraySize; i++)
            {
                var elem = _expressionsProperty.GetArrayElementAtIndex(i);
                var idProp = elem.FindPropertyRelative("id");
                var nameProp = elem.FindPropertyRelative("name");
                var id = idProp != null ? idProp.stringValue : null;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                _expressionIdChoices.Add(id);
                var displayName = nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)
                    ? $"{nameProp.stringValue} ({id})"
                    : id;
                _expressionDisplayChoices.Add(displayName);
            }
            UpdateDebugLabels();
        }

        private void RefreshLayerChoices()
        {
            _layerNameChoices.Clear();
            if (_layersProperty == null)
            {
                UpdateDebugLabels();
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
            UpdateDebugLabels();
        }

        private void RefreshReferenceModelChoices()
        {
            _blendShapeNameChoices.Clear();
            _boneNameChoices.Clear();

#if UNITY_EDITOR
            if (_referenceModelProperty == null)
            {
                return;
            }
            var model = _referenceModelProperty.objectReferenceValue as GameObject;
            if (model == null)
            {
                return;
            }
            var blendShapes = BlendShapeNameProvider.GetBlendShapeNames(model);
            for (int i = 0; i < blendShapes.Length; i++)
            {
                _blendShapeNameChoices.Add(blendShapes[i]);
            }
            var bones = BoneNameProvider.GetBoneNames(model);
            for (int i = 0; i < bones.Length; i++)
            {
                _boneNameChoices.Add(bones[i]);
            }
#endif
        }

        /// <summary>
        /// 候補リストに現在値が含まれない場合でも、現在値を選択肢として保持する安全なリストを構築する。
        /// </summary>
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
    }
}
