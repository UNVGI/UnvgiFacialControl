using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// slot 名と overlay 対象 Expression ID の対応を表す値型。
    /// <para>
    /// <see cref="ExpressionId"/> が null / 空文字 / 全空白の場合は「明示的な suppress」を意味し、
    /// その slot に対する overlay 発火を完全に抑制する。<see cref="IsSuppress"/> で判定できる。
    /// </para>
    /// <para>
    /// <see cref="Expression.Overlays"/> および <see cref="FacialProfile.DefaultOverlays"/> の要素として用いられる。
    /// 解決ロジックは表情ごとの宣言を優先し、未宣言 slot は profile の DefaultOverlays に fallback する。
    /// </para>
    /// </summary>
    public readonly struct OverlaySlotBinding : IEquatable<OverlaySlotBinding>
    {
        /// <summary>slot 識別子。空文字不可。</summary>
        public string Slot { get; }

        /// <summary>
        /// 発火させる overlay Expression の ID。null / 空文字 / 全空白なら明示 suppress。
        /// </summary>
        public string ExpressionId { get; }

        /// <summary>
        /// この binding が「明示的な suppress」を意味するかどうか。
        /// suppress の場合、解決時は overlay を発火しない（fallback もしない）。
        /// </summary>
        public bool IsSuppress => string.IsNullOrWhiteSpace(ExpressionId);

        /// <summary>
        /// OverlaySlotBinding を生成する。
        /// </summary>
        /// <param name="slot">slot 識別子（空文字不可）。</param>
        /// <param name="expressionId">overlay Expression の ID。null / 空文字で suppress。</param>
        /// <exception cref="ArgumentException"><paramref name="slot"/> が null / 空 / 全空白の場合。</exception>
        public OverlaySlotBinding(string slot, string expressionId)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                throw new ArgumentException("slot は空にできません。", nameof(slot));
            }

            Slot = slot;
            ExpressionId = expressionId;
        }

        public bool Equals(OverlaySlotBinding other)
        {
            return string.Equals(Slot, other.Slot, StringComparison.Ordinal)
                && string.Equals(ExpressionId, other.ExpressionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is OverlaySlotBinding b && Equals(b);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Slot != null ? StringComparer.Ordinal.GetHashCode(Slot) : 0;
                h = (h * 397) ^ (ExpressionId != null ? StringComparer.Ordinal.GetHashCode(ExpressionId) : 0);
                return h;
            }
        }

        public override string ToString()
        {
            return IsSuppress
                ? $"OverlaySlotBinding(slot='{Slot}', SUPPRESS)"
                : $"OverlaySlotBinding(slot='{Slot}', expressionId='{ExpressionId}')";
        }
    }
}
