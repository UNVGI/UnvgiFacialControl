namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// active な BonePose を毎フレーム basis 相対で <see cref="UnityEngine.Transform.localRotation"/> に
    /// 書戻すサービス (Req 5.1〜5.6, 6.1〜6.3, 11.1〜11.5)。
    /// </summary>
    /// <remarks>
    /// 実装はタスク 7.2 / 7.4 / 7.6 で行う (Red はタスク 7.1 / 7.3 / 7.5)。
    /// preview.1: メインスレッド限定、hot path で alloc しない。
    /// MAJOR-1 反映: <c>RestoreInitialRotations</c> は遅延スナップショット方式 (タスク 7.5 / 7.6)。
    /// MINOR-1 反映: <c>Initialize(in BonePose, basisBoneName)</c> で basis をキャッシュ、
    /// <c>Apply()</c> は引数なし (タスク 6.2)。
    /// </remarks>
    public sealed class BoneWriter : IBonePoseSource, IBonePoseProvider
    {
    }
}
