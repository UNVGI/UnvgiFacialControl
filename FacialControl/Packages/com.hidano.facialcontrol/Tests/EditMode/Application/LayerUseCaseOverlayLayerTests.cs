using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Application.UseCases;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Application
{
    /// <summary>
    /// 「emotion + overlay」 2 段レイヤー構成で <see cref="OverlayInputSource"/> を駆動した最終出力の回帰テスト。
    /// 仕様:
    /// <list type="bullet">
    ///   <item>active 表情の overlays.blink → 該当 overlay Expression が overlay レイヤー出力</item>
    ///   <item>active 表情の overlays.blink = "" (suppress) → overlay 出力 false、emotion 値貫通</item>
    ///   <item>active 表情に slot 宣言なし → profile.defaultOverlays.blink fallback</item>
    ///   <item>overlay レイヤー全体の weight = Trigger 値で lerp（LayerBlender 既存仕様）</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class LayerUseCaseOverlayLayerTests
    {
        private const string BrowName = "bs_brow";
        private const string EyeMakeupName = "bs_eye_lift"; // 目尻吊り上げ相当
        private const string EyeBlinkName = "bs_eye_blink";
        private const string MouthName = "bs_mouth";

        private static (FacialProfile profile, string[] bsNames) BuildProfile()
        {
            var bsNames = new[] { BrowName, EyeMakeupName, EyeBlinkName, MouthName };
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("overlay", 1, ExclusionMode.LastWins),
            };

            var anger = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[]
                {
                    new BlendShapeMapping(BrowName, 1.0f),
                    new BlendShapeMapping(EyeMakeupName, 1.0f),  // 目尻吊り上げ
                },
                overlays: new[] { new OverlaySlotBinding("blink", "anger_blink") });

            // anger 専用の閉じ目（目尻=0 で打消し、まばたき=1）
            var angerBlink = new Expression(
                "anger_blink", "AngerBlink", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[]
                {
                    new BlendShapeMapping(EyeMakeupName, 0.0f),
                    new BlendShapeMapping(EyeBlinkName, 1.0f),
                });

            var smile = new Expression(
                "smile", "Smile", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[]
                {
                    new BlendShapeMapping(MouthName, 0.5f),
                });

            // すでに目を閉じている表情。blink slot を suppress 宣言。
            var smileClosedEye = new Expression(
                "smile_closed_eye", "SmileClosedEye", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[]
                {
                    new BlendShapeMapping(MouthName, 0.5f),
                    new BlendShapeMapping(EyeBlinkName, 1.0f),
                },
                overlays: new[] { new OverlaySlotBinding("blink", null) });

            var defaultBlink = new Expression(
                "default_blink", "DefaultBlink", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: new[] { new BlendShapeMapping(EyeBlinkName, 1.0f) });

            var inputSources = new[]
            {
                new[] { new InputSourceDeclaration("input", 1f, null) },
                new[] { new InputSourceDeclaration("input:overlay:blink", 1f, null) },
            };

            var profile = new FacialProfile(
                "1.0", layers,
                new[] { anger, angerBlink, smile, smileClosedEye, defaultBlink },
                rendererPaths: null,
                layerInputSources: inputSources,
                defaultOverlays: new[] { new OverlaySlotBinding("blink", "default_blink") });

            return (profile, bsNames);
        }

        private static (LayerUseCase useCase, ExpressionUseCase exprUseCase) BuildPipeline(
            FacialProfile profile, string[] bsNames)
        {
            var exprUseCase = new ExpressionUseCase(profile);

            var overlayInputSource = new OverlayInputSource(
                id: InputSourceId.Parse("overlay:blink"),
                slot: "blink",
                blendShapeCount: bsNames.Length,
                blendShapeNames: bsNames,
                profile: profile,
                activeProvider: exprUseCase,
                emotionLayerName: "emotion");

            var additional = new List<(int layerIdx, IInputSource source, float weight)>
            {
                (1, overlayInputSource, 1f),
            };
            var useCase = new LayerUseCase(profile, exprUseCase, bsNames, additional);
            return (useCase, exprUseCase);
        }

        [Test]
        public void AngerHold_FullTrigger_OverlayReplacesEyeBlendShapes_BrowVisits()
        {
            var (profile, bsNames) = BuildProfile();
            var (useCase, exprUseCase) = BuildPipeline(profile, bsNames);
            using (useCase)
            {
                exprUseCase.Activate(profile.FindExpressionById("anger").Value);
                useCase.SetLayerWeight("overlay", 1f); // RT 全押し
                useCase.UpdateWeights(1f);

                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(1.0f, output[0], 1e-3f, "Brow は overlay の ContributeMask off → emotion 値貫通");
                Assert.AreEqual(0.0f, output[1], 1e-3f, "目尻吊り上げ: emotion=1.0 を overlay=0.0 で完全 lerp 上書き");
                Assert.AreEqual(1.0f, output[2], 1e-3f, "まばたき: overlay=1.0 で完全立ち上がり");
            }
        }

        [Test]
        public void AngerHold_HalfTrigger_LinearInterpolation()
        {
            var (profile, bsNames) = BuildProfile();
            var (useCase, exprUseCase) = BuildPipeline(profile, bsNames);
            using (useCase)
            {
                exprUseCase.Activate(profile.FindExpressionById("anger").Value);
                useCase.SetLayerWeight("overlay", 0.5f); // RT 半押し
                useCase.UpdateWeights(1f);

                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(1.0f, output[0], 1e-3f, "Brow は emotion=1.0 維持");
                Assert.AreEqual(0.5f, output[1], 1e-3f, "目尻: lerp(1.0, 0.0, 0.5) = 0.5");
                Assert.AreEqual(0.5f, output[2], 1e-3f, "まばたき: lerp(0, 1.0, 0.5) = 0.5");
            }
        }

        [Test]
        public void SmileClosedEyeHold_FullTrigger_OverlaySuppressed()
        {
            var (profile, bsNames) = BuildProfile();
            var (useCase, exprUseCase) = BuildPipeline(profile, bsNames);
            using (useCase)
            {
                exprUseCase.Activate(profile.FindExpressionById("smile_closed_eye").Value);
                useCase.SetLayerWeight("overlay", 1f);
                useCase.UpdateWeights(1f);

                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(0.5f, output[3], 1e-3f, "Mouth は emotion 値維持");
                Assert.AreEqual(1.0f, output[2], 1e-3f, "まばたき: smile_closed_eye 自体が 1.0 を出している。overlay は suppress なので二重発火しない");
                // overlay 出力が無効 = Mouth/Eye いずれの index も emotion 値が貫通
            }
        }

        [Test]
        public void NoActiveExpression_FullTrigger_DefaultBlinkFires()
        {
            var (profile, bsNames) = BuildProfile();
            var (useCase, _) = BuildPipeline(profile, bsNames);
            using (useCase)
            {
                useCase.SetLayerWeight("overlay", 1f);
                useCase.UpdateWeights(1f);

                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(0.0f, output[0], 1e-3f);
                Assert.AreEqual(0.0f, output[1], 1e-3f);
                Assert.AreEqual(1.0f, output[2], 1e-3f, "active 表情なし → defaultOverlays.blink (default_blink) が発火、まばたき=1");
            }
        }

        [Test]
        public void SmileHold_FullTrigger_FallsBackToDefaultBlink()
        {
            var (profile, bsNames) = BuildProfile();
            var (useCase, exprUseCase) = BuildPipeline(profile, bsNames);
            using (useCase)
            {
                // smile は overlays.blink を宣言していない → defaultOverlays.blink にフォールバック
                exprUseCase.Activate(profile.FindExpressionById("smile").Value);
                useCase.SetLayerWeight("overlay", 1f);
                useCase.UpdateWeights(1f);

                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(0.5f, output[3], 1e-3f, "Mouth は smile 値が emotion レイヤーから貫通");
                Assert.AreEqual(1.0f, output[2], 1e-3f, "まばたき: default_blink 経由で 1");
            }
        }
    }
}
