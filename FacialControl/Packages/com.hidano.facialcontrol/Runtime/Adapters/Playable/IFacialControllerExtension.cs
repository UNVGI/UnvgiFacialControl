using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.Playable
{
    /// <summary>
    /// FacialController の初期化時にサブパッケージから入力源を追加登録するための拡張点。
    /// 公式サブパッケージ（<c>com.hidano.facialcontrol.osc</c> /
    /// <c>com.hidano.facialcontrol.inputsystem</c> 等）が MonoBehaviour として実装し、
    /// <see cref="FacialController"/> と同じ GameObject に配置することで自動検出される。
    /// </summary>
    /// <remarks>
    /// FacialController は <c>InitializeInternal</c> で <see cref="InputSourceFactory"/> を構築した直後、
    /// 同 GameObject 上の <see cref="IFacialControllerExtension"/> 全てに対し
    /// <see cref="ConfigureFactory"/> を呼び出す。各 extension はそこで
    /// <see cref="InputSourceFactory.RegisterReserved{TOptions}"/> 等を用いて入力源を登録する。
    /// 失敗（例外）は呼出側でキャッチされエラーログが出るが他の extension の処理は継続される。
    /// </remarks>
    public interface IFacialControllerExtension
    {
        /// <summary>
        /// FacialController の <see cref="InputSourceFactory"/> 構築直後に呼ばれる。
        /// 必要な入力源を <see cref="InputSourceFactory.RegisterReserved{TOptions}"/> で登録する。
        /// </summary>
        /// <param name="factory">構築直後の InputSourceFactory（追加登録可能）</param>
        /// <param name="profile">現在ロード中の FacialProfile</param>
        /// <param name="blendShapeNames">
        /// SkinnedMeshRenderer から集計済みの BlendShape 名（Expression トリガー型アダプタの
        /// 名前→インデックス解決に用いる）。FacialController が一度だけ集計済み。
        /// </param>
        void ConfigureFactory(
            InputSourceFactory factory,
            FacialProfile profile,
            IReadOnlyList<string> blendShapeNames);
    }
}
