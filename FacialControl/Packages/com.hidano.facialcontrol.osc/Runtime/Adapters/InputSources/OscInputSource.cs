using System;
using System.Threading;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Unity.Collections;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// 予約 id <c>osc</c> を持つ BlendShape 値提供型アダプタ。
    /// <see cref="OscDoubleBuffer"/> の読み取りバッファを <c>output</c> Span に
    /// コピーし、受信停止時は <c>stalenessSeconds</c> オプトインで IsValid=false を返す
    /// （Req 5.2, 5.4, 5.5, 5.7）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// staleness 判定は <see cref="OscDoubleBuffer.WriteTick"/> を <see cref="Volatile.Read(ref int)"/>
    /// で監視し、新規受信を観測したフレームで <see cref="ITimeProvider.UnscaledTimeSeconds"/>
    /// を <c>_lastDataTime</c> に記録する。<c>stalenessSeconds &gt; 0</c> のとき
    /// 現在時刻 − <c>_lastDataTime</c> が閾値を超えると <see cref="TryWriteValues"/> が
    /// false を返し、<c>output</c> は変更しない。
    /// </para>
    /// <para>
    /// 本アダプタは Domain の <see cref="ITimeProvider"/> にのみ依存し、
    /// Unity API を直接参照しない (テスト時は <c>ManualTimeProvider</c> を DI して
    /// 時刻を決定論的に前進させる — Critical 3)。
    /// </para>
    /// </remarks>
    public sealed class OscInputSource : ValueProviderInputSourceBase
    {
        /// <summary>本アダプタの予約識別子。</summary>
        public const string ReservedId = "osc";

        private readonly OscDoubleBuffer _buffer;
        private readonly ITimeProvider _timeProvider;
        private readonly float _stalenessSeconds;

        private int _lastObservedTick;
        private double _lastDataTime;

        /// <summary>
        /// <see cref="OscInputSource"/> を構築する。
        /// </summary>
        /// <param name="buffer">OSC 受信側ダブルバッファ。読み取りバッファを本アダプタが参照する。</param>
        /// <param name="stalenessSeconds">
        /// 受信停止とみなす秒数 (&gt;= 0)。0 なら staleness 判定を無効化し常に true を返す (Req 5.5)。
        /// </param>
        /// <param name="timeProvider">現在時刻の供給元。Adapters 層では <c>UnityTimeProvider</c> を DI する。</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> または <paramref name="timeProvider"/> が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stalenessSeconds"/> が負。</exception>
        public OscInputSource(OscDoubleBuffer buffer, float stalenessSeconds, ITimeProvider timeProvider)
            : base(InputSourceId.Parse(ReservedId), buffer != null ? buffer.Size : 0)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (timeProvider == null)
            {
                throw new ArgumentNullException(nameof(timeProvider));
            }
            if (stalenessSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(stalenessSeconds), stalenessSeconds,
                    "stalenessSeconds は 0 以上を指定してください。");
            }

            _buffer = buffer;
            _timeProvider = timeProvider;
            _stalenessSeconds = stalenessSeconds;
            _lastObservedTick = 0;
            _lastDataTime = timeProvider.UnscaledTimeSeconds;
        }

        /// <summary>
        /// OSC 受信バッファの内容を <paramref name="output"/> に書込む。
        /// staleness 超過時は false を返し <paramref name="output"/> を変更しない。
        /// </summary>
        public override bool TryWriteValues(Span<float> output)
        {
            int currentTick = _buffer.WriteTick;
            if (currentTick != _lastObservedTick)
            {
                _lastDataTime = _timeProvider.UnscaledTimeSeconds;
                _lastObservedTick = currentTick;
            }

            if (_stalenessSeconds > 0f &&
                _timeProvider.UnscaledTimeSeconds - _lastDataTime > _stalenessSeconds)
            {
                return false;
            }

            NativeArray<float>.ReadOnly readBuffer = _buffer.GetReadBuffer();
            int copyLength = output.Length < readBuffer.Length ? output.Length : readBuffer.Length;
            for (int i = 0; i < copyLength; i++)
            {
                output[i] = readBuffer[i];
            }

            return true;
        }
    }
}
