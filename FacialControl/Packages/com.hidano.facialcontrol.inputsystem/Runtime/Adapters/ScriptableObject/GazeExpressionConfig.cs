using System;
using Hidano.FacialControl.Adapters.ScriptableObject;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// <see cref="GazeBindingConfig"/> の Unity InputSystem 連携派生クラス。
    /// 入力源として <see cref="InputActionReference"/> を保持する以外は、ボーン制御・可動範囲・
    /// BlendShape 4 系統 clip などの設定を全て基底クラスから継承する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// OSC・ARKit 等の他経路から目線を駆動したい場合は、本クラスではなく基底
    /// <see cref="GazeBindingConfig"/> を直接使用するか、別の派生クラスを定義する。
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class GazeExpressionConfig : GazeBindingConfig
    {
        [Tooltip("Vector2 入力 (joystick 等) を提供する InputAction の参照。expectedControlType=Vector2 を推奨。")]
        public InputActionReference inputAction;
    }
}
