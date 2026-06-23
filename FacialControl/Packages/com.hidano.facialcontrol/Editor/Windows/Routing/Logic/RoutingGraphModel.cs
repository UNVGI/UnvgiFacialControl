using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Editor.Windows.Routing.Logic
{
    public readonly struct SourceNodeDescriptor
    {
        public SourceNodeDescriptor(string canonicalId, string label, string bindingSlug)
        {
            CanonicalId = canonicalId ?? string.Empty;
            Label = label ?? string.Empty;
            BindingSlug = bindingSlug ?? string.Empty;
        }

        public string CanonicalId { get; }

        public string Label { get; }

        public string BindingSlug { get; }
    }

    public readonly struct LayerNodeData
    {
        public LayerNodeData(
            int layerIndex,
            string name,
            int priority,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> overrideMask)
        {
            LayerIndex = layerIndex;
            Name = name ?? string.Empty;
            Priority = priority;
            ExclusionMode = exclusionMode;
            OverrideMask = CopyStrings(overrideMask);
        }

        public int LayerIndex { get; }

        public string Name { get; }

        public int Priority { get; }

        public ExclusionMode ExclusionMode { get; }

        public IReadOnlyList<string> OverrideMask { get; }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copy[i] = values[i] ?? string.Empty;
            }

            return copy;
        }
    }

    public readonly struct OutputLayerData
    {
        public OutputLayerData(int layerIndex, string name, int priority, IReadOnlyList<string> overrideMask)
        {
            LayerIndex = layerIndex;
            Name = name ?? string.Empty;
            Priority = priority;
            OverrideMask = CopyStrings(overrideMask);
        }

        public int LayerIndex { get; }

        public string Name { get; }

        public int Priority { get; }

        public IReadOnlyList<string> OverrideMask { get; }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copy[i] = values[i] ?? string.Empty;
            }

            return copy;
        }
    }

    public sealed class OutputNodeData
    {
        public OutputNodeData(IReadOnlyList<OutputLayerData> orderedLayers)
        {
            OrderedLayers = Copy(orderedLayers);
        }

        public IReadOnlyList<OutputLayerData> OrderedLayers { get; }

        private static IReadOnlyList<OutputLayerData> Copy(IReadOnlyList<OutputLayerData> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<OutputLayerData>();
            }

            var copy = new OutputLayerData[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copy[i] = values[i];
            }

            return copy;
        }
    }

    public readonly struct WiringEdgeData
    {
        public WiringEdgeData(int layerIndex, string canonicalId, float weight)
        {
            LayerIndex = layerIndex;
            CanonicalId = canonicalId ?? string.Empty;
            Weight = weight;
        }

        public int LayerIndex { get; }

        public string CanonicalId { get; }

        public float Weight { get; }
    }

    public readonly struct DanglingEdgeData
    {
        public DanglingEdgeData(int layerIndex, int declarationIndex, string id)
        {
            LayerIndex = layerIndex;
            DeclarationIndex = declarationIndex;
            Id = id ?? string.Empty;
        }

        public int LayerIndex { get; }

        public int DeclarationIndex { get; }

        public string Id { get; }
    }

    public sealed class RoutingGraphModel
    {
        public RoutingGraphModel(
            IReadOnlyList<SourceNodeDescriptor> sourceNodes,
            IReadOnlyList<LayerNodeData> layerNodes,
            OutputNodeData outputNode,
            IReadOnlyList<WiringEdgeData> edges,
            IReadOnlyList<DanglingEdgeData> invalidEdges)
        {
            SourceNodes = Copy(sourceNodes);
            LayerNodes = Copy(layerNodes);
            OutputNode = outputNode ?? new OutputNodeData(Array.Empty<OutputLayerData>());
            Edges = Copy(edges);
            InvalidEdges = Copy(invalidEdges);
        }

        public IReadOnlyList<SourceNodeDescriptor> SourceNodes { get; }

        public IReadOnlyList<LayerNodeData> LayerNodes { get; }

        public OutputNodeData OutputNode { get; }

        public IReadOnlyList<WiringEdgeData> Edges { get; }

        public IReadOnlyList<DanglingEdgeData> InvalidEdges { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<T>();
            }

            var copy = new T[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copy[i] = values[i];
            }

            return copy;
        }
    }
}
