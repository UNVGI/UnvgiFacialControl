using NUnit.Framework;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// tasks.md 7.4: <see cref="SystemTextJsonParser"/> の順序保持と round-trip 安定性の契約テスト
    /// (Req 3.5, 8.4, D-5)。
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///     <item><c>SerializeProfile → ParseProfile → SerializeProfile</c> が同一文字列を返す</item>
    ///     <item><c>inputSources</c> の宣言順が round-trip で保持される</item>
    ///     <item>既定値（<c>weight=1.0</c>）の省略規則が一貫する</item>
    /// </list>
    /// </para>
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserRoundTripTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        // ================================================================
        // SerializeProfile → ParseProfile → SerializeProfile 文字列等価
        // ================================================================

        [Test]
        public void SerializeParseSerialize_SampleJson_ProducesIdenticalString()
        {
            var profile = _parser.ParseProfile(JsonSchemaDefinition.SampleProfileJson);

            var s1 = _parser.SerializeProfile(profile);
            var p2 = _parser.ParseProfile(s1);
            var s2 = _parser.SerializeProfile(p2);

            Assert.AreEqual(s1, s2, "round-trip 後の JSON 文字列が一致すること");
        }

        [Test]
        public void SerializeParseSerialize_MinimalProfile_ProducesIdenticalString()
        {
            // in-memory 構築プロファイル（inputSources 未設定）。Serialize 側が placeholder を emit し、
            // Parse で LayerInputSources に取り込まれ、再 Serialize で同一出力になる。
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("lipsync", 1, ExclusionMode.Blend)
            };
            var profile = new FacialProfile("1.0", layers);

            var s1 = _parser.SerializeProfile(profile);
            var s2 = _parser.SerializeProfile(_parser.ParseProfile(s1));

            Assert.AreEqual(s1, s2);
        }

        [Test]
        public void SerializeParseSerialize_MultipleInputSourcesPerLayer_PreservesOrderAndString()
        {
            var json = @"{
    ""schemaVersion"": ""1.0"",
    ""layers"": [
        {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
            {""id"": ""controller-expr"", ""weight"": 0.5},
            {""id"": ""osc"", ""weight"": 0.5}
        ]}
    ],
    ""expressions"": []
}";

            var p1 = _parser.ParseProfile(json);
            var s1 = _parser.SerializeProfile(p1);
            var p2 = _parser.ParseProfile(s1);
            var s2 = _parser.SerializeProfile(p2);

            Assert.AreEqual(s1, s2);

            // 宣言順の保持 (Req 3.5): [controller-expr, osc] が保たれる。
            Assert.AreEqual(2, p2.LayerInputSources.Span[0].Length);
            Assert.AreEqual("controller-expr", p2.LayerInputSources.Span[0][0].Id);
            Assert.AreEqual("osc", p2.LayerInputSources.Span[0][1].Id);
        }

        [Test]
        public void SerializeParseSerialize_WithOscOptions_PreservesOptionsAndString()
        {
            var json = @"{
    ""schemaVersion"": ""1.0"",
    ""layers"": [
        {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
            {""id"": ""osc"", ""weight"": 1.0, ""options"": {""stalenessSeconds"": 2.5}}
        ]}
    ],
    ""expressions"": []
}";

            var p1 = _parser.ParseProfile(json);
            var s1 = _parser.SerializeProfile(p1);
            var p2 = _parser.ParseProfile(s1);
            var s2 = _parser.SerializeProfile(p2);

            Assert.AreEqual(s1, s2);

            // options が round-trip で optionsJson として保持されていること。
            Assert.AreEqual(1, p2.LayerInputSources.Span[0].Length);
            var decl = p2.LayerInputSources.Span[0][0];
            Assert.AreEqual("osc", decl.Id);
            Assert.IsNotNull(decl.OptionsJson);
            StringAssert.Contains("stalenessSeconds", decl.OptionsJson);
            StringAssert.Contains("2.5", decl.OptionsJson);
        }

        // ================================================================
        // 宣言順と既定値の扱い (Req 3.5)
        // ================================================================

        [Test]
        public void ParseProfile_PreservesInputSourceDeclarationOrder()
        {
            var json = @"{
    ""schemaVersion"": ""1.0"",
    ""layers"": [
        {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
            {""id"": ""osc"", ""weight"": 0.3},
            {""id"": ""controller-expr"", ""weight"": 0.3},
            {""id"": ""keyboard-expr"", ""weight"": 0.4}
        ]}
    ],
    ""expressions"": []
}";

            var profile = _parser.ParseProfile(json);

            var span = profile.LayerInputSources.Span[0];
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("osc", span[0].Id);
            Assert.AreEqual("controller-expr", span[1].Id);
            Assert.AreEqual("keyboard-expr", span[2].Id);
        }

        [Test]
        public void SerializeProfile_EmitsInputSourcesInDeclarationOrder()
        {
            var profile = _parser.ParseProfile(@"{
    ""schemaVersion"": ""1.0"",
    ""layers"": [
        {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
            {""id"": ""osc"", ""weight"": 0.3},
            {""id"": ""controller-expr"", ""weight"": 0.7}
        ]}
    ],
    ""expressions"": []
}");

            var serialized = _parser.SerializeProfile(profile);

            // 出力中の "osc" が "controller-expr" より先に現れること。
            int oscIdx = serialized.IndexOf("\"osc\"", System.StringComparison.Ordinal);
            int ctrlIdx = serialized.IndexOf("\"controller-expr\"", System.StringComparison.Ordinal);
            Assert.Greater(oscIdx, 0);
            Assert.Greater(ctrlIdx, 0);
            Assert.Less(oscIdx, ctrlIdx, "宣言順が維持されて出力されること");
        }

        [Test]
        public void SerializeProfile_WeightIsAlwaysEmittedConsistently()
        {
            // 既定値 (weight=1.0) でも一貫して weight フィールドが emit される
            // (JsonUtility はフィールドを省略しないため、omission 規則は『常に出力』で一貫)。
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var profile = new FacialProfile("1.0", layers);

            var serialized = _parser.SerializeProfile(profile);

            StringAssert.Contains("\"weight\":", serialized);
            StringAssert.Contains("1", serialized);
        }

        // ================================================================
        // LayerInputSources の FacialProfile 担体機能
        // ================================================================

        [Test]
        public void ParseProfile_PopulatesLayerInputSourcesAlignedWithLayers()
        {
            var profile = _parser.ParseProfile(JsonSchemaDefinition.SampleProfileJson);

            Assert.AreEqual(profile.Layers.Length, profile.LayerInputSources.Length,
                "LayerInputSources の外側インデックスは Layers と揃う");

            Assert.AreEqual("controller-expr", profile.LayerInputSources.Span[0][0].Id);
            Assert.AreEqual("osc", profile.LayerInputSources.Span[0][1].Id);
            Assert.AreEqual("lipsync", profile.LayerInputSources.Span[1][0].Id);
            Assert.AreEqual("keyboard-expr", profile.LayerInputSources.Span[2][0].Id);
        }

        [Test]
        public void FacialProfile_WithExplicitLayerInputSources_RoundTripsThroughParser()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var layerInputSources = new[]
            {
                new[]
                {
                    new InputSourceDeclaration("controller-expr", 0.5f, null),
                    new InputSourceDeclaration("osc", 0.5f, "{\"stalenessSeconds\":1.0}")
                }
            };
            var profile = new FacialProfile("1.0", layers, null, null, layerInputSources);

            var s1 = _parser.SerializeProfile(profile);
            var p2 = _parser.ParseProfile(s1);
            var s2 = _parser.SerializeProfile(p2);

            Assert.AreEqual(s1, s2);

            Assert.AreEqual(2, p2.LayerInputSources.Span[0].Length);
            Assert.AreEqual("controller-expr", p2.LayerInputSources.Span[0][0].Id);
            Assert.AreEqual(0.5f, p2.LayerInputSources.Span[0][0].Weight);
            Assert.AreEqual("osc", p2.LayerInputSources.Span[0][1].Id);
            Assert.AreEqual(0.5f, p2.LayerInputSources.Span[0][1].Weight);
            StringAssert.Contains("stalenessSeconds", p2.LayerInputSources.Span[0][1].OptionsJson);
        }
    }
}
