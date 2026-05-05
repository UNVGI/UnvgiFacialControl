namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// slug-keyed <see cref="Hidano.FacialControl.Domain.Interfaces.IInputSource"/> lookup の
    /// 中立 interface。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本 interface は <see cref="Hidano.FacialControl.Domain.Adapters.AdapterBuildContext"/> の
    /// field 型として参照されるため Domain asmdef 配置とする（Adapters は Domain を参照するが
    /// その逆は不可なので、interface のみ Domain に置き impl は Adapters に置く）。
    /// </para>
    /// <para>
    /// task 3.4 / 3.5 で <c>Register</c> / <c>TryResolve</c> 等の API を本 interface に追加し、
    /// 実装クラス <c>InputSourceRegistry</c> を Adapters/InputSources 層に配置する。
    /// 本タスク (3.1) では <see cref="AdapterBuildContext"/> から forward-reference できるよう
    /// 型のみ宣言する。
    /// </para>
    /// </remarks>
    public interface IInputSourceRegistry
    {
    }
}
