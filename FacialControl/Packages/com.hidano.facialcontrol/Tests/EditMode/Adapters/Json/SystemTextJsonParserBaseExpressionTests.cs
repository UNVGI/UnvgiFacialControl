using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.Json.Dto;
using NUnit.Framework;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    [TestFixture]
    public class SystemTextJsonParserBaseExpressionTests
    {
        private SystemTextJsonParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new SystemTextJsonParser();
        }

        [Test]
        public void ParseProfileSnapshotV2_BaseExpressionField_PopulatesSnapshotBlendShapes()
        {
            var dto = _parser.ParseProfileSnapshotV2(BuildProfileJsonWithBaseExpression());

            var baseExpression = GetBaseExpression(dto);

            Assert.That(baseExpression.blendShapes, Is.Not.Null);
            Assert.That(baseExpression.blendShapes, Has.Count.EqualTo(2));
            AssertBlendShape(baseExpression.blendShapes[0], "Body", "Brow_Angry", 64.5f);
            AssertBlendShape(baseExpression.blendShapes[1], "Face", "Eye_Narrow", 28.25f);
        }

        [Test]
        public void ParseProfileSnapshotV2_BaseExpressionMissing_CreatesEmptySnapshot()
        {
            var dto = _parser.ParseProfileSnapshotV2(@"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [],
                ""expressions"": [],
                ""rendererPaths"": []
            }");

            var baseExpression = GetBaseExpression(dto);

            Assert.That(baseExpression.blendShapes, Is.Not.Null);
            Assert.That(baseExpression.blendShapes, Is.Empty);
        }

        [Test]
        public void SerializeProfileSnapshot_BaseExpression_EmitsBaseExpressionSchema()
        {
            var dto = CreateProfileSnapshotDto();
            SetBaseExpression(dto, CreateBaseExpressionSnapshot());

            string json = _parser.SerializeProfileSnapshot(dto);

            StringAssert.Contains(@"""baseExpression""", json);
            StringAssert.Contains(@"""blendShapes""", json);
            StringAssert.Contains(@"""Brow_Angry""", json);

            var parsed = _parser.ParseProfileSnapshotV2(json);
            var baseExpression = GetBaseExpression(parsed);

            Assert.That(baseExpression.blendShapes, Has.Count.EqualTo(2));
            AssertBlendShape(baseExpression.blendShapes[0], "Body", "Brow_Angry", 64.5f);
            AssertBlendShape(baseExpression.blendShapes[1], "Face", "Eye_Narrow", 28.25f);
        }

        [Test]
        public void SerializeProfileSnapshot_BaseExpression_DoesNotEmitAnimationClipPath()
        {
            var dto = CreateProfileSnapshotDto();
            SetBaseExpression(dto, CreateBaseExpressionSnapshot());

            string json = _parser.SerializeProfileSnapshot(dto);

            StringAssert.Contains(@"""baseExpression""", json);
            StringAssert.DoesNotContain("animationClip", json);
            StringAssert.DoesNotContain("BaseExpression_RoundTripClip.anim", json);
            StringAssert.DoesNotContain("Assets/BaseExpression_RoundTripClip.anim", json);
        }

        private static string BuildProfileJsonWithBaseExpression()
        {
            return @"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [],
                ""expressions"": [],
                ""rendererPaths"": [""Body"", ""Face""],
                ""baseExpression"": {
                    ""blendShapes"": [
                        {""rendererPath"": ""Body"", ""name"": ""Brow_Angry"", ""value"": 64.5},
                        {""rendererPath"": ""Face"", ""name"": ""Eye_Narrow"", ""value"": 28.25}
                    ]
                }
            }";
        }

        private static ProfileSnapshotDto CreateProfileSnapshotDto()
        {
            return new ProfileSnapshotDto
            {
                schemaVersion = SystemTextJsonParser.SchemaVersionV2,
                layers = new List<LayerDefinitionDto>(),
                expressions = new List<ExpressionDto>(),
                rendererPaths = new List<string> { "Body", "Face" },
                gazeConfigs = new List<GazeBindingConfigDto>(),
            };
        }

        private static ExpressionSnapshotDto CreateBaseExpressionSnapshot()
        {
            return new ExpressionSnapshotDto
            {
                blendShapes = new List<BlendShapeSnapshotDto>
                {
                    new BlendShapeSnapshotDto
                    {
                        rendererPath = "Body",
                        name = "Brow_Angry",
                        value = 64.5f,
                    },
                    new BlendShapeSnapshotDto
                    {
                        rendererPath = "Face",
                        name = "Eye_Narrow",
                        value = 28.25f,
                    },
                },
                bones = new List<BoneSnapshotDto>(),
                rendererPaths = new List<string> { "Body", "Face" },
            };
        }

        private static ExpressionSnapshotDto GetBaseExpression(ProfileSnapshotDto dto)
        {
            var value = GetBaseExpressionField().GetValue(dto) as ExpressionSnapshotDto;
            Assert.That(value, Is.Not.Null,
                "ProfileSnapshotDto.baseExpression must be normalized to an empty ExpressionSnapshotDto when the JSON field is missing.");
            return value;
        }

        private static void SetBaseExpression(ProfileSnapshotDto dto, ExpressionSnapshotDto snapshot)
        {
            GetBaseExpressionField().SetValue(dto, snapshot);
        }

        private static FieldInfo GetBaseExpressionField()
        {
            var field = typeof(ProfileSnapshotDto).GetField(
                "baseExpression",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null,
                "ProfileSnapshotDto must expose root baseExpression for profile.json schema round-trip.");
            Assert.That(field.IsPublic, Is.True,
                "ProfileSnapshotDto.baseExpression must be public so JsonUtility can serialize the schema field.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(ExpressionSnapshotDto)),
                "ProfileSnapshotDto.baseExpression must reuse ExpressionSnapshotDto.");
            return field;
        }

        private static void AssertBlendShape(
            BlendShapeSnapshotDto actual,
            string expectedRendererPath,
            string expectedName,
            float expectedValue)
        {
            Assert.That(actual.rendererPath, Is.EqualTo(expectedRendererPath));
            Assert.That(actual.name, Is.EqualTo(expectedName));
            Assert.That(actual.value, Is.EqualTo(expectedValue).Within(1e-6f));
        }
    }
}
