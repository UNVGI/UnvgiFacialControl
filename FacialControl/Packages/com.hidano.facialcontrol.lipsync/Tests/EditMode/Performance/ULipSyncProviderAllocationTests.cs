using System;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Tests.Shared;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Performance
{
    [TestFixture]
    public class ULipSyncProviderAllocationTests
    {
        private const int Iterations = 10000;
        private const double FrameDt = 1.0 / 60.0;

        [Test]
        public void OnLipSyncUpdateAndGetLipSyncValues_TenThousandIterations_ZeroBytes()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            var snapshots = new[]
            {
                new PhonemeSnapshot("A", new[] { 0.50f, 0.00f, 0.25f, 0.10f }),
                new PhonemeSnapshot("I", new[] { 0.20f, 0.40f, 0.00f, 0.30f }),
                new PhonemeSnapshot("O", new[] { 0.00f, 0.10f, 0.75f, 0.20f }),
            };
            using var provider = new ULipSyncProvider(
                source,
                snapshots,
                blendShapeCount: 4,
                smoothness: ULipSyncProvider.DefaultSmoothness,
                timeProvider: time);
            var output = new float[4];
            var info = new uLipSync.LipSyncInfo
            {
                volume = 1f,
                phonemeRatios = new Dictionary<string, float>(3)
                {
                    { "A", 0.70f },
                    { "I", 0.15f },
                    { "O", 0.35f },
                },
            };

            // warmup: SmoothDamp の内部状態を収束させ、計測中の状態遷移を除外する。
            for (int i = 0; i < 128; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            ForceFullCollection();
            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1,
                ProfilerRecorderOptions.SumAllSamplesInFrame
                    | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            for (int i = 0; i < Iterations; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            long gcAllocBytes = recorder.LastValue;

            Assert.That(gcAllocBytes, Is.EqualTo(0L),
                "ULipSyncProvider hot path reported GC.Alloc: " + gcAllocBytes + " bytes.");
            Assert.That(output[0], Is.GreaterThan(0f));
        }

        [Test]
        public void Update_WithExpressionPhonemeEntrySnapshots_ZeroGCPerFrame()
        {
            var source = new FakeULipSyncEventSource();
            var time = new ManualTimeProvider();
            PhonemeSnapshot[] snapshots = BuildExpressionSnapshots();
            using var provider = new ULipSyncProvider(
                source,
                snapshots,
                blendShapeCount: 4,
                smoothness: ULipSyncProvider.DefaultSmoothness,
                timeProvider: time);
            var output = new float[4];
            var info = new uLipSync.LipSyncInfo
            {
                volume = 1f,
                phonemeRatios = new Dictionary<string, float>(1)
                {
                    { "A", 1f },
                },
            };

            for (int i = 0; i < 128; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            ForceFullCollection();
            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1,
                ProfilerRecorderOptions.SumAllSamplesInFrame
                    | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            for (int i = 0; i < Iterations; i++)
            {
                time.UnscaledTimeSeconds += FrameDt;
                source.Invoke(info);
                provider.GetLipSyncValues(output);
            }

            long gcAllocBytes = recorder.LastValue;

            Assert.That(gcAllocBytes, Is.EqualTo(0L),
                "ULipSyncProvider ExpressionPhonemeEntry snapshot hot path reported GC.Alloc: "
                + gcAllocBytes + " bytes.");
            Assert.That(output[0], Is.GreaterThan(0f));
        }

        private static PhonemeSnapshot[] BuildExpressionSnapshots()
        {
            var entry = new ExpressionPhonemeEntry
            {
                PhonemeId = "A",
                MaxWeight = 100f,
            };
            SetPrivateField(entry, "_expressionId", "expr-a");
            var binding = new ULipSyncAdapterBinding();
            binding.Configure(
                default,
                null,
                new PhonemeEntryBase[] { entry });

            var profile = new FacialProfile(
                "1.0",
                expressions: new[]
                {
                    new Expression(
                        "expr-a",
                        "A",
                        "lipsync",
                        blendShapeValues: new[]
                        {
                            new BlendShapeMapping("Mouth_A", 0.75f),
                            new BlendShapeMapping("Mouth_I", 0.25f),
                            new BlendShapeMapping("Mouth_U", 0.50f),
                            new BlendShapeMapping("Mouth_O", 0.10f),
                        }),
                });
            var host = new GameObject("ULipSyncProviderAllocationTestsHost");
            try
            {
                var ctx = new AdapterBuildContext(
                    profile,
                    new[] { "Mouth_A", "Mouth_I", "Mouth_U", "Mouth_O" },
                    new InputSourceRegistry(),
                    new FacialOutputBus(),
                    new ManualTimeProvider(),
                    host,
                    null);
                PhonemeSnapshot[] snapshots = InvokeBuildSnapshots(binding, ctx);
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].PhonemeId, Is.EqualTo("A"));
                return snapshots;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static PhonemeSnapshot[] InvokeBuildSnapshots(
            ULipSyncAdapterBinding binding,
            AdapterBuildContext ctx)
        {
            MethodInfo method = typeof(ULipSyncAdapterBinding).GetMethod(
                "BuildSnapshots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            try
            {
                return (PhonemeSnapshot[])method.Invoke(binding, new object[] { ctx });
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
