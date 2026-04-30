using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// FacialController が表情プロファイルを取得するための抽象。
    /// 具象 ScriptableObject (<see cref="FacialCharacterProfileSO"/>) は SO アセット名を介して
    /// StreamingAssets/FacialControl/{name}/profile.json への規約パスを公開し、
    /// JSON が見つからない場合は Inspector でシリアライズされたフィールドから
    /// FacialProfile をフォールバック構築する。
    /// </summary>
    public interface IFacialCharacterProfile
    {
        /// <summary>
        /// SO アセット名。StreamingAssets 配下の規約パス算出に使われる。
        /// </summary>
        string CharacterAssetName { get; }

        /// <summary>
        /// JSON ファイルが見つからない場合に Inspector でシリアライズされた
        /// フィールドから FacialProfile を組み立てる。
        /// </summary>
        FacialProfile BuildFallbackProfile();
    }
}
