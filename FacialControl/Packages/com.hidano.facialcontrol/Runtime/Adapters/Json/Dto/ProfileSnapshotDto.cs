using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// 中間 JSON Schema v2.0: トップレベルプロファイル DTO。
    /// <c>schemaVersion / layers[] / expressions[] / rendererPaths[]</c> の 4 フィールドを保持する。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// <para>
    /// 旧 v1.0 トップレベル DTO（<see cref="SystemTextJsonParser"/> 内の private <c>ProfileDto</c>）
    /// との衝突を避けるため <c>ProfileSnapshotDto</c> という名称で公開する。
    /// </para>
    /// <para>
    /// schemaVersion は <c>"2.0"</c> 以外を許容しない（Req 10.1）。
    /// パース時の strict チェックは <see cref="SystemTextJsonParser.ParseProfileSnapshotV2(string)"/> が担う。
    /// </para>
    /// </summary>
    [System.Serializable]
    public sealed class ProfileSnapshotDto
    {
        /// <summary>スキーマバージョン文字列（v2.0 では <c>"2.0"</c> 固定）。</summary>
        public string schemaVersion;

        /// <summary>レイヤー定義配列。</summary>
        public List<LayerDefinitionDto> layers;

        /// <summary>表情エントリ配列（<see cref="ExpressionSnapshotDto"/> を含む v2.0 形式）。</summary>
        public List<ExpressionDto> expressions;

        /// <summary>
        /// プロファイル全体で参照される SkinnedMeshRenderer Transform 階層パスの集合。
        /// 各 <see cref="ExpressionSnapshotDto.rendererPaths"/> はこの集合の subset である必要がある（Req 9.7）。
        /// </summary>
        public List<string> rendererPaths;
    }
}
