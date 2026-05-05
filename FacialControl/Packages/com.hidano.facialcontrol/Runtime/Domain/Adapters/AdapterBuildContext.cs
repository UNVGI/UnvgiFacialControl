namespace Hidano.FacialControl.Domain.Adapters
{
    /// <summary>
    /// Binding が <see cref="AdapterBindingBase.OnStart"/> で必要とする中立 service 一式を渡す
    /// <c>readonly struct</c>（task 3.1 で実フィールドを充填する forward-declaration ステージ）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Domain 層配置のため UnityEngine 参照や VContainer interface は import しない（Req 4.10, 13.1）。
    /// 値型のため stack 渡し。<c>in</c> 修飾で binding の <see cref="AdapterBindingBase.OnStart"/> に渡す。
    /// </para>
    /// <para>
    /// 本タスク (2.3) では <see cref="AdapterBindingBase"/> の virtual シグネチャを成立させるための
    /// 空 struct として宣言する。task 3.1 で <c>FacialProfile</c>, <c>BlendShapeNames</c>,
    /// <c>InputSourceRegistry</c>, <c>TimeProvider</c>, <c>HostGameObject</c>,
    /// <c>LipSyncProvider</c> 等の readonly field を追加する。
    /// </para>
    /// </remarks>
    public readonly struct AdapterBuildContext
    {
    }
}
