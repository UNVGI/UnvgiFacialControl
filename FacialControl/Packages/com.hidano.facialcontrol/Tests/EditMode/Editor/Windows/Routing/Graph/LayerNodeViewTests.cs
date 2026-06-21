using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.AdapterBindings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class LayerNodeViewTests
    {
        private TestFacialCharacterProfileSO _profile;
        private SerializedObject _serializedObject;
        private RecordingWiringSerializedMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<TestFacialCharacterProfileSO>();
            _profile.Layers.Add(new LayerDefinitionSerializable
            {
                name = "overlay",
                priority = 1,
                exclusionMode = ExclusionMode.Blend,
                layerOverrideMask = new List<string> { "emotion" },
            });
            _serializedObject = new SerializedObject(_profile);
            _mapper = new RecordingWiringSerializedMapper();
        }

        [TearDown]
        public void TearDown()
        {
            if (_profile != null)
            {
                Object.DestroyImmediate(_profile);
                _profile = null;
            }
        }

        [Test]
        public void Construct_RendersEditableFieldsFromLayerData()
        {
            var view = CreateView();

            Assert.That(view.title, Is.EqualTo("overlay"));
            Assert.That(view.NameField.value, Is.EqualTo("overlay"));
            Assert.That(view.PriorityField.value, Is.EqualTo(1));
            Assert.That((ExclusionMode)view.ExclusionModeField.value, Is.EqualTo(ExclusionMode.Blend));
            Assert.That(view.OverrideMaskField.value, Is.EqualTo("emotion"));
            Assert.That(view.InputPort, Is.Not.Null);
            Assert.That(view.InputPort.direction, Is.EqualTo(UnityEditor.Experimental.GraphView.Direction.Input));
        }

        [Test]
        public void EditingFields_PropagatesUpdatedLayerProperties()
        {
            var view = CreateView();

            InvokeApplyLayerProperties(view, "overlay-updated", 1, ExclusionMode.Blend, new[] { "emotion" });
            InvokeApplyLayerProperties(view, "overlay-updated", 4, ExclusionMode.Blend, new[] { "emotion" });
            InvokeApplyLayerProperties(view, "overlay-updated", 4, ExclusionMode.LastWins, new[] { "emotion" });
            InvokeApplyLayerProperties(view, "overlay-updated", 4, ExclusionMode.LastWins, new[] { "emotion", "fx", "face" });

            Assert.That(_mapper.Invocations, Has.Count.EqualTo(4));
            Assert.That(_mapper.LastLayerIndex, Is.EqualTo(0));
            Assert.That(_mapper.LastLayerName, Is.EqualTo("overlay-updated"));
            Assert.That(_mapper.LastPriority, Is.EqualTo(4));
            Assert.That(_mapper.LastExclusionMode, Is.EqualTo(ExclusionMode.LastWins));
            CollectionAssert.AreEqual(new[] { "emotion", "fx", "face" }, _mapper.LastOverrideMask);
            Assert.That(view.title, Is.EqualTo("overlay-updated"));
            Assert.That(view.OverrideMaskField.value, Is.EqualTo("emotion, fx, face"));
        }

        [Test]
        public void SetLayerNodes_RebuildsGraphWithLayerNodeViews()
        {
            var graphView = new RoutingGraphView();
            var layerNodes = new[]
            {
                new LayerNodeData(0, "overlay", 1, ExclusionMode.Blend, new[] { "emotion" }),
                new LayerNodeData(1, "emotion", 2, ExclusionMode.LastWins, new string[0]),
            };

            graphView.SetLayerNodes(layerNodes, _serializedObject, _mapper);

            Assert.That(graphView.LayerNodeViews, Has.Count.EqualTo(2));
            Assert.That(graphView.LayerNodeViews[0].NameField.value, Is.EqualTo("overlay"));
            Assert.That(graphView.LayerNodeViews[1].PriorityField.value, Is.EqualTo(2));
        }

        private LayerNodeView CreateView()
        {
            return new LayerNodeView(
                new LayerNodeData(0, "overlay", 1, ExclusionMode.Blend, new[] { "emotion" }),
                _serializedObject,
                _mapper);
        }

        private static void InvokeApplyLayerProperties(
            LayerNodeView view,
            string layerName,
            int priority,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> overrideMask)
        {
            MethodInfo method = typeof(LayerNodeView).GetMethod(
                "ApplyLayerProperties",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, new object[] { layerName, priority, exclusionMode, overrideMask });
        }

        private sealed class RecordingWiringSerializedMapper : IWiringSerializedMapper
        {
            public List<string> Invocations { get; } = new List<string>();

            public int LastLayerIndex { get; private set; }

            public string LastLayerName { get; private set; } = string.Empty;

            public int LastPriority { get; private set; }

            public ExclusionMode LastExclusionMode { get; private set; }

            public IReadOnlyList<string> LastOverrideMask { get; private set; } = new string[0];

            public void AddDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
            }

            public void RemoveDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId)
            {
            }

            public void SetWeight(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
            }

            public void SetLayerProperties(
                SerializedObject serializedObject,
                int layerIndex,
                string layerName,
                int priority,
                ExclusionMode exclusionMode,
                IReadOnlyList<string> overrideMask)
            {
                Invocations.Add("SetLayerProperties");
                LastLayerIndex = layerIndex;
                LastLayerName = layerName;
                LastPriority = priority;
                LastExclusionMode = exclusionMode;
                LastOverrideMask = overrideMask;
            }

            public void BeginContinuousWeight(SerializedObject serializedObject, int layerIndex, string canonicalId)
            {
            }

            public void SetWeightContinuous(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
            }

            public void EndContinuousWeight()
            {
            }
        }
    }
}
