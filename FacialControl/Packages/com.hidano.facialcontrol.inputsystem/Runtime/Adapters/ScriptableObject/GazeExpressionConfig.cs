using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// Vector2 入力 (左スティック等) で両目を同時駆動するアナログ表情の設定。
    /// 対応する <see cref="Hidano.FacialControl.Adapters.ScriptableObject.Serializable.ExpressionSerializable"/>
    /// (kind=Analog) に対して expressionId で紐づく。
    /// </summary>
    /// <remarks>
    /// 入力 Vector2 の x 成分が両目の左右回転 (バインディング側で軸を選択)、y 成分が上下回転にマップされる。
    /// 多くのモデルでは目線はボーン操作だが、BlendShape ベースのモデルもあるため両方を任意に併用できる。
    /// </remarks>
    [Serializable]
    public sealed class GazeExpressionConfig
    {
        [Tooltip("対応する Expression の ID。Expressions リスト内に id 一致するエントリが存在する必要がある。")]
        public string expressionId;

        [Tooltip("Vector2 入力 (joystick 等) を提供する InputAction の参照。expectedControlType=Vector2 を推奨。")]
        public InputActionReference inputAction;

        // ----------------- ボーン制御 (主) -----------------

        [Tooltip("左目ボーンの GameObject 階層パス (Animator のルートからの相対パス、例: Armature/.../LeftEye)。空なら無効。")]
        public string leftEyeBonePath;

        [Tooltip("左目ボーンの初期回転 (Euler 度)。アナログ入力 0 のときに保つ姿勢。アナログ入力はこの値に加算される。")]
        public Vector3 leftEyeInitialRotation;

        [Tooltip("右目ボーンの GameObject 階層パス。空なら無効。")]
        public string rightEyeBonePath;

        [Tooltip("右目ボーンの初期回転 (Euler 度)。")]
        public Vector3 rightEyeInitialRotation;

        // ----------------- BlendShape 制御 (オプション) -----------------

        [Tooltip("入力 x 成分が反映される左目の BlendShape 名。空なら無効。")]
        public string leftEyeXBlendShape;

        [Tooltip("入力 y 成分が反映される左目の BlendShape 名。空なら無効。")]
        public string leftEyeYBlendShape;

        [Tooltip("入力 x 成分が反映される右目の BlendShape 名。空なら無効。")]
        public string rightEyeXBlendShape;

        [Tooltip("入力 y 成分が反映される右目の BlendShape 名。空なら無効。")]
        public string rightEyeYBlendShape;
    }
}
