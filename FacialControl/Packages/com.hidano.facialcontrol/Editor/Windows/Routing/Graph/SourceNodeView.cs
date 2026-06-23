using System;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// Renders a source node and exposes a batch wire button for the overlay layer.
    /// </summary>
    public sealed class SourceNodeView : Node
    {
        public const string AutoWireButtonText = "overlay \u30ec\u30a4\u30e4\u30fc\u3078\u4e00\u62ec\u914d\u7dda";

        private readonly Action<SourceNodeDescriptor> _onAutoWireRequested;

        public SourceNodeView(
            SourceNodeDescriptor descriptor,
            Action<SourceNodeDescriptor> onAutoWireRequested)
        {
            Descriptor = descriptor;
            _onAutoWireRequested = onAutoWireRequested;

            name = $"routing-source-node-{Descriptor.CanonicalId}";
            title = string.IsNullOrEmpty(Descriptor.BindingSlug)
                ? "Source"
                : Descriptor.BindingSlug;

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            AutoWireButton = new Button(NotifyAutoWireRequested)
            {
                text = AutoWireButtonText,
            };
            AutoWireButton.AddToClassList(FacialControlStyles.ActionButton);
            titleButtonContainer.Add(AutoWireButton);

            OutputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(float));
            OutputPort.portName = Descriptor.Label;
            OutputPort.tooltip = Descriptor.CanonicalId;
            OutputPort.userData = Descriptor.CanonicalId;
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        public SourceNodeDescriptor Descriptor { get; }

        public Button AutoWireButton { get; }

        public Port OutputPort { get; }

        private void NotifyAutoWireRequested()
        {
            _onAutoWireRequested?.Invoke(Descriptor);
        }
    }
}
