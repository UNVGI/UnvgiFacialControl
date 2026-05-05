using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.Adapters.AdapterBindings.InputSystem
{
    /// <summary>
    /// InputSystem 結線（Trigger + Analog + Gaze）を 1 binding に集約した
    /// <see cref="AdapterBindingBase"/> 派生（Req 6.1, 6.8）。
    /// </summary>
    /// <remarks>
    /// 旧 <c>FacialCharacterInputExtension</c> / <c>InputFacialControllerExtension</c> /
    /// <c>FacialCharacterSO</c> / <c>InputRegistration</c> の責務を統合する（D-8）。
    /// <list type="bullet">
    ///   <item><see cref="OnStart"/>: <see cref="InputActionAsset.Instantiate()"/> +
    ///         <see cref="InputActionMap.Enable"/> +
    ///         <see cref="ExpressionInputSourceAdapter"/> の AddComponent +
    ///         primary <see cref="IInputSource"/> 登録 + Analog source 登録 + Gaze provider 構築。</item>
    ///   <item><see cref="OnLateTick"/>: analog Tick + Gaze provider Apply（旧 LateUpdate 相当）。</item>
    ///   <item><see cref="Dispose"/>: ActionMap.Disable + runtime Asset destroy + provider dispose。</item>
    /// </list>
    /// </remarks>
    [Serializable]
    [FacialAdapterBinding(displayName: "Input System")]
    public sealed class InputSystemAdapterBinding : AdapterBindingBase
    {
        [Tooltip("キーアサインを定義する InputActionAsset。Project ウィンドウで作成した .inputactions をここに割り当てる。")]
        [SerializeField]
        private InputActionAsset _inputActionAsset;

        [Tooltip("対象 ActionMap の名前。既定は \"Expression\"。")]
        [SerializeField]
        private string _actionMapName = "Expression";

        [Tooltip("InputAction 名と Expression ID の対応一覧。")]
        [SerializeField]
        private List<ExpressionBindingEntry> _expressionBindings = new List<ExpressionBindingEntry>();

        [Tooltip("Vector2 アナログ表情（目線等）ごとの InputAction + 両目ボーン/BlendShape 設定。")]
        [SerializeField]
        private List<GazeExpressionConfig> _gazeConfigs = new List<GazeExpressionConfig>();

        [NonSerialized] private InputActionAsset _runtimeActionAsset;
        [NonSerialized] private InputActionMap _runtimeActionMap;
        [NonSerialized] private ExpressionTriggerInputSource _triggerSink;
        [NonSerialized] private ExpressionInputSourceAdapter _adapter;
        [NonSerialized] private List<InputActionAnalogSource> _analogSources;
        [NonSerialized] private GazeBonePoseProvider _gazeBoneProvider;
        [NonSerialized] private bool _isStarted;

        /// <summary>キーアサインを定義する InputActionAsset（Inspector / API 用）。</summary>
        public InputActionAsset InputActionAsset
        {
            get => _inputActionAsset;
            set => _inputActionAsset = value;
        }

        /// <summary>対象 ActionMap の名前。</summary>
        public string ActionMapName
        {
            get => _actionMapName;
            set => _actionMapName = value;
        }

        /// <summary><see cref="OnStart"/> 後に解決される runtime ActionAsset（<see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> clone）。</summary>
        public InputActionAsset RuntimeActionAsset => _runtimeActionAsset;

        /// <summary><see cref="OnStart"/> 後に解決される runtime ActionMap。</summary>
        public InputActionMap RuntimeActionMap => _runtimeActionMap;

        /// <summary><see cref="OnStart"/> 完了後 true、<see cref="Dispose"/> 後 false。</summary>
        public bool IsStarted => _isStarted;

        /// <summary>Gaze provider が構築済みかどうか（<see cref="_gazeConfigs"/> が 1 件以上のとき true）。</summary>
        public bool HasGazeProvider => _gazeBoneProvider != null;

        /// <summary>
        /// テスト / プログラムからの構成注入。Inspector 経由で設定される値と同等の効果を持つ。
        /// </summary>
        public void Configure(
            InputActionAsset asset,
            string actionMapName,
            IReadOnlyList<ExpressionBindingEntry> expressionBindings,
            IReadOnlyList<GazeExpressionConfig> gazeConfigs = null)
        {
            _inputActionAsset = asset;
            _actionMapName = actionMapName;
            _expressionBindings = expressionBindings == null
                ? new List<ExpressionBindingEntry>()
                : new List<ExpressionBindingEntry>(expressionBindings);
            _gazeConfigs = gazeConfigs == null
                ? new List<GazeExpressionConfig>()
                : new List<GazeExpressionConfig>(gazeConfigs);
        }

        /// <inheritdoc />
        public override void OnStart(in AdapterBuildContext ctx)
        {
            if (_isStarted)
            {
                return;
            }

            if (_inputActionAsset == null)
            {
                Debug.LogWarning(
                    "[InputSystemAdapterBinding] InputActionAsset が未設定のため OnStart を skip します。");
                return;
            }

            if (!AdapterSlug.TryParse(Slug, out var slug))
            {
                Debug.LogWarning(
                    $"[InputSystemAdapterBinding] Slug '{Slug ?? "<null>"}' が AdapterSlug 規約に合致しません。OnStart を skip します。");
                return;
            }

            _runtimeActionAsset = UnityEngine.Object.Instantiate(_inputActionAsset);
            _runtimeActionMap = _runtimeActionAsset.FindActionMap(_actionMapName);
            if (_runtimeActionMap == null)
            {
                Debug.LogWarning(
                    $"[InputSystemAdapterBinding] ActionMap '{_actionMapName}' が InputActionAsset に存在しません。OnStart を skip します。");
                UnityEngine.Object.Destroy(_runtimeActionAsset);
                _runtimeActionAsset = null;
                return;
            }
            _runtimeActionMap.Enable();

            int blendShapeCount = ctx.BlendShapeNames?.Count ?? 0;
            IReadOnlyList<string> blendShapeNames = ctx.BlendShapeNames ?? Array.Empty<string>();

            _triggerSink = new ExpressionTriggerInputSource(
                id: InputSourceId.Parse(ExpressionTriggerInputSource.InputReservedId),
                blendShapeCount: blendShapeCount,
                maxStackDepth: 16,
                exclusionMode: ExclusionMode.LastWins,
                blendShapeNames: blendShapeNames,
                profile: ctx.Profile);
            ctx.InputSourceRegistry.Register(slug, _triggerSink);

            _adapter = ctx.HostGameObject.AddComponent<ExpressionInputSourceAdapter>();
            _adapter.Initialize(_triggerSink, _triggerSink);

            BindExpressionEntries();

            _analogSources = new List<InputActionAnalogSource>();
            BuildAnalogSources(ctx, slug);

            BuildGazeProvider(ctx);

            _isStarted = true;
        }

        /// <inheritdoc />
        public override void OnLateTick(float deltaTime)
        {
            if (!_isStarted)
            {
                return;
            }

            if (_analogSources != null)
            {
                for (int i = 0; i < _analogSources.Count; i++)
                {
                    _analogSources[i].Tick(deltaTime);
                }
            }

            _gazeBoneProvider?.Apply();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (!_isStarted)
            {
                return;
            }
            _isStarted = false;

            if (_adapter != null)
            {
                _adapter.UnbindAll();
                if (_adapter != null)
                {
                    UnityEngine.Object.Destroy(_adapter);
                }
                _adapter = null;
            }

            if (_gazeBoneProvider != null)
            {
                _gazeBoneProvider.Dispose();
                _gazeBoneProvider = null;
            }

            if (_analogSources != null)
            {
                for (int i = 0; i < _analogSources.Count; i++)
                {
                    _analogSources[i].Dispose();
                }
                _analogSources = null;
            }

            _triggerSink = null;

            if (_runtimeActionMap != null)
            {
                _runtimeActionMap.Disable();
                _runtimeActionMap = null;
            }

            if (_runtimeActionAsset != null)
            {
                UnityEngine.Object.Destroy(_runtimeActionAsset);
                _runtimeActionAsset = null;
            }
        }

        private void BindExpressionEntries()
        {
            if (_expressionBindings == null || _expressionBindings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _expressionBindings.Count; i++)
            {
                var entry = _expressionBindings[i];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.actionName)
                    || string.IsNullOrWhiteSpace(entry.expressionId))
                {
                    continue;
                }

                var action = _runtimeActionMap.FindAction(entry.actionName);
                if (action == null)
                {
                    Debug.LogWarning(
                        $"[InputSystemAdapterBinding] Action '{entry.actionName}' が ActionMap '{_runtimeActionMap.name}' に見つかりません。binding を skip します。");
                    continue;
                }

                _adapter.BindExpression(action, entry.expressionId);
            }
        }

        private void BuildAnalogSources(in AdapterBuildContext ctx, AdapterSlug slug)
        {
            if (_gazeConfigs == null || _gazeConfigs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _gazeConfigs.Count; i++)
            {
                var cfg = _gazeConfigs[i];
                if (cfg == null || cfg.inputAction == null || cfg.inputAction.action == null)
                {
                    continue;
                }
                string actionName = cfg.inputAction.action.name;
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                var action = _runtimeActionMap.FindAction(actionName);
                if (action == null)
                {
                    Debug.LogWarning(
                        $"[InputSystemAdapterBinding] Analog 経路の Action '{actionName}' が ActionMap '{_runtimeActionMap.name}' に見つかりません。skip します。");
                    continue;
                }

                AnalogInputShape shape = DetermineShape(action);

                if (!InputSourceId.TryParse(actionName, out var srcId))
                {
                    Debug.LogWarning(
                        $"[InputSystemAdapterBinding] Action 名 '{actionName}' が InputSourceId 規約に合致しません。analog source の登録を skip します。");
                    continue;
                }

                var src = new InputActionAnalogSource(srcId, action, shape);
                _analogSources.Add(src);
                ctx.InputSourceRegistry.Register(slug, actionName, new AnalogInputSourceWrapper(src));
            }
        }

        private void BuildGazeProvider(in AdapterBuildContext ctx)
        {
            if (_gazeConfigs == null || _gazeConfigs.Count == 0)
            {
                return;
            }

            var resolver = new BoneTransformResolver(ctx.HostGameObject.transform);
            var bindings = new List<GazeBoneBinding>(_gazeConfigs.Count);

            for (int i = 0; i < _gazeConfigs.Count; i++)
            {
                var cfg = _gazeConfigs[i];
                if (cfg == null || cfg.inputAction == null || cfg.inputAction.action == null)
                {
                    continue;
                }
                string actionName = cfg.inputAction.action.name;
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                IAnalogInputSource source = FindAnalogSource(actionName);
                if (source == null)
                {
                    continue;
                }

                bindings.Add(new GazeBoneBinding(cfg, source));
            }

            _gazeBoneProvider = new GazeBonePoseProvider(resolver, bindings);
        }

        private IAnalogInputSource FindAnalogSource(string actionName)
        {
            if (_analogSources == null)
            {
                return null;
            }
            for (int i = 0; i < _analogSources.Count; i++)
            {
                if (string.Equals(_analogSources[i].Id, actionName, StringComparison.Ordinal))
                {
                    return _analogSources[i];
                }
            }
            return null;
        }

        private static AnalogInputShape DetermineShape(InputAction action)
        {
            if (!string.IsNullOrEmpty(action.expectedControlType)
                && string.Equals(action.expectedControlType, "Vector2", StringComparison.OrdinalIgnoreCase))
            {
                return AnalogInputShape.Vector2;
            }

            var controls = action.controls;
            if (controls.Count > 0 && controls[0] is InputControl<UnityEngine.Vector2>)
            {
                return AnalogInputShape.Vector2;
            }

            return AnalogInputShape.Scalar;
        }

        /// <summary>
        /// <see cref="InputActionAnalogSource"/>（<see cref="IAnalogInputSource"/>）を
        /// <see cref="IInputSource"/> contract で <see cref="IInputSourceRegistry"/> に登録するための
        /// 薄いラッパー。Aggregator 経由の BlendShape 出力には寄与せず（<see cref="TryWriteValues"/> は false）、
        /// 解決対象の analog source を <see cref="IAnalogInputSource"/> 利用者（GazeBonePoseProvider 等）に
        /// 引き渡すための識別チャネルとしてのみ機能する。
        /// </summary>
        private sealed class AnalogInputSourceWrapper : IInputSource
        {
            private readonly InputActionAnalogSource _inner;

            public AnalogInputSourceWrapper(InputActionAnalogSource inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public string Id => _inner.Id;

            public InputSourceType Type => InputSourceType.ValueProvider;

            public int BlendShapeCount => 0;

            public void Tick(float deltaTime)
            {
                _inner.Tick(deltaTime);
            }

            public bool TryWriteValues(Span<float> output) => false;
        }
    }
}
