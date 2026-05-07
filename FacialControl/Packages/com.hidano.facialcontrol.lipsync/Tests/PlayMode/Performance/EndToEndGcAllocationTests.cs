using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using NUnit.Framework;
using UnityEngine;

namespace Hidano.FacialControl.LipSync.Tests.PlayMode.Performance
{
    [TestFixture]
    public class EndToEndGcAllocationTests
    {
        private const int FrameCount = 1000;
        private const int BlendShapeCount = 4;
        private const string PhonemeId = "A";

        private GameObject _hostGameObject;

        [TearDown]
        public void TearDown()
        {
            if (_hostGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_hostGameObject);
                _hostGameObject = null;
            }
        }

        [Test]
        public void UnityEventToAggregator_OneThousandFrames_AllocatesZeroBytes()
        {
            _hostGameObject = new GameObject("EndToEndGcAllocationTestsHost");
            var analyzer = _hostGameObject.AddComponent<uLipSync.uLipSync>();
            using var bridge = new ULipSyncEventBridge(analyzer);

            var snapshots = new[]
            {
                new PhonemeSnapshot(PhonemeId, new[] { 0.75f, 0.25f, 0.50f, 0.10f }),
            };
            using var provider = new ULipSyncProvider(bridge, snapshots, BlendShapeCount);
            var inputSource = new LipSyncInputSource(provider, BlendShapeCount);

            var profile = new FacialProfile(
                "1.0",
                new[]
                {
                    new LayerDefinition("lipSync", 0, ExclusionMode.LastWins),
                });
            var bindings = new (int layerIdx, int sourceIdx, IInputSource source)[]
            {
                (0, 0, inputSource),
            };
            using var registry = new LayerInputSourceRegistry(profile, BlendShapeCount, bindings);
            using var weightBuffer = new LayerInputSourceWeightBuffer(
                registry.LayerCount,
                registry.MaxSourcesPerLayer);
            weightBuffer.SetWeight(0, 0, 1f);

            var aggregator = new LayerInputSourceAggregator(registry, weightBuffer, BlendShapeCount);
            var priorities = new[] { 0 };
            var layerWeights = new[] { 1f };
            var finalOutput = new float[BlendShapeCount];
            var info = new uLipSync.LipSyncInfo
            {
                phoneme = PhonemeId,
                volume = 1f,
                rawVolume = 1f,
                phonemeRatios = new Dictionary<string, float>(1)
                {
                    { PhonemeId, 1f },
                },
            };

            WarmUp(analyzer, aggregator, priorities, layerWeights, finalOutput, info);
            ForceFullCollection();

            long before = ReadAllocatedBytes();
            for (int frame = 0; frame < FrameCount; frame++)
            {
                analyzer.onLipSyncUpdate.Invoke(info);
                aggregator.AggregateAndBlend(1f / 60f, priorities, layerWeights, finalOutput);
            }
            long after = ReadAllocatedBytes();
            long allocatedBytes = after - before;

            Assert.That(allocatedBytes, Is.EqualTo(0L),
                "uLipSync UnityEvent -> ULipSyncProvider -> LipSyncInputSource -> " +
                "LayerInputSourceAggregator hot path allocated " + allocatedBytes + " bytes.");
            Assert.That(finalOutput[0], Is.EqualTo(0.75f).Within(1e-6f),
                "End-to-end 計測経路が phoneme weight を最終 BlendShape 出力へ反映していること。");
        }

        private static void WarmUp(
            uLipSync.uLipSync analyzer,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights,
            float[] finalOutput,
            uLipSync.LipSyncInfo info)
        {
            for (int frame = 0; frame < 128; frame++)
            {
                analyzer.onLipSyncUpdate.Invoke(info);
                aggregator.AggregateAndBlend(1f / 60f, priorities, layerWeights, finalOutput);
            }
        }

        private static long ReadAllocatedBytes()
        {
            return AllocatedBytesReader.Read();
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private delegate long TotalAllocatedBytesReader(bool forceFullCollection);

        private static class AllocatedBytesReader
        {
            private static readonly Func<long> Reader = CreateReader();

            public static long Read()
            {
                return Reader();
            }

            private static Func<long> CreateReader()
            {
                var method = typeof(GC).GetMethod("GetTotalAllocatedBytes", new[] { typeof(bool) });
                if (method != null)
                {
                    var reader = (TotalAllocatedBytesReader)Delegate.CreateDelegate(
                        typeof(TotalAllocatedBytesReader),
                        method);
                    return () => reader(true);
                }

                return () => GC.GetTotalMemory(false);
            }
        }
    }
}
