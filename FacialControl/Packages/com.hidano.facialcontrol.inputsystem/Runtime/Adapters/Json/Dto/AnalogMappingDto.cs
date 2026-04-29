using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Domain.Models.AnalogMappingFunction"/> の JSON 永続化 DTO（Req 6.3, 6.4）。
    /// dead-zone / scale / offset / curve / invert / clamp(min, max) を JsonUtility 互換フィールドで保持する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="curveType"/> は文字列で永続化し（"linear" / "easeIn" / "easeOut" / "easeInOut" / "custom"）、
    /// 既存 <c>SystemTextJsonParser.SerializeTransitionCurveType</c> と整合させる。
    /// 不正値はローダ側で <c>Debug.LogWarning</c> + skip 扱い（Req 6.5）。
    /// </para>
    /// </remarks>
    [System.Serializable]
    public sealed class AnalogMappingDto
    {
        /// <summary>デッドゾーン閾値（絶対値比較）。</summary>
        public float deadZone;

        /// <summary>スケール係数（gain）。</summary>
        public float scale;

        /// <summary>オフセット（bias）。</summary>
        public float offset;

        /// <summary>カーブ種別（"linear" / "easeIn" / "easeOut" / "easeInOut" / "custom"、大小無視）。</summary>
        public string curveType;

        /// <summary>Custom カーブ時のキーフレーム配列。プリセット時は null / 空。</summary>
        public List<CurveKeyFrameDto> curveKeyFrames;

        /// <summary>反転フラグ。</summary>
        public bool invert;

        /// <summary>出力下限（clamp 用、min &lt;= max を要求）。</summary>
        public float min;

        /// <summary>出力上限（clamp 用、min &lt;= max を要求）。</summary>
        public float max;
    }
}
