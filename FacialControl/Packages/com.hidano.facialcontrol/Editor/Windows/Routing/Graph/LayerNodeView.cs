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
    /// Renders a layer node and writes attribute edits back to the serialized profile.
    /// </summary>
    public sealed class LayerNodeView : Node
    {
        public const string NameFieldName = "routing-layer-name";
        public const string PriorityFieldName = "routing-layer-priority";
        public const string ExclusionModeFieldName = "routing-layer-exclusion-mode";
        public const string OverrideMaskFieldName = "routing-layer-override-mask";

        private readonly SerializedObject _serializedObject;
        private readonly IWiringSerializedMapper _wiringSerializedMapper;
        private string _layerName;
        private int _priority;
        private ExclusionMode _exclusionMode;
        private List<string> _overrideMask;

        public LayerNodeView(
            LayerNodeData layerNodeData,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper)
        {
            _serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            _wiringSerializedMapper = wiringSerializedMapper ?? throw new ArgumentNullException(nameof(wiringSerializedMapper));
            LayerNodeData = layerNodeData;
            _layerName = layerNodeData.Name;
            _priority = layerNodeData.Priority;
            _exclusionMode = layerNodeData.ExclusionMode;
            _overrideMask = CopyOverrideMask(layerNodeData.OverrideMask);

            name = $"routing-layer-node-{LayerNodeData.LayerIndex}";
            title = GetNodeTitle(_layerName, LayerNodeData.LayerIndex);

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            NameField = new TextField("Name")
            {
                name = NameFieldName,
                value = _layerName,
            };
            NameField.RegisterValueChangedCallback(evt => ApplyLayerProperties(
                evt.newValue,
                _priority,
                _exclusionMode,
                _overrideMask));

            PriorityField = new IntegerField("Priority")
            {
                name = PriorityFieldName,
                value = _priority,
            };
            PriorityField.RegisterValueChangedCallback(evt => ApplyLayerProperties(
                _layerName,
                evt.newValue,
                _exclusionMode,
                _overrideMask));

            ExclusionModeField = new EnumField("Exclusion Mode", _exclusionMode)
            {
                name = ExclusionModeFieldName,
            };
            ExclusionModeField.RegisterValueChangedCallback(evt => ApplyLayerProperties(
                _layerName,
                _priority,
                (ExclusionMode)evt.newValue,
                _overrideMask));

            OverrideMaskField = new TextField("Override Mask")
            {
                name = OverrideMaskFieldName,
                multiline = true,
                value = FormatOverrideMask(_overrideMask),
            };
            OverrideMaskField.RegisterValueChangedCallback(evt => ApplyLayerProperties(
                _layerName,
                _priority,
                _exclusionMode,
                ParseOverrideMask(evt.newValue)));
            OverrideMaskField.tooltip = "Comma or newline separated layer names.";

            extensionContainer.Add(NameField);
            extensionContainer.Add(PriorityField);
            extensionContainer.Add(ExclusionModeField);
            extensionContainer.Add(OverrideMaskField);

            RefreshExpandedState();
            RefreshPorts();
        }

        public LayerNodeData LayerNodeData { get; }

        public TextField NameField { get; }

        public IntegerField PriorityField { get; }

        public EnumField ExclusionModeField { get; }

        public TextField OverrideMaskField { get; }

        private void ApplyLayerProperties(
            string layerName,
            int priority,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> overrideMask)
        {
            _layerName = layerName ?? string.Empty;
            _priority = priority;
            _exclusionMode = exclusionMode;
            _overrideMask = CopyOverrideMask(overrideMask);

            _wiringSerializedMapper.SetLayerProperties(
                _serializedObject,
                LayerNodeData.LayerIndex,
                _layerName,
                _priority,
                _exclusionMode,
                _overrideMask);

            title = GetNodeTitle(_layerName, LayerNodeData.LayerIndex);
            OverrideMaskField.SetValueWithoutNotify(FormatOverrideMask(_overrideMask));
        }

        private static string GetNodeTitle(string layerName, int layerIndex)
        {
            return string.IsNullOrWhiteSpace(layerName)
                ? $"Layer {layerIndex}"
                : layerName;
        }

        private static List<string> CopyOverrideMask(IReadOnlyList<string> overrideMask)
        {
            var copy = new List<string>();
            if (overrideMask == null)
            {
                return copy;
            }

            for (int i = 0; i < overrideMask.Count; i++)
            {
                string value = overrideMask[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    copy.Add(value);
                }
            }

            return copy;
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
