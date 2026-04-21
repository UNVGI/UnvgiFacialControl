using NUnit.Framework;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// LayerInputSourceWeightBuffer の SetWeight / GetWeight / silent clamp /
    /// SwapIfDirty (index flip + copy-forward) の契約テスト。
    /// </summary>
    /// <remarks>
    /// 3.1 の silent clamp (Req 2.5, 4.1) に加え、3.2 で導入した
    /// copy-forward アルゴリズム (Critical 1 回帰防止, Req 4.2, 4.4) を検証する。
    /// BulkScope (4.5) と範囲外キーの警告 (4.3) は後続タスクで扱う。
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
    }
}
