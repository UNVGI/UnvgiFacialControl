using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Input;
using Hidano.FacialControl.Osc;
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
            // コアの InputSourceFactory は lipsync のビルトイン登録のみを持つ。
            // OSC / Controller / Keyboard はサブパッケージ側のヘルパー経由で登録する。
            // 既存テスト互換のため、引数未指定時は stub を用いて全予約 id を登録しておく。
            var factory = new InputSourceFactory(lipSyncProvider: lipSyncProvider);

            var effectiveBuffer = oscBuffer ?? new OscDoubleBuffer(BlendShapeNames.Length);
            var effectiveTime = timeProvider ?? new ManualTimeProvider();
            OscRegistration.Register(factory, effectiveBuffer, effectiveTime);
            InputRegistration.Register(factory, BlendShapeNames);

            return factory;
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

        // =====================================================================
        // tasks.md 7.6: RegisterExtension<TOptions> 契約テスト (Req 1.7, 3.7)
        // =====================================================================

        [System.Serializable]
        private sealed class TestSensorOptionsDto : InputSourceOptionsDto
        {
            public float threshold;
            public int channels;
        }

        private sealed class TestExtensionInputSource : IInputSource
        {
            public string Id { get; }
            public InputSourceType Type => InputSourceType.ValueProvider;
            public int BlendShapeCount { get; }
            public TestSensorOptionsDto CapturedOptions { get; }

            public TestExtensionInputSource(string id, int blendShapeCount, TestSensorOptionsDto options)
            {
                Id = id;
                BlendShapeCount = blendShapeCount;
                CapturedOptions = options;
            }

            public void Tick(float deltaTime) { }

            public bool TryWriteValues(System.Span<float> output) => false;
        }

        [Test]
        public void RegisterExtension_XPrefixId_IsRegistered()
        {
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            Assert.IsTrue(factory.IsRegistered(id));
        }

        [Test]
        public void RegisterExtension_XPrefixIdWithTypedOptions_DeserializesAsTOptions()
        {
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            var deserialized = factory.TryDeserializeOptions(
                id,
                "{\"threshold\":0.75,\"channels\":3}");

            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOf<TestSensorOptionsDto>(deserialized);
            var typed = (TestSensorOptionsDto)deserialized;
            Assert.AreEqual(0.75f, typed.threshold, 1e-5f);
            Assert.AreEqual(3, typed.channels);
        }

        [Test]
        public void RegisterExtension_XPrefixIdWithNullOptionsJson_ReturnsDefaultDto()
        {
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            var deserialized = factory.TryDeserializeOptions(id, null);

            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOf<TestSensorOptionsDto>(deserialized);
            var typed = (TestSensorOptionsDto)deserialized;
            Assert.AreEqual(0f, typed.threshold, 1e-5f);
            Assert.AreEqual(0, typed.channels);
        }

        [Test]
        public void RegisterExtension_TryCreate_InvokesCreatorWithTypedOptions()
        {
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            var options = new TestSensorOptionsDto { threshold = 0.5f, channels = 2 };
            var source = factory.TryCreate(
                id,
                options,
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<TestExtensionInputSource>(source);
            var ext = (TestExtensionInputSource)source;
            Assert.AreEqual(id.Value, ext.Id);
            Assert.AreEqual(4, ext.BlendShapeCount);
            Assert.AreSame(options, ext.CapturedOptions);
        }

        [Test]
        public void RegisterExtension_TryCreateWithNullOptions_PassesDefaultTOptionsToCreator()
        {
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            var source = factory.TryCreate(
                id,
                options: null,
                blendShapeCount: 4,
                profile: BuildProfile());

            Assert.IsNotNull(source);
            Assert.IsInstanceOf<TestExtensionInputSource>(source);
            var ext = (TestExtensionInputSource)source;
            Assert.IsNotNull(ext.CapturedOptions);
            Assert.AreEqual(0f, ext.CapturedOptions.threshold, 1e-5f);
            Assert.AreEqual(0, ext.CapturedOptions.channels);
        }

        [Test]
        public void RegisterExtension_FullRoundTrip_JsonToAdapter()
        {
            // Critical 2 相当の経路を x-* 拡張側で検証:
            // TryDeserializeOptions → TryCreate が一貫して typed DTO を運ぶ。
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(id.Value, blendShapeCount, options));

            var options = factory.TryDeserializeOptions(id, "{\"threshold\":0.9,\"channels\":5}");
            var source = factory.TryCreate(id, options, blendShapeCount: 8, profile: BuildProfile());

            Assert.IsInstanceOf<TestExtensionInputSource>(source);
            var ext = (TestExtensionInputSource)source;
            Assert.AreEqual(0.9f, ext.CapturedOptions.threshold, 1e-5f);
            Assert.AreEqual(5, ext.CapturedOptions.channels);
            Assert.AreEqual(8, ext.BlendShapeCount);
        }

        [Test]
        public void RegisterExtension_NullCreator_ThrowsArgumentNullException()
        {
            var factory = CreateFactory();

            Assert.Throws<ArgumentNullException>(() =>
                factory.RegisterExtension<TestSensorOptionsDto>(
                    InputSourceId.Parse("x-test-sensor"),
                    creator: null));
        }

        [Test]
        public void RegisterExtension_UninitializedId_ThrowsArgumentException()
        {
            var factory = CreateFactory();

            Assert.Throws<ArgumentException>(() =>
                factory.RegisterExtension<TestSensorOptionsDto>(
                    id: default,
                    (options, blendShapeCount, profile) =>
                        new TestExtensionInputSource("x", blendShapeCount, options)));
        }

        [Test]
        public void RegisterExtension_ReservedId_WarnsAndDoesNotOverrideBuiltin()
        {
            // 予約 id (例: osc) は RegisterExtension で上書きできない。
            // 警告ログを出し、既存ビルトインの OscOptionsDto マッピングを維持する (Req 1.7)。
            using var buffer = new OscDoubleBuffer(4);
            var time = new ManualTimeProvider();
            var factory = CreateFactory(oscBuffer: buffer, timeProvider: time);

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "\\[InputSourceFactory\\].*Reserved id 'osc'.*cannot be overridden"));

            factory.RegisterExtension<TestSensorOptionsDto>(
                InputSourceId.Parse(OscInputSource.ReservedId),
                (options, blendShapeCount, profile) =>
                    new TestExtensionInputSource(OscInputSource.ReservedId, blendShapeCount, options));

            // 上書きされていないので OSC は OscOptionsDto を返し、OscInputSource を生成できる。
            var deserialized = factory.TryDeserializeOptions(
                InputSourceId.Parse(OscInputSource.ReservedId),
                "{\"stalenessSeconds\":1.25}");
            Assert.IsInstanceOf<OscOptionsDto>(deserialized);
            Assert.AreEqual(1.25f, ((OscOptionsDto)deserialized).stalenessSeconds, 1e-5f);

            var source = factory.TryCreate(
                InputSourceId.Parse(OscInputSource.ReservedId),
                deserialized,
                blendShapeCount: buffer.Size,
                profile: BuildProfile());
            Assert.IsInstanceOf<OscInputSource>(source);
        }

        [Test]
        public void RegisterExtension_SameIdTwice_LastRegistrationWins()
        {
            // preview 段階では extension の重複登録を警告せずに後勝ちで上書きする。
            // （開発/テスト時の再登録を許容するための選択。Req 1.7 には明示なし。）
            var factory = CreateFactory();
            var id = InputSourceId.Parse("x-test-sensor");

            var firstCalled = false;
            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                {
                    firstCalled = true;
                    return new TestExtensionInputSource("first", blendShapeCount, options);
                });

            var secondCalled = false;
            factory.RegisterExtension<TestSensorOptionsDto>(
                id,
                (options, blendShapeCount, profile) =>
                {
                    secondCalled = true;
                    return new TestExtensionInputSource("second", blendShapeCount, options);
                });

            var source = factory.TryCreate(
                id,
                new TestSensorOptionsDto(),
                blendShapeCount: 2,
                profile: BuildProfile());

            Assert.IsFalse(firstCalled);
            Assert.IsTrue(secondCalled);
            Assert.AreEqual("second", source.Id);
        }
    }
}
