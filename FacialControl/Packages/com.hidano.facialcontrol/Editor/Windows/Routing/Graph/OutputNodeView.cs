using System;
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

            for (int i = 0; i < OutputNodeData.OrderedLayers.Count; i++)
            {
                extensionContainer.Add(CreateLayerRow(i, OutputNodeData.OrderedLayers[i]));
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public OutputNodeData OutputNodeData { get; }

        private static Label CreateLayerRow(int index, OutputLayerData layer)
        {
            var row = new Label(BuildRowText(index, layer));
            row.AddToClassList(LayerRowClassName);
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
