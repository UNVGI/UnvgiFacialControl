using System;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Domain.Interfaces
{
    /// <summary>
    /// D-1 ハイブリッドモデルにおける入力源の共通契約。
    /// Expression トリガー型 / BlendShape 値提供型のいずれのアダプタも本インターフェースを実装し、
    /// 固定長バッファへの書込と自身の有効性申告を提供する。
    /// Domain 層配置のため Unity API に非依存（Req 1.5）。
    /// </summary>
    /// <remarks>
    /// Preconditions:
    /// <list type="bullet">
    ///   <item><c>output.Length &gt; 0</c>（呼出側が保証）。</item>
    ///   <item><c>Tick</c> はメインスレッドから毎フレーム 1 回呼ばれる。</item>
    /// </list>
    /// Postconditions:
    /// <list type="bullet">
    ///   <item><c>TryWriteValues</c> が false を返した場合、<c>output</c> は変更されない。</item>
    ///   <item>true の場合、overlap 範囲のみ値を書込む（残余は呼出側が事前にゼロクリア）。</item>
    /// </list>
    /// Invariants:
    /// <list type="bullet">
    ///   <item><see cref="Id"/> と <see cref="Type"/> は生涯不変。</item>
    ///   <item><see cref="BlendShapeCount"/> はプロファイル有効期間中不変。</item>
    /// </list>
    /// </remarks>
    public interface IInputSource
    {
        /// <summary>
        /// 入力源識別子。<c>[a-zA-Z0-9_.-]{1,64}</c> 規約に従う（Req 1.7）。
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 入力源の種別（Expression トリガー型 / 値提供型）。
        /// </summary>
        InputSourceType Type { get; }

        /// <summary>
        /// 本入力源が書込む BlendShape の個数。
        /// </summary>
        int BlendShapeCount { get; }

        /// <summary>
        /// 1 フレーム分の時間進行。Expression トリガー型のみ実質的な work を行う
        /// （TransitionCalculator 状態の更新）。値提供型は通常空実装。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過秒数（&gt;= 0）</param>
        void Tick(float deltaTime);

        /// <summary>
        /// 自身の BlendShape 値を出力バッファに書込む。
        /// </summary>
        /// <param name="output">書込先バッファ。<c>Length &gt; 0</c>。長さ不足時は overlap のみ書込む。</param>
        /// <returns>有効なら true、無効/未接続なら false（false の場合 <paramref name="output"/> は変更されない）。</returns>
        bool TryWriteValues(Span<float> output);
    }
}
