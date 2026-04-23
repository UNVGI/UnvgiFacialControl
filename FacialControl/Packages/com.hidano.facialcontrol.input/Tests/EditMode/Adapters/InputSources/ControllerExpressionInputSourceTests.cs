using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// <see cref="ControllerExpressionInputSource"/> の EditMode 契約テスト (tasks.md 6.3)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>予約 id <c>controller-expr</c> と <see cref="InputSourceType.ExpressionTrigger"/> を持つ。</item>
    ///   <item>Category プロパティが <see cref="InputSourceCategory.Controller"/> を返す。</item>
    ///   <item>擬似 InputSystemAdapter から <c>TriggerOn("smile")</c> を受けると
    ///     <c>TryWriteValues</c> が smile の BlendShape 値を書込む。</item>
    ///   <item>擬似ルータが Category を参照してディスパッチした場合、
    ///     Keyboard カテゴリ由来の入力では本アダプタが反応しない。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class ControllerExpressionInputSourceTests
    {
        private static readonly string[] BlendShapeNames = { "smile", "angry", "sad", "surprised" };

        private static FacialProfile BuildProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", priority: 0, ExclusionMode.LastWins),
            };

            var expressions = new[]
            {
                new Expression(
                    id: "smile",
                    name: "smile",
                    layer: "emotion",
                    transitionDuration: 0.2f,
                    transitionCurve: TransitionCurve.Linear,
                    blendShapeValues: new[]
                    {
                        new BlendShapeMapping("smile", 1.0f),
                    }),
                new Expression(
                    id: "angry",
                    name: "angry",
                    layer: "emotion",
                    transitionDuration: 0.2f,
                    transitionCurve: TransitionCurve.Linear,
                    blendShapeValues: new[]
                    {
                        new BlendShapeMapping("angry", 1.0f),
                    }),
            };

            return new FacialProfile("1.0", layers: layers, expressions: expressions);
        }

        private static ControllerExpressionInputSource CreateSource(
            int maxStackDepth = 8,
            ExclusionMode exclusionMode = ExclusionMode.LastWins)
        {
            return new ControllerExpressionInputSource(
                blendShapeCount: BlendShapeNames.Length,
                maxStackDepth: maxStackDepth,
                exclusionMode: exclusionMode,
                blendShapeNames: BlendShapeNames,
                profile: BuildProfile());
        }

        [Test]
        public void Id_IsReservedControllerExpr()
        {
            var source = CreateSource();

            Assert.AreEqual(ControllerExpressionInputSource.ReservedId, source.Id);
            Assert.AreEqual("controller-expr", source.Id);
        }

        [Test]
        public void Type_IsExpressionTriggerViaIInputSource()
        {
            IInputSource source = CreateSource();

            Assert.AreEqual(InputSourceType.ExpressionTrigger, source.Type);
        }

        [Test]
        public void Category_IsController()
        {
            var source = CreateSource();

            Assert.AreEqual(InputSourceCategory.Controller, source.Category);
        }

        [Test]
        public void BlendShapeCount_MatchesConstructorArgument()
        {
            var source = CreateSource();

            Assert.AreEqual(BlendShapeNames.Length, source.BlendShapeCount);
        }

        [Test]
        public void TriggerOn_Smile_AfterFullTick_WritesSmileBlendShape()
        {
            var source = CreateSource();

            source.TriggerOn("smile");
            source.Tick(1.0f); // 遷移時間 (0.2s) を超える

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsTrue(wrote);
            Assert.AreEqual(1.0f, buffer[0], 1e-5f, "smile");
            Assert.AreEqual(0.0f, buffer[1], 1e-5f, "angry");
            Assert.AreEqual(0.0f, buffer[2], 1e-5f, "sad");
            Assert.AreEqual(0.0f, buffer[3], 1e-5f, "surprised");
        }

        [Test]
        public void FakeInputSystemAdapter_ControllerCategoryBinding_RoutesTriggerToAdapter()
        {
            var source = CreateSource();
            var router = new FakeInputBindingRouter();
            router.Register(source);

            // コントローラ由来の binding が smile を発火 → Controller アダプタが反応する。
            router.OnBindingPerformed(InputSourceCategory.Controller, "smile");
            source.Tick(1.0f);

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsTrue(wrote, "Controller カテゴリの入力では Controller アダプタが反応する");
            Assert.AreEqual(1.0f, buffer[0], 1e-5f);
        }

        [Test]
        public void FakeInputSystemAdapter_KeyboardCategoryBinding_DoesNotRouteToControllerAdapter()
        {
            var source = CreateSource();
            var router = new FakeInputBindingRouter();
            router.Register(source);

            // キーボード由来の binding が smile を発火 → Controller アダプタは反応しない。
            router.OnBindingPerformed(InputSourceCategory.Keyboard, "smile");
            source.Tick(1.0f);

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsFalse(wrote,
                "Keyboard カテゴリの入力では Controller アダプタは反応しない (空スタック)。");
        }

        [Test]
        public void TriggerOff_AfterTriggerOn_RemovesFromStack()
        {
            var source = CreateSource();

            source.TriggerOn("smile");
            source.Tick(1.0f);

            source.TriggerOff("smile");
            source.Tick(1.0f); // フェードアウト完了

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsFalse(wrote, "TriggerOff でスタックが空になり遷移完了すれば false を返す。");
        }

        /// <summary>
        /// <see cref="InputBindingProfileSO.InputSourceCategory"/> を参照して
        /// トリガーを適切な Expression トリガー型アダプタへ振り分ける擬似ルータ。
        /// 本 Fake は <c>FacialInputBinder</c> が本来行う責務を EditMode 上で
        /// 最小限シミュレートするために用意する。
        /// </summary>
        private sealed class FakeInputBindingRouter
        {
            private readonly Dictionary<InputSourceCategory, Action<string>> _triggerOn
                = new Dictionary<InputSourceCategory, Action<string>>();

            private readonly Dictionary<InputSourceCategory, Action<string>> _triggerOff
                = new Dictionary<InputSourceCategory, Action<string>>();

            public void Register(ControllerExpressionInputSource source)
            {
                _triggerOn[source.Category] = source.TriggerOn;
                _triggerOff[source.Category] = source.TriggerOff;
            }

            public void OnBindingPerformed(InputSourceCategory category, string expressionId)
            {
                if (_triggerOn.TryGetValue(category, out var handler))
                {
                    handler(expressionId);
                }
            }

            public void OnBindingCanceled(InputSourceCategory category, string expressionId)
            {
                if (_triggerOff.TryGetValue(category, out var handler))
                {
                    handler(expressionId);
                }
            }
        }
    }
}
