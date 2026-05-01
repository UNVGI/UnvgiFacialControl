using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// Phase 3.6 (inspector-and-data-model-redesign) tasks.md 3.6:
    /// <see cref="SystemTextJsonParser"/> が schema v2.0 専用となり、
    /// <c>schemaVersion: "2.0"</c> 以外を <see cref="Debug.LogError(object)"/> +
    /// <see cref="InvalidOperationException"/> で拒否することの契約テスト（Req 9.1, 9.2, 9.7, 10.1）。
    /// </summary>
    [TestFixture]
    public class SystemTextJsonParserV2Tests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        // ================================================================
        // Parse_SchemaV2_ReturnsExpectedProfile
        // ================================================================

        [Test]
        public void Parse_SchemaV2_ReturnsExpectedProfile()
        {
            var json = @"{
                ""schemaVersion"": ""2.0"",
                ""rendererPaths"": [""Body""],
                ""layers"": [
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"": [
                    {
                        ""id"": ""550e8400-e29b-41d4-a716-446655440000"",
                        ""name"": ""Smile"",
                        ""layer"": ""emotion"",
                        ""layerOverrideMask"": [""emotion""],
                        ""snapshot"": {
                            ""transitionDuration"": 0.3,
                            ""transitionCurvePreset"": ""EaseInOut"",
                            ""blendShapes"": [
                                {""rendererPath"":""Body"",""name"":""Smile"",""value"":1.0}
                            ],
                            ""bones"": [],
                            ""rendererPaths"": [""Body""]
                        }
                    }
                ]
            }";

            var profile = _parser.ParseProfile(json);

            Assert.AreEqual(SystemTextJsonParser.SchemaVersionV2, profile.SchemaVersion);
            Assert.AreEqual(1, profile.Layers.Length);
            Assert.AreEqual("emotion", profile.Layers.Span[0].Name);

            Assert.AreEqual(1, profile.Expressions.Length);
            var expr = profile.Expressions.Span[0];
            Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", expr.Id);
            Assert.AreEqual("Smile", expr.Name);
            Assert.AreEqual("emotion", expr.Layer);
            Assert.AreEqual(0.3f, expr.TransitionDuration);
            Assert.AreEqual(TransitionCurveType.EaseInOut, expr.TransitionCurve.Type);

            Assert.AreEqual(1, expr.BlendShapeValues.Length);
            Assert.AreEqual("Smile", expr.BlendShapeValues.Span[0].Name);
            Assert.AreEqual(1.0f, expr.BlendShapeValues.Span[0].Value);
            Assert.AreEqual("Body", expr.BlendShapeValues.Span[0].Renderer);

            Assert.AreEqual(1, profile.RendererPaths.Length);
            Assert.AreEqual("Body", profile.RendererPaths.Span[0]);
        }

        // ================================================================
        // Parse_SchemaV1_ThrowsAndLogsError
        // ================================================================

        [Test]
        public void Parse_SchemaV1_ThrowsAndLogsError()
        {
            var json = @"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"": []
            }";

            LogAssert.Expect(LogType.Error, new Regex("schema v2.0 の strict チェックに失敗"));

            var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseProfile(json));
            StringAssert.Contains("'1.0'", ex.Message);
            StringAssert.Contains("'2.0'", ex.Message);
        }

        // ================================================================
        // Parse_MissingSchemaVersion_ThrowsAndLogsError
        // ================================================================

        [Test]
        public void Parse_MissingSchemaVersion_ThrowsAndLogsError()
        {
            var json = @"{
                ""layers"": [
                    {""name"":""emotion"",""priority"":0,""exclusionMode"":""lastWins"",""inputSources"":[
                        {""id"":""controller-expr"",""weight"":1.0}
                    ]}
                ],
                ""expressions"": []
            }";

            LogAssert.Expect(LogType.Error, new Regex("schema v2.0 の strict チェックに失敗"));

            var ex = Assert.Throws<InvalidOperationException>(() => _parser.ParseProfile(json));
            StringAssert.Contains("<missing>", ex.Message);
            StringAssert.Contains("'2.0'", ex.Message);
        }
    }
}
