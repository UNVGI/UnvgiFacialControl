using System;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// <see cref="OverlayInputSource"/> の解決ロジック契約テスト。
    /// active 表情側の overlays / profile の defaultOverlays / suppress / 未解決の各シナリオを観測する。
    /// </summary>
    [TestFixture]
    public class OverlayInputSourceTests
    {
        private const string BrowName = "bs_brow";
        private const string MouthName = "bs_mouth";
        private const string EyeName = "bs_eye";

        /// <summary>固定 Expression を返すフェイク provider。</summary>
        private sealed class StubActiveProvider : IActiveExpressionProvider
        {
            public Expression? Active;
            public Expression? TryGetTopActiveExpression(string layerName) => Active;
        }

        private static (FacialProfile profile, string[] bsNames) BuildProfile(
            OverlaySlotBinding[] angerOverlays,
            OverlaySlotBinding[] defaultOverlays)
        {
            var bsNames = new[] { BrowName, MouthName, EyeName };
            var layers = new[] { new LayerDefinition("emotion", 0, ExclusionMode.LastWins) };

            var anger = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[]
                {
                    new BlendShapeMapping(BrowName, 1.0f),
                    new BlendShapeMapping(MouthName, 0.5f),
                },
                overlays: angerOverlays);
            var smileClosed = new Expression(
                "smile_closed_eye", "SmileClosed", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[] { new BlendShapeMapping(EyeName, 1.0f) },
                overlays: new[] { new OverlaySlotBinding("blink", null) });   // suppress
            var angerBlink = new Expression(
                "anger_blink", "AngerBlink", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[] { new BlendShapeMapping(EyeName, 1.0f) });
            var defaultBlink = new Expression(
                "default_blink", "DefaultBlink", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[] { new BlendShapeMapping(EyeName, 1.0f) });

            var profile = new FacialProfile(
                "1.0", layers,
                new[] { anger, smileClosed, angerBlink, defaultBlink },
                rendererPaths: null,
                layerInputSources: null,
                defaultOverlays: defaultOverlays);

            return (profile, bsNames);
        }

        private static OverlayInputSource CreateSource(
            FacialProfile profile, string[] bsNames, IActiveExpressionProvider provider)
        {
            return new OverlayInputSource(
                id: InputSourceId.Parse("overlay:blink"),
                slot: "blink",
                blendShapeCount: bsNames.Length,
                blendShapeNames: bsNames,
                profile: profile,
                activeProvider: provider,
                emotionLayerName: "emotion");
        }

        [Test]
        public void TryWriteValues_ActiveExpressionDeclaresOverlay_WritesResolvedExpression()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: new[] { new OverlaySlotBinding("blink", "anger_blink") },
                defaultOverlays: null);
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0f, output[0], 1e-6f, "Brow は anger_blink で touch されないので 0");
            Assert.AreEqual(0f, output[1], 1e-6f, "Mouth は anger_blink で touch されないので 0");
            Assert.AreEqual(1f, output[2], 1e-6f, "Eye は anger_blink で 1.0 で書き込まれる");
            Assert.IsTrue(src.ContributeMask[2], "Eye は active mask で立つ");
            Assert.IsFalse(src.ContributeMask[0], "Brow は touch しないので mask off");
            Assert.IsFalse(src.ContributeMask[1], "Mouth は touch しないので mask off");
        }

        [Test]
        public void TryWriteValues_ActiveExpressionSuppress_ReturnsFalseAndEmptyMask()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: null,
                defaultOverlays: new[] { new OverlaySlotBinding("blink", "default_blink") });
            var provider = new StubActiveProvider
            {
                Active = profile.FindExpressionById("smile_closed_eye"),
            };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            output.Fill(0.123f);
            bool wrote = src.TryWriteValues(output);

            Assert.IsFalse(wrote, "明示 suppress 表情 active のとき false を返すこと");
            Assert.AreEqual(0.123f, output[2], 1e-6f, "false 時は output を変更しない");
            Assert.IsFalse(src.ContributeMask[2], "suppress 時は mask off");
        }

        [Test]
        public void TryWriteValues_ActiveExpressionUnknownSlot_FallsBackToDefault()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: null, // anger は blink を宣言しない
                defaultOverlays: new[] { new OverlaySlotBinding("blink", "default_blink") });
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsTrue(wrote, "active 表情が slot を宣言していなくても profile.defaultOverlays に fallback");
            Assert.AreEqual(1f, output[2], 1e-6f, "default_blink 経由で Eye=1");
        }

        [Test]
        public void TryWriteValues_NoActiveExpression_FallsBackToDefault()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: null,
                defaultOverlays: new[] { new OverlaySlotBinding("blink", "default_blink") });
            var provider = new StubActiveProvider { Active = null };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsTrue(wrote, "active 表情なしでも defaultOverlays が動くこと");
            Assert.AreEqual(1f, output[2], 1e-6f);
        }

        [Test]
        public void TryWriteValues_NoMatchAnywhere_ReturnsFalse()
        {
            var (profile, bsNames) = BuildProfile(angerOverlays: null, defaultOverlays: null);
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsFalse(wrote, "active 表情にも default にも宣言が無ければ false");
        }

        [Test]
        public void TryWriteValues_ActiveExpressionOverridesDefault()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: new[] { new OverlaySlotBinding("blink", "anger_blink") },
                defaultOverlays: new[] { new OverlaySlotBinding("blink", "default_blink") });
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var src = CreateSource(profile, bsNames, provider);

            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsTrue(wrote);
            // anger_blink を選んだことを確認 (Eye=1)。default_blink でも Eye=1 だが、解決ロジック上 anger 側を優先したことが重要。
            Assert.AreEqual(1f, output[2], 1e-6f);
        }

        [Test]
        public void TryWriteValues_UnknownExpressionId_ReturnsFalseWithWarning()
        {
            var (profile, bsNames) = BuildProfile(
                angerOverlays: new[] { new OverlaySlotBinding("blink", "no_such_expression") },
                defaultOverlays: null);
            var provider = new StubActiveProvider { Active = profile.FindExpressionById("anger") };
            var src = CreateSource(profile, bsNames, provider);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("OverlayInputSource"));
            Span<float> output = stackalloc float[bsNames.Length];
            bool wrote = src.TryWriteValues(output);

            Assert.IsFalse(wrote);
        }
    }
}
