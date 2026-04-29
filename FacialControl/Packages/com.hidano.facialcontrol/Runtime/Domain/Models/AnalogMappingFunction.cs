using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// アナログ入力の宣言的マッピング関数（Domain 値型）。
    /// dead-zone / scale / offset / curve / inversion / clamp(min, max) を不変パラメータとして保持する（Req 2.1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 実評価は <see cref="Hidano.FacialControl.Domain.Services.AnalogMappingEvaluator.Evaluate"/> 経由で行う。
    /// 適用順は厳密に <c>dead-zone(re-center) → scale → offset → curve → invert → clamp(min, max)</c>（Req 2.3）。
    /// </para>
    /// <para>
    /// <see cref="Curve"/> は既存 <see cref="TransitionCurve"/> をそのまま再利用する（design R-1 / R-4）。
    /// プリセット (Linear/EaseIn/EaseOut/EaseInOut) と Custom (Hermite 補間) はカーブ側で表現する（Req 2.2）。
    /// </para>
    /// </remarks>
    public readonly struct AnalogMappingFunction
    {
        /// <summary>デッドゾーン閾値（絶対値比較）。<c>|input| &lt;= DeadZone</c> のとき再センタ後 0 を返す（Req 2.4）。</summary>
        public float DeadZone { get; }

        /// <summary>スケール係数（gain）。dead-zone 後の値に乗算される。</summary>
        public float Scale { get; }

        /// <summary>オフセット（bias）。スケール後の値に加算される。</summary>
        public float Offset { get; }

        /// <summary>カーブ設定。プリセット / Custom (Hermite) 双方を表現する（Req 2.2）。</summary>
        public TransitionCurve Curve { get; }

        /// <summary>反転フラグ。true のときカーブ評価後の値の符号を反転する（<c>v = -v</c>）。</summary>
        public bool Invert { get; }

        /// <summary>出力下限。最終段で <c>clamp(min, max)</c> される。</summary>
        public float Min { get; }

        /// <summary>出力上限。最終段で <c>clamp(min, max)</c> される。</summary>
        public float Max { get; }

        /// <summary>
        /// マッピング関数を構築する。
        /// </summary>
        /// <param name="deadZone">デッドゾーン閾値（&gt;= 0 推奨、絶対値で比較）</param>
        /// <param name="scale">スケール係数</param>
        /// <param name="offset">オフセット</param>
        /// <param name="curve">カーブ設定（既存 <see cref="TransitionCurve"/> を再利用）</param>
        /// <param name="invert">反転フラグ</param>
        /// <param name="min">出力下限</param>
        /// <param name="max">出力上限</param>
        /// <exception cref="ArgumentException"><paramref name="min"/> &gt; <paramref name="max"/> のとき（Req 2.5）。</exception>
        public AnalogMappingFunction(
            float deadZone,
            float scale,
            float offset,
            in TransitionCurve curve,
            bool invert,
            float min,
            float max)
        {
            if (min > max)
            {
                throw new ArgumentException(
                    $"AnalogMappingFunction: min ({min}) must be <= max ({max}) (Req 2.5).",
                    nameof(min));
            }

            DeadZone = deadZone;
            Scale = scale;
            Offset = offset;
            Curve = curve;
            Invert = invert;
            Min = min;
            Max = max;
        }

        /// <summary>
        /// 恒等マッピング（dead-zone=0, scale=1, offset=0, curve=Linear, invert=false, min=0, max=1）を返す（Req 2.7）。
        /// </summary>
        public static AnalogMappingFunction Identity => new AnalogMappingFunction(
            deadZone: 0f,
            scale: 1f,
            offset: 0f,
            curve: TransitionCurve.Linear,
            invert: false,
            min: 0f,
            max: 1f);
    }
}
