using System.Collections.Generic;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.AdapterBindings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class RoutingGraphViewTests
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
                inputSources = new List<InputSourceDeclarationSerializable>(),
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
        public void Construct_InitializesGraphViewManipulatorsAndMiniMap()
        {
            var graphView = new RoutingGraphView();

            Assert.That(graphView.MiniMap, Is.Not.Null);
            Assert.That(graphView.Q<MiniMap>(), Is.SameAs(graphView.MiniMap));
            Assert.That(graphView.Q<GridBackground>(), Is.Not.Null);
            Assert.That(graphView.contentViewContainer.transform.scale.x, Is.GreaterThan(0f));
        }

        [Test]
        public void GetCompatiblePorts_AllowsOnlySourceToLayerConnections()
        {
            RoutingGraphView graphView = CreateGraphView();

            List<Port> compatiblePorts = graphView.GetCompatiblePorts(
                graphView.SourceNodeViews[0].OutputPort,
                null);

            Assert.That(compatiblePorts, Has.Count.EqualTo(1));
            Assert.That(compatiblePorts[0], Is.SameAs(graphView.LayerNodeViews[0].InputPort));
        }

        [Test]
        public void GraphViewChanged_CreateEdge_UsesCanonicalIdAndReplacesWithRoutingEdge()
        {
            RoutingGraphView graphView = CreateGraphView();
            var edge = new Edge
            {
                output = graphView.SourceNodeViews[0].OutputPort,
                input = graphView.LayerNodeViews[0].InputPort,
            };

            GraphViewChange change = graphView.graphViewChanged(new GraphViewChange
            {
                edgesToCreate = new List<Edge> { edge },
            });

            Assert.That(_mapper.AddDeclarationCalls, Is.EqualTo(1));
            Assert.That(_mapper.LastLayerIndex, Is.EqualTo(0));
            Assert.That(_mapper.LastCanonicalId, Is.EqualTo("lipsync-overlay:a"));
            Assert.That(_mapper.LastWeight, Is.EqualTo(1f));
            Assert.That(change.edgesToCreate, Has.Count.EqualTo(1));
            Assert.That(change.edgesToCreate[0], Is.InstanceOf<RoutingEdge>());
        }

        [Test]
        public void GraphViewChanged_RemoveEdge_RemovesMatchingDeclaration()
        {
            RoutingGraphView graphView = CreateGraphView();
            graphView.SetWiringEdges(
                new[] { new WiringEdgeData(0, "lipsync-overlay:a", 0.4f) },
                _serializedObject,
                _mapper);

            graphView.graphViewChanged(new GraphViewChange
            {
                elementsToRemove = new List<GraphElement> { graphView.RoutingEdges[0] },
            });

            Assert.That(_mapper.RemoveDeclarationCalls, Is.EqualTo(1));
            Assert.That(_mapper.LastLayerIndex, Is.EqualTo(0));
            Assert.That(_mapper.LastCanonicalId, Is.EqualTo("lipsync-overlay:a"));
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

        private sealed class RecordingWiringSerializedMapper : IWiringSerializedMapper
        {
            public int AddDeclarationCalls { get; private set; }

            public int RemoveDeclarationCalls { get; private set; }

            public int LastLayerIndex { get; private set; }

            public string LastCanonicalId { get; private set; } = string.Empty;

            public float LastWeight { get; private set; }

            public void AddDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId, float weight)
            {
                AddDeclarationCalls++;
                LastLayerIndex = layerIndex;
                LastCanonicalId = canonicalId;
                LastWeight = weight;
            }

            public void RemoveDeclaration(SerializedObject serializedObject, int layerIndex, string canonicalId)
            {
                RemoveDeclarationCalls++;
                LastLayerIndex = layerIndex;
                LastCanonicalId = canonicalId;
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
