using System;
using System.Collections.Generic;
using System.IO;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Sampling;
using UnityEngine;
using Debug = UnityEngine.Debug;
using GazeBindingConfig = Hidano.FacialControl.Adapters.ScriptableObject.GazeBindingConfig;

namespace Hidano.FacialControl.Editor.AutoExport
{
    /// <summary>
    /// <see cref="FacialCharacterProfileSO"/> 系アセットの汎用エクスポート処理。
    /// AnimationClip サンプリング → <see cref="ExpressionSerializable.cachedSnapshot"/> 反映、
    /// および <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c> (schema v2.1) への
    /// 書き出しを行う。入力方式 (InputSystem / OSC / ARKit 等) には依存しない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// InputSystem 連携固有の追加エクスポート (analog_bindings.json) や Gaze clip サンプリングは
    /// inputsystem パッケージ側のラッパで本クラスを呼出した上で重ねる。これにより
    /// <see cref="FacialCharacterProfileSO"/> 抽象基底だけを参照するインスペクタやツールから
    /// 入力方式を問わずに永続化を実行できる。
    /// </para>
    /// </remarks>
    public static class FacialCharacterProfileExporter
    {
        /// <summary>
        /// 指定 SO 配下の各 Expression について、AnimationClip があれば sampler で時刻 0 サンプリングを行い、
        /// 結果を <see cref="ExpressionSerializable.cachedSnapshot"/> に書き戻す。
        /// AnimationClip が null の Expression はスキップする。
        /// </summary>
        /// <param name="so">対象 SO。null は no-op。</param>
        /// <param name="sampler">注入された sampler 実装。</param>
        public static void SampleAnimationClipsIntoCachedSnapshots(
            FacialCharacterProfileSO so,
            IExpressionAnimationClipSampler sampler)
        {
            if (so == null) return;
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));

            var expressions = so.Expressions;
            if (expressions == null) return;

