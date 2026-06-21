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

        public OutputNodeView OutputNodeView => _outputNodeView;

        public void SetSourceNodes(
            IReadOnlyList<SourceNodeDescriptor> sourceNodes,
            Action<SourceNodeDescriptor> onAutoWireRequested)
        {
            if (sourceNodes == null)
            {
                throw new ArgumentNullException(nameof(sourceNodes));
            }

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
    }
}
