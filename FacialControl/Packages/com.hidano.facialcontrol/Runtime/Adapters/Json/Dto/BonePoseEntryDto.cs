using UnityEngine;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <c>bonePoses[].entries[]</c> の 1 エントリを表す JSON 直接 DTO（schema v1.0 専用）。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// <para>
    /// schema v2.0 では <see cref="BoneSnapshotDto"/> に役割が引き継がれる。
    /// 本クラスは Phase 2 では bridge 期間として保持され、Phase 3.6（タスク 3.6 / 3.3）で物理削除される。
    /// 詳細は <c>.kiro/specs/inspector-and-data-model-redesign/tasks.md</c> 参照。
    /// </para>
    /// </summary>
    [System.Obsolete("schema v1.0 用の DTO。schema v2.0 では BoneSnapshotDto を使用する。Phase 3.6 で物理削除予定。")]
    [System.Serializable]
    public sealed class BonePoseEntryDto
    {
        /// <summary>対象ボーン名。多バイト文字を含む任意の文字列を受理する。</summary>
        public string boneName;

        /// <summary>X/Y/Z 軸オイラー角（度、Z-X-Y Tait-Bryan 順で解釈される）。</summary>
        public Vector3 eulerXYZ;
    }
}
