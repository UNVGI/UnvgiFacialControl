using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.AutoExport;
using Hidano.FacialControl.Editor.Sampling;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.AutoExport
{
    [TestFixture]
    public class FacialCharacterProfileExporter_BaseExpressionBakeTests
    {
        [Test]
        public void SampleAnimationClipsIntoCachedSnapshots_BaseExpressionClip_BakesCachedSnapshot()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialCharacterProfileSO>();
            var clip = new AnimationClip { name = "BaseExpression_BakeClip" };
            var sampler = new RecordingSampler(CreateSnapshot(
                "base-expression",
                new BlendShapeSnapshot("Body/Face", "Brow_Angry", 64.5f),
                new BlendShapeSnapshot("Body/Face", "Mouth_Frown", 22.0f)));

            try
            {
                so.BaseExpression.animationClip = clip;
                so.BaseExpression.cachedSnapshot = BaseExpressionSerializable.CreateEmptySnapshot();

                FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);

                ExpressionSnapshotDto snapshot = so.BaseExpression.cachedSnapshot;
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(snapshot.blendShapes, Has.Count.EqualTo(2));
                Assert.That(snapshot.blendShapes[0].rendererPath, Is.EqualTo("Body/Face"));
                Assert.That(snapshot.blendShapes[0].name, Is.EqualTo("Brow_Angry"));
                Assert.That(snapshot.blendShapes[0].value, Is.EqualTo(64.5f).Within(1e-6f));
                Assert.That(snapshot.blendShapes[1].name, Is.EqualTo("Mouth_Frown"));
                Assert.That(snapshot.blendShapes[1].value, Is.EqualTo(22.0f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void SampleAnimationClipsIntoCachedSnapshots_BaseExpressionClipNull_RegeneratesEmptySnapshot()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialCharacterProfileSO>();
            var sampler = new RecordingSampler(CreateSnapshot(
                "unused",
                new BlendShapeSnapshot("Body/Face", "ShouldNotBeSampled", 100f)));

            try
            {
                so.BaseExpression.animationClip = null;
                so.BaseExpression.cachedSnapshot = new ExpressionSnapshotDto
                {
                    blendShapes = new List<BlendShapeSnapshotDto>
                    {
                        new BlendShapeSnapshotDto
                        {
                            rendererPath = "Body/Face",
                            name = "StaleSmile",
                            value = 100f,
                        },
                    },
                    bones = new List<BoneSnapshotDto>(),
                    rendererPaths = new List<string> { "Body/Face" },
                };

                FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);

                Assert.That(sampler.SampleSnapshotCallCount, Is.EqualTo(0));
                Assert.That(so.BaseExpression.cachedSnapshot, Is.Not.Null);
                Assert.That(so.BaseExpression.cachedSnapshot.blendShapes, Is.Not.Null);
                Assert.That(so.BaseExpression.cachedSnapshot.blendShapes, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void SampleAnimationClipsIntoCachedSnapshots_BaseExpressionClip_UsesInjectedSampler()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialCharacterProfileSO>();
            var clip = new AnimationClip { name = "BaseExpression_InjectedSamplerClip" };
            var sampler = new RecordingSampler(CreateSnapshot(
                "base-expression",
                new BlendShapeSnapshot("Body/Face", "SamplerOnlyShape", 12.5f)));

            try
            {
                so.BaseExpression.animationClip = clip;
                so.BaseExpression.cachedSnapshot = BaseExpressionSerializable.CreateEmptySnapshot();

                FacialCharacterProfileExporter.SampleAnimationClipsIntoCachedSnapshots(so, sampler);

                Assert.That(sampler.SampleSnapshotCallCount, Is.EqualTo(1));
                Assert.That(sampler.LastClip, Is.SameAs(clip));
                Assert.That(so.BaseExpression.cachedSnapshot.blendShapes, Has.Count.EqualTo(1));
                Assert.That(so.BaseExpression.cachedSnapshot.blendShapes[0].name, Is.EqualTo("SamplerOnlyShape"));
                Assert.That(so.BaseExpression.cachedSnapshot.blendShapes[0].value, Is.EqualTo(12.5f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(so);
            }
        }

        private static ExpressionSnapshot CreateSnapshot(string id, params BlendShapeSnapshot[] blendShapes)
        {
            return new ExpressionSnapshot(
                id,
                Expression.DefaultTransitionDuration,
                TransitionCurvePreset.Linear,
                blendShapes,
                Array.Empty<BoneSnapshot>(),
                CollectRendererPaths(blendShapes));
        }

        private static string[] CollectRendererPaths(IReadOnlyList<BlendShapeSnapshot> blendShapes)
        {
            var paths = new List<string>();
            for (int i = 0; i < blendShapes.Count; i++)
            {
                string path = blendShapes[i].RendererPath ?? string.Empty;
                if (!paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths.ToArray();
        }

        private sealed class RecordingSampler : IExpressionAnimationClipSampler
        {
            private readonly ExpressionSnapshot _snapshot;

            public int SampleSnapshotCallCount { get; private set; }
            public AnimationClip LastClip { get; private set; }

            public RecordingSampler(ExpressionSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public ExpressionSnapshot SampleSnapshot(string snapshotId, AnimationClip clip)
            {
                SampleSnapshotCallCount++;
                LastClip = clip;
                return _snapshot;
            }

            public ClipSummary SampleSummary(AnimationClip clip)
            {
                return new ClipSummary(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Expression.DefaultTransitionDuration,
                    TransitionCurvePreset.Linear);
            }
        }
    }
}
