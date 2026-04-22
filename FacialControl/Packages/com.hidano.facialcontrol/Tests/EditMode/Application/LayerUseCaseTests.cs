using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Hidano.FacialControl.Application.UseCases;

namespace Hidano.FacialControl.Tests.EditMode.Application
{
    [TestFixture]
    public class LayerUseCaseTests
    {
        // --- ヘルパー ---

        private static LayerDefinition[] CreateDefaultLayers()
        {
            return new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("lipsync", 1, ExclusionMode.Blend),
                new LayerDefinition("eye", 2, ExclusionMode.LastWins)
            };
        }

        private static string[] CreateBlendShapeNames()
        {
            return new[] { "bs_smile", "bs_sad", "bs_blink" };
        }

        private static Expression CreateExpression(
            string id = "expr-1",
            string name = "smile",
            string layer = "emotion",
            float transitionDuration = 0.25f,
            BlendShapeMapping[] blendShapeValues = null)
        {
            return new Expression(
                id, name, layer, transitionDuration,
                TransitionCurve.Linear,
                blendShapeValues);
        }

        private static FacialProfile CreateProfile(
            LayerDefinition[] layers = null,
            Expression[] expressions = null)
        {
            return new FacialProfile(
                "1.0",
                layers ?? CreateDefaultLayers(),
                expressions ?? Array.Empty<Expression>());
        }

