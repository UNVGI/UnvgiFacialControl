using System;
using System.Collections.Generic;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using NUnit.Framework;
using Unity.Profiling;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Performance
{
    [TestFixture]
    public class ULipSyncProviderAllocationTests
    {
        private const int Iterations = 10000;

        [Test]
        public void OnLipSyncUpdateAndGetLipSyncValues_TenThousandIterations_ZeroBytes()
        {
            var source = new FakeULipSyncEventSource();
            var snapshots = new[]
            {
                new PhonemeSnapshot("A", new[] { 0.50f, 0.00f, 0.25f, 0.10f }),
                new PhonemeSnapshot("I", new[] { 0.20f, 0.40f, 0.00f, 0.30f }),
                new PhonemeSnapshot("O", new[] { 0.00f, 0.10f, 0.75f, 0.20f }),
            };
            using var provider = new ULipSyncProvider(source, snapshots, blendShapeCount: 4);
            var output = new float[4];
            var info = new uLipSync.LipSyncInfo
            {
                volume = 0.8f,
                phonemeRatios = new Dictionary<string, float>(3)
                {
                    { "A", 0.70f },
                    { "I", 0.15f },
                    { "O", 0.35f },
                },
            };

            for (int i = 0; i < 128; i++)
            {
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            ForceFullCollection();
            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1,
                ProfilerRecorderOptions.SumAllSamplesInFrame
                    | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            for (int i = 0; i < Iterations; i++)
            {
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            long gcAllocBytes = recorder.LastValue;

            Assert.That(gcAllocBytes, Is.EqualTo(0L),
                "ULipSyncProvider hot path reported GC.Alloc: " + gcAllocBytes + " bytes.");
            Assert.That(output[0], Is.GreaterThan(0f));
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
