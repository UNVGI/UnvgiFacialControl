using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem;

namespace Hidano.FacialControl.Adapters.Input
{
    /// <summary>
    /// <see cref="AnalogInputBindingProfileSO"/> に定義されたアナログバインディングを
    /// <see cref="FacialController"/> に結線する MonoBehaviour（Req 7.1〜7.7、tasks.md 6.1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DefaultExecutionOrderAttribute"/> で <c>-50</c> を指定し、<see cref="FacialController.LateUpdate"/>
    /// より早い <see cref="LateUpdate"/> 実行順を確保する（Req 7.5、Flow 1）。
    /// </para>
    /// <para>
    /// OnEnable で profile を Domain に変換し、binding ごとに <see cref="InputActionAnalogSource"/> を
    /// 生成する。<see cref="RegisterExternalSource"/> で OSC 等の外部ソースを併用できる。
    /// BlendShape 側 binding は <see cref="AnalogBlendShapeRegistration"/> を経由して
    /// <see cref="InputSourceFactory"/> に登録する（Req 3.8）。BonePose 側 binding は
    /// <see cref="AnalogBonePoseProvider"/> を直接保持し、<see cref="FacialController"/> の
    /// <see cref="IBonePoseProvider.SetActiveBonePose"/> 経由で 1 frame 1 回投入する（Req 4.5）。
    /// </para>
    /// <para>
    /// 離散トリガー側 <see cref="FacialInputBinder"/> とは独立した別 MonoBehaviour として並走する
    /// （Req 7.6, 9.3）。共有しないリソース（独自 ActionMap "Analog" / 独自 SO）。
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("FacialControl/Facial Analog Input Binder")]
    public sealed class FacialAnalogInputBinder : MonoBehaviour
    {
        [Tooltip("バインディング対象の FacialController。")]
        [SerializeField]
        private FacialController _facialController;

        [Tooltip("アナログバインディング定義を持つ AnalogInputBindingProfileSO。")]
        [SerializeField]
        private AnalogInputBindingProfileSO _profile;

        [Tooltip("InputAction を解決するための InputActionAsset（Analog ActionMap を含む）。")]
        [SerializeField]
        private InputActionAsset _actionAsset;

        [Tooltip("解決対象 ActionMap 名。既定は \"Analog\"。")]
        [SerializeField]
        private string _actionMapName = "Analog";

        private readonly Dictionary<string, IAnalogInputSource> _externalSources =
            new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal);

        private InputActionAsset _runtimeActionAsset;
        private InputActionMap _runtimeActionMap;
        private List<IAnalogInputSource> _ownedSources;
        private Dictionary<string, IAnalogInputSource> _activeSources;
        private AnalogBonePoseProvider _bonePoseProvider;
        private AnalogBlendShapeRegistration _registration;
        private bool _isActive;

        /// <summary>結線対象の <see cref="FacialController"/>。</summary>
        public FacialController FacialController
        {
            get => _facialController;
            set => _facialController = value;
        }

        /// <summary>現在のバインディング Profile（読取用）。</summary>
        public AnalogInputBindingProfileSO Profile => _profile;

        /// <summary>
        /// OSC 等の外部 <see cref="IAnalogInputSource"/> を sourceId 名で登録する。
        /// OnEnable 時に <see cref="InputAction"/> 由来のソース解決に先立って参照される。
        /// </summary>
        /// <param name="sourceId">バインディング JSON で参照される sourceId 文字列。</param>
        /// <param name="source">読出元アダプタ。</param>
        public void RegisterExternalSource(string sourceId, IAnalogInputSource source)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                throw new ArgumentException("sourceId は null/空にできません。", nameof(sourceId));
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            _externalSources[sourceId] = source;
        }

        /// <summary>
        /// 登録済みの外部ソースを除去する。未登録の sourceId は no-op。
        /// </summary>
        public void UnregisterExternalSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return;
            }
            _externalSources.Remove(sourceId);
        }

        /// <summary>
        /// プロファイルを差替えて結線をやり直す（Req 7.4）。
        /// 内部で <see cref="OnDisable"/> 等価 → <see cref="OnEnable"/> 等価を実行する。
        /// </summary>
        /// <param name="profile">新しいバインディングプロファイル（null も許容、その場合 OnDisable のみ走る）。</param>
        public void SetProfile(AnalogInputBindingProfileSO profile)
        {
            Teardown();
            _profile = profile;
            if (isActiveAndEnabled)
            {
                Setup();
            }
        }

        private void OnEnable()
        {
            Setup();
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void LateUpdate()
        {
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
            if (_externalSources.Count > 0)
            {
                foreach (var kv in _externalSources)
                {
                    kv.Value.Tick(dt);
                }
            }

            _bonePoseProvider?.BuildAndPush();
        }

        private void Setup()
        {
            if (_isActive)
            {
                return;
            }

            if (_facialController == null)
            {
                Debug.LogWarning(
                    "FacialAnalogInputBinder: _facialController が未設定のため結線をスキップします。");
                return;
            }

            if (_profile == null)
            {
                Debug.LogWarning(
                    "FacialAnalogInputBinder: _profile が未設定のため結線をスキップします。");
                return;
            }

            AnalogInputBindingProfile domainProfile;
            try
            {
                domainProfile = _profile.ToDomain();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialAnalogInputBinder: profile の解析に失敗したため結線をスキップします: {ex.Message}");
                return;
            }

            var bindings = domainProfile.Bindings;
            int bindingCount = bindings.Length;

            // ActionMap を runtime instance 化（FacialInputBinder と同じ慣習で副作用を分離する）。
            _runtimeActionAsset = null;
            _runtimeActionMap = null;
            if (_actionAsset != null)
            {
                _runtimeActionAsset = Instantiate(_actionAsset);
                _runtimeActionMap = _runtimeActionAsset.FindActionMap(_actionMapName);
                if (_runtimeActionMap == null)
                {
                    Debug.LogWarning(
                        $"FacialAnalogInputBinder: ActionMap '{_actionMapName}' が InputActionAsset に存在しません。");
                }
                else
                {
                    // shape 判定で controls[0] を参照するため、source 生成前に Enable して bindings を解決する。
                    _runtimeActionMap.Enable();
                }
            }

            _ownedSources = new List<IAnalogInputSource>();
            _activeSources = new Dictionary<string, IAnalogInputSource>(StringComparer.Ordinal);

            // sourceId 単位でユニークに sources を構築する。
            // 優先順: 外部登録 (OSC 等) → InputActionMap 上の同名アクション。
            var sourceIdToShape = new Dictionary<string, AnalogInputShape>(StringComparer.Ordinal);
            var bindingsSpan = bindings.Span;
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
                if (_externalSources.TryGetValue(kv.Key, out var external))
                {
                    _activeSources[kv.Key] = external;
                    continue;
                }

                if (_runtimeActionMap == null)
                {
                    Debug.LogWarning(
                        $"FacialAnalogInputBinder: sourceId '{kv.Key}' に対応する外部ソース・InputAction が見つかりませんでした。");
                    continue;
                }

                var action = _runtimeActionMap.FindAction(kv.Key);
                if (action == null)
                {
                    Debug.LogWarning(
                        $"FacialAnalogInputBinder: ActionMap '{_actionMapName}' に Action '{kv.Key}' が見つかりません。");
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
                        $"FacialAnalogInputBinder: sourceId '{kv.Key}' が InputSourceId 規約に合致しません。");
                    continue;
                }

                var inputActionSource = new InputActionAnalogSource(srcId, action, shape);
                _ownedSources.Add(inputActionSource);
                _activeSources[kv.Key] = inputActionSource;
            }

            // BlendShape 側 binding を AnalogBlendShapeRegistration に注入する。
            // FacialController が分離初期化済みであっても、profile.LayerInputSources で
            // analog-blendshape を宣言していなければ TryCreate は呼ばれないので、本注入は次回
            // FC 初期化時に効く。FC 既起動の場合は ReloadProfile を呼んで反映する。
            _registration = _facialController.GetComponent<AnalogBlendShapeRegistration>();
            if (_registration == null)
            {
                _registration = _facialController.gameObject.AddComponent<AnalogBlendShapeRegistration>();
            }

            // BlendShape 専用の bindings リストを抽出する（BonePose は除外）。
            var blendShapeBindings = new List<AnalogBindingEntry>();
            for (int i = 0; i < bindingsSpan.Length; i++)
            {
                var entry = bindingsSpan[i];
                if (entry.TargetKind == AnalogBindingTargetKind.BlendShape)
                {
                    blendShapeBindings.Add(entry);
                }
            }
            _registration.Configure(_activeSources, blendShapeBindings);

            // BlendShape binding が宣言済みなら ReloadProfile を呼んで registration を反映させる。
            if (blendShapeBindings.Count > 0 && _facialController.IsInitialized)
            {
                _facialController.ReloadProfile();
            }

            // BonePose binding を抽出して provider を構築する。
            var bonePoseBindings = new List<AnalogBindingEntry>();
            for (int i = 0; i < bindingsSpan.Length; i++)
            {
                var entry = bindingsSpan[i];
                if (entry.TargetKind == AnalogBindingTargetKind.BonePose)
                {
                    bonePoseBindings.Add(entry);
                }
            }

            if (bonePoseBindings.Count > 0)
            {
                _bonePoseProvider = new AnalogBonePoseProvider(
                    _facialController, _activeSources, bonePoseBindings);
            }

            _isActive = true;
        }

        private void Teardown()
        {
            _isActive = false;

            if (_bonePoseProvider != null)
            {
                _bonePoseProvider.Dispose();
                _bonePoseProvider = null;
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

            if (_registration != null)
            {
                _registration.Clear();
            }

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
        }
    }
}
