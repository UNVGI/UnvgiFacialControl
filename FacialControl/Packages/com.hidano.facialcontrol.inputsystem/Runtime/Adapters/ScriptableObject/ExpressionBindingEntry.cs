using System;
using UnityEngine;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// InputAction 名と Expression ID を 1 対 1 に対応させる Inspector 編集用エントリ。
    /// <see cref="FacialCharacterSO"/> 内のリスト要素として使用。
    /// </summary>
    /// <remarks>
    /// device 種別 (Keyboard / Controller) は <c>ExpressionInputSourceAdapter</c> が
    /// <see cref="UnityEngine.InputSystem.InputAction.bindings"/> から自動推定するため、
    /// 旧 <c>category</c> field は撤去された (Req 7.1, tasks.md 4.6)。
    /// 押下時の挙動 (<see cref="triggerMode"/>) はバインディング毎に設定可能で、
    /// 既定は押下中のみ ON となる Hold モード。
    /// </remarks>
    [Serializable]
    public sealed class ExpressionBindingEntry
    {
        [Tooltip("対象 InputAction 名 (InputActionAsset の対象 ActionMap 配下に存在するもの)。")]
        public string actionName;

        [Tooltip("発火対象の Expression の ID (FacialCharacterSO.Expressions[].id と一致させる)。")]
        public string expressionId;

        [Tooltip(
            "押下時の動作モード。"
            + " Hold: 押している間だけ ON、ボタンを離すと OFF。"
            + " Toggle: 押すたびに ON/OFF が切替わる。")]
        public TriggerMode triggerMode = TriggerMode.Hold;
    }
}
