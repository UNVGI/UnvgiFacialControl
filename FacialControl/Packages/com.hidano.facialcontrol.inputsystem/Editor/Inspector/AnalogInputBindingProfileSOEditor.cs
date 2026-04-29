using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// <see cref="AnalogInputBindingProfileSO"/> 用の UI Toolkit カスタム Inspector
    /// (Req 4.4 / 10.1〜10.6, preview.1 読取専用)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// preview.1 ではマッピングカーブのフル GUI 編集は提供せず、bindings の読取専用列挙と
    /// Import / Export / Humanoid 自動割当の 3 ボタンのみを提供する (Req 10.6)。
    /// 編集は SO 内 <c>_jsonText</c> フィールド（標準 PropertyField で表示）または
    /// 外部 JSON ファイルの直接編集 → Import 経由で行う。
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(AnalogInputBindingProfileSO))]
    internal sealed class AnalogInputBindingProfileSOEditor : UnityEditor.Editor
    {
        private const string BindingsSectionLabel = "バインディング一覧 (読取専用)";
        private const string ImportButtonText = "Import JSON...";
        private const string ExportButtonText = "Export JSON...";
        private const string HumanoidAutoAssignButtonText = "Humanoid 自動割当";
        private const string AnimatorFieldLabel = "参照 Animator (Humanoid)";

        private VisualElement _root;
        private Foldout _bindingsFoldout;
        private ListView _bindingsListView;
        private Button _importButton;
        private Button _exportButton;
        private Button _humanoidButton;
        private ObjectField _animatorField;

        private readonly List<AnalogBindingEntry> _items = new List<AnalogBindingEntry>();

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            BuildJsonTextSection(_root);
            BuildBindingsSection(_root);
            BuildImportExportSection(_root);
            BuildHumanoidAutoAssignSection(_root);

            RefreshItems();
            return _root;
        }

        // ============================================================
        // Section builders
        // ============================================================

        private void BuildJsonTextSection(VisualElement root)
        {
            var jsonProperty = serializedObject.FindProperty("_jsonText");
            if (jsonProperty == null)
            {
                return;
            }

            var jsonField = new PropertyField(jsonProperty, "JSON Text");
            jsonField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshItems());
            root.Add(jsonField);

            var streamingPathProperty = serializedObject.FindProperty("_streamingAssetPath");
            if (streamingPathProperty != null)
            {
                var streamingField = new PropertyField(streamingPathProperty, "StreamingAssets Path");
                root.Add(streamingField);
            }
        }

        private void BuildBindingsSection(VisualElement root)
        {
            _bindingsFoldout = new Foldout
            {
                text = BindingsSectionLabel,
                value = true,
            };
            _bindingsFoldout.style.marginTop = 6;
            root.Add(_bindingsFoldout);

            _bindingsListView = new ListView
            {
                fixedItemHeight = 40f,
                selectionType = SelectionType.None,
                showBorder = true,
                itemsSource = _items,
                makeItem = MakeBindingRow,
                bindItem = BindBindingRow,
            };
            _bindingsListView.style.minHeight = 60f;
            _bindingsListView.style.maxHeight = 360f;
            _bindingsFoldout.Add(_bindingsListView);
        }

        private void BuildImportExportSection(VisualElement root)
        {
            var ioRow = new VisualElement();
            ioRow.style.flexDirection = FlexDirection.Row;
            ioRow.style.marginTop = 6;

            _importButton = new Button(ImportJsonInteractive)
            {
                name = "analogJsonImport",
                text = ImportButtonText,
            };
            _importButton.style.flexGrow = 1;
            ioRow.Add(_importButton);

            _exportButton = new Button(ExportJsonInteractive)
            {
                name = "analogJsonExport",
                text = ExportButtonText,
            };
            _exportButton.style.flexGrow = 1;
            _exportButton.style.marginLeft = 4;
            ioRow.Add(_exportButton);

            root.Add(ioRow);
        }

        private void BuildHumanoidAutoAssignSection(VisualElement root)
        {
            _animatorField = new ObjectField(AnimatorFieldLabel)
            {
                objectType = typeof(Animator),
                allowSceneObjects = true,
            };
            _animatorField.style.marginTop = 6;
            root.Add(_animatorField);

            _humanoidButton = new Button(AutoAssignHumanoidEyesInteractive)
            {
                name = "humanoidAutoAssign",
                text = HumanoidAutoAssignButtonText,
            };
            _humanoidButton.style.marginTop = 2;
            root.Add(_humanoidButton);
        }

        // ============================================================
        // ListView item make/bind
        // ============================================================

        private VisualElement MakeBindingRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 2;

            var head = new Label { name = "head" };
            head.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(head);

            var sub = new Label { name = "sub" };
            row.Add(sub);

            return row;
        }

        private void BindBindingRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }

            var entry = _items[index];
            var head = element.Q<Label>("head");
            if (head != null)
            {
                head.text = $"{entry.SourceId}[{entry.SourceAxis}] → {entry.TargetKind}/{entry.TargetIdentifier} ({entry.TargetAxis})";
            }

            var sub = element.Q<Label>("sub");
            if (sub != null)
            {
                var mapping = entry.Mapping;
                sub.text = $"curve={mapping.Curve.Type}, scale={mapping.Scale}, offset={mapping.Offset}";
            }
        }

        // ============================================================
        // Items refresh from SO._jsonText
        // ============================================================

        internal void RefreshItems()
        {
            _items.Clear();

            var so = target as AnalogInputBindingProfileSO;
            if (so != null)
            {
                var profile = so.ToDomain();
                var bindings = profile.Bindings.Span;
                for (int i = 0; i < bindings.Length; i++)
                {
                    _items.Add(bindings[i]);
                }
            }

            if (_bindingsListView != null)
            {
                _bindingsListView.Rebuild();
            }
        }

        // ============================================================
        // Import / Export
        // ============================================================

        private void ImportJsonInteractive()
        {
            var path = EditorUtility.OpenFilePanel(
                "Analog Binding JSON のインポート", string.Empty, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            ImportJsonFromPath(path);
        }

        private void ExportJsonInteractive()
        {
            var so = target as AnalogInputBindingProfileSO;
            var defaultName = so != null && !string.IsNullOrEmpty(so.name)
                ? so.name
                : "AnalogInputBindingProfile";
            var path = EditorUtility.SaveFilePanel(
                "Analog Binding JSON のエクスポート", string.Empty, defaultName, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            ExportJsonToPath(path);
        }

        internal void ImportJsonFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var so = target as AnalogInputBindingProfileSO;
            if (so == null)
            {
                return;
            }

            Undo.RecordObject(so, "Analog Profile JSON Import");
            so.ImportJson(path);
            EditorUtility.SetDirty(so);
            serializedObject.Update();
            RefreshItems();
        }

        internal void ExportJsonToPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var so = target as AnalogInputBindingProfileSO;
            if (so == null)
            {
                return;
            }

            so.ExportJson(path);
        }

        // ============================================================
        // Humanoid auto assign
        // ============================================================

        private void AutoAssignHumanoidEyesInteractive()
        {
            var animator = _animatorField != null ? _animatorField.value as Animator : null;
            if (animator == null)
            {
                Debug.LogWarning("[AnalogInputBindingProfileSOEditor] Humanoid 自動割当: 参照 Animator が未設定です。");
                return;
            }

            var eyes = HumanoidBoneAutoAssigner.ResolveEyeBoneNames(animator);
            if (string.IsNullOrEmpty(eyes.LeftEye) && string.IsNullOrEmpty(eyes.RightEye))
            {
                // 警告は HumanoidBoneAutoAssigner 側で出力済み
                return;
            }
            AutoAssignEyeBoneNames(eyes.LeftEye, eyes.RightEye);
        }

        /// <summary>
        /// BonePose-target binding のうち <c>targetIdentifier</c> が "LeftEye" / "RightEye"
        /// のものを実 bone 名で置換する (Req 4.4 / 10.4)。
        /// </summary>
        internal void AutoAssignEyeBoneNames(string leftEyeBoneName, string rightEyeBoneName)
        {
            var so = target as AnalogInputBindingProfileSO;
            if (so == null)
            {
                return;
            }

            var profile = so.ToDomain();
            var bindings = profile.Bindings.Span;
            if (bindings.Length == 0)
            {
                return;
            }

            var newBindings = new AnalogBindingEntry[bindings.Length];
            bool changed = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                var entry = bindings[i];
                if (entry.TargetKind != AnalogBindingTargetKind.BonePose)
                {
                    newBindings[i] = entry;
                    continue;
                }

                string newTargetIdentifier = entry.TargetIdentifier;
                if (!string.IsNullOrEmpty(leftEyeBoneName)
                    && string.Equals(entry.TargetIdentifier, "LeftEye", StringComparison.OrdinalIgnoreCase))
                {
                    newTargetIdentifier = leftEyeBoneName;
                }
                else if (!string.IsNullOrEmpty(rightEyeBoneName)
                    && string.Equals(entry.TargetIdentifier, "RightEye", StringComparison.OrdinalIgnoreCase))
                {
                    newTargetIdentifier = rightEyeBoneName;
                }

                if (!string.Equals(newTargetIdentifier, entry.TargetIdentifier, StringComparison.Ordinal))
                {
                    changed = true;
                    newBindings[i] = new AnalogBindingEntry(
                        entry.SourceId,
                        entry.SourceAxis,
                        entry.TargetKind,
                        newTargetIdentifier,
                        entry.TargetAxis,
                        entry.Mapping);
                }
                else
                {
                    newBindings[i] = entry;
                }
            }

            if (!changed)
            {
                return;
            }

            var newProfile = new AnalogInputBindingProfile(profile.Version, newBindings);
            var newJson = AnalogInputBindingJsonLoader.Save(newProfile);

            Undo.RecordObject(so, "Analog Humanoid Auto Assign");
            so.JsonText = newJson;
            EditorUtility.SetDirty(so);
            serializedObject.Update();
            RefreshItems();
        }
    }
}
