using Hidano.FacialControl.Domain.Interfaces;

namespace Hidano.FacialControl.Tests.Shared
{
    /// <summary>
    /// テストから明示的に時刻を前進させるための <see cref="ITimeProvider"/> フェイク。
    /// EditMode の staleness テスト（Req 5.5）や verbose log rate-limit テスト（Req 8.5）
    /// で時刻を決定論的に制御するために使う。
    /// </summary>
    public sealed class ManualTimeProvider : ITimeProvider
    {
        /// <summary>
        /// 現在の経過秒数。テストは代入で時刻を任意に前進させる。
        /// 単調増加の契約はテスト側の責務で担保する（Req 2.2 の単調性テストで検証）。
        /// </summary>
        public double UnscaledTimeSeconds { get; set; }
    }
}
