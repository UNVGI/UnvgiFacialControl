namespace Hidano.FacialControl.InputSystem.Adapters.ScriptableObject
{
    /// <summary>
    /// Expression トリガーバインディングの入力源カテゴリ。
    /// <see cref="FacialCharacterSO"/> に登録された 1 件のバインディングが
    /// Controller 系 / Keyboard 系 のどちらの InputSource (controller-expr / keyboard-expr)
    /// に振り分けられるかを決定する。
    /// </summary>
    public enum InputSourceCategory
    {
        Controller = 0,
        Keyboard = 1,
    }
}
