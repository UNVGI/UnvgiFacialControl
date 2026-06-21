using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;

namespace Hidano.FacialControl.Editor.Windows.Routing.Logic
{
    public interface IRoutingGraphModelBuilder
    {
        RoutingGraphModel Build(FacialCharacterProfileSO profile);
    }

    public sealed class RoutingGraphModelBuilder : IRoutingGraphModelBuilder
    {
        private readonly ISourcePortEnumerator _sourcePortEnumerator;
        private readonly IInvalidIdValidator _invalidIdValidator;

        public RoutingGraphModelBuilder()
            : this(new SourcePortEnumerator(), new InvalidIdValidator())
        {
        }

        public RoutingGraphModelBuilder(
            ISourcePortEnumerator sourcePortEnumerator,
            IInvalidIdValidator invalidIdValidator)
        {
            _sourcePortEnumerator = sourcePortEnumerator ?? throw new ArgumentNullException(nameof(sourcePortEnumerator));
            _invalidIdValidator = invalidIdValidator ?? throw new ArgumentNullException(nameof(invalidIdValidator));
        }

        public RoutingGraphModel Build(FacialCharacterProfileSO profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            IReadOnlyList<LayerDefinitionSerializable> layers =
                profile.Layers != null
                    ? profile.Layers
                    : Array.Empty<LayerDefinitionSerializable>();
            string[] layerNames = BuildLayerNames(layers);
            IReadOnlyList<AdapterBindingBase> bindings =
                profile.AdapterBindings != null
                    ? profile.AdapterBindings
                    : Array.Empty<AdapterBindingBase>();
            IReadOnlyList<SourcePort> sourcePorts =
                _sourcePortEnumerator.Enumerate(bindings, layerNames);
            ISet<string> validCanonicalIds =
                _sourcePortEnumerator.EnumerateCanonicalIds(bindings, layerNames);
            IReadOnlyList<InvalidDeclarationRef> invalidDeclarations =
                _invalidIdValidator.Validate(profile, validCanonicalIds);

            SourceNodeDescriptor[] sourceNodes = BuildSourceNodes(sourcePorts);
            LayerNodeData[] layerNodes = BuildLayerNodes(layers);
            WiringEdgeData[] edges = BuildEdges(layers, invalidDeclarations);
            DanglingEdgeData[] invalidEdges = BuildInvalidEdges(invalidDeclarations);
            OutputNodeData outputNode = BuildOutputNode(layerNodes);

            return new RoutingGraphModel(sourceNodes, layerNodes, outputNode, edges, invalidEdges);
        }

        private static string[] BuildLayerNames(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<string>();
            }

            var layerNames = new string[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                layerNames[i] = layers[i]?.name ?? string.Empty;
            }

            return layerNames;
        }

        private static SourceNodeDescriptor[] BuildSourceNodes(IReadOnlyList<SourcePort> sourcePorts)
        {
            if (sourcePorts == null || sourcePorts.Count == 0)
            {
                return Array.Empty<SourceNodeDescriptor>();
            }

            var sourceNodes = new SourceNodeDescriptor[sourcePorts.Count];
            for (int i = 0; i < sourcePorts.Count; i++)
            {
                SourcePort sourcePort = sourcePorts[i];
                sourceNodes[i] = new SourceNodeDescriptor(
                    sourcePort.CanonicalId,
                    sourcePort.Label,
                    sourcePort.BindingSlug);
            }

            return sourceNodes;
        }

        private static LayerNodeData[] BuildLayerNodes(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<LayerNodeData>();
            }

            var layerNodes = new LayerNodeData[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                LayerDefinitionSerializable layer = layers[i];
                layerNodes[i] = new LayerNodeData(
                    i,
                    layer?.name ?? string.Empty,
                    layer?.priority ?? 0,
                    layer?.exclusionMode ?? 0,
                    layer?.layerOverrideMask);
            }

            return layerNodes;
        }

        private static WiringEdgeData[] BuildEdges(
            IReadOnlyList<LayerDefinitionSerializable> layers,
            IReadOnlyList<InvalidDeclarationRef> invalidDeclarations)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<WiringEdgeData>();
            }

            var invalidSet = new HashSet<(int layerIndex, int declarationIndex)>();
            for (int i = 0; i < invalidDeclarations.Count; i++)
            {
                InvalidDeclarationRef invalid = invalidDeclarations[i];
                invalidSet.Add((invalid.LayerIndex, invalid.DeclarationIndex));
            }

            var edges = new List<WiringEdgeData>();
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                IList<InputSourceDeclarationSerializable> declarations = layers[layerIndex]?.inputSources;
                if (declarations == null)
                {
                    continue;
                }

                for (int declarationIndex = 0; declarationIndex < declarations.Count; declarationIndex++)
                {
                    if (invalidSet.Contains((layerIndex, declarationIndex)))
                    {
                        continue;
                    }

                    InputSourceDeclarationSerializable declaration = declarations[declarationIndex];
                    edges.Add(new WiringEdgeData(
                        layerIndex,
                        declaration?.id ?? string.Empty,
                        declaration?.weight ?? 0f));
                }
            }

            return edges.ToArray();
        }

        private static DanglingEdgeData[] BuildInvalidEdges(IReadOnlyList<InvalidDeclarationRef> invalidDeclarations)
        {
            if (invalidDeclarations == null || invalidDeclarations.Count == 0)
            {
                return Array.Empty<DanglingEdgeData>();
            }

            var invalidEdges = new DanglingEdgeData[invalidDeclarations.Count];
            for (int i = 0; i < invalidDeclarations.Count; i++)
            {
                InvalidDeclarationRef invalid = invalidDeclarations[i];
                invalidEdges[i] = new DanglingEdgeData(invalid.LayerIndex, invalid.DeclarationIndex, invalid.Id);
            }

            return invalidEdges;
        }

        private static OutputNodeData BuildOutputNode(IReadOnlyList<LayerNodeData> layerNodes)
        {
            if (layerNodes == null || layerNodes.Count == 0)
            {
                return new OutputNodeData(Array.Empty<OutputLayerData>());
            }

            var orderedLayers = new List<OutputLayerData>(layerNodes.Count);
            for (int i = 0; i < layerNodes.Count; i++)
            {
                LayerNodeData layer = layerNodes[i];
                orderedLayers.Add(new OutputLayerData(
                    layer.LayerIndex,
                    layer.Name,
                    layer.Priority,
                    layer.OverrideMask));
            }

            orderedLayers.Sort(static (left, right) =>
            {
                int priorityCompare = left.Priority.CompareTo(right.Priority);
                if (priorityCompare != 0)
                {
                    return priorityCompare;
                }

                return left.LayerIndex.CompareTo(right.LayerIndex);
            });

            return new OutputNodeData(orderedLayers);
        }
    }
}
