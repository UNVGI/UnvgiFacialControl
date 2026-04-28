using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// FacialProfileSO Inspector に表示する BonePose 一覧 / 編集ビュー (Req 9.1 / 9.2 / 9.4 / 9.6)。
    /// UI Toolkit (<see cref="Foldout"/> + <see cref="ListView"/>) で BonePose を一覧表示し、
    /// 各エントリ行で boneName 入力 (<see cref="DropdownField"/> または <see cref="TextField"/>)、
    /// Euler 入力 (<see cref="Vector3Field"/>)、削除ボタンを提供する。
    /// </summary>
    /// <remarks>
    /// preview.1 では 1 BonePose = 1 entry の単純化モデルを採用する。
    /// AddBonePose / RemoveBonePoseAt / UpdateEntryEuler の各操作は
    /// <see cref="EditorUtility.SetDirty(UnityEngine.Object)"/> を呼び、SO を Undo 連携付きで永続化する。
    /// </remarks>
    internal sealed class FacialProfileSO_BonePoseView : IDisposable
    {
        private const string SectionLabel = "BonePose 一覧";

        private readonly FacialProfileSO _target;
        private readonly VisualElement _root;
        private readonly Foldout _foldout;
        private readonly ListView _listView;
        private readonly Button _autoAssignButton;
        private readonly List<BonePoseSerializable> _items = new List<BonePoseSerializable>();

        public VisualElement RootElement => _root;

        public FacialProfileSO_BonePoseView(FacialProfileSO target)
        {
            _target = target;

            _root = new VisualElement();

            _foldout = new Foldout
            {
                text = SectionLabel,
                value = true,
            };
            _root.Add(_foldout);

            _listView = new ListView
            {
                fixedItemHeight = 24f,
                selectionType = SelectionType.None,
                showBorder = true,
                itemsSource = _items,
                makeItem = MakeItem,
                bindItem = BindItem,
            };
            _listView.style.minHeight = 40f;
            _listView.style.maxHeight = 240f;
            _foldout.Add(_listView);

            var addButton = new Button(AddBonePose) { text = "BonePose 追加" };
            addButton.style.marginTop = 4;
            _foldout.Add(addButton);

            _autoAssignButton = new Button(AutoAssignHumanoidEyes)
            {
                name = "humanoidAutoAssign",
                text = "Humanoid 自動アサイン",
            };
            _autoAssignButton.style.marginTop = 2;
            _foldout.Add(_autoAssignButton);
            UpdateAutoAssignButtonState();

            var importExportContainer = new VisualElement();
            importExportContainer.style.flexDirection = FlexDirection.Row;
            importExportContainer.style.marginTop = 4;

            var importButton = new Button(ImportBonePosesFromJson)
            {
                name = "bonePoseJsonImport",
                text = "JSON Import",
            };
            importButton.style.flexGrow = 1;
            importExportContainer.Add(importButton);

            var exportButton = new Button(ExportBonePosesToJson)
            {
                name = "bonePoseJsonExport",
                text = "JSON Export",
            };
            exportButton.style.flexGrow = 1;
            exportButton.style.marginLeft = 4;
            importExportContainer.Add(exportButton);

            _foldout.Add(importExportContainer);

            RefreshItems();
        }

        public void Dispose()
        {
            // 現状解放対象の購読は持たないが、IDisposable 契約は将来拡張のため維持する
        }

        // ============================================================
        // 公開操作 API （テストおよびインライン UI から呼ばれる）
        // ============================================================

        internal void AddBonePose()
        {
            if (_target == null)
                return;

            var existing = _target.BonePoses ?? Array.Empty<BonePoseSerializable>();
            var newArray = new BonePoseSerializable[existing.Length + 1];
            Array.Copy(existing, newArray, existing.Length);
            newArray[existing.Length] = new BonePoseSerializable
            {
                id = string.Empty,
                entries = new[]
                {
                    new BonePoseEntrySerializable
                    {
                        boneName = string.Empty,
                        eulerXYZ = Vector3.zero,
                    },
                },
            };

            Undo.RecordObject(_target, "BonePose 追加");
            _target.BonePoses = newArray;
            EditorUtility.SetDirty(_target);

            RefreshItems();
        }

        internal void RemoveBonePoseAt(int index)
        {
            if (_target == null)
                return;

            var existing = _target.BonePoses;
            if (existing == null || index < 0 || index >= existing.Length)
                return;

            var newArray = new BonePoseSerializable[existing.Length - 1];
            int dest = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                if (i == index)
                    continue;
                newArray[dest++] = existing[i];
            }

            Undo.RecordObject(_target, "BonePose 削除");
            _target.BonePoses = newArray;
            EditorUtility.SetDirty(_target);

            RefreshItems();
        }

        internal void UpdateEntryEuler(int bonePoseIndex, int entryIndex, Vector3 newEuler)
        {
            if (_target == null)
                return;

            var poses = _target.BonePoses;
            if (poses == null || bonePoseIndex < 0 || bonePoseIndex >= poses.Length)
                return;

            var pose = poses[bonePoseIndex];
            if (pose == null || pose.entries == null)
                return;

            if (entryIndex < 0 || entryIndex >= pose.entries.Length)
                return;

            var entry = pose.entries[entryIndex];
            if (entry == null)
                return;

            Undo.RecordObject(_target, "BonePose Euler 編集");
            entry.eulerXYZ = newEuler;
            EditorUtility.SetDirty(_target);
        }

        /// <summary>
        /// 参照モデルが Humanoid の場合に LeftEye / RightEye の bone 名を取得し、
        /// それぞれを 1 entry 持つ BonePose として <c>_bonePoses</c> 末尾に追加する (Req 3.3 / 9.3)。
        /// 非 Humanoid または Animator 不在のときは Warning ログ + no-op。
        /// </summary>
        internal void AutoAssignHumanoidEyes()
        {
            if (_target == null)
                return;

            var animator = ResolveAnimator();
            if (animator == null)
            {
                Debug.LogWarning("[FacialProfileSO_BonePoseView] Humanoid 自動アサイン: 参照モデルに Animator が存在しません。");
                return;
            }

            var eyes = HumanoidBoneAutoAssigner.ResolveEyeBoneNames(animator);
            bool hasLeft = !string.IsNullOrEmpty(eyes.LeftEye);
            bool hasRight = !string.IsNullOrEmpty(eyes.RightEye);
            if (!hasLeft && !hasRight)
            {
                // 警告は HumanoidBoneAutoAssigner 側で出力済み
                return;
            }

            int addCount = (hasLeft ? 1 : 0) + (hasRight ? 1 : 0);
            var existing = _target.BonePoses ?? Array.Empty<BonePoseSerializable>();
            var newArray = new BonePoseSerializable[existing.Length + addCount];
            Array.Copy(existing, newArray, existing.Length);

            int dest = existing.Length;
            if (hasLeft)
            {
                newArray[dest++] = CreateSinglePoseFor(eyes.LeftEye);
            }
            if (hasRight)
            {
                newArray[dest++] = CreateSinglePoseFor(eyes.RightEye);
            }

            Undo.RecordObject(_target, "Humanoid 自動アサイン");
            _target.BonePoses = newArray;
            EditorUtility.SetDirty(_target);

            RefreshItems();
        }

        /// <summary>
        /// 外部 JSON ファイルを選択してパースし、<c>bonePoses</c> ブロックの内容で
        /// SO の <c>_bonePoses</c> を上書きする (Req 9.5)。
        /// </summary>
        internal void ImportBonePosesFromJson()
        {
            if (_target == null)
                return;

            var importPath = EditorUtility.OpenFilePanel("BonePose JSON のインポート", string.Empty, "json");
            if (string.IsNullOrEmpty(importPath))
                return;

            try
            {
                var json = File.ReadAllText(importPath, Encoding.UTF8);
                var parser = new SystemTextJsonParser();
                var profile = parser.ParseProfile(json);
                var serializable = FacialProfileMapper.ToSerializableBonePoses(profile.BonePoses);

                Undo.RecordObject(_target, "BonePose JSON インポート");
                _target.BonePoses = serializable;
                EditorUtility.SetDirty(_target);

                RefreshItems();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialProfileSO_BonePoseView] BonePose JSON インポートエラー: {ex}");
            }
        }

        /// <summary>
        /// 現在の <c>_bonePoses</c> を Domain の <see cref="BonePose"/> 配列に変換し、
        /// JSON 文字列化して外部ファイルに保存する (Req 9.5)。
        /// </summary>
        internal void ExportBonePosesToJson()
        {
            if (_target == null)
                return;

            var exportPath = EditorUtility.SaveFilePanel("BonePose JSON のエクスポート", string.Empty, "boneposes.json", "json");
            if (string.IsNullOrEmpty(exportPath))
                return;

            try
            {
                var domainBonePoses = FacialProfileMapper.ToDomainBonePoses(_target.BonePoses);
                var profile = new FacialProfile(
                    schemaVersion: "1.0",
                    bonePoses: domainBonePoses);

                var parser = new SystemTextJsonParser();
                var json = parser.SerializeProfile(profile);
                File.WriteAllText(exportPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialProfileSO_BonePoseView] BonePose JSON エクスポートエラー: {ex}");
            }
        }

        internal void UpdateEntryBoneName(int bonePoseIndex, int entryIndex, string newBoneName)
        {
            if (_target == null)
                return;

            var poses = _target.BonePoses;
            if (poses == null || bonePoseIndex < 0 || bonePoseIndex >= poses.Length)
                return;

            var pose = poses[bonePoseIndex];
            if (pose == null || pose.entries == null)
                return;

            if (entryIndex < 0 || entryIndex >= pose.entries.Length)
                return;

            var entry = pose.entries[entryIndex];
            if (entry == null)
                return;

            Undo.RecordObject(_target, "BonePose boneName 編集");
            entry.boneName = newBoneName ?? string.Empty;
            EditorUtility.SetDirty(_target);
        }

        // ============================================================
        // 内部: ListView のレンダリング
        // ============================================================

        private void RefreshItems()
        {
            _items.Clear();
            if (_target != null && _target.BonePoses != null)
            {
                for (int i = 0; i < _target.BonePoses.Length; i++)
                {
                    _items.Add(_target.BonePoses[i] ?? new BonePoseSerializable());
                }
            }
            _listView.Rebuild();
            UpdateAutoAssignButtonState();
        }

        private static BonePoseSerializable CreateSinglePoseFor(string boneName)
        {
            return new BonePoseSerializable
            {
                id = boneName ?? string.Empty,
                entries = new[]
                {
                    new BonePoseEntrySerializable
                    {
                        boneName = boneName ?? string.Empty,
                        eulerXYZ = Vector3.zero,
                    },
                },
            };
        }

        private Animator ResolveAnimator()
        {
            if (_target == null || _target.ReferenceModel == null)
                return null;

            var animator = _target.ReferenceModel.GetComponent<Animator>();
            if (animator == null)
                animator = _target.ReferenceModel.GetComponentInChildren<Animator>(includeInactive: true);
            return animator;
        }

        private void UpdateAutoAssignButtonState()
        {
            if (_autoAssignButton == null)
                return;

            var animator = ResolveAnimator();
            bool isHumanoid =
                animator != null
                && animator.avatar != null
                && animator.avatar.isHuman;
            _autoAssignButton.SetEnabled(isHumanoid);
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var candidates = GetBoneNameCandidates();
            VisualElement boneNameInput;
            if (candidates != null && candidates.Length > 0)
            {
                var dropdown = new DropdownField(new List<string>(candidates), 0)
                {
                    name = "boneName",
                };
                dropdown.style.flexGrow = 1;
                dropdown.style.minWidth = 96;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    if (row.userData is int idx)
                        UpdateEntryBoneName(idx, 0, evt.newValue);
                });
                boneNameInput = dropdown;
            }
            else
            {
                var textField = new TextField
                {
                    name = "boneName",
                };
                textField.style.flexGrow = 1;
                textField.style.minWidth = 96;
                textField.RegisterValueChangedCallback(evt =>
                {
                    if (row.userData is int idx)
                        UpdateEntryBoneName(idx, 0, evt.newValue);
                });
                boneNameInput = textField;
            }
            row.Add(boneNameInput);

            var eulerField = new Vector3Field
            {
                name = "eulerXYZ",
            };
            eulerField.style.flexGrow = 2;
            eulerField.style.minWidth = 120;
            eulerField.RegisterValueChangedCallback(evt =>
            {
                if (row.userData is int idx)
                    UpdateEntryEuler(idx, 0, evt.newValue);
            });
            row.Add(eulerField);

            var deleteButton = new Button(() =>
            {
                if (row.userData is int idx)
                    RemoveBonePoseAt(idx);
            })
            {
                name = "delete",
                text = "×",
            };
            deleteButton.style.width = 24;
            deleteButton.style.height = 20;
            deleteButton.style.marginLeft = 2;
            row.Add(deleteButton);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _items.Count)
                return;

            element.userData = index;

            var pose = _items[index];
            BonePoseEntrySerializable firstEntry = null;
            if (pose != null && pose.entries != null && pose.entries.Length > 0)
            {
                firstEntry = pose.entries[0];
            }

            string boneName = firstEntry?.boneName ?? string.Empty;
            Vector3 euler = firstEntry?.eulerXYZ ?? Vector3.zero;

            var dropdown = element.Q<DropdownField>("boneName");
            if (dropdown != null)
            {
                if (!string.IsNullOrEmpty(boneName) && (dropdown.choices == null || !dropdown.choices.Contains(boneName)))
                {
                    var choices = dropdown.choices != null ? new List<string>(dropdown.choices) : new List<string>();
                    choices.Insert(0, boneName);
                    dropdown.choices = choices;
                }
                dropdown.SetValueWithoutNotify(boneName);
            }
            else
            {
                var textField = element.Q<TextField>("boneName");
                textField?.SetValueWithoutNotify(boneName);
            }

            var eulerField = element.Q<Vector3Field>("eulerXYZ");
            eulerField?.SetValueWithoutNotify(euler);
        }

        private string[] GetBoneNameCandidates()
        {
#if UNITY_EDITOR
            if (_target != null && _target.ReferenceModel != null)
            {
                return BoneNameProvider.GetBoneNames(_target.ReferenceModel);
            }
#endif
            return Array.Empty<string>();
        }
    }
}
