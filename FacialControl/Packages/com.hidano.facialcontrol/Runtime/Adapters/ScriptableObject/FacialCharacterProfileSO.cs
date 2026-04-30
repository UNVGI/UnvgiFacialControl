using System.Collections.Generic;
using System.IO;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// FacialController が表情データを取得する抽象 ScriptableObject。
    /// Inspector 上でレイヤー / Expression / BonePose / Renderer パスを直接編集できる。
    /// 入力連携 (InputActionAsset / バインディング) を持つ具象クラスは
    /// <c>com.hidano.facialcontrol.inputsystem</c> 等のサブパッケージで定義する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// データソースの優先度: 起動時に <c>StreamingAssets/FacialControl/{<see cref="CharacterAssetName"/>}/profile.json</c>
    /// が存在すれば JSON を正規データとして読み込む。存在しなければ Inspector でシリアライズされた
    /// SO 内のフィールドから <see cref="BuildFallbackProfile"/> で組み立てる (preview のオフライン動作)。
    /// </para>
    /// <para>
    /// SO 編集時は Editor が裏で同 JSON を自動エクスポートし、ビルド後のコンテンツ差し替えが
    /// 常に可能な状態に保つ (3-B モデル)。
    /// </para>
    /// <para>
    /// namespace は <c>.Serializable</c> 配下に置いて旧 <c>FacialProfileSO</c> 同名型 (BonePoseSerializable 等)
    /// との衝突を回避している。Task 8 で旧 SO 削除後にネームスペース整理を再評価する。
    /// </para>
    /// </remarks>
    public abstract class FacialCharacterProfileSO : UnityEngine.ScriptableObject, IFacialCharacterProfile
    {
        /// <summary>
        /// StreamingAssets 配下の規約フォルダ名 (<c>StreamingAssets/FacialControl/...</c>)。
        /// </summary>
        public const string StreamingAssetsRootFolder = "FacialControl";

        /// <summary>
        /// 規約 JSON ファイル名 (<c>profile.json</c>)。
        /// </summary>
        public const string ProfileJsonFileName = "profile.json";

        [Tooltip("JSON スキーマバージョン。現状は \"1.0\"。")]
        [SerializeField]
        protected string _schemaVersion = "1.0";

        [Tooltip("レイヤー定義一覧。")]
        [SerializeField]
        protected List<LayerDefinitionSerializable> _layers = new List<LayerDefinitionSerializable>();

        [Tooltip("Expression 一覧。")]
        [SerializeField]
        protected List<ExpressionSerializable> _expressions = new List<ExpressionSerializable>();

        [Tooltip("SkinnedMeshRenderer のヒエラルキーパス (モデルルート相対)。")]
        [SerializeField]
        protected List<string> _rendererPaths = new List<string>();

        [Tooltip("BonePose 一覧 (顔相対 Euler 角)。")]
        [SerializeField]
        protected List<BonePoseSerializable> _bonePoses = new List<BonePoseSerializable>();

#if UNITY_EDITOR
        [Tooltip("BlendShape 名 / ボーン名取得用の参照モデル (Editor 専用、ビルドには含まれない)。")]
        [SerializeField]
        protected GameObject _referenceModel;

        /// <summary>
        /// Inspector で BlendShape 名取得などに用いる参照モデル (Editor 専用)。
        /// </summary>
        public GameObject ReferenceModel
        {
            get => _referenceModel;
            set => _referenceModel = value;
        }
#endif

        /// <inheritdoc />
        public string CharacterAssetName => name;

        /// <summary>JSON スキーマバージョン。</summary>
        public string SchemaVersion
        {
            get => _schemaVersion;
            set => _schemaVersion = value;
        }

        /// <summary>レイヤー定義リスト (編集用)。</summary>
        public List<LayerDefinitionSerializable> Layers => _layers;

        /// <summary>Expression リスト (編集用)。</summary>
        public List<ExpressionSerializable> Expressions => _expressions;

        /// <summary>SkinnedMeshRenderer ヒエラルキーパスリスト (編集用)。</summary>
        public List<string> RendererPaths => _rendererPaths;

        /// <summary>BonePose リスト (編集用)。</summary>
        public List<BonePoseSerializable> BonePoses => _bonePoses;

        /// <summary>
        /// SO 内のシリアライズ済みフィールドから <see cref="FacialProfile"/> を組み立てる。
        /// JSON が見つからない・読み込めない場合のフォールバックや、JSON エクスポート前段で利用する。
        /// </summary>
        public virtual FacialProfile BuildFallbackProfile()
        {
            return FacialCharacterProfileConverter.ToFacialProfile(
                _schemaVersion,
                _layers,
                _expressions,
                _rendererPaths,
                _bonePoses);
        }

        /// <summary>
        /// <see cref="StreamingAssetsRootFolder"/> + SO 名 + <see cref="ProfileJsonFileName"/> の規約パスを返す。
        /// 結果は <c>UnityEngine.Application.streamingAssetsPath</c> からの絶対パス。
        /// </summary>
        /// <param name="assetName"><see cref="CharacterAssetName"/> 。空白なら null を返す。</param>
        public static string GetStreamingAssetsProfilePath(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }
            return Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                StreamingAssetsRootFolder,
                assetName,
                ProfileJsonFileName);
        }

        /// <summary>
        /// 同 SO に対して <c>StreamingAssets/FacialControl/{name}/profile.json</c> を読みに行き、
        /// 存在すれば JSON から、存在しなければ <see cref="BuildFallbackProfile"/> から
        /// <see cref="FacialProfile"/> を構築する。
        /// </summary>
        /// <remarks>
        /// 例外 (パース失敗 / I/O 失敗) は <see cref="Debug.LogWarning"/> で報告し、フォールバックに切り替える。
        /// </remarks>
        public virtual FacialProfile LoadProfile()
        {
            string path = GetStreamingAssetsProfilePath(CharacterAssetName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return BuildFallbackProfile();
            }

            try
            {
                var repo = new FileProfileRepository(new SystemTextJsonParser());
                return repo.LoadProfile(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterProfileSO '{name}': StreamingAssets JSON の読み込みに失敗したため SO の Inspector データから組み立てます: {ex.Message}");
                return BuildFallbackProfile();
            }
        }
    }
}
