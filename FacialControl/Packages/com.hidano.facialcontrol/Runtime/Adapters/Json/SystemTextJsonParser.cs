using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.Json
{
    /// <summary>
    /// IJsonParser の実装。Unity の JsonUtility をベースに、
    /// DTO を介してドメインモデルとの変換を行う。
    /// schemaVersion チェックと不正 JSON 時の例外スローを提供する。
    /// </summary>
    public sealed class SystemTextJsonParser : IJsonParser
    {
        private const string SupportedSchemaVersion = "1.0";

        /// <inheritdoc/>
        public FacialProfile ParseProfile(string json)
        {
            var dto = ParseProfileDto(json);
            return ConvertToProfile(dto);
        }

        /// <summary>
        /// <c>layers[].inputSources[]</c> をパースして、レイヤー順に <see cref="InputSourceDto"/> 配列を返す。
        /// <see cref="InputSourceDto.optionsJson"/> は JSON 上の <c>options</c> オブジェクトの生 JSON サブ文字列を保持する
        /// （JsonUtility の 1 段ネスト object 非対応を回避、Req 3.1 / 3.7, Critical 2）。
        /// </summary>
        /// <param name="json">プロファイル JSON 文字列</param>
        /// <returns>レイヤー順に並んだ <see cref="InputSourceDto"/> 配列の配列</returns>
        /// <exception cref="FormatException">
        /// いずれかのレイヤーで <c>inputSources</c> が欠落 / 空配列の場合
        /// （preview 破壊的変更 D-5, Req 3.2）。
        /// </exception>
        public InputSourceDto[][] ParseLayerInputSources(string json)
        {
            var dto = ParseProfileDto(json);
            return ExtractInputSources(dto);
        }

        private static ProfileDto ParseProfileDto(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 文字列を空にすることはできません。", nameof(json));

            // JsonUtility は 1 段ネスト object を直接デシリアライズできないため、
            // inputSources[].options を生 JSON 文字列として optionsJson フィールドに退避する。
            string preprocessed = PreprocessInputSourceOptions(json);

            ProfileDto dto;
            try
            {
                dto = JsonUtility.FromJson<ProfileDto>(preprocessed);
            }
            catch (Exception ex)
            {
                throw new FormatException("プロファイル JSON のパースに失敗しました。", ex);
            }

            if (dto == null)
                throw new FormatException("プロファイル JSON のパースに失敗しました。結果が null です。");

            if (string.IsNullOrEmpty(dto.schemaVersion))
                throw new FormatException("schemaVersion が指定されていません。");

            ValidateSchemaVersion(dto.schemaVersion);

            ValidateLayerInputSources(dto);

            return dto;
        }

        // preview 破壊的変更 (D-5, Req 3.2): inputSources は必須フィールド。欠落 / 空配列はエラー。
        private static void ValidateLayerInputSources(ProfileDto dto)
        {
            if (dto.layers == null)
                return;

            for (int i = 0; i < dto.layers.Count; i++)
            {
                var layer = dto.layers[i];
                if (layer == null)
                    continue;

                if (layer.inputSources == null || layer.inputSources.Count == 0)
                {
                    throw new FormatException(
                        $"レイヤー '{layer.name}' に必須フィールド 'inputSources' が欠落しています。" +
                        "preview 段階の破壊的変更 (D-5) により、inputSources は各レイヤー必須かつ非空配列である必要があります。");
                }
            }
        }

        private static InputSourceDto[][] ExtractInputSources(ProfileDto dto)
        {
            if (dto.layers == null || dto.layers.Count == 0)
                return Array.Empty<InputSourceDto[]>();

            var result = new InputSourceDto[dto.layers.Count][];
            for (int i = 0; i < dto.layers.Count; i++)
            {
                var entries = dto.layers[i].inputSources;
                var array = new InputSourceDto[entries.Count];
                for (int j = 0; j < entries.Count; j++)
                {
                    var src = entries[j];
                    // JsonUtility はフィールドの初期化子を尊重しないケースがあるため、
                    // optionsJson が未設定のときは空文字列として扱わない（null のまま返す）。
                    array[j] = new InputSourceDto
                    {
                        id = src.id,
                        weight = src.weight,
                        optionsJson = string.IsNullOrEmpty(src.optionsJson) ? null : src.optionsJson
                    };
                }
                result[i] = array;
            }
            return result;
        }

        /// <inheritdoc/>
        public string SerializeProfile(FacialProfile profile)
        {
            var dto = ConvertToProfileDto(profile);
            return JsonUtility.ToJson(dto, true);
        }

        /// <inheritdoc/>
        public FacialControlConfig ParseConfig(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 文字列を空にすることはできません。", nameof(json));

            ConfigDto dto;
            try
            {
                dto = JsonUtility.FromJson<ConfigDto>(json);
            }
            catch (Exception ex)
            {
                throw new FormatException("設定 JSON のパースに失敗しました。", ex);
            }

            if (dto == null)
                throw new FormatException("設定 JSON のパースに失敗しました。結果が null です。");

            if (string.IsNullOrEmpty(dto.schemaVersion))
                throw new FormatException("schemaVersion が指定されていません。");

            ValidateSchemaVersion(dto.schemaVersion);

            return ConvertToConfig(dto);
        }

        /// <inheritdoc/>
        public string SerializeConfig(FacialControlConfig config)
        {
            var dto = ConvertToConfigDto(config);
            return JsonUtility.ToJson(dto, true);
        }

        private static void ValidateSchemaVersion(string version)
        {
            if (version != SupportedSchemaVersion)
                throw new FormatException(
                    $"サポートされていないスキーマバージョンです: {version}（サポート対象: {SupportedSchemaVersion}）");
        }

        // "options": { ... } を "optionsJson": "<escaped>" に書き換える。
        // JsonUtility が自由形式のネスト object を扱えないため、生 JSON サブ文字列に退避する。
        private static string PreprocessInputSourceOptions(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            const string OptionsKey = "\"options\"";
            var sb = new StringBuilder(json.Length + 64);
            int i = 0;

            while (i < json.Length)
            {
                int keyStart = json.IndexOf(OptionsKey, i, StringComparison.Ordinal);
                if (keyStart < 0)
                {
                    sb.Append(json, i, json.Length - i);
                    break;
                }

                sb.Append(json, i, keyStart - i);

                int afterKey = keyStart + OptionsKey.Length;
                int colonIdx = afterKey;
                while (colonIdx < json.Length && char.IsWhiteSpace(json[colonIdx]))
                    colonIdx++;

                if (colonIdx >= json.Length || json[colonIdx] != ':')
                {
                    // "options" が field key ではなく文字列中の単語として出現したケース。そのまま通す。
                    sb.Append(json, keyStart, afterKey - keyStart);
                    i = afterKey;
                    continue;
                }

                int valueStart = colonIdx + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    valueStart++;

                if (valueStart >= json.Length || json[valueStart] != '{')
                {
                    // options の値が object でない場合（null, string 等）は変換しない。
                    sb.Append(json, keyStart, valueStart - keyStart);
                    i = valueStart;
                    continue;
                }

                int objectEnd = FindMatchingBrace(json, valueStart);
                if (objectEnd < 0)
                {
                    // 閉じブラケットが見つからない不正 JSON。残りをそのまま通して、後段で例外にさせる。
                    sb.Append(json, keyStart, json.Length - keyStart);
                    i = json.Length;
                    break;
                }

                string rawOptions = json.Substring(valueStart, objectEnd - valueStart + 1);
                sb.Append("\"optionsJson\":\"");
                AppendJsonEscaped(sb, rawOptions);
                sb.Append('"');

                i = objectEnd + 1;
            }

            return sb.ToString();
        }

        private static int FindMatchingBrace(string json, int openIndex)
        {
            int depth = 1;
            int p = openIndex + 1;
            bool inString = false;
            bool escaped = false;

            while (p < json.Length)
            {
                char c = json[p];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                }
                else
                {
                    if (c == '"') inString = true;
                    else if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                            return p;
                    }
                }
                p++;
            }
            return -1;
        }

        private static void AppendJsonEscaped(StringBuilder sb, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
        }

        // --- Profile 変換 ---

        private static FacialProfile ConvertToProfile(ProfileDto dto)
        {
            var layers = ConvertLayers(dto.layers);
            var expressions = ConvertExpressions(dto.expressions);
            var rendererPaths = ConvertRendererPaths(dto.rendererPaths);
            return new FacialProfile(dto.schemaVersion, layers, expressions, rendererPaths);
        }

        private static string[] ConvertRendererPaths(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
                return null;

            return paths.ToArray();
        }

        private static LayerDefinition[] ConvertLayers(List<LayerDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<LayerDefinition>();

            var layers = new LayerDefinition[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                var exclusionMode = ParseExclusionMode(d.exclusionMode);
                layers[i] = new LayerDefinition(d.name, d.priority, exclusionMode);
            }
            return layers;
        }

        private static Expression[] ConvertExpressions(List<ExpressionDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<Expression>();

            var expressions = new Expression[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                var blendShapes = ConvertBlendShapeMappings(d.blendShapeValues);
                var layerSlots = ConvertLayerSlots(d.layerSlots);
                var curve = ConvertTransitionCurve(d.transitionCurve);

                expressions[i] = new Expression(
                    d.id,
                    d.name,
                    d.layer,
                    d.transitionDuration,
                    curve,
                    blendShapes,
                    layerSlots);
            }
            return expressions;
        }

        private static BlendShapeMapping[] ConvertBlendShapeMappings(List<BlendShapeMappingDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<BlendShapeMapping>();

            var mappings = new BlendShapeMapping[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                mappings[i] = new BlendShapeMapping(
                    d.name,
                    d.value,
                    string.IsNullOrEmpty(d.renderer) ? null : d.renderer);
            }
            return mappings;
        }

        private static LayerSlot[] ConvertLayerSlots(List<LayerSlotDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<LayerSlot>();

            var slots = new LayerSlot[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                var blendShapes = ConvertBlendShapeMappings(d.blendShapeValues);
                slots[i] = new LayerSlot(d.layer, blendShapes);
            }
            return slots;
        }

        private static TransitionCurve ConvertTransitionCurve(TransitionCurveDto dto)
        {
            if (dto == null)
                return TransitionCurve.Linear;

            var type = ParseTransitionCurveType(dto.type);
            var keys = ConvertCurveKeyFrames(dto.keys);
            return new TransitionCurve(type, keys);
        }

        private static CurveKeyFrame[] ConvertCurveKeyFrames(List<CurveKeyFrameDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<CurveKeyFrame>();

            var keys = new CurveKeyFrame[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                keys[i] = new CurveKeyFrame(
                    d.time, d.value, d.inTangent, d.outTangent,
                    d.inWeight, d.outWeight, d.weightedMode);
            }
            return keys;
        }

        private static ExclusionMode ParseExclusionMode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return ExclusionMode.LastWins;

            return value.ToLowerInvariant() switch
            {
                "lastwins" => ExclusionMode.LastWins,
                "blend" => ExclusionMode.Blend,
                _ => throw new FormatException($"不正な ExclusionMode 値: {value}")
            };
        }

        private static TransitionCurveType ParseTransitionCurveType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return TransitionCurveType.Linear;

            return value.ToLowerInvariant() switch
            {
                "linear" => TransitionCurveType.Linear,
                "easein" => TransitionCurveType.EaseIn,
                "easeout" => TransitionCurveType.EaseOut,
                "easeinout" => TransitionCurveType.EaseInOut,
                "custom" => TransitionCurveType.Custom,
                _ => throw new FormatException($"不正な TransitionCurveType 値: {value}")
            };
        }

        // --- Profile → DTO 変換 ---

        private static ProfileDto ConvertToProfileDto(FacialProfile profile)
        {
            var dto = new ProfileDto
            {
                schemaVersion = profile.SchemaVersion,
                layers = new List<LayerDto>(),
                expressions = new List<ExpressionDto>(),
                rendererPaths = new List<string>()
            };

            var rendererPathsSpan = profile.RendererPaths.Span;
            for (int i = 0; i < rendererPathsSpan.Length; i++)
            {
                dto.rendererPaths.Add(rendererPathsSpan[i]);
            }

            var layerSpan = profile.Layers.Span;
            for (int i = 0; i < layerSpan.Length; i++)
            {
                // preview 破壊的変更 (D-5, Req 3.2): inputSources は必須フィールドのため、
                // FacialProfile が inputSources を保持していない段階では round-trip を壊さないよう
                // 最小の既定エントリ（controller-expr, weight=1.0）を placeholder として出力する。
                // 実際の inputSources の round-trip (Req 3.5, 8.4, 7.4) は後続タスクで本 DTO パイプラインに統合する。
                dto.layers.Add(new LayerDto
                {
                    name = layerSpan[i].Name,
                    priority = layerSpan[i].Priority,
                    exclusionMode = SerializeExclusionMode(layerSpan[i].ExclusionMode),
                    inputSources = new List<InputSourceDto>
                    {
                        new InputSourceDto { id = "controller-expr", weight = 1.0f }
                    }
                });
            }

            var exprSpan = profile.Expressions.Span;
            for (int i = 0; i < exprSpan.Length; i++)
            {
                dto.expressions.Add(ConvertToExpressionDto(exprSpan[i]));
            }

            return dto;
        }

        private static ExpressionDto ConvertToExpressionDto(Expression expr)
        {
            var dto = new ExpressionDto
            {
                id = expr.Id,
                name = expr.Name,
                layer = expr.Layer,
                transitionDuration = expr.TransitionDuration,
                transitionCurve = ConvertToTransitionCurveDto(expr.TransitionCurve),
                blendShapeValues = new List<BlendShapeMappingDto>(),
                layerSlots = new List<LayerSlotDto>()
            };

            var bsSpan = expr.BlendShapeValues.Span;
            for (int i = 0; i < bsSpan.Length; i++)
            {
                dto.blendShapeValues.Add(new BlendShapeMappingDto
                {
                    name = bsSpan[i].Name,
                    value = bsSpan[i].Value,
                    renderer = bsSpan[i].Renderer ?? ""
                });
            }

            var slotSpan = expr.LayerSlots.Span;
            for (int i = 0; i < slotSpan.Length; i++)
            {
                var slotDto = new LayerSlotDto
                {
                    layer = slotSpan[i].Layer,
                    blendShapeValues = new List<BlendShapeMappingDto>()
                };

                var slotBsSpan = slotSpan[i].BlendShapeValues.Span;
                for (int j = 0; j < slotBsSpan.Length; j++)
                {
                    slotDto.blendShapeValues.Add(new BlendShapeMappingDto
                    {
                        name = slotBsSpan[j].Name,
                        value = slotBsSpan[j].Value,
                        renderer = slotBsSpan[j].Renderer ?? ""
                    });
                }

                dto.layerSlots.Add(slotDto);
            }

            return dto;
        }

        private static TransitionCurveDto ConvertToTransitionCurveDto(TransitionCurve curve)
        {
            var dto = new TransitionCurveDto
            {
                type = SerializeTransitionCurveType(curve.Type),
                keys = new List<CurveKeyFrameDto>()
            };

            var keysSpan = curve.Keys.Span;
            for (int i = 0; i < keysSpan.Length; i++)
            {
                dto.keys.Add(new CurveKeyFrameDto
                {
                    time = keysSpan[i].Time,
                    value = keysSpan[i].Value,
                    inTangent = keysSpan[i].InTangent,
                    outTangent = keysSpan[i].OutTangent,
                    inWeight = keysSpan[i].InWeight,
                    outWeight = keysSpan[i].OutWeight,
                    weightedMode = keysSpan[i].WeightedMode
                });
            }

            return dto;
        }

        private static string SerializeExclusionMode(ExclusionMode mode)
        {
            return mode switch
            {
                ExclusionMode.LastWins => "lastWins",
                ExclusionMode.Blend => "blend",
                _ => "lastWins"
            };
        }

        private static string SerializeTransitionCurveType(TransitionCurveType type)
        {
            return type switch
            {
                TransitionCurveType.Linear => "linear",
                TransitionCurveType.EaseIn => "easeIn",
                TransitionCurveType.EaseOut => "easeOut",
                TransitionCurveType.EaseInOut => "easeInOut",
                TransitionCurveType.Custom => "custom",
                _ => "linear"
            };
        }

        // --- Config 変換 ---

        private static FacialControlConfig ConvertToConfig(ConfigDto dto)
        {
            var oscMappings = ConvertOscMappings(dto.osc?.mapping);
            var osc = new OscConfiguration(
                dto.osc?.sendPort ?? OscConfiguration.DefaultSendPort,
                dto.osc?.receivePort ?? OscConfiguration.DefaultReceivePort,
                dto.osc?.preset ?? OscConfiguration.DefaultPreset,
                oscMappings);

            var cache = new CacheConfiguration(
                dto.cache?.animationClipLruSize ?? CacheConfiguration.DefaultAnimationClipLruSize);

            return new FacialControlConfig(dto.schemaVersion, osc, cache);
        }

        private static OscMapping[] ConvertOscMappings(List<OscMappingDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Array.Empty<OscMapping>();

            var mappings = new OscMapping[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                mappings[i] = new OscMapping(d.oscAddress, d.blendShapeName, d.layer);
            }
            return mappings;
        }

        private static ConfigDto ConvertToConfigDto(FacialControlConfig config)
        {
            var dto = new ConfigDto
            {
                schemaVersion = config.SchemaVersion,
                osc = new OscConfigurationDto
                {
                    sendPort = config.Osc.SendPort,
                    receivePort = config.Osc.ReceivePort,
                    preset = config.Osc.Preset,
                    mapping = new List<OscMappingDto>()
                },
                cache = new CacheConfigurationDto
                {
                    animationClipLruSize = config.Cache.AnimationClipLruSize
                }
            };

            var mappingSpan = config.Osc.Mapping.Span;
            for (int i = 0; i < mappingSpan.Length; i++)
            {
                dto.osc.mapping.Add(new OscMappingDto
                {
                    oscAddress = mappingSpan[i].OscAddress,
                    blendShapeName = mappingSpan[i].BlendShapeName,
                    layer = mappingSpan[i].Layer
                });
            }

            return dto;
        }

        // ====================================================================
        // DTO 定義（JsonUtility 用の Serializable クラス）
        // ====================================================================

        [Serializable]
        private class ProfileDto
        {
            public string schemaVersion;
            public List<LayerDto> layers;
            public List<ExpressionDto> expressions;
            public List<string> rendererPaths;
        }

        [Serializable]
        private class LayerDto
        {
            public string name;
            public int priority;
            public string exclusionMode;
            public List<InputSourceDto> inputSources;
        }

        [Serializable]
        private class ExpressionDto
        {
            public string id;
            public string name;
            public string layer;
            public float transitionDuration = 0.25f;
            public TransitionCurveDto transitionCurve;
            public List<BlendShapeMappingDto> blendShapeValues;
            public List<LayerSlotDto> layerSlots;
        }

        [Serializable]
        private class TransitionCurveDto
        {
            public string type;
            public List<CurveKeyFrameDto> keys;
        }

        [Serializable]
        private class CurveKeyFrameDto
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
            public float inWeight;
            public float outWeight;
            public int weightedMode;
        }

        [Serializable]
        private class BlendShapeMappingDto
        {
            public string name;
            public float value;
            public string renderer;
        }

        [Serializable]
        private class LayerSlotDto
        {
            public string layer;
            public List<BlendShapeMappingDto> blendShapeValues;
        }

        [Serializable]
        private class ConfigDto
        {
            public string schemaVersion;
            public OscConfigurationDto osc;
            public CacheConfigurationDto cache;
        }

        [Serializable]
        private class OscConfigurationDto
        {
            public int sendPort = OscConfiguration.DefaultSendPort;
            public int receivePort = OscConfiguration.DefaultReceivePort;
            public string preset = OscConfiguration.DefaultPreset;
            public List<OscMappingDto> mapping;
        }

        [Serializable]
        private class OscMappingDto
        {
            public string oscAddress;
            public string blendShapeName;
            public string layer;
        }

        [Serializable]
        private class CacheConfigurationDto
        {
            public int animationClipLruSize = CacheConfiguration.DefaultAnimationClipLruSize;
        }
    }
}
