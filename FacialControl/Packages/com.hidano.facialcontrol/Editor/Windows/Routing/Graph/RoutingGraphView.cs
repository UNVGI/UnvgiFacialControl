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
        private readonly List<AdapterNodeView> _adapterNodeViews = new List<AdapterNodeView>();
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

        public IReadOnlyList<AdapterNodeView> AdapterNodeViews => _adapterNodeViews;

        public IReadOnlyList<LayerNodeView> LayerNodeViews => _layerNodeViews;

        public IReadOnlyList<RoutingEdge> RoutingEdges => _routingEdges;

        public IReadOnlyList<OrphanInputNodeView> OrphanInputNodes => _orphanInputNodes;

        public IReadOnlyList<Edge> OrphanInputEdges => _orphanInputEdges;

        public IReadOnlyList<Edge> CompositionEdges => _compositionEdges;

        public OutputNodeView OutputNodeView => _outputNodeView;

        public MiniMap MiniMap => _miniMap;

        public void SetAdapterNodes(
            IReadOnlyList<AdapterNodeData> adapterNodes,
            Action<string> onAutoWireRequested)
        {
            if (adapterNodes == null)
            {
                throw new ArgumentNullException(nameof(adapterNodes));
            }

            ClearRoutingEdges();
            ClearOrphanInputs();
            ClearCompositionEdges();
            ClearAdapterNodes();

            float y = ContentTopMargin;
            for (int i = 0; i < adapterNodes.Count; i++)
            {
                AdapterNodeData data = adapterNodes[i];
                var adapterNodeView = new AdapterNodeView(data, onAutoWireRequested);
                float height = Mathf.Max(140f, 56f + (Mathf.Max(1, data.Outputs.Count) * 24f));
                adapterNodeView.SetPosition(new Rect(32f, y, 240f, height));
                _adapterNodeViews.Add(adapterNodeView);
                AddElement(adapterNodeView);
                y += height + 24f;
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

        public void SetOutputNode(
            OutputNodeData outputNodeData,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper)
        {
            if (outputNodeData == null)
            {
                throw new ArgumentNullException(nameof(outputNodeData));
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

            ClearCompositionEdges();
            ClearOutputNode();

            _outputNodeView = new OutputNodeView(outputNodeData, serializedObject, wiringSerializedMapper);
            float height = Mathf.Max(160f, 64f + (outputNodeData.OrderedLayers.Count * 108f));
            _outputNodeView.SetPosition(new Rect(672f, ContentTopMargin, 340f, height));
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
                Port sourcePort = FindOutputPort(edgeData.CanonicalId);
                LayerNodeView layerNode = FindLayerNode(edgeData.LayerIndex);
                if (sourcePort == null || layerNode == null)
                {
                    continue;
                }

                var edge = new RoutingEdge(sourcePort, layerNode.InputPort, edgeData);
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
                float y = ContentTopMargin + ((_adapterNodeViews.Count + i) * OrphanRowSpacing);
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

        private void ClearAdapterNodes()
        {
            for (int i = 0; i < _adapterNodeViews.Count; i++)
            {
                RemoveElement(_adapterNodeViews[i]);
            }

            _adapterNodeViews.Clear();
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

        private Port FindOutputPort(string canonicalId)
        {
            for (int i = 0; i < _adapterNodeViews.Count; i++)
            {
                Port port = _adapterNodeViews[i].GetOutputPort(canonicalId);
                if (port != null)
                {
                    return port;
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

            // 繋ぎ替えは「旧エッジ削除（elementsToRemove）＋新エッジ作成（edgesToCreate）」として
            // 同一 GraphViewChange で報告される。先に削除を処理し、削除された配線の weight を
            // canonical id で控えておくことで、同一 canonical id の新規エッジへ weight を引き継ぐ。
            Dictionary<string, float> removedWeights = null;
            if (graphViewChange.elementsToRemove != null && graphViewChange.elementsToRemove.Count > 0)
            {
                removedWeights = CaptureRemovedWeights(graphViewChange.elementsToRemove);
                RemoveDeletedDeclarations(graphViewChange.elementsToRemove);
            }

            if (graphViewChange.edgesToCreate != null && graphViewChange.edgesToCreate.Count > 0)
            {
                graphViewChange.edgesToCreate = ConvertCreatedEdges(graphViewChange.edgesToCreate, removedWeights);
            }

            return graphViewChange;
        }

        private static Dictionary<string, float> CaptureRemovedWeights(IEnumerable<GraphElement> elementsToRemove)
        {
            var map = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (GraphElement element in elementsToRemove)
            {
                if (element is RoutingEdge routingEdge
                    && !string.IsNullOrEmpty(routingEdge.EdgeData.CanonicalId))
                {
                    map[routingEdge.EdgeData.CanonicalId] = routingEdge.EdgeData.Weight;
                }
            }

            return map;
        }

        private List<Edge> ConvertCreatedEdges(
            IEnumerable<Edge> edgesToCreate,
            Dictionary<string, float> removedWeights)
        {
            var replacementEdges = new List<Edge>();

            foreach (Edge edge in edgesToCreate)
            {
                if (!TryGetConnectionData(edge, out string canonicalId, out LayerNodeView layerNode))
                {
                    continue;
                }

                float weight = DefaultDeclarationWeight;
                if (removedWeights != null && removedWeights.TryGetValue(canonicalId, out float carriedWeight))
                {
                    weight = carriedWeight;
                }

                _wiringSerializedMapper.AddDeclaration(
                    _serializedObject,
                    layerNode.LayerNodeData.LayerIndex,
                    canonicalId,
                    weight);

                replacementEdges.Add(new RoutingEdge(
                    edge.output,
                    edge.input,
                    new WiringEdgeData(layerNode.LayerNodeData.LayerIndex, canonicalId, weight)));
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

                if (!TryGetConnectionData(edge, out string canonicalId, out LayerNodeView layerNode))
                {
                    continue;
                }

                _wiringSerializedMapper.RemoveDeclaration(
                    _serializedObject,
                    layerNode.LayerNodeData.LayerIndex,
                    canonicalId);
            }
        }

        private static bool TryGetConnectionData(
            Edge edge,
            out string canonicalId,
            out LayerNodeView layerNode)
        {
            canonicalId = edge?.output?.node is AdapterNodeView
                ? edge.output.userData as string
                : null;
            layerNode = edge?.input?.node as LayerNodeView;
            return !string.IsNullOrEmpty(canonicalId) && layerNode != null;
        }

        private static bool IsCompatibleSourceLayerPair(Port startPort, Port candidatePort)
        {
            return (startPort.direction == Direction.Output
                    && candidatePort.direction == Direction.Input
                    && startPort.node is AdapterNodeView
                    && candidatePort.node is LayerNodeView)
                || (startPort.direction == Direction.Input
                    && candidatePort.direction == Direction.Output
                    && startPort.node is LayerNodeView
                    && candidatePort.node is AdapterNodeView);
        }
    }
}
