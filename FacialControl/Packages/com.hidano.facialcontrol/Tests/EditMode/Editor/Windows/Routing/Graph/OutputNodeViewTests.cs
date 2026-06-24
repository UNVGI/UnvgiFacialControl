using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.AdapterBindings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class OutputNodeViewTests
    {
        private TestFacialCharacterProfileSO _profile;
        private SerializedObject _serializedObject;
        private RecordingWiringSerializedMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<TestFacialCharacterProfileSO>();
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
        public void Construct_RendersEditableCompositionFieldsPerLayer()
        {
            var view = new OutputNodeView(
                new OutputNodeData(new[]
                {
                    new OutputLayerData(1, "emotion", 0, ExclusionMode.LastWins, new string[0]),
                    new OutputLayerData(0, "overlay", 2, ExclusionMode.Blend, new[] { "emotion", "fx" }),
                }),
                _serializedObject,
                _mapper);

            Assert.That(view.title, Is.EqualTo(OutputNodeView.NodeTitle));

            IntegerField[] priorityFields = view
                .Query<IntegerField>(className: OutputNodeView.PriorityFieldClassName)
                .ToList()
                .ToArray();
            Assert.That(priorityFields, Has.Length.EqualTo(2));
            Assert.That(priorityFields[0].value, Is.EqualTo(0));
            Assert.That(priorityFields[1].value, Is.EqualTo(2));

            EnumField[] exclusionFields = view
                .Query<EnumField>(className: OutputNodeView.ExclusionFieldClassName)
                .ToList()
                .ToArray();
            Assert.That(exclusionFields, Has.Length.EqualTo(2));
            Assert.That((ExclusionMode)exclusionFields[1].value, Is.EqualTo(ExclusionMode.Blend));

            TextField[] maskFields = view
                .Query<TextField>(className: OutputNodeView.MaskFieldClassName)
                .ToList()
                .ToArray();
            Assert.That(maskFields, Has.Length.EqualTo(2));
            Assert.That(maskFields[1].value, Is.EqualTo("emotion, fx"));

            Assert.That(view.GetLayerInputPort(0), Is.Not.Null);
            Assert.That(view.GetLayerInputPort(1), Is.Not.Null);
        }

        [Test]
        public void ApplyLayer_WritesLayerPropertiesPreservingName()
        {
            var view = new OutputNodeView(
                new OutputNodeData(new[]
                {
                    new OutputLayerData(0, "overlay", 2, ExclusionMode.Blend, new[] { "emotion" }),
                }),
                _serializedObject,
                _mapper);

            InvokeApplyLayer(view, 0, "overlay", 5, ExclusionMode.LastWins, new[] { "emotion", "fx" });

            Assert.That(_mapper.SetLayerPropertiesCalls, Is.EqualTo(1));
            Assert.That(_mapper.LastLayerIndex, Is.EqualTo(0));
            Assert.That(_mapper.LastLayerName, Is.EqualTo("overlay"));
            Assert.That(_mapper.LastPriority, Is.EqualTo(5));
            Assert.That(_mapper.LastExclusionMode, Is.EqualTo(ExclusionMode.LastWins));
            CollectionAssert.AreEqual(new[] { "emotion", "fx" }, _mapper.LastOverrideMask);
        }

        [Test]
        public void SetOutputNode_RebuildsGraphWithUpdatedRows()
        {
            var graphView = new RoutingGraphView();

            graphView.SetOutputNode(
                new OutputNodeData(new[]
                {
                    new OutputLayerData(0, "overlay", 5, ExclusionMode.Blend, new[] { "emotion" }),
                }),
                _serializedObject,
                _mapper);

            Assert.That(graphView.OutputNodeView, Is.Not.Null);
            Assert.That(
                graphView.OutputNodeView
                    .Query<IntegerField>(className: OutputNodeView.PriorityFieldClassName)
                    .ToList(),
                Has.Count.EqualTo(1));

            graphView.SetOutputNode(
                new OutputNodeData(new[]
                {
                    new OutputLayerData(1, "emotion", 1, ExclusionMode.LastWins, new string[0]),
                    new OutputLayerData(0, "overlay", 3, ExclusionMode.Blend, new[] { "emotion", "fx" }),
                }),
                _serializedObject,
                _mapper);

            Assert.That(
                graphView.OutputNodeView
                    .Query<IntegerField>(className: OutputNodeView.PriorityFieldClassName)
                    .ToList(),
                Has.Count.EqualTo(2));
        }

        private static void InvokeApplyLayer(
            OutputNodeView view,
            int layerIndex,
            string layerName,
            int priority,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> overrideMask)
        {
            MethodInfo method = typeof(OutputNodeView).GetMethod(
                "ApplyLayer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, new object[] { layerIndex, layerName, priority, exclusionMode, overrideMask });
        }

        private sealed class RecordingWiringSerializedMapper : IWiringSerializedMapper
        {
            public int SetLayerPropertiesCalls { get; private set; }

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
                SetLayerPropertiesCalls++;
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
