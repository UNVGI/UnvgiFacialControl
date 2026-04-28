namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 外部 (analog-input-binding 等) が active BonePose を注入する契約 (Req 11.1, 11.2, 11.3, 11.4, 11.5)。
    /// </summary>
    /// <remarks>
    /// preview.1 ではメインスレッド限定契約、hot path で alloc しない。
    /// 詳細な API 形状はタスク 6.2 で確定する。
    /// </remarks>
    public interface IBonePoseProvider
    {
    }
}
