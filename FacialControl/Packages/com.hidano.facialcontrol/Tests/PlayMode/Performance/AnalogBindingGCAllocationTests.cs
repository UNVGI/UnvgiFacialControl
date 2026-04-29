using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.Profiling;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.PlayMode.Performance
{
    /// <summary>
    /// Task 8.1: アナログ入力バインディング経路の GC ゼロアロケーション検証
    /// (Req 2.6, 3.6, 4.7, 8.1, 8.2, 8.6)。
    /// </summary>
    /// <remarks>
    /// 測定方針 (既存 <see cref="BoneWriterGCAllocationTests"/> と同形パターン):
    ///   - 主指標: <see cref="GC.GetTotalMemory(bool)"/> 差分 (managed alloc) を <c>LessOrEqual(0)</c> でアサート。
    ///   - 補助指標: <see cref="ProfilerRecorder"/> の <c>GC.Alloc</c> を <c>AreEqual(0)</c> でアサート。
    /// 検証経路:
    ///   - <see cref="AnalogMappingEvaluator.Evaluate"/> 10000 回 (Req 2.6, 8.1)
    ///   - <see cref="AnalogBlendShapeInputSource.TryWriteValues"/> 10 binding / 64 BlendShape 1000 frame (Req 3.6, 8.1)
    ///   - <see cref="AnalogBonePoseProvider.BuildAndPush"/> 5 bone × 3 axis bindings 1000 frame (Req 4.7, 8.1, 8.2)
    ///   - OSC <c>Volatile.Read/Write</c> 経路 (D-7 整合, Req 8.6)
    /// </remarks>
    [TestFixture]
    public class AnalogBindingGCAllocationTests
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

            public void SetAxis(int index, float value) => _values[index] = value;

            public void Tick(float deltaTime) { }

            public bool TryReadScalar(out float value)
            {
                if (!IsValid || AxisCount < 1) { value = 0f; return false; }
                value = _values[0];
                return true;
            }

            public bool TryReadVector2(out float x, out float y)
            {
                if (!IsValid || AxisCount < 2) { x = 0f; y = 0f; return false; }
                x = _values[0];
                y = _values[1];
                return true;
            }

            public bool TryReadAxes(Span<float> output)
            {
                if (!IsValid) return false;
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

        private static AnalogMappingFunction CustomMapping()
        {
            // dead-zone / scale / offset / clamp が全て効くマッピング。Curve は Linear で十分。
            return new AnalogMappingFunction(
                deadZone: 0.05f,
                scale: 1.5f,
                offset: 0.1f,
                curve: TransitionCurve.Linear,
                invert: false,
                min: 0f,
                max: 1f);
        }

        // ============================================================
        // AnalogMappingEvaluator.Evaluate (Req 2.6, 8.1)
        // ============================================================

        [Test]
        public void AnalogMappingEvaluator_Evaluate_TenThousandIterations_ZeroGCAllocation()
        {
            var mapping = CustomMapping();

            // ウォームアップ: JIT と評価器内部の安定化。
            float warmAcc = 0f;
            for (int i = 0; i < 1000; i++)
            {
                warmAcc += AnalogMappingEvaluator.Evaluate(in mapping, i * 0.001f);
            }
            Assert.IsTrue(!float.IsNaN(warmAcc));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            float acc = 0f;
            for (int i = 0; i < 10000; i++)
            {
                acc += AnalogMappingEvaluator.Evaluate(in mapping, i * 0.0001f);
            }
            Assert.IsTrue(!float.IsNaN(acc));

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            Assert.LessOrEqual(managedDiff, 0,
                $"AnalogMappingEvaluator.Evaluate の 10000 回ループで managed alloc が発生: " +
                $"diff={managedDiff} bytes (Req 2.6, 8.1)。");
        }

        [Test]
        public void AnalogMappingEvaluator_Evaluate_TenThousandIterations_ZeroGCAllocation_Profiler()
        {
            var mapping = CustomMapping();

            for (int i = 0; i < 1000; i++)
            {
                _ = AnalogMappingEvaluator.Evaluate(in mapping, i * 0.001f);
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            try
            {
                float acc = 0f;
                for (int i = 0; i < 10000; i++)
                {
                    acc += AnalogMappingEvaluator.Evaluate(in mapping, i * 0.0001f);
                }
                Assert.IsTrue(!float.IsNaN(acc));

                long gcAlloc = recorder.LastValue;
                Assert.AreEqual(0, gcAlloc,
                    $"AnalogMappingEvaluator.Evaluate で GC.Alloc が検出されました: {gcAlloc} bytes (Req 2.6, 8.1)。");
            }
            finally
            {
                recorder.Dispose();
            }
        }

        // ============================================================
        // AnalogBlendShapeInputSource.TryWriteValues (Req 3.6, 8.1)
        // ============================================================

        [Test]
        public void AnalogBlendShapeInputSource_TryWriteValues_TenBindings_OneThousandFrames_ZeroGCAllocation()
        {
            var (source, sources, bindings, blendShapeNames) = BuildBlendShapeRig(
                blendShapeCount: 64, bindingCount: 10);
            var output = new float[64];

            // ウォームアップ
            for (int i = 0; i < 100; i++)
            {
                MutateBlendShapeSources(sources, i);
                source.TryWriteValues(output);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            for (int frame = 0; frame < 1000; frame++)
            {
                MutateBlendShapeSources(sources, frame);
                source.TryWriteValues(output);
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            Assert.LessOrEqual(managedDiff, 0,
                $"AnalogBlendShapeInputSource.TryWriteValues の 1000 frame ループで managed alloc が発生: " +
                $"diff={managedDiff} bytes (Req 3.6, 8.1)。");
            // 結果配列が確実に書込まれていること (sanity)
            Assert.IsTrue(!float.IsNaN(output[0]));

            GC.KeepAlive(bindings);
            GC.KeepAlive(blendShapeNames);
        }

        [Test]
        public void AnalogBlendShapeInputSource_TryWriteValues_TenBindings_OneThousandFrames_ZeroGCAllocation_Profiler()
        {
            var (source, sources, bindings, blendShapeNames) = BuildBlendShapeRig(
                blendShapeCount: 64, bindingCount: 10);
            var output = new float[64];

            for (int i = 0; i < 100; i++)
            {
                MutateBlendShapeSources(sources, i);
                source.TryWriteValues(output);
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            try
            {
                for (int frame = 0; frame < 1000; frame++)
                {
                    MutateBlendShapeSources(sources, frame);
                    source.TryWriteValues(output);
                }

                long gcAlloc = recorder.LastValue;
                Assert.AreEqual(0, gcAlloc,
                    $"AnalogBlendShapeInputSource.TryWriteValues で GC.Alloc が検出されました: " +
                    $"{gcAlloc} bytes (Req 3.6, 8.1)。");
            }
            finally
            {
                recorder.Dispose();
            }

            GC.KeepAlive(bindings);
            GC.KeepAlive(blendShapeNames);
        }

        // ============================================================
        // AnalogBonePoseProvider.BuildAndPush (Req 4.7, 8.1, 8.2)
        // ============================================================

        [Test]
        public void AnalogBonePoseProvider_BuildAndPush_FiveBonesThreeAxes_OneThousandFrames_ZeroGCAllocation()
        {
            var (provider, sources, bp) = BuildBonePoseRig(boneCount: 5, axesPerBone: 3);

            try
            {
                // ウォームアップ
                for (int i = 0; i < 100; i++)
                {
                    MutateBonePoseSources(sources, i);
                    provider.BuildAndPush();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

                for (int frame = 0; frame < 1000; frame++)
                {
                    MutateBonePoseSources(sources, frame);
                    provider.BuildAndPush();
                }

                long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
                long managedDiff = managedAfter - managedBefore;

                Assert.LessOrEqual(managedDiff, 0,
                    $"AnalogBonePoseProvider.BuildAndPush の 1000 frame ループで managed alloc が発生: " +
                    $"diff={managedDiff} bytes (Req 4.7, 8.1, 8.2)。" +
                    "Phase 2.1 の internal hot-path ctor 経由で _entryBuffer 共有を期待。");
                Assert.GreaterOrEqual(bp.CallCount, 1100);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void AnalogBonePoseProvider_BuildAndPush_FiveBonesThreeAxes_OneThousandFrames_ZeroGCAllocation_Profiler()
        {
            var (provider, sources, bp) = BuildBonePoseRig(boneCount: 5, axesPerBone: 3);

            try
            {
                for (int i = 0; i < 100; i++)
                {
                    MutateBonePoseSources(sources, i);
                    provider.BuildAndPush();
                }

                var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
                try
                {
                    for (int frame = 0; frame < 1000; frame++)
                    {
                        MutateBonePoseSources(sources, frame);
                        provider.BuildAndPush();
                    }

                    long gcAlloc = recorder.LastValue;
                    Assert.AreEqual(0, gcAlloc,
                        $"AnalogBonePoseProvider.BuildAndPush で GC.Alloc が検出されました: " +
                        $"{gcAlloc} bytes (Req 4.7, 8.1, 8.2)。");
                }
                finally
                {
                    recorder.Dispose();
                }
                Assert.IsTrue(bp.CallCount > 0);
            }
            finally
            {
                provider.Dispose();
            }
        }

        // ============================================================
        // OSC Volatile.Read/Write 経路 (Req 8.6, D-7)
        // ============================================================

        [Test]
        public void OscVolatileReadWrite_HotPath_ZeroGCAllocation()
        {
            // OscFloatAnalogSource の受信 → メインスレッド転写経路を Volatile.Read/Write +
            // Interlocked.Increment で再現する。これらの API が boxing なしで動作することを確認する。
            // ProfilerRecorder の GC.Alloc を主指標とすることでテストランナー由来の
            // mono ヒープチャンク確保ノイズを排除する (Req 8.6, D-7)。
            float pendingValue = 0f;
            int writeTick = 0;
            int lastObservedTick = 0;
            float cachedValue = 0f;

            // ウォームアップ
            for (int i = 0; i < 1000; i++)
            {
                Volatile.Write(ref pendingValue, i * 0.001f);
                Interlocked.Increment(ref writeTick);
                int t = Volatile.Read(ref writeTick);
                if (t != lastObservedTick)
                {
                    lastObservedTick = t;
                    cachedValue = Volatile.Read(ref pendingValue);
                }
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            try
            {
                for (int i = 0; i < 10000; i++)
                {
                    Volatile.Write(ref pendingValue, i * 0.0001f);
                    Interlocked.Increment(ref writeTick);
                    int t = Volatile.Read(ref writeTick);
                    if (t != lastObservedTick)
                    {
                        lastObservedTick = t;
                        cachedValue = Volatile.Read(ref pendingValue);
                    }
                }
                Assert.IsTrue(!float.IsNaN(cachedValue));

                long gcAlloc = recorder.LastValue;
                Assert.AreEqual(0, gcAlloc,
                    $"OSC Volatile.Read/Write 経路で GC.Alloc が検出: {gcAlloc} bytes (Req 8.6, D-7)。");
            }
            finally
            {
                recorder.Dispose();
            }
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private static (AnalogBlendShapeInputSource source,
                       Dictionary<string, IAnalogInputSource> sources,
                       AnalogBindingEntry[] bindings,
                       string[] blendShapeNames)
            BuildBlendShapeRig(int blendShapeCount, int bindingCount)
        {
            var blendShapeNames = new string[blendShapeCount];
            for (int i = 0; i < blendShapeCount; i++)
            {
                blendShapeNames[i] = $"blendShape_{i}";
            }

            // 複数 source を散らして AxisCount 1 / 2 の両経路を踏む。
            var sources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal);
            int sourceCount = (bindingCount + 1) / 2;
            for (int s = 0; s < sourceCount; s++)
            {
                int axisCount = (s % 2 == 0) ? 1 : 2;
                sources[$"src_{s}"] = new FakeAnalogSource($"src_{s}", axisCount);
            }

            var mapping = CustomMapping();
            var bindings = new AnalogBindingEntry[bindingCount];
            for (int i = 0; i < bindingCount; i++)
            {
                int srcIndex = i / 2;
                int axis = (sources[$"src_{srcIndex}"].AxisCount == 1) ? 0 : (i % 2);
                int bsIndex = i % blendShapeCount;
                bindings[i] = new AnalogBindingEntry(
                    sourceId: $"src_{srcIndex}",
                    sourceAxis: axis,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: blendShapeNames[bsIndex],
                    targetAxis: AnalogTargetAxis.X,
                    mapping: mapping);
            }

            var source = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount,
                blendShapeNames,
                sources,
                bindings);
            return (source, sources, bindings, blendShapeNames);
        }

        private static void MutateBlendShapeSources(
            Dictionary<string, IAnalogInputSource> sources, int frame)
        {
            foreach (var kv in sources)
            {
                var src = (FakeAnalogSource)kv.Value;
                for (int a = 0; a < src.AxisCount; a++)
                {
                    src.SetAxis(a, ((frame + a) % 100) * 0.01f);
                }
            }
        }

        private static (AnalogBonePoseProvider provider,
                       Dictionary<string, IAnalogInputSource> sources,
                       FakeBonePoseProvider bp)
            BuildBonePoseRig(int boneCount, int axesPerBone)
        {
            var sources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal)
            {
                { "stickL", new FakeAnalogSource("stickL", 2) },
                { "stickR", new FakeAnalogSource("stickR", 2) },
                { "trig",   new FakeAnalogSource("trig",   1) },
            };

            var mapping = CustomMapping();
            var boneNames = new string[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                boneNames[b] = $"Bone_{b}";
            }

            var bindings = new List<AnalogBindingEntry>(boneCount * axesPerBone);
            for (int b = 0; b < boneCount; b++)
            {
                for (int a = 0; a < axesPerBone; a++)
                {
                    string sourceId = a switch
                    {
                        0 => "stickL",
                        1 => "stickR",
                        _ => "trig",
                    };
                    int sourceAxis = (sources[sourceId].AxisCount == 1) ? 0 : (a % 2);
                    var targetAxis = (AnalogTargetAxis)(a % 3);
                    bindings.Add(new AnalogBindingEntry(
                        sourceId: sourceId,
                        sourceAxis: sourceAxis,
                        targetKind: AnalogBindingTargetKind.BonePose,
                        targetIdentifier: boneNames[b],
                        targetAxis: targetAxis,
                        mapping: mapping));
                }
            }

            var bp = new FakeBonePoseProvider();
            var provider = new AnalogBonePoseProvider(bp, sources, bindings);
            return (provider, sources, bp);
        }

        private static void MutateBonePoseSources(
            Dictionary<string, IAnalogInputSource> sources, int frame)
        {
            foreach (var kv in sources)
            {
                var src = (FakeAnalogSource)kv.Value;
                for (int a = 0; a < src.AxisCount; a++)
                {
                    src.SetAxis(a, ((frame + a * 7) % 100) * 0.01f);
                }
            }
        }
    }
}
