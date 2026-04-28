namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 名前から <see cref="UnityEngine.Transform"/> を解決し結果をキャッシュするサービス
    /// (Req 2.1, 2.2, 2.3, 2.4, 2.5)。
    /// </summary>
    /// <remarks>
    /// 実装はタスク 6.4 で行う (Red はタスク 6.3)。
    /// preview.1: 同名 Transform 複数時は「最初の発見を採用、警告なし」。
    /// </remarks>
    public sealed class BoneTransformResolver
    {
    }
}
