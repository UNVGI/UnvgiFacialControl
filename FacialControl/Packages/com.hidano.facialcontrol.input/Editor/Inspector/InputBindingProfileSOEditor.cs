using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// InputBindingProfileSO のカスタム Inspector。
    /// UI Toolkit で Action / Expression をドロップダウンから選択できる GUI を提供する。
    /// </summary>
    [CustomEditor(typeof(InputBindingProfileSO))]
    public class InputBindingProfileSOEditor : UnityEditor.Editor
    {
        private const string ActionAssetFieldLabel = "Input Action Asset";
        private const string ActionMapFieldLabel = "Action Map";
        private const string FacialProfileFieldLabel = "Facial Profile（参照用）";
        private const string BindingsSectionLabel = "バインディング一覧";
        private const string RefreshButtonText = "JSON を再読み込み";
        private const string AddBindingButtonText = "バインディングを追加";
        private const string RemoveRowButtonText = "×";
        private const string UnassignedOption = "(未選択)";

        private SerializedProperty _actionAssetProperty;
        private SerializedProperty _actionMapNameProperty;
        private SerializedProperty _bindingsProperty;

        private ObjectField _actionAssetField;
        private ObjectField _facialProfileField;
        private DropdownField _actionMapDropdown;
        private VisualElement _bindingsContainer;
        private Label _profileStatusLabel;
        private Button _refreshButton;

        // Editor 専用の FacialProfileSO 参照（SO 本体には保存しない）
        private FacialProfileSO _referenceFacialProfile;

        // JSON から読み込んだ Expression キャッシュ
        private readonly List<string> _expressionIds = new List<string>();
        private readonly List<string> _expressionDisplayNames = new List<string>();

        public override VisualElement CreateInspectorGUI()
        {
            _actionAssetProperty = serializedObject.FindProperty("_actionAsset");
            _actionMapNameProperty = serializedObject.FindProperty("_actionMapName");
            _bindingsProperty = serializedObject.FindProperty("_bindings");

            var root = new VisualElement();
            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            BuildActionAssetSection(root);
            BuildActionMapSection(root);
            BuildFacialProfileSection(root);
            BuildBindingsSection(root);

            RebuildBindingsUi();

            return root;
        }

        private void BuildActionAssetSection(VisualElement root)
        {
            _actionAssetField = new ObjectField(ActionAssetFieldLabel)
            {
                objectType = typeof(InputActionAsset),
                allowSceneObjects = false
            };
            _actionAssetField.BindProperty(_actionAssetProperty);
            _actionAssetField.RegisterValueChangedCallback(_ =>
            {
                RefreshActionMapChoices();
                RebuildBindingsUi();
            });
            root.Add(_actionAssetField);
        }

        private void BuildActionMapSection(VisualElement root)
        {
            _actionMapDropdown = new DropdownField(ActionMapFieldLabel);
            RefreshActionMapChoices();

            _actionMapDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_actionMapNameProperty == null)
                {
                    return;
                }

                _actionMapNameProperty.stringValue = evt.newValue ?? string.Empty;
                serializedObject.ApplyModifiedProperties();
                RebuildBindingsUi();
            });
            root.Add(_actionMapDropdown);
        }

        private void BuildFacialProfileSection(VisualElement root)
        {
            var facialProfileRow = new VisualElement();
            facialProfileRow.style.marginTop = 6;

            _facialProfileField = new ObjectField(FacialProfileFieldLabel)
            {
                objectType = typeof(FacialProfileSO),
                allowSceneObjects = false,
                value = null
            };
            _facialProfileField.RegisterValueChangedCallback(evt =>
            {
                _referenceFacialProfile = evt.newValue as FacialProfileSO;
                ReloadExpressionCache();
                RebuildBindingsUi();
            });
            facialProfileRow.Add(_facialProfileField);

            _refreshButton = new Button(() =>
            {
                ReloadExpressionCache();
                RebuildBindingsUi();
            })
            {
                text = RefreshButtonText
            };
            _refreshButton.AddToClassList(FacialControlStyles.ActionButton);
            facialProfileRow.Add(_refreshButton);

            _profileStatusLabel = new Label();
            _profileStatusLabel.AddToClassList(FacialControlStyles.StatusLabel);
            _profileStatusLabel.style.display = DisplayStyle.None;
            facialProfileRow.Add(_profileStatusLabel);

            root.Add(facialProfileRow);
        }

        private void BuildBindingsSection(VisualElement root)
        {
            var sectionLabel = new Label(BindingsSectionLabel);
            sectionLabel.style.marginTop = 10;
            sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(sectionLabel);

            _bindingsContainer = new ScrollView(ScrollViewMode.Vertical);
            _bindingsContainer.style.maxHeight = 360;
            _bindingsContainer.style.marginTop = 4;
            root.Add(_bindingsContainer);

            var addButton = new Button(AddBinding)
            {
                text = AddBindingButtonText
            };
            addButton.AddToClassList(FacialControlStyles.ActionButton);
            addButton.style.marginTop = 4;
            root.Add(addButton);
        }

        private void RefreshActionMapChoices()
        {
            if (_actionMapDropdown == null)
            {
                return;
            }

            var asset = _actionAssetProperty?.objectReferenceValue as InputActionAsset;
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

            var currentName = _actionMapNameProperty != null ? _actionMapNameProperty.stringValue : string.Empty;
            if (!string.IsNullOrEmpty(currentName) && choices.Contains(currentName))
            {
                _actionMapDropdown.SetValueWithoutNotify(currentName);
            }
            else if (choices.Count > 0)
            {
                _actionMapDropdown.SetValueWithoutNotify(choices[0]);
                if (_actionMapNameProperty != null && _actionMapNameProperty.stringValue != choices[0])
                {
                    _actionMapNameProperty.stringValue = choices[0];
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                _actionMapDropdown.SetValueWithoutNotify(string.Empty);
            }
        }

        private List<string> GetCurrentActionNames()
        {
            var result = new List<string>();
            var asset = _actionAssetProperty?.objectReferenceValue as InputActionAsset;
            if (asset == null)
            {
                return result;
            }

            var mapName = _actionMapNameProperty != null ? _actionMapNameProperty.stringValue : string.Empty;
            if (string.IsNullOrEmpty(mapName))
            {
                return result;
            }

            var map = asset.FindActionMap(mapName);
            if (map == null)
            {
                return result;
            }

            foreach (var action in map.actions)
            {
                if (!string.IsNullOrEmpty(action.name))
                {
                    result.Add(action.name);
                }
            }

            return result;
        }

        private void ReloadExpressionCache()
        {
            _expressionIds.Clear();
            _expressionDisplayNames.Clear();

            if (_referenceFacialProfile == null)
            {
                SetProfileStatus(string.Empty, false);
                return;
            }

            var relative = _referenceFacialProfile.JsonFilePath;
            if (string.IsNullOrWhiteSpace(relative))
            {
                SetProfileStatus("JsonFilePath が未設定です。", true);
                return;
            }

            var fullPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, relative);
            if (!File.Exists(fullPath))
            {
                SetProfileStatus($"JSON ファイルが見つかりません: {fullPath}", true);
                return;
            }

            try
            {
                var json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                var parser = new SystemTextJsonParser();
                var profile = parser.ParseProfile(json);

                var expressions = profile.Expressions.Span;
                for (int i = 0; i < expressions.Length; i++)
                {
                    var expr = expressions[i];
                    if (string.IsNullOrEmpty(expr.Id))
                    {
                        continue;
                    }

                    _expressionIds.Add(expr.Id);
                    var displayName = string.IsNullOrEmpty(expr.Name) ? expr.Id : $"{expr.Name} ({expr.Id})";
                    _expressionDisplayNames.Add(displayName);
                }

                SetProfileStatus($"Expression を {_expressionIds.Count} 件読み込みました。", false);
            }
            catch (Exception ex)
            {
                SetProfileStatus($"JSON パースに失敗しました: {ex.Message}", true);
            }
        }

        private void SetProfileStatus(string message, bool isError)
        {
            if (_profileStatusLabel == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                _profileStatusLabel.style.display = DisplayStyle.None;
                _profileStatusLabel.text = string.Empty;
                return;
            }

            _profileStatusLabel.style.display = DisplayStyle.Flex;
            _profileStatusLabel.text = message;
            _profileStatusLabel.RemoveFromClassList(FacialControlStyles.StatusError);
            _profileStatusLabel.RemoveFromClassList(FacialControlStyles.StatusSuccess);
            _profileStatusLabel.AddToClassList(isError ? FacialControlStyles.StatusError : FacialControlStyles.StatusSuccess);
        }

        private void RebuildBindingsUi()
        {
            if (_bindingsContainer == null || _bindingsProperty == null)
            {
                return;
            }

            serializedObject.Update();
            _bindingsContainer.Clear();

            var actionNames = GetCurrentActionNames();

            for (int i = 0; i < _bindingsProperty.arraySize; i++)
            {
                var row = BuildBindingRow(i, actionNames);
                _bindingsContainer.Add(row);
            }
        }

        private VisualElement BuildBindingRow(int index, List<string> actionNames)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;

            var element = _bindingsProperty.GetArrayElementAtIndex(index);
            var actionNameProp = element.FindPropertyRelative("actionName");
            var expressionIdProp = element.FindPropertyRelative("expressionId");

            // Action ドロップダウン
            var actionChoices = new List<string> { UnassignedOption };
            actionChoices.AddRange(actionNames);

            var currentAction = actionNameProp.stringValue;
            var actionInitial = !string.IsNullOrEmpty(currentAction) && actionNames.Contains(currentAction)
                ? currentAction
                : (!string.IsNullOrEmpty(currentAction) ? currentAction : UnassignedOption);

            // Action 名がドロップダウン候補にない場合でも値として保持する
            if (!string.IsNullOrEmpty(currentAction) && !actionChoices.Contains(currentAction))
            {
                actionChoices.Add(currentAction);
            }

            var actionDropdown = new DropdownField("Action", actionChoices, actionInitial);
            actionDropdown.style.flexGrow = 1;
            actionDropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var elem = _bindingsProperty.GetArrayElementAtIndex(index);
                var actionProp = elem.FindPropertyRelative("actionName");
                actionProp.stringValue = evt.newValue == UnassignedOption ? string.Empty : evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            row.Add(actionDropdown);

            // Expression ドロップダウン
            var expressionChoices = new List<string> { UnassignedOption };
            expressionChoices.AddRange(_expressionDisplayNames);

            var currentExprId = expressionIdProp.stringValue;
            string expressionInitial = UnassignedOption;
            if (!string.IsNullOrEmpty(currentExprId))
            {
                var idx = _expressionIds.IndexOf(currentExprId);
                if (idx >= 0)
                {
                    expressionInitial = _expressionDisplayNames[idx];
                }
                else
                {
                    // キャッシュ未取得 or ID 不在: 生 ID を選択肢として表示
                    var fallback = $"(未解決) {currentExprId}";
                    if (!expressionChoices.Contains(fallback))
                    {
                        expressionChoices.Add(fallback);
                    }
                    expressionInitial = fallback;
                }
            }

            var expressionDropdown = new DropdownField("Expression", expressionChoices, expressionInitial);
            expressionDropdown.style.flexGrow = 1;
            expressionDropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var elem = _bindingsProperty.GetArrayElementAtIndex(index);
                var exprProp = elem.FindPropertyRelative("expressionId");

                if (evt.newValue == UnassignedOption)
                {
                    exprProp.stringValue = string.Empty;
                }
                else
                {
                    var displayIndex = _expressionDisplayNames.IndexOf(evt.newValue);
                    if (displayIndex >= 0)
                    {
                        exprProp.stringValue = _expressionIds[displayIndex];
                    }
                    // (未解決) 系はそのまま維持するため、何もしない
                }

                serializedObject.ApplyModifiedProperties();
            });
            row.Add(expressionDropdown);

            var removeButton = new Button(() => RemoveBindingAt(index))
            {
                text = RemoveRowButtonText
            };
            removeButton.style.width = 24;
            row.Add(removeButton);

            return row;
        }

        private void AddBinding()
        {
            serializedObject.Update();
            _bindingsProperty.arraySize += 1;
            var newElement = _bindingsProperty.GetArrayElementAtIndex(_bindingsProperty.arraySize - 1);
            newElement.FindPropertyRelative("actionName").stringValue = string.Empty;
            newElement.FindPropertyRelative("expressionId").stringValue = string.Empty;
            serializedObject.ApplyModifiedProperties();
            RebuildBindingsUi();
        }

        private void RemoveBindingAt(int index)
        {
            if (index < 0 || index >= _bindingsProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            _bindingsProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            RebuildBindingsUi();
        }
    }
}
