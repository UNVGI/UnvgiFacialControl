using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

// schema v1.0 専用の Obsolete DTO（BonePoseDto / BonePoseEntryDto）を bridge 期間として
// 引き続き使用するため、CS0618 警告を抑制する。Phase 3.6（タスク 3.6）で v1.0 経路ごと物理削除予定。
#pragma warning disable 618
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

        /// <summary>
        /// 中間 JSON Schema v2.0（タスク 2.3）の strict version 文字列。
        /// <see cref="ParseProfileSnapshotV2(string)"/> はこの値以外を <see cref="InvalidOperationException"/> で拒否する（Req 10.1）。
        /// </summary>
        public const string SchemaVersionV2 = "2.0";

        /// <inheritdoc/>
        public FacialProfile ParseProfile(string json)
        {
            var dto = ParseProfileDto(json);
            var inputSources = ExtractInputSources(dto);
            var bonePoses = ExtractBonePoses(dto);
            return ConvertToProfile(dto, inputSources, bonePoses);
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

        /// <summary>
        /// 中間 JSON Schema v2.0（タスク 2.3）の専用パース経路。
        /// <c>schemaVersion</c> が <see cref="SchemaVersionV2"/>（<c>"2.0"</c>）以外の場合は
        /// <see cref="Debug.LogError(object)"/> で報告した上で <see cref="InvalidOperationException"/> を投げる（Req 10.1）。
        /// <para>
        /// 欠落 / null の <see cref="ProfileSnapshotDto.expressions"/> エントリ内 snapshot は
        /// 既定値（<see cref="ExpressionSnapshotDto.transitionDuration"/> = 0.25,
        /// <see cref="ExpressionSnapshotDto.transitionCurvePreset"/> = "Linear", 各配列空）に正規化する。
        /// </para>
        /// <para>
        /// 本メソッドは v2.0 schema を Domain <see cref="FacialProfile"/> へ変換する責務までは持たない。
        /// snapshot DTO 形式での round-trip 確立のみを行う（Domain 変換は Phase 3.6 / タスク 3.6 で実装される）。
        /// </para>
        /// </summary>
        /// <param name="json">v2.0 schema 準拠のプロファイル JSON 文字列</param>
        /// <returns>正規化済み <see cref="ProfileSnapshotDto"/></returns>
        /// <exception cref="ArgumentNullException">json が null</exception>
        /// <exception cref="ArgumentException">json が空 / 全空白</exception>
        /// <exception cref="InvalidOperationException">
        /// schemaVersion が欠落、またはサポート対象 v2.0 と一致しない場合（Req 10.1）。
        /// </exception>
        /// <exception cref="FormatException">JSON 自体のパースに失敗した場合</exception>
        public ProfileSnapshotDto ParseProfileSnapshotV2(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 文字列を空にすることはできません。", nameof(json));

            ProfileSnapshotDto dto;
            try
            {
                dto = JsonUtility.FromJson<ProfileSnapshotDto>(json);
            }
            catch (Exception ex)
            {
                throw new FormatException("プロファイル JSON (v2.0) のパースに失敗しました。", ex);
            }

            if (dto == null)
                throw new FormatException("プロファイル JSON (v2.0) のパースに失敗しました。結果が null です。");

            if (string.IsNullOrEmpty(dto.schemaVersion) || dto.schemaVersion != SchemaVersionV2)
            {
                var actual = string.IsNullOrEmpty(dto.schemaVersion) ? "<missing>" : dto.schemaVersion;
                Debug.LogError(
                    $"SystemTextJsonParser: 中間 JSON schema v2.0 の strict チェックに失敗しました。" +
                    $"期待値 '{SchemaVersionV2}'、実際 '{actual}'。Req 10.1 により旧 schema は拒否されます。");
                throw new InvalidOperationException(
                    $"サポートされていないスキーマバージョンです: '{actual}' (期待値 '{SchemaVersionV2}')。");
            }

            NormalizeProfileSnapshotDto(dto);
            return dto;
        }

        /// <summary>
        /// <see cref="ParseProfileSnapshotV2(string)"/> の後処理: null collection を空 collection に、
        /// 欠落 snapshot を既定値の <see cref="ExpressionSnapshotDto"/> に正規化する。
        /// </summary>
        private static void NormalizeProfileSnapshotDto(ProfileSnapshotDto dto)
        {
            if (dto.layers == null)
                dto.layers = new List<LayerDefinitionDto>();
            if (dto.expressions == null)
            {
                // private inner ExpressionDto と Dto.ExpressionDto の名前衝突を避けるため fully qualified を使う。
                dto.expressions = new List<Hidano.FacialControl.Adapters.Json.Dto.ExpressionDto>();
            }
            if (dto.rendererPaths == null)
                dto.rendererPaths = new List<string>();

            for (int i = 0; i < dto.expressions.Count; i++)
            {
                var expr = dto.expressions[i];
                if (expr == null)
                    continue;

                if (expr.layerOverrideMask == null)
                    expr.layerOverrideMask = new List<string>();

                if (expr.snapshot == null)
                {
                    expr.snapshot = new ExpressionSnapshotDto
                    {
                        transitionDuration = 0.25f,
                        transitionCurvePreset = "Linear",
                        blendShapes = new List<BlendShapeSnapshotDto>(),
                        bones = new List<BoneSnapshotDto>(),
                        rendererPaths = new List<string>(),
                    };
                    continue;
                }

                if (string.IsNullOrEmpty(expr.snapshot.transitionCurvePreset))
                    expr.snapshot.transitionCurvePreset = "Linear";
                if (expr.snapshot.blendShapes == null)
                    expr.snapshot.blendShapes = new List<BlendShapeSnapshotDto>();
                if (expr.snapshot.bones == null)
                    expr.snapshot.bones = new List<BoneSnapshotDto>();
                if (expr.snapshot.rendererPaths == null)
                    expr.snapshot.rendererPaths = new List<string>();
            }
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

        // Req 1.7, 3.3, 3.4 / D-5, D-6: inputSources エントリの識別子検証と重複解決。
        // - regex 違反 (InputSourceId.TryParse が false) → 警告 + skip
        // - 予約 ID でも x- プレフィックスでもない id (= 仕様上未登録) → 警告 + skip
        // - 同レイヤー内の重複 id → 警告 + last-wins (最後の出現を採用し、最後の位置で保持)
        // いずれの場合も例外は投げず、他レイヤー・他エントリの parse は継続する。
        private static InputSourceDto[][] ExtractInputSources(ProfileDto dto)
        {
            if (dto.layers == null || dto.layers.Count == 0)
                return Array.Empty<InputSourceDto[]>();

            var result = new InputSourceDto[dto.layers.Count][];
            var lastValidIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicateWarned = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < dto.layers.Count; i++)
            {
                var layer = dto.layers[i];
                var entries = layer.inputSources;
                lastValidIndexById.Clear();
                duplicateWarned.Clear();

                // 1 pass 目: 各エントリを検証し、有効なもののみ「id → 最終インデックス」を記録する。
                var validSchema = new bool[entries.Count];
                for (int j = 0; j < entries.Count; j++)
                {
                    var src = entries[j];
                    var rawId = src.id;

                    if (!InputSourceId.TryParse(rawId, out var parsedId))
                    {
                        Debug.LogWarning(
                            $"SystemTextJsonParser: レイヤー '{layer.name}' の inputSources[{j}] に不正な識別子 '{rawId ?? "<null>"}' が指定されました。" +
                            "識別子は [a-zA-Z0-9_.-]{1,64} を満たす必要があります (D-5 により 'legacy' は受理されません)。スキップします。");
                        continue;
                    }

                    if (!parsedId.IsReserved && !parsedId.IsThirdPartyExtension)
                    {
                        Debug.LogWarning(
                            $"SystemTextJsonParser: レイヤー '{layer.name}' の inputSources[{j}] に未登録の識別子 '{rawId}' が指定されました。" +
                            "予約 ID (osc / lipsync / controller-expr / keyboard-expr / input) または 'x-' プレフィックス拡張のみ使用できます。スキップします。");
                        continue;
                    }

                    validSchema[j] = true;
                    lastValidIndexById[rawId] = j;
                }

                // 2 pass 目: 最後の出現位置のみを採用し、宣言順を保った結果リストを構築する。
                var accepted = new List<InputSourceDto>(entries.Count);
                for (int j = 0; j < entries.Count; j++)
                {
                    if (!validSchema[j])
                        continue;

                    var src = entries[j];
                    if (lastValidIndexById[src.id] != j)
                    {
                        if (duplicateWarned.Add(src.id))
                        {
                            Debug.LogWarning(
                                $"SystemTextJsonParser: レイヤー '{layer.name}' に同一識別子 '{src.id}' の inputSources エントリが重複しています。最後の出現を採用します (last-wins)。");
                        }
                        continue;
                    }

                    // JsonUtility はフィールドの初期化子を尊重しないケースがあるため、
                    // optionsJson が未設定のときは空文字列として扱わない（null のまま返す）。
                    accepted.Add(new InputSourceDto
                    {
                        id = src.id,
                        weight = src.weight,
                        optionsJson = string.IsNullOrEmpty(src.optionsJson) ? null : src.optionsJson
                    });
                }

                result[i] = accepted.ToArray();
            }
            return result;
        }

        /// <inheritdoc/>
        public string SerializeProfile(FacialProfile profile)
        {
            var dto = ConvertToProfileDto(profile);
            var raw = JsonUtility.ToJson(dto, true);
            // JsonUtility は optionsJson を文字列フィールドとして常時出力する。
            // スキーマ上は "options": {...} 形式であり、round-trip 安定性 (Req 3.5, 8.4) のため
            // 出力側で optionsJson を options に戻す（空値フィールドは削除、非空は JSON オブジェクトに展開）。
            return PostprocessInputSourceOptions(raw);
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

        // InputSourceDeclaration[] → List<InputSourceDto>。宣言が空の場合は placeholder を返す。
        private static List<InputSourceDto> BuildInputSourceDtoList(InputSourceDeclaration[] declarations)
        {
            if (declarations != null && declarations.Length > 0)
            {
                var list = new List<InputSourceDto>(declarations.Length);
                for (int j = 0; j < declarations.Length; j++)
                {
                    var d = declarations[j];
                    list.Add(new InputSourceDto
                    {
                        id = d.Id,
                        weight = d.Weight,
                        optionsJson = string.IsNullOrEmpty(d.OptionsJson) ? null : d.OptionsJson
                    });
                }
                return list;
            }

            return new List<InputSourceDto>
            {
                new InputSourceDto { id = "controller-expr", weight = 1.0f }
            };
        }

        // "optionsJson" フィールドを "options" に戻す後処理 (PreprocessInputSourceOptions の逆変換)。
        // - "optionsJson": "" → フィールドごと削除（直前のカンマも含めて）
        // - "optionsJson": "<escaped JSON>" → "options": <unescaped JSON>
        // InputSourceDto では optionsJson が最後のフィールドなので、基本形は「カンマ + 空白 + key」。
        private static string PostprocessInputSourceOptions(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            const string KeyQuoted = "\"optionsJson\"";
            var sb = new StringBuilder(json.Length);
            int cursor = 0;

            while (cursor < json.Length)
            {
                int keyStart = json.IndexOf(KeyQuoted, cursor, StringComparison.Ordinal);
                if (keyStart < 0)
                {
                    sb.Append(json, cursor, json.Length - cursor);
                    break;
                }

                int afterKey = keyStart + KeyQuoted.Length;
                int colonIdx = afterKey;
                while (colonIdx < json.Length && char.IsWhiteSpace(json[colonIdx]))
                    colonIdx++;

                if (colonIdx >= json.Length || json[colonIdx] != ':')
                {
                    sb.Append(json, cursor, afterKey - cursor);
                    cursor = afterKey;
                    continue;
                }

                int valueStart = colonIdx + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    valueStart++;

                if (valueStart >= json.Length || json[valueStart] != '"')
                {
                    sb.Append(json, cursor, valueStart - cursor);
                    cursor = valueStart;
                    continue;
                }

                int valueEnd = FindMatchingQuote(json, valueStart);
                if (valueEnd < 0)
                {
                    sb.Append(json, cursor, json.Length - cursor);
                    cursor = json.Length;
                    break;
                }

                string escaped = json.Substring(valueStart + 1, valueEnd - valueStart - 1);

                if (escaped.Length == 0)
                {
                    // フィールド削除: 直前のカンマから後ろを捨てる。先頭フィールドの場合は後続カンマを飛ばす。
                    int removeFromBack = keyStart;
                    int p = keyStart - 1;
                    while (p >= 0 && char.IsWhiteSpace(json[p]))
                        p--;

                    int afterValue = valueEnd + 1;
                    if (p >= 0 && json[p] == ',')
                    {
                        removeFromBack = p;
                    }
                    else
                    {
                        // 先頭フィールドなので後続カンマをスキップする。
                        int q = afterValue;
                        while (q < json.Length && char.IsWhiteSpace(json[q]))
                            q++;
                        if (q < json.Length && json[q] == ',')
                            afterValue = q + 1;
                    }

                    sb.Append(json, cursor, removeFromBack - cursor);
                    cursor = afterValue;
                }
                else
                {
                    // 置換: "optionsJson":"..." → "options":<unescaped JSON>
                    sb.Append(json, cursor, keyStart - cursor);
                    sb.Append("\"options\":");
                    sb.Append(UnescapeJsonString(escaped));
                    cursor = valueEnd + 1;
                }
            }

            return sb.ToString();
        }

        private static int FindMatchingQuote(string json, int openIndex)
        {
            int p = openIndex + 1;
            bool escaped = false;
            while (p < json.Length)
            {
                char c = json[p];
                if (escaped) { escaped = false; }
                else if (c == '\\') { escaped = true; }
                else if (c == '"') { return p; }
                p++;
            }
            return -1;
        }

        private static string UnescapeJsonString(string escaped)
        {
            var sb = new StringBuilder(escaped.Length);
            int i = 0;
            while (i < escaped.Length)
            {
                char c = escaped[i];
                if (c == '\\' && i + 1 < escaped.Length)
                {
                    char next = escaped[i + 1];
                    switch (next)
                    {
                        case '\\': sb.Append('\\'); i += 2; break;
                        case '"': sb.Append('"'); i += 2; break;
                        case 'b': sb.Append('\b'); i += 2; break;
                        case 'f': sb.Append('\f'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        case 'u':
                            if (i + 5 < escaped.Length)
                            {
                                string hex = escaped.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 6;
                                    break;
                                }
                            }
                            sb.Append(c);
                            i++;
                            break;
                        default:
                            sb.Append(c);
                            i++;
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        // --- Profile 変換 ---

        private static FacialProfile ConvertToProfile(ProfileDto dto, InputSourceDto[][] inputSourceDtos, BonePose[] bonePoses)
        {
            var layers = ConvertLayers(dto.layers);
            var expressions = ConvertExpressions(dto.expressions);
            var rendererPaths = ConvertRendererPaths(dto.rendererPaths);
            var layerInputSources = ConvertLayerInputSources(inputSourceDtos);
            return new FacialProfile(dto.schemaVersion, layers, expressions, rendererPaths, layerInputSources, bonePoses);
        }

        // Req 7.1, 7.2, 7.4, 7.5: bonePoses ブロックを Domain BonePose[] に変換する。
        // - boneName が null / 空 / 全空白のエントリは Warning + skip + 続行 (Req 7.4)
        // - Domain ctor が ArgumentException を投げる pose（同名 boneName 重複等）は
        //   その BonePose 全体を Warning + skip + 続行 (Req 7.4 / Req 1.7)
        // - bonePoses 自体の欠落 / null / 空配列は空 BonePose[] を返す (Req 7.3 / 10.2)
        private static BonePose[] ExtractBonePoses(ProfileDto dto)
        {
            if (dto.bonePoses == null || dto.bonePoses.Count == 0)
                return Array.Empty<BonePose>();

            var result = new List<BonePose>(dto.bonePoses.Count);
            for (int i = 0; i < dto.bonePoses.Count; i++)
            {
                var poseDto = dto.bonePoses[i];
                if (poseDto == null)
                    continue;

                var validEntries = new List<BonePoseEntry>(poseDto.entries != null ? poseDto.entries.Count : 0);
                if (poseDto.entries != null)
                {
                    for (int j = 0; j < poseDto.entries.Count; j++)
                    {
                        var entryDto = poseDto.entries[j];
                        if (entryDto == null || string.IsNullOrWhiteSpace(entryDto.boneName))
                        {
                            Debug.LogWarning(
                                $"SystemTextJsonParser: bonePoses[{i}].entries[{j}] に boneName が指定されていません。スキップします。");
                            continue;
                        }

                        validEntries.Add(new BonePoseEntry(
                            entryDto.boneName,
                            entryDto.eulerXYZ.x,
                            entryDto.eulerXYZ.y,
                            entryDto.eulerXYZ.z));
                    }
                }

                BonePose pose;
                try
                {
                    pose = new BonePose(poseDto.id, validEntries.ToArray());
                }
                catch (ArgumentException ex)
                {
                    Debug.LogWarning(
                        $"SystemTextJsonParser: bonePoses[{i}] (id='{poseDto.id}') の構築に失敗したためスキップします: {ex.Message}");
                    continue;
                }

                result.Add(pose);
            }

            return result.ToArray();
        }

        // InputSourceDto[][] → InputSourceDeclaration[][] 変換。round-trip 担体用。
        private static InputSourceDeclaration[][] ConvertLayerInputSources(InputSourceDto[][] dtos)
        {
            if (dtos == null || dtos.Length == 0)
                return null;

            var result = new InputSourceDeclaration[dtos.Length][];
            for (int i = 0; i < dtos.Length; i++)
            {
                var inner = dtos[i];
                if (inner == null || inner.Length == 0)
                {
                    result[i] = Array.Empty<InputSourceDeclaration>();
                    continue;
                }
                var arr = new InputSourceDeclaration[inner.Length];
                for (int j = 0; j < inner.Length; j++)
                {
                    arr[j] = new InputSourceDeclaration(inner[j].id, inner[j].weight, inner[j].optionsJson);
                }
                result[i] = arr;
            }
            return result;
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
            var lisSpan = profile.LayerInputSources.Span;
            for (int i = 0; i < layerSpan.Length; i++)
            {
                // preview 破壊的変更 (D-5, Req 3.2): inputSources は必須フィールドのため、
                // FacialProfile に保持された inputSources 宣言 (round-trip 担体) を使って復元する。
                // 宣言が無い / 空のレイヤーは最小の placeholder (controller-expr, weight=1.0) を出力する
                // (Req 3.5, 8.4: SerializeProfile 出力はスキーマ上 inputSources 非空を保証する)。
                dto.layers.Add(new LayerDto
                {
                    name = layerSpan[i].Name,
                    priority = layerSpan[i].Priority,
                    exclusionMode = SerializeExclusionMode(layerSpan[i].ExclusionMode),
                    inputSources = BuildInputSourceDtoList(i < lisSpan.Length ? lisSpan[i] : null)
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
            public List<BonePoseDto> bonePoses;
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
