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
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewFacialCharacter",
        menuName = "FacialControl/Facial Character",
        order = 0)]
    public sealed class FacialCharacterSO : FacialCharacterProfileSO
    {
        [Header("入力 (Input)")]
        [Tooltip("キーアサインを定義する InputActionAsset。Project ウィンドウで作成した .inputactions をここに割り当てる。")]
        [SerializeField]
        private InputActionAsset _inputActionAsset;

        [Tooltip("対象 ActionMap の名前。既定は \"Expression\"。")]
        [SerializeField]
        private string _actionMapName = "Expression";

        [Header("キーバインディング (Action ↔ Expression)")]
        [Tooltip("InputAction 名と Expression ID の対応一覧。Keyboard / Controller の振分けは ExpressionInputSourceAdapter が自動推定する (Req 7.1)。")]
        [SerializeField]
        private List<ExpressionBindingEntry> _expressionBindings = new List<ExpressionBindingEntry>();

        [Header("アナログバインディング (連続値 → BlendShape / BonePose)")]
        [Tooltip("右スティック等の連続値入力源を BlendShape または BonePose 軸へ写像する宣言。")]
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

        /// <summary>アナログバインディング (編集用)。</summary>
        public List<AnalogBindingEntrySerializable> AnalogBindings => _analogBindings;

        /// <summary>
        /// 登録された Expression バインディングを Domain 値に変換して返す。
        /// 空文字エントリ (action 名 / expression ID 未設定) はスキップ。
        /// device 種別 (Keyboard / Controller) は呼出側 (ExpressionInputSourceAdapter) が
        /// InputAction.bindings から自動推定するため category 引数は不要 (Req 7.1, 8.1)。
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
        /// </summary>
        public AnalogInputBindingProfile BuildAnalogProfile()
        {
            return FacialCharacterProfileConverter.ToAnalogProfile(_schemaVersion, _analogBindings);
        }
    }
}
