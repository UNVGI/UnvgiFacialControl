using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// ボーン姿勢オーバーライドの集合。
    /// 0 個以上の <see cref="BonePoseEntry"/> を <see cref="System.ReadOnlyMemory{T}"/> として保持する Domain 値型。
    /// Unity 非依存（UnityEngine.* を参照しない）。
    /// </summary>
    public readonly struct BonePose
    {
        private readonly string _id;
        private readonly ReadOnlyMemory<BonePoseEntry> _entries;

        /// <summary>
        /// プロファイル内識別子（preview.1 では参照キー未使用、空文字許容）。
        /// </summary>
        public string Id => _id ?? string.Empty;

        /// <summary>
        /// 姿勢オーバーライドエントリの配列。
        /// </summary>
        public ReadOnlyMemory<BonePoseEntry> Entries => _entries;

        /// <summary>
        /// BonePose を生成する。
        /// </summary>
        /// <param name="id">プロファイル内識別子（空文字許容）</param>
        /// <param name="entries">エントリ配列（防御的コピーされる、null は空配列扱い）</param>
        /// <exception cref="ArgumentException">同一 boneName のエントリが重複する場合</exception>
        public BonePose(string id, BonePoseEntry[] entries)
        {
            _id = id ?? string.Empty;

            if (entries == null || entries.Length == 0)
            {
                _entries = ReadOnlyMemory<BonePoseEntry>.Empty;
                return;
            }

            var copy = new BonePoseEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var current = entries[i];
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(copy[j].BoneName, current.BoneName, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Duplicate boneName '{current.BoneName}' detected in BonePose entries.",
                            nameof(entries));
                    }
                }
                copy[i] = current;
            }

            _entries = copy;
        }
    }
}
