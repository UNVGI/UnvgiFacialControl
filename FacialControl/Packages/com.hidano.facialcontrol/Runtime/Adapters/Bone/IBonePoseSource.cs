using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// BoneWriter 自身が実装する内部契約。現在 active な <see cref="BonePose"/> を返す (Req 5.6, 11.1)。
    /// </summary>
    /// <remarks>
    /// preview.1 ではメインスレッド限定契約。hot path で alloc しない (Req 11.5)。
    /// </remarks>
    public interface IBonePoseSource
    {
        /// <summary>
        /// 現在 active な <see cref="BonePose"/> を返す。
        /// </summary>
        /// <returns>active BonePose（未設定時は空 BonePose）</returns>
        BonePose GetActiveBonePose();
    }
}
