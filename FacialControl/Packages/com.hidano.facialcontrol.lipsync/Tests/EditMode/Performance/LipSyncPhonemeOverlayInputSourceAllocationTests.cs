using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using Unity.Profiling;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Performance
{
    [TestFixture]
    public sealed class LipSyncPhonemeOverlayInputSourceAllocationTests
    {
        private const int FramesToMeasure = 1000;
        private const double FrameDt = 1.0 / 60.0;

        [Test]
        public void TryWriteValues_RepeatedCalls_AllocatesZeroBytes()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            var snapshots = new[]
            {
                new PhonemeSnapshot("A", new[] { 0.50f, 0.00f, 0.25f, 0.10f }),
                new PhonemeSnapshot("I", new[] { 0.20f, 0.40f, 0.00f, 0.30f }),
            };
            using var provider = new ULipSyncProvider(
                source,
                snapshots,
                blendShapeCount: 4,
                smoothness: ULipSyncProvider.DefaultSmoothness,
                timeProvider: time);
            var inputSource = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:a"),
                "A",
                provider,
                blendShapeCount: 4);
            var output = new float[4];
            var info = new uLipSync.LipSyncInfo
            {
                volume = 0.8f,
                phonemeRatios = new Dictionary<string, float>(2)
                {
                    { "A", 0.70f },
                    { "I", 0.30f },
                },
            };

            source.Invoke(info);
            for (int i = 0; i < 128; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                inputSource.TryWriteValues(output);
            }

            ForceFullCollection();
            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1,
                ProfilerRecorderOptions.SumAllSamplesInFrame
                    | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            bool wroteAllFrames = true;
            for (int i = 0; i < FramesToMeasure; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                wroteAllFrames &= inputSource.TryWriteValues(output);
            }

            long gcAllocBytes = recorder.LastValue;

            Assert.That(wroteAllFrames, Is.True);
            Assert.That(gcAllocBytes, Is.EqualTo(0L),
                "LipSyncPhonemeOverlayInputSource.TryWriteValues hot path reported GC.Alloc: " + gcAllocBytes + " bytes.");
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
