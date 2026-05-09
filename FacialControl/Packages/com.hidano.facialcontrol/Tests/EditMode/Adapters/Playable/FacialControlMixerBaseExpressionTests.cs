using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Playable
{
    [TestFixture]
    public class FacialControlMixerBaseExpressionTests
    {
        private const float Tolerance = 1e-6f;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly List<Object> _objects = new List<Object>();
        private PlayableGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = PlayableGraph.Create("FacialControlMixerBaseExpressionTests");
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
        }

        [Test]
        public void ComputeOutput_BaseExpressionClipNull_InitializesOutputToZero()
        {
            var profile = CreateProfile(new BaseExpressionSerializable
            {
                animationClip = null,
                cachedSnapshot = CreateSnapshot(
                    ("BrowDown", 0.4f),
                    ("MouthSmile", 0.8f)),
            });

            var mixer = CreateMixer(
                profile,
                new[] { "BrowDown", "MouthSmile" },
                aggregator: null,
                priorities: Array.Empty<int>(),
                layerWeights: Array.Empty<float>());

            mixer.ComputeOutput();

            AssertOutput(mixer, 0f, 0f);
        }

        [Test]
        public void ComputeOutput_BaseExpressionSnapshotValues_InitializesOutputPerBlendShape()
        {
            var clip = Track(new AnimationClip { name = "BaseExpression_WithSnapshot" });
            var profile = CreateProfile(new BaseExpressionSerializable
            {
                animationClip = clip,
                cachedSnapshot = CreateSnapshot(
                    ("MouthSmile", 0.75f),
                    ("BrowDown", 0.25f)),
            });

            var mixer = CreateMixer(
                profile,
                new[] { "BrowDown", "EyeBlink", "MouthSmile" },
                aggregator: null,
                priorities: Array.Empty<int>(),
                layerWeights: Array.Empty<float>());

            mixer.ComputeOutput();

            AssertOutput(mixer, 0.25f, 0f, 0.75f);
        }

        [Test]
        public void ComputeOutput_AllLayerMasksFalseForIndex_KeepsBaseValue()
        {
            var clip = Track(new AnimationClip { name = "BaseExpression_MaskedLayer" });
            var profile = CreateProfile(new BaseExpressionSerializable
            {
                animationClip = clip,
                cachedSnapshot = CreateSnapshot(
                    ("BrowDown", 0.2f),
                    ("EyeBlink", 0.35f),
                    ("MouthSmile", 0.6f)),
            });

            var source = new MaskedValueSource(
                id: "mouth-source",
                blendShapeCount: 3,
                values: new[] { 1f, 1f, 1f },
                contributeIndices: new[] { 2 });
            var aggregator = CreateAggregator(blendShapeCount: 3, source);

            var mixer = CreateMixer(
                profile,
                new[] { "BrowDown", "EyeBlink", "MouthSmile" },
                aggregator,
                priorities: new[] { 0 },
                layerWeights: new[] { 1f });

            mixer.ComputeOutput();

            AssertOutput(mixer, 0.2f, 0.35f, 1f);
        }

        private FacialControlMixer CreateMixer(
            TestCharacterProfileSO profile,
            string[] blendShapeNames,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights)
        {
            MethodInfo createMethod = FindCreateMethod(requireAggregator: aggregator != null);
            Assert.That(createMethod, Is.Not.Null,
                aggregator == null
                    ? "FacialControlMixer は FacialCharacterProfileSO の BaseExpression を受け取る Create 経路を公開する必要がある。"
                    : "FacialControlMixer は FacialCharacterProfileSO と LayerInputSourceAggregator を受け取る Create 経路を公開する必要がある。");

            object[] args = BuildCreateArguments(
                createMethod,
                profile,
                blendShapeNames,
                aggregator,
                priorities,
                layerWeights);

            var playable = (ScriptPlayable<FacialControlMixer>)createMethod.Invoke(null, args);
            return playable.GetBehaviour();
        }

        private MethodInfo FindCreateMethod(bool requireAggregator)
        {
            MethodInfo fallback = null;
            var methods = typeof(FacialControlMixer).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != "Create")
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 3
                    || parameters[0].ParameterType != typeof(PlayableGraph)
                    || parameters[1].ParameterType != typeof(string[]))
                {
                    continue;
                }

                bool hasProfile = false;
                bool hasAggregator = false;
                for (int p = 0; p < parameters.Length; p++)
                {
                    var parameterType = parameters[p].ParameterType;
                    if (parameterType.IsAssignableFrom(typeof(FacialCharacterProfileSO))
                        || typeof(FacialCharacterProfileSO).IsAssignableFrom(parameterType))
                    {
                        hasProfile = true;
                    }
                    if (parameterType == typeof(LayerInputSourceAggregator))
                    {
                        hasAggregator = true;
                    }
                }

                if (!hasProfile)
                {
                    continue;
                }

                if (hasAggregator)
                {
                    return method;
                }

                fallback = method;
            }

            return requireAggregator ? null : fallback;
        }

        private object[] BuildCreateArguments(
            MethodInfo method,
            TestCharacterProfileSO profile,
            string[] blendShapeNames,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType == typeof(PlayableGraph))
                {
                    args[i] = _graph;
                }
                else if (parameterType == typeof(string[]))
                {
                    args[i] = blendShapeNames;
                }
                else if (parameterType.IsAssignableFrom(typeof(TestCharacterProfileSO))
                         || parameterType.IsAssignableFrom(typeof(FacialCharacterProfileSO)))
                {
                    args[i] = profile;
                }
                else if (parameterType == typeof(LayerInputSourceAggregator))
                {
                    args[i] = aggregator;
                }
                else if (parameterType == typeof(int[]))
                {
                    args[i] = priorities;
                }
                else if (parameterType == typeof(float[]))
                {
                    args[i] = layerWeights;
                }
                else if (parameters[i].HasDefaultValue)
                {
                    args[i] = parameters[i].DefaultValue;
                }
                else
                {
                    Assert.Fail(
                        "FacialControlMixer.Create の BaseExpression 対応 overload に未対応の必須引数があります: "
                        + parameterType.FullName);
                }
            }

            return args;
        }

        private LayerInputSourceAggregator CreateAggregator(int blendShapeCount, IInputSource source)
        {
            var profile = new FacialProfile(
                "2.0",
                new[] { new LayerDefinition("masked-layer", priority: 0, ExclusionMode.LastWins) });
            var bindings = new List<(int layerIdx, int sourceIdx, IInputSource source)>
            {
                (0, 0, source),
            };

            var registry = new LayerInputSourceRegistry(profile, blendShapeCount, bindings);
            var weightBuffer = new LayerInputSourceWeightBuffer(registry.LayerCount, registry.MaxSourcesPerLayer);
            weightBuffer.SetWeight(0, 0, 1f);

            _disposables.Add(registry);
            _disposables.Add(weightBuffer);

            return new LayerInputSourceAggregator(registry, weightBuffer, blendShapeCount);
        }

        private TestCharacterProfileSO CreateProfile(BaseExpressionSerializable baseExpression)
        {
            var profile = Track(ScriptableObject.CreateInstance<TestCharacterProfileSO>());
            profile.SetBaseExpression(baseExpression);
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
            var dto = new ExpressionSnapshotDto
            {
                blendShapes = new List<BlendShapeSnapshotDto>(values.Length),
            };

            for (int i = 0; i < values.Length; i++)
            {
                dto.blendShapes.Add(new BlendShapeSnapshotDto
                {
                    rendererPath = "Face",
                    name = values[i].name,
                    value = values[i].value,
                });
            }

            return dto;
        }

        private static void AssertOutput(FacialControlMixer mixer, params float[] expected)
        {
            Assert.That(mixer.OutputWeights.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(mixer.OutputWeights[i], Is.EqualTo(expected[i]).Within(Tolerance),
                    $"index {i} の出力が期待値と一致すること。");
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
                int blendShapeCount,
                float[] values,
                int[] contributeIndices)
            {
                Id = id;
                BlendShapeCount = blendShapeCount;
                _values = values;
                ContributeMask = new BitArray(blendShapeCount, false);
                for (int i = 0; i < contributeIndices.Length; i++)
                {
                    int index = contributeIndices[i];
                    if ((uint)index < (uint)blendShapeCount)
                    {
                        ContributeMask[index] = true;
                    }
                }
            }

            public string Id { get; }
            public InputSourceType Type => InputSourceType.ValueProvider;
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
