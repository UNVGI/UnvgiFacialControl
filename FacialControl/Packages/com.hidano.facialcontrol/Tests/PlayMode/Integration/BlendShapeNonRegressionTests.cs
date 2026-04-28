using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Application.UseCases;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    /// <summary>
    /// Task 10.3: 既存 BlendShape パイプラインの非破壊検証（PlayMode）。
    ///
    /// BoneWriter 統合の追加によって、既存の BlendShape 関連パイプライン
    /// （<see cref="LayerUseCase"/> + <see cref="Hidano.FacialControl.Adapters.Playable.FacialControlMixer"/>
    /// + <see cref="Hidano.FacialControl.Domain.Services.LayerInputSourceAggregator"/> 系）の
    /// 出力が bit-exact で同一に保たれること、および GC alloc 予算が維持されることを検証する。
    ///
    /// _Requirements: 10.1, 10.3
    /// </summary>
    [TestFixture]
    public class BlendShapeNonRegressionTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _gameObject = null;
            }
        }

        // ================================================================
        // LayerUseCase 出力の bit-exact 非破壊性
        // （BonePoses の有無で BlendShape パイプライン出力に差が出ないこと）
        // ================================================================

        [UnityTest]
        public IEnumerator LayerUseCaseOutput_WithAndWithoutBonePoses_BitExactSame()
        {
            string[] blendShapeNames = { "smile", "blink", "frown" };

            var profileWithoutBonePoses = CreateBlendShapeProfile(includeBonePoses: false);
            var profileWithBonePoses = CreateBlendShapeProfile(includeBonePoses: true);

            // BonePose 配列の有無のみが異なる前提を確認。
            Assert.AreEqual(0, profileWithoutBonePoses.BonePoses.Length,
                "前提: 比較元プロファイルは BonePoses 空");
            Assert.Greater(profileWithBonePoses.BonePoses.Length, 0,
                "前提: 比較先プロファイルは BonePoses を含む");

            var euc1 = new ExpressionUseCase(profileWithoutBonePoses);
            var luc1 = new LayerUseCase(profileWithoutBonePoses, euc1, blendShapeNames);

            var euc2 = new ExpressionUseCase(profileWithBonePoses);
            var luc2 = new LayerUseCase(profileWithBonePoses, euc2, blendShapeNames);

            try
            {
                var smile1 = profileWithoutBonePoses.Expressions.Span[0];
                var smile2 = profileWithBonePoses.Expressions.Span[0];
                euc1.Activate(smile1);
                euc2.Activate(smile2);

                // 30 フレーム分まわして、毎フレーム bit-exact 一致を確認する。
                // 最初の数フレームは遷移補間中、残りは定常状態の両方を踏む。
                for (int frame = 0; frame < 30; frame++)
                {
                    luc1.UpdateWeights(0.016f);
                    luc2.UpdateWeights(0.016f);

                    var span1 = luc1.BlendedOutputSpan;
                    var span2 = luc2.BlendedOutputSpan;

                    Assert.AreEqual(span1.Length, span2.Length,
                        $"frame={frame}: 出力長が一致すること");

                    for (int i = 0; i < span1.Length; i++)
                    {
                        // bit-exact: 浮動小数点を整数表現で比較する（Req 10.3）。
                        int bits1 = BitConverter.SingleToInt32Bits(span1[i]);
                        int bits2 = BitConverter.SingleToInt32Bits(span2[i]);
                        Assert.AreEqual(bits1, bits2,
                            $"frame={frame}, idx={i}: BonePoses 追加で BlendShape 出力が変化しないこと " +
                            $"(without={span1[i]} 0x{bits1:X8} vs with={span2[i]} 0x{bits2:X8})");
                    }
                }
            }
            finally
            {
                luc1.Dispose();
                luc2.Dispose();
            }

            yield break;
        }

        // ================================================================
        // 遷移割込中も bit-exact が保たれること
        // ================================================================

        [UnityTest]
        public IEnumerator LayerUseCaseOutput_DuringTransitionInterrupt_BitExactSame()
        {
            string[] blendShapeNames = { "smile", "blink", "frown" };

            var profileWithoutBonePoses = CreateBlendShapeProfile(includeBonePoses: false);
            var profileWithBonePoses = CreateBlendShapeProfile(includeBonePoses: true);

            var euc1 = new ExpressionUseCase(profileWithoutBonePoses);
            var luc1 = new LayerUseCase(profileWithoutBonePoses, euc1, blendShapeNames);

            var euc2 = new ExpressionUseCase(profileWithBonePoses);
            var luc2 = new LayerUseCase(profileWithBonePoses, euc2, blendShapeNames);

            try
            {
                var smile1 = profileWithoutBonePoses.Expressions.Span[0];
                var sad1 = profileWithoutBonePoses.Expressions.Span[1];
                var smile2 = profileWithBonePoses.Expressions.Span[0];
                var sad2 = profileWithBonePoses.Expressions.Span[1];

                // 1) Smile を活性化、3 フレーム経過
                euc1.Activate(smile1);
                euc2.Activate(smile2);
                for (int frame = 0; frame < 3; frame++)
                {
                    luc1.UpdateWeights(0.016f);
                    luc2.UpdateWeights(0.016f);
                    AssertOutputsBitExact(luc1.BlendedOutputSpan, luc2.BlendedOutputSpan,
                        $"smile transition frame={frame}");
                }

                // 2) 遷移途中で Sad に切替（割込）
                euc1.Activate(sad1);
                euc2.Activate(sad2);
                for (int frame = 0; frame < 30; frame++)
                {
                    luc1.UpdateWeights(0.016f);
                    luc2.UpdateWeights(0.016f);
                    AssertOutputsBitExact(luc1.BlendedOutputSpan, luc2.BlendedOutputSpan,
                        $"interrupt transition frame={frame}");
                }
            }
            finally
            {
                luc1.Dispose();
                luc2.Dispose();
            }

            yield break;
        }

        // ================================================================
        // GC alloc 予算: BonePoses を含むプロファイルでも BlendShape 経路の
        // per-frame ヒープ確保が 0 のまま保たれること。
        //
        // 既存 <see cref="Hidano.FacialControl.Tests.PlayMode.Performance.GCAllocationTests"/>
        // と同経路 (<see cref="Hidano.FacialControl.Adapters.Playable.PlayableGraphBuilder.Build"/>
        // → <c>LayerPlayable.UpdateTransition</c>) を BonePoses 入りプロファイルで再走させ、
        // BoneWriter 統合後も BlendShape 側の hot path に新規 alloc が混入していないことを確認する
        // (Req 10.3)。
        // ================================================================

        [Test]
        public void UpdateTransition_WithBonePosesProfile_ZeroGCAllocation()
        {
            _gameObject = new GameObject("BlendShapeNonRegressionGC");
            _gameObject.AddComponent<Animator>();

            string[] blendShapeNames = CreateBlendShapeNames(52);
            var profile = CreateBlendShapeProfileWithLayer(blendShapeNames, includeBonePoses: true);

            var result = PlayableGraphBuilder.Build(
                _gameObject.GetComponent<Animator>(), profile, blendShapeNames);
            try
            {
                var behaviour = result.LayerPlayables["emotion"].GetBehaviour();
                var targetValues = new float[blendShapeNames.Length];
                for (int i = 0; i < targetValues.Length; i++)
                {
                    targetValues[i] = 0.8f;
                }
                behaviour.SetTargetExpression("expr-1", targetValues, 1.0f, TransitionCurve.Linear);

                // ウォームアップ: JIT・初回キャッシュ膨張を吸収する。
                behaviour.UpdateTransition(0.01f);

                long allocBefore = GC.GetTotalMemory(forceFullCollection: false);
                for (int frame = 0; frame < 100; frame++)
                {
                    behaviour.UpdateTransition(0.001f);
                }
                long allocAfter = GC.GetTotalMemory(forceFullCollection: false);

                long allocated = allocAfter - allocBefore;
                Assert.LessOrEqual(allocated, 0,
                    $"BonePoses 入りプロファイルで UpdateTransition の per-frame alloc が " +
                    $"検出されました: {allocated} bytes (BoneWriter 統合後も BlendShape 経路は " +
                    $"alloc 0 を維持する必要がある、Req 10.3)");
            }
            finally
            {
                result.Dispose();
            }
        }

        // ================================================================
        // FacialController 統合: BonePoses 追加で初期化・Activate 経路が破壊されない
        // ================================================================

        [UnityTest]
        public IEnumerator FacialController_WithBonePoses_InitAndActivateUnaffected()
        {
            var profile = CreateBlendShapeProfile(includeBonePoses: true);
            var controller = CreateInitializedController(profile);

            yield return null;

            Assert.IsTrue(controller.IsInitialized,
                "BonePoses を含むプロファイルで FacialController が初期化できる");
            Assert.IsTrue(controller.CurrentProfile.HasValue);
            Assert.AreEqual(profile.SchemaVersion, controller.CurrentProfile.Value.SchemaVersion);

            // Activate 経路が変わらないこと（既存 EndToEndTests / FacialControllerLifecycleTests と同等の挙動）
            var smile = profile.Expressions.Span[0];
            controller.Activate(smile);

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(1, active.Count, "Activate 後にアクティブ Expression が 1 件");
            Assert.AreEqual("Smile", active[0].Name);

            controller.Deactivate(smile);
            Assert.AreEqual(0, controller.GetActiveExpressions().Count,
                "Deactivate 後にアクティブ Expression が 0 件に戻る");
        }

        // ================================================================
        // Helpers
        // ================================================================

        private FacialController CreateInitializedController(FacialProfile profile)
        {
            _gameObject = new GameObject("BlendShapeNonRegressionTest");
            _gameObject.AddComponent<Animator>();
            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_gameObject.transform);
            meshObj.AddComponent<SkinnedMeshRenderer>();

            var controller = _gameObject.AddComponent<FacialController>();
            controller.InitializeWithProfile(profile);
            return controller;
        }

        private static FacialProfile CreateBlendShapeProfile(bool includeBonePoses)
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("eye", 1, ExclusionMode.LastWins),
            };
            var expressions = new Expression[]
            {
                new Expression("expr-smile", "Smile", "emotion", 0.25f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("smile", 1.0f),
                    }),
                new Expression("expr-sad", "Sad", "emotion", 0.15f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("frown", 0.8f),
                    }),
                new Expression("expr-blink", "Blink", "eye", 0.08f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("blink", 1.0f),
                    }),
            };

            BonePose[] bonePoses = null;
            if (includeBonePoses)
            {
                bonePoses = new BonePose[]
                {
                    new BonePose("look-right", new BonePoseEntry[]
                    {
                        new BonePoseEntry("LeftEye", 0f, 25f, 0f),
                        new BonePoseEntry("RightEye", 0f, 25f, 0f),
                    }),
                };
            }

            return new FacialProfile(
                schemaVersion: "1.0.0",
                layers: layers,
                expressions: expressions,
                rendererPaths: null,
                layerInputSources: null,
                bonePoses: bonePoses);
        }

        private static FacialProfile CreateBlendShapeProfileWithLayer(string[] blendShapeNames, bool includeBonePoses)
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            var bsValues = new BlendShapeMapping[blendShapeNames.Length];
            for (int i = 0; i < blendShapeNames.Length; i++)
            {
                bsValues[i] = new BlendShapeMapping(blendShapeNames[i], 0.5f);
            }
            var expressions = new Expression[]
            {
                new Expression("expr-1", "All", "emotion", 0.25f, TransitionCurve.Linear, bsValues),
            };

            BonePose[] bonePoses = null;
            if (includeBonePoses)
            {
                bonePoses = new BonePose[]
                {
                    new BonePose("p0", new BonePoseEntry[]
                    {
                        new BonePoseEntry("LeftEye", 0f, 10f, 0f),
                        new BonePoseEntry("RightEye", 0f, 10f, 0f),
                    }),
                };
            }

            return new FacialProfile(
                schemaVersion: "1.0.0",
                layers: layers,
                expressions: expressions,
                rendererPaths: null,
                layerInputSources: null,
                bonePoses: bonePoses);
        }

        private static string[] CreateBlendShapeNames(int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = $"bs_{i}";
            }
            return names;
        }

        private static void AssertOutputsBitExact(ReadOnlySpan<float> span1, ReadOnlySpan<float> span2, string label)
        {
            Assert.AreEqual(span1.Length, span2.Length, $"{label}: 出力長が一致");
            for (int i = 0; i < span1.Length; i++)
            {
                int bits1 = BitConverter.SingleToInt32Bits(span1[i]);
                int bits2 = BitConverter.SingleToInt32Bits(span2[i]);
                Assert.AreEqual(bits1, bits2,
                    $"{label}, idx={i}: bit-exact 一致せず " +
                    $"(without={span1[i]} 0x{bits1:X8} vs with={span2[i]} 0x{bits2:X8})");
            }
        }
    }
}
