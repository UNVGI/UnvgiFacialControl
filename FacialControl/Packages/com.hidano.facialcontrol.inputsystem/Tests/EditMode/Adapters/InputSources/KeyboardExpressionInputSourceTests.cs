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
    /// <see cref="KeyboardExpressionInputSource"/> の EditMode 契約テスト (tasks.md 6.4)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>予約 id <c>keyboard-expr</c> と <see cref="InputSourceType.ExpressionTrigger"/> を持つ。</item>
    ///   <item>Category プロパティが <see cref="InputSourceCategory.Keyboard"/> を返す。</item>
    ///   <item>擬似ルータで Keyboard カテゴリの <c>TriggerOn("smile")</c> を受けると
    ///     <c>TryWriteValues</c> が smile の BlendShape 値を書込む。</item>
    ///   <item>Controller カテゴリ由来の入力では本アダプタは反応しない (D-13 独立性)。</item>
    ///   <item>Controller / Keyboard を同レイヤーに並べ smile / angry を同時トリガー →
    ///     各アダプタの独立スタックがそれぞれ独立に遷移し、
    ///     両アダプタの <c>TryWriteValues</c> 出力の加重和がその時点の BlendShape 値と一致する
    ///     (D-12 意図挙動)。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class KeyboardExpressionInputSourceTests
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

        private static KeyboardExpressionInputSource CreateKeyboardSource(
            int maxStackDepth = 8,
            ExclusionMode exclusionMode = ExclusionMode.LastWins)
        {
            return new KeyboardExpressionInputSource(
                blendShapeCount: BlendShapeNames.Length,
                maxStackDepth: maxStackDepth,
                exclusionMode: exclusionMode,
                blendShapeNames: BlendShapeNames,
                profile: BuildProfile());
        }

        private static ControllerExpressionInputSource CreateControllerSource(
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
        public void Id_IsReservedKeyboardExpr()
        {
            var source = CreateKeyboardSource();

            Assert.AreEqual(KeyboardExpressionInputSource.ReservedId, source.Id);
            Assert.AreEqual("keyboard-expr", source.Id);
        }

        [Test]
        public void Type_IsExpressionTriggerViaIInputSource()
        {
            IInputSource source = CreateKeyboardSource();

            Assert.AreEqual(InputSourceType.ExpressionTrigger, source.Type);
        }

        [Test]
        public void Category_IsKeyboard()
        {
            var source = CreateKeyboardSource();

            Assert.AreEqual(InputSourceCategory.Keyboard, source.Category);
        }

        [Test]
        public void BlendShapeCount_MatchesConstructorArgument()
        {
            var source = CreateKeyboardSource();

            Assert.AreEqual(BlendShapeNames.Length, source.BlendShapeCount);
        }

        [Test]
        public void TriggerOn_Smile_AfterFullTick_WritesSmileBlendShape()
        {
            var source = CreateKeyboardSource();

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
        public void FakeInputSystemAdapter_KeyboardCategoryBinding_RoutesTriggerToAdapter()
        {
            var source = CreateKeyboardSource();
            var router = new FakeInputBindingRouter();
            router.Register(source.Category, source.TriggerOn, source.TriggerOff);

            router.OnBindingPerformed(InputSourceCategory.Keyboard, "smile");
            source.Tick(1.0f);

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsTrue(wrote, "Keyboard カテゴリの入力では Keyboard アダプタが反応する");
            Assert.AreEqual(1.0f, buffer[0], 1e-5f);
        }

        [Test]
        public void FakeInputSystemAdapter_ControllerCategoryBinding_DoesNotRouteToKeyboardAdapter()
        {
            var source = CreateKeyboardSource();
            var router = new FakeInputBindingRouter();
            router.Register(source.Category, source.TriggerOn, source.TriggerOff);

            router.OnBindingPerformed(InputSourceCategory.Controller, "smile");
            source.Tick(1.0f);

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsFalse(wrote,
                "Controller カテゴリの入力では Keyboard アダプタは反応しない (空スタック)。");
        }

        [Test]
        public void TriggerOff_AfterTriggerOn_RemovesFromStack()
        {
            var source = CreateKeyboardSource();

            source.TriggerOn("smile");
            source.Tick(1.0f);

            source.TriggerOff("smile");
            source.Tick(1.0f); // フェードアウト完了

            var buffer = new float[BlendShapeNames.Length];
            bool wrote = source.TryWriteValues(buffer);

            Assert.IsFalse(wrote, "TriggerOff でスタックが空になり遷移完了すれば false を返す。");
        }

        /// <summary>
        /// D-13 独立性: 同レイヤーに Controller / Keyboard アダプタを並べ、
        /// 片方に smile、もう片方に angry をトリガーした場合、
        /// 各アダプタは独立した Expression スタックと TransitionCalculator 状態を
        /// 保持しており、互いに干渉しない (Req 1.6 / 1.8)。
        /// </summary>
        [Test]
        public void SameLayer_ControllerAndKeyboard_HaveIndependentExpressionStacks()
        {
            var keyboard = CreateKeyboardSource();
            var controller = CreateControllerSource();

            keyboard.TriggerOn("smile");
            controller.TriggerOn("angry");

            keyboard.Tick(1.0f);
            controller.Tick(1.0f);

            var kbBuffer = new float[BlendShapeNames.Length];
            var ctBuffer = new float[BlendShapeNames.Length];
            bool kbWrote = keyboard.TryWriteValues(kbBuffer);
            bool ctWrote = controller.TryWriteValues(ctBuffer);

            Assert.IsTrue(kbWrote);
            Assert.IsTrue(ctWrote);

            // keyboard は smile のみ、controller は angry のみを駆動している。
            Assert.AreEqual(1.0f, kbBuffer[0], 1e-5f, "keyboard smile");
            Assert.AreEqual(0.0f, kbBuffer[1], 1e-5f, "keyboard angry 寄与なし");
            Assert.AreEqual(0.0f, ctBuffer[0], 1e-5f, "controller smile 寄与なし");
            Assert.AreEqual(1.0f, ctBuffer[1], 1e-5f, "controller angry");
        }

        /// <summary>
        /// D-12 意図挙動: Controller / Keyboard を同レイヤーに並べて smile / angry を
        /// 同時トリガーした場合、両アダプタの TryWriteValues 出力を加重和すると
        /// 最終 BlendShape 値 (ここでは weight=1.0 ずつ、上限 1.0 に silent clamp) が得られる。
        /// Aggregator 本体を使わず手計算で検証し、アダプタ側の契約のみを対象にする。
        /// </summary>
        [Test]
        public void SameLayer_ControllerAndKeyboard_OutputsCombineAdditivelyViaWeightedSum()
        {
            var keyboard = CreateKeyboardSource();
            var controller = CreateControllerSource();

            keyboard.TriggerOn("smile");
            controller.TriggerOn("angry");

            keyboard.Tick(1.0f);
            controller.Tick(1.0f);

            var kbBuffer = new float[BlendShapeNames.Length];
            var ctBuffer = new float[BlendShapeNames.Length];
            keyboard.TryWriteValues(kbBuffer);
            controller.TryWriteValues(ctBuffer);

            const float kbWeight = 1.0f;
            const float ctWeight = 1.0f;
            var combined = new float[BlendShapeNames.Length];
            for (int k = 0; k < combined.Length; k++)
            {
                float v = kbWeight * kbBuffer[k] + ctWeight * ctBuffer[k];
                if (v < 0f) v = 0f;
                if (v > 1f) v = 1f;
                combined[k] = v;
            }

            Assert.AreEqual(1.0f, combined[0], 1e-5f, "smile は keyboard 由来");
            Assert.AreEqual(1.0f, combined[1], 1e-5f, "angry は controller 由来");
            Assert.AreEqual(0.0f, combined[2], 1e-5f);
            Assert.AreEqual(0.0f, combined[3], 1e-5f);
        }

        /// <summary>
        /// <see cref="InputBindingProfileSO.InputSourceCategory"/> を参照して
        /// トリガーを適切な Expression トリガー型アダプタへ振り分ける擬似ルータ。
        /// </summary>
        private sealed class FakeInputBindingRouter
        {
            private readonly Dictionary<InputSourceCategory, Action<string>> _triggerOn
                = new Dictionary<InputSourceCategory, Action<string>>();

            private readonly Dictionary<InputSourceCategory, Action<string>> _triggerOff
                = new Dictionary<InputSourceCategory, Action<string>>();

            public void Register(
                InputSourceCategory category,
                Action<string> triggerOn,
                Action<string> triggerOff)
            {
                _triggerOn[category] = triggerOn;
                _triggerOff[category] = triggerOff;
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
