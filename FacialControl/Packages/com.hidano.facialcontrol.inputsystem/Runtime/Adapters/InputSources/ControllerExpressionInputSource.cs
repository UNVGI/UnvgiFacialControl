using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// 予約 id <c>controller-expr</c> を持つ Expression トリガー型入力源アダプタ。
    /// ゲームコントローラ系（<see cref="InputSourceCategory.Controller"/>）の
    /// <see cref="InputBindingProfileSO"/> を経由した入力を <see cref="TriggerOn"/> /
    /// <see cref="TriggerOff"/> で受け取り、自身の独立した Expression スタックと
    /// TransitionCalculator 状態を駆動する（Req 5.1, 5.7, D-1, D-13）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本アダプタはキーボード相当の <c>keyboard-expr</c> アダプタとは互いに独立した
    /// Expression スタックを持ち、同一レイヤー上で同時に異なる Expression を駆動しても
    /// 干渉しない（D-12）。カテゴリ分離は呼出側（<c>FacialInputBinder</c> など）が
    /// <see cref="InputBindingProfileSO.InputSourceCategory"/> と本アダプタの
    /// <see cref="Category"/> を突き合わせて行う責務であり、
    /// 本アダプタ自身はカテゴリによる入力フィルタリングを行わない。
    /// </para>
    /// </remarks>
    public sealed class ControllerExpressionInputSource : ExpressionTriggerInputSourceBase
    {
        /// <summary>
        /// 本アダプタの予約識別子。<see cref="ExpressionTriggerInputSourceBase.Id"/> は
        /// 常にこの値を返す。
        /// </summary>
        public const string ReservedId = "controller-expr";

        /// <summary>
        /// 本アダプタが担当する入力源カテゴリ。常に <see cref="InputSourceCategory.Controller"/>。
        /// 呼出側は本プロパティを参照して、入力トリガーを適切なアダプタへディスパッチする。
        /// </summary>
        public InputSourceCategory Category => InputSourceCategory.Controller;

        /// <summary>
        /// <see cref="ControllerExpressionInputSource"/> を構築する。
        /// </summary>
        /// <param name="blendShapeCount">書込む BlendShape 個数 (0 以上)。</param>
        /// <param name="maxStackDepth">Expression スタックの最大深度 (1 以上)。</param>
        /// <param name="exclusionMode">本アダプタ内部で適用する排他モード。</param>
        /// <param name="blendShapeNames">BlendShape 名の列 (名前→インデックス解決用)。</param>
        /// <param name="profile">Expression 検索に用いる <see cref="FacialProfile"/>。</param>
        public ControllerExpressionInputSource(
            int blendShapeCount,
            int maxStackDepth,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> blendShapeNames,
            FacialProfile profile)
            : base(
                InputSourceId.Parse(ReservedId),
                blendShapeCount,
                maxStackDepth,
                exclusionMode,
                blendShapeNames,
                profile)
        {
        }
    }
}
