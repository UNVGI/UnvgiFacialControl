using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.Bone;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using DomainInputBinding = Hidano.FacialControl.Domain.Models.InputBinding;
using AdapterInputSystemAdapter = Hidano.FacialControl.Adapters.Input.InputSystemAdapter;

namespace Hidano.FacialControl.InputSystem.Adapters.Input
{
    /// <summary>
    /// 同 GameObject 上の <see cref="FacialController"/> に結線された
    /// <see cref="FacialCharacterSO"/> から入力結線一式 (Expression トリガー結線 +
    /// アナログバインディング登録) を担う MonoBehaviour 拡張。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 旧 <c>FacialInputBinder</c> + <c>FacialAnalogInputBinder</c> + <c>AnalogBlendShapeRegistration</c>
    /// の役割を 1 コンポーネントに統合し、ユーザーは Scene 上で <see cref="FacialController"/>
    /// と本拡張の 2 個だけを置けばよい (将来 Editor 側で自動付与予定)。
    /// </para>
    /// <para>
    /// 1 フレームの責務分担:
    /// <list type="bullet">
    ///   <item><see cref="ConfigureFactory"/>: <see cref="FacialController.Initialize"/> が呼んだ
    ///         <see cref="InputSourceFactory"/> 構築直後に呼ばれる。
    ///         ActionMap Instantiate/Enable + analog sources の構築 + factory への RegisterReserved を行う。</item>
    ///   <item><see cref="OnEnable"/>: Expression トリガー結線を <see cref="AdapterInputSystemAdapter"/>
    ///         経由で行う。device 種別 (Keyboard / Controller) は adapter 側で
    ///         <c>InputAction.bindings</c> から自動推定されるため、本拡張側で category 別に分岐する
    ///         必要はない (Req 7.1, 8.1, tasks.md 4.6)。FC が初期化済みでない場合は
    ///         <see cref="FacialController.OnEnable"/> 後に再エントリされる想定。</item>
    ///   <item><see cref="LateUpdate"/>: analog sources の Tick + BonePose の BuildAndPush。
    ///         <see cref="FacialController.LateUpdate"/> よりも先に評価する必要があるため
    ///         <see cref="DefaultExecutionOrderAttribute"/> で -50 を指定する。</item>
    /// </list>
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("FacialControl/Facial Character Input Extension")]
    public sealed class FacialCharacterInputExtension : MonoBehaviour, IFacialControllerExtension
    {
        [Tooltip("結線対象の FacialController。未指定の場合は同 GameObject から自動取得する。")]
        [SerializeField]
        private FacialController _facialController;

        // ActionMap / sources は ConfigureFactory と OnEnable の双方から参照されるため、
        // どちらが先に呼ばれても整合する形で lazy 初期化する。
        private FacialCharacterSO _resolvedCharacterSO;
        private InputActionAsset _runtimeActionAsset;
        private InputActionMap _runtimeActionMap;
        private List<IAnalogInputSource> _ownedSources;
        private Dictionary<string, IAnalogInputSource> _activeSources;
        private AnalogBonePoseProvider _bonePoseProvider;
        private GazeBonePoseProvider _gazeBoneProvider;
        private AdapterInputSystemAdapter _adapter;
        private bool _inputBound;
        private bool _analogReady;
        private bool _isActive;

        /// <summary>結線対象の <see cref="FacialController"/>。</summary>
        public FacialController FacialController
        {
            get => _facialController;
            set => _facialController = value;
        }

        /// <inheritdoc />
        public void ConfigureFactory(
            InputSourceFactory factory,
            FacialProfile profile,
            IReadOnlyList<string> blendShapeNames)
        {
            if (factory == null)
            {
                return;
            }

            EnsureAnalogReady();

            // 注入前 / 解除済みの呼出は空辞書 + 空 bindings として扱う (no-op 互換)。
            var sources = (IReadOnlyDictionary<string, IAnalogInputSource>)
                (_activeSources ?? new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal));
            var bindings = GetBlendShapeBindings();
            var names = blendShapeNames ?? (IReadOnlyList<string>)Array.Empty<string>();

            factory.RegisterReserved<AnalogBlendShapeOptionsDto>(
                InputSourceId.Parse(AnalogBlendShapeInputSource.ReservedId),
                (options, blendShapeCount, prof) =>
                {
                    return new AnalogBlendShapeInputSource(
                        InputSourceId.Parse(AnalogBlendShapeInputSource.ReservedId),
                        blendShapeCount,
                        names,
                        sources,
                        bindings);
                });
        }

        private void OnEnable()
        {
            if (_facialController == null)
            {
                _facialController = GetComponent<FacialController>();
            }

            EnsureAnalogReady();
            EnsureExpressionBound();

            _isActive = _analogReady || _inputBound;
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void LateUpdate()
        {
            // FacialController.Initialize が profile.json を非同期にロードするため、
            // OnEnable 時点では CurrentProfile が未確定で BindAllExpressions が silently
            // 全 skip されることがある。Profile が ready になるまで毎 LateUpdate で結線を再試行する。
            if (!_inputBound)
            {
                EnsureExpressionBound();
                if (_inputBound)
                {
                    _isActive = true;
                }
            }

            if (!_isActive)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (_ownedSources != null)
            {
                for (int i = 0; i < _ownedSources.Count; i++)
                {
                    _ownedSources[i].Tick(dt);
                }
            }

            _bonePoseProvider?.BuildAndPush();
            _gazeBoneProvider?.Apply();
        }

        // ================================================================
        // Analog 結線 (ConfigureFactory / OnEnable から共通呼び出し)
        // ================================================================

        private void EnsureAnalogReady()
        {
            if (_analogReady)
            {
                return;
            }

            var so = ResolveCharacterSO();
            if (so == null)
            {
                return;
            }

            // 旧 FacialAnalogInputBinder.Setup と同等のフロー。
            AnalogInputBindingProfile domainProfile;
            try
            {
                domainProfile = so.BuildAnalogProfile();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterInputExtension: SO '{so.name}' のアナログプロファイル構築に失敗したため結線をスキップします: {ex.Message}");
                return;
            }

            EnsureRuntimeActionMap(so);

            _ownedSources = new List<IAnalogInputSource>();
            _activeSources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal);

            // sourceId 単位で shape を確定 (scalar / Vector2)。
            var bindingsSpan = domainProfile.Bindings.Span;
            var sourceIdToShape = new Dictionary<string, AnalogInputShape>(StringComparer.Ordinal);
            for (int i = 0; i < bindingsSpan.Length; i++)
            {
                var entry = bindingsSpan[i];
                if (string.IsNullOrEmpty(entry.SourceId))
                {
                    continue;
                }

                AnalogInputShape shape = entry.SourceAxis >= 1
                    ? AnalogInputShape.Vector2
                    : AnalogInputShape.Scalar;
                if (sourceIdToShape.TryGetValue(entry.SourceId, out var existing))
                {
                    if (existing == AnalogInputShape.Scalar && shape == AnalogInputShape.Vector2)
                    {
                        sourceIdToShape[entry.SourceId] = AnalogInputShape.Vector2;
                    }
                }
                else
                {
                    sourceIdToShape[entry.SourceId] = shape;
                }
            }

            foreach (var kv in sourceIdToShape)
            {
                if (_runtimeActionMap == null)
                {
                    Debug.LogWarning(
                        $"FacialCharacterInputExtension: sourceId '{kv.Key}' に対応する InputAction が見つかりませんでした (ActionMap 未解決)。");
                    continue;
                }

                var action = _runtimeActionMap.FindAction(kv.Key);
                if (action == null)
                {
                    // analog binding の sourceId は Expression 用 ActionMap には登録されていないことが
                    // 多い (sourceId は OSC など外部由来の場合もある)。1 件警告に留める。
                    Debug.LogWarning(
                        $"FacialCharacterInputExtension: ActionMap 内に Action '{kv.Key}' が見つかりません。"
                        + " analog source の解決をスキップします。");
                    continue;
                }

                var shape = kv.Value;
                if (!string.IsNullOrEmpty(action.expectedControlType) &&
                    string.Equals(action.expectedControlType, "Vector2", StringComparison.OrdinalIgnoreCase))
                {
                    shape = AnalogInputShape.Vector2;
                }
                else if (action.controls.Count > 0 &&
                         action.controls[0] is global::UnityEngine.InputSystem.InputControl<UnityEngine.Vector2>)
                {
                    shape = AnalogInputShape.Vector2;
                }

                if (!InputSourceId.TryParse(kv.Key, out var srcId))
                {
                    Debug.LogWarning(
                        $"FacialCharacterInputExtension: sourceId '{kv.Key}' が InputSourceId 規約に合致しません。");
                    continue;
                }

                var inputActionSource = new InputActionAnalogSource(srcId, action, shape);
                _ownedSources.Add(inputActionSource);
                _activeSources[kv.Key] = inputActionSource;
            }

            // BonePose binding を抽出して provider を構築する (FC が IBonePoseProvider 実装)。
            var bonePoseBindings = new List<AnalogBindingEntry>();
            for (int i = 0; i < bindingsSpan.Length; i++)
            {
                var entry = bindingsSpan[i];
                if (entry.TargetKind == AnalogBindingTargetKind.BonePose)
                {
                    bonePoseBindings.Add(entry);
                }
            }
            if (bonePoseBindings.Count > 0 && _facialController != null)
            {
                var restPoses = so.GetGazeBoneRestPoses();
                _bonePoseProvider = new AnalogBonePoseProvider(
                    _facialController, _activeSources, bonePoseBindings, restPoses);
            }

            // 目線ボーン専用 provider: GazeConfig 単位で yaw/pitch 軸と可動範囲を反映。
            if (so.GazeConfigs != null && so.GazeConfigs.Count > 0 && _facialController != null)
            {
                var gazeResolver = new BoneTransformResolver(_facialController.transform);
                _gazeBoneProvider = new GazeBonePoseProvider(
                    gazeResolver, _activeSources, so.GazeConfigs);
            }

            _analogReady = true;
        }

        private IReadOnlyList<AnalogBindingEntry> GetBlendShapeBindings()
        {
            var so = ResolveCharacterSO();
            if (so == null)
            {
                return Array.Empty<AnalogBindingEntry>();
            }

            AnalogInputBindingProfile domainProfile;
            try
            {
                domainProfile = so.BuildAnalogProfile();
            }
            catch
            {
                return Array.Empty<AnalogBindingEntry>();
            }

            var bindingsSpan = domainProfile.Bindings.Span;
            var blendShapeBindings = new List<AnalogBindingEntry>();
            for (int i = 0; i < bindingsSpan.Length; i++)
            {
                var entry = bindingsSpan[i];
                if (entry.TargetKind == AnalogBindingTargetKind.BlendShape)
                {
                    blendShapeBindings.Add(entry);
                }
            }
            return blendShapeBindings;
        }

        // ================================================================
        // Expression 結線 (旧 FacialInputBinder.OnEnable の移植)
        // ================================================================

        private void EnsureExpressionBound()
        {
            if (_inputBound)
            {
                return;
            }

            var so = ResolveCharacterSO();
            if (so == null)
            {
                return;
            }

            EnsureRuntimeActionMap(so);
            if (_runtimeActionMap == null)
            {
                return;
            }

            if (_facialController == null)
            {
                Debug.LogWarning(
                    "FacialCharacterInputExtension: _facialController が解決できないため Expression 結線をスキップします。");
                return;
            }

            // FacialController.Initialize は profile.json を非同期にロードする。Profile 未確定
            // の間に BindAllExpressions を呼ぶと ResolveExpression が常に null を返し、全 binding
            // が silent skip された状態で _inputBound = true に固定されてしまう。Profile が
            // ready になるまで結線を保留し、LateUpdate のリトライに任せる。
            if (!_facialController.CurrentProfile.HasValue)
            {
                return;
            }

            if (_adapter == null)
            {
                _adapter = new AdapterInputSystemAdapter(_facialController);
            }

            // device 種別 (Keyboard / Controller) は ExpressionInputSourceAdapter が
            // InputAction.bindings から自動推定するため、ここでは category 別に分岐せず
            // 全 binding を 1 ループで処理する (Req 7.1, 8.1, tasks.md 4.6)。
            BindAllExpressions(so);

            _inputBound = true;
        }

        private void BindAllExpressions(FacialCharacterSO so)
        {
            IReadOnlyList<DomainInputBinding> bindings = so.GetExpressionBindings();
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];

                Expression? expression = ResolveExpression(binding.ExpressionId);
                if (!expression.HasValue)
                {
                    Debug.LogWarning(
                        $"FacialCharacterInputExtension: ExpressionId '{binding.ExpressionId}' が "
                        + "FacialController のプロファイルに存在しないためバインディングをスキップします。");
                    continue;
                }

                var action = _runtimeActionMap.FindAction(binding.ActionName);
                if (action == null)
                {
                    Debug.LogWarning(
                        $"FacialCharacterInputExtension: Action '{binding.ActionName}' が "
                        + $"ActionMap '{_runtimeActionMap.name}' に見つかりません。");
                    continue;
                }

                _adapter.BindExpression(action, expression.Value, binding.TriggerMode);
            }
        }

        // ================================================================
        // 共通ヘルパー
        // ================================================================

        private FacialCharacterSO ResolveCharacterSO()
        {
            if (_resolvedCharacterSO != null)
            {
                return _resolvedCharacterSO;
            }

            if (_facialController == null)
            {
                _facialController = GetComponent<FacialController>();
            }
            if (_facialController == null)
            {
                return null;
            }

            // FacialCharacterProfileSO 抽象基底は InputActionAsset を持たないため、
            // 入力結線が成立するのは具象 FacialCharacterSO に降格できる場合のみ。
            _resolvedCharacterSO = _facialController.CharacterSO as FacialCharacterSO;
            return _resolvedCharacterSO;
        }

        private void EnsureRuntimeActionMap(FacialCharacterSO so)
        {
            if (_runtimeActionMap != null)
            {
                return;
            }

            var sourceAsset = so?.InputActionAsset;
            if (sourceAsset == null)
            {
                Debug.LogWarning(
                    "FacialCharacterInputExtension: SO の InputActionAsset が未設定のため ActionMap 解決をスキップします。");
                return;
            }

            _runtimeActionAsset = Instantiate(sourceAsset);
            _runtimeActionMap = _runtimeActionAsset.FindActionMap(so.ActionMapName);
            if (_runtimeActionMap == null)
            {
                Debug.LogWarning(
                    $"FacialCharacterInputExtension: ActionMap '{so.ActionMapName}' が InputActionAsset に存在しません。");
                return;
            }

            _runtimeActionMap.Enable();
        }

        private Expression? ResolveExpression(string expressionId)
        {
            if (_facialController == null)
            {
                return null;
            }

            var profile = _facialController.CurrentProfile;
            if (!profile.HasValue)
            {
                return null;
            }

            return profile.Value.FindExpressionById(expressionId);
        }

        private void Teardown()
        {
            _isActive = false;

            if (_adapter != null)
            {
                _adapter.UnbindAll();
                _adapter.Dispose();
                _adapter = null;
            }

            if (_bonePoseProvider != null)
            {
                _bonePoseProvider.Dispose();
                _bonePoseProvider = null;
            }

            if (_gazeBoneProvider != null)
            {
                _gazeBoneProvider.Dispose();
                _gazeBoneProvider = null;
            }

            if (_ownedSources != null)
            {
                for (int i = 0; i < _ownedSources.Count; i++)
                {
                    if (_ownedSources[i] is IDisposable d)
                    {
                        d.Dispose();
                    }
                }
                _ownedSources = null;
            }

            _activeSources = null;

            if (_runtimeActionMap != null)
            {
                _runtimeActionMap.Disable();
                _runtimeActionMap = null;
            }

            if (_runtimeActionAsset != null)
            {
                Destroy(_runtimeActionAsset);
                _runtimeActionAsset = null;
            }

            _resolvedCharacterSO = null;
            _inputBound = false;
            _analogReady = false;
        }
    }
}
