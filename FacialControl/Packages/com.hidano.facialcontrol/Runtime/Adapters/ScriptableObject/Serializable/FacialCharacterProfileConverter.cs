using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// Inspector でシリアライズされた SO データ (Serializable 群) と Domain モデル
    /// (<see cref="FacialProfile"/> / <see cref="AnalogInputBindingProfile"/>) の相互変換ユーティリティ。
    /// JSON 経路を通さず直接 Domain を構築できるため、JSON 不在時のフォールバック・テスト・
    /// JSON エクスポート前段として用いられる。
    /// </summary>
    public static class FacialCharacterProfileConverter
    {
        /// <summary>
        /// Serializable 群から <see cref="FacialProfile"/> を構築する。
        /// 配列・リストの null は空配列扱い。schemaVersion が空なら "1.0" を使う。
        /// </summary>
        public static FacialProfile ToFacialProfile(
            string schemaVersion,
            IReadOnlyList<LayerDefinitionSerializable> layers,
            IReadOnlyList<ExpressionSerializable> expressions,
            IReadOnlyList<string> rendererPaths,
            IReadOnlyList<BonePoseSerializable> bonePoses)
        {
            string version = string.IsNullOrWhiteSpace(schemaVersion) ? "1.0" : schemaVersion;

            var layerArr = ConvertLayers(layers);
            var inputSourceArr = ConvertLayerInputSources(layers);
            var expressionArr = ConvertExpressions(expressions);
            var rendererArr = ConvertStrings(rendererPaths);
            var bonePoseArr = ConvertBonePoses(bonePoses);

            return new FacialProfile(
                version,
                layerArr,
                expressionArr,
                rendererArr,
                inputSourceArr,
                bonePoseArr);
        }

        /// <summary>
        /// Serializable 群から <see cref="AnalogInputBindingProfile"/> を構築する。
        /// バージョン文字列はビルド成果物の互換性管理用 (空文字許容)。
        /// </summary>
        public static AnalogInputBindingProfile ToAnalogProfile(
            string version,
            IReadOnlyList<AnalogBindingEntrySerializable> bindings)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return new AnalogInputBindingProfile(version ?? string.Empty, Array.Empty<AnalogBindingEntry>());
            }

            var entries = new List<AnalogBindingEntry>(bindings.Count);
            for (int i = 0; i < bindings.Count; i++)
            {
                var src = bindings[i];
                if (src == null || string.IsNullOrWhiteSpace(src.targetIdentifier))
                {
                    continue;
                }

                AnalogBindingEntry entry;
                try
                {
                    // Phase 4.7: AnalogBindingEntrySerializable は 3 フィールドに簡素化済（Req 6.2）。
                    // sourceId は inputActionRef を継承し、sourceAxis は scalar=0 既定、
                    // targetKind は BlendShape 既定とする（BonePose は今後 InputProcessor 経由で扱う）。
                    entry = new AnalogBindingEntry(
                        src.inputActionRef ?? string.Empty,
                        0,
                        AnalogBindingTargetKind.BlendShape,
                        src.targetIdentifier,
                        src.targetAxis);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                entries.Add(entry);
            }

            return new AnalogInputBindingProfile(version ?? string.Empty, entries.ToArray());
        }

        private static LayerDefinition[] ConvertLayers(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<LayerDefinition>();
            }

            var result = new List<LayerDefinition>(layers.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                var src = layers[i];
                if (src == null || string.IsNullOrWhiteSpace(src.name))
                {
                    continue;
                }

                int priority = src.priority < 0 ? 0 : src.priority;
                result.Add(new LayerDefinition(src.name, priority, src.exclusionMode));
            }
            return result.ToArray();
        }

        private static InputSourceDeclaration[][] ConvertLayerInputSources(IReadOnlyList<LayerDefinitionSerializable> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<InputSourceDeclaration[]>();
            }

            // ConvertLayers と同じ「有効レイヤーだけ」順に並べる必要があるため、
            // 名前未設定レイヤーは同じくスキップする。
            var result = new List<InputSourceDeclaration[]>(layers.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                var src = layers[i];
                if (src == null || string.IsNullOrWhiteSpace(src.name))
                {
                    continue;
                }

                if (src.inputSources == null || src.inputSources.Count == 0)
                {
                    result.Add(Array.Empty<InputSourceDeclaration>());
                    continue;
                }

                var arr = new List<InputSourceDeclaration>(src.inputSources.Count);
                for (int j = 0; j < src.inputSources.Count; j++)
                {
                    var s = src.inputSources[j];
                    if (s == null || string.IsNullOrWhiteSpace(s.id))
                    {
                        continue;
                    }
                    string options = string.IsNullOrEmpty(s.optionsJson) ? null : s.optionsJson;
                    arr.Add(new InputSourceDeclaration(s.id, s.weight, options));
                }
                result.Add(arr.ToArray());
            }
            return result.ToArray();
        }

        private static Expression[] ConvertExpressions(IReadOnlyList<ExpressionSerializable> expressions)
        {
            if (expressions == null || expressions.Count == 0)
            {
                return Array.Empty<Expression>();
            }

            var result = new List<Expression>(expressions.Count);
            for (int i = 0; i < expressions.Count; i++)
            {
                var src = expressions[i];
                if (src == null
                    || string.IsNullOrWhiteSpace(src.id)
                    || string.IsNullOrWhiteSpace(src.name)
                    || string.IsNullOrWhiteSpace(src.layer))
                {
                    continue;
                }

                var blendShapes = ConvertBlendShapes(src.blendShapeValues);
                var curve = ConvertTransitionCurve(src.transitionCurve);

                result.Add(new Expression(
                    src.id,
                    src.name,
                    src.layer,
                    src.transitionDuration,
                    curve,
                    blendShapes));
            }
            return result.ToArray();
        }

        private static BlendShapeMapping[] ConvertBlendShapes(IReadOnlyList<BlendShapeMappingSerializable> mappings)
        {
            if (mappings == null || mappings.Count == 0)
            {
                return Array.Empty<BlendShapeMapping>();
            }

            var result = new List<BlendShapeMapping>(mappings.Count);
            for (int i = 0; i < mappings.Count; i++)
            {
                var src = mappings[i];
                if (src == null || string.IsNullOrEmpty(src.name))
                {
                    continue;
                }
                string renderer = string.IsNullOrEmpty(src.renderer) ? null : src.renderer;
                result.Add(new BlendShapeMapping(src.name, src.value, renderer));
            }
            return result.ToArray();
        }

        private static TransitionCurve ConvertTransitionCurve(TransitionCurveSerializable curve)
        {
            if (curve == null)
            {
                return TransitionCurve.Linear;
            }

            CurveKeyFrame[] keys;
            if (curve.keys == null || curve.keys.Count == 0)
            {
                keys = Array.Empty<CurveKeyFrame>();
            }
            else
            {
                keys = new CurveKeyFrame[curve.keys.Count];
                for (int i = 0; i < curve.keys.Count; i++)
                {
                    var k = curve.keys[i] ?? new CurveKeyFrameSerializable();
                    keys[i] = new CurveKeyFrame(
                        k.time,
                        k.value,
                        k.inTangent,
                        k.outTangent,
                        k.inWeight,
                        k.outWeight,
                        k.weightedMode);
                }
            }
            return new TransitionCurve(curve.type, keys);
        }

        private static string[] ConvertStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }
            var result = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                result[i] = values[i] ?? string.Empty;
            }
            return result;
        }

        private static BonePose[] ConvertBonePoses(IReadOnlyList<BonePoseSerializable> bonePoses)
        {
            if (bonePoses == null || bonePoses.Count == 0)
            {
                return Array.Empty<BonePose>();
            }

            var result = new List<BonePose>(bonePoses.Count);
            for (int i = 0; i < bonePoses.Count; i++)
            {
                var src = bonePoses[i];
                if (src == null)
                {
                    continue;
                }

                BonePoseEntry[] entries;
                if (src.entries == null || src.entries.Length == 0)
                {
                    entries = Array.Empty<BonePoseEntry>();
                }
                else
                {
                    var validEntries = new List<BonePoseEntry>(src.entries.Length);
                    for (int j = 0; j < src.entries.Length; j++)
                    {
                        var e = src.entries[j];
                        if (e == null || string.IsNullOrWhiteSpace(e.boneName))
                        {
                            continue;
                        }
                        validEntries.Add(new BonePoseEntry(e.boneName, e.eulerXYZ.x, e.eulerXYZ.y, e.eulerXYZ.z));
                    }
                    entries = validEntries.ToArray();
                }

                BonePose pose;
                try
                {
                    pose = new BonePose(src.id ?? string.Empty, entries);
                }
                catch (ArgumentException)
                {
                    // boneName 重複等のバリデーション失敗 pose はスキップして続行。
                    continue;
                }
                result.Add(pose);
            }
            return result.ToArray();
        }

    }
}
