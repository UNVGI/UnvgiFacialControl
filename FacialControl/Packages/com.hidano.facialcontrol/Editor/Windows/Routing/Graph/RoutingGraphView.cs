using System;
using System.Collections.Generic;
using System.Linq;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// ルーティング UI の GraphView ルート要素。
    /// </summary>
    public sealed class RoutingGraphView : GraphView
    {
        private const float DefaultDeclarationWeight = 1f;

        // 左上固定の MiniMap (16,16,200,140 → 下端 156) とノード/ダングリングバッジが
        // 重ならないよう、ノード行の開始 Y を MiniMap 下端より下に置く。
        private const float ContentTopMargin = 176f;
        private readonly List<SourceNodeView> _sourceNodeViews = new List<SourceNodeView>();
        private readonly List<LayerNodeView> _layerNodeViews = new List<LayerNodeView>();
        private const float OrphanColumnX = 32f;
        private const float OrphanRowSpacing = 120f;
        private readonly List<RoutingEdge> _routingEdges = new List<RoutingEdge>();
        private readonly List<OrphanInputNodeView> _orphanInputNodes = new List<OrphanInputNodeView>();
        private readonly List<Edge> _orphanInputEdges = new List<Edge>();
        private readonly List<Edge> _compositionEdges = new List<Edge>();
        private readonly MiniMap _miniMap;
        private OutputNodeView _outputNodeView;
        private SerializedObject _serializedObject;
        private IWiringSerializedMapper _wiringSerializedMapper;

        public RoutingGraphView()
        {
            name = "routing-graph-view";

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            _miniMap = new MiniMap
            {
                anchored = true,
            };
            _miniMap.SetPosition(new Rect(16f, 16f, 200f, 140f));
            Add(_miniMap);

            graphViewChanged = OnGraphViewChanged;
        }

        public IReadOnlyList<SourceNodeView> SourceNodeViews => _sourceNodeViews;

        public IReadOnlyList<LayerNodeView> LayerNodeViews => _layerNodeViews;

        public IReadOnlyList<RoutingEdge> RoutingEdges => _routingEdges;

        public IReadOnlyList<OrphanInputNodeView> OrphanInputNodes => _orphanInputNodes;

        public IReadOnlyList<Edge> OrphanInputEdges => _orphanInputEdges;

        public IReadOnlyList<Edge> CompositionEdges => _compositionEdges;

        public OutputNodeView OutputNodeView => _outputNodeView;

        public MiniMap MiniMap => _miniMap;

        public void SetSourceNodes(
            IReadOnlyList<SourceNodeDescriptor> sourceNodes,
            Action<SourceNodeDescriptor> onAutoWireRequested)
        {
            if (sourceNodes == null)
            {
                throw new ArgumentNullException(nameof(sourceNodes));
            }

            ClearRoutingEdges();
            ClearOrphanInputs();
            ClearCompositionEdges();
            ClearSourceNodes();

            for (int i = 0; i < sourceNodes.Count; i++)
            {
                var sourceNodeView = new SourceNodeView(sourceNodes[i], onAutoWireRequested);
                sourceNodeView.SetPosition(new Rect(32f, ContentTopMargin + (i * 140f), 240f, 96f));
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

            _serializedObject = serializedObject;
            _wiringSerializedMapper = wiringSerializedMapper;

            ClearRoutingEdges();
            ClearOrphanInputs();
            ClearCompositionEdges();
            ClearLayerNodes();

            for (int i = 0; i < layerNodes.Count; i++)
            {
                var layerNodeView = new LayerNodeView(layerNodes[i], serializedObject, wiringSerializedMapper);
                layerNodeView.SetPosition(new Rect(336f, ContentTopMargin + (i * 240f), 280f, 180f));
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

            ClearCompositionEdges();
            ClearOutputNode();

            _outputNodeView = new OutputNodeView(outputNodeData);
            _outputNodeView.SetPosition(new Rect(672f, ContentTopMargin, 320f, Mathf.Max(140f, 56f + (outputNodeData.OrderedLayers.Count * 24f))));
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

            _serializedObject = serializedObject;
            _wiringSerializedMapper = wiringSerializedMapper;

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

        /// <summary>
        /// 未解決（ソース未存在）の入力を、ドラッグ移動可能な孤立ノード＋読み取り専用エッジで描画する。
        /// </summary>
        public void SetInvalidInputs(IReadOnlyList<DanglingEdgeData> invalidInputs)
        {
            if (invalidInputs == null)
            {
                throw new ArgumentNullException(nameof(invalidInputs));
            }

            ClearOrphanInputs();

            for (int i = 0; i < invalidInputs.Count; i++)
            {
                DanglingEdgeData data = invalidInputs[i];
                LayerNodeView layerNode = FindLayerNode(data.LayerIndex);
                if (layerNode == null)
                {
                    continue;
                }

                var orphanNode = new OrphanInputNodeView(data);
                float y = ContentTopMargin + ((_sourceNodeViews.Count + i) * OrphanRowSpacing);
                orphanNode.SetPosition(new Rect(OrphanColumnX, y, 220f, 80f));
                _orphanInputNodes.Add(orphanNode);
                AddElement(orphanNode);

                Edge edge = CreateReadOnlyEdge(orphanNode.OutputPort, layerNode.InputPort);
                _orphanInputEdges.Add(edge);
                AddElement(edge);
            }
        }

        /// <summary>
        /// 各レイヤーノードから Composite Output へ、ブレンドを示す読み取り専用エッジを引く。
        /// </summary>
        public void SetCompositionEdges()
        {
            ClearCompositionEdges();

            if (_outputNodeView == null)
            {
                return;
            }

            for (int i = 0; i < _layerNodeViews.Count; i++)
            {
                LayerNodeView layerNode = _layerNodeViews[i];
                Port outputPort = _outputNodeView.GetLayerInputPort(layerNode.LayerNodeData.LayerIndex);
                if (outputPort == null)
                {
                    continue;
                }

                Edge edge = CreateReadOnlyEdge(layerNode.OutputPort, outputPort);
                _compositionEdges.Add(edge);
                AddElement(edge);
            }
        }

        private static Edge CreateReadOnlyEdge(Port outputPort, Port inputPort)
        {
            var edge = new Edge
            {
                output = outputPort,
                input = inputPort,
            };
            outputPort.Connect(edge);
            inputPort.Connect(edge);
            edge.capabilities &= ~(Capabilities.Selectable | Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);
            edge.pickingMode = PickingMode.Ignore;
            return edge;
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

        private void ClearOrphanInputs()
        {
            for (int i = 0; i < _orphanInputEdges.Count; i++)
            {
                RemoveElement(_orphanInputEdges[i]);
            }

            _orphanInputEdges.Clear();

            for (int i = 0; i < _orphanInputNodes.Count; i++)
            {
                RemoveElement(_orphanInputNodes[i]);
            }

            _orphanInputNodes.Clear();
        }

        private void ClearCompositionEdges()
        {
            for (int i = 0; i < _compositionEdges.Count; i++)
            {
                RemoveElement(_compositionEdges[i]);
            }

            _compositionEdges.Clear();
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

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            if (startPort == null)
            {
                return new List<Port>();
            }

            return ports
                .ToList()
                .Where(port =>
                    port != startPort
                    && port.node != startPort.node
                    && IsCompatibleSourceLayerPair(startPort, port))
                .ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (_serializedObject == null || _wiringSerializedMapper == null)
            {
                return graphViewChange;
            }

            if (graphViewChange.edgesToCreate != null && graphViewChange.edgesToCreate.Count > 0)
            {
                graphViewChange.edgesToCreate = ConvertCreatedEdges(graphViewChange.edgesToCreate);
            }

            if (graphViewChange.elementsToRemove != null && graphViewChange.elementsToRemove.Count > 0)
            {
                RemoveDeletedDeclarations(graphViewChange.elementsToRemove);
            }

            return graphViewChange;
        }

        private List<Edge> ConvertCreatedEdges(IEnumerable<Edge> edgesToCreate)
        {
            var replacementEdges = new List<Edge>();

            foreach (Edge edge in edgesToCreate)
            {
                if (!TryGetConnectionData(edge, out SourceNodeView sourceNode, out LayerNodeView layerNode))
                {
                    continue;
                }

                string canonicalId = sourceNode.Descriptor.CanonicalId;
                _wiringSerializedMapper.AddDeclaration(
                    _serializedObject,
                    layerNode.LayerNodeData.LayerIndex,
                    canonicalId,
                    DefaultDeclarationWeight);

                replacementEdges.Add(new RoutingEdge(
                    sourceNode.OutputPort,
                    layerNode.InputPort,
                    _serializedObject,
                    _wiringSerializedMapper,
                    new WiringEdgeData(
                        layerNode.LayerNodeData.LayerIndex,
                        canonicalId,
                        DefaultDeclarationWeight)));
            }

            return replacementEdges;
        }

        private void RemoveDeletedDeclarations(IEnumerable<GraphElement> elementsToRemove)
        {
            foreach (GraphElement element in elementsToRemove)
            {
                if (element is not Edge edge)
                {
                    continue;
                }

                if (!TryGetConnectionData(edge, out SourceNodeView sourceNode, out LayerNodeView layerNode))
                {
                    continue;
                }

                _wiringSerializedMapper.RemoveDeclaration(
                    _serializedObject,
                    layerNode.LayerNodeData.LayerIndex,
                    sourceNode.Descriptor.CanonicalId);
            }
        }

        private static bool TryGetConnectionData(
            Edge edge,
            out SourceNodeView sourceNode,
            out LayerNodeView layerNode)
        {
            sourceNode = edge?.output?.node as SourceNodeView;
            layerNode = edge?.input?.node as LayerNodeView;
            return sourceNode != null && layerNode != null;
        }

        private static bool IsCompatibleSourceLayerPair(Port startPort, Port candidatePort)
        {
            return (startPort.direction == Direction.Output
                    && candidatePort.direction == Direction.Input
                    && startPort.node is SourceNodeView
                    && candidatePort.node is LayerNodeView)
                || (startPort.direction == Direction.Input
                    && candidatePort.direction == Direction.Output
                    && startPort.node is LayerNodeView
                    && candidatePort.node is SourceNodeView);
        }
    }
}
