using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    /// <summary>
    /// <see cref="AnalogBlendShapeInputSource"/> の EditMode 契約テスト（tasks.md 4.1）。
    /// </summary>
    /// <remarks>
    /// 検証事項:
    /// <list type="bullet">
    ///   <item>予約 id <c>analog-blendshape</c> と <see cref="InputSourceType.ValueProvider"/> を持つ。</item>
    ///   <item>BlendShape 名 → index 解決（Req 3.4）。未存在 BS は warn + skip（Req 3.5）。</item>
    ///   <item>同一 BS index への複数 binding 値の sum（Req 3.3、二重 clamp なし）。</item>
    ///   <item>N-axis passthrough（Req 3.7）。</item>
    ///   <item><c>TryWriteValues</c> の overlap-only 書込（Req 1.4）。</item>
    ///   <item>全ソース無効時に false 返却・<c>output</c> 不変。</item>
    ///   <item>per-frame で alloc=0（Req 3.6, 8.1）。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class AnalogBlendShapeInputSourceTests
    {
        private sealed class FakeAnalogSource : IAnalogInputSource
        {
            private readonly float[] _values;
            public string Id { get; }
            public bool IsValid { get; set; } = true;
            public int AxisCount { get; }

            public FakeAnalogSource(string id, int axisCount, float[] values = null)
            {
                Id = id;
                AxisCount = axisCount;
                _values = values != null ? (float[])values.Clone() : new float[axisCount];
            }

            public void SetValues(params float[] values)
            {
                int n = _values.Length < values.Length ? _values.Length : values.Length;
                for (int i = 0; i < n; i++)
                {
                    _values[i] = values[i];
                }
            }

            public void SetAxis(int index, float value)
            {
                _values[index] = value;
            }

            public void Tick(float deltaTime) { }

            public bool TryReadScalar(out float value)
            {
                if (!IsValid || AxisCount < 1)
                {
                    value = 0f;
                    return false;
                }
                value = _values[0];
                return true;
            }

            public bool TryReadVector2(out float x, out float y)
            {
                if (!IsValid || AxisCount < 2)
                {
                    x = 0f;
                    y = 0f;
                    return false;
                }
                x = _values[0];
                y = _values[1];
                return true;
            }

            public bool TryReadAxes(Span<float> output)
            {
                if (!IsValid)
                {
                    return false;
                }
                int n = output.Length < _values.Length ? output.Length : _values.Length;
                for (int i = 0; i < n; i++)
                {
                    output[i] = _values[i];
                }
                return true;
            }
        }

        private static AnalogMappingFunction Identity() => AnalogMappingFunction.Identity;

        // --- 基本契約 ---

        [Test]
        public void Id_IsReservedAnalogBlendShape()
        {
            var source = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 2,
                blendShapeNames: new[] { "A", "B" },
                sources: new Dictionary<string, IAnalogInputSource>(),
                bindings: Array.Empty<AnalogBindingEntry>());

            Assert.AreEqual("analog-blendshape", source.Id);
            Assert.AreEqual(InputSourceType.ValueProvider, source.Type);
            Assert.AreEqual(2, source.BlendShapeCount);
        }

        // --- 単一 binding の書込 ---

        [Test]
        public void TryWriteValues_SingleBinding_WritesMappedValue()
        {
            var src = new FakeAnalogSource("osc", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "osc", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "osc", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 2,
                blendShapeNames: new[] { "Smile", "Anger" },
                sources: sources,
                bindings: bindings);

            var output = new float[2];
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.5f, output[0], 1e-5f);
            Assert.AreEqual(0f, output[1], 1e-5f);
        }

        // --- 同一 BS への複数 binding は sum（Req 3.3） ---

        [Test]
        public void TryWriteValues_SameBlendShape_TwoBindings_SumsValues()
        {
            var srcA = new FakeAnalogSource("a", 1);
            srcA.SetValues(0.3f);
            var srcB = new FakeAnalogSource("b", 1);
            srcB.SetValues(0.4f);

            var sources = new Dictionary<string, IAnalogInputSource>
            {
                { "a", srcA },
                { "b", srcB },
            };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "b", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: bindings);

            var output = new float[1];
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.7f, output[0], 1e-5f, "post-mapping 値の sum、二重 clamp なし。");
        }

        // --- 同一 BS への複数 binding sum で 1.0 を超えても clamp しない（Req 3.3） ---

        [Test]
        public void TryWriteValues_SumExceedsOne_NotClampedHere()
        {
            var srcA = new FakeAnalogSource("a", 1);
            srcA.SetValues(0.7f);
            var srcB = new FakeAnalogSource("b", 1);
            srcB.SetValues(0.8f);

            var sources = new Dictionary<string, IAnalogInputSource>
            {
                { "a", srcA },
                { "b", srcB },
            };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "b", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: bindings);

            var output = new float[1];
            inputSource.TryWriteValues(output);

            Assert.AreEqual(1.5f, output[0], 1e-5f,
                "Aggregator が clamp01 する想定で adapter 側は raw sum を出す。");
        }

        // --- 未存在 BS は warn + skip（Req 3.5） ---

        [Test]
        public void Constructor_UnknownBlendShape_WarnsAndSkips()
        {
            var src = new FakeAnalogSource("a", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "MissingBS",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("MissingBS"));

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: bindings);

            var output = new float[1];
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.5f, output[0], 1e-5f,
                "未存在 BS の binding は skip され、Smile だけが書込まれる。");
        }

        // --- N-axis passthrough（Req 3.7） ---

        [Test]
        public void TryWriteValues_NAxisPassthrough_WritesEachAxisToCorrespondingBlendShape()
        {
            var arkit = new FakeAnalogSource("arkit", 3);
            arkit.SetValues(0.1f, 0.2f, 0.3f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "arkit", arkit } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "arkit", 0, AnalogBindingTargetKind.BlendShape, "BS_A",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "arkit", 1, AnalogBindingTargetKind.BlendShape, "BS_B",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "arkit", 2, AnalogBindingTargetKind.BlendShape, "BS_C",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 3,
                blendShapeNames: new[] { "BS_A", "BS_B", "BS_C" },
                sources: sources,
                bindings: bindings);

            var output = new float[3];
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.1f, output[0], 1e-5f);
            Assert.AreEqual(0.2f, output[1], 1e-5f);
            Assert.AreEqual(0.3f, output[2], 1e-5f);
        }

        // --- BonePose ターゲットは BlendShape adapter で無視 ---

        [Test]
        public void TryWriteValues_BonePoseTargetEntries_AreIgnored()
        {
            var src = new FakeAnalogSource("a", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: bindings);

            var output = new float[1];
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.5f, output[0], 1e-5f);
        }

        // --- 全ソース無効 → false、output 不変 ---

        [Test]
        public void TryWriteValues_AllSourcesInvalid_ReturnsFalseAndDoesNotTouchOutput()
        {
            var src = new FakeAnalogSource("a", 1) { IsValid = false };
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: bindings);

            var output = new float[] { 7f };
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(7f, output[0], 1e-6f, "false の場合 output 不変（IInputSource 契約）。");
        }

        // --- bindings 0 件 → false ---

        [Test]
        public void TryWriteValues_NoBindings_ReturnsFalse()
        {
            var sources = new Dictionary<string, IAnalogInputSource>();

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "Smile" },
                sources: sources,
                bindings: Array.Empty<AnalogBindingEntry>());

            var output = new float[] { 9f };
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsFalse(wrote);
            Assert.AreEqual(9f, output[0], 1e-6f);
        }

        // --- overlap-only: output が短い ---

        [Test]
        public void TryWriteValues_OutputShorter_WritesOverlapOnly()
        {
            var src = new FakeAnalogSource("a", 2);
            src.SetValues(0.3f, 0.4f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "BS_A",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "a", 1, AnalogBindingTargetKind.BlendShape, "BS_B",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 2,
                blendShapeNames: new[] { "BS_A", "BS_B" },
                sources: sources,
                bindings: bindings);

            var output = new float[] { 9f };
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.3f, output[0], 1e-5f);
        }

        // --- overlap-only: output が長い → 残余は呼出側責務 ---

        [Test]
        public void TryWriteValues_OutputLonger_WritesOverlapOnlyResidualUntouched()
        {
            var src = new FakeAnalogSource("a", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "BS_A",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 1,
                blendShapeNames: new[] { "BS_A" },
                sources: sources,
                bindings: bindings);

            var output = new float[] { 7f, 7f, 7f };
            bool wrote = inputSource.TryWriteValues(output);

            Assert.IsTrue(wrote);
            Assert.AreEqual(0.5f, output[0], 1e-5f);
            Assert.AreEqual(7f, output[1], 1e-6f, "残余は呼出側責務（IInputSource 契約）。");
            Assert.AreEqual(7f, output[2], 1e-6f);
        }

        // --- per-frame alloc=0（Req 3.6, 8.1） ---

        [Test]
        public void TryWriteValues_HotPath_OneThousandFrames_ZeroAllocation()
        {
            var src = new FakeAnalogSource("a", 2);
            src.SetValues(0.3f, 0.4f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "BS_A",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "a", 1, AnalogBindingTargetKind.BlendShape, "BS_B",
                    AnalogTargetAxis.X, Identity()),
            };

            var inputSource = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount: 2,
                blendShapeNames: new[] { "BS_A", "BS_B" },
                sources: sources,
                bindings: bindings);

            var output = new float[2];

            // ウォームアップ：測定と同じ入力分布で全 JIT ブランチを事前コンパイル
            for (int i = 0; i < 200; i++)
            {
                src.SetAxis(0, i * 0.001f);
                src.SetAxis(1, i * 0.0005f);
                inputSource.TryWriteValues(output);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long monoBefore = Profiler.GetMonoUsedSizeLong();

            for (int i = 0; i < 1000; i++)
            {
                src.SetAxis(0, i * 0.001f);
                src.SetAxis(1, i * 0.0005f);
                inputSource.TryWriteValues(output);
            }

            long monoAfter = Profiler.GetMonoUsedSizeLong();
            long diff = monoAfter - monoBefore;

            // Mono ヒープページノイズ許容 65536 bytes（既存 OscControllerBlendingIntegrationTests と同基準）
            Assert.LessOrEqual(diff, 65536,
                $"hot path で managed alloc がページノイズ許容 (65536 bytes) を超過: diff={diff} bytes (Req 3.6, 8.1)");
        }
    }
}
