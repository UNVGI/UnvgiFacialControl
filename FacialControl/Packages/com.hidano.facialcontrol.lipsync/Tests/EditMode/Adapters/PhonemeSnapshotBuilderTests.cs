using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Adapters
{
    [TestFixture]
    public class PhonemeSnapshotBuilderTests
    {
        private readonly List<UnityEngine.Object> _createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Build_BlendShapeEntryDirectFill_FillsCorrectIndex()
        {
            GameObject host = CreateHost();
            AddRenderer(host, "FaceMesh", "Mouth_A", "Mouth_I");
            ULipSyncAdapterBinding binding = CreateBinding(
                new BlendShapePhonemeEntry
                {
                    PhonemeId = "A",
                    BlendShapeName = "Mouth_A",
                    MaxWeight = 80f,
                });

            PhonemeSnapshot[] snapshots = BuildSnapshots(
                binding,
                CreateContext(host, "Mouth_A", "Mouth_I"));

            Assert.That(snapshots, Has.Length.EqualTo(1));
            Assert.That(snapshots[0].PhonemeId, Is.EqualTo("A"));
            AssertWeights(snapshots[0].Weights, 0.8f, 0f);
        }

        [Test]
        public void Build_AnimationClipEntryTimeZero_ExtractsBlendShapeWeights()
        {
            GameObject host = CreateHost();
            SkinnedMeshRenderer renderer = AddRenderer(host, "FaceMesh", "Mouth_A", "Mouth_I");
            renderer.SetBlendShapeWeight(0, 13f);
            renderer.SetBlendShapeWeight(1, 33f);

            AnimationClip clip = CreateClip(
                Curve("FaceMesh", "Mouth_A", 60f));
            ULipSyncAdapterBinding binding = CreateBinding(
                new AnimationClipPhonemeEntry
                {
                    PhonemeId = "A",
                    Clip = clip,
                    MaxWeight = 50f,
                });

            PhonemeSnapshot[] snapshots = BuildSnapshots(
                binding,
                CreateContext(host, "Mouth_A", "Mouth_I"));

            Assert.That(snapshots, Has.Length.EqualTo(1));
            AssertWeights(snapshots[0].Weights, 0.3f, 0f);
        }

        [Test]
        public void Build_MixedEntries_BothApplyConsistently()
        {
            GameObject host = CreateHost();
            AddRenderer(host, "FaceMesh", "Mouth_A", "Mouth_I");

            AnimationClip clip = CreateClip(
                Curve("FaceMesh", "Mouth_I", 40f));
            ULipSyncAdapterBinding binding = CreateBinding(
                new BlendShapePhonemeEntry
                {
                    PhonemeId = "A",
                    BlendShapeName = "Mouth_A",
                    MaxWeight = 100f,
                },
                new AnimationClipPhonemeEntry
                {
                    PhonemeId = "I",
                    Clip = clip,
                    MaxWeight = 25f,
                });

            PhonemeSnapshot[] snapshots = BuildSnapshots(
                binding,
                CreateContext(host, "Mouth_A", "Mouth_I"));

            Assert.That(snapshots, Has.Length.EqualTo(2));
            Assert.That(snapshots[0].PhonemeId, Is.EqualTo("A"));
            AssertWeights(snapshots[0].Weights, 1f, 0f);
            Assert.That(snapshots[1].PhonemeId, Is.EqualTo("I"));
            AssertWeights(snapshots[1].Weights, 0f, 0.1f);
        }

        [Test]
        public void Build_UnresolvedBlendShapeName_LogsWarningAndSkips()
        {
            GameObject host = CreateHost();
            AddRenderer(host, "FaceMesh", "Mouth_A");
            ULipSyncAdapterBinding binding = CreateBinding(
                new BlendShapePhonemeEntry
                {
                    PhonemeId = "A",
                    BlendShapeName = "MissingBlendShape",
                    MaxWeight = 100f,
                });

            LogAssert.Expect(
                LogType.Warning,
                new Regex("ULipSyncAdapterBinding.*MissingBlendShape.*could not be resolved"));

            PhonemeSnapshot[] snapshots = BuildSnapshots(binding, CreateContext(host, "Mouth_A"));

            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void Build_AfterAnimationClipSampling_RestoresSmrWeights()
        {
            GameObject host = CreateHost();
            SkinnedMeshRenderer face = AddRenderer(host, "FaceMesh", "Mouth_A");
            SkinnedMeshRenderer accessory = AddRenderer(host, "AccessoryMesh", "AccessorySmile");
            face.SetBlendShapeWeight(0, 13f);
            accessory.SetBlendShapeWeight(0, 45f);

            AnimationClip clip = CreateClip(
                Curve("FaceMesh", "Mouth_A", 80f),
                Curve("AccessoryMesh", "AccessorySmile", 90f));
            ULipSyncAdapterBinding binding = CreateBinding(
                new AnimationClipPhonemeEntry
                {
                    PhonemeId = "A",
                    Clip = clip,
                    MaxWeight = 100f,
                });

            PhonemeSnapshot[] snapshots = BuildSnapshots(binding, CreateContext(host, "Mouth_A"));

            Assert.That(snapshots, Has.Length.EqualTo(1));
            AssertWeights(snapshots[0].Weights, 0.8f);
            Assert.That(face.GetBlendShapeWeight(0), Is.EqualTo(13f).Within(1e-6f));
            Assert.That(accessory.GetBlendShapeWeight(0), Is.EqualTo(45f).Within(1e-6f));
        }

        [Test]
        public void Build_TargetMeshHintMissing_FallsBackToFirstSmr()
        {
            GameObject host = CreateHost();
            AddRenderer(host, "FirstMesh", "Mouth_A");
            AddRenderer(host, "SecondMesh", "Mouth_A");

            AnimationClip clip = CreateClip(
                Curve("FirstMesh", "Mouth_A", 20f),
                Curve("SecondMesh", "Mouth_A", 80f));
            ULipSyncAdapterBinding binding = CreateBinding(
                new AnimationClipPhonemeEntry
                {
                    PhonemeId = "A",
                    Clip = clip,
                    MaxWeight = 100f,
                });
            SetPrivateField(binding, "_targetMeshHint", "MissingMesh");

            LogAssert.Expect(
                LogType.Warning,
                new Regex("ULipSyncAdapterBinding.*Target mesh hint 'MissingMesh'.*not resolved"));

            PhonemeSnapshot[] snapshots = BuildSnapshots(binding, CreateContext(host, "Mouth_A"));

            Assert.That(snapshots, Has.Length.EqualTo(1));
            AssertWeights(snapshots[0].Weights, 0.2f);
        }

        private GameObject CreateHost()
        {
            var host = new GameObject("PhonemeSnapshotBuilderTestsHost");
            _createdObjects.Add(host);
            return host;
        }

        private SkinnedMeshRenderer AddRenderer(
            GameObject host,
            string childName,
            params string[] blendShapeNames)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(host.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(childName + "Mesh", blendShapeNames);
            return renderer;
        }

        private Mesh CreateMesh(string name, params string[] blendShapeNames)
        {
            var mesh = new Mesh { name = name };
            _createdObjects.Add(mesh);
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.triangles = new[] { 0, 1, 2 };

            var deltaVertices = new[]
            {
                Vector3.up * 0.01f,
                Vector3.up * 0.01f,
                Vector3.up * 0.01f,
            };
            var deltaNormals = new Vector3[deltaVertices.Length];
            var deltaTangents = new Vector3[deltaVertices.Length];
            for (int i = 0; i < blendShapeNames.Length; i++)
            {
                mesh.AddBlendShapeFrame(
                    blendShapeNames[i],
                    100f,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);
            }

            return mesh;
        }

        private AnimationClip CreateClip(params ClipCurve[] curves)
        {
            var clip = new AnimationClip();
            _createdObjects.Add(clip);
            for (int i = 0; i < curves.Length; i++)
            {
                clip.SetCurve(
                    curves[i].RelativePath,
                    typeof(SkinnedMeshRenderer),
                    "blendShape." + curves[i].BlendShapeName,
                    new AnimationCurve(new Keyframe(0f, curves[i].Weight)));
            }

            return clip;
        }

        private static ClipCurve Curve(
            string relativePath,
            string blendShapeName,
            float weight)
        {
            return new ClipCurve(relativePath, blendShapeName, weight);
        }

        private ULipSyncAdapterBinding CreateBinding(params PhonemeEntryBase[] entries)
        {
            var binding = new ULipSyncAdapterBinding();
            binding.Configure(
                default,
                null,
                entries);
            return binding;
        }

        private static AdapterBuildContext CreateContext(
            GameObject host,
            params string[] blendShapeNames)
        {
            return new AdapterBuildContext(
                new FacialProfile("1.0"),
                new List<string>(blendShapeNames),
                new InputSourceRegistry(),
                new UnityTimeProvider(),
                host,
                null);
        }

        private static PhonemeSnapshot[] BuildSnapshots(
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

        private static void AssertWeights(float[] actual, params float[] expected)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-6f), "index " + i);
            }
        }

        private readonly struct ClipCurve
        {
            public readonly string RelativePath;
            public readonly string BlendShapeName;
            public readonly float Weight;

            public ClipCurve(string relativePath, string blendShapeName, float weight)
            {
                RelativePath = relativePath;
                BlendShapeName = blendShapeName;
                Weight = weight;
            }
        }
    }
}
