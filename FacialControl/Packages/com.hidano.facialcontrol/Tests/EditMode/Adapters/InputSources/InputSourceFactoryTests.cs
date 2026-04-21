using NUnit.Framework;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Tests.Shared;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// <see cref="InputSourceFactory"/> の EditMode 契約テスト (tasks.md 7.5)。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>予約 id ごとに DTO 型を持ち、<see cref="InputSourceFactory.TryDeserializeOptions"/>
    ///     が <see cref="UnityEngine.JsonUtility.FromJson(string, System.Type)"/> 相当で
    ///     typed DTO を返す (Critical 2, Req 3.7)。</item>
    ///   <item><see cref="InputSourceFactory.TryCreate"/> が予約 id ごとに対応アダプタを返す
    ///     (Req 3.1, 5.7)。</item>
    ///   <item>未登録 id は <c>TryCreate</c> / <c>TryDeserializeOptions</c> ともに <c>null</c>
    ///     を返す (呼出側が警告 + skip、Req 3.3)。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class InputSourceFactoryTests
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
            };

            return new FacialProfile("1.0", layers: layers, expressions: expressions);
        }

        private sealed class StubLipSyncProvider : ILipSyncProvider
        {
            public void GetLipSyncValues(System.Span<float> output) { }

            public System.ReadOnlySpan<string> BlendShapeNames => System.ReadOnlySpan<string>.Empty;
        }

        private static InputSourceFactory CreateFactory(
            OscDoubleBuffer oscBuffer = null,
            ITimeProvider timeProvider = null,
            ILipSyncProvider lipSyncProvider = null)
        {
            return new InputSourceFactory(
                oscBuffer: oscBuffer,
                timeProvider: timeProvider,
                lipSyncProvider: lipSyncProvider,
                blendShapeNames: BlendShapeNames);
        }

        [Test]
        public void IsRegistered_ReservedOscId_ReturnsTrue()
        {
            var factory = CreateFactory();

            Assert.IsTrue(factory.IsRegistered(InputSourceId.Parse(OscInputSource.ReservedId)));
        }

        [Test]
        public void IsRegistered_ReservedLipSyncId_ReturnsTrue()
        {
            var factory = CreateFactory();

            Assert.IsTrue(factory.IsRegistered(InputSourceId.Parse(LipSyncInputSource.ReservedId)));
        }

        [Test]
        public void IsRegistered_ReservedControllerExprId_ReturnsTrue()
        {
            var factory = CreateFactory();

            Assert.IsTrue(factory.IsRegistered(InputSourceId.Parse(ControllerExpressionInputSource.ReservedId)));
        }

        [Test]
        public void IsRegistered_ReservedKeyboardExprId_ReturnsTrue()
        {
            var factory = CreateFactory();

            Assert.IsTrue(factory.IsRegistered(InputSourceId.Parse(KeyboardExpressionInputSource.ReservedId)));
        }

        [Test]
        public void IsRegistered_UnregisteredId_ReturnsFalse()
        {
            var factory = CreateFactory();

            Assert.IsFalse(factory.IsRegistered(InputSourceId.Parse("x-unknown-sensor")));
        }

        /// <summary>
        /// Critical 2: JSON → 型付き DTO のラウンドトリップ（Req 3.7）。
        /// </summary>
        [Test]
        public void TryDeserializeOptions_OscWithStalenessSeconds_ReturnsOscOptionsDto()
        {
            var factory = CreateFactory();

            var options = factory.TryDeserializeOptions(
                InputSourceId.Parse(OscInputSource.ReservedId),
                "{\"stalenessSeconds\":2.5}");

            Assert.IsNotNull(options);
            Assert.IsInstanceOf<OscOptionsDto>(options);
            Assert.AreEqual(2.5f, ((OscOptionsDto)options).stalenessSeconds, 1e-5f);
        }

        [Test]
        public void TryDeserializeOptions_ExpressionTriggerWithMaxStackDepth_ReturnsTypedDto()
        {
            var factory = CreateFactory();

            var options = factory.TryDeserializeOptions(
                InputSourceId.Parse(ControllerExpressionInputSource.ReservedId),
                "{\"maxStackDepth\":4}");

            Assert.IsNotNull(options);
            Assert.IsInstanceOf<ExpressionTriggerOptionsDto>(options);
            Assert.AreEqual(4, ((ExpressionTriggerOptionsDto)options).maxStackDepth);
        }

        [Test]
        public void TryDeserializeOptions_LipSync_ReturnsEmptyLipSyncOptionsDto()
        {
            var factory = CreateFactory();

            var options = factory.TryDeserializeOptions(
                InputSourceId.Parse(LipSyncInputSource.ReservedId),
                "{}");

            Assert.IsNotNull(options);
            Assert.IsInstanceOf<LipSyncOptionsDto>(options);
        }

        [Test]
        public void TryDeserializeOptions_NullOrEmptyOptionsJson_ReturnsDefaultDto()
        {
            var factory = CreateFactory();

            var oscDefault = factory.TryDeserializeOptions(
                InputSourceId.Parse(OscInputSource.ReservedId),
                null);
            var exprDefault = factory.TryDeserializeOptions(
                InputSourceId.Parse(ControllerExpressionInputSource.ReservedId),
                "   ");

            Assert.IsNotNull(oscDefault);
            Assert.IsInstanceOf<OscOptionsDto>(oscDefault);
            Assert.AreEqual(0f, ((OscOptionsDto)oscDefault).stalenessSeconds, 1e-5f);

            Assert.IsNotNull(exprDefault);
            Assert.IsInstanceOf<ExpressionTriggerOptionsDto>(exprDefault);
            Assert.AreEqual(0, ((ExpressionTriggerOptionsDto)exprDefault).maxStackDepth);
        }

        [Test]
        public void TryDeserializeOptions_UnregisteredId_ReturnsNull()
        {
            var factory = CreateFactory();

            var options = factory.TryDeserializeOptions(
                InputSourceId.Parse("x-unknown"),
                "{\"foo\":1}");

            Assert.IsNull(options);
        }

        [Test]
        public void TryCreate_OscWithStalenessOptions_ReturnsOscInputSource()
        {
            using var buffer = new OscDoubleBuffer(4);
            var time = new ManualTimeProvider();
            var factory = CreateFactory(oscBuffer: buffer, timeProvider: time);

            var options = new OscOptionsDto { stalenessSeconds = 2.5f };
            var source = factory.TryCreate(
                InputSourceId.Parse(OscInputSource.ReservedId),
                options,
                blendShapeCount: buffer.Size,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<OscInputSource>(source);
            Assert.AreEqual(OscInputSource.ReservedId, source.Id);
            Assert.AreEqual(InputSourceType.ValueProvider, source.Type);
            Assert.AreEqual(buffer.Size, source.BlendShapeCount);
        }

        [Test]
        public void TryCreate_LipSync_ReturnsLipSyncInputSource()
        {
            var factory = CreateFactory(lipSyncProvider: new StubLipSyncProvider());

            var source = factory.TryCreate(
                InputSourceId.Parse(LipSyncInputSource.ReservedId),
                new LipSyncOptionsDto(),
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<LipSyncInputSource>(source);
            Assert.AreEqual(LipSyncInputSource.ReservedId, source.Id);
            Assert.AreEqual(InputSourceType.ValueProvider, source.Type);
        }

        [Test]
        public void TryCreate_ControllerExpr_ReturnsControllerExpressionInputSource()
        {
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse(ControllerExpressionInputSource.ReservedId),
                new ExpressionTriggerOptionsDto { maxStackDepth = 4 },
                blendShapeCount: BlendShapeNames.Length,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<ControllerExpressionInputSource>(source);
            Assert.AreEqual(ControllerExpressionInputSource.ReservedId, source.Id);
            Assert.AreEqual(InputSourceType.ExpressionTrigger, source.Type);
            Assert.AreEqual(BlendShapeNames.Length, source.BlendShapeCount);
        }

        [Test]
        public void TryCreate_KeyboardExpr_ReturnsKeyboardExpressionInputSource()
        {
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse(KeyboardExpressionInputSource.ReservedId),
                new ExpressionTriggerOptionsDto { maxStackDepth = 2 },
                blendShapeCount: BlendShapeNames.Length,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<KeyboardExpressionInputSource>(source);
            Assert.AreEqual(KeyboardExpressionInputSource.ReservedId, source.Id);
            Assert.AreEqual(InputSourceType.ExpressionTrigger, source.Type);
        }

        [Test]
        public void TryCreate_ExpressionTriggerWithZeroMaxStackDepth_UsesDefault()
        {
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse(ControllerExpressionInputSource.ReservedId),
                new ExpressionTriggerOptionsDto { maxStackDepth = 0 },
                blendShapeCount: BlendShapeNames.Length,
                profile: BuildProfile());

            // 0 のときは既定値が用いられ、インスタンス化に成功する (D-14)。
            Assert.IsNotNull(source);
            Assert.IsInstanceOf<ControllerExpressionInputSource>(source);
        }

        [Test]
        public void TryCreate_NullOptions_UsesDefaultDtoAndSucceeds()
        {
            using var buffer = new OscDoubleBuffer(3);
            var time = new ManualTimeProvider();
            var factory = CreateFactory(oscBuffer: buffer, timeProvider: time);

            var source = factory.TryCreate(
                InputSourceId.Parse(OscInputSource.ReservedId),
                options: null,
                blendShapeCount: buffer.Size,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<OscInputSource>(source);
        }

        [Test]
        public void TryCreate_UnregisteredId_ReturnsNull()
        {
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse("x-unknown-sensor"),
                options: null,
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNull(source);
        }

        [Test]
        public void TryCreate_OscIdWithoutOscDependencies_ReturnsNull()
        {
            // OscDoubleBuffer / ITimeProvider が未注入の Factory では osc アダプタを
            // 生成できない。呼出側は null を受けて警告 + skip する契約 (Req 3.3)。
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse(OscInputSource.ReservedId),
                new OscOptionsDto(),
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNull(source);
        }

        [Test]
        public void TryCreate_LipSyncIdWithoutProvider_ReturnsNull()
        {
            var factory = CreateFactory();

            var source = factory.TryCreate(
                InputSourceId.Parse(LipSyncInputSource.ReservedId),
                new LipSyncOptionsDto(),
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNull(source);
        }
    }
}
