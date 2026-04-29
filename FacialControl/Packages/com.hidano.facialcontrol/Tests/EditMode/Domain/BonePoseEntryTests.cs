using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// BonePoseEntry の Red フェーズテスト。
    /// 単一ボーンの (boneName, EulerX, EulerY, EulerZ) を保持する readonly struct の振る舞いを検証する。
    /// 検証範囲:
    ///   - boneName が null / 空文字 / 全空白の場合に ArgumentException
    ///   - Euler degrees の round-trip（コンストラクタ引数 ↔ getter）
    ///   - Equals / GetHashCode の同名・同値による等価性
    /// _Requirements: 1.2, 1.6, 4.1
    /// </summary>
    [TestFixture]
    public class BonePoseEntryTests
    {
        // --- 正常系: round-trip ---

        [Test]
        public void Constructor_ValidParameters_StoresBoneName()
        {
            var entry = new BonePoseEntry("LeftEye", 1f, 2f, 3f);

            Assert.AreEqual("LeftEye", entry.BoneName);
        }

        [Test]
        public void Constructor_ValidParameters_StoresEulerX()
        {
            var entry = new BonePoseEntry("LeftEye", 12.5f, 0f, 0f);

            Assert.AreEqual(12.5f, entry.EulerX);
        }

        [Test]
        public void Constructor_ValidParameters_StoresEulerY()
        {
            var entry = new BonePoseEntry("LeftEye", 0f, -45.25f, 0f);

            Assert.AreEqual(-45.25f, entry.EulerY);
        }

        [Test]
        public void Constructor_ValidParameters_StoresEulerZ()
        {
            var entry = new BonePoseEntry("LeftEye", 0f, 0f, 90.125f);

            Assert.AreEqual(90.125f, entry.EulerZ);
        }

        [Test]
        public void Constructor_ZeroEulers_RoundTripZero()
        {
            var entry = new BonePoseEntry("Head", 0f, 0f, 0f);

            Assert.AreEqual(0f, entry.EulerX);
            Assert.AreEqual(0f, entry.EulerY);
            Assert.AreEqual(0f, entry.EulerZ);
        }

        [Test]
        public void Constructor_NegativeEulers_RoundTripNegative()
        {
            var entry = new BonePoseEntry("Head", -10f, -20f, -30f);

            Assert.AreEqual(-10f, entry.EulerX);
            Assert.AreEqual(-20f, entry.EulerY);
            Assert.AreEqual(-30f, entry.EulerZ);
        }

        [Test]
        public void Constructor_LargeEulers_RoundTripValues()
        {
            var entry = new BonePoseEntry("Head", 180f, -180f, 359.999f);

            Assert.AreEqual(180f, entry.EulerX);
            Assert.AreEqual(-180f, entry.EulerY);
            Assert.AreEqual(359.999f, entry.EulerZ);
        }

        // --- 多バイト・特殊記号 boneName ---

        [Test]
        public void Constructor_JapaneseBoneName_StoresName()
        {
            var entry = new BonePoseEntry("頭", 0f, 0f, 0f);

            Assert.AreEqual("頭", entry.BoneName);
        }

        [Test]
        public void Constructor_BoneNameWithSpecialCharacters_StoresName()
        {
            var entry = new BonePoseEntry("Bone.L_01-eye", 0f, 0f, 0f);

            Assert.AreEqual("Bone.L_01-eye", entry.BoneName);
        }

        // --- BoneName バリデーション (Req 1.6) ---

        [Test]
        public void Constructor_NullBoneName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BonePoseEntry(null, 0f, 0f, 0f));
        }

        [Test]
        public void Constructor_EmptyBoneName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BonePoseEntry(string.Empty, 0f, 0f, 0f));
        }

        [Test]
        public void Constructor_WhitespaceBoneName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BonePoseEntry("   ", 0f, 0f, 0f));
        }

        [Test]
        public void Constructor_TabAndSpaceBoneName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BonePoseEntry("\t \t", 0f, 0f, 0f));
        }

        // --- Equals / GetHashCode 同名・同値 ---

        [Test]
        public void Equals_SameBoneNameAndEulers_ReturnsTrue()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("LeftEye", 10f, 20f, 30f);

            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_SameBoneNameAndEulers_ObjectOverloadReturnsTrue()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            object b = new BonePoseEntry("LeftEye", 10f, 20f, 30f);

            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentBoneName_ReturnsFalse()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("RightEye", 10f, 20f, 30f);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentEulerX_ReturnsFalse()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("LeftEye", 11f, 20f, 30f);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentEulerY_ReturnsFalse()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("LeftEye", 10f, 21f, 30f);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentEulerZ_ReturnsFalse()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("LeftEye", 10f, 20f, 31f);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_SameBoneNameAndEulers_ReturnsSameValue()
        {
            var a = new BonePoseEntry("LeftEye", 10f, 20f, 30f);
            var b = new BonePoseEntry("LeftEye", 10f, 20f, 30f);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
