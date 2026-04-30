using System;
using NUnit.Framework;
using UnityEngine.Profiling;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Models
{
    /// <summary>
    /// <see cref="BonePose"/> の internal hot-path ctor (skipValidation, alloc=0) 検証
    /// （tasks.md 2.1 / Req 8.1, 8.2, 9.1）。
    /// </summary>
    /// <remarks>
    /// 既存 public ctor (`BonePose(string, BonePoseEntry[])`) は別ファイル
    /// <c>BonePoseTests</c> にて挙動・シグネチャ温存を保証する。本テストは internal 加算 ctor の
    /// alloc=0 と「引数配列を直接 backing として保持」する契約のみを検証する。
    /// </remarks>
    [TestFixture]
    public class BonePoseHotPathCtorTests
    {
        // --- 基本契約: 引数配列を直接 backing として保持（防御的コピーしない） ---

        [Test]
        public void Constructor_SkipValidation_StoresEntriesWithoutCopy()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
                new BonePoseEntry("RightEye", 4f, 5f, 6f),
            };

            var pose = new BonePose("hot-1", entries, skipValidation: true);

            Assert.AreEqual(2, pose.Entries.Length);
            Assert.AreEqual(entries[0], pose.Entries.Span[0]);
            Assert.AreEqual(entries[1], pose.Entries.Span[1]);
        }

        [Test]
        public void Constructor_SkipValidation_ExternalArrayMutation_ReflectedInternally()
        {
            // hot-path ctor は防御的コピーを skip するため、
            // 同一 backing を共有していることを確認する（呼出側が共有バッファ前提）。
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
            };

            var pose = new BonePose("hot-share", entries, skipValidation: true);

            entries[0] = new BonePoseEntry("LeftEye", 9f, 9f, 9f);

            Assert.AreEqual(1, pose.Entries.Length);
            Assert.AreEqual(entries[0], pose.Entries.Span[0]);
        }

        [Test]
        public void Constructor_SkipValidation_DuplicateBoneName_DoesNotThrow()
        {
            // 重複チェックを skip する契約。public ctor が throw するケースでも、
            // 加算 ctor では呼出側が責任を持つため throw しない。
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
                new BonePoseEntry("LeftEye", 2f, 0f, 0f),
            };

            Assert.DoesNotThrow(() => new BonePose("hot-dup", entries, skipValidation: true));
        }

        [Test]
        public void Constructor_SkipValidation_NullEntries_TreatedAsEmpty()
        {
            var pose = new BonePose("hot-null", null, skipValidation: true);

            Assert.AreEqual(0, pose.Entries.Length);
        }

        [Test]
        public void Constructor_SkipValidation_EmptyEntries_TreatedAsEmpty()
        {
            var pose = new BonePose("hot-empty", Array.Empty<BonePoseEntry>(), skipValidation: true);

            Assert.AreEqual(0, pose.Entries.Length);
        }

        [Test]
        public void Constructor_SkipValidation_RoundTripsId()
        {
            var pose = new BonePose("視線_前方", new[] { new BonePoseEntry("Head", 0f, 0f, 0f) }, skipValidation: true);

            Assert.AreEqual("視線_前方", pose.Id);
        }

        [Test]
        public void Constructor_SkipValidation_NullId_NormalizedToEmpty()
        {
            var pose = new BonePose(null, new[] { new BonePoseEntry("Head", 0f, 0f, 0f) }, skipValidation: true);

            Assert.AreEqual(string.Empty, pose.Id);
        }

        // --- hot-path GC ゼロ検証 (Req 8.1, 8.2) ---

        [Test]
        public void Constructor_SkipValidation_HotPath_TenThousandIterations_ZeroAllocation()
        {
            // 共有バッファ（_entryBuffer 想定）を 1 つ事前に確保
            var sharedBuffer = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
                new BonePoseEntry("Head", 0f, 0f, 0f),
            };
            const string id = "analog-bonepose";

            // ウォームアップ：測定と同じバッファ書換えパターンで全 JIT ブランチを事前コンパイル
            int warmLen = 0;
            for (int i = 0; i < 1000; i++)
            {
                sharedBuffer[0] = new BonePoseEntry("LeftEye", i * 0.001f, 0f, 0f);
                sharedBuffer[1] = new BonePoseEntry("RightEye", -i * 0.001f, 0f, 0f);
                sharedBuffer[2] = new BonePoseEntry("Head", 0f, i * 0.0005f, 0f);
                var warm = new BonePose(id, sharedBuffer, skipValidation: true);
                warmLen += warm.Entries.Length;
            }
            Assert.IsTrue(warmLen >= 0);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long monoBefore = Profiler.GetMonoUsedSizeLong();

            int acc = 0;
            for (int i = 0; i < 10000; i++)
            {
                // entries を毎回書換え（共有バッファ再利用）
                sharedBuffer[0] = new BonePoseEntry("LeftEye", i * 0.001f, 0f, 0f);
                sharedBuffer[1] = new BonePoseEntry("RightEye", -i * 0.001f, 0f, 0f);
                sharedBuffer[2] = new BonePoseEntry("Head", 0f, i * 0.0005f, 0f);

                var pose = new BonePose(id, sharedBuffer, skipValidation: true);
                acc += pose.Entries.Length;
            }
            Assert.IsTrue(acc > 0);

            long monoAfter = Profiler.GetMonoUsedSizeLong();
            long monoDiff = monoAfter - monoBefore;

            // Mono ヒープページノイズ許容 65536 bytes（既存 OscControllerBlendingIntegrationTests と同基準）
            Assert.LessOrEqual(monoDiff, 65536,
                $"hot-path ctor 10000 回で managed alloc がページノイズ許容 (65536 bytes) を超過: diff={monoDiff} bytes (Req 8.1, 8.2)");
        }

        // --- 既存 public ctor との独立性（既存挙動温存の補強） ---

        [Test]
        public void PublicConstructor_StillPerformsDefensiveCopy()
        {
            // 加算 ctor が public ctor の挙動を破壊していないことを補強的に確認。
            // 詳細な回帰は既存 BonePoseTests に委譲する。
            var entries = new[]
            {
                new BonePoseEntry("Head", 1f, 2f, 3f),
            };

            var pose = new BonePose("public-id", entries);

            // 外部配列を書換え
            entries[0] = new BonePoseEntry("Head", 99f, 99f, 99f);

            // public ctor は防御的コピーするため、内部値は不変
            Assert.AreEqual(new BonePoseEntry("Head", 1f, 2f, 3f), pose.Entries.Span[0]);
        }

        [Test]
        public void PublicConstructor_DuplicateBoneName_StillThrows()
        {
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
            };

            Assert.Throws<ArgumentException>(() => new BonePose("public-dup", entries));
        }
    }
}
