using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.LipSync.Tests.PlayMode.Integration
{
    [TestFixture]
    public sealed class PhonemeOverlayIntegrationTests
    {
        private const string EmotionLayer = "emotion";
        private const string FacePath = "Face";
        private const string MouthA = "Mouth_A";
        private const string MouthI = "Mouth_I";
        private const float Tolerance = 1e-5f;

        private static readonly string[] BlendShapeNames =
        {
            MouthA,
            MouthI,
        };

        private readonly StubActiveExpressionProvider _activeProvider = new StubActiveExpressionProvider();
        private readonly ManualTimeProvider _timeProvider = new ManualTimeProvider();

        [UnityTest]
        public IEnumerator ActiveExpressionWithOverride_PhonemeSlot_ProducesSnapshotInOneFrame()
        {
            ExpressionSnapshot overrideSnapshot = Snapshot("smile-a", new BlendShapeSnapshot(FacePath, MouthA, 0.82f));
            FacialProfile profile = BuildProfile(
                smileOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.A, suppress: false, snapshot: overrideSnapshot),
                });
            _activeProvider.Active = profile.FindExpressionById("smile");

            using FrameRig rig = CreateRig(profile);
            rig.SetLipSyncFrame(1f, ("A", 1f));
            yield return null;

            Assert.That(rig.LipSyncWouldWrite(PhonemeOverlaySlots.A), Is.True);
            Assert.That(rig.LipSyncOutput[0], Is.EqualTo(0.35f).Within(Tolerance));

            bool wrote = rig.TryResolve(PhonemeOverlaySlots.A);

            Assert.That(wrote, Is.True);
            Assert.That(rig.Output[0], Is.EqualTo(0.82f).Within(Tolerance));
            Assert.That(rig.Output[1], Is.EqualTo(0f).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator ActiveExpressionWithSuppress_PhonemeSlot_BlocksLipSyncOutput()
        {
            FacialProfile profile = BuildProfile(
                smileOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.A, suppress: true, snapshot: null),
                });
            _activeProvider.Active = profile.FindExpressionById("smile");

            using FrameRig rig = CreateRig(profile);
            rig.SetLipSyncFrame(1f, ("A", 1f));
            yield return null;

            Assert.That(rig.LipSyncWouldWrite(PhonemeOverlaySlots.A), Is.True);
            Assert.That(rig.LipSyncOutput[0], Is.EqualTo(0.35f).Within(Tolerance));

            bool wrote = rig.TryResolve(PhonemeOverlaySlots.A);

            Assert.That(wrote, Is.False);
            Assert.That(rig.Output[0], Is.EqualTo(0f).Within(Tolerance));
            Assert.That(rig.Output[1], Is.EqualTo(0f).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator NoActiveOverride_PhonemeSlot_DelegatesToLipSyncDefault()
        {
            FacialProfile profile = BuildProfile(
                smileOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.A, suppress: false, snapshot: null),
                });
            _activeProvider.Active = profile.FindExpressionById("smile");

            using FrameRig rig = CreateRig(profile);
            rig.SetLipSyncFrame(1f, ("A", 1f));
            yield return null;

            bool wrote = rig.TryResolve(PhonemeOverlaySlots.A);

            Assert.That(wrote, Is.True);
            Assert.That(rig.Output[0], Is.EqualTo(0.35f).Within(Tolerance));
            Assert.That(rig.Output[1], Is.EqualTo(0f).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator ExpressionSwitch_BetweenTwoOverrides_ReflectsNewSnapshotInOneFrame()
        {
            ExpressionSnapshot smileSnapshot = Snapshot("smile-a", new BlendShapeSnapshot(FacePath, MouthA, 0.7f));
            ExpressionSnapshot angrySnapshot = Snapshot("angry-a", new BlendShapeSnapshot(FacePath, MouthA, 0.2f));
            FacialProfile profile = BuildProfile(
                smileOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.A, suppress: false, snapshot: smileSnapshot),
                },
                angryOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.A, suppress: false, snapshot: angrySnapshot),
                });

            using FrameRig rig = CreateRig(profile);
            rig.SetLipSyncFrame(1f, ("A", 1f));

            _activeProvider.Active = profile.FindExpressionById("smile");
            yield return null;
            Assert.That(rig.TryResolve(PhonemeOverlaySlots.A), Is.True);
            Assert.That(rig.Output[0], Is.EqualTo(0.7f).Within(Tolerance));

            _activeProvider.Active = profile.FindExpressionById("angry");
            yield return null;
            Assert.That(rig.TryResolve(PhonemeOverlaySlots.A), Is.True);
            Assert.That(rig.Output[0], Is.EqualTo(0.2f).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator BaseExpressionOnly_PhonemeSlot_UsesDefaultOverlaysThenLipSync()
        {
            ExpressionSnapshot defaultSnapshot = Snapshot("default-i", new BlendShapeSnapshot(FacePath, MouthI, 0.64f));
            FacialProfile profileWithDefault = BuildProfile(
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding(PhonemeOverlaySlots.I, suppress: false, snapshot: defaultSnapshot),
                });
            _activeProvider.Active = null;

            using FrameRig defaultRig = CreateRig(profileWithDefault);
            defaultRig.SetLipSyncFrame(1f, ("I", 1f));
            yield return null;

            Assert.That(defaultRig.TryResolve(PhonemeOverlaySlots.I), Is.True);
            Assert.That(defaultRig.Output[0], Is.EqualTo(0f).Within(Tolerance));
            Assert.That(defaultRig.Output[1], Is.EqualTo(0.64f).Within(Tolerance));

            FacialProfile profileWithoutDefault = BuildProfile();
            using FrameRig fallbackRig = CreateRig(profileWithoutDefault);
            fallbackRig.SetLipSyncFrame(1f, ("I", 1f));
            yield return null;

            Assert.That(fallbackRig.TryResolve(PhonemeOverlaySlots.I), Is.True);
            Assert.That(fallbackRig.Output[0], Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fallbackRig.Output[1], Is.EqualTo(0.5f).Within(Tolerance));
        }

        private FrameRig CreateRig(FacialProfile profile)
        {
            return new FrameRig(profile, _activeProvider, _timeProvider);
        }

        private static FacialProfile BuildProfile(
            OverlaySlotBinding[] smileOverlays = null,
            OverlaySlotBinding[] angryOverlays = null,
            OverlaySlotBinding[] defaultOverlays = null)
        {
            var smile = new Expression(
                id: "smile",
                name: "Smile",
                layer: EmotionLayer,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurve: default,
                blendShapeValues: null,
                overlays: smileOverlays);
            var angry = new Expression(
                id: "angry",
                name: "Angry",
                layer: EmotionLayer,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurve: default,
                blendShapeValues: null,
                overlays: angryOverlays);

            return new FacialProfile(
                schemaVersion: "1.0",
                layers: new[] { new LayerDefinition(EmotionLayer, 0, ExclusionMode.LastWins) },
                expressions: new[] { smile, angry },
                defaultOverlays: defaultOverlays,
                slots: PhonemeOverlaySlots.ReservedNames.ToArray());
        }

        private static ExpressionSnapshot Snapshot(string id, params BlendShapeSnapshot[] blendShapes)
        {
            return new ExpressionSnapshot(
                id: id,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurvePreset: TransitionCurvePreset.Linear,
                blendShapes: blendShapes,
                bones: null,
                rendererPaths: new[] { FacePath });
        }

        private sealed class FrameRig : IDisposable
        {
            private readonly FacialProfile _profile;
            private readonly StubActiveExpressionProvider _activeProvider;
            private readonly FakePhonemeWeightSource _weightSource;
            private readonly ULipSyncProvider _provider;
            private readonly Dictionary<string, OverlayInputSource> _overlays =
                new Dictionary<string, OverlayInputSource>(StringComparer.Ordinal);
            private readonly Dictionary<string, LipSyncPhonemeOverlayInputSource> _lipSyncSources =
                new Dictionary<string, LipSyncPhonemeOverlayInputSource>(StringComparer.Ordinal);

            public FrameRig(
                FacialProfile profile,
                StubActiveExpressionProvider activeProvider,
                ManualTimeProvider timeProvider)
            {
                _profile = profile;
                _activeProvider = activeProvider;
                _weightSource = new FakePhonemeWeightSource();
                _provider = new ULipSyncProvider(
                    _weightSource,
                    new[]
                    {
                        new PhonemeSnapshot("A", new[] { 0.35f, 0f }),
                        new PhonemeSnapshot("I", new[] { 0f, 0.5f }),
                    },
                    BlendShapeNames.Length);
                Output = new float[BlendShapeNames.Length];
                LipSyncOutput = new float[BlendShapeNames.Length];
            }

            public float[] Output { get; }

            public float[] LipSyncOutput { get; }

            public void SetLipSyncFrame(float volume, params (string PhonemeId, float Weight)[] weights)
            {
                // uLipSync 委譲後の確定 weight/volume を直接与える（テストは単一音素 weight=1 を使用）。
                _weightSource.CurrentVolume = volume;
                for (int i = 0; i < weights.Length; i++)
                {
                    _weightSource.SetPhonemeWeight(weights[i].PhonemeId, weights[i].Weight);
                }
            }

            public bool TryResolve(string slot)
            {
                Array.Clear(Output, 0, Output.Length);

                OverlayInputSource overlay = GetOverlay(slot);
                if (overlay.TryWriteValues(Output))
                {
                    return true;
                }

                if (IsSuppressed(slot))
                {
                    return false;
                }

                return GetLipSync(slot).TryWriteValues(Output);
            }

            public bool LipSyncWouldWrite(string slot)
            {
                Array.Clear(LipSyncOutput, 0, LipSyncOutput.Length);
                return GetLipSync(slot).TryWriteValues(LipSyncOutput);
            }

            public void Dispose()
            {
                _provider.Dispose();
            }

            private OverlayInputSource GetOverlay(string slot)
            {
                if (_overlays.TryGetValue(slot, out OverlayInputSource source))
                {
                    return source;
                }

                source = new OverlayInputSource(
                    InputSourceId.Parse("overlay:" + slot),
                    slot,
                    BlendShapeNames.Length,
                    BlendShapeNames,
                    _profile,
                    _activeProvider,
                    EmotionLayer);
                _overlays.Add(slot, source);
                return source;
            }

            private LipSyncPhonemeOverlayInputSource GetLipSync(string slot)
            {
                if (_lipSyncSources.TryGetValue(slot, out LipSyncPhonemeOverlayInputSource source))
                {
                    return source;
                }

                source = new LipSyncPhonemeOverlayInputSource(
                    InputSourceId.Parse("lipsync-overlay:" + slot),
                    PhonemeOverlaySlots.MapReservedToPhonemeId(slot),
                    _provider,
                    BlendShapeNames.Length);
                _lipSyncSources.Add(slot, source);
                return source;
            }

            private bool IsSuppressed(string slot)
            {
                if (_activeProvider.Active.HasValue
                    && _activeProvider.Active.Value.TryGetOverlay(slot, out OverlaySlotBinding activeBinding))
                {
                    if (activeBinding.Suppress)
                    {
                        return true;
                    }

                    if (!activeBinding.IsDefaultFallback)
                    {
                        return false;
                    }
                }

                return _profile.TryGetDefaultOverlay(slot, out OverlaySlotBinding defaultBinding)
                    && defaultBinding.Suppress;
            }
        }

        private sealed class StubActiveExpressionProvider : IActiveExpressionProvider
        {
            public Expression? Active;

            public Expression? TryGetTopActiveExpression(string layerName)
            {
                return Active;
            }
        }

        private sealed class ManualTimeProvider : ITimeProvider
        {
            public double UnscaledTimeSeconds { get; set; }
        }
    }
}
