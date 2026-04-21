using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// LayerInputSourceAggregator の per-layer 加重和 + 最終クランプテスト (tasks.md 5.1)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item><c>w1=0.5, w2=0.5, v1[k]=1, v2[k]=1</c> → <c>output[k]=1.0</c>。</item>
    ///   <item>Σw·v > 1 の場合でもクランプのみ (Req 2.3)。</item>
    ///   <item>3 source × 2 layer の固定値入力に対し <c>output[k] = clamp01(Σ wᵢ · values_i[k])</c> が手計算と一致する (Req 2.2)。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class LayerInputSourceAggregatorTests
    {
        /// <summary>
        /// 固定値を書込む最小 <see cref="IInputSource"/> フェイク。
        /// </summary>
        private sealed class FixedValueSource : IInputSource
        {
            private readonly float _value;
            private readonly bool _isValid;

            public FixedValueSource(string id, int blendShapeCount, float value, bool isValid = true)
            {
                Id = id;
                BlendShapeCount = blendShapeCount;
                _value = value;
                _isValid = isValid;
            }

            public string Id { get; }
            public InputSourceType Type => InputSourceType.ValueProvider;
            public int BlendShapeCount { get; }

            public int TickCallCount { get; private set; }

            public void Tick(float deltaTime)
            {
                TickCallCount++;
            }

            public bool TryWriteValues(Span<float> output)
            {
                if (!_isValid)
                {
                    return false;
                }

                int len = Math.Min(output.Length, BlendShapeCount);
                for (int i = 0; i < len; i++)
                {
                    output[i] = _value;
                }
                return true;
            }
        }

        private static FacialProfile BuildProfile(int layerCount)
        {
            var layers = new LayerDefinition[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                layers[i] = new LayerDefinition($"layer{i}", priority: i, ExclusionMode.LastWins);
            }
            return new FacialProfile("1.0", layers: layers);
        }

        [Test]
        public void Aggregate_TwoSourcesHalfWeightEach_SumsToOne()
        {
            // 観測完了条件: w1=0.5, w2=0.5, v1=1, v2=1 → output[k] = 1.0
            const int blendShapeCount = 4;
            var profile = BuildProfile(layerCount: 1);

            var s0 = new FixedValueSource("s0", blendShapeCount, value: 1f);
            var s1 = new FixedValueSource("s1", blendShapeCount, value: 1f);

            var bindings = new List<(int, int, IInputSource)>
            {
                (0, 0, s0),
                (0, 1, s1),
            };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            weightBuffer.SetWeight(0, 0, 0.5f);
            weightBuffer.SetWeight(0, 1, 0.5f);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);

            var values = outputPerLayer[0].BlendShapeValues.Span;
            Assert.AreEqual(blendShapeCount, values.Length);
            for (int k = 0; k < values.Length; k++)
            {
                Assert.AreEqual(1.0f, values[k], 1e-6f,
                    $"w1=0.5*1 + w2=0.5*1 = 1.0 に一致すること (k={k})");
            }
        }

        [Test]
        public void Aggregate_SumExceedsOne_IsClamped()
        {
            // Σw·v > 1 でもクランプのみ (Req 2.3)
            const int blendShapeCount = 3;
            var profile = BuildProfile(layerCount: 1);

            var s0 = new FixedValueSource("s0", blendShapeCount, value: 1f);
            var s1 = new FixedValueSource("s1", blendShapeCount, value: 1f);
            var s2 = new FixedValueSource("s2", blendShapeCount, value: 1f);

            var bindings = new List<(int, int, IInputSource)>
            {
                (0, 0, s0),
                (0, 1, s1),
                (0, 2, s2),
            };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            weightBuffer.SetWeight(0, 0, 1f);
            weightBuffer.SetWeight(0, 1, 1f);
            weightBuffer.SetWeight(0, 2, 1f);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);

            var values = outputPerLayer[0].BlendShapeValues.Span;
            for (int k = 0; k < values.Length; k++)
            {
                Assert.AreEqual(1.0f, values[k], 1e-6f,
                    $"Σw·v = 3.0 でもクランプで 1.0 に収まること (k={k})");
            }
        }

        [Test]
        public void Aggregate_MixedWeights_MatchesManualComputation()
        {
            // 3 source × 2 layer、異なる値・異なる weight で手計算と一致すること。
            // layer 0: 3 sources, weights (0.1, 0.3, 0.2), values (0.5, 1.0, 0.25)
            //   → 0.1*0.5 + 0.3*1.0 + 0.2*0.25 = 0.05 + 0.3 + 0.05 = 0.4
            // layer 1: 3 sources, weights (0.4, 0.2, 0.1), values (0.25, 0.5, 1.0)
            //   → 0.4*0.25 + 0.2*0.5 + 0.1*1.0 = 0.1 + 0.1 + 0.1 = 0.3
            const int blendShapeCount = 2;
            var profile = BuildProfile(layerCount: 2);

            var l0s0 = new FixedValueSource("l0s0", blendShapeCount, value: 0.5f);
            var l0s1 = new FixedValueSource("l0s1", blendShapeCount, value: 1.0f);
            var l0s2 = new FixedValueSource("l0s2", blendShapeCount, value: 0.25f);
            var l1s0 = new FixedValueSource("l1s0", blendShapeCount, value: 0.25f);
            var l1s1 = new FixedValueSource("l1s1", blendShapeCount, value: 0.5f);
            var l1s2 = new FixedValueSource("l1s2", blendShapeCount, value: 1.0f);

            var bindings = new List<(int, int, IInputSource)>
            {
                (0, 0, l0s0), (0, 1, l0s1), (0, 2, l0s2),
                (1, 0, l1s0), (1, 1, l1s1), (1, 2, l1s2),
            };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            weightBuffer.SetWeight(0, 0, 0.1f);
            weightBuffer.SetWeight(0, 1, 0.3f);
            weightBuffer.SetWeight(0, 2, 0.2f);
            weightBuffer.SetWeight(1, 0, 0.4f);
            weightBuffer.SetWeight(1, 1, 0.2f);
            weightBuffer.SetWeight(1, 2, 0.1f);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[2];
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);

            var l0Values = outputPerLayer[0].BlendShapeValues.Span;
            for (int k = 0; k < l0Values.Length; k++)
            {
                Assert.AreEqual(0.4f, l0Values[k], 1e-6f,
                    $"layer0: Σ w·v = 0.4 (k={k})");
            }

            var l1Values = outputPerLayer[1].BlendShapeValues.Span;
            for (int k = 0; k < l1Values.Length; k++)
            {
                Assert.AreEqual(0.3f, l1Values[k], 1e-6f,
                    $"layer1: Σ w·v = 0.3 (k={k})");
            }
        }

        [Test]
        public void Aggregate_ZeroWeights_OutputsZero()
        {
            const int blendShapeCount = 2;
            var profile = BuildProfile(layerCount: 1);

            var s0 = new FixedValueSource("s0", blendShapeCount, value: 1f);
            var bindings = new List<(int, int, IInputSource)> { (0, 0, s0) };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);

            var values = outputPerLayer[0].BlendShapeValues.Span;
            for (int k = 0; k < values.Length; k++)
            {
                Assert.AreEqual(0f, values[k], 1e-6f,
                    $"全ての weight=0 のときは output=0 (k={k})");
            }
        }

        [Test]
        public void Aggregate_CallsTickOnSources()
        {
            // Aggregate は各 source の Tick を毎フレーム 1 回呼ぶ。
            const int blendShapeCount = 2;
            var profile = BuildProfile(layerCount: 1);
            var s0 = new FixedValueSource("s0", blendShapeCount, value: 1f);
            var bindings = new List<(int, int, IInputSource)> { (0, 0, s0) };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];

            aggregator.Aggregate(deltaTime: 0.016f, outputPerLayer);
            aggregator.Aggregate(deltaTime: 0.016f, outputPerLayer);

            Assert.AreEqual(2, s0.TickCallCount,
                "Aggregate 呼出しごとに Tick が 1 回呼ばれること");
        }

        /// <summary>
        /// 書込長を制限できる <see cref="IInputSource"/> フェイク。
        /// <c>writeLength</c> で指定した先頭 N インデックスにのみ値を書込む。
        /// 残余は <see cref="TryWriteValues"/> 内で一切触らず、Aggregator のゼロクリア契約
        /// (overlap-only) を検証するために使う。
        /// </summary>
        private sealed class PartialWriteSource : IInputSource
        {
            private readonly float _value;
            private readonly int _writeLength;

            public PartialWriteSource(string id, int blendShapeCount, int writeLength, float value)
            {
                Id = id;
                BlendShapeCount = blendShapeCount;
                _writeLength = writeLength;
                _value = value;
            }

            public string Id { get; }
            public InputSourceType Type => InputSourceType.ValueProvider;
            public int BlendShapeCount { get; }

            public void Tick(float deltaTime) { }

            public bool TryWriteValues(Span<float> output)
            {
                int len = Math.Min(_writeLength, output.Length);
                for (int i = 0; i < len; i++)
                {
                    output[i] = _value;
                }
                return true;
            }
        }

        [Test]
        public void Aggregate_SourceWritesShorterSpan_UnwrittenIndicesContributeZero()
        {
            // Req 1.3 (overlap-only): source が BlendShape 個数未満しか書込まない場合、
            // Aggregator 側で scratch が事前ゼロクリアされるため、未書込インデックスの
            // 寄与は 0 になり、前フレームの値が残らない契約を検証する。
            const int blendShapeCount = 4;
            var profile = BuildProfile(layerCount: 1);

            // 先頭 2 indices のみ 1.0 を書込む。残余 (index 2, 3) は触らない。
            var partial = new PartialWriteSource("partial", blendShapeCount, writeLength: 2, value: 1f);
            var bindings = new List<(int, int, IInputSource)> { (0, 0, partial) };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            weightBuffer.SetWeight(0, 0, 1f);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];

            // 1 フレーム目
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);
            var values1 = outputPerLayer[0].BlendShapeValues.Span;
            Assert.AreEqual(1f, values1[0], 1e-6f, "overlap 範囲の index 0 は書込値");
            Assert.AreEqual(1f, values1[1], 1e-6f, "overlap 範囲の index 1 は書込値");
            Assert.AreEqual(0f, values1[2], 1e-6f, "未書込 index 2 はゼロ (事前クリア済み)");
            Assert.AreEqual(0f, values1[3], 1e-6f, "未書込 index 3 はゼロ (事前クリア済み)");

            // 2 フレーム目 (前フレーム値の leakage が無いことを確認)
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);
            var values2 = outputPerLayer[0].BlendShapeValues.Span;
            Assert.AreEqual(0f, values2[2], 1e-6f, "前フレーム値が leakage しないこと");
            Assert.AreEqual(0f, values2[3], 1e-6f, "前フレーム値が leakage しないこと");
        }

        [Test]
        public void Aggregate_InvalidSource_ContributesZeroWithoutException()
        {
            // Req 1.4 観測完了条件:
            // 3 source のうち 1 source だけ IsValid=false のとき、残り 2 source の
            // 加重和のみが出力され、例外が発生しないこと。
            const int blendShapeCount = 3;
            var profile = BuildProfile(layerCount: 1);

            var valid0 = new FixedValueSource("valid0", blendShapeCount, value: 0.5f);
            var invalid = new FixedValueSource("invalid", blendShapeCount, value: 1f, isValid: false);
            var valid1 = new FixedValueSource("valid1", blendShapeCount, value: 0.25f);

            var bindings = new List<(int, int, IInputSource)>
            {
                (0, 0, valid0),
                (0, 1, invalid),
                (0, 2, valid1),
            };

            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            weightBuffer.SetWeight(0, 0, 0.4f);
            weightBuffer.SetWeight(0, 1, 1f);    // 無効ソースに何らかの weight が設定されていても
            weightBuffer.SetWeight(0, 2, 0.2f);  // 寄与ゼロであることを確認する。

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
            Span<LayerBlender.LayerInput> outputPerLayer = new LayerBlender.LayerInput[1];

            // 無効ソースが混在していても Aggregate は例外を出してはならない
            // (Span<T> は ref struct で Assert.DoesNotThrow のラムダに捕捉できないため直呼び出しで検証)
            aggregator.Aggregate(deltaTime: 0f, outputPerLayer);

            var values = outputPerLayer[0].BlendShapeValues.Span;
            // 期待値: 0.4 * 0.5 + 0 * 1 + 0.2 * 0.25 = 0.2 + 0 + 0.05 = 0.25
            for (int k = 0; k < values.Length; k++)
            {
                Assert.AreEqual(0.25f, values[k], 1e-6f,
                    $"無効ソース (isValid=false) の寄与は 0、残り 2 source の加重和のみ (k={k})");
            }
        }

        [Test]
        public void Constructor_NullRegistry_Throws()
        {
            using var weightBuffer = new LayerInputSourceWeightBuffer(1, 1);
            Assert.Throws<ArgumentNullException>(() =>
                new LayerInputSourceAggregator(null, weightBuffer, blendShapeCount: 4));
        }

        [Test]
        public void Constructor_NullWeightBuffer_Throws()
        {
            var profile = BuildProfile(layerCount: 1);
            var bindings = new List<(int, int, IInputSource)>();
            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount: 4, bindings);

            Assert.Throws<ArgumentNullException>(() =>
                new LayerInputSourceAggregator(registry, null, blendShapeCount: 4));
        }

        [Test]
        public void Constructor_NegativeBlendShapeCount_Throws()
        {
            var profile = BuildProfile(layerCount: 1);
            var bindings = new List<(int, int, IInputSource)>();
            using var registry = new LayerInputSourceRegistry(profile, blendShapeCount: 0, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(1, 1);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount: -1));
        }
    }
}