            for (int i = 0; i < expressions.Count; i++)
            {
                var expr = expressions[i];
                if (expr == null || expr.animationClip == null)
                {
                    continue;
                }
                var snapshotId = string.IsNullOrEmpty(expr.id) ? string.Empty : expr.id;
                var snapshot = sampler.SampleSnapshot(snapshotId, expr.animationClip);
                var dto = ConvertSnapshotToDto(snapshot);
                // Inspector スライダー (expr.transitionDuration) を cachedSnapshot 側にも反映し、
                // 後続の JSON 出力経路と SO 直読み経路で値を一致させる。
                dto.transitionDuration = expr.transitionDuration;
                expr.cachedSnapshot = dto;
            }
        }

        /// <summary>
        /// 指定 SO の最新データを規約パス <c>StreamingAssets/FacialControl/{SO 名}/profile.json</c>
        /// に書き出す。本メソッドはサンプリング自体は行わないため、呼出側が事前に
        /// <see cref="SampleAnimationClipsIntoCachedSnapshots"/> を呼ぶか、Inspector で
        /// cachedSnapshot を埋めておく必要がある。
        /// </summary>
        /// <param name="so">エクスポート対象 SO。null の場合は何もせず false を返す。</param>
        /// <returns>少なくとも 1 ファイルを書き出せた場合 true。</returns>
        public static bool ExportProfileJson(FacialCharacterProfileSO so)
        {
            if (so == null)
            {
                return false;
            }

            string assetName = so.CharacterAssetName;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                Debug.LogWarning(
                    "FacialCharacterProfileExporter: SO 名が空のため StreamingAssets エクスポートをスキップします。"
                    + " SO アセットを保存し名前を確定させてください。");
                return false;
            }

            try
            {
                string profilePath = FacialCharacterProfileSO.GetStreamingAssetsProfilePath(assetName);
                if (string.IsNullOrEmpty(profilePath))
                {
                    return false;
                }
                EnsureParentDirectory(profilePath);
                var dto = BuildProfileSnapshotDto(so);
                var json = new SystemTextJsonParser().SerializeProfileSnapshot(dto);
                File.WriteAllText(profilePath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterProfileExporter: '{assetName}' の profile.json エクスポートに失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 親ディレクトリを必要に応じて作成する。<see cref="ExportProfileJson"/> から呼び出されるが、
        /// ラッパ側でも同パスへの書き出しを行う場合に再利用できるよう公開している。
        /// </summary>
        public static void EnsureParentDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // ============================================================
        // 内部ヘルパー: snapshot ↔ DTO 変換
        // ============================================================

        internal static ProfileSnapshotDto BuildProfileSnapshotDto(FacialCharacterProfileSO so)
        {
            var dto = new ProfileSnapshotDto
            {
                schemaVersion = string.IsNullOrEmpty(so.SchemaVersion) ? SystemTextJsonParser.SchemaVersionV2 : so.SchemaVersion,
                layers = new List<LayerDefinitionDto>(),
                expressions = new List<ExpressionDto>(),
                rendererPaths = new List<string>(),
                gazeConfigs = ConvertGazeConfigsToDto(so.GazeConfigs),
            };

            // top-level rendererPaths: Inspector 入力 + 各 Expression snapshot からの統合
            var rendererPathSet = new HashSet<string>(StringComparer.Ordinal);
            if (so.RendererPaths != null)
            {
                for (int i = 0; i < so.RendererPaths.Count; i++)
                {
                    var p = so.RendererPaths[i] ?? string.Empty;
                    if (rendererPathSet.Add(p)) dto.rendererPaths.Add(p);
                }
            }

            // layers
            if (so.Layers != null)
            {
                for (int i = 0; i < so.Layers.Count; i++)
                {
                    var src = so.Layers[i];
                    if (src == null) continue;
                    dto.layers.Add(new LayerDefinitionDto
                    {
                        name = src.name ?? string.Empty,
                        priority = src.priority,
                        exclusionMode = SerializeExclusionMode(src.exclusionMode),
                        inputSources = BuildInputSourceDtoList(src.inputSources),
                    });
                }
            }

            // expressions: cachedSnapshot を優先採用
            if (so.Expressions != null)
            {
                for (int i = 0; i < so.Expressions.Count; i++)
                {
                    var src = so.Expressions[i];
                    if (src == null) continue;

                    // LayerOverrideMask は所属 Layer から取得する。Layer 側が空なら従来の
                    // Expression 自体の mask（後方互換）を採用する。
                    var layerMask = FacialCharacterProfileConverter.ResolveLayerMask(so.Layers, src.layer);
                    var resolvedMask = (layerMask != null && layerMask.Count > 0)
                        ? CopyStringList(new List<string>(layerMask))
                        : CopyStringList(src.layerOverrideMask);

                    var exprDto = new ExpressionDto
                    {
                        id = src.id ?? string.Empty,
                        name = src.name ?? string.Empty,
                        layer = src.layer ?? string.Empty,
                        layerOverrideMask = resolvedMask,
                        snapshot = src.cachedSnapshot ?? CreateDefaultSnapshotDto(),
                    };

                    // Inspector スライダーの transitionDuration を JSON 出力にも反映する。
                    // cachedSnapshot 側がベイク時の旧値を保持していても、Inspector で編集された
                    // 最新値が runtime (StreamingAssets JSON 経路) でも採用されるように上書きする。
                    if (exprDto.snapshot != null)
                    {
                        exprDto.snapshot.transitionDuration = src.transitionDuration;
                    }

                    // Expression snapshot の rendererPaths を top-level set にマージ
                    if (exprDto.snapshot != null && exprDto.snapshot.rendererPaths != null)
                    {
                        for (int j = 0; j < exprDto.snapshot.rendererPaths.Count; j++)
                        {
                            var p = exprDto.snapshot.rendererPaths[j] ?? string.Empty;
                            if (rendererPathSet.Add(p)) dto.rendererPaths.Add(p);
                        }
                    }

                    dto.expressions.Add(exprDto);
                }
            }

            return dto;
        }

        private static List<GazeBindingConfigDto> ConvertGazeConfigsToDto(IReadOnlyList<GazeBindingConfig> configs)
        {
            if (configs == null || configs.Count == 0)
                return new List<GazeBindingConfigDto>();

            var result = new List<GazeBindingConfigDto>(configs.Count);
            for (int i = 0; i < configs.Count; i++)
            {
                var src = configs[i];
                if (src == null) continue;

                result.Add(new GazeBindingConfigDto
                {
                    expressionId = src.expressionId,
                    leftEyeBonePath = src.leftEyeBonePath,
                    leftEyeInitialRotation = src.leftEyeInitialRotation,
                    leftEyeYawAxisLocal = src.leftEyeYawAxisLocal,
                    leftEyePitchAxisLocal = src.leftEyePitchAxisLocal,
                    rightEyeBonePath = src.rightEyeBonePath,
                    rightEyeInitialRotation = src.rightEyeInitialRotation,
                    rightEyeYawAxisLocal = src.rightEyeYawAxisLocal,
                    rightEyePitchAxisLocal = src.rightEyePitchAxisLocal,
                    lookUpAngle = src.lookUpAngle,
                    lookDownAngle = src.lookDownAngle,
                    outerYawAngle = src.outerYawAngle,
                    innerYawAngle = src.innerYawAngle,
                });
            }

            return result;
        }

        private static ExpressionSnapshotDto ConvertSnapshotToDto(ExpressionSnapshot snapshot)
        {
            var dto = new ExpressionSnapshotDto
            {
                transitionDuration = snapshot.TransitionDuration,
                transitionCurvePreset = snapshot.TransitionCurvePreset.ToString(),
                blendShapes = new List<BlendShapeSnapshotDto>(snapshot.BlendShapes.Length),
                bones = new List<BoneSnapshotDto>(snapshot.Bones.Length),
                rendererPaths = new List<string>(snapshot.RendererPaths.Length),
            };

            var bsSpan = snapshot.BlendShapes.Span;
            for (int i = 0; i < bsSpan.Length; i++)
            {
                dto.blendShapes.Add(new BlendShapeSnapshotDto
                {
                    rendererPath = bsSpan[i].RendererPath ?? string.Empty,
                    name = bsSpan[i].Name ?? string.Empty,
                    value = bsSpan[i].Value,
                });
            }

            var boneSpan = snapshot.Bones.Span;
            for (int i = 0; i < boneSpan.Length; i++)
            {
                var b = boneSpan[i];
                dto.bones.Add(new BoneSnapshotDto
                {
                    bonePath = b.BonePath ?? string.Empty,
                    position = new Vector3(b.PositionX, b.PositionY, b.PositionZ),
                    rotationEuler = new Vector3(b.EulerX, b.EulerY, b.EulerZ),
                    scale = new Vector3(b.ScaleX, b.ScaleY, b.ScaleZ),
                });
            }

            var rpSpan = snapshot.RendererPaths.Span;
            for (int i = 0; i < rpSpan.Length; i++)
            {
                dto.rendererPaths.Add(rpSpan[i] ?? string.Empty);
            }

            return dto;
        }

        private static ExpressionSnapshotDto CreateDefaultSnapshotDto()
        {
            return new ExpressionSnapshotDto
            {
                transitionDuration = 0.25f,
                transitionCurvePreset = "Linear",
                blendShapes = new List<BlendShapeSnapshotDto>(),
                bones = new List<BoneSnapshotDto>(),
                rendererPaths = new List<string>(),
            };
        }

        private static List<InputSourceDto> BuildInputSourceDtoList(List<InputSourceDeclarationSerializable> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return new List<InputSourceDto>
                {
                    new InputSourceDto { id = "input", weight = 1.0f }
                };
            }
            var list = new List<InputSourceDto>(sources.Count);
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s == null) continue;
                list.Add(new InputSourceDto
                {
                    id = s.id ?? string.Empty,
                    weight = s.weight,
                    optionsJson = string.IsNullOrEmpty(s.optionsJson) ? null : s.optionsJson,
                });
            }
            return list;
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

        private static List<string> CopyStringList(List<string> src)
        {
            if (src == null) return new List<string>();
            var copy = new List<string>(src.Count);
            for (int i = 0; i < src.Count; i++) copy.Add(src[i] ?? string.Empty);
            return copy;
        }
    }
}
