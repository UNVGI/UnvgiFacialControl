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
    /// 不正エントリ（未知の <c>targetKind</c> / 欠損 <c>targetIdentifier</c> 等）は
    /// <see cref="Debug.LogWarning"/> + skip + 残余ロード継続（Req 6.5）。
    /// JSON パース自体に失敗した場合も警告ログを出して空プロファイルを返し、例外伝播はしない。
    /// </para>
    /// <para>
    /// targetKind / targetAxis の文字列値は大小無視で解釈する。
    /// Phase 3.5 で <c>mapping</c> field を撤去したため、dead-zone / scale / offset / curve / invert / clamp の
    /// 値変換は Adapters 側 InputProcessor 経路で扱う（Decision 4 / Req 13.3）。
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

            try
            {
                entry = new AnalogBindingEntry(
                    sourceId: dto.sourceId ?? string.Empty,
                    sourceAxis: dto.sourceAxis,
                    targetKind: targetKind,
                    targetIdentifier: dto.targetIdentifier,
                    targetAxis: targetAxis);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AnalogInputBindingJsonLoader: bindings[{index}] の構築に失敗したため skip します: {ex.Message}");
                return false;
            }
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

        private static AnalogBindingEntryDto ConvertEntryToDto(in AnalogBindingEntry entry)
        {
            return new AnalogBindingEntryDto
            {
                sourceId = entry.SourceId ?? string.Empty,
                sourceAxis = entry.SourceAxis,
                targetKind = SerializeTargetKind(entry.TargetKind),
                targetIdentifier = entry.TargetIdentifier,
                targetAxis = SerializeTargetAxis(entry.TargetAxis)
            };
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
    }
}
