using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.Json
{
    /// <summary>
    /// <see cref="AnalogInputBindingProfile"/> の JSON 永続化（Load / Save、Req 6.3〜6.5, 6.7, 6.8, 9.6）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>JsonUtility.FromJson&lt;AnalogInputBindingProfileDto&gt;</c> でデシリアライズ後、
    /// 各エントリを <see cref="AnalogBindingEntry"/> へ変換する。
    /// 不正エントリ（未知の <c>targetKind</c> / 欠損 <c>targetIdentifier</c> / <c>min &gt; max</c> 等）は
    /// <see cref="Debug.LogWarning"/> + skip + 残余ロード継続（Req 6.5）。
    /// JSON パース自体に失敗した場合も警告ログを出して空プロファイルを返し、例外伝播はしない。
    /// </para>
    /// <para>
    /// curveType / targetKind / targetAxis の文字列値は大小無視で解釈する（既存
    /// <c>SystemTextJsonParser.ParseTransitionCurveType</c> の規約に整合）。
    /// </para>
    /// </remarks>
    public static class AnalogInputBindingJsonLoader
    {
        /// <summary>
        /// JSON 文字列を <see cref="AnalogInputBindingProfile"/> へ変換する。
        /// </summary>
        /// <param name="json">JSON 文字列（null / 空 / 全空白は空プロファイルを返す）。</param>
        public static AnalogInputBindingProfile Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AnalogInputBindingProfile(string.Empty, Array.Empty<AnalogBindingEntry>());
            }

            AnalogInputBindingProfileDto dto;
            try
            {
                dto = JsonUtility.FromJson<AnalogInputBindingProfileDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: JSON のパースに失敗しました: {ex.Message}");
                return new AnalogInputBindingProfile(string.Empty, Array.Empty<AnalogBindingEntry>());
            }

            if (dto == null)
            {
                return new AnalogInputBindingProfile(string.Empty, Array.Empty<AnalogBindingEntry>());
            }

            var version = dto.version ?? string.Empty;

            if (dto.bindings == null || dto.bindings.Count == 0)
            {
                return new AnalogInputBindingProfile(version, Array.Empty<AnalogBindingEntry>());
            }

            var entries = new List<AnalogBindingEntry>(dto.bindings.Count);
            for (int i = 0; i < dto.bindings.Count; i++)
            {
                var entryDto = dto.bindings[i];
                if (TryConvertEntry(entryDto, i, out var entry))
                {
                    entries.Add(entry);
                }
            }

            return new AnalogInputBindingProfile(version, entries.ToArray());
        }

        /// <summary>
        /// <see cref="AnalogInputBindingProfile"/> を JSON 文字列へ変換する。
        /// </summary>
        /// <param name="profile">永続化対象プロファイル。</param>
        /// <param name="prettyPrint">JsonUtility の pretty print 指定（既定 true）。</param>
        public static string Save(in AnalogInputBindingProfile profile, bool prettyPrint = true)
        {
            var dto = new AnalogInputBindingProfileDto
            {
                version = profile.Version,
                bindings = new List<AnalogBindingEntryDto>(profile.Bindings.Length)
            };

            var bindings = profile.Bindings.Span;
            for (int i = 0; i < bindings.Length; i++)
            {
                dto.bindings.Add(ConvertEntryToDto(bindings[i]));
            }

            return JsonUtility.ToJson(dto, prettyPrint);
        }

        private static bool TryConvertEntry(AnalogBindingEntryDto dto, int index, out AnalogBindingEntry entry)
        {
            entry = default;

            if (dto == null)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] が null のため skip します。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.targetIdentifier))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の targetIdentifier が空のため skip します。");
                return false;
            }

            if (dto.sourceAxis < 0)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の sourceAxis が負 ({dto.sourceAxis}) のため skip します。");
                return false;
            }

            if (!TryParseTargetKind(dto.targetKind, out var targetKind))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の targetKind '{dto.targetKind}' が未知のため skip します。");
                return false;
            }

            if (!TryParseTargetAxis(dto.targetAxis, out var targetAxis))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の targetAxis '{dto.targetAxis}' が未知のため skip します。");
                return false;
            }

            if (!TryConvertMapping(dto.mapping, index, out var mapping))
            {
                return false;
            }

            try
            {
                entry = new AnalogBindingEntry(
                    sourceId: dto.sourceId ?? string.Empty,
                    sourceAxis: dto.sourceAxis,
                    targetKind: targetKind,
                    targetIdentifier: dto.targetIdentifier,
                    targetAxis: targetAxis,
                    mapping: mapping);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の構築に失敗したため skip します: {ex.Message}");
                return false;
            }
        }

        private static bool TryConvertMapping(AnalogMappingDto dto, int index, out AnalogMappingFunction mapping)
        {
            if (dto == null)
            {
                mapping = AnalogMappingFunction.Identity;
                return true;
            }

            if (!IsFinite(dto.deadZone) || !IsFinite(dto.scale) || !IsFinite(dto.offset)
                || !IsFinite(dto.min) || !IsFinite(dto.max))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}].mapping に NaN/Inf が含まれるため skip します。");
                mapping = default;
                return false;
            }

            if (dto.min > dto.max)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}].mapping の min ({dto.min}) > max ({dto.max}) のため skip します。");
                mapping = default;
                return false;
            }

            if (!TryParseCurveType(dto.curveType, out var curveType))
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}].mapping の curveType '{dto.curveType}' が未知のため skip します。");
                mapping = default;
                return false;
            }

            var keys = ConvertCurveKeyFrames(dto.curveKeyFrames);
            var curve = new TransitionCurve(curveType, keys);

            try
            {
                mapping = new AnalogMappingFunction(
                    deadZone: dto.deadZone,
                    scale: dto.scale,
                    offset: dto.offset,
                    curve: curve,
                    invert: dto.invert,
                    min: dto.min,
                    max: dto.max);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}].mapping の構築に失敗したため skip します: {ex.Message}");
                mapping = default;
                return false;
            }
        }

        private static CurveKeyFrame[] ConvertCurveKeyFrames(List<CurveKeyFrameDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
            {
                return Array.Empty<CurveKeyFrame>();
            }

            var keys = new CurveKeyFrame[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i] ?? new CurveKeyFrameDto();
                keys[i] = new CurveKeyFrame(
                    time: d.time,
                    value: d.value,
                    inTangent: d.inTangent,
                    outTangent: d.outTangent,
                    inWeight: d.inWeight,
                    outWeight: d.outWeight,
                    weightedMode: d.weightedMode);
            }
            return keys;
        }

        private static bool TryParseTargetKind(string value, out AnalogBindingTargetKind result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default;
                return false;
            }

            switch (value.ToLowerInvariant())
            {
                case "blendshape":
                    result = AnalogBindingTargetKind.BlendShape;
                    return true;
                case "bonepose":
                    result = AnalogBindingTargetKind.BonePose;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryParseTargetAxis(string value, out AnalogTargetAxis result)
        {
            // BlendShape ターゲットでは TargetAxis は無視されるため、null / 空は X 既定で許容する。
            if (string.IsNullOrWhiteSpace(value))
            {
                result = AnalogTargetAxis.X;
                return true;
            }

            switch (value.ToLowerInvariant())
            {
                case "x":
                    result = AnalogTargetAxis.X;
                    return true;
                case "y":
                    result = AnalogTargetAxis.Y;
                    return true;
                case "z":
                    result = AnalogTargetAxis.Z;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryParseCurveType(string value, out TransitionCurveType result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = TransitionCurveType.Linear;
                return true;
            }

            switch (value.ToLowerInvariant())
            {
                case "linear":
                    result = TransitionCurveType.Linear;
                    return true;
                case "easein":
                    result = TransitionCurveType.EaseIn;
                    return true;
                case "easeout":
                    result = TransitionCurveType.EaseOut;
                    return true;
                case "easeinout":
                    result = TransitionCurveType.EaseInOut;
                    return true;
                case "custom":
                    result = TransitionCurveType.Custom;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static AnalogBindingEntryDto ConvertEntryToDto(in AnalogBindingEntry entry)
        {
            return new AnalogBindingEntryDto
            {
                sourceId = entry.SourceId ?? string.Empty,
                sourceAxis = entry.SourceAxis,
                targetKind = SerializeTargetKind(entry.TargetKind),
                targetIdentifier = entry.TargetIdentifier,
                targetAxis = SerializeTargetAxis(entry.TargetAxis),
                mapping = ConvertMappingToDto(entry.Mapping)
            };
        }

        private static AnalogMappingDto ConvertMappingToDto(in AnalogMappingFunction mapping)
        {
            var dto = new AnalogMappingDto
            {
                deadZone = mapping.DeadZone,
                scale = mapping.Scale,
                offset = mapping.Offset,
                curveType = SerializeCurveType(mapping.Curve.Type),
                curveKeyFrames = new List<CurveKeyFrameDto>(),
                invert = mapping.Invert,
                min = mapping.Min,
                max = mapping.Max
            };

            var keys = mapping.Curve.Keys.Span;
            for (int i = 0; i < keys.Length; i++)
            {
                dto.curveKeyFrames.Add(new CurveKeyFrameDto
                {
                    time = keys[i].Time,
                    value = keys[i].Value,
                    inTangent = keys[i].InTangent,
                    outTangent = keys[i].OutTangent,
                    inWeight = keys[i].InWeight,
                    outWeight = keys[i].OutWeight,
                    weightedMode = keys[i].WeightedMode
                });
            }

            return dto;
        }

        private static string SerializeTargetKind(AnalogBindingTargetKind kind)
        {
            return kind switch
            {
                AnalogBindingTargetKind.BlendShape => "blendshape",
                AnalogBindingTargetKind.BonePose => "bonepose",
                _ => "blendshape"
            };
        }

        private static string SerializeTargetAxis(AnalogTargetAxis axis)
        {
            return axis switch
            {
                AnalogTargetAxis.X => "X",
                AnalogTargetAxis.Y => "Y",
                AnalogTargetAxis.Z => "Z",
                _ => "X"
            };
        }

        private static string SerializeCurveType(TransitionCurveType type)
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
    }
}
