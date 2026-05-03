using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using DomainInputBinding = Hidano.FacialControl.Domain.Models.InputBinding;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// キャラクター単位の表情データ + 入力バインディング統合 ScriptableObject。
    /// FacialControl のセットアップに必要な全情報を 1 アセットに集約し、
    /// ペアの InputActionAsset 1 個と組で動作する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 結線: Scene 上の <c>FacialController</c> に本 SO を 1 個 D&amp;D するだけで完了する。
    /// InputActionAsset は本 SO 内部に参照を持つため、別途 D&amp;D 不要。
    /// </para>
    /// <para>
    /// データソースは StreamingAssets/FacialControl/{SO 名}/profile.json (起動時に存在すれば優先)、
    /// 不在時は本 SO の Inspector データからフォールバック構築 (3-B モデル、preview)。
    /// </para>
    /// <para>
    /// 目線等の Vector2 アナログ表情は <see cref="GazeConfigs"/> として保持され、所属 Expression
    /// (<see cref="ExpressionKind.EyeLook"/>) と id で紐づく。先代の per-axis <c>_analogBindings</c>
    /// は legacy として温存されるが <see cref="GazeConfigs"/> が 1 件でもあれば無視される。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewFacialCharacter",
        menuName = "FacialControl/Facial Character",
        order = 0)]
    public sealed class FacialCharacterSO : FacialCharacterProfileSO
    {
        [Tooltip("キーアサインを定義する InputActionAsset。Project ウィンドウで作成した .inputactions をここに割り当てる。")]
        [SerializeField]
        private InputActionAsset _inputActionAsset;

        [Tooltip("対象 ActionMap の名前。既定は \"Expression\"。")]
        [SerializeField]
        private string _actionMapName = "Expression";

        [Tooltip("InputAction 名と Expression ID の対応一覧。")]
        [SerializeField]
        private List<ExpressionBindingEntry> _expressionBindings = new List<ExpressionBindingEntry>();

        [Tooltip("EyeLook 系 Expression ごとの Vector2 入力 + 両目 BlendShape 設定。"
            + " Expressions リストの ExpressionKind.EyeLook なエントリと expressionId で紐づく。")]
        [SerializeField]
        private List<GazeExpressionConfig> _gazeConfigs = new List<GazeExpressionConfig>();

        // legacy: per-axis アナログバインディング。新規入力は GazeConfigs で行うため Inspector では非表示。
        // 既存アセット互換のため残置するが、GazeConfigs が 1 件でもあれば無視される。
        [HideInInspector]
        [SerializeField]
        private List<AnalogBindingEntrySerializable> _analogBindings = new List<AnalogBindingEntrySerializable>();

        /// <summary>キーアサインを定義する InputActionAsset。</summary>
        public InputActionAsset InputActionAsset
        {
            get => _inputActionAsset;
            set => _inputActionAsset = value;
        }

        /// <summary>対象 ActionMap の名前。</summary>
        public string ActionMapName
        {
            get => _actionMapName;
            set => _actionMapName = value;
        }

        /// <summary>キーバインディング (編集用)。</summary>
        public List<ExpressionBindingEntry> ExpressionBindings => _expressionBindings;

        /// <summary>EyeLook 系 Expression の Vector2 アナログ設定 (編集用)。</summary>
        public List<GazeExpressionConfig> GazeConfigs => _gazeConfigs;

        /// <summary>
        /// 登録された Expression バインディングを Domain 値に変換して返す。
        /// 空文字エントリ (action 名 / expression ID 未設定) はスキップ。
        /// </summary>
        public IReadOnlyList<DomainInputBinding> GetExpressionBindings()
        {
            if (_expressionBindings == null || _expressionBindings.Count == 0)
            {
                return Array.Empty<DomainInputBinding>();
            }

            var result = new List<DomainInputBinding>(_expressionBindings.Count);
            for (int i = 0; i < _expressionBindings.Count; i++)
            {
                var entry = _expressionBindings[i];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.actionName)
                    || string.IsNullOrWhiteSpace(entry.expressionId))
                {
                    continue;
                }
                result.Add(new DomainInputBinding(entry.actionName, entry.expressionId));
            }
            return result;
        }

        /// <summary>
        /// アナログバインディングを Domain プロファイルに変換して返す。
        /// <see cref="_gazeConfigs"/> が 1 件でもあれば、そこから 4 軸 (LeftEye-X/Y, RightEye-X/Y) の
        /// AnalogBindingEntry を生成して返す。空であれば legacy <see cref="_analogBindings"/> を変換する。
        /// </summary>
        public AnalogInputBindingProfile BuildAnalogProfile()
        {
            if (_gazeConfigs != null && _gazeConfigs.Count > 0)
            {
                return BuildAnalogProfileFromGazeConfigs();
            }
            return FacialCharacterProfileConverter.ToAnalogProfile(_schemaVersion, _analogBindings);
        }

        private AnalogInputBindingProfile BuildAnalogProfileFromGazeConfigs()
        {
            var entries = new List<AnalogBindingEntry>(_gazeConfigs.Count * 4);
            for (int i = 0; i < _gazeConfigs.Count; i++)
            {
                var cfg = _gazeConfigs[i];
                if (cfg == null || cfg.inputAction == null || cfg.inputAction.action == null)
                {
                    continue;
                }
                string sourceId = cfg.inputAction.action.name;
                if (string.IsNullOrWhiteSpace(sourceId))
                {
                    continue;
                }

                AppendBlendShapeBinding(entries, sourceId, sourceAxis: 0, cfg.leftEyeXBlendShape);
                AppendBlendShapeBinding(entries, sourceId, sourceAxis: 1, cfg.leftEyeYBlendShape);
                AppendBlendShapeBinding(entries, sourceId, sourceAxis: 0, cfg.rightEyeXBlendShape);
                AppendBlendShapeBinding(entries, sourceId, sourceAxis: 1, cfg.rightEyeYBlendShape);
            }
            return new AnalogInputBindingProfile(_schemaVersion, entries.ToArray());
        }

        private static void AppendBlendShapeBinding(
            List<AnalogBindingEntry> sink, string sourceId, int sourceAxis, string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
            {
                return;
            }
            try
            {
                sink.Add(new AnalogBindingEntry(
                    sourceId,
                    sourceAxis,
                    AnalogBindingTargetKind.BlendShape,
                    blendShapeName,
                    AnalogTargetAxis.X));
            }
            catch (ArgumentException)
            {
                // targetIdentifier が空など。スキップ。
            }
        }
    }
}
