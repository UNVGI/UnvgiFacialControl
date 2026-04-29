using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// 1 つのアナログ入力源軸 → 1 つのターゲット (BlendShape または BonePose 軸) の
    /// 宣言的バインディング (Domain 値型, Req 6.2)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SourceId"/> は <see cref="InputSourceId"/> 規約に従う識別子（実体検証は呼出側責務）。
    /// <see cref="SourceAxis"/> は scalar=0 / Vector2 (X=0, Y=1) / N-axis のチャンネル番号（&gt;= 0）。
    /// <see cref="TargetAxis"/> は <see cref="AnalogBindingTargetKind.BonePose"/> のときのみ意味を持ち、
    /// <see cref="AnalogBindingTargetKind.BlendShape"/> では無視される。
    /// </para>
    /// </remarks>
    public readonly struct AnalogBindingEntry
    {
        /// <summary>入力源識別子（<see cref="InputSourceId.Value"/> 文字列形）。</summary>
        public string SourceId { get; }

        /// <summary>入力源側のチャンネル番号（&gt;= 0）。scalar の場合は 0、Vector2 では 0=X / 1=Y。</summary>
        public int SourceAxis { get; }

        /// <summary>ターゲット種別（BlendShape / BonePose）。</summary>
        public AnalogBindingTargetKind TargetKind { get; }

        /// <summary>ターゲット識別子（BlendShape 名 または bone 名）。null / 空 / 全空白は不可。</summary>
        public string TargetIdentifier { get; }

        /// <summary>BonePose ターゲットでの Euler 軸（X/Y/Z）。BlendShape ターゲットでは未使用。</summary>
        public AnalogTargetAxis TargetAxis { get; }

        /// <summary>マッピング関数。dead-zone / scale / offset / curve / inversion / clamp を含む。</summary>
        public AnalogMappingFunction Mapping { get; }

        /// <summary>
        /// バインディングエントリを構築する。
        /// </summary>
        /// <param name="sourceId">入力源識別子（呼出側で <see cref="InputSourceId"/> 規約に従っていることを保証）。</param>
        /// <param name="sourceAxis">入力源軸 (&gt;= 0)。</param>
        /// <param name="targetKind">ターゲット種別。</param>
        /// <param name="targetIdentifier">ターゲット識別子（BlendShape 名 / bone 名）。</param>
        /// <param name="targetAxis">BonePose ターゲットの Euler 軸（BlendShape では無視）。</param>
        /// <param name="mapping">マッピング関数。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceAxis"/> が負の場合。</exception>
        /// <exception cref="ArgumentException"><paramref name="targetIdentifier"/> が null / 空 / 全空白の場合。</exception>
        public AnalogBindingEntry(
            string sourceId,
            int sourceAxis,
            AnalogBindingTargetKind targetKind,
            string targetIdentifier,
            AnalogTargetAxis targetAxis,
            AnalogMappingFunction mapping)
        {
            if (sourceAxis < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAxis),
                    sourceAxis,
                    "AnalogBindingEntry.sourceAxis must be >= 0.");
            }

            if (string.IsNullOrWhiteSpace(targetIdentifier))
            {
                throw new ArgumentException(
                    "AnalogBindingEntry.targetIdentifier must be a non-empty, non-whitespace string.",
                    nameof(targetIdentifier));
            }

            SourceId = sourceId ?? string.Empty;
            SourceAxis = sourceAxis;
            TargetKind = targetKind;
            TargetIdentifier = targetIdentifier;
            TargetAxis = targetAxis;
            Mapping = mapping;
        }
    }
}
