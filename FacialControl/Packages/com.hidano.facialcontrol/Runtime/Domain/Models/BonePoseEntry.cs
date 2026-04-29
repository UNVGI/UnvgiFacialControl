using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// 単一ボーンの姿勢オーバーライドエントリ。
    /// (BoneName, EulerX, EulerY, EulerZ degrees) を保持する Domain 値型。
    /// Unity 非依存（UnityEngine.* を参照しない）。
    /// </summary>
    public readonly struct BonePoseEntry : IEquatable<BonePoseEntry>
    {
        /// <summary>
        /// 対象ボーン名。null / 空文字 / 全空白は不可。
        /// </summary>
        public string BoneName { get; }

        /// <summary>
        /// X 軸オイラー角（度）
        /// </summary>
        public float EulerX { get; }

        /// <summary>
        /// Y 軸オイラー角（度）
        /// </summary>
        public float EulerY { get; }

        /// <summary>
        /// Z 軸オイラー角（度）
        /// </summary>
        public float EulerZ { get; }

        /// <summary>
        /// BonePoseEntry を生成する。
        /// </summary>
        /// <param name="boneName">対象ボーン名（null / 空文字 / 全空白は ArgumentException）</param>
        /// <param name="eulerX">X 軸オイラー角（度）</param>
        /// <param name="eulerY">Y 軸オイラー角（度）</param>
        /// <param name="eulerZ">Z 軸オイラー角（度）</param>
        /// <exception cref="ArgumentException">boneName が null / 空文字 / 全空白の場合</exception>
        public BonePoseEntry(string boneName, float eulerX, float eulerY, float eulerZ)
        {
            if (string.IsNullOrWhiteSpace(boneName))
            {
                throw new ArgumentException("boneName must be a non-empty, non-whitespace string.", nameof(boneName));
            }

            BoneName = boneName;
            EulerX = eulerX;
            EulerY = eulerY;
            EulerZ = eulerZ;
        }

        public bool Equals(BonePoseEntry other)
        {
            return string.Equals(BoneName, other.BoneName, StringComparison.Ordinal)
                && EulerX.Equals(other.EulerX)
                && EulerY.Equals(other.EulerY)
                && EulerZ.Equals(other.EulerZ);
        }

        public override bool Equals(object obj) => obj is BonePoseEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (BoneName != null ? StringComparer.Ordinal.GetHashCode(BoneName) : 0);
                hash = (hash * 31) + EulerX.GetHashCode();
                hash = (hash * 31) + EulerY.GetHashCode();
                hash = (hash * 31) + EulerZ.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(BonePoseEntry left, BonePoseEntry right) => left.Equals(right);

        public static bool operator !=(BonePoseEntry left, BonePoseEntry right) => !left.Equals(right);
    }
}
