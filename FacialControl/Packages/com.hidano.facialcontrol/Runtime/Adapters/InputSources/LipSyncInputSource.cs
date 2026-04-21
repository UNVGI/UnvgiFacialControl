using System;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// 予約 id <c>lipsync</c> を持つ BlendShape 値提供型アダプタ。
    /// <see cref="ILipSyncProvider.GetLipSyncValues(Span{float})"/> の出力を
    /// <c>output</c> Span にコピーし、値合計が <see cref="SilenceThreshold"/> 未満の
    /// 無音フレームでは <c>IsValid = false</c> を返す（Req 5.3, 5.6, 5.7）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IInputSource"/> 契約の「false 時は <c>output</c> 非変更」を満たすため、
    /// provider から受け取った値は一旦内部スクラッチバッファに格納してから
    /// 合計を判定し、閾値以上のときだけ <c>output</c> へコピーする。
    /// スクラッチは構築時に 1 度だけ確保し、毎フレームのヒープ確保を避ける。
    /// </para>
    /// <para>
    /// 本アダプタは <see cref="ILipSyncProvider"/> のみに依存し、Unity API を直接参照しない。
    /// 音声解析はスコープ外であり、外部プラグイン（uLipSync 等）が provider を実装する前提。
    /// </para>
    /// </remarks>
    public sealed class LipSyncInputSource : ValueProviderInputSourceBase
    {
        /// <summary>本アダプタの予約識別子。</summary>
        public const string ReservedId = "lipsync";

        /// <summary>無音判定の閾値。合計がこれ未満なら <c>IsValid = false</c>。</summary>
        public const float SilenceThreshold = 1e-4f;

        private readonly ILipSyncProvider _provider;
        private readonly float[] _scratch;

        /// <summary>
        /// <see cref="LipSyncInputSource"/> を構築する。
        /// </summary>
        /// <param name="provider">リップシンク値の供給元。</param>
        /// <param name="blendShapeCount">書込む BlendShape 個数 (0 以上)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="blendShapeCount"/> が負。</exception>
        public LipSyncInputSource(ILipSyncProvider provider, int blendShapeCount)
            : base(InputSourceId.Parse(ReservedId), blendShapeCount)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _provider = provider;
            _scratch = new float[blendShapeCount];
        }

        /// <summary>
        /// provider からリップシンク値を取得し、無音でなければ <paramref name="output"/> に書込む。
        /// </summary>
        /// <param name="output">書込先バッファ。長さ不足時は overlap のみ書込む。</param>
        /// <returns>発話時 true、無音時 false（false の場合 <paramref name="output"/> 非変更）。</returns>
        public override bool TryWriteValues(Span<float> output)
        {
            Span<float> scratch = _scratch;
            // provider が overlap のみ書込む実装でも残値が残らないよう毎フレーム明示クリア。
            scratch.Clear();
            _provider.GetLipSyncValues(scratch);

            float sum = 0f;
            for (int i = 0; i < scratch.Length; i++)
            {
                sum += scratch[i];
            }

            if (sum < SilenceThreshold)
            {
                return false;
            }

            int copyLength = output.Length < scratch.Length ? output.Length : scratch.Length;
            for (int i = 0; i < copyLength; i++)
            {
                output[i] = scratch[i];
            }

            return true;
        }
    }
}
