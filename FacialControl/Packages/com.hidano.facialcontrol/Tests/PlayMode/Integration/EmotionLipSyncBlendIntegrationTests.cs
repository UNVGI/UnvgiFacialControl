#if false
using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    [TestFixture]
    public class EmotionLipSyncBlendIntegrationTests
    {
        private const float Tolerance = 1e-6f;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly List<Object> _objects = new List<Object>();
        private GameObject _gameObject;
        private Animator _animator;
        private PlayableGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("EmotionLipSyncBlendIntegrationTests");
            _animator = _gameObject.AddComponent<Animator>();
            _graph = PlayableGraph.Create("EmotionLipSyncBlendIntegrationTests");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        }

        [TearDown]
        public void TearDown()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }
            _disposables.Clear();

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }
            _objects.Clear();

            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
                _gameObject = null;
                _animator = null;
            }
        }

        [UnityTest]
        public IEnumerator Evaluate_BaseExpressionFixedAngryFace_BlendsEmotionAndLipSyncByContributionMask()
        {
            string[] blendShapeNames =
            {
                "EyeBlinkLeft",
                "BrowDownLeft",
                "MouthSmileLeft",
                "MouthA",
                "CheekPuff"
            };

            var baseClip = Track(new AnimationClip { name = "BaseExpression_FixedAngryFace" });
            var characterProfile = CreateCharacterProfile(
                baseClip,
                CreateSnapshot(
                    ("EyeBlinkLeft", 0.10f),
                    ("BrowDownLeft", 0.25f),
                    ("MouthSmileLeft", 0.40f),
                    ("MouthA", 0.05f),
                    ("CheekPuff", 0.30f)));

            var aggregator = CreateAggregator(
                blendShapeNames.Length,
                new MaskedValueSource(
                    "emotion",
                    InputSourceType.ExpressionTrigger,
                    new[] { 0.70f, 0.60f, 0.20f, 1.00f, 1.00f },
                    new[] { 0, 1, 2 }),
                new MaskedValueSource(
                    "lipsync",
                    InputSourceType.ValueProvider,
                    new[] { 1.00f, 1.00f, 0.10f, 0.90f, 1.00f },
                    new[] { 2, 3 }));

            var mixer = CreateMixer(blendShapeNames, characterProfile, aggregator);

            _graph.Play();
            _graph.Evaluate(0.016f);
            yield return null;

            AssertOutput(mixer, 0.70f, 0.60f, 0.10f, 0.90f, 0.30f);
        }

        [UnityTest]
        public IEnumerator Evaluate_BaseExpressionEmpty_BlendsEmotionAndLipSyncWithZeroDefault()
        {
            string[] blendShapeNames =
            {
                "EyeBlinkLeft",
                "BrowDownLeft",
                "MouthSmileLeft",
                "MouthA",
                "CheekPuff"
            };

            var characterProfile = CreateCharacterProfile(
                null,
                BaseExpressionSerializable.CreateEmptySnapshot());

            var aggregator = CreateAggregator(
                blendShapeNames.Length,
                new MaskedValueSource(
                    "emotion",
                    InputSourceType.ExpressionTrigger,
                    new[] { 0.70f, 0.60f, 0.20f, 1.00f, 1.00f },
                    new[] { 0, 1, 2 }),
                new MaskedValueSource(
                    "lipsync",
                    InputSourceType.ValueProvider,
                    new[] { 1.00f, 1.00f, 0.10f, 0.90f, 1.00f },
                    new[] { 2, 3 }));

            var mixer = CreateMixer(blendShapeNames, characterProfile, aggregator);

            _graph.Play();
            _graph.Evaluate(0.016f);
            yield return null;

            AssertOutput(mixer, 0.70f, 0.60f, 0.10f, 0.90f, 0.00f);
        }

        private FacialControlMixer CreateMixer(
            string[] blendShapeNames,
            TestCharacterProfileSO characterProfile,
            LayerInputSourceAggregator aggregator)
        {
            var mixerPlayable = FacialControlMixer.Create(
                _graph,
                blendShapeNames,
                characterProfile,
                aggregator,
                new[] { 0, 1 },
                new[] { 1.0f, 1.0f });
            var output = AnimationPlayableOutput.Create(
                _graph,
                "EmotionLipSyncBlendIntegrationOutput",
                _animator);
            output.SetSourcePlayable(mixerPlayable);
            return mixerPlayable.GetBehaviour();
        }

        private LayerInputSourceAggregator CreateAggregator(
            int blendShapeCount,
            IInputSource emotionSource,
            IInputSource lipSyncSource)
        {
            var profile = CreateLayerProfile();
            var bindings = new List<(int layerIdx, int sourceIdx, IInputSource source)>
            {
                (0, 0, emotionSource),
                (1, 0, lipSyncSource),
            };

            var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            var weightBuffer = new LayerInputSourceWeightBuffer(registry.LayerCount, registry.MaxSourcesPerLayer);
            weightBuffer.SetWeight(0, 0, 1.0f);
            weightBuffer.SetWeight(1, 0, 1.0f);

            _disposables.Add(registry);
            _disposables.Add(weightBuffer);

            return new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
        }

        private static FacialProfile CreateLayerProfile()
        {
            return new FacialProfile(
                "2.0",
                new[]
                {
                    new LayerDefinition("emotion", priority: 0, ExclusionMode.LastWins),
                    new LayerDefinition("lipsync", priority: 1, ExclusionMode.Blend),
                });
        }

        private TestCharacterProfileSO CreateCharacterProfile(
            AnimationClip animationClip,
            ExpressionSnapshotDto cachedSnapshot)
        {
            var profile = Track(ScriptableObject.CreateInstance<TestCharacterProfileSO>());
            profile.SetBaseExpression(new BaseExpressionSerializable
            {
                animationClip = animationClip,
                cachedSnapshot = cachedSnapshot,
            });
            return profile;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }

        private static ExpressionSnapshotDto CreateSnapshot(params (string name, float value)[] values)
        {
            var snapshot = new ExpressionSnapshotDto
            {
                blendShapes = new List<BlendShapeSnapshotDto>(values.Length),
            };

            for (int i = 0; i < values.Length; i++)
            {
                snapshot.blendShapes.Add(new BlendShapeSnapshotDto
                {
                    rendererPath = "Face",
                    name = values[i].name,
                    value = values[i].value,
                });
            }

            return snapshot;
        }

        private static void AssertOutput(FacialControlMixer mixer, params float[] expected)
        {
            Assert.That(mixer.OutputWeights.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(mixer.OutputWeights[i], Is.EqualTo(expected[i]).Within(Tolerance),
                    $"BlendShape output at index {i} must match.");
            }
        }

        private sealed class TestCharacterProfileSO : FacialCharacterProfileSO
        {
            public void SetBaseExpression(BaseExpressionSerializable baseExpression)
            {
                _baseExpression = baseExpression;
            }
        }

        private sealed class MaskedValueSource : IInputSource
        {
            private readonly float[] _values;

            public MaskedValueSource(
                string id,
                InputSourceType type,
                float[] values,
                int[] contributeIndices)
            {
                Id = id;
                Type = type;
                BlendShapeCount = values.Length;
                _values = values;
                ContributeMask = new BitArray(BlendShapeCount, false);
                for (int i = 0; i < contributeIndices.Length; i++)
                {
                    int index = contributeIndices[i];
                    if ((uint)index < (uint)BlendShapeCount)
                    {
                        ContributeMask[index] = true;
                    }
                }
            }

            public string Id { get; }
            public InputSourceType Type { get; }
            public int BlendShapeCount { get; }
            public BitArray ContributeMask { get; }

            public void Tick(float deltaTime)
            {
            }

            public bool TryWriteValues(Span<float> output)
            {
                int len = Math.Min(output.Length, _values.Length);
                for (int i = 0; i < len; i++)
                {
                    output[i] = _values[i];
                }
                return true;
            }
        }
    }
}
#endif
