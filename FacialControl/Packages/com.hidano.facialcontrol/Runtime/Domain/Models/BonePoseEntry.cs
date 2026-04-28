using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// 単一ボーンの姿勢オーバーライドエントリ。
    /// (BoneName, EulerX, EulerY, EulerZ degrees) を保持する Domain 値型。
    /// Red フェーズスタブ: 本体は task 1.2 (Green) で実装する。
    /// </summary>
    public readonly struct BonePoseEntry : IEquatable<BonePoseEntry>
    {
        public string BoneName => throw new NotImplementedException();
        public float EulerX => throw new NotImplementedException();
        public float EulerY => throw new NotImplementedException();
        public float EulerZ => throw new NotImplementedException();

        public BonePoseEntry(string boneName, float eulerX, float eulerY, float eulerZ)
        {
            // task 1.2 (Green) で boneName のバリデーションと値保持を実装する。
            throw new NotImplementedException();
        }

        public bool Equals(BonePoseEntry other) => throw new NotImplementedException();

        public override bool Equals(object obj) => throw new NotImplementedException();

        public override int GetHashCode() => throw new NotImplementedException();

        public static bool operator ==(BonePoseEntry left, BonePoseEntry right) => left.Equals(right);

        public static bool operator !=(BonePoseEntry left, BonePoseEntry right) => !left.Equals(right);
    }
}
