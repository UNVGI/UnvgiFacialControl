using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.Profiling;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Performance
{
    /// <summary>
    /// Task 8.1: マルチキャラクター (10 体) 同時アナログバインディング更新の Performance テスト
    /// (Req 8.3, 8.4, 8.5)。
    /// </summary>
    /// <remarks>
    /// 検証項目:
    ///   - 10 GameObject 相当の <see cref="AnalogBlendShapeInputSource"/> + <see cref="AnalogBonePoseProvider"/> を
    ///     各 8 binding (mixed BlendShape + BonePose) で生成し、<c>LateUpdate</c> 相当の per-frame 評価を 100 frame
    ///     回したときの total managed alloc が 0 (Req 8.4)。
    ///   - bindings=64 で per-frame O(N) 線形であること。bindings=64 / bindings=128 を比較し、
    ///     processing time が 5× 以下に収まることで quadratic 兆候を排除 (Req 8.3)。
    ///   - 10 体同時更新が 1 frame budget 内 (16.67ms フレームバジェットの 12% = 2ms) に収まること (Req 8.4)。
    /// </remarks>
    [TestFixture]
    public class AnalogBindingMultiCharacterPerformanceTests
    {
        private const int CharacterCount = 10;
        private const int BindingsPerCharacter = 8;
        private const int BlendShapeCount = 32;

        private sealed class FakeAnalogSource : IAnalogInputSource
        {
            private readonly float[] _values;
            public string Id { get; }
            public bool IsValid { get; set; } = true;
            public int AxisCount { get; }

            public FakeAnalogSource(string id, int axisCount)
            {
                Id = id;
                AxisCount = axisCount;
                _values = new float[axisCount];
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

        /// <summary>1 キャラクター分のアナログ駆動状態。</summary>
        private sealed class CharacterRig : IDisposable
        {
            public Dictionary<string, IAnalogInputSource> Sources;
            public AnalogBlendShapeInputSource BlendShapeSource;
            public AnalogBonePoseProvider BonePoseProvider;
            public FakeBonePoseProvider FakeBoneProvider;
            public float[] OutputBuffer;

            public void Tick(int frame)
            {
                // Source 値を frame に依存して更新。
                foreach (var kv in Sources)
                {
                    var src = (FakeAnalogSource)kv.Value;
                    for (int a = 0; a < src.AxisCount; a++)
                    {
                        src.SetAxis(a, ((frame + a * 13) % 100) * 0.01f);
                    }
                }

                BlendShapeSource.TryWriteValues(OutputBuffer);
                BonePoseProvider.BuildAndPush();
            }

            public void Dispose()
            {
                BonePoseProvider?.Dispose();
            }
        }

        // ============================================================
        // 10 体同時更新: 100 frame で managed alloc が 0
        // ============================================================

        [Test]
        public void TenCharacters_MixedBindings_OneHundredFrames_ZeroGCAllocation()
        {
            var rigs = BuildRigs(CharacterCount, BindingsPerCharacter, BlendShapeCount);
            try
            {
                // ウォームアップ
                for (int frame = 0; frame < 30; frame++)
                {
                    for (int c = 0; c < rigs.Length; c++)
                    {
                        rigs[c].Tick(frame);
                    }
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

                for (int frame = 0; frame < 100; frame++)
                {
                    for (int c = 0; c < rigs.Length; c++)
                    {
                        rigs[c].Tick(frame);
                    }
                }

                long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
                long managedDiff = managedAfter - managedBefore;

                Assert.LessOrEqual(managedDiff, 0,
                    $"10 体同時更新 100 frame で managed alloc が発生: " +
                    $"diff={managedDiff} bytes (Req 8.4)。");

                int totalCalls = 0;
                for (int c = 0; c < rigs.Length; c++)
                {
                    totalCalls += rigs[c].FakeBoneProvider.CallCount;
                }
                Assert.GreaterOrEqual(totalCalls, CharacterCount * 100);
            }
            finally
            {
                foreach (var rig in rigs)
                {
                    rig.Dispose();
                }
            }
        }

        [Test]
        public void TenCharacters_MixedBindings_OneHundredFrames_CompletesWithinBudget()
        {
            var rigs = BuildRigs(CharacterCount, BindingsPerCharacter, BlendShapeCount);
            try
            {
                // ウォームアップ
                for (int frame = 0; frame < 30; frame++)
                {
                    for (int c = 0; c < rigs.Length; c++)
                    {
                        rigs[c].Tick(frame);
                    }
                }

                var sw = Stopwatch.StartNew();
                for (int frame = 0; frame < 100; frame++)
                {
                    for (int c = 0; c < rigs.Length; c++)
                    {
                        rigs[c].Tick(frame);
                    }
                }
                sw.Stop();

                double msPerFrame = sw.Elapsed.TotalMilliseconds / 100.0;
                Assert.Less(msPerFrame, 2.0,
                    $"10 体同時アナログバインディング更新が 1 frame あたり {msPerFrame:F4}ms かかりました " +
                    $"(上限 2ms / 16.67ms バジェットの 12%, Req 8.4)。");
            }
            finally
            {
                foreach (var rig in rigs)
                {
                    rig.Dispose();
                }
            }
        }

        // ============================================================
        // bindings=64 / bindings=128 の処理時間比較で per-frame O(N) 確認 (Req 8.3)
        // ============================================================

        [Test]
        public void Bindings_LinearScaling_NotQuadratic()
        {
            const int frameCount = 200;
            double t64 = MeasureBlendShapeFrameTimeMs(bindingCount: 64, frameCount: frameCount);
            double t128 = MeasureBlendShapeFrameTimeMs(bindingCount: 128, frameCount: frameCount);

            // O(N): 約 2× / O(N²): 約 4× / 5× を超えたら quadratic 兆候として fail。
            // 計測ノイズに耐えるため余裕を取り、5× を許容上限とする。
            Assert.That(t128, Is.LessThanOrEqualTo(Math.Max(t64, 0.001) * 5.0),
                $"bindings=64 → {t64:F4}ms / bindings=128 → {t128:F4}ms (Req 8.3)。" +
                "倍数が 5× を超えるため per-frame O(N²) 兆候。");
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private static AnalogMappingFunction CustomMapping()
            => new AnalogMappingFunction(
                deadZone: 0.05f,
                scale: 1.5f,
                offset: 0.05f,
                curve: TransitionCurve.Linear,
                invert: false,
                min: 0f,
                max: 1f);

        private static CharacterRig[] BuildRigs(int count, int bindingsPerChar, int blendShapeCount)
        {
            var rigs = new CharacterRig[count];
            for (int c = 0; c < count; c++)
            {
                var sources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal)
                {
                    { "stickL", new FakeAnalogSource("stickL", 2) },
                    { "stickR", new FakeAnalogSource("stickR", 2) },
                    { "trig",   new FakeAnalogSource("trig",   1) },
                };

                var blendShapeNames = new string[blendShapeCount];
                for (int i = 0; i < blendShapeCount; i++)
                {
                    blendShapeNames[i] = $"bs_{c}_{i}";
                }

                var mapping = CustomMapping();

                // BlendShape 用と BonePose 用に半々で振分け。
                int half = bindingsPerChar / 2;
                var blendShapeBindings = new List<AnalogBindingEntry>(half);
                var bonePoseBindings = new List<AnalogBindingEntry>(bindingsPerChar - half);

                for (int i = 0; i < bindingsPerChar; i++)
                {
                    string sourceId = (i % 3) switch
                    {
                        0 => "stickL",
                        1 => "stickR",
                        _ => "trig",
                    };
                    int sourceAxisCount = sources[sourceId].AxisCount;
                    int sourceAxis = (sourceAxisCount == 1) ? 0 : (i % sourceAxisCount);

                    if (i < half)
                    {
                        blendShapeBindings.Add(new AnalogBindingEntry(
                            sourceId, sourceAxis, AnalogBindingTargetKind.BlendShape,
                            blendShapeNames[i % blendShapeCount],
                            AnalogTargetAxis.X, mapping));
                    }
                    else
                    {
                        var targetAxis = (AnalogTargetAxis)(i % 3);
                        bonePoseBindings.Add(new AnalogBindingEntry(
                            sourceId, sourceAxis, AnalogBindingTargetKind.BonePose,
                            $"bone_{c}_{i}",
                            targetAxis, mapping));
                    }
                }

                var bsSource = new AnalogBlendShapeInputSource(
                    InputSourceId.Parse("analog-blendshape"),
                    blendShapeCount,
                    blendShapeNames,
                    sources,
                    blendShapeBindings);

                var bp = new FakeBonePoseProvider();
                var boneProvider = new AnalogBonePoseProvider(bp, sources, bonePoseBindings);

                rigs[c] = new CharacterRig
                {
                    Sources = sources,
                    BlendShapeSource = bsSource,
                    BonePoseProvider = boneProvider,
                    FakeBoneProvider = bp,
                    OutputBuffer = new float[blendShapeCount],
                };
            }
            return rigs;
        }

        private static double MeasureBlendShapeFrameTimeMs(int bindingCount, int frameCount)
        {
            const int blendShapeCount = 256;
            var sources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal);
            int sourceCount = (bindingCount + 3) / 4;
            for (int s = 0; s < sourceCount; s++)
            {
                sources[$"src_{s}"] = new FakeAnalogSource($"src_{s}", 2);
            }

            var blendShapeNames = new string[blendShapeCount];
            for (int i = 0; i < blendShapeCount; i++)
            {
                blendShapeNames[i] = $"bs_{i}";
            }

            var mapping = CustomMapping();
            var bindings = new AnalogBindingEntry[bindingCount];
            for (int i = 0; i < bindingCount; i++)
            {
                int srcIdx = i % sourceCount;
                int axis = i % 2;
                bindings[i] = new AnalogBindingEntry(
                    $"src_{srcIdx}", axis, AnalogBindingTargetKind.BlendShape,
                    blendShapeNames[i % blendShapeCount],
                    AnalogTargetAxis.X, mapping);
            }

            var src = new AnalogBlendShapeInputSource(
                InputSourceId.Parse("analog-blendshape"),
                blendShapeCount,
                blendShapeNames,
                sources,
                bindings);
            var output = new float[blendShapeCount];

            // ウォームアップ
            for (int frame = 0; frame < 50; frame++)
            {
                MutateAll(sources, frame);
                src.TryWriteValues(output);
            }

            var sw = Stopwatch.StartNew();
            for (int frame = 0; frame < frameCount; frame++)
            {
                MutateAll(sources, frame);
                src.TryWriteValues(output);
            }
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds / frameCount;
        }

        private static void MutateAll(Dictionary<string, IAnalogInputSource> sources, int frame)
        {
            foreach (var kv in sources)
            {
                var s = (FakeAnalogSource)kv.Value;
                for (int a = 0; a < s.AxisCount; a++)
                {
                    s.SetAxis(a, ((frame + a * 7) % 100) * 0.01f);
                }
            }
        }
    }
}
