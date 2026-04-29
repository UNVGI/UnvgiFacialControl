using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// BonePose の Red フェーズテスト。
    /// 0 個以上の <see cref="BonePoseEntry"/> を <see cref="System.ReadOnlyMemory{T}"/> として保持する readonly struct の振る舞いを検証する。
    /// 検証範囲:
    ///   - 0 個以上のエントリを ReadOnlyMemory&lt;BonePoseEntry&gt; として保持
    ///   - 同一 boneName の重複エントリで構築すると ArgumentException (Req 1.7)
    ///   - 防御的コピー（外部配列を後から書き換えても内部値が変わらない）
    ///   - 空エントリ（0 件）でも構築可能
    ///   - Id が string で round-trip 可能（preview.1 では参照キー未使用、空文字許容）
    /// _Requirements: 1.1, 1.2, 1.7
    /// </summary>
    [TestFixture]
    public class BonePoseTests
    {
        // --- ヘルパー ---

        private static BonePoseEntry MakeEntry(string boneName)
        {
            return new BonePoseEntry(boneName, 0f, 0f, 0f);
        }

        // --- 正常系: 構築 / Entries 保持 ---

        [Test]
        public void Constructor_EmptyEntries_CreatesInstanceWithZeroEntries()
        {
            var pose = new BonePose("pose-1", Array.Empty<BonePoseEntry>());

            Assert.AreEqual(0, pose.Entries.Length);
        }

        [Test]
        public void Constructor_NullEntries_TreatedAsEmpty()
        {
            var pose = new BonePose("pose-1", null);

            Assert.AreEqual(0, pose.Entries.Length);
        }

        [Test]
        public void Constructor_SingleEntry_StoresEntry()
        {
            var entry = new BonePoseEntry("LeftEye", 1f, 2f, 3f);

            var pose = new BonePose("pose-1", new[] { entry });

            Assert.AreEqual(1, pose.Entries.Length);
            Assert.AreEqual(entry, pose.Entries.Span[0]);
        }

        [Test]
        public void Constructor_MultipleDistinctEntries_StoresAllEntries()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 1f, 0f),
                new BonePoseEntry("Head", 0f, 0f, 1f),
            };

            var pose = new BonePose("pose-1", entries);

            Assert.AreEqual(3, pose.Entries.Length);
            Assert.AreEqual(entries[0], pose.Entries.Span[0]);
            Assert.AreEqual(entries[1], pose.Entries.Span[1]);
            Assert.AreEqual(entries[2], pose.Entries.Span[2]);
        }

        // --- ReadOnlyMemory<BonePoseEntry> 保持の構造的保証 ---

        [Test]
        public void Entries_ReturnsReadOnlyMemoryOfBonePoseEntry()
        {
            var pose = new BonePose("pose-1", new[] { MakeEntry("Head") });

            ReadOnlyMemory<BonePoseEntry> memory = pose.Entries;

            Assert.AreEqual(1, memory.Length);
        }

        // --- 重複 boneName の拒否 (Req 1.7) ---

        [Test]
        public void Constructor_DuplicateBoneName_ThrowsArgumentException()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("LeftEye", 10f, 20f, 30f),
            };

            Assert.Throws<ArgumentException>(() => new BonePose("pose-1", entries));
        }

        [Test]
        public void Constructor_DuplicateBoneNameAmongMany_ThrowsArgumentException()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
                new BonePoseEntry("Head", 0f, 0f, 0f),
                new BonePoseEntry("Head", 1f, 2f, 3f),
            };

            Assert.Throws<ArgumentException>(() => new BonePose("pose-1", entries));
        }

        [Test]
        public void Constructor_DistinctBoneNamesIncludingMultiByte_DoesNotThrow()
        {
            var entries = new[]
            {
                new BonePoseEntry("頭", 0f, 0f, 0f),
                new BonePoseEntry("左目", 0f, 0f, 0f),
                new BonePoseEntry("右目", 0f, 0f, 0f),
            };

            Assert.DoesNotThrow(() => new BonePose("pose-1", entries));
        }

        // --- 防御的コピー（Req 1.1 / 1.2 の不変性） ---

        [Test]
        public void Constructor_ExternalArrayMutated_InternalEntriesUnchanged()
        {
            var original = new BonePoseEntry("Head", 1f, 2f, 3f);
            var entries = new[] { original };

            var pose = new BonePose("pose-1", entries);

            // 外部配列を書換える
            entries[0] = new BonePoseEntry("Neck", 99f, 99f, 99f);

            // BonePose 内部は元の値を保持していること
            Assert.AreEqual(1, pose.Entries.Length);
            Assert.AreEqual(original, pose.Entries.Span[0]);
        }

        [Test]
        public void Constructor_ExternalArrayLengthSafe_InternalLengthPreserved()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
            };

            var pose = new BonePose("pose-1", entries);

            // 元配列の参照を持っていても、内部長は構築時点で固定であること
            Assert.AreEqual(2, pose.Entries.Length);
        }

        // --- Id round-trip（空文字許容） ---

        [Test]
        public void Constructor_NonEmptyId_RoundTripsId()
        {
            var pose = new BonePose("eye-look", Array.Empty<BonePoseEntry>());

            Assert.AreEqual("eye-look", pose.Id);
        }

        [Test]
        public void Constructor_EmptyId_AllowedAndRoundTrips()
        {
            var pose = new BonePose(string.Empty, Array.Empty<BonePoseEntry>());

            Assert.AreEqual(string.Empty, pose.Id);
        }

        [Test]
        public void Constructor_MultiByteId_RoundTrips()
        {
            var pose = new BonePose("視線_前方", Array.Empty<BonePoseEntry>());

            Assert.AreEqual("視線_前方", pose.Id);
        }
    }
}
