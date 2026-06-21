using System.Collections.Generic;
using System.Linq;
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
    public class RoutingEdgeTests
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
                inputSources = new List<InputSourceDeclarationSerializable>
                {
                    new InputSourceDeclarationSerializable
                    {
                        id = "lipsync-overlay:a",
                        weight = 0.4f,
                    },
                },
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
        public void SetWiringEdges_BuildsRoutingEdgeWithWeightKnob()
        {
            RoutingGraphView graphView = CreateGraphView();

            graphView.SetWiringEdges(
                new[] { new WiringEdgeData(0, "lipsync-overlay:a", 0.4f) },
                _serializedObject,
                _mapper);

            Assert.That(graphView.RoutingEdges, Has.Count.EqualTo(1));
            RoutingEdge edge = graphView.RoutingEdges[0];
            Assert.That(edge.output, Is.SameAs(graphView.SourceNodeViews[0].OutputPort));
            Assert.That(edge.input, Is.SameAs(graphView.LayerNodeViews[0].InputPort));
            Assert.That(edge.WeightKnob, Is.Not.Null);
            Assert.That(edge.WeightField.value, Is.EqualTo(0.4f));
        }

        [Test]
        public void EditingWeightField_UsesDiscreteSetWeight()
        {
            RoutingEdge edge = CreateSingleEdge();

            InvokeWeightFieldChanged(edge, 0.75f);

            Assert.That(_mapper.SetWeightCalls, Is.EqualTo(1));
            Assert.That(_mapper.LastSetWeight, Is.EqualTo(0.75f));
            Assert.That(edge.CurrentWeight, Is.EqualTo(0.75f));
        }

        [Test]
        public void DragWeight_UsesContinuousWeightApi()
        {
            RoutingEdge edge = CreateSingleEdge();

            edge.BeginWeightDrag();
            edge.UpdateDraggedWeight(0.2f);
            edge.UpdateDraggedWeight(0.8f);
            edge.EndWeightDrag();

            Assert.That(_mapper.BeginContinuousCalls, Is.EqualTo(1));
            Assert.That(_mapper.SetWeightContinuousCalls, Is.EqualTo(2));
            Assert.That(_mapper.EndContinuousCalls, Is.EqualTo(1));
            Assert.That(_mapper.LastSetWeightContinuous, Is.EqualTo(0.8f));
            Assert.That(edge.WeightField.value, Is.EqualTo(0.8f));
        }

        [Test]
        public void SetWiringEdges_RebuildsExistingEdges()
        {
            RoutingGraphView graphView = CreateGraphView();

            graphView.SetWiringEdges(
                new[] { new WiringEdgeData(0, "lipsync-overlay:a", 0.4f) },
                _serializedObject,
                _mapper);
            graphView.SetWiringEdges(
                new[] { new WiringEdgeData(0, "lipsync-overlay:a", 0.9f) },
                _serializedObject,
                _mapper);

            Assert.That(graphView.RoutingEdges, Has.Count.EqualTo(1));
            Assert.That(graphView.RoutingEdges[0].WeightField.value, Is.EqualTo(0.9f));
        }

        [Test]
        public void SetDanglingEdges_BuildsReadOnlyDanglingEdgeWithoutRepairButton()
        {
            RoutingGraphView graphView = CreateGraphView();

            graphView.SetDanglingEdges(
                new[] { new DanglingEdgeData(0, 2, "ulipsync:a") });

            Assert.That(graphView.DanglingEdges, Has.Count.EqualTo(1));
            DanglingEdge edge = graphView.DanglingEdges[0];
            Assert.That(edge.output, Is.Null);
            Assert.That(edge.input, Is.SameAs(graphView.LayerNodeViews[0].InputPort));
            Assert.That(edge.IdBadge.text, Is.EqualTo("ulipsync:a"));
            Assert.That(edge.CurrentWireColor.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(edge.CurrentWireColor.g, Is.LessThan(0.35f));
            Assert.That(edge.IsSelectable(), Is.False);
            Assert.That(edge.Children().OfType<UnityEngine.UIElements.Button>().ToArray(), Is.Empty);
        }

        [Test]
        public void SetDanglingEdges_RebuildsExistingDanglingEdges()
        {
            RoutingGraphView graphView = CreateGraphView();

            graphView.SetDanglingEdges(
                new[] { new DanglingEdgeData(0, 1, "legacy:a") });
            graphView.SetDanglingEdges(
                new[] { new DanglingEdgeData(0, 3, "legacy:b") });

            Assert.That(graphView.DanglingEdges, Has.Count.EqualTo(1));
            Assert.That(graphView.DanglingEdges[0].IdBadge.text, Is.EqualTo("legacy:b"));
        }

        private RoutingGraphView CreateGraphView()
        {
            var graphView = new RoutingGraphView();
            graphView.SetSourceNodes(
                new[] { new SourceNodeDescriptor("lipsync-overlay:a", "a", "ulipsync") },
                _ => { });
            graphView.SetLayerNodes(
                new[] { new LayerNodeData(0, "overlay", 1, ExclusionMode.Blend, new string[0]) },
                _serializedObject,
                _mapper);
            return graphView;
        }

        private RoutingEdge CreateSingleEdge()
        {
            RoutingGraphView graphView = CreateGraphView();
            graphView.SetWiringEdges(
                new[] { new WiringEdgeData(0, "lipsync-overlay:a", 0.4f) },
                _serializedObject,
                _mapper);
            return graphView.RoutingEdges[0];
        }

        private static void InvokeWeightFieldChanged(RoutingEdge edge, float value)
        {
            MethodInfo method = typeof(RoutingEdge).GetMethod(
                "OnWeightFieldChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float previous = edge.WeightField.value;
            object evt = UnityEngine.UIElements.ChangeEvent<float>.GetPooled(previous, value);
            try
            {
                method.Invoke(edge, new[] { evt });
            }
            finally
            {
                (evt as System.IDisposable)?.Dispose();
            }
        }

        private sealed class RecordingWiringSerializedMapper : IWiringSerializedMapper
        {
            public int SetWeightCalls { get; private set; }

            public float LastSetWeight { get; private set; }

            public int BeginContinuousCalls { get; private set; }

            public int SetWeightContinuousCalls { get; private set; }

            public float LastSetWeightContinuous { get; private set; }

            public int EndContinuousCalls { get; private set; }

            public void AddDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
            }

            public void RemoveDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId)
            {
            }

            public void SetWeight(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
                SetWeightCalls++;
                LastSetWeight = weight;
            }

            public void SetLayerProperties(
                SerializedObject serializedObject,
                int layerIndex,
                string layerName,
                int priority,
                ExclusionMode exclusionMode,
                IReadOnlyList<string> overrideMask)
            {
            }

            public void BeginContinuousWeight(SerializedObject serializedObject, int layerIndex, string canonicalId)
            {
                BeginContinuousCalls++;
            }

            public void SetWeightContinuous(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
                SetWeightContinuousCalls++;
                LastSetWeightContinuous = weight;
            }

            public void EndContinuousWeight()
            {
                EndContinuousCalls++;
            }
        }
    }
}
