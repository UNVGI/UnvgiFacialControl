using System;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// <see cref="LipSyncInputSource"/> の EditMode 契約テスト (tasks.md 6.7)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>予約 id <c>lipsync</c> と <see cref="InputSourceType.ValueProvider"/> を持つ。</item>
    ///   <item>無音フレーム (値合計が 1e-4 未満) では <c>TryWriteValues</c> が false を返す。</item>
    ///   <item>発話フレーム (値合計が 1e-4 以上) では true を返し、
    ///     provider の値がそのまま <c>output</c> にコピーされる。</item>
    ///   <item>false を返した場合は <c>output</c> を変更しない (IInputSource 契約)。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class LipSyncInputSourceTests
    {
        /// <summary>
        /// 固定長バッファに指定値を供給するフェイク <see cref="ILipSyncProvider"/>。
        /// GC フリー契約を崩さないよう内部バッファは構築時に 1 度だけ確保する。
        /// </summary>
        private sealed class FakeLipSyncProvider : ILipSyncProvider
        {
            private readonly float[] _values;

            public FakeLipSyncProvider(float[] values)
            {
                _values = values;
            }

            public void GetLipSyncValues(Span<float> output)
            {
                int n = output.Length < _values.Length ? output.Length : _values.Length;
                for (int i = 0; i < n; i++)
                {
                    output[i] = _values[i];
                }
            }

            public ReadOnlySpan<string> BlendShapeNames => ReadOnlySpan<string>.Empty;
        }

        [Test]
        public void Id_IsReservedLipsync()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0f, 0f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 2);

            Assert.AreEqual(LipSyncInputSource.ReservedId, source.Id);
            Assert.AreEqual("lipsync", source.Id);
        }

        [Test]
        public void Type_IsValueProviderViaIInputSource()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0f, 0f });
            IInputSource source = new LipSyncInputSource(provider, blendShapeCount: 2);

            Assert.AreEqual(InputSourceType.ValueProvider, source.Type);
        }

        [Test]
        public void BlendShapeCount_MatchesConstructorArg()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0f, 0f, 0f, 0f, 0f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 5);

            Assert.AreEqual(5, source.BlendShapeCount);
        }

        [Test]
        public void Tick_IsNoOp_DoesNotThrow()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0f, 0f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 2);

            Assert.DoesNotThrow(() => source.Tick(0.016f));
        }

        [Test]
        public void Ctor_NullProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new LipSyncInputSource(null, blendShapeCount: 2));
        }

        [Test]
        public void Ctor_NegativeBlendShapeCount_Throws()
        {
            var provider = new FakeLipSyncProvider(new float[] { });
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new LipSyncInputSource(provider, blendShapeCount: -1));
        }

        [Test]
        public void TryWriteValues_Silence_ReturnsFalseAndDoesNotTouchOutput()
        {
            // 全て 0 の無音フレーム。
            var provider = new FakeLipSyncProvider(new float[] { 0f, 0f, 0f, 0f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 4);

            var output = new float[] { 7f, 7f, 7f, 7f };
            bool wrote = source.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(7f, output[0], 1e-6f,
                "false を返した場合は output を変更しないこと (IInputSource 契約)。");
            Assert.AreEqual(7f, output[1], 1e-6f);
            Assert.AreEqual(7f, output[2], 1e-6f);
            Assert.AreEqual(7f, output[3], 1e-6f);
        }

        [Test]
        public void TryWriteValues_NearZeroSum_ReturnsFalse()
        {
            // 合計が閾値 1e-4 未満 (例: 0.00005)。
            var provider = new FakeLipSyncProvider(new float[] { 0.00001f, 0.00002f, 0.00001f, 0.00001f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 4);

            var output = new float[] { 9f, 9f, 9f, 9f };
            bool wrote = source.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(9f, output[0], 1e-6f);
        }

        [Test]
        public void TryWriteValues_Speaking_ReturnsTrueAndCopiesValues()
        {
            // 発話フレーム (合計 >= 閾値)。
            var provider = new FakeLipSyncProvider(new float[] { 0.1f, 0.3f, 0.6f, 0.2f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 4);

            var output = new float[4];
            bool wrote = source.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.1f, output[0], 1e-5f);
            Assert.AreEqual(0.3f, output[1], 1e-5f);
            Assert.AreEqual(0.6f, output[2], 1e-5f);
            Assert.AreEqual(0.2f, output[3], 1e-5f);
        }

        [Test]
        public void TryWriteValues_SilentThenSpeakingThenSilent_TogglesIsValid()
        {
            // 同一インスタンスで無音 → 発話 → 無音 の切替が検出できること。
            var mutableValues = new float[] { 0f, 0f };
            var provider = new FakeLipSyncProvider(mutableValues);
            var source = new LipSyncInputSource(provider, blendShapeCount: 2);

            var output = new float[2];

            // 無音
            Assert.IsFalse(source.TryWriteValues(output));

            // 発話
            mutableValues[0] = 0.5f;
            mutableValues[1] = 0.2f;
            Assert.IsTrue(source.TryWriteValues(output));
            Assert.AreEqual(0.5f, output[0], 1e-5f);
            Assert.AreEqual(0.2f, output[1], 1e-5f);

            // 再度無音 → 前回の output 値は保持、返値は false。
            mutableValues[0] = 0f;
            mutableValues[1] = 0f;
            bool wrote = source.TryWriteValues(output);
            Assert.IsFalse(wrote);
            Assert.AreEqual(0.5f, output[0], 1e-6f,
                "false を返した場合は output を変更しないこと。");
            Assert.AreEqual(0.2f, output[1], 1e-6f);
        }

        [Test]
        public void TryWriteValues_OutputShorterThanBlendShapeCount_WritesOverlapOnly()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0.1f, 0.2f, 0.3f, 0.4f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 4);

            var output = new float[2];
            bool wrote = source.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.1f, output[0], 1e-5f);
            Assert.AreEqual(0.2f, output[1], 1e-5f);
        }

        [Test]
        public void TryWriteValues_OutputLongerThanBlendShapeCount_WritesOverlapOnly()
        {
            var provider = new FakeLipSyncProvider(new float[] { 0.1f, 0.2f });
            var source = new LipSyncInputSource(provider, blendShapeCount: 2);

            var output = new float[] { 7f, 7f, 7f, 7f };
            bool wrote = source.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.1f, output[0], 1e-5f);
            Assert.AreEqual(0.2f, output[1], 1e-5f);
            Assert.AreEqual(7f, output[2], 1e-5f, "残余は呼出側責務で保持 (IInputSource 契約)。");
            Assert.AreEqual(7f, output[3], 1e-5f);
        }
    }
}
