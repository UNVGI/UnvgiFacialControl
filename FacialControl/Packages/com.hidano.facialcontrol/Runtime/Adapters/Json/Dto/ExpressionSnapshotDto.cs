using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// 中間 JSON Schema v2.0: <c>expressions[].snapshot</c> オブジェクト。
    /// AnimationClip サンプリング結果（時刻 0 の BlendShape 値 + Bone 姿勢 + 遷移メタ）を
    /// JSON へ運搬する DTO。JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// <para>
    /// Domain 側の対応値型は <see cref="Hidano.FacialControl.Domain.Models.ExpressionSnapshot"/>。
    /// </para>
    /// </summary>
    [System.Serializable]
    public sealed class ExpressionSnapshotDto
    {
        /// <summary>表情遷移時間（秒）。0〜1 秒、デフォルト 0.25 秒（Req 2.5）。</summary>
        public float transitionDuration = 0.25f;

        /// <summary>
        /// 遷移カーブプリセット名（"Linear" / "EaseIn" / "EaseOut" / "EaseInOut"）。
        /// デフォルト "Linear"（Req 2.6）。
        /// 空文字 / null は Linear として解釈される。
        /// </summary>
        public string transitionCurvePreset = "Linear";

        /// <summary>BlendShape スナップショット配列（Req 9.2）。</summary>
        public List<BlendShapeSnapshotDto> blendShapes;

        /// <summary>Bone 姿勢スナップショット配列（Req 9.2）。</summary>
        public List<BoneSnapshotDto> bones;

        /// <summary>
        /// この snapshot 内に登場する SkinnedMeshRenderer Transform 階層パスのサマリ。
        /// トップレベル <see cref="ProfileSnapshotDto.rendererPaths"/> の subset である必要がある（Req 9.7）。
        /// </summary>
        public List<string> rendererPaths;
    }
}
