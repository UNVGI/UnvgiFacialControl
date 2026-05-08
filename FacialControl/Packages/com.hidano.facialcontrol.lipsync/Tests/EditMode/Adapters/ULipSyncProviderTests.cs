using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Adapters
{
    [TestFixture]
    public class ULipSyncProviderTests
    {
        [Test]
        public void Constructor_NullSource_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new ULipSyncProvider(null, Array.Empty<PhonemeSnapshot>(), 1),
                Throws.ArgumentNullException);
        }

        [Test]
        public void OnLipSyncUpdate_PhonemeRatiosWithKnownKeys_AccumulatesWeightedSum()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                3,
                Snapshot("A", 0.5f, 0f, 0.25f),
                Snapshot("I", 0.2f, 0.4f, 0f));
            var output = new float[3];

            source.Invoke(Info(0.5f, ("A", 0.8f), ("I", 0.25f)));
            provider.GetLipSyncValues(output);

            AssertValues(output, 0.225f, 0.05f, 0.1f);
        }

        [Test]
        public void OnLipSyncUpdate_UnknownPhonemeKey_IsIgnoredWithoutLog()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, 2, Snapshot("A", 0.4f, 0.6f));
            var output = new float[2];

            source.Invoke(Info(1f, ("Unknown", 1f), ("A", 0.5f)));
            provider.GetLipSyncValues(output);

            AssertValues(output, 0.2f, 0.3f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnLipSyncUpdate_ConfiguredEntryAbsentInFrame_ContributesZero()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                3,
                Snapshot("A", 0.2f, 0.3f, 0f),
                Snapshot("E", 0f, 0.5f, 1f));
            var output = new float[3];

            source.Invoke(Info(0.75f, ("A", 0.4f)));
            provider.GetLipSyncValues(output);

            AssertValues(output, 0.06f, 0.09f, 0f);
        }

        [Test]
        public void GetLipSyncValues_SilentFrame_ProducesSubThresholdSum()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, 3, Snapshot("A", 1f, 0.5f, 0.25f));
            var output = new float[3];

            source.Invoke(Info(0f, ("A", 1f)));
            provider.GetLipSyncValues(output);

            Assert.That(Sum(output), Is.LessThan(LipSyncInputSource.SilenceThreshold));
        }

        [Test]
        public void BlendShapeNames_AfterConstruction_ReturnsFixedOrder()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, 3, Snapshot("A", 1f, 0f, 0f));

            var first = provider.BlendShapeNames.ToArray();
            var second = provider.BlendShapeNames.ToArray();

            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Dispose_AfterCall_NoLongerReceivesEvents()
        {
            var source = new FakeULipSyncEventSource();
            var provider = CreateProvider(
                source,
                2,
                Snapshot("A", 0.25f, 0.5f),
                Snapshot("O", 1f, 0f));
            var output = new float[2];

            source.Invoke(Info(1f, ("A", 1f)));
            provider.GetLipSyncValues(output);
            AssertValues(output, 0.25f, 0.5f);

            provider.Dispose();
            source.Invoke(Info(1f, ("O", 1f)));
            provider.GetLipSyncValues(output);

            AssertValues(output, 0.25f, 0.5f);
        }

        [Test]
        public void GetLipSyncValues_AfterRequestZeroOutput_ProducesZeroSpan()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, 3, Snapshot("A", 0.4f, 0.2f, 0.1f));
            var output = new float[3];

            source.Invoke(Info(1f, ("A", 1f)));
            provider.RequestZeroOutputForNextFrame();
            provider.GetLipSyncValues(output);

            AssertValues(output, 0f, 0f, 0f);

            provider.GetLipSyncValues(output);

            AssertValues(output, 0.4f, 0.2f, 0.1f);
        }

        private static ULipSyncProvider CreateProvider(
            FakeULipSyncEventSource source,
            int blendShapeCount,
            params PhonemeSnapshot[] snapshots)
        {
            return new ULipSyncProvider(source, snapshots, blendShapeCount);
        }

        private static PhonemeSnapshot Snapshot(string phonemeId, params float[] weights)
        {
            return new PhonemeSnapshot(phonemeId, weights);
        }

        private static uLipSync.LipSyncInfo Info(
            float volume,
            params (string PhonemeId, float Ratio)[] ratios)
        {
            var phonemeRatios = new Dictionary<string, float>(ratios.Length);
            for (int i = 0; i < ratios.Length; i++)
            {
                phonemeRatios[ratios[i].PhonemeId] = ratios[i].Ratio;
            }

            return new uLipSync.LipSyncInfo
            {
                volume = volume,
                phonemeRatios = phonemeRatios,
            };
        }

        private static float Sum(float[] values)
        {
            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }

            return sum;
        }

        private static void AssertValues(float[] actual, params float[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-6f), "index " + i);
            }
        }
    }
}
