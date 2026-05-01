using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// SO Serializable → Domain <see cref="FacialProfile"/> 変換器。
    /// Phase 3.6 (inspector-and-data-model-redesign) で schema v2.0 専用となり、
    /// <see cref="ExpressionSerializable.cachedSnapshot"/> から BlendShape 値 / 遷移メタを
    /// 展開（snapshot 展開ロジック）して Domain Expression を構築する。
    /// </summary>
    public static class FacialCharacterProfileConverter
    {
        public static FacialProfile ToFacialProfile(
            string schemaVersion,
            IReadOnlyList<LayerDefinitionSerializable> layers,
            IReadOnlyList<ExpressionSerializable> expressions,
            IReadOnlyList<string> rendererPaths)
        {
            string version = string.IsNullOrWhiteSpace(schemaVersion)
                ? SystemTextJsonParser.SchemaVersionV2
                : schemaVersion;
            var layerArr = ConvertLayers(layers);
            var inputSourceArr = ConvertLayerInputSources(layers);
            var expressionArr = ConvertExpressions(expressions);
            var rendererArr = ConvertStrings(rendererPaths);
            return new FacialProfile(version, layerArr, expressionArr, rendererArr, inputSourceArr);
        }

        public static AnalogInputBindingProfile ToAnalogProfile(
            string version,
            IReadOnlyList<AnalogBindingEntrySerializable> bindings)
        {
            if (bindings == null || bindings.Count == 0)
                return new AnalogInputBindingProfile(version ?? string.Empty, Array.Empty<AnalogBindingEntry>());
            var entries = new List<AnalogBindingEntry>(bindings.Count);
            for (int i = 0; i < bindings.Count; i++)
            {
                var src = bindings[i];
                if (src == null || string.IsNullOrWhiteSpace(src.targetIdentifier)) continue;
                AnalogBindingEntry entry;
                try
                {
                    entry = new AnalogBindingEntry(src.inputActionRef ?? string.Empty, 0, AnalogBindingTargetKind.BlendShape, src.targetIdentifier, src.targetAxis);
                }
                catch (ArgumentOutOfRangeException) { continue; }
                catch (ArgumentException) { continue; }
                entries.Add(entry);
            }
            return new AnalogInputBindingProfile(version ?? string.Empty, entries.ToArray());
        }

        private static LayerDefinition[] ConvertLayers(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0) return Array.Empty<LayerDefinition>();
            var result = new List<LayerDefinition>(layers.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                var src = layers[i];
                if (src == null || string.IsNullOrWhiteSpace(src.name)) continue;
                int priority = src.priority < 0 ? 0 : src.priority;
                result.Add(new LayerDefinition(src.name, priority, src.exclusionMode));
            }
            return result.ToArray();
        }

        private static InputSourceDeclaration[][] ConvertLayerInputSources(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0) return Array.Empty<InputSourceDeclaration[]>();
            var result = new List<InputSourceDeclaration[]>(layers.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                var src = layers[i];
                if (src == null || string.IsNullOrWhiteSpace(src.name)) continue;
                if (src.inputSources == null || src.inputSources.Count == 0) { result.Add(Array.Empty<InputSourceDeclaration>()); continue; }
                var arr = new List<InputSourceDeclaration>(src.inputSources.Count);
                for (int j = 0; j < src.inputSources.Count; j++)
                {
                    var s = src.inputSources[j];
                    if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
                    string options = string.IsNullOrEmpty(s.optionsJson) ? null : s.optionsJson;
                    arr.Add(new InputSourceDeclaration(s.id, s.weight, options));
                }
                result.Add(arr.ToArray());
            }
            return result.ToArray();
        }

        private static Expression[] ConvertExpressions(IReadOnlyList<ExpressionSerializable> expressions)
        {
            if (expressions == null || expressions.Count == 0) return Array.Empty<Expression>();
            var result = new List<Expression>(expressions.Count);
            for (int i = 0; i < expressions.Count; i++)
            {
                var src = expressions[i];
                if (src == null || string.IsNullOrWhiteSpace(src.id) || string.IsNullOrWhiteSpace(src.name) || string.IsNullOrWhiteSpace(src.layer)) continue;

                float duration;
                TransitionCurve curve;
                BlendShapeMapping[] blendShapes;

                if (src.cachedSnapshot != null)
                {
                    duration = src.cachedSnapshot.transitionDuration;
                    curve = ConvertTransitionCurvePreset(src.cachedSnapshot.transitionCurvePreset);
                    blendShapes = ConvertSnapshotBlendShapes(src.cachedSnapshot.blendShapes);
                }
                else
                {
                    duration = src.transitionDuration;
                    curve = ConvertTransitionCurve(src.transitionCurve);
                    blendShapes = ConvertBlendShapes(src.blendShapeValues);
                }

                result.Add(new Expression(src.id, src.name, src.layer, duration, curve, blendShapes));
            }
            return result.ToArray();
        }

        private static BlendShapeMapping[] ConvertBlendShapes(IReadOnlyList<BlendShapeMappingSerializable> mappings)
        {
            if (mappings == null || mappings.Count == 0) return Array.Empty<BlendShapeMapping>();
            var result = new List<BlendShapeMapping>(mappings.Count);
            for (int i = 0; i < mappings.Count; i++)
            {
                var src = mappings[i];
                if (src == null || string.IsNullOrEmpty(src.name)) continue;
                string renderer = string.IsNullOrEmpty(src.renderer) ? null : src.renderer;
                result.Add(new BlendShapeMapping(src.name, src.value, renderer));
            }
            return result.ToArray();
        }

        private static BlendShapeMapping[] ConvertSnapshotBlendShapes(IReadOnlyList<BlendShapeSnapshotDto> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0) return Array.Empty<BlendShapeMapping>();
            var result = new List<BlendShapeMapping>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                var src = snapshots[i];
                if (src == null || string.IsNullOrEmpty(src.name)) continue;
                string renderer = string.IsNullOrEmpty(src.rendererPath) ? null : src.rendererPath;
                result.Add(new BlendShapeMapping(src.name, src.value, renderer));
            }
            return result.ToArray();
        }

        private static TransitionCurve ConvertTransitionCurve(TransitionCurveSerializable curve)
        {
            if (curve == null) return TransitionCurve.Linear;
            CurveKeyFrame[] keys;
            if (curve.keys == null || curve.keys.Count == 0) { keys = Array.Empty<CurveKeyFrame>(); }
            else
            {
                keys = new CurveKeyFrame[curve.keys.Count];
                for (int i = 0; i < curve.keys.Count; i++)
                {
                    var k = curve.keys[i] ?? new CurveKeyFrameSerializable();
                    keys[i] = new CurveKeyFrame(k.time, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight, k.weightedMode);
                }
            }
            return new TransitionCurve(curve.type, keys);
        }

        private static TransitionCurve ConvertTransitionCurvePreset(string preset)
        {
            if (string.IsNullOrEmpty(preset))
                return TransitionCurve.Linear;

            return preset.Trim() switch
            {
                "Linear"    => new TransitionCurve(TransitionCurveType.Linear),
                "EaseIn"    => new TransitionCurve(TransitionCurveType.EaseIn),
                "EaseOut"   => new TransitionCurve(TransitionCurveType.EaseOut),
                "EaseInOut" => new TransitionCurve(TransitionCurveType.EaseInOut),
                _ => TransitionCurve.Linear
            };
        }

        private static string[] ConvertStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            var result = new string[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i] ?? string.Empty;
            return result;
        }
    }
}
