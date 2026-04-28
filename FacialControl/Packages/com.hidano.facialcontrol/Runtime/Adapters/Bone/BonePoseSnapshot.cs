namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// hot path で再利用する事前確保中間 buffer (Req 6.1, 6.2, 6.3)。
    /// </summary>
    /// <remarks>
    /// 実装はタスク 6.8 で行う (Red はタスク 6.7)。
    /// 解決済み <see cref="UnityEngine.Transform"/> 配列と中間 quaternion (qx, qy, qz, qw) 配列を保持。
    /// 容量不足時のみ拡張、縮小しない。
    /// </remarks>
    public sealed class BonePoseSnapshot
    {
    }
}
