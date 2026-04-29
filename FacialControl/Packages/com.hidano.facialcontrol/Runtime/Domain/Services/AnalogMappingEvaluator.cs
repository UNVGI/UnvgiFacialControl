using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Domain.Services
{
    /// <summary>
    /// <see cref="AnalogMappingFunction"/> を評価する静的サービス。
    /// 適用順は厳密に <c>dead-zone(re-center) → scale → offset → curve → invert → clamp(min, max)</c>（Req 2.3）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// hot path で managed heap 確保ゼロを保証する（Req 2.6 / 8.1）。
    /// </para>
    /// <para>
    /// カーブ評価は既存 <see cref="TransitionCalculator.Evaluate(in TransitionCurve, float)"/> を委譲利用し、
    /// Hermite 補間ロジックを二重実装しない（design R-1 / R-4）。
    /// </para>
    /// </remarks>
    public static class AnalogMappingEvaluator
    {
        /// <summary>
        /// マッピング関数を評価する。
        /// </summary>
        /// <param name="mapping">マッピング関数</param>
        /// <param name="input">入力値（典型的に [-1, 1] または [0, 1]）</param>
        /// <returns>マッピング適用後の出力値（<c>[mapping.Min, mapping.Max]</c> にクランプ済み）</returns>
        public static float Evaluate(in AnalogMappingFunction mapping, float input)
        {
            // Stage 1: dead-zone with re-centering（Req 2.4）
            float v = ApplyDeadZone(input, mapping.DeadZone);

            // Stage 2: scale
            v *= mapping.Scale;

            // Stage 3: offset
            v += mapping.Offset;

            // Stage 4: curve（既存 TransitionCalculator に委譲、design R-4）
            v = TransitionCalculator.Evaluate(mapping.Curve, v);

            // Stage 5: invert
            if (mapping.Invert)
            {
                v = -v;
            }

            // Stage 6: clamp(min, max)
            float min = mapping.Min;
            float max = mapping.Max;
            if (v < min)
            {
                v = min;
            }
            else if (v > max)
            {
                v = max;
            }

            return v;
        }

        /// <summary>
        /// デッドゾーンを適用し、残った範囲を [-1, 1] / [0, 1] スケールへ再センタする。
        /// </summary>
        /// <remarks>
        /// <c>|input| &lt;= deadZone</c> のとき厳密にゼロ（Req 2.4）。
        /// それ以外は <c>sign(input) * (|input| - deadZone) / (1 - deadZone)</c> で再マップ。
        /// <c>deadZone &gt;= 1</c> のような病的設定では常に 0 を返す（divide-by-zero 回避）。
        /// </remarks>
        private static float ApplyDeadZone(float input, float deadZone)
        {
            if (deadZone <= 0f)
            {
                return input;
            }

            float abs = input < 0f ? -input : input;
            if (abs <= deadZone)
            {
                return 0f;
            }

            float denom = 1f - deadZone;
            if (denom <= 0f)
            {
                return 0f;
            }

            float sign = input < 0f ? -1f : 1f;
            return sign * (abs - deadZone) / denom;
        }
    }
}
