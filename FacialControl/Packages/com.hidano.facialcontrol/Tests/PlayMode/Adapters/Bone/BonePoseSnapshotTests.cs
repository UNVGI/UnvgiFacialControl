using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Bone;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Bone
{
    /// <summary>
    /// BonePoseSnapshot のテスト（Red、PlayMode）。
    ///
    /// PlayMode 配置の理由: 実 <see cref="UnityEngine.Transform"/> 参照を保持する
    /// hot path 用 buffer のため、Transform を扱える環境が必要。
    ///
    /// 検証項目:
    ///   - 解決済み <see cref="UnityEngine.Transform"/> 配列と中間 quaternion
    ///     (qx, qy, qz, qw) 配列を事前確保し、BonePose 切替時に容量不足のみ拡張、
    ///     縮小しないこと（Req 6.2, 6.3）。
    ///   - 同一 BonePose 継続中は内部配列を再確保しないこと（Req 6.3）。
    ///   - <see cref="BoneWriter"/> の <c>Apply</c> ホットパスで参照される配列が
    ///     <c>null</c> でないこと。
    ///
    /// _Requirements: 6.2, 6.3
    /// </summary>
    [TestFixture]
    public class BonePoseSnapshotTests
    {
        // ================================================================
        // 構築直後: ホットパスで参照される配列が null でない
        // ================================================================

        [Test]
        public void Construct_Default_TransformsAndRotationsArraysAreNotNull()
        {
            // BoneWriter.Apply の hot path で参照する配列は構築直後から null であってはならない。
            // 容量 0 の場合でも空配列（Length == 0）を保持し、null ガードを不要にする。
            var snapshot = new BonePoseSnapshot();

            Assert.IsNotNull(snapshot.Transforms,
                "構築直後の Transforms 配列は null であってはならない（hot path null ガード不要のため）");
            Assert.IsNotNull(snapshot.Rotations,
                "構築直後の Rotations 配列は null であってはならない（hot path null ガード不要のため）");
        }

        [Test]
        public void Construct_Default_CapacityIsNonNegative()
        {
            // 容量は 0 以上。事前確保された配列の長さは Capacity から導出可能であること。
            var snapshot = new BonePoseSnapshot();

            Assert.GreaterOrEqual(snapshot.Capacity, 0,
                "Capacity は 0 以上であること");
        }

        // ================================================================
        // EnsureCapacity: 容量不足時のみ拡張（Req 6.2）
        // ================================================================

        [Test]
        public void EnsureCapacity_LargerThanCurrent_GrowsCapacity()
        {
            // 容量不足時は EnsureCapacity で要求容量以上に拡張される（Req 6.2）。
            var snapshot = new BonePoseSnapshot();

            snapshot.EnsureCapacity(8);

            Assert.GreaterOrEqual(snapshot.Capacity, 8,
                "EnsureCapacity 後は要求容量以上の Capacity を持つこと（Req 6.2）");
        }

        [Test]
        public void EnsureCapacity_LargerThanCurrent_TransformsArrayLengthIsAtLeastRequested()
        {
            // 内部の Transforms 配列も拡張後の容量を反映していること
            // （hot path で snapshot.Transforms[i] にアクセス可能であるため）。
            var snapshot = new BonePoseSnapshot();

            snapshot.EnsureCapacity(8);

            Assert.GreaterOrEqual(snapshot.Transforms.Length, 8,
                "Transforms 配列長は要求容量以上であること");
        }

        [Test]
        public void EnsureCapacity_LargerThanCurrent_RotationsArrayLengthIsAtLeastRequested()
        {
            // Rotations は (qx, qy, qz, qw) 4 成分。flat float[] であれば 4*N、
            // struct 配列であれば N 以上の長さを持つ。
            // 4*N（flat）または N（struct array）のいずれであっても hot path で
            // 全エントリ分の領域が確保されていることを保証する：少なくとも N 以上の長さ。
            var snapshot = new BonePoseSnapshot();

            snapshot.EnsureCapacity(8);

            Assert.GreaterOrEqual(snapshot.Rotations.Length, 8,
                "Rotations 配列長は要求容量以上であること（flat float[4*N] / struct[N] いずれでも N 以上）");
        }

        // ================================================================
        // EnsureCapacity: 縮小しない（Req 6.2, 6.3）
        // ================================================================

        [Test]
        public void EnsureCapacity_SmallerThanCurrent_DoesNotShrinkCapacity()
        {
            // 一度拡張された容量は、より小さい容量を要求しても縮小しない（Req 6.2）。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(16);
            int capacityBefore = snapshot.Capacity;

            snapshot.EnsureCapacity(4);

            Assert.AreEqual(capacityBefore, snapshot.Capacity,
                "より小さい容量を要求しても Capacity は縮小しないこと（Req 6.2）");
        }

        [Test]
        public void EnsureCapacity_SmallerThanCurrent_DoesNotReallocateArrays()
        {
            // 縮小要求時は内部配列のインスタンスも入れ替わらない（既存配列を再利用）。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(16);
            var transformsBefore = snapshot.Transforms;
            var rotationsBefore = snapshot.Rotations;

            snapshot.EnsureCapacity(4);

            Assert.AreSame(transformsBefore, snapshot.Transforms,
                "縮小要求時は Transforms 配列を再確保しないこと（Req 6.2, 6.3）");
            Assert.AreSame(rotationsBefore, snapshot.Rotations,
                "縮小要求時は Rotations 配列を再確保しないこと（Req 6.2, 6.3）");
        }

        // ================================================================
        // 同一 BonePose 継続中: 内部配列を再確保しない（Req 6.3）
        // ================================================================

        [Test]
        public void EnsureCapacity_SameAsCurrent_DoesNotReallocateArrays()
        {
            // 同一 BonePose を継続して使う典型シナリオ：毎フレーム同じ N で
            // EnsureCapacity を呼んでも内部配列は同一インスタンスを返す（Req 6.3）。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(8);
            var transformsBefore = snapshot.Transforms;
            var rotationsBefore = snapshot.Rotations;

            snapshot.EnsureCapacity(8);

            Assert.AreSame(transformsBefore, snapshot.Transforms,
                "同容量再要求時は Transforms 配列を再確保しないこと（Req 6.3）");
            Assert.AreSame(rotationsBefore, snapshot.Rotations,
                "同容量再要求時は Rotations 配列を再確保しないこと（Req 6.3）");
        }

        [Test]
        public void EnsureCapacity_SameCapacityRepeated_NoGCAllocation()
        {
            // 同一 BonePose が継続している間は EnsureCapacity が呼ばれても
            // GC アロケーションが発生しないこと（Req 6.1, 6.3）。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(8);

            // ウォームアップ（JIT 等を排除）
            for (int i = 0; i < 5; i++)
            {
                snapshot.EnsureCapacity(8);
            }

            // GC 計測（Profiler API）
            var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc");

            for (int frame = 0; frame < 60; frame++)
            {
                snapshot.EnsureCapacity(8);
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"同容量での EnsureCapacity 連呼で GC アロケーションが検出されました: {gcAlloc} bytes");
        }

        [Test]
        public void EnsureCapacity_GrowOnceThenStable_LaterCallsAreZeroAlloc()
        {
            // 最初の 1 回だけ拡張、それ以降は安定容量で再呼出 → 後続呼出は 0 alloc。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(8);

            // ウォームアップ
            for (int i = 0; i < 5; i++)
            {
                snapshot.EnsureCapacity(4);
                snapshot.EnsureCapacity(8);
            }

            var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc");

            for (int frame = 0; frame < 60; frame++)
            {
                snapshot.EnsureCapacity(4); // 縮小要求は no-op
                snapshot.EnsureCapacity(8); // 同容量要求も no-op
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"安定後の EnsureCapacity 呼出で GC アロケーションが検出されました: {gcAlloc} bytes");
        }

        // ================================================================
        // 拡張後も配列が null でない（hot path 不変条件）
        // ================================================================

        [Test]
        public void EnsureCapacity_AfterGrow_TransformsAndRotationsAreNotNull()
        {
            // 容量拡張直後でも Transforms / Rotations が null でないこと
            // （拡張パスで一時的にでも null にならないこと）。
            var snapshot = new BonePoseSnapshot();
            snapshot.EnsureCapacity(16);

            Assert.IsNotNull(snapshot.Transforms,
                "拡張後の Transforms 配列は null であってはならない");
            Assert.IsNotNull(snapshot.Rotations,
                "拡張後の Rotations 配列は null であってはならない");
        }

        // ================================================================
        // BonePose 切替シナリオ: 容量不足時のみ拡張（Req 6.2）
        // ================================================================

        [Test]
        public void EnsureCapacity_GrowingSequence_OnlyGrowsWhenInsufficient()
        {
            // BonePose 切替を模したシナリオ:
            //   pose A (3 entries) → pose B (8 entries) → pose C (5 entries) → pose D (2 entries)
            //   の順で EnsureCapacity を呼ぶ。
            //   - A → B では拡張される（容量不足）。
            //   - B → C / B → D では再確保されない（容量充足）。
            var snapshot = new BonePoseSnapshot();

            snapshot.EnsureCapacity(3);
            int capacityAfterA = snapshot.Capacity;

            snapshot.EnsureCapacity(8);
            int capacityAfterB = snapshot.Capacity;
            var transformsAfterB = snapshot.Transforms;
            var rotationsAfterB = snapshot.Rotations;

            snapshot.EnsureCapacity(5);
            Assert.AreEqual(capacityAfterB, snapshot.Capacity,
                "B (8) → C (5) で容量が縮小されないこと");
            Assert.AreSame(transformsAfterB, snapshot.Transforms,
                "B → C で Transforms が再確保されないこと");
            Assert.AreSame(rotationsAfterB, snapshot.Rotations,
                "B → C で Rotations が再確保されないこと");

            snapshot.EnsureCapacity(2);
            Assert.AreEqual(capacityAfterB, snapshot.Capacity,
                "B (8) → D (2) で容量が縮小されないこと");
            Assert.AreSame(transformsAfterB, snapshot.Transforms,
                "B → D で Transforms が再確保されないこと");
            Assert.AreSame(rotationsAfterB, snapshot.Rotations,
                "B → D で Rotations が再確保されないこと");

            Assert.GreaterOrEqual(capacityAfterB, capacityAfterA,
                "A → B では容量が拡張されること（Req 6.2）");
        }
    }
}
