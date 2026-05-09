using System;
using UnityEngine;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// Analog 入力 (LT/RT 等の連続値) と Expression を結ぶ Inspector 編集用エントリ。
    /// <see cref="Hidano.FacialControl.Adapters.AdapterBindings.InputSystem.InputSystemAdapterBinding"/> の
    /// <c>_analogExpressionBindings</c> 内要素として使用する。
    /// </summary>
    /// <remarks>
    /// LT/RT を半押しすると当該 Expression の各 BlendShape が一律 0..1 の連続値で出力される
    /// (例: actionName=LeftTrigger, expressionId=smile)。
    /// </remarks>
    [Serializable]
    public sealed class AnalogExpressionBindingEntry
    {
        [Tooltip("対象 InputAction 名 (Value 型 / Pass-Through 型を推奨。Scalar 連続値を読む)。")]
        public string actionName;

        [Tooltip("駆動対象の Expression の ID (FacialCharacterProfileSO.Expressions[].id と一致させる)。")]
        public string expressionId;

        [Tooltip("入力値に対する倍率。既定 1.0 で raw scalar をそのまま使う。")]
        public float scale = 1.0f;
    }
}
