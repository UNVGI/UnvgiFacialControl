using NUnit.Framework;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// LayerInputSourceWeightBuffer の SetWeight / GetWeight / silent clamp /
    /// SwapIfDirty (index flip + copy-forward) / BulkScope の契約テスト。
    /// </summary>
    /// <remarks>
    /// 3.1 の silent clamp (Req 2.5, 4.1)、3.2 の copy-forward (Critical 1 回帰防止,
    /// Req 4.2, 4.4)、3.3 の BulkScope による atomic flush (Req 4.5) を検証する。
    /// 範囲外キーの警告 (4.3) は後続タスクで扱う。
    /// </remarks>
    [TestFixture]
    public class LayerInputSourceWeightBufferTests
    {
        [Test]
        public void Constructor_ExposesLayerAndSourceCapacity()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 3, maxSourcesPerLayer: 4);

            Assert.AreEqual(3, buffer.LayerCount);
            Assert.AreEqual(4, buffer.MaxSourcesPerLayer);
        }

        [Test]
        public void GetWeight_BeforeAnyWrite_ReturnsZero()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
            Assert.AreEqual(0f, buffer.GetWeight(1, 1));
        }

        [Test]
        public void SetWeight_WithinRange_IsObservableAfterSwap()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            buffer.SetWeight(1, 0, 0.5f);
            buffer.SwapIfDirty();

            Assert.AreEqual(0.5f, buffer.GetWeight(1, 0));
        }

        [Test]
        public void SetWeight_NegativeValue_SilentlyClampsToZero()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            buffer.SetWeight(0, 1, -0.5f);
            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 1));
        }

        [Test]
        public void SetWeight_AboveOne_SilentlyClampsToOne()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            buffer.SetWeight(0, 1, 2.0f);
            buffer.SwapIfDirty();

            Assert.AreEqual(1f, buffer.GetWeight(0, 1));
        }

        [Test]
        public void SetWeight_BoundaryValues_AreStoredAsIs()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 2);

            buffer.SetWeight(0, 0, 0f);
            buffer.SetWeight(0, 1, 1f);
            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
            Assert.AreEqual(1f, buffer.GetWeight(0, 1));
        }

        [Test]
        public void SwapIfDirty_WithoutAnyWrite_IsNoOp()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
        }

        [Test]
        public void SetWeight_BeforeSwap_IsNotYetObservable()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.SetWeight(0, 0, 0.75f);

            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
        }

        [Test]
        public void SetWeight_DifferentCoordinates_AreIsolatedInIndexSpace()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 3);

            buffer.SetWeight(0, 0, 0.25f);
            buffer.SetWeight(0, 2, 0.5f);
            buffer.SetWeight(1, 1, 0.75f);
            buffer.SwapIfDirty();

            Assert.AreEqual(0.25f, buffer.GetWeight(0, 0));
            Assert.AreEqual(0f, buffer.GetWeight(0, 1));
            Assert.AreEqual(0.5f, buffer.GetWeight(0, 2));
            Assert.AreEqual(0f, buffer.GetWeight(1, 0));
            Assert.AreEqual(0.75f, buffer.GetWeight(1, 1));
            Assert.AreEqual(0f, buffer.GetWeight(1, 2));
        }

        [Test]
        public void SetWeight_NegativeThenPositiveOverflow_BothClampedAcrossSwaps()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.SetWeight(0, 0, -0.5f);
            buffer.SwapIfDirty();
            Assert.AreEqual(0f, buffer.GetWeight(0, 0));

            buffer.SetWeight(0, 0, 2.0f);
            buffer.SwapIfDirty();
            Assert.AreEqual(1f, buffer.GetWeight(0, 0));
        }

        [Test]
        public void SwapIfDirty_FrameWithoutSetAfterWrite_PreservesPreviousValue_CopyForward()
        {
            // Critical 1 回帰: フレーム 1 で Set → Swap、フレーム 2 は Set なしで Swap しても
            // 直近書込値が保たれていること (スタレデータバグ防止, Req 4.2, 4.4)。
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            // Frame 1
            buffer.SetWeight(0, 0, 0.5f);
            buffer.SwapIfDirty();
            Assert.AreEqual(0.5f, buffer.GetWeight(0, 0), "Frame 1 swap 後に値が観測できること");

            // Frame 2: Set なし
            buffer.SwapIfDirty();

            Assert.AreEqual(0.5f, buffer.GetWeight(0, 0),
                "Set のないフレームで SwapIfDirty しても直近値が保持されること");
        }

        [Test]
        public void SwapIfDirty_WritesAcrossFramesOnDifferentIndexes_AllValuesPersist_CopyForward()
        {
            // Critical 1 の本質的な回帰テスト: 異なるインデックスへの Set が
            // フレームをまたいで全て保持されること。copy-forward が無い場合、
            // 旧 readBuffer に残っていた値が新 writeBuffer に引き継がれず失われる。
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 2);

            // Frame 1: (0,0) に 0.5
            buffer.SetWeight(0, 0, 0.5f);
            buffer.SwapIfDirty();

            // Frame 2: (0,1) に 0.25 のみ（(0,0) は触らない）
            buffer.SetWeight(0, 1, 0.25f);
            buffer.SwapIfDirty();

            Assert.AreEqual(0.5f, buffer.GetWeight(0, 0),
                "前フレームで書いた (0,0) が copy-forward で保持されること");
            Assert.AreEqual(0.25f, buffer.GetWeight(0, 1),
                "現フレームで書いた (0,1) が観測できること");
        }

        [Test]
        public void SwapIfDirty_ConsecutiveSwapsWithoutWrite_AreAllNoOp()
        {
            // 「Set なしの SwapIfDirty は値を変化させない」(tasks.md 3.2 観測完了条件の 2 つ目)。
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.SetWeight(0, 0, 0.8f);
            buffer.SwapIfDirty();

            buffer.SwapIfDirty();
            buffer.SwapIfDirty();
            buffer.SwapIfDirty();

            Assert.AreEqual(0.8f, buffer.GetWeight(0, 0),
                "Set の無い連続 SwapIfDirty は値を変化させないこと");
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.Dispose();
            Assert.DoesNotThrow(() => buffer.Dispose());
        }

        // ----- 3.3 BulkScope による atomic flush (Req 4.5) -----

        [Test]
        public void BulkScope_WritesBeforeDispose_AreNotObservableEvenAfterSwap()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            var scope = buffer.BeginBulk();
            scope.SetWeight(0, 0, 0.3f);
            scope.SetWeight(1, 1, 0.7f);

            // Dispose していないので pending に留まっているはず。
            // SwapIfDirty を呼んでも scope 内の書込は writeBuffer に反映されていない。
            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 0),
                "Dispose 前の BulkScope 書込は外部から観測されないこと");
            Assert.AreEqual(0f, buffer.GetWeight(1, 1),
                "Dispose 前の BulkScope 書込は外部から観測されないこと");

            scope.Dispose();
        }

        [Test]
        public void BulkScope_AfterDispose_FlushesAllWritesOnNextSwap()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(0, 0, 0.3f);
                scope.SetWeight(1, 1, 0.7f);
            }

            buffer.SwapIfDirty();

            Assert.AreEqual(0.3f, buffer.GetWeight(0, 0),
                "Dispose 後の SwapIfDirty で BulkScope の書込が一括反映されること");
            Assert.AreEqual(0.7f, buffer.GetWeight(1, 1),
                "Dispose 後の SwapIfDirty で BulkScope の書込が一括反映されること");
        }

        [Test]
        public void BulkScope_InterleavedWithSingleSet_DifferentKeys_AllPersist()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 4);

            // 単発 Set と BulkScope が異なるキーに書く → Dispose 後の Swap で両方観測できる。
            buffer.SetWeight(0, 0, 0.25f);
            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(0, 1, 0.5f);
                scope.SetWeight(0, 2, 0.75f);
            }
            buffer.SetWeight(0, 3, 1.0f);

            buffer.SwapIfDirty();

            Assert.AreEqual(0.25f, buffer.GetWeight(0, 0));
            Assert.AreEqual(0.5f, buffer.GetWeight(0, 1));
            Assert.AreEqual(0.75f, buffer.GetWeight(0, 2));
            Assert.AreEqual(1.0f, buffer.GetWeight(0, 3));
        }

        [Test]
        public void BulkScope_CommitAfterSingleSet_SameKey_BulkWinsAsLastWriter()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.SetWeight(0, 0, 0.2f);
            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(0, 0, 0.9f);
            }
            buffer.SwapIfDirty();

            Assert.AreEqual(0.9f, buffer.GetWeight(0, 0),
                "同キーへ単発 Set が先行 → Bulk が後続で Commit されたら Bulk の値が勝つ (D-7)");
        }

        [Test]
        public void BulkScope_ClampsValuesOutsideZeroOne()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 2);

            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(0, 0, -1.0f);
                scope.SetWeight(0, 1, 2.5f);
            }
            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
            Assert.AreEqual(1f, buffer.GetWeight(0, 1));
        }

        [Test]
        public void BulkScope_OutOfRangeIndex_IsSilentNoOp()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(5, 0, 0.5f);   // layerIdx 範囲外
                scope.SetWeight(0, 9, 0.5f);   // sourceIdx 範囲外
                scope.SetWeight(-1, -1, 0.5f); // 負数
            }
            buffer.SwapIfDirty();

            Assert.AreEqual(0f, buffer.GetWeight(0, 0),
                "BulkScope でも範囲外キーは silent no-op で既存値を変更しないこと");
        }

        [Test]
        public void BulkScope_EmptyScope_DoesNotTriggerSwap()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            // 先に既知値を書いてから、空の bulk scope を回す。
            buffer.SetWeight(0, 0, 0.5f);
            buffer.SwapIfDirty();
            Assert.AreEqual(0.5f, buffer.GetWeight(0, 0));

            using (var _ = buffer.BeginBulk())
            {
                // 何も書かない
            }

            // 空 scope の Dispose は dirtyTick を進めないため、Swap しても値は変わらない。
            buffer.SwapIfDirty();
            Assert.AreEqual(0.5f, buffer.GetWeight(0, 0),
                "書込の無い BulkScope は dirtyTick を進めず、readBuffer は不変であること");
        }

        [Test]
        public void BulkScope_SameKeyRepeatedWrites_LastValueWinsOnFlush()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            using (var scope = buffer.BeginBulk())
            {
                scope.SetWeight(0, 0, 0.1f);
                scope.SetWeight(0, 0, 0.5f);
                scope.SetWeight(0, 0, 0.9f);
            }
            buffer.SwapIfDirty();

            Assert.AreEqual(0.9f, buffer.GetWeight(0, 0),
                "同キーへの連続書込は scope 内 last-wins で flush されること");
        }

        [Test]
        public void BulkScope_MultipleScopesInSequence_ReuseDictFromPool()
        {
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 2);

            for (int i = 0; i < 3; i++)
            {
                using (var scope = buffer.BeginBulk())
                {
                    scope.SetWeight(0, 0, 0.1f * (i + 1));
                    scope.SetWeight(0, 1, 0.2f * (i + 1));
                }
                buffer.SwapIfDirty();
            }

            // 3 巡目の値のみが観測できる。プール再利用時に前回値が残らないこと。
            Assert.AreEqual(0.3f, buffer.GetWeight(0, 0), 1e-6f);
            Assert.AreEqual(0.6f, buffer.GetWeight(0, 1), 1e-6f);
        }

        [Test]
        public void BulkScope_AtomicFlush_ObservationIsAllOrNothingPerSwap()
        {
            // Bulk scope 内で複数 layer/source に書いた値は、Dispose した瞬間の
            // dirtyTick 進行以降に初めて観測可能。Dispose 前は全て未観測、
            // Dispose 後の一度の Swap で全て観測される (atomic flush)。
            using var buffer = new LayerInputSourceWeightBuffer(layerCount: 2, maxSourcesPerLayer: 2);

            var scope = buffer.BeginBulk();
            scope.SetWeight(0, 0, 0.1f);
            scope.SetWeight(0, 1, 0.2f);
            scope.SetWeight(1, 0, 0.3f);
            scope.SetWeight(1, 1, 0.4f);

            // Dispose 前: 全て 0 (readBuffer の初期値)
            buffer.SwapIfDirty();
            Assert.AreEqual(0f, buffer.GetWeight(0, 0));
            Assert.AreEqual(0f, buffer.GetWeight(0, 1));
            Assert.AreEqual(0f, buffer.GetWeight(1, 0));
            Assert.AreEqual(0f, buffer.GetWeight(1, 1));

            scope.Dispose();

            // Dispose 後の Swap で全て一括反映
            buffer.SwapIfDirty();
            Assert.AreEqual(0.1f, buffer.GetWeight(0, 0));
            Assert.AreEqual(0.2f, buffer.GetWeight(0, 1));
            Assert.AreEqual(0.3f, buffer.GetWeight(1, 0));
            Assert.AreEqual(0.4f, buffer.GetWeight(1, 1));
        }
    }
}
