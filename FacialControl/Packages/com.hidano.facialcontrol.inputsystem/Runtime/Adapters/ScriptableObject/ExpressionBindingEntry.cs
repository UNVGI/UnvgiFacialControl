using System;
using UnityEngine;

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
    /// </remarks>
    [Serializable]
    public sealed class ExpressionBindingEntry
    {
        [Tooltip("対象 InputAction 名 (InputActionAsset の対象 ActionMap 配下に存在するもの)。")]
        public string actionName;

        [Tooltip("発火対象の Expression の ID (FacialCharacterSO.Expressions[].id と一致させる)。")]
        public string expressionId;
    }
}
