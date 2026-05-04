using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;

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
    }
}
