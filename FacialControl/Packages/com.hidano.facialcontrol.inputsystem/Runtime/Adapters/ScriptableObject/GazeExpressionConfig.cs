using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// Vector2 入力（左スティック等）で両目を同時駆動する目線表情の設定。
    /// 対応する <see cref="Hidano.FacialControl.Adapters.ScriptableObject.Serializable.ExpressionSerializable"/>
    /// に対して expressionId で紐づく。
    /// </summary>
    /// <remarks>
    /// 入力 Vector2 の x 成分が左右両目の X 軸 BlendShape に、y 成分が左右両目の Y 軸 BlendShape に
    /// それぞれ反映される。BlendShape の値域は 0〜100 を想定し、上流の InputProcessor チェーン
    /// (analogScale 等) で適切にスケールされる。
    /// </remarks>
    [Serializable]
    public sealed class GazeExpressionConfig
    {
        [Tooltip("対応する Expression の ID。Expressions リスト内に id 一致するエントリが存在する必要がある。")]
        public string expressionId;

        [Tooltip("Vector2 入力 (joystick 等) を提供する InputAction の参照。expectedControlType=Vector2 を推奨。")]
        public InputActionReference inputAction;

        [Tooltip("入力 x 成分が反映される左目の BlendShape 名。空なら無効化。")]
        public string leftEyeXBlendShape;

        [Tooltip("入力 y 成分が反映される左目の BlendShape 名。空なら無効化。")]
        public string leftEyeYBlendShape;

        [Tooltip("入力 x 成分が反映される右目の BlendShape 名。空なら無効化。")]
        public string rightEyeXBlendShape;

        [Tooltip("入力 y 成分が反映される右目の BlendShape 名。空なら無効化。")]
        public string rightEyeYBlendShape;
    }
}
