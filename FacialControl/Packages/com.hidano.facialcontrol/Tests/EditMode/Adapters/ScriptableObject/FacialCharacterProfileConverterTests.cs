using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;
using GazeBindingConfig = Hidano.FacialControl.Adapters.ScriptableObject.GazeBindingConfig;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests
{
    /// <summary>
    /// <see cref="FacialCharacterProfileConverter"/> の SO → Domain 変換挙動を検証する。
    /// 特に bridge field <c>transitionDuration</c> が cachedSnapshot 存在時にも
    /// Inspector スライダー値（src.transitionDuration）として尊重されることを担保する。
    /// </summary>
    [TestFixture]
    public class FacialCharacterProfileConverterTests
    {
        [Test]
        public void ConvertExpressions_CachedSnapshotPresent_UsesInspectorTransitionDuration()
        {
            // Inspector スライダーで 0.6f に編集された Expression を再現する。
            // cachedSnapshot 側は AnimationEvent 由来の旧値 0.25f を保持しているケース。
            var layers = new List<LayerDefinitionSerializable>
            {
                new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                },
            };
            var expressions = new List<ExpressionSerializable>
            {
                new ExpressionSerializable
                {
                    id = "smile",
                    name = "Smile",
                    layer = "emotion",
                    transitionDuration = 0.6f,
                    cachedSnapshot = new ExpressionSnapshotDto
                    {
                        transitionDuration = 0.25f,
                        transitionCurvePreset = "Linear",
                        blendShapes = new List<BlendShapeSnapshotDto>
                        {
                            new BlendShapeSnapshotDto
                            {
                                rendererPath = "Body",
                                name = "笑い",
                                value = 1f,
                            },
                        },
                    },
                },
            };

            var profile = FacialCharacterProfileConverter.ToFacialProfile(
                "2.0",
                layers,
                expressions,
                new List<string> { "Body" });

            Assert.That(profile.Expressions.Length, Is.EqualTo(1));
            var expr = profile.Expressions.Span[0];
            Assert.That(
                expr.TransitionDuration,
                Is.EqualTo(0.6f).Within(1e-6f),
                "Inspector スライダー src.transitionDuration が cachedSnapshot より優先されるべき。");

            // BlendShape は引き続き cachedSnapshot から拾われていること。
            Assert.That(expr.BlendShapeValues.Length, Is.EqualTo(1));
            Assert.That(expr.BlendShapeValues.Span[0].Name, Is.EqualTo("笑い"));
        }

        [Test]
        public void ConvertExpressions_CachedSnapshotAbsent_UsesInspectorTransitionDuration()
        {
            // cachedSnapshot が無い場合も従来通り src.transitionDuration が使われる。
            var layers = new List<LayerDefinitionSerializable>
            {
                new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                },
            };
            var expressions = new List<ExpressionSerializable>
            {
                new ExpressionSerializable
                {
                    id = "smile",
                    name = "Smile",
                    layer = "emotion",
                    transitionDuration = 0.4f,
                },
            };

            var profile = FacialCharacterProfileConverter.ToFacialProfile(
                "2.0",
                layers,
                expressions,
                new List<string>());

            Assert.That(profile.Expressions.Length, Is.EqualTo(1));
            Assert.That(
                profile.Expressions.Span[0].TransitionDuration,
                Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void ToSORootGazeConfigs_RootDtoGazeConfigs_MapsEveryValueToRootConfig()
        {
            var dto = new ProfileSnapshotDto
            {
                gazeConfigs = new List<GazeBindingConfigDto>
                {
                    new GazeBindingConfigDto
                    {
                        expressionId = "eye_look",
                        leftEyeBonePath = "Armature/Hips/Head/LeftEye",
                        leftEyeInitialRotation = new Vector3(1f, 2f, 3f),
                        leftEyeYawAxisLocal = new Vector3(0f, 1f, 0f),
                        leftEyePitchAxisLocal = new Vector3(1f, 0f, 0f),
                        rightEyeBonePath = "Armature/Hips/Head/RightEye",
                        rightEyeInitialRotation = new Vector3(4f, 5f, 6f),
                        rightEyeYawAxisLocal = new Vector3(0f, 0.75f, 0.25f),
                        rightEyePitchAxisLocal = new Vector3(0.5f, 0f, 0.5f),
                        lookUpAngle = 16f,
                        lookDownAngle = 8f,
                        outerYawAngle = 17f,
                        innerYawAngle = 12f,
                    },
                },
            };

            List<GazeBindingConfig> configs = FacialCharacterProfileConverter.ToSORootGazeConfigs(dto);

            Assert.That(configs, Has.Count.EqualTo(1));
            GazeBindingConfig config = configs[0];
            Assert.That(config.expressionId, Is.EqualTo("eye_look"));
            Assert.That(config.leftEyeBonePath, Is.EqualTo("Armature/Hips/Head/LeftEye"));
            Assert.That(config.leftEyeInitialRotation, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(config.leftEyeYawAxisLocal, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(config.leftEyePitchAxisLocal, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(config.rightEyeBonePath, Is.EqualTo("Armature/Hips/Head/RightEye"));
            Assert.That(config.rightEyeInitialRotation, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(config.rightEyeYawAxisLocal, Is.EqualTo(new Vector3(0f, 0.75f, 0.25f)));
            Assert.That(config.rightEyePitchAxisLocal, Is.EqualTo(new Vector3(0.5f, 0f, 0.5f)));
            Assert.That(config.lookUpAngle, Is.EqualTo(16f));
            Assert.That(config.lookDownAngle, Is.EqualTo(8f));
            Assert.That(config.outerYawAngle, Is.EqualTo(17f));
            Assert.That(config.innerYawAngle, Is.EqualTo(12f));
            Assert.That(config.lookLeftClip, Is.Null);
            Assert.That(config.lookRightClip, Is.Null);
            Assert.That(config.lookUpClip, Is.Null);
            Assert.That(config.lookDownClip, Is.Null);
            Assert.That(config.lookLeftSamples, Is.Empty);
            Assert.That(config.lookRightSamples, Is.Empty);
            Assert.That(config.lookUpSamples, Is.Empty);
            Assert.That(config.lookDownSamples, Is.Empty);
        }

        [Test]
        public void ToSORootGazeConfigs_NullOrEmptyRootDto_ReturnsEmptyList()
        {
            Assert.That(FacialCharacterProfileConverter.ToSORootGazeConfigs((ProfileSnapshotDto)null), Is.Empty);
            Assert.That(FacialCharacterProfileConverter.ToSORootGazeConfigs(new ProfileSnapshotDto()), Is.Empty);
        }
    }
}
