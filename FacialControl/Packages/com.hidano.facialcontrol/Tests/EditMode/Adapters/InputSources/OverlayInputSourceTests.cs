using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    [TestFixture]
    public class OverlayInputSourceTests
    {
        private const string LayerName = "emotion";
        private const string BlinkSlot = "blink";
        private const string BrowName = "Brow";
        private const string MouthName = "Mouth";
        private const string EyeName = "Blink";

        private static readonly string[] BlendShapeNames =
        {
            BrowName,
            MouthName,
            EyeName,
        };

        private sealed class StubActiveProvider : IActiveExpressionProvider
        {
            public Expression? Active;

            public Expression? TryGetTopActiveExpression(string layerName)
            {
                return Active;
            }
        }

        [Test]
        public void TryWriteValues_ActiveExpressionSnapshotOverride_WritesSnapshotValues()
        {
            var activeSnapshot = CreateSnapshot(
                "anger-blink-inline",
                new BlendShapeSnapshot("Face", EyeName, 0.8f));
            var defaultSnapshot = CreateSnapshot(
                "default-blink-inline",
                new BlendShapeSnapshot("Face", EyeName, 0.25f));
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: activeSnapshot),
                },
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: defaultSnapshot),
                });
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var source = CreateSource(profile, provider);

            Span<float> output = stackalloc float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0f, output[0], 1e-6f);
            Assert.AreEqual(0f, output[1], 1e-6f);
            Assert.AreEqual(0.8f, output[2], 1e-6f);
            Assert.IsFalse(source.ContributeMask[0]);
            Assert.IsFalse(source.ContributeMask[1]);
            Assert.IsTrue(source.ContributeMask[2]);
        }

        [Test]
        public void TryWriteValues_ActiveExpressionSuppress_ReturnsFalseAndEmptyMask()
        {
            var defaultSnapshot = CreateSnapshot(
                "default-blink-inline",
                new BlendShapeSnapshot("Face", EyeName, 0.75f));
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: true, snapshot: null),
                },
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: defaultSnapshot),
                });
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var source = CreateSource(profile, provider);

            Span<float> output = stackalloc float[BlendShapeNames.Length];
            output.Fill(0.123f);
            bool wrote = source.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(0.123f, output[2], 1e-6f);
            Assert.IsFalse(source.ContributeMask[0]);
            Assert.IsFalse(source.ContributeMask[1]);
            Assert.IsFalse(source.ContributeMask[2]);
        }

        [Test]
        public void TryWriteValues_ActiveExpressionDefaultFallback_UsesProfileDefaultOverlaySnapshot()
        {
            var defaultSnapshot = CreateSnapshot(
                "default-blink-inline",
                new BlendShapeSnapshot("Face", EyeName, 0.6f),
                new BlendShapeSnapshot("Face", MouthName, 0.2f));
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: null),
                },
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: defaultSnapshot),
                });
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var source = CreateSource(profile, provider);

            Span<float> output = stackalloc float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0f, output[0], 1e-6f);
            Assert.AreEqual(0.2f, output[1], 1e-6f);
            Assert.AreEqual(0.6f, output[2], 1e-6f);
            Assert.IsFalse(source.ContributeMask[0]);
            Assert.IsTrue(source.ContributeMask[1]);
            Assert.IsTrue(source.ContributeMask[2]);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryWriteValues_DefaultOverlayDefaultOrMissing_ReturnsFalse(bool hasDefaultFallback)
        {
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: null),
                },
                defaultOverlays: hasDefaultFallback
                    ? new[] { new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: null) }
                    : null);
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var source = CreateSource(profile, provider);

            Span<float> output = stackalloc float[BlendShapeNames.Length];
            output.Fill(0.456f);
            bool wrote = source.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(0.456f, output[0], 1e-6f);
            Assert.AreEqual(0.456f, output[1], 1e-6f);
            Assert.AreEqual(0.456f, output[2], 1e-6f);
            Assert.IsFalse(source.ContributeMask[0]);
            Assert.IsFalse(source.ContributeMask[1]);
            Assert.IsFalse(source.ContributeMask[2]);
        }

        [Test]
        public void Constructor_UndeclaredSlot_LogsWarningOnceAndSourceReturnsFalse()
        {
            var snapshot = CreateSnapshot(
                "anger-blink-inline",
                new BlendShapeSnapshot("Face", EyeName, 1f));
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding(BlinkSlot, suppress: false, snapshot: snapshot),
                },
                defaultOverlays: null,
                slots: Array.Empty<string>());
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };

            LogAssert.Expect(LogType.Warning, new Regex("OverlayInputSource"));
            var source = CreateSource(profile, provider);

            Span<float> output = stackalloc float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.IsFalse(source.ContributeMask[0]);
            Assert.IsFalse(source.ContributeMask[1]);
            Assert.IsFalse(source.ContributeMask[2]);
        }

        private static FacialProfile BuildProfile(
            OverlaySlotBinding[] angerOverlays,
            OverlaySlotBinding[] defaultOverlays,
            string[] slots = null)
        {
            var anger = new Expression(
                id: "anger",
                name: "Anger",
                layer: LayerName,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurve: default,
                blendShapeValues: null,
                overlays: angerOverlays);
            var neutral = new Expression(
                id: "neutral",
                name: "Neutral",
                layer: LayerName,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurve: default,
                blendShapeValues: null,
                overlays: null);

            return new FacialProfile(
                schemaVersion: "1.0",
                layers: new[] { new LayerDefinition(LayerName, 0, ExclusionMode.LastWins) },
                expressions: new[] { anger, neutral },
                rendererPaths: null,
                layerInputSources: null,
                defaultOverlays: defaultOverlays,
                slots: slots ?? new[] { BlinkSlot });
        }

        private static OverlayInputSource CreateSource(
            FacialProfile profile,
            IActiveExpressionProvider provider)
        {
            return new OverlayInputSource(
                id: InputSourceId.Parse("overlay:blink"),
                slot: BlinkSlot,
                blendShapeCount: BlendShapeNames.Length,
                blendShapeNames: BlendShapeNames,
                profile: profile,
                activeProvider: provider,
                emotionLayerName: LayerName);
        }

        private static ExpressionSnapshot CreateSnapshot(
            string id,
            params BlendShapeSnapshot[] blendShapes)
        {
            return new ExpressionSnapshot(
                id: id,
                transitionDuration: Expression.DefaultTransitionDuration,
                transitionCurvePreset: TransitionCurvePreset.Linear,
                blendShapes: blendShapes,
                bones: null,
                rendererPaths: new[] { "Face" });
        }
    }
}
