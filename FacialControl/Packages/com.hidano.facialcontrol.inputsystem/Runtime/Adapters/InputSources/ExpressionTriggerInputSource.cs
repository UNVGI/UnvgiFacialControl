using System.Collections.Generic;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// Expression トリガー型入力源の汎用具象実装 (tasks.md 4.6)。
    /// 旧 <c>KeyboardExpressionInputSource</c> / <c>ControllerExpressionInputSource</c> を統合し、
    /// device 種別に依存しない 1 個の concrete 実装として提供する (Req 7.1, 8.1, 8.2)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本クラスは <see cref="ExpressionTriggerInputSourceBase"/> を継承し、構築時に与えられた
    /// 任意の <see cref="InputSourceId"/> を保持する。device 別カテゴリ分けは
    /// <see cref="ExpressionInputSourceAdapter"/> + <see cref="Hidano.FacialControl.Adapters.Input.InputDeviceCategorizer"/>
    /// が <see cref="UnityEngine.InputSystem.InputAction.bindings"/> から自動推定して dispatch する責務を負う。
    /// </para>
    /// <para>
    /// 後方互換のため予約 id 文字列 <c>controller-expr</c> / <c>keyboard-expr</c> を
    /// <see cref="ControllerReservedId"/> / <see cref="KeyboardReservedId"/> として公開する。
    /// JSON Layer の <c>inputSources[]</c> 宣言で従来通り両 id を併用できる。
    /// </para>
    /// </remarks>
    public sealed class ExpressionTriggerInputSource : ExpressionTriggerInputSourceBase
    {
        /// <summary>後方互換用予約 id (Controller 系)。新規実装では device 自動推定により区別不要。</summary>
        public const string ControllerReservedId = "controller-expr";

        /// <summary>後方互換用予約 id (Keyboard 系)。新規実装では device 自動推定により区別不要。</summary>
        public const string KeyboardReservedId = "keyboard-expr";

        /// <summary>
        /// <see cref="ExpressionTriggerInputSource"/> を構築する。
        /// </summary>
        /// <param name="id">入力源識別子 (任意の <see cref="InputSourceId"/>)。</param>
        /// <param name="blendShapeCount">書込む BlendShape 個数 (0 以上)。</param>
        /// <param name="maxStackDepth">Expression スタックの最大深度 (1 以上)。</param>
        /// <param name="exclusionMode">本インスタンス内部で適用する排他モード。</param>
        /// <param name="blendShapeNames">BlendShape 名の列 (名前→インデックス解決用)。</param>
        /// <param name="profile">Expression 検索に用いる <see cref="FacialProfile"/>。</param>
        public ExpressionTriggerInputSource(
            InputSourceId id,
            int blendShapeCount,
            int maxStackDepth,
            ExclusionMode exclusionMode,
            IReadOnlyList<string> blendShapeNames,
            FacialProfile profile)
            : base(id, blendShapeCount, maxStackDepth, exclusionMode, blendShapeNames, profile)
        {
        }
    }
}
