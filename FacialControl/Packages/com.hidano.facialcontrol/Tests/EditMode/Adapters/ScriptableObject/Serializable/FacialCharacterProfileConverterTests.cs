using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.Serializable
{
    /// <summary>
    /// FacialCharacterProfileConverter の Serializable → Domain 変換を検証する。
    /// JSON 経路を通さず直接 FacialProfile / AnalogInputBindingProfile が組み立てられることを保証する
    /// (3-B モデルにおける JSON 不在時のフォールバックの実体)。
    /// </summary>
    [TestFixture]
    public class FacialCharacterProfileConverterTests
    {
        [Test]
        public void ToFacialProfile_FullPayload_ProducesEquivalentDomainModel()
        {
            var layers = new List<LayerDefinitionSerializable>
            {
                new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                    inputSources = new List<InputSourceDeclarationSerializable>
                    {
                        new InputSourceDeclarationSerializable { id = "controller-expr", weight = 1.0f },
                    },
                },
                new LayerDefinitionSerializable
                {
                    name = "eye",
                    priority = 1,
                    exclusionMode = ExclusionMode.Blend,
                    inputSources = new List<InputSourceDeclarationSerializable>
                    {
                        new InputSourceDeclarationSerializable { id = "x-analog-eye", weight = 0.5f, optionsJson = "{\"k\":1}" },
                    },
                },
            };

            var expressions = new List<ExpressionSerializable>
            {
                new ExpressionSerializable
                {
                    id = "smile",
                    name = "Smile",
                    layer = "emotion",
                    transitionDuration = 0.3f,
                    transitionCurve = new TransitionCurveSerializable { type = TransitionCurveType.EaseIn },
                    blendShapeValues = new List<BlendShapeMappingSerializable>
                    {
                        new BlendShapeMappingSerializable { name = "Smile", value = 1.0f },
                    },
                },
            };

            var rendererPaths = new List<string> { "Body" };
            var bonePoses = new List<BonePoseSerializable>
            {
                new BonePoseSerializable
                {
                    id = "neutral",
                    entries = new[]
                    {
                        new BonePoseEntrySerializable { boneName = "Head", eulerXYZ = new Vector3(1f, 2f, 3f) },
                    },
                },
            };

            var profile = FacialCharacterProfileConverter.ToFacialProfile(
                "1.0", layers, expressions, rendererPaths, bonePoses);

            Assert.That(profile.SchemaVersion, Is.EqualTo("1.0"));
            Assert.That(profile.Layers.Length, Is.EqualTo(2));
            Assert.That(profile.Layers.Span[0].Name, Is.EqualTo("emotion"));
            Assert.That(profile.Layers.Span[1].ExclusionMode, Is.EqualTo(ExclusionMode.Blend));

            Assert.That(profile.LayerInputSources.Length, Is.EqualTo(2));
            Assert.That(profile.LayerInputSources.Span[0].Length, Is.EqualTo(1));
            Assert.That(profile.LayerInputSources.Span[0][0].Id, Is.EqualTo("controller-expr"));
            Assert.That(profile.LayerInputSources.Span[1][0].OptionsJson, Is.EqualTo("{\"k\":1}"));

            Assert.That(profile.Expressions.Length, Is.EqualTo(1));
            Assert.That(profile.Expressions.Span[0].Id, Is.EqualTo("smile"));
            Assert.That(profile.Expressions.Span[0].TransitionCurve.Type, Is.EqualTo(TransitionCurveType.EaseIn));

            Assert.That(profile.RendererPaths.Length, Is.EqualTo(1));
            Assert.That(profile.RendererPaths.Span[0], Is.EqualTo("Body"));

            Assert.That(profile.BonePoses.Length, Is.EqualTo(1));
            var poseEntries = profile.BonePoses.Span[0].Entries.Span;
            Assert.That(poseEntries.Length, Is.EqualTo(1));
            Assert.That(poseEntries[0].BoneName, Is.EqualTo("Head"));
            Assert.That(poseEntries[0].EulerY, Is.EqualTo(2f));
        }

        [Test]
        public void ToFacialProfile_NullCollections_ReturnsEmptyProfileWithDefaultSchema()
        {
            var profile = FacialCharacterProfileConverter.ToFacialProfile(null, null, null, null, null);

            Assert.That(profile.SchemaVersion, Is.EqualTo("1.0"));
            Assert.That(profile.Layers.Length, Is.EqualTo(0));
            Assert.That(profile.Expressions.Length, Is.EqualTo(0));
            Assert.That(profile.RendererPaths.Length, Is.EqualTo(0));
            Assert.That(profile.BonePoses.Length, Is.EqualTo(0));
        }

        [Test]
        public void ToFacialProfile_LayerWithoutName_IsSkipped()
        {
            var layers = new List<LayerDefinitionSerializable>
            {
                new LayerDefinitionSerializable { name = "valid", priority = 0 },
                new LayerDefinitionSerializable { name = "", priority = 0 },
                new LayerDefinitionSerializable { name = "another", priority = 1 },
            };

            var profile = FacialCharacterProfileConverter.ToFacialProfile(
                "1.0", layers, null, null, null);

            Assert.That(profile.Layers.Length, Is.EqualTo(2));
            Assert.That(profile.Layers.Span[0].Name, Is.EqualTo("valid"));
            Assert.That(profile.Layers.Span[1].Name, Is.EqualTo("another"));
        }

        [Test]
        public void ToFacialProfile_ExpressionWithMissingMandatoryField_IsSkipped()
        {
            var expressions = new List<ExpressionSerializable>
            {
                new ExpressionSerializable { id = "ok", name = "OK", layer = "emotion" },
                new ExpressionSerializable { id = "", name = "no-id", layer = "emotion" },
                new ExpressionSerializable { id = "no-name", name = "", layer = "emotion" },
                new ExpressionSerializable { id = "no-layer", name = "X", layer = "" },
            };

            var profile = FacialCharacterProfileConverter.ToFacialProfile(
                "1.0", null, expressions, null, null);

            Assert.That(profile.Expressions.Length, Is.EqualTo(1));
            Assert.That(profile.Expressions.Span[0].Id, Is.EqualTo("ok"));
        }

        [Test]
        public void ToAnalogProfile_ProducesEquivalentDomainModel()
        {
            var bindings = new List<AnalogBindingEntrySerializable>
            {
                new AnalogBindingEntrySerializable
                {
                    sourceId = "x-right-stick",
                    sourceAxis = 1,
                    targetKind = AnalogBindingTargetKind.BlendShape,
                    targetIdentifier = "JawOpen",
                    mapping = new AnalogMappingFunctionSerializable
                    {
                        deadZone = 0.1f,
                        scale = 2f,
                        offset = 0.05f,
                        invert = true,
                        min = 0f,
                        max = 1f,
                    },
                },
            };

            var profile = FacialCharacterProfileConverter.ToAnalogProfile("1.0", bindings);

            Assert.That(profile.Bindings.Length, Is.EqualTo(1));
            var entry = profile.Bindings.Span[0];
            Assert.That(entry.SourceId, Is.EqualTo("x-right-stick"));
            Assert.That(entry.SourceAxis, Is.EqualTo(1));
            Assert.That(entry.TargetIdentifier, Is.EqualTo("JawOpen"));
            Assert.That(entry.Mapping.DeadZone, Is.EqualTo(0.1f));
            Assert.That(entry.Mapping.Invert, Is.True);
        }

        [Test]
        public void ToAnalogProfile_InvalidEntries_AreSkipped()
        {
            var bindings = new List<AnalogBindingEntrySerializable>
            {
                new AnalogBindingEntrySerializable
                {
                    sourceId = "x-right-stick",
                    targetIdentifier = "valid",
                },
                new AnalogBindingEntrySerializable
                {
                    sourceId = "x-empty-target",
                    targetIdentifier = "",
                },
                new AnalogBindingEntrySerializable
                {
                    sourceId = "x-bad-range",
                    targetIdentifier = "valid",
                    mapping = new AnalogMappingFunctionSerializable { min = 1f, max = 0f },
                },
            };

            var profile = FacialCharacterProfileConverter.ToAnalogProfile("1.0", bindings);

            Assert.That(profile.Bindings.Length, Is.EqualTo(1));
            Assert.That(profile.Bindings.Span[0].SourceId, Is.EqualTo("x-right-stick"));
        }
    }
}
