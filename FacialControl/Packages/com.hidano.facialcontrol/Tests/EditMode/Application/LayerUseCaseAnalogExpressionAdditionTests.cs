using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Application.UseCases;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Application
{
    /// <summary>
    /// <see cref="AnalogExpressionInputSource"/> による intra-layer 加算経路の契約テスト。
    /// 同一レイヤーに <see cref="LayerUseCase"/> 既定の LayerExpressionSource (sourceIdx=0) と
    /// <see cref="AnalogExpressionInputSource"/> (sourceIdx=1) を同居させ、Analog 値に応じて
    /// 特定 BlendShape のみが上乗せされ、それ以外の BlendShape は表情の最終値を維持することを観測する。
    /// （Overlay slot による inter-layer 機構は別ファイル <c>LayerUseCaseOverlayLayerTests</c> で扱う。）
    /// </summary>
    [TestFixture]
    public class LayerUseCaseAnalogExpressionAdditionTests
    {
        private const string BrowName = "bs_brow";
        private const string MouthName = "bs_mouth";
        private const string EyeName = "bs_eye";

        /// <summary>
        /// AnalogExpressionInputSource に流す scalar をテスト側で書換可能にしたフェイクソース。
        /// </summary>
        private sealed class FakeScalarSource : IAnalogInputSource
        {
            public FakeScalarSource(string id) { Id = id; }

            public string Id { get; }
            public bool IsValid => true;
            public int AxisCount => 1;
            public float Value { get; set; }

            public void Tick(float deltaTime) { }

            public bool TryReadScalar(out float value)
            {
                value = Value;
                return true;
            }

            public bool TryReadVector2(out float x, out float y)
            {
                x = Value;
                y = 0f;
                return true;
            }

            public bool TryReadAxes(Span<float> output)
            {
                if (output.Length >= 1) output[0] = Value;
                return true;
            }
        }

        private static FacialProfile CreateProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            var smile = new Expression(
                "smile", "Smile", "emotion", 0f, TransitionCurve.Linear,
                new[]
                {
                    new BlendShapeMapping(BrowName, 1.0f),
                    new BlendShapeMapping(MouthName, 0.5f),
                });
            var blinkOverlay = new Expression(
                "blink_overlay", "BlinkOverlay", "emotion", 0f, TransitionCurve.Linear,
                new[]
                {
                    new BlendShapeMapping(EyeName, 1.0f),
                });

            return new FacialProfile("1.0", layers, new[] { smile, blinkOverlay });
        }

        private static (LayerUseCase useCase, ExpressionUseCase exprUseCase, FakeScalarSource trigger)
            BuildPipeline(FacialProfile profile, string[] blendShapeNames)
        {
            var exprUseCase = new ExpressionUseCase(profile);

            var trigger = new FakeScalarSource("trigger");
            var sources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal)
            {
                { trigger.Id, trigger },
            };
            var bindings = new[]
            {
                new AnalogExpressionBinding(
                    sourceId: trigger.Id,
                    sourceAxis: 0,
                    expressionId: "blink_overlay",
                    scale: 1f),
            };

            var analogOverlay = new AnalogExpressionInputSource(
                id: InputSourceId.Parse(AnalogExpressionInputSource.ReservedId),
                blendShapeCount: blendShapeNames.Length,
                blendShapeNames: blendShapeNames,
                profile: profile,
                sources: sources,
                bindings: bindings);

            var additional = new List<(int layerIdx, IInputSource source, float weight)>
            {
                (0, analogOverlay, 1f),
            };

            var useCase = new LayerUseCase(profile, exprUseCase, blendShapeNames, additional);
            return (useCase, exprUseCase, trigger);
        }

        private static void ActivateSmile(ExpressionUseCase exprUseCase, FacialProfile profile)
        {
            var smile = profile.FindExpressionById("smile");
            Assert.IsTrue(smile.HasValue, "テスト前提: profile に smile が存在すること。");
            exprUseCase.Activate(smile.Value);
        }

        [Test]
        public void TriggerZero_KeepsExpressionAndLeavesEyeUntouched()
        {
            var profile = CreateProfile();
            var blendShapeNames = new[] { BrowName, MouthName, EyeName };
            var (useCase, exprUseCase, trigger) = BuildPipeline(profile, blendShapeNames);
            using (useCase)
            {
                ActivateSmile(exprUseCase, profile);
                trigger.Value = 0f;

                // 1 frame 進めて Expression 遷移を完了させる (transitionDuration = 0)
                useCase.UpdateWeights(1f);
                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(1.0f, output[0], 1e-4f, "Trigger=0 でも Brow は smile の値を維持すること");
                Assert.AreEqual(0.5f, output[1], 1e-4f, "Trigger=0 でも MouthCorner は smile の値を維持すること");
                Assert.AreEqual(0.0f, output[2], 1e-4f, "Trigger=0 のとき Eye は閉じないこと");
            }
        }

        [Test]
        public void TriggerHalf_OnlyEyeReceivesHalfClosure()
        {
            var profile = CreateProfile();
            var blendShapeNames = new[] { BrowName, MouthName, EyeName };
            var (useCase, exprUseCase, trigger) = BuildPipeline(profile, blendShapeNames);
            using (useCase)
            {
                ActivateSmile(exprUseCase, profile);
                trigger.Value = 0.5f;

                useCase.UpdateWeights(1f);
                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(1.0f, output[0], 1e-4f, "Trigger 半押しでも Brow は smile の値を維持すること");
                Assert.AreEqual(0.5f, output[1], 1e-4f, "Trigger 半押しでも MouthCorner は smile の値を維持すること");
                Assert.AreEqual(0.5f, output[2], 1e-4f, "Trigger 半押しで Eye は半閉じになること (blink_overlay の Eye 値 1.0 × Trigger 0.5)");
            }
        }

        [Test]
        public void TriggerFull_EyeFullyClosedWithoutDisturbingOthers()
        {
            var profile = CreateProfile();
            var blendShapeNames = new[] { BrowName, MouthName, EyeName };
            var (useCase, exprUseCase, trigger) = BuildPipeline(profile, blendShapeNames);
            using (useCase)
            {
                ActivateSmile(exprUseCase, profile);
                trigger.Value = 1.0f;

                useCase.UpdateWeights(1f);
                var output = useCase.GetBlendedOutput();

                Assert.AreEqual(1.0f, output[0], 1e-4f, "Trigger 全押しでも Brow は smile の値を維持すること");
                Assert.AreEqual(0.5f, output[1], 1e-4f, "Trigger 全押しでも MouthCorner は smile の値を維持すること");
                Assert.AreEqual(1.0f, output[2], 1e-4f, "Trigger 全押しで Eye は完全に閉じること");
            }
        }

        [Test]
        public void TriggerSweep_EyeFollowsTriggerLinearly_BrowAndMouthStable()
        {
            var profile = CreateProfile();
            var blendShapeNames = new[] { BrowName, MouthName, EyeName };
            var (useCase, exprUseCase, trigger) = BuildPipeline(profile, blendShapeNames);
            using (useCase)
            {
                ActivateSmile(exprUseCase, profile);

                // 表情遷移を完了させてから Trigger を動かす。
                trigger.Value = 0f;
                useCase.UpdateWeights(1f);

                foreach (var t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1.0f })
                {
                    trigger.Value = t;
                    useCase.UpdateWeights(0.016f);
                    var output = useCase.GetBlendedOutput();

                    Assert.AreEqual(1.0f, output[0], 1e-4f, $"Trigger={t} で Brow が smile の値から動かないこと");
                    Assert.AreEqual(0.5f, output[1], 1e-4f, $"Trigger={t} で MouthCorner が smile の値から動かないこと");
                    Assert.AreEqual(t, output[2], 1e-4f, $"Trigger={t} に Eye が線形追従すること");
                }
            }
        }
    }
}
