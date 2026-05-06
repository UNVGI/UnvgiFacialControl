using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// InputSystem 側が保持する gaze 用の薄い結線情報。
    /// キャラ固有のボーンパスや可動範囲は SO ルートの GazeBindingConfig が所有し、
    /// この型は対応する expressionId と InputActionReference のみを保持する。
    /// </summary>
    [Serializable]
    public sealed class InputSystemGazeBinding
    {
        [Tooltip("対応する SO ルート GazeBindingConfig.expressionId。")]
        public string expressionId;

        [Tooltip("Vector2 入力を提供する InputActionReference。ExpectedControlType=Vector2 を推奨。")]
        public InputActionReference inputActionRef;
    }
}
