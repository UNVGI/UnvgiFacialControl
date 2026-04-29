using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 外部 (analog-input-binding 等) が active <see cref="BonePose"/> を注入する契約 (Req 11.1, 11.2, 11.3, 11.4, 11.5)。
    /// </summary>
    /// <remarks>
    /// preview.1 ではメインスレッド限定契約。<see cref="SetActiveBonePose"/> は <c>in</c> 渡しで struct コピーを避け、
    /// hot path で alloc しない (Req 11.5)。設定された pose は次フレームの <c>Apply</c> から有効 (Req 11.2)。
    /// </remarks>
    public interface IBonePoseProvider
    {
        /// <summary>
        /// active な <see cref="BonePose"/> を差替える。次フレームの <c>Apply</c> から有効。
        /// </summary>
        /// <param name="pose"><c>in</c> 渡しで struct コピーを避ける</param>
        void SetActiveBonePose(in BonePose pose);
    }
}
