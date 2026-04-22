using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Application.UseCases;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Adapters.Playable
{
    /// <summary>
    /// FacialControl のメインコンポーネント。
    /// FacialProfileSO を参照して PlayableGraph を構築し、
    /// Expression のアクティブ化・非アクティブ化を制御する。
    /// OnEnable で自動初期化、Initialize() で手動初期化が可能。
    /// OnDisable で PlayableGraph と NativeArray を破棄する。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("FacialControl/Facial Controller")]
    public class FacialController : MonoBehaviour
    {
        /// <summary>
        /// 表情プロファイルの ScriptableObject 参照
        /// </summary>
        [Tooltip("表情プロファイルの ScriptableObject")]
        [SerializeField]
        private FacialProfileSO _profileSO;

        /// <summary>
        /// SkinnedMeshRenderer の手動オーバーライドリスト。
        /// 空の場合は子オブジェクトから自動検索する。
        /// </summary>
        [Tooltip("SkinnedMeshRenderer のリスト（空の場合は自動検索）")]
        [SerializeField]
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;

        /// <summary>
        /// OSC 送信ポート番号
        /// </summary>
        [Tooltip("OSC 送信ポート番号")]
        [SerializeField]
        private int _oscSendPort = Domain.Models.OscConfiguration.DefaultSendPort;

        /// <summary>
        /// OSC 受信ポート番号
        /// </summary>
        [Tooltip("OSC 受信ポート番号")]
        [SerializeField]
        private int _oscReceivePort = Domain.Models.OscConfiguration.DefaultReceivePort;

        private Animator _animator;
        private PlayableGraphBuilder.BuildResult _graphBuildResult;
        private ExpressionUseCase _expressionUseCase;
        private LayerUseCase _layerUseCase;
        private InputSourceFactory _inputSourceFactory;
        private FacialProfile? _currentProfile;
        private string[] _blendShapeNames;
        private bool _isInitialized;

        // プロセス内で 1 インスタンスだけ保持する時刻源 (8.2)。
        // 初期化経路 (InputSourceFactory / LayerInputSourceAggregator) で共有する。
        private static ITimeProvider s_sharedTimeProvider;

        // BlendShape 出力インデックス → (Renderer, Renderer 上の BS index) のマッピング
        private BlendShapeTarget[][] _blendShapeTargets;

        /// <summary>
        /// BlendShape の出力先ターゲット情報。
        /// </summary>
        private struct BlendShapeTarget
        {
            public SkinnedMeshRenderer Renderer;
            public int RendererBlendShapeIndex;

            public BlendShapeTarget(SkinnedMeshRenderer renderer, int rendererBlendShapeIndex)
            {
                Renderer = renderer;
                RendererBlendShapeIndex = rendererBlendShapeIndex;
            }
        }

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 現在のプロファイル。未読み込みの場合は null。
        /// </summary>
        public FacialProfile? CurrentProfile => _currentProfile;

        /// <summary>
        /// FacialProfileSO の参照
        /// </summary>
        public FacialProfileSO ProfileSO
        {
            get => _profileSO;
            set => _profileSO = value;
        }

        /// <summary>
        /// SkinnedMeshRenderer のリスト（手動オーバーライド用）
        /// </summary>
        public SkinnedMeshRenderer[] SkinnedMeshRenderers
        {
            get => _skinnedMeshRenderers;
            set => _skinnedMeshRenderers = value;
        }

        /// <summary>
        /// OSC 送信ポート番号
        /// </summary>
        public int OscSendPort
        {
            get => _oscSendPort;
            set => _oscSendPort = value;
        }

        /// <summary>
        /// OSC 受信ポート番号
        /// </summary>
        public int OscReceivePort
        {
            get => _oscReceivePort;
            set => _oscReceivePort = value;
        }

        // ================================================================
        // ライフサイクル
        // ================================================================

        private void OnEnable()
        {
            // ProfileSO が設定されていれば自動初期化を試みる
            if (_profileSO != null)
            {
                Initialize();
            }
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _blendShapeTargets == null || _layerUseCase == null)
                return;

            // Aggregator パイプラインを 1 フレーム分進める。
            // sourceIdx=0 の LayerExpressionSource は ExpressionUseCase.GetActiveExpressions から駆動、
            // sourceIdx=1+ の IInputSource (controller-expr / keyboard-expr / osc 等) は
            // 各アダプタの TriggerOn/Off または WriteTick 経由で駆動される。
            _layerUseCase.UpdateWeights(Time.deltaTime);

            // Aggregator 出力を BlendShape に転写。PlayableGraph の出力はバイパスする
            // （PlayableGraph は preview.2 以降で撤去検討）。
            var output = _layerUseCase.BlendedOutputSpan;

            int count = Math.Min(output.Length, _blendShapeTargets.Length);
            for (int i = 0; i < count; i++)
            {
                var targets = _blendShapeTargets[i];
                if (targets == null)
                    continue;

                float weight = output[i] * 100f; // Unity は 0-100 スケール
                for (int t = 0; t < targets.Length; t++)
                {
                    targets[t].Renderer.SetBlendShapeWeight(
                        targets[t].RendererBlendShapeIndex, weight);
                }
            }
        }

        /// <summary>
        /// 手動初期化。ProfileSO からプロファイルを読み込み、PlayableGraph を構築する。
        /// ProfileSO が未設定の場合は警告を出して何もしない。
        /// </summary>
        public void Initialize()
        {
            if (_profileSO == null)
            {
                return;
            }

            // Animator 取得
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogWarning("Animator コンポーネントが見つかりません。初期化をスキップします。");
                return;
            }

            // SkinnedMeshRenderer を取得
            var renderers = ResolveSkinnedMeshRenderers();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("SkinnedMeshRenderer が見つかりません。初期化をスキップします。");
                return;
            }

            // BlendShape 名を収集
            _blendShapeNames = CollectBlendShapeNames(renderers);

            // プロファイルを構築（JSON パスが設定されていれば StreamingAssets から読み込む）
            FacialProfile profile = LoadProfileFromSO(_profileSO);

            InitializeInternal(profile);
        }

        /// <summary>
        /// テスト・内部用: FacialProfile を直接指定して初期化する。
        /// JSON ファイルパスを経由せずにインメモリのプロファイルで初期化できる。
        /// </summary>
        /// <param name="profile">使用するプロファイル</param>
        public void InitializeWithProfile(FacialProfile profile)
        {
            // Animator 取得
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogWarning("Animator コンポーネントが見つかりません。初期化をスキップします。");
                return;
            }

            // SkinnedMeshRenderer を取得
            var renderers = ResolveSkinnedMeshRenderers();

            // BlendShape 名を収集
            _blendShapeNames = CollectBlendShapeNames(renderers);

            InitializeInternal(profile);
        }

        private void InitializeInternal(FacialProfile profile)
        {
            // 既存のリソースがあればクリーンアップ (Registry / WeightBuffer の Dispose を含む)
            Cleanup();

            _currentProfile = profile;
            _expressionUseCase = new ExpressionUseCase(profile);

            // UnityTimeProvider はプロセス内単一インスタンスを再利用する (8.2)
            if (s_sharedTimeProvider == null)
            {
                s_sharedTimeProvider = new UnityTimeProvider();
            }

            // InputSourceFactory をプロファイルごとに構築する
            // (blendShapeNames などプロファイル由来の依存を持つため)。
            // OSC / LipSync は後続タスクで配線する想定で今は null を渡す
            // (osc / lipsync 宣言は TryCreate が null を返して呼出側で skip される契約)。
            var blendShapeNames = _blendShapeNames ?? Array.Empty<string>();
            _inputSourceFactory = new InputSourceFactory(
                oscBuffer: null,
                timeProvider: s_sharedTimeProvider,
                lipSyncProvider: null,
                blendShapeNames: blendShapeNames);

            // profile.LayerInputSources を Factory 経由で IInputSource 列に変換する。
            var additionalSources = BuildAdditionalInputSources(profile, _inputSourceFactory, blendShapeNames.Length);

            // LayerUseCase に組み立て済み IInputSource 列を注入し、
            // 内部で LayerInputSourceRegistry / LayerInputSourceWeightBuffer / LayerInputSourceAggregator を再構築させる。
            _layerUseCase = new LayerUseCase(profile, _expressionUseCase, blendShapeNames, additionalSources);

            // PlayableGraph を構築
            _graphBuildResult = PlayableGraphBuilder.Build(
                _animator, profile, blendShapeNames);

            _graphBuildResult.Graph.Play();
            _isInitialized = true;
        }

        private static List<(int layerIdx, IInputSource source, float weight)> BuildAdditionalInputSources(
            FacialProfile profile,
            InputSourceFactory factory,
            int blendShapeCount)
        {
            var result = new List<(int layerIdx, IInputSource source, float weight)>();
            var layerInputSourcesSpan = profile.LayerInputSources.Span;
            int layerCount = profile.Layers.Length;
            int declarationLayers = layerInputSourcesSpan.Length;
            int upper = layerCount < declarationLayers ? layerCount : declarationLayers;

            for (int l = 0; l < upper; l++)
            {
                var declarations = layerInputSourcesSpan[l];
                if (declarations == null || declarations.Length == 0)
                {
                    continue;
                }

                for (int d = 0; d < declarations.Length; d++)
                {
                    var decl = declarations[d];
                    if (!InputSourceId.TryParse(decl.Id, out var id))
                    {
                        Debug.LogWarning(
                            $"FacialController: inputSource id '{decl.Id ?? "<null>"}' が識別子規約に合致しないため layer {l} でスキップします。");
                        continue;
                    }

                    var options = factory.TryDeserializeOptions(id, decl.OptionsJson);
                    var source = factory.TryCreate(id, options, blendShapeCount, profile);
                    if (source == null)
                    {
                        Debug.LogWarning(
                            $"FacialController: inputSource id '{decl.Id}' のアダプタ生成に失敗したため layer {l} でスキップします (未登録 id または必須依存未注入)。");
                        continue;
                    }

                    result.Add((l, source, decl.Weight));
                }
            }

            return result;
        }

        // ================================================================
        // 公開 API
        // ================================================================

        /// <summary>
        /// Expression をアクティブ化する。
        /// レイヤーの排他モードに基づいて処理される。
        /// </summary>
        /// <param name="expression">アクティブ化する Expression</param>
        public void Activate(Expression expression)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("FacialController が初期化されていません。Activate は無視されます。");
                return;
            }

            _expressionUseCase.Activate(expression);

            // PlayableGraph のレイヤーにも反映
            ApplyExpressionToPlayable(expression);
        }

        /// <summary>
        /// Expression を非アクティブ化する。
        /// </summary>
        /// <param name="expression">非アクティブ化する Expression</param>
        public void Deactivate(Expression expression)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("FacialController が初期化されていません。Deactivate は無視されます。");
                return;
            }

            _expressionUseCase.Deactivate(expression);

            // PlayableGraph のレイヤーからも除去
            RemoveExpressionFromPlayable(expression);
        }

        /// <summary>
        /// プロファイルを切り替える。PlayableGraph を再構築する。
        /// </summary>
        /// <param name="profileSO">新しいプロファイル SO</param>
        public void LoadProfile(FacialProfileSO profileSO)
        {
            if (profileSO == null)
            {
                Debug.LogWarning("ProfileSO が null です。LoadProfile は無視されます。");
                return;
            }

            _profileSO = profileSO;

            var profile = LoadProfileFromSO(profileSO);
            InitializeInternal(profile);
        }

        /// <summary>
        /// 現在のプロファイルを再読み込みする。PlayableGraph を再構築する。
        /// </summary>
        public void ReloadProfile()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("FacialController が初期化されていません。ReloadProfile は無視されます。");
                return;
            }

            if (_currentProfile.HasValue)
            {
                InitializeInternal(_currentProfile.Value);
            }
        }

        /// <summary>
        /// (layer, source) スロットの入力源ウェイトをランタイムで書込む (8.3)。
        /// 任意スレッドから呼出可能で、書込は次回の <c>LayerInputSourceAggregator.Aggregate</c>
        /// 入口の <c>SwapIfDirty</c> 以降に観測される (Req 4.1, 4.2, 4.4)。
        /// 値は 0〜1 に silent clamp され、範囲外 (layer, source) は警告 + no-op (Req 4.3)。
        /// 未初期化の場合は警告ログを出して何もしない。
        /// </summary>
        /// <param name="layerIdx">レイヤーインデックス。</param>
        /// <param name="sourceIdx">入力源インデックス。<c>0</c> は内部 Expression スロットの予約枠、
        /// プロファイル宣言 (<c>inputSources</c>) で追加された入力源は登録順に <c>1, 2, ...</c> を取る (8.2)。</param>
        /// <param name="weight">ウェイト値 (範囲外は silent clamp)。</param>
        public void SetInputSourceWeight(int layerIdx, int sourceIdx, float weight)
        {
            if (!_isInitialized || _layerUseCase == null)
            {
                Debug.LogWarning("FacialController が初期化されていません。SetInputSourceWeight は無視されます。");
                return;
            }

            _layerUseCase.SetInputSourceWeight(layerIdx, sourceIdx, weight);
        }

        /// <summary>
        /// 入力源ウェイトのバルク書込スコープを開始する (8.3)。
        /// 返された <see cref="LayerInputSourceWeightBuffer.BulkScope"/> の
        /// <c>SetWeight</c> で書いた値はスコープの <c>Dispose</c> 時に一括 flush され、
        /// 次回 Aggregate で atomic に観測される (Req 4.5)。
        /// 戻り値は <see cref="IDisposable"/> として <c>using</c> 文で利用可能。
        /// 未初期化の場合は no-op となるスコープを返す。
        /// </summary>
        public LayerInputSourceWeightBuffer.BulkScope BeginInputSourceWeightBatch()
        {
            if (!_isInitialized || _layerUseCase == null)
            {
                Debug.LogWarning("FacialController が初期化されていません。BeginInputSourceWeightBatch は no-op スコープを返します。");
                return default;
            }

            return _layerUseCase.BeginInputSourceWeightBatch();
        }

        /// <summary>
        /// 直近 Aggregate で観測された (layer, source) ウェイトの診断スナップショットを返す
        /// (Req 8.1, 8.3, 8.6)。Editor の読取専用ビュー向け。
        /// 未初期化の場合は空リストを返す。
        /// </summary>
        public IReadOnlyList<LayerSourceWeightEntry> GetInputSourceWeightsSnapshot()
        {
            if (!_isInitialized || _layerUseCase == null)
            {
                return Array.Empty<LayerSourceWeightEntry>();
            }
            return _layerUseCase.GetInputSourceWeightsSnapshot();
        }

        /// <summary>
        /// プロファイルの <c>inputSources</c> 宣言から生成された Expression トリガー型
        /// 入力源 (<c>controller-expr</c> / <c>keyboard-expr</c> など) を id で検索する。
        /// Samples のデモ HUD や Editor ツールから特定アダプタを掴んで
        /// <see cref="ExpressionTriggerInputSourceBase.TriggerOn"/> /
        /// <see cref="ExpressionTriggerInputSourceBase.TriggerOff"/> を直接呼びたい場合に利用する。
        /// </summary>
        /// <param name="id">検索対象の入力源 id。</param>
        /// <param name="source">見つかったアダプタ、見つからない場合は null。</param>
        /// <returns>id が一致する Expression トリガー型ソースが登録されていれば true。</returns>
        public bool TryGetExpressionTriggerSourceById(string id, out ExpressionTriggerInputSourceBase source)
        {
            source = null;
            if (!_isInitialized || _layerUseCase == null)
            {
                return false;
            }
            return _layerUseCase.TryGetExpressionTriggerSourceById(id, out source);
        }

        /// <summary>
        /// 現在アクティブな Expression のリストを返す。
        /// </summary>
        /// <returns>アクティブな Expression のリスト</returns>
        public List<Expression> GetActiveExpressions()
        {
            if (_expressionUseCase == null)
            {
                return new List<Expression>();
            }

            return _expressionUseCase.GetActiveExpressions();
        }

        // ================================================================
        // 内部メソッド
        // ================================================================

        private SkinnedMeshRenderer[] ResolveSkinnedMeshRenderers()
        {
            // 手動オーバーライドが設定されていればそれを使用
            if (_skinnedMeshRenderers != null && _skinnedMeshRenderers.Length > 0)
            {
                return _skinnedMeshRenderers;
            }

            // 子オブジェクトから自動検索し、フィールドに保持する
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            return _skinnedMeshRenderers;
        }

        private string[] CollectBlendShapeNames(SkinnedMeshRenderer[] renderers)
        {
            var names = new List<string>();
            var nameSet = new HashSet<string>();
            // 名前 → 出力インデックスの逆引き
            var nameToIndex = new Dictionary<string, int>();
            // 出力インデックス → ターゲットリスト（同名 BS が複数 Renderer に存在する場合）
            var targetLists = new List<List<BlendShapeTarget>>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sharedMesh == null)
                    continue;

                var mesh = renderer.sharedMesh;
                int blendShapeCount = mesh.blendShapeCount;

                for (int j = 0; j < blendShapeCount; j++)
                {
                    string bsName = mesh.GetBlendShapeName(j);

                    if (nameSet.Add(bsName))
                    {
                        int outputIndex = names.Count;
                        names.Add(bsName);
                        nameToIndex[bsName] = outputIndex;

                        var list = new List<BlendShapeTarget>();
                        list.Add(new BlendShapeTarget(renderer, j));
                        targetLists.Add(list);
                    }
                    else
                    {
                        // 同名 BS が別の Renderer にも存在する
                        int outputIndex = nameToIndex[bsName];
                        targetLists[outputIndex].Add(new BlendShapeTarget(renderer, j));
                    }
                }
            }

            // ターゲットマッピングを配列に変換
            _blendShapeTargets = new BlendShapeTarget[targetLists.Count][];
            for (int i = 0; i < targetLists.Count; i++)
            {
                _blendShapeTargets[i] = targetLists[i].ToArray();
            }

            return names.ToArray();
        }

        private void ApplyExpressionToPlayable(Expression expression)
        {
            if (_graphBuildResult == null || !_currentProfile.HasValue)
                return;

            var profile = _currentProfile.Value;
            string effectiveLayer = profile.GetEffectiveLayer(expression);

            if (!_graphBuildResult.LayerPlayables.TryGetValue(effectiveLayer, out var layerPlayable))
                return;

            var behaviour = layerPlayable.GetBehaviour();

            if (behaviour.ExclusionMode == ExclusionMode.LastWins)
            {
                // BlendShape 値を名前ベースで展開
                var targetValues = ExpandBlendShapeValues(expression);
                behaviour.SetTargetExpression(
                    expression.Id,
                    targetValues,
                    expression.TransitionDuration,
                    expression.TransitionCurve);
            }
            else
            {
                // Blend モード
                var values = ExpandBlendShapeValues(expression);
                behaviour.AddBlendExpression(expression.Id, values, 1.0f);
                behaviour.ComputeBlendOutput();
            }
        }

        private void RemoveExpressionFromPlayable(Expression expression)
        {
            if (_graphBuildResult == null || !_currentProfile.HasValue)
                return;

            var profile = _currentProfile.Value;
            string effectiveLayer = profile.GetEffectiveLayer(expression);

            if (!_graphBuildResult.LayerPlayables.TryGetValue(effectiveLayer, out var layerPlayable))
                return;

            var behaviour = layerPlayable.GetBehaviour();

            if (behaviour.ExclusionMode == ExclusionMode.LastWins)
            {
                behaviour.Deactivate(expression.TransitionDuration);
            }
            else
            {
                behaviour.RemoveBlendExpression(expression.Id);
                behaviour.ComputeBlendOutput();
            }
        }

        private float[] ExpandBlendShapeValues(Expression expression)
        {
            var bsNames = _blendShapeNames ?? Array.Empty<string>();
            var values = new float[bsNames.Length];

            var bsSpan = expression.BlendShapeValues.Span;
            for (int i = 0; i < bsSpan.Length; i++)
            {
                int idx = FindBlendShapeIndex(bsSpan[i].Name);
                if (idx >= 0)
                {
                    values[idx] = bsSpan[i].Value;
                }
            }

            return values;
        }

        private int FindBlendShapeIndex(string name)
        {
            if (_blendShapeNames == null)
                return -1;

            for (int i = 0; i < _blendShapeNames.Length; i++)
            {
                if (_blendShapeNames[i] == name)
                    return i;
            }

            return -1;
        }

        private void Cleanup()
        {
            if (_graphBuildResult != null)
            {
                _graphBuildResult.Dispose();
                _graphBuildResult = null;
            }

            // プロファイル再ロード時は Registry / WeightBuffer を Dispose して再構築する (8.2)
            if (_layerUseCase != null)
            {
                _layerUseCase.Dispose();
                _layerUseCase = null;
            }

            _inputSourceFactory = null;
            _expressionUseCase = null;
            _isInitialized = false;
        }

        private static FacialProfile LoadProfileFromSO(FacialProfileSO so)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.JsonFilePath))
            {
                return CreateDefaultProfile();
            }

            var fullPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, so.JsonFilePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning(
                    $"FacialController: プロファイル JSON が見つかりません: {fullPath}。デフォルトプロファイルで初期化します。");
                return CreateDefaultProfile();
            }

            try
            {
                var repository = new FileProfileRepository(new SystemTextJsonParser());
                return repository.LoadProfile(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialController: プロファイル JSON の読み込みに失敗しました: {ex.Message}。デフォルトプロファイルで初期化します。");
                return CreateDefaultProfile();
            }
        }

        private static FacialProfile CreateDefaultProfile()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("lipsync", 1, ExclusionMode.Blend),
                new LayerDefinition("eye", 2, ExclusionMode.LastWins)
            };
            return new FacialProfile("1.0.0", layers);
        }
    }
}
