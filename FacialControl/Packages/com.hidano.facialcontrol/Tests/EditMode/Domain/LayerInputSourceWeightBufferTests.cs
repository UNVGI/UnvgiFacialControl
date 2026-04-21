using NUnit.Framework;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// 3.1 LayerInputSourceWeightBuffer 基本 SetWeight / GetWeight / silent clamp の契約テスト。
    /// </summary>
    /// <remarks>
    /// 本段階では SwapIfDirty の copy-forward は未実装であり（タスク 3.2 で扱う）、
    /// 範囲外ウェイトが 0〜1 に silent clamp される仕様 (Req 2.5, 4.1) のみを検証する。
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
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var buffer = new LayerInputSourceWeightBuffer(layerCount: 1, maxSourcesPerLayer: 1);

            buffer.Dispose();
            Assert.DoesNotThrow(() => buffer.Dispose());
        }
    }
}
