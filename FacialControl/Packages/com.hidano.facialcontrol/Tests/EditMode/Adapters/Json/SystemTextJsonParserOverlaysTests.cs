using NUnit.Framework;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// Overlay 機能 (Expression.Overlays / FacialProfile.DefaultOverlays) の JSON round-trip 契約テスト。
    /// suppress (空文字 expressionId) / fallback / 未指定 slot の各シナリオを観測する。
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserOverlaysTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        private static FacialProfile BuildProfile(
            OverlaySlotBinding[] angerOverlays,
            OverlaySlotBinding[] defaultOverlays)
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            var inputSources = new[]
            {
                new[] { new InputSourceDeclaration("input", 1f, null) },
            };
            var expressions = new[]
            {
                new Expression(
                    "anger", "Anger", "emotion",
                    Expression.DefaultTransitionDuration, default,
                    blendShapeValues: new[] { new BlendShapeMapping("怒り", 1f) },
                    overlays: angerOverlays),
            };
            return new FacialProfile(
                "1.0", layers, expressions, rendererPaths: null,
                layerInputSources: inputSources, defaultOverlays: defaultOverlays);
        }

        [Test]
        public void RoundTrip_ExpressionOverlays_PreservedInsertionOrder()
        {
            var overlays = new[]
            {
                new OverlaySlotBinding("blink", "anger_blink"),
                new OverlaySlotBinding("blush", "anger_blush"),
            };
            var profile = BuildProfile(overlays, defaultOverlays: null);

            var json = _parser.SerializeProfile(profile);
            var parsed = _parser.ParseProfile(json);

            var anger = parsed.FindExpressionById("anger").Value;
            Assert.AreEqual(2, anger.Overlays.Length);
            Assert.AreEqual("blink", anger.Overlays.Span[0].Slot);
            Assert.AreEqual("anger_blink", anger.Overlays.Span[0].ExpressionId);
            Assert.AreEqual("blush", anger.Overlays.Span[1].Slot);
            Assert.AreEqual("anger_blush", anger.Overlays.Span[1].ExpressionId);
        }

        [Test]
        public void RoundTrip_SuppressEntry_PreservesEmptyExpressionId()
        {
            var overlays = new[]
            {
                new OverlaySlotBinding("blink", null),   // 明示 suppress
            };
            var profile = BuildProfile(overlays, defaultOverlays: null);

            var json = _parser.SerializeProfile(profile);
            var parsed = _parser.ParseProfile(json);

            var anger = parsed.FindExpressionById("anger").Value;
            Assert.AreEqual(1, anger.Overlays.Length);
            Assert.AreEqual("blink", anger.Overlays.Span[0].Slot);
            Assert.IsTrue(anger.Overlays.Span[0].IsSuppress, "suppress 指定が round-trip で保持されること");
        }

        [Test]
        public void RoundTrip_DefaultOverlays_PreservedAtProfileRoot()
        {
            var defaults = new[]
            {
                new OverlaySlotBinding("blink", "default_blink"),
            };
            var profile = BuildProfile(angerOverlays: null, defaultOverlays: defaults);

            var json = _parser.SerializeProfile(profile);
            var parsed = _parser.ParseProfile(json);

            Assert.AreEqual(1, parsed.DefaultOverlays.Length);
            Assert.AreEqual("blink", parsed.DefaultOverlays.Span[0].Slot);
            Assert.AreEqual("default_blink", parsed.DefaultOverlays.Span[0].ExpressionId);
        }

        [Test]
        public void RoundTrip_NoOverlaysDeclared_ProducesEmpty()
        {
            var profile = BuildProfile(angerOverlays: null, defaultOverlays: null);

            var json = _parser.SerializeProfile(profile);
            var parsed = _parser.ParseProfile(json);

            Assert.AreEqual(0, parsed.FindExpressionById("anger").Value.Overlays.Length);
            Assert.AreEqual(0, parsed.DefaultOverlays.Length);
        }

        [Test]
        public void Serialize_ThenParseTwice_ProducesIdenticalString()
        {
            var profile = BuildProfile(
                angerOverlays: new[]
                {
                    new OverlaySlotBinding("blink", "anger_blink"),
                    new OverlaySlotBinding("blush", null),
                },
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding("blink", "default_blink"),
                });

            var s1 = _parser.SerializeProfile(profile);
            var p2 = _parser.ParseProfile(s1);
            var s2 = _parser.SerializeProfile(p2);

            Assert.AreEqual(s1, s2, "Overlay 含み round-trip でも完全一致");
        }

        [Test]
        public void Parse_OverlaysFieldMissing_FallsBackToEmpty()
        {
            // overlays / defaultOverlays フィールドが完全に欠落した既存スキーマ JSON を読む
            string legacyJson = JsonSchemaDefinition.SampleProfileJson;
            var parsed = _parser.ParseProfile(legacyJson);

            Assert.AreEqual(0, parsed.DefaultOverlays.Length);
            var exprSpan = parsed.Expressions.Span;
            for (int i = 0; i < exprSpan.Length; i++)
            {
                Assert.AreEqual(0, exprSpan[i].Overlays.Length, $"既存 schema JSON: overlays は空配列にフォールバック (id={exprSpan[i].Id})");
            }
        }
    }
}
