namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// BoneWriter が「現在 active な BonePose」を取り出す内部契約 (Req 5.6, 11.1)。
    /// </summary>
    /// <remarks>
    /// preview.1 ではメインスレッド限定契約。
    /// 詳細な API 形状はタスク 6.2 で確定する。
    /// </remarks>
    public interface IBonePoseSource
    {
    }
}
