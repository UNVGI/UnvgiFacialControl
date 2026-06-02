using System;
using System.Collections;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Adapters
{
    [TestFixture]
    public sealed class LipSyncPhonemeOverlayInputSourceTests
    {
        private const int BlendShapeCount = 3;

        [Test]
        public void Constructor_NullProvider_Throws()
        {
            Assert.That(
                () => new LipSyncPhonemeOverlayInputSource(
                    InputSourceId.Parse("lipsync-overlay:a"),
                    "A",
                    null,
                    BlendShapeCount),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_UnknownPhonemeId_LogsWarningOnce()
        {
            using var provider = CreateProvider(new FakeULipSyncEventSource(), new ManualTimeProvider(), Snapshot("A", 1f, 0f, 0f));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Phoneme 'Unknown' is not registered"));
            var source = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:unknown"),
                "Unknown",
                provider,
                BlendShapeCount);

            var output = new float[BlendShapeCount];
            Assert.That(source.TryWriteValues(output), Is.False);
            Assert.That(source.TryWriteValues(output), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryWriteValues_PhonemeNotRegistered_ReturnsFalse()
        {
            using var provider = CreateProvider(new FakeULipSyncEventSource(), new ManualTimeProvider(), Snapshot("A", 1f, 0f, 0f));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Phoneme 'Unknown' is not registered"));
            var source = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:unknown"),
                "Unknown",
                provider,
                BlendShapeCount);
            var output = new[] { 0.1f, 0.2f, 0.3f };

            bool written = source.TryWriteValues(output);

            Assert.That(written, Is.False);
            Assert.That(output, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
        }

        [Test]
        public void TryWriteValues_ProviderActiveWithVolume_WritesScaledWeights()
        {
            var eventSource = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(eventSource, time, Snapshot("A", 0.5f, 0.25f, 0f));
            var source = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:a"),
                "A",
                provider,
                BlendShapeCount);
            var output = new float[BlendShapeCount];

            eventSource.Invoke(Info(0.8f, ("A", 0.5f)));
            time.UnscaledTimeSeconds += 1.0 / 60.0;
            bool written = source.TryWriteValues(output);

            Assert.That(written, Is.True);
            AssertValuesClose(output, 0.8f * 0.5f, 0.8f * 0.25f, 0f);
        }

        [Test]
        public void TryWriteValues_ProviderSilent_ReturnsFalse()
        {
            var eventSource = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(eventSource, time, Snapshot("A", 1f, 0.5f, 0.25f));
            var source = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:a"),
                "A",
                provider,
                BlendShapeCount);
            var output = new[] { 0.1f, 0.2f, 0.3f };

            eventSource.Invoke(Info(0f, ("A", 1f)));
            time.UnscaledTimeSeconds += 1.0 / 60.0;
            bool written = source.TryWriteValues(output);

            Assert.That(written, Is.False);
            Assert.That(output, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
        }

        [Test]
        public void ContributeMask_AfterConstruction_MatchesProviderMask()
        {
            using var provider = CreateProvider(
                new FakeULipSyncEventSource(),
                new ManualTimeProvider(),
                Snapshot("A", 1f, 0f, 0.25f),
                Snapshot("I", 0f, 0.5f, 0f));
            var source = new LipSyncPhonemeOverlayInputSource(
                InputSourceId.Parse("lipsync-overlay:a"),
                "A",
                provider,
                BlendShapeCount);

            BitArray mask = source.ContributeMask;

            Assert.That(mask.Length, Is.EqualTo(BlendShapeCount));
            Assert.That(mask[0], Is.True);
            Assert.That(mask[1], Is.False);
            Assert.That(mask[2], Is.True);
        }

        private static ULipSyncProvider CreateProvider(
            FakeULipSyncEventSource eventSource,
            ManualTimeProvider time,
            params PhonemeSnapshot[] snapshots)
        {
            return new ULipSyncProvider(
                eventSource,
                snapshots,
                BlendShapeCount,
                smoothness: ULipSyncProvider.DefaultSmoothness,
                timeProvider: time);
        }

        private static PhonemeSnapshot Snapshot(string phonemeId, params float[] weights)
        {
            return new PhonemeSnapshot(phonemeId, weights);
        }

        private static uLipSync.LipSyncInfo Info(
            float volume,
            params (string PhonemeId, float Ratio)[] ratios)
        {
            var phonemeRatios = new System.Collections.Generic.Dictionary<string, float>(ratios.Length);
            for (int i = 0; i < ratios.Length; i++)
            {
                phonemeRatios[ratios[i].PhonemeId] = ratios[i].Ratio;
            }

            // provider は info.volume ではなく info.rawVolume を既定 min/max(-2.5/-1.5) で
            // 正規化する。正規化済み volume へ戻る rawVolume を逆算してセットする。
            float rawVolume = volume <= 0f
                ? 0f
                : Mathf.Pow(
                    10f,
                    volume
                        * (ULipSyncProvider.DefaultMaxVolume - ULipSyncProvider.DefaultMinVolume)
                        + ULipSyncProvider.DefaultMinVolume);

            return new uLipSync.LipSyncInfo
            {
                rawVolume = rawVolume,
                phonemeRatios = phonemeRatios,
            };
        }

        private static void AssertValuesClose(float[] actual, params float[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-4f), "index " + i);
            }
        }
    }
}
