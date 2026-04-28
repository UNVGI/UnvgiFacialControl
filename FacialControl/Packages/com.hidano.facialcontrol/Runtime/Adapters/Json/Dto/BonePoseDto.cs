using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <c>bonePoses[]</c> の 1 エントリを表す JSON 直接 DTO。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// </summary>
    [System.Serializable]
    public sealed class BonePoseDto
    {
        /// <summary>プロファイル内識別子（preview.1 では参照キー未使用、空文字許容）。</summary>
        public string id;

        /// <summary>姿勢オーバーライドエントリの配列。</summary>
        public List<BonePoseEntryDto> entries;

        /// <summary>
        /// DTO を Domain <see cref="BonePose"/> へ変換する。
        /// Domain ctor のバリデーション（boneName 空白不可、boneName 重複不可）を通す。
        /// </summary>
        public BonePose ToDomain()
        {
            BonePoseEntry[] domainEntries;
            if (entries == null || entries.Count == 0)
            {
                domainEntries = System.Array.Empty<BonePoseEntry>();
            }
            else
            {
                domainEntries = new BonePoseEntry[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    domainEntries[i] = new BonePoseEntry(
                        e != null ? e.boneName : null,
                        e != null ? e.eulerXYZ.x : 0f,
                        e != null ? e.eulerXYZ.y : 0f,
                        e != null ? e.eulerXYZ.z : 0f);
                }
            }

            return new BonePose(id, domainEntries);
        }

        /// <summary>
        /// Domain <see cref="BonePose"/> から DTO を生成する。
        /// </summary>
        public static BonePoseDto FromDomain(BonePose pose)
        {
            var dto = new BonePoseDto
            {
                id = pose.Id,
                entries = new List<BonePoseEntryDto>(pose.Entries.Length),
            };

            var span = pose.Entries.Span;
            for (int i = 0; i < span.Length; i++)
            {
                var entry = span[i];
                dto.entries.Add(new BonePoseEntryDto
                {
                    boneName = entry.BoneName,
                    eulerXYZ = new UnityEngine.Vector3(entry.EulerX, entry.EulerY, entry.EulerZ),
                });
            }

            return dto;
        }
    }
}
