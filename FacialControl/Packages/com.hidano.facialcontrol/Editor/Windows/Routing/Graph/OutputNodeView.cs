using System;
using System.Collections.Generic;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// Renders the final output composition order as a read-only summary node.
    /// </summary>
    public sealed class OutputNodeView : Node
    {
        public const string NodeTitle = "Composite Output";
        public const string LayerRowClassName = "routing-output-node-row";
        private static readonly UnityEngine.Color TitleBarColor = new UnityEngine.Color(0.13f, 0.26f, 0.26f, 1f);

        private readonly Dictionary<int, Port> _layerInputPorts = new Dictionary<int, Port>();

        public OutputNodeView(OutputNodeData outputNodeData)
        {
            OutputNodeData = outputNodeData ?? throw new ArgumentNullException(nameof(outputNodeData));

            name = "routing-output-node";
            title = NodeTitle;

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            titleContainer.style.backgroundColor = TitleBarColor;

            for (int i = 0; i < OutputNodeData.OrderedLayers.Count; i++)
            {
                extensionContainer.Add(CreateLayerRow(i, OutputNodeData.OrderedLayers[i]));
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public OutputNodeData OutputNodeData { get; }

        /// <summary>
        /// 指定レイヤーに対応する合成入力ポートを返す。存在しなければ null。
        /// </summary>
        public Port GetLayerInputPort(int layerIndex)
        {
            return _layerInputPorts.TryGetValue(layerIndex, out Port port) ? port : null;
        }

        private VisualElement CreateLayerRow(int index, OutputLayerData layer)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            Port port = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(float));
            port.portName = string.Empty;
            port.tooltip = string.IsNullOrWhiteSpace(layer.Name) ? $"Layer {layer.LayerIndex}" : layer.Name;
            port.userData = layer.LayerIndex;
            _layerInputPorts[layer.LayerIndex] = port;
            row.Add(port);

            var label = new Label(BuildRowText(index, layer));
            label.AddToClassList(LayerRowClassName);
            row.Add(label);

            return row;
        }

        private static string BuildRowText(int index, OutputLayerData layer)
        {
            string layerName = string.IsNullOrWhiteSpace(layer.Name)
                ? $"Layer {layer.LayerIndex}"
                : layer.Name;
            string mask = layer.OverrideMask == null || layer.OverrideMask.Count == 0
                ? "-"
                : string.Join(", ", layer.OverrideMask);
            return $"{index + 1}. {layerName} | priority {layer.Priority} | mask {mask}";
        }
    }
}
