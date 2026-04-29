using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Profiling;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Bone
{
    /// <summary>
    /// <see cref="AnalogBonePoseProvider"/> の EditMode 契約テスト（tasks.md 4.1）。
    /// </summary>
    /// <remarks>
    /// 検証事項:
    /// <list type="bullet">
    ///   <item>binding entry → bone (X/Y/Z) → mapping → BonePose 構築（Req 4.1, 4.2）。</item>
    ///   <item>同一 (bone, axis) への複数 binding 値の sum（Req 4.6）。</item>
    ///   <item>per-frame で <see cref="IBonePoseProvider.SetActiveBonePose"/> を 1 回だけ呼ぶ（Req 4.5）。</item>
    ///   <item>bindings 0 件 / 全ソース無効で空 BonePose（Req 4.8）。</item>
    ///   <item><c>_entryBuffer</c> 再利用で per-frame alloc=0（Req 4.7、Phase 2.1 ctor 経由）。</item>
    ///   <item><see cref="IDisposable"/> 契約。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class AnalogBonePoseProviderTests
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

        private sealed class FakeBonePoseProvider : IBonePoseProvider
        {
            public int CallCount;
            public BonePose LastPose;

            public void SetActiveBonePose(in BonePose pose)
            {
                CallCount++;
                LastPose = pose;
            }
        }

        private static AnalogMappingFunction Identity() => AnalogMappingFunction.Identity;

        // --- 基本契約 ---

        [Test]
        public void BuildAndPush_SingleBinding_BuildsBonePoseWithExpectedEuler()
        {
            var src = new FakeAnalogSource("stick", 2);
            src.SetValues(0.3f, 0.6f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.CallCount);
            Assert.AreEqual(1, bp.LastPose.Entries.Length);
            Assert.AreEqual("LeftEye", bp.LastPose.Entries.Span[0].BoneName);
            Assert.AreEqual(0f, bp.LastPose.Entries.Span[0].EulerX, 1e-5f);
            Assert.AreEqual(0.3f, bp.LastPose.Entries.Span[0].EulerY, 1e-5f);
            Assert.AreEqual(0f, bp.LastPose.Entries.Span[0].EulerZ, 1e-5f);
        }

        [Test]
        public void BuildAndPush_MultipleAxesOnSameBone_PopulatedAcrossXyz()
        {
            // Linear curve は内部で clamp01 されるため、入力は [0..1] 範囲に揃える
            // （AnalogMappingEvaluator の意図的振る舞い、AnalogMappingEvaluatorTests と整合）。
            var src = new FakeAnalogSource("stick", 2);
            src.SetValues(0.6f, 0.2f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "Head",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "stick", 1, AnalogBindingTargetKind.BonePose, "Head",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.LastPose.Entries.Length);
            var entry = bp.LastPose.Entries.Span[0];
            Assert.AreEqual("Head", entry.BoneName);
            Assert.AreEqual(0.6f, entry.EulerX, 1e-5f);
            Assert.AreEqual(0.2f, entry.EulerY, 1e-5f);
            Assert.AreEqual(0f, entry.EulerZ, 1e-5f);
        }

        // --- 同一 (bone, axis) は sum（Req 4.6） ---

        [Test]
        public void BuildAndPush_SameBoneAxis_TwoBindings_SumsValues()
        {
            var srcA = new FakeAnalogSource("a", 1);
            srcA.SetValues(0.2f);
            var srcB = new FakeAnalogSource("b", 1);
            srcB.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource>
            {
                { "a", srcA },
                { "b", srcB },
            };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
                new AnalogBindingEntry(
                    "b", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.LastPose.Entries.Length);
            Assert.AreEqual(0.7f, bp.LastPose.Entries.Span[0].EulerY, 1e-5f);
        }

        // --- bindings 0 件 → 空 BonePose（Req 4.8） ---

        [Test]
        public void BuildAndPush_NoBindings_PushesEmptyBonePose()
        {
            var sources = new Dictionary<string, IAnalogInputSource>();
            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(
                bp, sources, Array.Empty<AnalogBindingEntry>());

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.CallCount);
            Assert.AreEqual(0, bp.LastPose.Entries.Length);
        }

        // --- 全ソース無効 → 空 BonePose 同等の出力（Req 4.8） ---

        [Test]
        public void BuildAndPush_AllSourcesInvalid_PushesEmptyOrZeroEntries()
        {
            var src = new FakeAnalogSource("stick", 2) { IsValid = false };
            src.SetValues(0.5f, 0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.CallCount);
            // 空 BonePose または zero-euler の単一エントリ。BoneWriter 的にはどちらも no-op。
            if (bp.LastPose.Entries.Length > 0)
            {
                var e = bp.LastPose.Entries.Span[0];
                Assert.AreEqual(0f, e.EulerX, 1e-5f);
                Assert.AreEqual(0f, e.EulerY, 1e-5f);
                Assert.AreEqual(0f, e.EulerZ, 1e-5f);
            }
        }

        // --- per-frame で SetActiveBonePose は 1 回だけ（Req 4.5） ---

        [Test]
        public void BuildAndPush_CalledOnce_InvokesSetActiveBonePoseOnce()
        {
            var src = new FakeAnalogSource("stick", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();
            provider.BuildAndPush();
            provider.BuildAndPush();

            Assert.AreEqual(3, bp.CallCount, "BuildAndPush 1 回 = SetActiveBonePose 1 回（Req 4.5）。");
        }

        // --- BlendShape ターゲットの binding は無視 ---

        [Test]
        public void BuildAndPush_BlendShapeBindings_AreIgnored()
        {
            var src = new FakeAnalogSource("a", 1);
            src.SetValues(0.5f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "a", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "a", 0, AnalogBindingTargetKind.BlendShape, "Smile",
                    AnalogTargetAxis.X, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(1, bp.CallCount);
            Assert.AreEqual(0, bp.LastPose.Entries.Length,
                "BlendShape ターゲットは BonePose adapter で無視され、空 BonePose を発行する。");
        }

        // --- ユニーク (bone, axis) の集約 ---

        [Test]
        public void BuildAndPush_TwoBones_BuildsTwoEntries()
        {
            var src = new FakeAnalogSource("stick", 2);
            src.SetValues(1f, 2f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.X, Identity()),
                new AnalogBindingEntry(
                    "stick", 1, AnalogBindingTargetKind.BonePose, "RightEye",
                    AnalogTargetAxis.Y, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            provider.BuildAndPush();

            Assert.AreEqual(2, bp.LastPose.Entries.Length);
            // 順序は stable 必須ではないが、bone 名で同定する。
            string b0 = bp.LastPose.Entries.Span[0].BoneName;
            string b1 = bp.LastPose.Entries.Span[1].BoneName;
            Assert.IsTrue(b0 == "LeftEye" || b0 == "RightEye");
            Assert.IsTrue(b1 == "LeftEye" || b1 == "RightEye");
            Assert.AreNotEqual(b0, b1);
        }

        // --- Dispose ---

        [Test]
        public void Dispose_DoesNotThrow_AndCanBeCalledTwice()
        {
            var sources = new Dictionary<string, IAnalogInputSource>();
            var bp = new FakeBonePoseProvider();
            var provider = new AnalogBonePoseProvider(
                bp, sources, Array.Empty<AnalogBindingEntry>());

            provider.Dispose();
            Assert.DoesNotThrow(() => provider.Dispose());
        }

        // --- per-frame alloc=0（Req 4.7、Phase 2.1 ctor 経由） ---

        [Test]
        public void BuildAndPush_HotPath_OneThousandFrames_ZeroAllocation()
        {
            var src = new FakeAnalogSource("stick", 2);
            src.SetValues(0.3f, 0.6f);

            var sources = new Dictionary<string, IAnalogInputSource> { { "stick", src } };
            var bindings = new[]
            {
                new AnalogBindingEntry(
                    "stick", 0, AnalogBindingTargetKind.BonePose, "LeftEye",
                    AnalogTargetAxis.Y, Identity()),
                new AnalogBindingEntry(
                    "stick", 1, AnalogBindingTargetKind.BonePose, "RightEye",
                    AnalogTargetAxis.X, Identity()),
            };

            var bp = new FakeBonePoseProvider();
            using var provider = new AnalogBonePoseProvider(bp, sources, bindings);

            // ウォームアップ
            for (int i = 0; i < 200; i++)
            {
                provider.BuildAndPush();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long monoBefore = Profiler.GetMonoUsedSizeLong();

            for (int i = 0; i < 1000; i++)
            {
                src.SetAxis(0, i * 0.001f);
                src.SetAxis(1, i * 0.0005f);
                provider.BuildAndPush();
            }

            long monoAfter = Profiler.GetMonoUsedSizeLong();
            long diff = monoAfter - monoBefore;

            Assert.LessOrEqual(diff, 0,
                $"hot path で managed alloc 検出: diff={diff} bytes (Req 4.7, 8.1, 8.2)。" +
                "Phase 2.1 の internal hot-path ctor 経由で _entryBuffer 共有を期待。");
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            var sources = new Dictionary<string, IAnalogInputSource>();
            var bp = new FakeBonePoseProvider();
            var bindings = Array.Empty<AnalogBindingEntry>();

            Assert.Throws<ArgumentNullException>(
                () => new AnalogBonePoseProvider(null, sources, bindings));
            Assert.Throws<ArgumentNullException>(
                () => new AnalogBonePoseProvider(bp, null, bindings));
            Assert.Throws<ArgumentNullException>(
                () => new AnalogBonePoseProvider(bp, sources, null));
        }
    }
}
