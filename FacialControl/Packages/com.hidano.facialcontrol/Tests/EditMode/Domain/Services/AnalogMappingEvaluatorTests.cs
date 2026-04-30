using System;
using NUnit.Framework;
using UnityEngine.Profiling;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Services
{
    /// <summary>
    /// <see cref="AnalogMappingEvaluator"/> の段階別評価テスト
    /// （tasks.md 1.2 / Req 2.3, 2.4, 2.6, 8.1）。
    /// </summary>
    /// <remarks>
    /// 適用順は厳密に <c>dead-zone(re-center) → scale → offset → curve → invert → clamp(min, max)</c>。
    /// 各段の単独効果を分離して検証するため、他段は無効値（Identity 同等）で固定する。
    /// </remarks>
    [TestFixture]
    public class AnalogMappingEvaluatorTests
    {
        private const float Tolerance = 0.0001f;

        // --- Identity 経路 ---

        [Test]
        public void Evaluate_Identity_PassesInputThrough()
        {
            // Identity: deadZone=0, scale=1, offset=0, curve=Linear, invert=false, min=0, max=1
            float result = AnalogMappingEvaluator.Evaluate(AnalogMappingFunction.Identity, 0.5f);

            Assert.AreEqual(0.5f, result, Tolerance);
        }

        [Test]
        public void Evaluate_Identity_NegativeInput_ClampedToZero()
        {
            // Identity の min=0 で負入力は 0 にクランプされる
            float result = AnalogMappingEvaluator.Evaluate(AnalogMappingFunction.Identity, -0.3f);

            Assert.AreEqual(0f, result, Tolerance);
        }

        [Test]
        public void Evaluate_Identity_AboveOne_ClampedToOne()
        {
            float result = AnalogMappingEvaluator.Evaluate(AnalogMappingFunction.Identity, 1.5f);

            Assert.AreEqual(1f, result, Tolerance);
        }

        // --- Stage 1: dead-zone (re-center) ---

        [Test]
        public void Evaluate_InputBelowDeadZone_ReturnsExactlyZero()
        {
            // Req 2.4: |input| < deadZone のとき再センタ後 0
            var fn = new AnalogMappingFunction(
                deadZone: 0.1f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.05f);

            Assert.AreEqual(0f, result, "デッドゾーン内は厳密ゼロ");
        }

        [Test]
        public void Evaluate_InputAtDeadZoneBoundary_ReturnsExactlyZero()
        {
            // 境界値 |input| == deadZone は dead-zone 扱い (厳密 0)
            var fn = new AnalogMappingFunction(
                deadZone: 0.1f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.1f);

            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Evaluate_NegativeInputBelowDeadZone_ReturnsExactlyZero()
        {
            // 負域もデッドゾーン適用
            var fn = new AnalogMappingFunction(
                deadZone: 0.2f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, -0.15f);

            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Evaluate_InputAboveDeadZone_ReCentered()
        {
            // deadZone=0.2, input=0.6 → 再センタ: (0.6 - 0.2) / (1 - 0.2) = 0.5
            var fn = new AnalogMappingFunction(
                deadZone: 0.2f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.6f);

            Assert.AreEqual(0.5f, result, Tolerance);
        }

        [Test]
        public void Evaluate_NegativeInputAboveDeadZone_ReCenteredAndNormalizedToPositiveRange()
        {
            // joystick 風の典型: scale=0.5, offset=0.5 で [-1,1] → [0,1] へ正規化
            // input=-0.6, deadZone=0.2 → 再センタ: -(0.6-0.2)/0.8 = -0.5
            // scale=0.5 → -0.25 → offset=0.5 → 0.25 → curve(Linear, 0.25) = 0.25
            var fn = new AnalogMappingFunction(
                deadZone: 0.2f, scale: 0.5f, offset: 0.5f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, -0.6f);

            Assert.AreEqual(0.25f, result, Tolerance,
                "負入力の再センタが下流の scale/offset を経由して期待値を生成するはず");
        }

        [Test]
        public void Evaluate_NegativeInputAboveDeadZone_PreservesSignBeforeCurveClamp()
        {
            // 再センタ単独の挙動 (negative sign 保持) を curve 前に確認するため、
            // scale=0.5, offset=0.5 で再センタ結果 -0.5 → 0.25 へ写像し、対称な input=+0.6 と比較する。
            var fn = new AnalogMappingFunction(
                deadZone: 0.2f, scale: 0.5f, offset: 0.5f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            float positive = AnalogMappingEvaluator.Evaluate(fn, 0.6f);
            float negative = AnalogMappingEvaluator.Evaluate(fn, -0.6f);

            // 対称性: positive=0.75, negative=0.25 → 和が 1.0
            Assert.AreEqual(0.75f, positive, Tolerance);
            Assert.AreEqual(0.25f, negative, Tolerance);
            Assert.AreEqual(1.0f, positive + negative, Tolerance,
                "再センタは sign を保持し、左右対称な入力は中点を挟んで対称な出力になる");
        }

        [Test]
        public void Evaluate_DeadZoneZero_PassesInputUnchanged()
        {
            // deadZone=0 の場合は入力をそのまま下流に流す
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.42f);

            Assert.AreEqual(0.42f, result, Tolerance);
        }

        [Test]
        public void Evaluate_DeadZoneInside_ScaleZero_StaysZero()
        {
            // design 言及: scale=0, offset=0 構成では結果も 0 のまま
            var fn = new AnalogMappingFunction(
                deadZone: 0.1f, scale: 0f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.05f);

            Assert.AreEqual(0f, result);
        }

        // --- Stage 2: scale ---

        [Test]
        public void Evaluate_ScaleAppliedAfterDeadZone()
        {
            // deadZone=0, scale=2, input=0.3 → 0.3 * 2 = 0.6 → curve(Linear, 0.6) = 0.6
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 2f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.3f);

            Assert.AreEqual(0.6f, result, Tolerance);
        }

        [Test]
        public void Evaluate_ScaleZero_OutputIsOffsetOnly()
        {
            // scale=0 のとき入力に依存せず offset のみが残る
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 0f, offset: 0.7f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.5f);

            Assert.AreEqual(0.7f, result, Tolerance);
        }

        // --- Stage 3: offset ---

        [Test]
        public void Evaluate_OffsetAppliedAfterScale()
        {
            // input=0.2, scale=2, offset=0.1 → 0.2*2 + 0.1 = 0.5
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 2f, offset: 0.1f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.2f);

            Assert.AreEqual(0.5f, result, Tolerance);
        }

        // --- Stage 4: curve ---

        [Test]
        public void Evaluate_CurveLinear_DelegatesToTransitionCalculator()
        {
            // Linear カーブは TransitionCalculator に委譲され bit-exact に一致
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float expected = TransitionCalculator.Evaluate(TransitionCurve.Linear, t);
                float actual = AnalogMappingEvaluator.Evaluate(fn, t);
                Assert.AreEqual(expected, actual, Tolerance, $"t={t}");
            }
        }

        [Test]
        public void Evaluate_CurveEaseIn_DelegatesToTransitionCalculator()
        {
            var curve = new TransitionCurve(TransitionCurveType.EaseIn);
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: curve, invert: false,
                min: 0f, max: 1f);

            float expected = TransitionCalculator.Evaluate(curve, 0.5f);
            float actual = AnalogMappingEvaluator.Evaluate(fn, 0.5f);

            Assert.AreEqual(expected, actual, Tolerance);
        }

        [Test]
        public void Evaluate_CurveCustom_BitExactWithTransitionCalculator()
        {
            // Custom カーブの Hermite 評価が TransitionCalculator と bit-exact で一致
            var keys = new[]
            {
                new CurveKeyFrame(0f, 0f, 0f, 0f),
                new CurveKeyFrame(0.5f, 1f, 0f, 0f),
                new CurveKeyFrame(1f, 0.2f, 0f, 0f)
            };
            var curve = new TransitionCurve(TransitionCurveType.Custom, keys);

            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: curve, invert: false,
                min: -1f, max: 2f);

            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float expected = TransitionCalculator.Evaluate(curve, t);
                float actual = AnalogMappingEvaluator.Evaluate(fn, t);
                // bit-exact (== 比較) を要求
                Assert.AreEqual(expected, actual, $"Custom curve bit-exact mismatch at t={t}");
            }
        }

        // --- Stage 5: invert ---

        [Test]
        public void Evaluate_InvertTrue_FlipsSignAfterCurve()
        {
            // invert=true は curve 評価後に符号反転
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: true,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.3f);

            Assert.AreEqual(-0.3f, result, Tolerance);
        }

        [Test]
        public void Evaluate_InvertFalse_DoesNotFlip()
        {
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.3f);

            Assert.AreEqual(0.3f, result, Tolerance);
        }

        [Test]
        public void Evaluate_InvertOnZero_StaysZero()
        {
            // 0 に invert を適用しても 0 のまま (sign-flip 仕様)
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 0f, offset: 0f,
                curve: TransitionCurve.Linear, invert: true,
                min: -1f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.5f);

            Assert.AreEqual(0f, result);
        }

        // --- Stage 6: clamp(min, max) ---

        [Test]
        public void Evaluate_InvertWithBelowMin_ClampedToMin()
        {
            // invert で負側に振れ、min より下回るケースを min にクランプ
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: true,
                min: -0.2f, max: 1f);

            // input=0.8 → Linear curve = 0.8 → invert → -0.8 → clamp to min=-0.2
            float result = AnalogMappingEvaluator.Evaluate(fn, 0.8f);

            Assert.AreEqual(-0.2f, result, Tolerance);
        }

        [Test]
        public void Evaluate_ResultAboveMax_ClampedToMax()
        {
            // scale=10, input=0.5 → 5.0 → curve(Linear, 5.0) = 1 (Clamp01) → max=0.4 で clamp to 0.4
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 10f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 0.4f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.5f);

            Assert.AreEqual(0.4f, result, Tolerance);
        }

        [Test]
        public void Evaluate_MinEqualToMax_AlwaysReturnsThatValue()
        {
            // min == max は固定値クランプ
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0.5f, max: 0.5f);

            Assert.AreEqual(0.5f, AnalogMappingEvaluator.Evaluate(fn, 0.0f));
            Assert.AreEqual(0.5f, AnalogMappingEvaluator.Evaluate(fn, 0.7f));
            Assert.AreEqual(0.5f, AnalogMappingEvaluator.Evaluate(fn, 1.0f));
        }

        // --- 適用順の総合検証 ---

        [Test]
        public void Evaluate_FullPipeline_AppliesStagesInDocumentedOrder()
        {
            // input=0.6, deadZone=0.2 → 再センタ (0.6-0.2)/0.8 = 0.5
            // scale=2 → 1.0
            // offset=-0.3 → 0.7
            // curve(Linear, 0.7) = 0.7
            // invert → -0.7
            // clamp(min=-0.5, max=1) → -0.5
            var fn = new AnalogMappingFunction(
                deadZone: 0.2f, scale: 2f, offset: -0.3f,
                curve: TransitionCurve.Linear, invert: true,
                min: -0.5f, max: 1f);

            float result = AnalogMappingEvaluator.Evaluate(fn, 0.6f);

            Assert.AreEqual(-0.5f, result, Tolerance);
        }

        // --- Hot path GC ゼロ検証 (Req 2.6 / 8.1) ---

        [Test]
        public void Evaluate_HotPath_TenThousandIterations_ZeroAllocation()
        {
            var fn = new AnalogMappingFunction(
                deadZone: 0.1f, scale: 1.5f, offset: 0.05f,
                curve: TransitionCurve.Linear, invert: false,
                min: 0f, max: 1f);

            // ウォームアップ
            float warm = 0f;
            for (int i = 0; i < 1000; i++)
            {
                warm += AnalogMappingEvaluator.Evaluate(fn, 0.5f);
            }
            // warm を使うことで JIT に最適化で消されないようにする
            Assert.IsTrue(warm >= 0f);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long monoBefore = Profiler.GetMonoUsedSizeLong();

            float acc = 0f;
            for (int i = 0; i < 10000; i++)
            {
                // 入力値を変えて分岐 (deadZone hit / not hit) 双方を通す
                float input = ((i % 21) - 10) * 0.1f;
                acc += AnalogMappingEvaluator.Evaluate(fn, input);
            }
            // acc を使う
            Assert.IsTrue(acc >= float.MinValue);

            long monoAfter = Profiler.GetMonoUsedSizeLong();
            long monoDiff = monoAfter - monoBefore;

            // GC が動いて減る方向は許容、増えていなければ alloc=0
            Assert.LessOrEqual(monoDiff, 0,
                $"hot path 10000 回で managed alloc が発生: diff={monoDiff} bytes (Req 2.6 / 8.1)");
        }

        [Test]
        public void Evaluate_HotPath_CustomCurve_ZeroAllocation()
        {
            // Custom カーブ経由でも hot path alloc=0 を確認
            var keys = new[]
            {
                new CurveKeyFrame(0f, 0f, 1f, 1f),
                new CurveKeyFrame(0.5f, 0.7f, 0.5f, 0.5f),
                new CurveKeyFrame(1f, 1f, 1f, 1f)
            };
            var curve = new TransitionCurve(TransitionCurveType.Custom, keys);
            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: curve, invert: false,
                min: 0f, max: 1f);

            // ウォームアップ：測定と同じ入力分布で EvaluateCustom の全 JIT ブランチを事前コンパイル
            // （input <= keys[0].Time の早期 return / input >= keys[last].Time の早期 return / Hermite 補間 の 3 ブランチ）
            float warm = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float input = (i % 11) * 0.1f;
                warm += AnalogMappingEvaluator.Evaluate(fn, input);
            }
            Assert.IsTrue(warm >= 0f);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long monoBefore = Profiler.GetMonoUsedSizeLong();

            float acc = 0f;
            for (int i = 0; i < 10000; i++)
            {
                float input = (i % 11) * 0.1f;
                acc += AnalogMappingEvaluator.Evaluate(fn, input);
            }
            Assert.IsTrue(acc >= float.MinValue);

            long monoAfter = Profiler.GetMonoUsedSizeLong();
            long monoDiff = monoAfter - monoBefore;

            // Mono ヒープページノイズ許容 65536 bytes（既存 OscControllerBlendingIntegrationTests と同基準）
            Assert.LessOrEqual(monoDiff, 65536,
                $"Custom curve hot path 10000 回で managed alloc がページノイズ許容 (65536 bytes) を超過: diff={monoDiff} bytes");
        }
    }
}
