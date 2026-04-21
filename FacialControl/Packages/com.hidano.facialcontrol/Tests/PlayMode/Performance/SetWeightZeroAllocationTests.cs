using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.PlayMode.Performance
{
    /// <summary>
    /// タスク 10.4: per-frame <c>SetWeight</c> 1000 回 × 60 frame の GC ゼロ検証 (Req 6.1 / 6.2)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件: verbose logging OFF の前提で、
    /// <see cref="LayerInputSourceRegistry"/> / <see cref="LayerInputSourceWeightBuffer"/> /
    /// <see cref="LayerInputSourceAggregator"/> のホットパス (SetWeight + Aggregate) を
    /// 60 フレーム × 1000 Set のシナリオで回しても managed 差分が 0 バイトとなること。
    /// </remarks>
    [TestFixture]
    public class SetWeightZeroAllocationTests
    {
        private const int LayerCount = 5;
        private const int SourcesPerLayer = 4;
        private const int BlendShapeCount = 200;
        private const int FramesToMeasure = 60;
        private const int SetsPerFrame = 1000;

        /// <summary>
        /// 固定値を書込む最小 <see cref="IInputSource"/> フェイク。GC ゼロ検証用。
        /// </summary>
        private sealed class FixedValueSource : IInputSource
        {
            private readonly float _value;

            public FixedValueSource(string id, int blendShapeCount, float value)
            {
                Id = id;
                BlendShapeCount = blendShapeCount;
                _value = value;
            }

            public string Id { get; }
            public InputSourceType Type => InputSourceType.ValueProvider;
            public int BlendShapeCount { get; }

            public void Tick(float deltaTime)
            {
            }

            public bool TryWriteValues(Span<float> output)
            {
                int len = output.Length < BlendShapeCount ? output.Length : BlendShapeCount;
                for (int i = 0; i < len; i++)
                {
                    output[i] = _value;
                }
                return true;
            }
        }

        [Test]
        public void SetWeightAndAggregate_60Frames_1000SetsPerFrame_ZeroManagedAllocation()
        {
            var profile = BuildProfile(LayerCount);
            var bindings = new List<(int, int, IInputSource)>(LayerCount * SourcesPerLayer);
            for (int l = 0; l < LayerCount; l++)
            {
                for (int s = 0; s < SourcesPerLayer; s++)
                {
                    bindings.Add((l, s, new FixedValueSource($"src_{l}_{s}", BlendShapeCount, 0.5f)));
                }
            }

            using var registry = new LayerInputSourceRegistry(profile, BlendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount, registry.MaxSourcesPerLayer);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, BlendShapeCount);
            // verbose logging は OFF のまま（既定）。Req 6.1 / 6.2 の 0-alloc 契約は verbose OFF 前提。

            var outputPerLayer = new LayerBlender.LayerInput[LayerCount];
            var outputSpan = outputPerLayer.AsSpan();

            // ウォームアップ: JIT / 初回キャッシュ / pending dict プールの初期化を済ませる。
            for (int w = 0; w < 2; w++)
            {
                for (int i = 0; i < SetsPerFrame; i++)
                {
                    int layerIdx = i % LayerCount;
                    int sourceIdx = (i / LayerCount) % SourcesPerLayer;
                    float value = ((i + w) % 101) / 100f;
                    weightBuffer.SetWeight(layerIdx, sourceIdx, value);
                }
                aggregator.Aggregate(deltaTime: 0.016f, outputSpan);
            }

            // 計測前に managed ヒープを安定させる。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);
            long profilerBefore = Profiler.GetTotalAllocatedMemoryLong();
            long monoBefore = Profiler.GetMonoUsedSizeLong();

            // 60 フレーム × 1000 Set のホットループ。
            for (int frame = 0; frame < FramesToMeasure; frame++)
            {
                for (int i = 0; i < SetsPerFrame; i++)
                {
                    int layerIdx = i % LayerCount;
                    int sourceIdx = (i / LayerCount) % SourcesPerLayer;
                    // 毎フレーム異なる値を書込んで SwapIfDirty の copy-forward 経路も必ず通す。
                    float value = ((i + frame) % 101) / 100f;
                    weightBuffer.SetWeight(layerIdx, sourceIdx, value);
                }

                aggregator.Aggregate(deltaTime: 0.016f, outputSpan);
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long profilerAfter = Profiler.GetTotalAllocatedMemoryLong();
            long monoAfter = Profiler.GetMonoUsedSizeLong();

            long managedDiff = managedAfter - managedBefore;
            long profilerDiff = profilerAfter - profilerBefore;
            long monoDiff = monoAfter - monoBefore;

            // Req 6.1: managed heap の差分はゼロ以下でなければならない
            // (GC が動いて減る方向は許容。増えたら Registry / WeightBuffer / Aggregator の
            //  ホットパスのいずれかがアロケートしている)。
            Assert.LessOrEqual(managedDiff, 0,
                $"60 フレーム × {SetsPerFrame} Set の managed 差分が 0 を超えました: {managedDiff} bytes " +
                $"(profiler diff={profilerDiff}, mono diff={monoDiff})");

            // Profiler.GetTotalAllocatedMemoryLong (タスク 10.4 指定の測定 API) 経由の差分も
            // 参考値として検証する。こちらは native 側の確保も含むため、ゼロに近い
            // しきい値で評価する（Unity 内部の軽微な変動は許容）。
            Assert.LessOrEqual(profilerDiff, 1024,
                $"Profiler.GetTotalAllocatedMemoryLong 差分が想定範囲を超えました: {profilerDiff} bytes " +
                $"(managed diff={managedDiff}, mono diff={monoDiff})");
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
    }
}
