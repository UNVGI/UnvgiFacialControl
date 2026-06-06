using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.InputSources;
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
    public class ULipSyncProviderTests
    {
        // 60 frame * 1/60 = 1 秒 ≒ 20 * smoothness の収束時間。
        // 本家 uLipSyncBlendShape と同じ smoothness=0.05 では誤差は exp(-20) で十分小さい。
        private const int DefaultConvergenceFrames = 60;
        private const double DefaultFrameDt = 1.0 / 60.0;

        // SmoothDamp 後の収束値の許容誤差。1e-4 で母音 sum 正規化挙動を十分検証できる。
        private const float ConvergenceTolerance = 1e-4f;
        private const float SilenceThreshold = 1e-4f;

        [Test]
        public void Constructor_NullSource_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new ULipSyncProvider(null, Array.Empty<PhonemeSnapshot>(), 1),
                Throws.ArgumentNullException);
        }

        [Test]
        public void OnLipSyncUpdate_PhonemeRatiosWithKnownKeys_AfterConverged_AccumulatesSumNormalizedWeightedValues()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(
                source, time, 3,
                Snapshot("A", 0.5f, 0f, 0.25f),
                Snapshot("I", 0.2f, 0.4f, 0f));
            var output = new float[3];

            source.Invoke(Info(0.5f, ("A", 0.8f), ("I", 0.25f)));
            AdvanceUntilConverged(time, provider, output);

            // 本家 uLipSyncBlendShape.cs:145-148 と等価な母音 sum 正規化:
            // sum = 0.8 + 0.25 = 1.05, normalized A = 0.8/1.05, I = 0.25/1.05
            // 各 BlendShape: smoothedVolume(=0.5) * Σ(normalizedWeight_i * snapshotWeight_i[k])
            float a = 0.8f / 1.05f;
            float i = 0.25f / 1.05f;
            AssertValuesClose(output,
                0.5f * (a * 0.5f + i * 0.2f),
                0.5f * (a * 0f + i * 0.4f),
                0.5f * (a * 0.25f + i * 0f));
        }

        [Test]
        public void OnLipSyncUpdate_UnknownPhonemeKey_IsIgnoredWithoutLog()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 2, Snapshot("A", 0.4f, 0.6f));
            var output = new float[2];

            source.Invoke(Info(1f, ("Unknown", 1f), ("A", 0.5f)));
            AdvanceUntilConverged(time, provider, output);

            // 未設定 phoneme は無視され、A のみが target になる。
            // 単独 phoneme なので sum 正規化後の weight は 1。
            // smoothedVolume=1.0, A=1.0 → output = snapshotWeight_A。
            AssertValuesClose(output, 0.4f, 0.6f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnLipSyncUpdate_ConfiguredEntryAbsentInFrame_ContributesZero()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(
                source, time, 3,
                Snapshot("A", 0.2f, 0.3f, 0f),
                Snapshot("E", 0f, 0.5f, 1f));
            var output = new float[3];

            source.Invoke(Info(0.75f, ("A", 0.4f)));
            AdvanceUntilConverged(time, provider, output);

            // E は frame に存在しないので targetRatio=0、SmoothDamp 後も 0 に収束する。
            // A 単独で sum=0.4 → normalized=1.0、smoothedVolume=0.75。
            // output = 0.75 * snapshotWeight_A。
            AssertValuesClose(output, 0.75f * 0.2f, 0.75f * 0.3f, 0f);
        }

        [Test]
        public void GetLipSyncValues_SilentFrame_AfterConverged_ProducesSubThresholdSum()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 3, Snapshot("A", 1f, 0.5f, 0.25f));
            var output = new float[3];

            source.Invoke(Info(0f, ("A", 1f)));
            AdvanceUntilConverged(time, provider, output);

            // volume=0 で SmoothDamp 収束後 _smoothedVolume ≒ 0 → output 全要素 ≒ 0
            // → sum < SilenceThreshold (1e-4)
            Assert.That(Sum(output), Is.LessThan(SilenceThreshold));
        }

        [Test]
        public void BlendShapeNames_AfterConstruction_ReturnsFixedOrder()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, new ManualTimeProvider(), 3, Snapshot("A", 1f, 0f, 0f));

            var first = provider.BlendShapeNames.ToArray();
            var second = provider.BlendShapeNames.ToArray();

            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ContributeMask_AllNonZeroPhonemeWeightIndexes_AreIncludedInUnionMask()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                new ManualTimeProvider(),
                5,
                Snapshot("A", 1f, 0f, 0.25f, 0f, 0f),
                Snapshot("I", 0f, 0.5f, 0f, 0f, 0f),
                Snapshot("O", 0f, 0f, 0f, 0.75f, 0f));

            BitArray mask = GetContributeMask(provider);

            Assert.That(mask[0], Is.True);
            Assert.That(mask[1], Is.True);
            Assert.That(mask[2], Is.True);
            Assert.That(mask[3], Is.True);
            Assert.That(mask[4], Is.False);
        }

        [Test]
        public void ContributeMask_AfterConstruction_LengthMatchesBlendShapeCount()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                new ManualTimeProvider(),
                4,
                Snapshot("A", 1f, 0f),
                Snapshot("I", 0f, 0f, 0.5f, 0f));

            BitArray mask = GetContributeMask(provider);

            Assert.That(mask.Length, Is.EqualTo(4));
        }

        [Test]
        public void ContributeMask_DuringRuntime_ReturnsSameReference()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(
                source, time, 3,
                Snapshot("A", 1f, 0f, 0f),
                Snapshot("I", 0f, 0.5f, 0.25f));
            var output = new float[3];

            BitArray first = GetContributeMask(provider);

            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceFrames(time, provider, output, 1);
            BitArray afterFrame = GetContributeMask(provider);

            provider.RequestZeroOutputForNextFrame();
            AdvanceFrames(time, provider, output, 1);
            BitArray afterZeroRequest = GetContributeMask(provider);

            Assert.That(afterFrame, Is.SameAs(first));
            Assert.That(afterZeroRequest, Is.SameAs(first));
        }

        [Test]
        public void TryGetPhonemeIndex_KnownPhonemeId_ReturnsTrueWithIndex()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                new ManualTimeProvider(),
                2,
                Snapshot("A", 1f, 0f),
                Snapshot("I", 0f, 1f));

            bool found = provider.TryGetPhonemeIndex("I", out int index);

            Assert.That(found, Is.True);
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void TryGetPhonemeIndex_UnknownPhonemeId_ReturnsFalse()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, new ManualTimeProvider(), 1, Snapshot("A", 1f));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Phoneme 'Unknown' is not registered"));
            bool found = provider.TryGetPhonemeIndex("Unknown", out int index);

            Assert.That(found, Is.False);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void TryComposePhonemeWeights_KnownPhonemeId_WritesExpectedWeights()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(
                source,
                time,
                3,
                Snapshot("A", 0.5f, 0.25f, 0f),
                Snapshot("I", 0f, 0.4f, 0.8f));
            var output = new float[3];

            source.Invoke(Info(0.5f, ("A", 0.75f), ("I", 0.25f)));
            bool composed = provider.TryComposePhonemeWeights("A", output);

            Assert.That(composed, Is.True);
            AssertValuesClose(output, 0.5f * 0.75f * 0.5f, 0.5f * 0.75f * 0.25f, 0f);
        }

        [Test]
        public void TryComposePhonemeWeights_UnknownPhonemeId_ReturnsFalse()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(source, new ManualTimeProvider(), 2, Snapshot("A", 1f, 0f));
            var output = new[] { 0.25f, 0.5f };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Phoneme 'Unknown' is not registered"));
            bool composed = provider.TryComposePhonemeWeights("Unknown", output);

            Assert.That(composed, Is.False);
            AssertValuesClose(output, 0.25f, 0.5f);
        }

        [Test]
        public void GetPhonemeContributeMask_KnownPhonemeId_ReturnsNonNullBitArray()
        {
            var source = new FakeULipSyncEventSource();
            using var provider = CreateProvider(
                source,
                new ManualTimeProvider(),
                4,
                Snapshot("A", 1f, 0f, 0.5f, 0f),
                Snapshot("I", 0f, 0.25f, 0f, 0f));

            BitArray mask = provider.GetPhonemeContributeMask("A");

            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.Length, Is.EqualTo(4));
            Assert.That(mask[0], Is.True);
            Assert.That(mask[1], Is.False);
            Assert.That(mask[2], Is.True);
            Assert.That(mask[3], Is.False);
        }

        [Test]
        public void Dispose_AfterCall_NoLongerReceivesEvents()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            var provider = CreateProvider(
                source, time, 2,
                Snapshot("A", 0.25f, 0.5f),
                Snapshot("O", 1f, 0f));
            var output = new float[2];

            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceUntilConverged(time, provider, output);
            // A 単独 → sum 正規化後 1.0, smoothedVolume=1.0, snapshotWeight_A=(0.25,0.5)
            AssertValuesClose(output, 0.25f, 0.5f);

            provider.Dispose();
            // Dispose 後は target が更新されないため、以後 GetLipSyncValues は以前の状態を保持しつつ
            // SmoothDamp を回しても _targetVolume / _phonemeTargetRatios は変化しない。
            source.Invoke(Info(1f, ("O", 1f)));
            AdvanceUntilConverged(time, provider, output);

            // O への遷移は起こらず、A のままで安定。
            AssertValuesClose(output, 0.25f, 0.5f);
        }

        [Test]
        public void GetLipSyncValues_AfterRequestZeroOutput_ProducesZeroSpanThenResumes()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 3, Snapshot("A", 0.4f, 0.2f, 0.1f));
            var output = new float[3];

            source.Invoke(Info(1f, ("A", 1f)));
            provider.RequestZeroOutputForNextFrame();
            AdvanceFrames(time, provider, output, 1);

            AssertValuesClose(output, 0f, 0f, 0f);

            // RequestZeroOutputForNextFrame は state を初期化するので、その後 target が再設定され
            // SmoothDamp 収束まで時間を進めると本家挙動どおりに output が立ち上がる。
            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceUntilConverged(time, provider, output);

            AssertValuesClose(output, 0.4f, 0.2f, 0.1f);
        }

        // ---- 新規テスト: 本家準拠 SmoothDamp 挙動の検証 ---------------------------------

        [Test]
        public void OnLipSyncUpdate_VolumeDipDuringSustain_OutputStaysAboveSilenceThreshold()
        {
            // 本対応の本丸: ロングトーン中に volume が瞬間的に 0 へディップしても、
            // SmoothDamp で smoothedVolume が緩やかに減衰するため sum < SilenceThreshold には
            // 即座には落ちず、overlay source が valid output を維持する。
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 3, Snapshot("A", 1f, 0.5f, 0.25f));
            var output = new float[3];

            // 発声中: volume=1.0 で収束させる。
            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceUntilConverged(time, provider, output);
            Assert.That(Sum(output), Is.GreaterThan(SilenceThreshold));

            // 1 フレーム (16ms) だけ volume=0 のディップが来る。
            source.Invoke(Info(0f, ("A", 1f)));
            AdvanceFrames(time, provider, output, 1);

            // SmoothDamp により smoothedVolume は 1 フレームでゼロまで落ちないので
            // output sum は SilenceThreshold を割らない (= LipSync 層が降りない)。
            Assert.That(Sum(output), Is.GreaterThan(SilenceThreshold));
        }

        [Test]
        public void UpdateVowels_MultiplePhonemesActive_AfterConverged_SumNormalizedToOne()
        {
            // 本家 uLipSyncBlendShape.cs:145-148 の sum=1 正規化を検証する。
            // snapshot を全 BlendShape index に 1.0 を割り当てておけば、収束後の
            // output[k] は smoothedVolume * Σ(normalizedWeight_i) になる。
            // Σ(normalizedWeight_i) は sum 正規化後に必ず 1 なので、output[k] = smoothedVolume。
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(
                source, time, 1,
                Snapshot("A", 1f),
                Snapshot("I", 1f),
                Snapshot("U", 1f));
            var output = new float[1];

            // volume=1.0, A=0.6, I=0.6, U=0.6 → 正規化前 sum=1.8、正規化後各 0.333…
            // Σ(normalized) = 1.0 → output[0] = smoothedVolume(=1.0) * 1.0
            source.Invoke(Info(1f, ("A", 0.6f), ("I", 0.6f), ("U", 0.6f)));
            AdvanceUntilConverged(time, provider, output);

            Assert.That(output[0], Is.EqualTo(1f).Within(ConvergenceTolerance));
        }

        [Test]
        public void UpdateVolume_RawVolumeStepFromZeroToOne_AttacksGradually()
        {
            // SmoothDamp が効いていることの直接的な証跡: ゼロ状態から target=1.0 に切替えても
            // 1 フレームでは target に到達せず、初期値 0 と target 1 の中間値を取る。
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 1, Snapshot("A", 1f));
            var output = new float[1];

            // 初期状態 (target=0) を収束させてから target=1 に切替える。
            source.Invoke(Info(0f, ("A", 1f)));
            AdvanceUntilConverged(time, provider, output);
            Assert.That(output[0], Is.LessThan(ConvergenceTolerance));

            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceFrames(time, provider, output, 1);

            // 1 フレーム後は 0 より大きく、target=1 にはまだ到達していない。
            Assert.That(output[0], Is.GreaterThan(0f));
            Assert.That(output[0], Is.LessThan(1f - ConvergenceTolerance));
        }

        [Test]
        public void RequestZeroOutputForNextFrame_ResetsSmoothingState()
        {
            // RequestZeroOutputForNextFrame は velocity 含め全状態を 0 にリセットする。
            // リセット直後の SmoothDamp 立ち上がりが「初回相当」になることを、
            // 同じ target を投げた場合の 1 フレーム後の値が等しいことで検証する。
            var source = new FakeULipSyncEventSource();
            var time1 = new ManualTimeProvider();
            using var providerA = CreateProvider(source, time1, 1, Snapshot("A", 1f));
            var output1 = new float[1];

            // ProviderA: 初回 target=1.0 を投入し 1 フレーム進めた値を記録。
            source.Invoke(Info(1f, ("A", 1f)));
            AdvanceFrames(time1, providerA, output1, 1);
            float firstRunFrame1Value = output1[0];

            // ProviderB: 一度収束させた後 RequestZeroOutputForNextFrame でリセットし、
            // 同条件で 1 フレーム進めた値を取る。
            var source2 = new FakeULipSyncEventSource();
            var time2 = new ManualTimeProvider();
            using var providerB = CreateProvider(source2, time2, 1, Snapshot("A", 1f));
            var output2 = new float[1];
            source2.Invoke(Info(1f, ("A", 1f)));
            AdvanceUntilConverged(time2, providerB, output2);

            providerB.RequestZeroOutputForNextFrame();
            AdvanceFrames(time2, providerB, output2, 1); // この呼び出しは output ゼロ + 状態リセット
            Assert.That(output2[0], Is.EqualTo(0f).Within(ConvergenceTolerance));

            source2.Invoke(Info(1f, ("A", 1f)));
            AdvanceFrames(time2, providerB, output2, 1);

            Assert.That(output2[0], Is.EqualTo(firstRunFrame1Value).Within(ConvergenceTolerance));
        }

        [Test]
        public void EnsureCurrentFrameComposed_CalledFiveTimesPerFrame_DoesNotAdvanceDtTwice()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 1, Snapshot("A", 1f));

            source.Invoke(Info(1f, ("A", 1f)));
            InvokeEnsureCurrentFrameComposed(provider);

            time.UnscaledTimeSeconds += DefaultFrameDt;
            InvokeEnsureCurrentFrameComposed(provider);
            double lastTime = GetPrivateDouble(provider, "_lastTimeSeconds");
            double frameStamp = GetPrivateDouble(provider, "_currentFrameStamp");
            float volume = GetPrivateFloat(provider, "_smoothedVolume");

            for (int i = 0; i < 4; i++)
            {
                InvokeEnsureCurrentFrameComposed(provider);
            }

            Assert.That(GetPrivateDouble(provider, "_lastTimeSeconds"), Is.EqualTo(lastTime));
            Assert.That(GetPrivateDouble(provider, "_currentFrameStamp"), Is.EqualTo(frameStamp));
            Assert.That(GetPrivateFloat(provider, "_smoothedVolume"), Is.EqualTo(volume));
        }

        [Test]
        public void EnsureCurrentFrameComposed_AfterFrameTick_RecomposesOnce()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            using var provider = CreateProvider(source, time, 1, Snapshot("A", 1f));

            source.Invoke(Info(0f, ("A", 1f)));
            InvokeEnsureCurrentFrameComposed(provider);
            Assert.That(GetPrivateFloat(provider, "_smoothedVolume"), Is.EqualTo(0f));

            source.Invoke(Info(1f, ("A", 1f)));
            time.UnscaledTimeSeconds += DefaultFrameDt;
            InvokeEnsureCurrentFrameComposed(provider);
            float firstFrameVolume = GetPrivateFloat(provider, "_smoothedVolume");

            InvokeEnsureCurrentFrameComposed(provider);
            Assert.That(GetPrivateFloat(provider, "_smoothedVolume"), Is.EqualTo(firstFrameVolume));

            time.UnscaledTimeSeconds += DefaultFrameDt;
            InvokeEnsureCurrentFrameComposed(provider);

            Assert.That(firstFrameVolume, Is.GreaterThan(0f));
            Assert.That(GetPrivateFloat(provider, "_smoothedVolume"), Is.GreaterThan(firstFrameVolume));
        }

        // ---- ヘルパ ---------------------------------

        private static ULipSyncProvider CreateProvider(
            FakeULipSyncEventSource source,
            ManualTimeProvider timeProvider,
            int blendShapeCount,
            params PhonemeSnapshot[] snapshots)
        {
            return new ULipSyncProvider(
                source,
                snapshots,
                blendShapeCount,
                smoothness: ULipSyncProvider.DefaultSmoothness,
                timeProvider: timeProvider);
        }

        private static void AdvanceUntilConverged(
            ManualTimeProvider time, ULipSyncProvider provider, float[] buffer,
            int frames = DefaultConvergenceFrames, double frameDt = DefaultFrameDt)
        {
            AdvanceFrames(time, provider, buffer, frames, frameDt);
        }

        private static void AdvanceFrames(
            ManualTimeProvider time, ULipSyncProvider provider, float[] buffer,
            int frames, double frameDt = DefaultFrameDt)
        {
            for (int i = 0; i < frames; i++)
            {
                time.UnscaledTimeSeconds += frameDt;
                provider.GetLipSyncValues(buffer);
            }
        }

        private static PhonemeSnapshot Snapshot(string phonemeId, params float[] weights)
        {
            return new PhonemeSnapshot(phonemeId, weights);
        }

        private static void InvokeEnsureCurrentFrameComposed(ULipSyncProvider provider)
        {
            MethodInfo method = typeof(ULipSyncProvider).GetMethod(
                "EnsureCurrentFrameComposed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "ULipSyncProvider は EnsureCurrentFrameComposed private method を持つ必要があります。");

            method.Invoke(provider, null);
        }

        private static double GetPrivateDouble(ULipSyncProvider provider, string fieldName)
        {
            FieldInfo field = typeof(ULipSyncProvider).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName + " field is required.");

            return (double)field.GetValue(provider);
        }

        private static float GetPrivateFloat(ULipSyncProvider provider, string fieldName)
        {
            FieldInfo field = typeof(ULipSyncProvider).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName + " field is required.");

            return (float)field.GetValue(provider);
        }

        private static BitArray GetContributeMask(ULipSyncProvider provider)
        {
            PropertyInfo property = typeof(ULipSyncProvider).GetProperty(
                "ContributeMask",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                "ULipSyncProvider は ContributeMask プロパティを公開する必要があります。");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(BitArray)));

            object value = property.GetValue(provider);
            Assert.That(value, Is.Not.Null);
            return (BitArray)value;
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

            // provider は uLipSync 本体が正規化済みの info.volume をそのまま target に反映する。
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

        private static void AssertValuesClose(float[] actual, params float[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(ConvergenceTolerance), "index " + i);
            }
        }
    }
}