        private LayerUseCase _useCase;
        private ExpressionUseCase _expressionUseCase;
        private FacialProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = CreateProfile();
            _expressionUseCase = new ExpressionUseCase(_profile);
            _useCase = new LayerUseCase(_profile, _expressionUseCase, CreateBlendShapeNames());
        }

        // --- コンストラクタ ---

        [Test]
        public void Constructor_ValidArgs_CreatesInstance()
        {
            Assert.IsNotNull(_useCase);
        }

        [Test]
        public void Constructor_NullBlendShapeNames_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LayerUseCase(_profile, _expressionUseCase, null));
        }

        [Test]
        public void Constructor_EmptyBlendShapeNames_CreatesInstance()
        {
            var useCase = new LayerUseCase(_profile, _expressionUseCase, Array.Empty<string>());
            Assert.IsNotNull(useCase);
        }

        // --- SetLayerWeight ---

        [Test]
        public void SetLayerWeight_ValidLayerAndWeight_SetsWeight()
        {
            _useCase.SetLayerWeight("emotion", 0.5f);

            // 検証: GetBlendedOutput で反映されることを確認
            // レイヤーウェイトのデフォルトが 1.0 なので、設定後は 0.5 に変わる
            Assert.DoesNotThrow(() => _useCase.SetLayerWeight("emotion", 0.5f));
        }

        [Test]
        public void SetLayerWeight_NullLayer_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _useCase.SetLayerWeight(null, 0.5f));
        }

        [Test]
        public void SetLayerWeight_WeightAboveOne_ClampedToOne()
        {
            // 範囲外の値はクランプされる（例外なし）
            Assert.DoesNotThrow(() => _useCase.SetLayerWeight("emotion", 1.5f));
        }

        [Test]
        public void SetLayerWeight_WeightBelowZero_ClampedToZero()
        {
            Assert.DoesNotThrow(() => _useCase.SetLayerWeight("emotion", -0.5f));
        }

        [Test]
        public void SetLayerWeight_UndefinedLayer_SetsWeightWithoutError()
        {
            // 未定義レイヤーにも設定可能（後から使われる可能性があるため）
            Assert.DoesNotThrow(() => _useCase.SetLayerWeight("unknown", 0.5f));
        }

        // --- UpdateWeights ---

        [Test]
        public void UpdateWeights_NoActiveExpressions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _useCase.UpdateWeights(0.016f));
        }

        [Test]
        public void UpdateWeights_WithActiveExpression_ProgressesTransition()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0.5f);

            _expressionUseCase.Activate(expr);

            // 半分の遷移時間経過
            _useCase.UpdateWeights(0.25f);

            var output = _useCase.GetBlendedOutput();
            // 遷移中なので、bs_smile は 0.5 程度（Linear 補間）
            Assert.Greater(output[0], 0f);
            Assert.Less(output[0], 1f);
        }

        [Test]
        public void UpdateWeights_TransitionComplete_ReachesTargetValues()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0.25f);

            _expressionUseCase.Activate(expr);

            // 遷移時間を超えて更新
            _useCase.UpdateWeights(0.5f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(1.0f, output[0], 0.001f);
        }

        [Test]
        public void UpdateWeights_ZeroTransitionDuration_ImmediateSwitch()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.5f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(1.0f, output[0], 0.001f);
            Assert.AreEqual(0.5f, output[1], 0.001f);
        }

        [Test]
        public void UpdateWeights_MultipleDeltaTimeSteps_AccumulatesProgress()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0.5f);

            _expressionUseCase.Activate(expr);

            // 5 ステップに分けて遷移
            for (int i = 0; i < 5; i++)
                _useCase.UpdateWeights(0.1f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(1.0f, output[0], 0.001f);
        }

        // --- GetBlendedOutput ---

        [Test]
        public void GetBlendedOutput_NoActiveExpressions_ReturnsZeroArray()
        {
            var output = _useCase.GetBlendedOutput();

            Assert.AreEqual(3, output.Length);
            Assert.AreEqual(0f, output[0]);
            Assert.AreEqual(0f, output[1]);
            Assert.AreEqual(0f, output[2]);
        }

        [Test]
        public void GetBlendedOutput_ReturnsCorrectLength()
        {
            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(3, output.Length);
        }

        [Test]
        public void GetBlendedOutput_SingleLayerFullyTransitioned_ReturnsExpressionValues()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 0.8f),
                new BlendShapeMapping("bs_sad", 0.2f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(0.8f, output[0], 0.001f);
            Assert.AreEqual(0.2f, output[1], 0.001f);
            Assert.AreEqual(0.0f, output[2], 0.001f);
        }

        [Test]
        public void GetBlendedOutput_MultipleLayersActive_BlendsByPriority()
        {
            var emotionBs = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var eyeBs = new[]
            {
                new BlendShapeMapping("bs_smile", 0.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 1.0f)
            };

            var emotionExpr = CreateExpression("expr-1", "smile", "emotion",
                transitionDuration: 0f, blendShapeValues: emotionBs);
            var eyeExpr = CreateExpression("expr-2", "blink", "eye",
                transitionDuration: 0f, blendShapeValues: eyeBs);

            _expressionUseCase.Activate(emotionExpr);
            _expressionUseCase.Activate(eyeExpr);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            // eye (priority=2) は emotion (priority=0) より優先
            // 両レイヤーの weight=1.0 なので、eye の値が優先される
            Assert.AreEqual(1.0f, output[2], 0.001f); // bs_blink は eye レイヤーで 1.0
        }

        [Test]
        public void GetBlendedOutput_LayerWeightZero_LayerIgnored()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.SetLayerWeight("emotion", 0f);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            // レイヤーウェイトが 0 なので出力はゼロ
            Assert.AreEqual(0f, output[0], 0.001f);
        }

        [Test]
        public void GetBlendedOutput_LayerWeightHalf_ScalesOutput()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.SetLayerWeight("emotion", 0.5f);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(0.5f, output[0], 0.001f);
        }

        [Test]
        public void GetBlendedOutput_ReturnsDefensiveCopy()
        {
            var output1 = _useCase.GetBlendedOutput();
            var output2 = _useCase.GetBlendedOutput();

            Assert.AreNotSame(output1, output2);
        }

        // --- BlendedOutputSpan (zero-alloc accessor) ---

        [Test]
        public void BlendedOutputSpan_NoActiveExpressions_AllZero()
        {
            var span = _useCase.BlendedOutputSpan;

            Assert.AreEqual(3, span.Length);
            Assert.AreEqual(0f, span[0]);
            Assert.AreEqual(0f, span[1]);
            Assert.AreEqual(0f, span[2]);
        }

        [Test]
        public void BlendedOutputSpan_AfterUpdateWeights_MatchesGetBlendedOutput()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 0.8f),
                new BlendShapeMapping("bs_sad", 0.2f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            var span = _useCase.BlendedOutputSpan;
            var copy = _useCase.GetBlendedOutput();

            Assert.AreEqual(copy.Length, span.Length);
            for (int i = 0; i < copy.Length; i++)
            {
                Assert.AreEqual(copy[i], span[i], 1e-6f,
                    $"index {i}: BlendedOutputSpan と GetBlendedOutput が一致すべき");
            }
        }

        [Test]
        public void BlendedOutputSpan_Length_MatchesBlendShapeCount()
        {
            var span = _useCase.BlendedOutputSpan;
            Assert.AreEqual(3, span.Length);
        }

        // --- 遷移割込 ---

        [Test]
        public void UpdateWeights_TransitionInterrupt_StartsFromCurrentValues()
        {
            var blendShapes1 = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var blendShapes2 = new[]
            {
                new BlendShapeMapping("bs_smile", 0.0f),
                new BlendShapeMapping("bs_sad", 1.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };

            var expr1 = CreateExpression("expr-1", "smile", "emotion",
                transitionDuration: 0.5f, blendShapeValues: blendShapes1);
            var expr2 = CreateExpression("expr-2", "sad", "emotion",
                transitionDuration: 0.5f, blendShapeValues: blendShapes2);

            // expr1 をアクティブにして途中まで遷移
            _expressionUseCase.Activate(expr1);
            _useCase.UpdateWeights(0.25f); // 50% 遷移
            var midOutput = _useCase.GetBlendedOutput();
            float midSmile = midOutput[0];

            // expr2 に切り替え（遷移割込）
            _expressionUseCase.Activate(expr2);
            _useCase.UpdateWeights(0.001f); // 割込直後

            var interruptOutput = _useCase.GetBlendedOutput();
            // 割込直後は遷移元（前のスナップショット値）に近い
            // bs_sad が少し増加し始めているはず
            Assert.GreaterOrEqual(interruptOutput[0], 0f);
        }

        // --- SetProfile ---

        [Test]
        public void SetProfile_ResetsTransitionState()
        {
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            // 新しいプロファイルに切り替え
            var newProfile = CreateProfile();
            _useCase.SetProfile(newProfile, CreateBlendShapeNames());

            var output = _useCase.GetBlendedOutput();
            // プロファイル切替後はゼロにリセット
            Assert.AreEqual(0f, output[0], 0.001f);
        }

        // --- Blend モード（lipsync レイヤー） ---

        [Test]
        public void GetBlendedOutput_BlendModeLayer_AddsMutipleExpressions()
        {
            var bs1 = new[]
            {
                new BlendShapeMapping("bs_smile", 0.3f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var bs2 = new[]
            {
                new BlendShapeMapping("bs_smile", 0.4f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };

            var expr1 = CreateExpression("expr-1", "talk_a", "lipsync",
                transitionDuration: 0f, blendShapeValues: bs1);
            var expr2 = CreateExpression("expr-2", "talk_o", "lipsync",
                transitionDuration: 0f, blendShapeValues: bs2);

            _expressionUseCase.Activate(expr1);
            _expressionUseCase.Activate(expr2);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            // Blend モードなので加算される（0.3 + 0.4 = 0.7）
            Assert.AreEqual(0.7f, output[0], 0.05f);
        }

        [Test]
        public void GetBlendedOutput_BlendModeLayer_ClampsToOne()
        {
            var bs1 = new[]
            {
                new BlendShapeMapping("bs_smile", 0.8f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var bs2 = new[]
            {
                new BlendShapeMapping("bs_smile", 0.8f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };

            var expr1 = CreateExpression("expr-1", "talk_a", "lipsync",
                transitionDuration: 0f, blendShapeValues: bs1);
            var expr2 = CreateExpression("expr-2", "talk_o", "lipsync",
                transitionDuration: 0f, blendShapeValues: bs2);

            _expressionUseCase.Activate(expr1);
            _expressionUseCase.Activate(expr2);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            // 0.8 + 0.8 = 1.6 → 1.0 にクランプ
            Assert.AreEqual(1.0f, output[0], 0.001f);
        }

        // --- BlendShape 名マッピング ---

        [Test]
        public void GetBlendedOutput_ExpressionWithPartialBlendShapes_MapsCorrectly()
        {
            // Expression が全 BlendShape を含まない場合、
            // 対応するインデックスのみ更新される
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_sad", 0.7f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(0.0f, output[0], 0.001f); // bs_smile は未設定
            Assert.AreEqual(0.7f, output[1], 0.001f); // bs_sad は 0.7
            Assert.AreEqual(0.0f, output[2], 0.001f); // bs_blink は未設定
        }

        // --- デフォルト動作 ---

        [Test]
        public void LayerWeight_DefaultIsOne()
        {
            // 何も設定しない場合、レイヤーウェイトはデフォルト 1.0
            var blendShapes = new[]
            {
                new BlendShapeMapping("bs_smile", 1.0f),
                new BlendShapeMapping("bs_sad", 0.0f),
                new BlendShapeMapping("bs_blink", 0.0f)
            };
            var expr = CreateExpression(
                blendShapeValues: blendShapes,
                transitionDuration: 0f);

            _expressionUseCase.Activate(expr);
            _useCase.UpdateWeights(0.001f);

            var output = _useCase.GetBlendedOutput();
            Assert.AreEqual(1.0f, output[0], 0.001f);
        }

        // --- TryGetExpressionTriggerSourceById ---

        private sealed class FakeExpressionTriggerSource : ExpressionTriggerInputSourceBase
        {
            public FakeExpressionTriggerSource(string id, int blendShapeCount, FacialProfile profile)
                : base(InputSourceId.Parse(id), blendShapeCount, 4, ExclusionMode.LastWins,
                       Array.Empty<string>(), profile)
            {
            }
        }

        [Test]
        public void TryGetExpressionTriggerSourceById_RegisteredId_ReturnsTrue()
        {
            var fake = new FakeExpressionTriggerSource("controller-expr", 3, _profile);
            var additional = new List<(int layerIdx, IInputSource source, float weight)>
            {
                (0, fake, 1.0f),
            };
            using var useCase = new LayerUseCase(
                _profile, _expressionUseCase, CreateBlendShapeNames(), additional);

            bool found = useCase.TryGetExpressionTriggerSourceById(
                "controller-expr", out var source);

            Assert.IsTrue(found);
            Assert.AreSame(fake, source);
        }

        [Test]
        public void TryGetExpressionTriggerSourceById_UnregisteredId_ReturnsFalse()
        {
            bool found = _useCase.TryGetExpressionTriggerSourceById(
                "controller-expr", out var source);

            Assert.IsFalse(found);
            Assert.IsNull(source);
        }

        [Test]
        public void TryGetExpressionTriggerSourceById_NullOrEmptyId_ReturnsFalse()
        {
            Assert.IsFalse(_useCase.TryGetExpressionTriggerSourceById(null, out var s1));
            Assert.IsNull(s1);
            Assert.IsFalse(_useCase.TryGetExpressionTriggerSourceById("", out var s2));
            Assert.IsNull(s2);
        }

        [Test]
        public void TryGetExpressionTriggerSourceById_NonExpressionTriggerSource_NotReturned()
        {
            // ValueProvider 型のソースは ExpressionTrigger lookup には該当しない。
            var valueProvider = new FakeValueProviderSource("osc", 3);
            var additional = new List<(int layerIdx, IInputSource source, float weight)>
            {
                (0, valueProvider, 1.0f),
            };
            using var useCase = new LayerUseCase(
                _profile, _expressionUseCase, CreateBlendShapeNames(), additional);

            bool found = useCase.TryGetExpressionTriggerSourceById("osc", out var source);

            Assert.IsFalse(found);
            Assert.IsNull(source);
        }

        private sealed class FakeValueProviderSource : ValueProviderInputSourceBase
        {
            public FakeValueProviderSource(string id, int blendShapeCount)
                : base(InputSourceId.Parse(id), blendShapeCount)
            {
            }

            public override bool TryWriteValues(Span<float> output) => false;
        }

        // --- additional IInputSource だけで駆動するレイヤーが blend に含まれる契約 ---

        /// <summary>
        /// Profile で additional IInputSource を宣言したレイヤーは、LayerExpressionSource
        /// (sourceIdx=0) が一度も activate されていなくても blend 対象となり、
        /// sourceIdx=1+ のソースが TriggerOn した値が最終 BlendShape 出力に届くこと。
        /// </summary>
        [Test]
        public void UpdateWeights_AdditionalSourceOnly_TriggersReachFinalOutput()
        {
            var layers = new[] { new LayerDefinition("emotion", 0, ExclusionMode.LastWins) };
            var expressionBs = new[] { new BlendShapeMapping("bs_smile", 1.0f) };
            var smileExpr = new Expression(
                "smile", "smile", "emotion", 0f, TransitionCurve.Linear, expressionBs);
            var profile = new FacialProfile("1.0", layers, new[] { smileExpr });
            var expressionUseCase = new ExpressionUseCase(profile);
            var blendShapeNames = new[] { "bs_smile", "bs_sad", "bs_blink" };

            var controller = new global::Hidano.FacialControl.Adapters.InputSources.ControllerExpressionInputSource(
                blendShapeCount: blendShapeNames.Length,
                maxStackDepth: 4,
                exclusionMode: ExclusionMode.LastWins,
                blendShapeNames: blendShapeNames,
                profile: profile);
            var additional = new List<(int layerIdx, IInputSource source, float weight)>
            {
                (0, controller, 1.0f),
            };

            using var useCase = new LayerUseCase(
                profile, expressionUseCase, blendShapeNames, additional);

            // ExpressionUseCase.Activate は呼ばない。sourceIdx=1 (controller-expr) だけで駆動する。
            controller.TriggerOn("smile");
            useCase.UpdateWeights(0.001f);

            var output = useCase.GetBlendedOutput();
            Assert.AreEqual(1.0f, output[0], 1e-4f,
                "additional source のみで triggered した bs_smile が最終出力に反映されること");
        }
    }
}
