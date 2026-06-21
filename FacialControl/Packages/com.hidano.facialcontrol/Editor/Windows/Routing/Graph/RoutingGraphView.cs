using System;
using System.Collections.Generic;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// ルーティング UI の GraphView ルート要素。
    /// </summary>
    public sealed class RoutingGraphView : GraphView
    {
        private readonly List<SourceNodeView> _sourceNodeViews = new List<SourceNodeView>();
        private readonly List<LayerNodeView> _layerNodeViews = new List<LayerNodeView>();
        private readonly List<RoutingEdge> _routingEdges = new List<RoutingEdge>();
        private readonly List<DanglingEdge> _danglingEdges = new List<DanglingEdge>();
        private OutputNodeView _outputNodeView;

        public RoutingGraphView()
        {
            name = "routing-graph-view";

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
        }

        public IReadOnlyList<SourceNodeView> SourceNodeViews => _sourceNodeViews;

        public IReadOnlyList<LayerNodeView> LayerNodeViews => _layerNodeViews;

        public IReadOnlyList<RoutingEdge> RoutingEdges => _routingEdges;

        public IReadOnlyList<DanglingEdge> DanglingEdges => _danglingEdges;

        public OutputNodeView OutputNodeView => _outputNodeView;

        public void SetSourceNodes(
            IReadOnlyList<SourceNodeDescriptor> sourceNodes,
            Action<SourceNodeDescriptor> onAutoWireRequested)
        {
            if (sourceNodes == null)
            {
                throw new ArgumentNullException(nameof(sourceNodes));
            }

            ClearRoutingEdges();
            ClearDanglingEdges();
            ClearSourceNodes();

            for (int i = 0; i < sourceNodes.Count; i++)
            {
                var sourceNodeView = new SourceNodeView(sourceNodes[i], onAutoWireRequested);
                sourceNodeView.SetPosition(new Rect(32f, 32f + (i * 140f), 240f, 96f));
                _sourceNodeViews.Add(sourceNodeView);
                AddElement(sourceNodeView);
            }
        }

        public void SetLayerNodes(
            IReadOnlyList<LayerNodeData> layerNodes,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper)
        {
            if (layerNodes == null)
            {
                throw new ArgumentNullException(nameof(layerNodes));
            }

            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (wiringSerializedMapper == null)
            {
                throw new ArgumentNullException(nameof(wiringSerializedMapper));
            }

            ClearRoutingEdges();
            ClearDanglingEdges();
            ClearLayerNodes();

            for (int i = 0; i < layerNodes.Count; i++)
            {
                var layerNodeView = new LayerNodeView(layerNodes[i], serializedObject, wiringSerializedMapper);
                layerNodeView.SetPosition(new Rect(336f, 32f + (i * 240f), 280f, 180f));
                _layerNodeViews.Add(layerNodeView);
                AddElement(layerNodeView);
            }
        }

        public void SetOutputNode(OutputNodeData outputNodeData)
        {
            if (outputNodeData == null)
            {
                throw new ArgumentNullException(nameof(outputNodeData));
            }

            ClearOutputNode();

            _outputNodeView = new OutputNodeView(outputNodeData);
            _outputNodeView.SetPosition(new Rect(672f, 32f, 320f, Mathf.Max(140f, 56f + (outputNodeData.OrderedLayers.Count * 24f))));
            AddElement(_outputNodeView);
        }

        public void SetWiringEdges(
            IReadOnlyList<WiringEdgeData> edges,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper)
        {
            if (edges == null)
            {
                throw new ArgumentNullException(nameof(edges));
            }

            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (wiringSerializedMapper == null)
            {
                throw new ArgumentNullException(nameof(wiringSerializedMapper));
            }

            ClearRoutingEdges();

            for (int i = 0; i < edges.Count; i++)
            {
                WiringEdgeData edgeData = edges[i];
                SourceNodeView sourceNode = FindSourceNode(edgeData.CanonicalId);
                LayerNodeView layerNode = FindLayerNode(edgeData.LayerIndex);
                if (sourceNode == null || layerNode == null)
                {
                    continue;
                }

                var edge = new RoutingEdge(
                    sourceNode.OutputPort,
                    layerNode.InputPort,
                    serializedObject,
                    wiringSerializedMapper,
                    edgeData);
                _routingEdges.Add(edge);
                AddElement(edge);
            }
        }

        public void SetDanglingEdges(IReadOnlyList<DanglingEdgeData> invalidEdges)
        {
            if (invalidEdges == null)
            {
                throw new ArgumentNullException(nameof(invalidEdges));
            }

            ClearDanglingEdges();

            for (int i = 0; i < invalidEdges.Count; i++)
            {
                DanglingEdgeData edgeData = invalidEdges[i];
                LayerNodeView layerNode = FindLayerNode(edgeData.LayerIndex);
                if (layerNode == null)
                {
                    continue;
                }

                var edge = new DanglingEdge(layerNode.InputPort, edgeData);
                _danglingEdges.Add(edge);
                AddElement(edge);
            }
        }

        private void ClearSourceNodes()
        {
            for (int i = 0; i < _sourceNodeViews.Count; i++)
            {
                RemoveElement(_sourceNodeViews[i]);
            }

            _sourceNodeViews.Clear();
        }

        private void ClearLayerNodes()
        {
            for (int i = 0; i < _layerNodeViews.Count; i++)
            {
                RemoveElement(_layerNodeViews[i]);
            }

            _layerNodeViews.Clear();
        }

        private void ClearOutputNode()
        {
            if (_outputNodeView == null)
            {
                return;
            }

            RemoveElement(_outputNodeView);
            _outputNodeView = null;
        }

        private void ClearRoutingEdges()
        {
            for (int i = 0; i < _routingEdges.Count; i++)
            {
                RemoveElement(_routingEdges[i]);
            }

            _routingEdges.Clear();
        }

        private void ClearDanglingEdges()
        {
            for (int i = 0; i < _danglingEdges.Count; i++)
            {
                RemoveElement(_danglingEdges[i]);
            }

            _danglingEdges.Clear();
        }

        private SourceNodeView FindSourceNode(string canonicalId)
        {
            for (int i = 0; i < _sourceNodeViews.Count; i++)
            {
                SourceNodeView sourceNode = _sourceNodeViews[i];
                if (string.Equals(sourceNode.Descriptor.CanonicalId, canonicalId, StringComparison.Ordinal))
                {
                    return sourceNode;
                }
            }

            return null;
        }

        private LayerNodeView FindLayerNode(int layerIndex)
        {
            for (int i = 0; i < _layerNodeViews.Count; i++)
            {
                LayerNodeView layerNode = _layerNodeViews[i];
                if (layerNode.LayerNodeData.LayerIndex == layerIndex)
                {
                    return layerNode;
                }
            }

            return null;
        }
    }
}
