namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// <see cref="ExpressionBindingEntry"/> の動作モード。
    /// 通常表情 / 目線操作 / アナログ表情を 1 リスト内で分別するための区分。
    /// </summary>
    /// <remarks>
    /// preview.1 の破壊的変更により、旧 <c>InputSystemGazeBinding</c> と
    /// <c>AnalogExpressionBindingEntry</c> 由来の機能をこの区分で統合する。
    /// </remarks>
    public enum BindingMode
    {
        /// <summary>通常のキー押下による Expression トリガ（Hold/Toggle 駆動）。</summary>
        Normal = 0,

        /// <summary>Vector2 入力で目線操作を駆動。GazeBindingConfig と連動する。</summary>
        Gaze = 1,

        /// <summary>Scalar 連続値で Expression weight を 0..1 駆動する Analog モード。</summary>
        Analog = 2,
    }
}
