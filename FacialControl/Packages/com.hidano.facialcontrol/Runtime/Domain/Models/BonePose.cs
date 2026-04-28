using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// ボーン姿勢オーバーライドの集合。
    /// 0 個以上の <see cref="BonePoseEntry"/> を <see cref="System.ReadOnlyMemory{T}"/> として保持する Domain 値型。
    /// Unity 非依存（UnityEngine.* を参照しない）。
    /// Red フェーズスタブ: 本体は task 1.4 (Green) で実装する。
    /// </summary>
    public readonly struct BonePose
    {
        /// <summary>
        /// プロファイル内識別子（preview.1 では参照キー未使用、空文字許容）。
        /// </summary>
        public string Id => throw new NotImplementedException();

        /// <summary>
        /// 姿勢オーバーライドエントリの配列。
        /// </summary>
        public ReadOnlyMemory<BonePoseEntry> Entries => throw new NotImplementedException();

        /// <summary>
        /// BonePose を生成する。
        /// </summary>
        /// <param name="id">プロファイル内識別子（空文字許容）</param>
        /// <param name="entries">エントリ配列（防御的コピーされる、null は空配列扱い）</param>
        /// <exception cref="ArgumentException">同一 boneName のエントリが重複する場合</exception>
        public BonePose(string id, BonePoseEntry[] entries)
        {
            // task 1.4 (Green) で防御的コピーと boneName 重複チェックを実装する。
            throw new NotImplementedException();
        }
    }
}
