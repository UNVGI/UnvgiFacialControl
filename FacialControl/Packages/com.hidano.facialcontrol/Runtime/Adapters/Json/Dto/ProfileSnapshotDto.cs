using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// プロファイル JSON: トップレベル DTO。
    /// <c>schemaVersion / layers[] / expressions[] / rendererPaths[] / gazeConfigs[]</c> の 5 フィールドを保持する。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// <para>
    /// preview.1 リリース前段階のため schemaVersion は <c>"1.0"</c> 以外を許容しない（Req 10.1）。
    /// パース時の strict チェックは <see cref="SystemTextJsonParser.ParseProfileSnapshotV2(string)"/> が担う。
    /// </para>
    /// </summary>
    [System.Serializable]
    public sealed class ProfileSnapshotDto
    {
        /// <summary>スキーマバージョン文字列（preview.1 段階では <c>"1.0"</c> 固定）。</summary>
        public string schemaVersion;

        /// <summary>レイヤー定義配列。</summary>
        public List<LayerDefinitionDto> layers;

        /// <summary>表情エントリ配列（<see cref="ExpressionSnapshotDto"/> を含む snapshot 形式）。</summary>
        public List<ExpressionDto> expressions;

        /// <summary>
        /// プロファイル全体で参照される SkinnedMeshRenderer Transform 階層パスの集合。
        /// 各 <see cref="ExpressionSnapshotDto.rendererPaths"/> はこの集合の subset である必要がある（Req 9.7）。
        /// </summary>
        public List<string> rendererPaths;

        public List<GazeBindingConfigDto> gazeConfigs;
    }
}
