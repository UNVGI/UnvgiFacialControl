using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// 合成結果（Composite Output）ノード。各レイヤーの合成順を行で示し、
    /// レイヤー間のふるまい（priority / exclusion / mask）をここで一元編集する。
    /// </summary>
    public sealed class OutputNodeView : Node
    {
        public const string NodeTitle = "Composite Output";
        public const string LayerRowClassName = "routing-output-node-row";
        public const string PriorityFieldClassName = "routing-output-priority";
        public const string ExclusionFieldClassName = "routing-output-exclusion";
        public const string MaskFieldClassName = "routing-output-mask";
        private static readonly UnityEngine.Color TitleBarColor = new UnityEngine.Color(0.13f, 0.26f, 0.26f, 1f);

        private readonly SerializedObject _serializedObject;
        private readonly IWiringSerializedMapper _wiringSerializedMapper;
        private readonly Dictionary<int, Port> _layerInputPorts = new Dictionary<int, Port>();

        public OutputNodeView(
            OutputNodeData outputNodeData,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper)
        {
            OutputNodeData = outputNodeData ?? throw new ArgumentNullException(nameof(outputNodeData));
            _serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            _wiringSerializedMapper = wiringSerializedMapper ?? throw new ArgumentNullException(nameof(wiringSerializedMapper));

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
            row.AddToClassList(LayerRowClassName);
            row.style.marginBottom = 6f;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            Port port = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(float));
            port.portName = string.Empty;
            port.tooltip = string.IsNullOrWhiteSpace(layer.Name) ? $"Layer {layer.LayerIndex}" : layer.Name;
            port.userData = layer.LayerIndex;
            _layerInputPorts[layer.LayerIndex] = port;
            headerRow.Add(port);

            string layerName = string.IsNullOrWhiteSpace(layer.Name) ? $"Layer {layer.LayerIndex}" : layer.Name;
            var headerLabel = new Label($"{index + 1}. {layerName}");
            headerLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            headerRow.Add(headerLabel);
            row.Add(headerRow);

            var priorityField = new IntegerField("Priority")
            {
                value = layer.Priority,
                isDelayed = true,
            };
            priorityField.AddToClassList(PriorityFieldClassName);

            var exclusionField = new EnumField("Exclusion Mode", layer.ExclusionMode);
            exclusionField.AddToClassList(ExclusionFieldClassName);

            var maskField = new TextField("Override Mask")
            {
                multiline = true,
                value = FormatOverrideMask(layer.OverrideMask),
            };
            maskField.AddToClassList(MaskFieldClassName);
            maskField.tooltip = "Comma or newline separated layer names.";

            int layerIndex = layer.LayerIndex;
            priorityField.RegisterValueChangedCallback(evt => ApplyLayer(
                layerIndex,
                layer.Name,
                evt.newValue,
                (ExclusionMode)exclusionField.value,
                ParseOverrideMask(maskField.value)));
            exclusionField.RegisterValueChangedCallback(evt => ApplyLayer(
                layerIndex,
                layer.Name,
                priorityField.value,
                (ExclusionMode)evt.newValue,
                ParseOverrideMask(maskField.value)));
            maskField.RegisterValueChangedCallback(evt => ApplyLayer(
                layerIndex,
                layer.Name,
                priorityField.value,
                (ExclusionMode)exclusionField.value,
                ParseOverrideMask(evt.newValue)));

            row.Add(priorityField);
            row.Add(exclusionField);
            row.Add(maskField);

            return row;
        }

        private void ApplyLayer(
            int layerIndex,
            string layerName,
            int priority,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> overrideMask)
        {
            // Name の真実源はレイヤーノード側。ここでは構築時のスナップショット名を保持して渡し、
            // priority / exclusion / mask のみを更新する（name の空上書きを防ぐ）。
            _wiringSerializedMapper.SetLayerProperties(
                _serializedObject,
                layerIndex,
                layerName,
                priority,
                exclusionMode,
                overrideMask);
        }

        private static string FormatOverrideMask(IReadOnlyList<string> overrideMask)
        {
            return overrideMask == null || overrideMask.Count == 0
                ? string.Empty
                : string.Join(", ", overrideMask);
        }

        private static List<string> ParseOverrideMask(string value)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return results;
            }

            string[] tokens = value.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token) || results.Contains(token))
                {
                    continue;
                }

                results.Add(token);
            }

            return results;
        }
    }
}
