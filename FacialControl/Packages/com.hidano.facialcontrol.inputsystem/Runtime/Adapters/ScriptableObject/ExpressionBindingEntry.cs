using System;
using UnityEngine;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// InputAction 名と Expression ID を 1 対 1 に対応させる Inspector 編集用エントリ。
    /// <see cref="FacialCharacterSO"/> 内のリスト要素として使用。
    /// </summary>
    [Serializable]
    public sealed class ExpressionBindingEntry
    {
        [Tooltip("対象 InputAction 名 (InputActionAsset の対象 ActionMap 配下に存在するもの)。")]
        public string actionName;

        [Tooltip("発火対象の Expression の ID (FacialCharacterSO.Expressions[].id と一致させる)。")]
        public string expressionId;

        [Tooltip("入力源カテゴリ。Controller=ゲームパッド系、Keyboard=キーボード系。InputAction の Bindings がどのデバイスを参照しているかに合わせる。")]
        public InputSourceCategory category = InputSourceCategory.Controller;
    }
}
