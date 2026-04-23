using System;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Osc
{
    /// <summary>
    /// FacialControl コアの <see cref="InputSourceFactory"/> に予約 id <c>osc</c> の
    /// アダプタ生成器を登録するヘルパー。
    /// 通常は <c>OscFacialControllerExtension</c> MonoBehaviour 経由で自動的に呼ばれるが、
    /// テストや手動配線でも直接利用できる。
    /// </summary>
    public static class OscRegistration
    {
        /// <summary>
        /// <paramref name="factory"/> に <see cref="OscInputSource"/> 生成器を登録する。
        /// </summary>
        /// <param name="factory">登録対象のファクトリ。</param>
        /// <param name="buffer">OSC 受信ダブルバッファ。OscReceiver から取得した参照を渡す。</param>
        /// <param name="timeProvider">staleness 判定用の時刻供給元。</param>
        /// <exception cref="ArgumentNullException">いずれかの引数が <c>null</c> の場合。</exception>
        public static void Register(
            InputSourceFactory factory,
            OscDoubleBuffer buffer,
            ITimeProvider timeProvider)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (timeProvider == null) throw new ArgumentNullException(nameof(timeProvider));

            factory.RegisterReserved<OscOptionsDto>(
                InputSourceId.Parse(OscInputSource.ReservedId),
                (options, blendShapeCount, profile) =>
                    new OscInputSource(buffer, options.stalenessSeconds, timeProvider));
        }
    }
}
