using System;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// BonePose 1 件分の Unity Serializable 投影 (旧 JSON: bonePoses[])。
    /// Domain の <see cref="Hidano.FacialControl.Domain.Models.BonePose"/> を Inspector 編集用に表現。
    /// </summary>
    [Serializable]
    public sealed class BonePoseSerializable
    {
        [Tooltip("BonePose 識別子 (空文字許容)。Expression からの参照キーは preview 段階では未使用。")]
        public string id;

        [Tooltip("オーバーライドするボーンと顔相対 Euler 角の配列。同名 boneName の重複は不可。")]
        public BonePoseEntrySerializable[] entries = Array.Empty<BonePoseEntrySerializable>();
    }

    /// <summary>
    /// BonePose のエントリ 1 件分 (boneName + eulerXYZ degrees)。
    /// </summary>
    [Serializable]
    public sealed class BonePoseEntrySerializable
    {
        [Tooltip("対象ボーン名 (Animator のヒエラルキー上の Transform 名)。")]
        public string boneName;

        [Tooltip("Euler 角 (度)。X / Y / Z はそれぞれ 顔相対の roll / yaw / pitch 等に対応。")]
        public Vector3 eulerXYZ;
    }
}
