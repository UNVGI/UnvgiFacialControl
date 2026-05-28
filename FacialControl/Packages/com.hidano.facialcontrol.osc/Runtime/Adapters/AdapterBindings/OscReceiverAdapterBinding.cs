using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.RuntimeSettings;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.AdapterBindings
{
    /// <summary>
    /// OSC 結線を 1 binding に集約した <see cref="AdapterBindingBase"/> 具象。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OnStart"/> で <c>ctx.HostGameObject.AddComponent&lt;OscReceiverHost&gt;()</c> を実行し、
    /// helper を <see cref="OscReceiverHost.Configure"/> で構成 → <see cref="OscInputSource"/> を構築 →
    /// <see cref="IInputSourceRegistry.Register(AdapterSlug, Hidano.FacialControl.Domain.Interfaces.IInputSource)"/>
    /// で primary 入力源として登録する（D-3, D-11）。
    /// </para>
    /// <para>
    /// <see cref="OnFixedTick"/> で <see cref="OscDoubleBuffer.Swap"/> を呼び、受信スレッドが書き込んだ値を
    /// 次フレームの読取バッファに反映する（既存 <c>OscReceiver</c> の MonoBehaviour Update 経路に依存しない自前 tick 化）。
    /// </para>
    /// <para>
    /// <see cref="Dispose"/> で <c>Object.Destroy(_helperHost)</c> → <c>OscDoubleBuffer.Dispose()</c> の順で解放する。
    /// </para>
    /// </remarks>
    [Serializable]
    [FacialAdapterBinding(displayName: "OSC Receiver")]
    public sealed class OscReceiverAdapterBinding : AdapterBindingBase
    {
        public enum MappingOrigin
        {
            Manual,
            HeartbeatAuto
        }

        public const string SenderIdentityAddress = SenderIdentity.OscAddress;
        public const string BlendShapeNamesAddress = "/_facialcontrol/blendshape_names";

        private const int MaxCachedBundleSenderDecisions = 32;

        /// <summary>
        /// 環境/運用依存の Receiver 設定を保持する SettingsSO (sub-asset)。
        /// Inspector / CollectionSO 経由で割り当てられる本番経路。
        /// </summary>
        [SerializeField]
        private OscRuntimeSettingsSO _settings;

        [SerializeField]
        private List<OscMappingEntry> _mappings = new List<OscMappingEntry>();

        /// <summary>
        /// 診断/テスト経路。Inspector で <see cref="_settings"/> を割り当てない代わりに
        /// プロパティ setter や <see cref="Configure"/> から値を流し込むと on-demand で生成され、
        /// <see cref="OnStart"/> で <see cref="_settings"/> のフォールバックとして採用される。
        /// </summary>
        [NonSerialized]
        private OscRuntimeSettingsSO _runtimeSettings;

        /// <summary>
        /// <see cref="OnStart"/> で確定した有効な Settings 参照。<see cref="OnFixedTick"/> 等の
        /// 読み出しは本フィールドを介して行い、起動後の SO 参照差し替えに左右されないようにする。
        /// </summary>
        [NonSerialized]
        private OscRuntimeSettingsSO _effectiveSettings;

        [NonSerialized]
        private OscMapping[] _runtimeMappings;

        [NonSerialized]
        private OscReceiverHost _helperHost;

        [NonSerialized]
        private OscDoubleBuffer _buffer;

        [NonSerialized]
        private OscBundleAccumulator _bundleAccumulator;

        [NonSerialized]
        private OscInputSource _inputSource;

        [NonSerialized]
        private List<GazeVector2InputSource> _gazeSources;

        [NonSerialized]
        private List<GazeRuntimeEntry> _gazeRuntimeEntries;

        [NonSerialized]
        private Dictionary<string, List<GazeRoute>> _gazeRoutes;

        [NonSerialized]
        private object _gazeBundleSync;

        [NonSerialized]
        private Queue<List<GazeSample>> _readyGazeFrames;

        [NonSerialized]
        private List<GazeSample> _currentGazeBundleValues;

        [NonSerialized]
        private List<GazeSample> _bareGazeValues;

        [NonSerialized]
        private ulong _currentGazeTimestampKey;

        [NonSerialized]
        private double _currentGazeBundleFirstReceivedAtSeconds;

        [NonSerialized]
        private bool _hasCurrentGazeBundle;

        [NonSerialized]
        private HashSet<string> _normalAddresses;

        [NonSerialized]
        private HashSet<string> _normalBlendShapeNames;

        [NonSerialized]
        private HeartbeatConsistencyChecker _heartbeatChecker;

        [NonSerialized]
        private ZombieEvictionPolicy _zombiePolicy;

        [NonSerialized]
        private Dictionary<ulong, bool> _bundleSenderDecisions;

        [NonSerialized]
        private Queue<ulong> _bundleSenderDecisionOrder;

        [NonSerialized]
        private List<string> _heartbeatScratch;

        [NonSerialized]
        private ITimeProvider _timeProvider;

        [NonSerialized]
        private SenderIdentity _currentSenderId;

        [NonSerialized]
        private double _lastAcceptedPacketTime;

        [NonSerialized]
        private bool _hasCurrentSenderId;

        [NonSerialized]
        private bool _hasBareSenderDecision;

        [NonSerialized]
        private bool _bareSenderAccepted;

        [NonSerialized]
        private bool _failSafeActive;

        [NonSerialized]
        private bool _started;

        /// <summary>
        /// パラメータレスコンストラクタ。Inspector の Add ドロップダウンで <c>Activator.CreateInstance</c> から
        /// 生成される必要があるため明示する。
        /// </summary>
        public OscReceiverAdapterBinding()
        {
        }

        /// <summary>
        /// Inspector で割り当てられた <see cref="OscRuntimeSettingsSO"/>。診断 setter / Configure 経由で
        /// 値を流し込む場合は <see cref="_runtimeSettings"/> が代わりに使われる。
        /// </summary>
        public OscRuntimeSettingsSO Settings
        {
            get => _settings;
            set => _settings = value;
        }

        /// <summary>有効な Settings 参照を返す。<see cref="Settings"/> が未代入なら診断用 runtime SO にフォールバック。</summary>
        public OscRuntimeSettingsSO EffectiveSettings =>
            _settings != null ? _settings : _runtimeSettings;

        /// <summary>送信元エンドポイント（IP/host）。現状 uOSC は port のみ使用するが診断用に保持。</summary>
        public string Endpoint
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.ListenEndpoint : OscRuntimeSettingsSO.DefaultListenEndpoint;
            }
            set => EnsureRuntimeSettings().SetListenEndpoint(value);
        }

        /// <summary>受信 UDP ポート。</summary>
        public int Port
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.ListenPort : OscConfiguration.DefaultReceivePort;
            }
            set => EnsureRuntimeSettings().SetListenPort(value);
        }

        /// <summary>staleness 判定秒数（0 で staleness 無効）。</summary>
        public float StalenessSeconds
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.StalenessSeconds : 0f;
            }
            set => EnsureRuntimeSettings().SetStalenessSeconds(value);
        }

        public List<OscMappingEntry> Mappings
        {
            get => _mappings;
            set => _mappings = value ?? new List<OscMappingEntry>();
        }

        public FailSafeMode FailSafeMode
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.FailSafeMode : FailSafeMode.RevertToBase;
            }
            set => EnsureRuntimeSettings().SetFailSafeMode(value);
        }

        public bool ConsistencyCheckWarnLog
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.ConsistencyCheckWarnLog : true;
            }
            set => EnsureRuntimeSettings().SetConsistencyCheckWarnLog(value);
        }

        public BundleInterpretationMode BundleMode
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null ? settings.BundleMode : BundleInterpretationMode.AtomicSwap;
            }
            set => EnsureRuntimeSettings().SetBundleMode(value);
        }

        public float BundleAccumulationTimeoutMs
        {
            get
            {
                OscRuntimeSettingsSO settings = EffectiveSettings;
                return settings != null
                    ? settings.BundleAccumulationTimeoutMs
                    : OscRuntimeSettingsSO.DefaultBundleAccumulationTimeoutMs;
            }
            set
            {
                EnsureRuntimeSettings().SetBundleAccumulationTimeoutMs(value);
                if (_bundleAccumulator != null)
                {
                    _bundleAccumulator.BundleAccumulationTimeoutMs = value;
                }
            }
        }

        /// <summary>OnStart で確保した helper MonoBehaviour（テスト/診断用、未開始は null）。</summary>
        public OscReceiverHost HelperHost => _helperHost;

        public OscDoubleBuffer Buffer => _buffer;

        public OscBundleAccumulator BundleAccumulator => _bundleAccumulator;

        public HeartbeatConsistencyChecker HeartbeatChecker => _heartbeatChecker;

        public ZombieEvictionPolicy ZombiePolicy => _zombiePolicy;

        public SenderIdentity? CurrentSenderId =>
            _hasCurrentSenderId ? _currentSenderId : (SenderIdentity?)null;

        public IReadOnlyList<GazeVector2InputSource> GazeSources =>
            _gazeSources ?? (IReadOnlyList<GazeVector2InputSource>)Array.Empty<GazeVector2InputSource>();

        /// <summary>OnStart で構築した <see cref="OscInputSource"/>（テスト/診断用、未開始は null）。</summary>
        public OscInputSource InputSource => _inputSource;

        /// <summary>OnStart 済みかどうか。</summary>
        public bool IsStarted => _started;

        /// <summary>
        /// Runtime / テストから endpoint・port・mappings をまとめて設定する。
        /// </summary>
        /// <remarks>
        /// design.md の "Configure for diagnostic" 仕様に従い、内部 runtime SO に値を書き込む形で
        /// <see cref="_settings"/> 未代入時の診断パスを保持する。
        /// </remarks>
        public void Configure(string endpoint, int port, OscMapping[] mappings)
        {
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            OscRuntimeSettingsSO settings = EnsureRuntimeSettings();
            settings.SetListenEndpoint(endpoint);
            settings.SetListenPort(port);
            _runtimeMappings = mappings;
        }

        /// <summary>
        /// 明示的に <see cref="OscRuntimeSettingsSO"/> インスタンスと mappings を流し込む診断 API。
        /// テストで sub-asset を経由せずに SettingsSO 経路をそのまま検証するために使用する。
        /// </summary>
        public void Configure(OscRuntimeSettingsSO settings, OscMapping[] mappings)
        {
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            _settings = settings;
            _runtimeMappings = mappings;
        }

        private OscRuntimeSettingsSO EnsureRuntimeSettings()
        {
            if (_runtimeSettings == null)
            {
                // FQN で UnityEngine.ScriptableObject を指定する。Adapters 配下に同名の
                // namespace (Hidano.FacialControl.Adapters.ScriptableObject) が存在するため
                // 短縮形だと CS0234 で解決失敗するのを回避する。
                _runtimeSettings = UnityEngine.ScriptableObject.CreateInstance<OscRuntimeSettingsSO>();
                _runtimeSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            return _runtimeSettings;
        }

        /// <inheritdoc />
        public override void OnStart(in AdapterBuildContext ctx)
        {
            if (_started)
            {
                return;
            }

            if (ctx.HostGameObject == null)
            {
                Debug.LogError("[OscReceiverAdapterBinding] HostGameObject が null のため OSC binding を起動できません。");
                return;
            }

            OscRuntimeSettingsSO settings = EffectiveSettings;
            if (settings == null)
            {
                Debug.LogWarning(
                    $"[OscReceiverAdapterBinding] _settings が未代入のため OSC Adapter は起動しません。slug='{Slug}'");
                return;
            }
            if (!settings.ReceiverEnabled)
            {
                Debug.LogWarning(
                    $"[OscReceiverAdapterBinding] _settings.ReceiverEnabled=false のため OSC Adapter は起動しません。slug='{Slug}'");
                return;
            }

            if (_mappings == null)
            {
                _mappings = new List<OscMappingEntry>();
            }

            OscMapping[] runtimeMappings = _runtimeMappings ?? CreateNormalBlendShapeMappings(_mappings);
            bool hasBlendShapeMappings = runtimeMappings != null && runtimeMappings.Length > 0;
            bool hasGazeMappings = HasGazeMappings(_mappings);
            if (!hasBlendShapeMappings && !hasGazeMappings)
            {
                Debug.LogWarning(
                    $"[OscReceiverAdapterBinding] OSC mappings が未設定のため入力源を登録しません。slug='{Slug}'");
                return;
            }

            if (!AdapterSlug.TryParse(Slug, out var slug))
            {
                Debug.LogError(
                    $"[OscReceiverAdapterBinding] Slug '{Slug}' が AdapterSlug 規約を満たしません。InputSourceRegistry に登録できません。");
                return;
            }

            _effectiveSettings = settings;
            _timeProvider = ctx.TimeProvider;
            _lastAcceptedPacketTime = ctx.TimeProvider.UnscaledTimeSeconds;
            _failSafeActive = false;
            _zombiePolicy = new ZombieEvictionPolicy();
            _bundleSenderDecisions = new Dictionary<ulong, bool>();
            _bundleSenderDecisionOrder = new Queue<ulong>();
            _heartbeatScratch = new List<string>();
            IReadOnlyList<string> meshBlendShapeNames = ResolveMeshBlendShapeNames(ctx.BlendShapeNames, runtimeMappings);
            int[] mappingIndexToMeshIndex = hasBlendShapeMappings
                ? BuildMappingIndexToMeshIndex(meshBlendShapeNames, runtimeMappings)
                : Array.Empty<int>();

            BuildNormalLookup(runtimeMappings);
            _heartbeatChecker = hasBlendShapeMappings
                ? new HeartbeatConsistencyChecker(meshBlendShapeNames, runtimeMappings, settings.ConsistencyCheckWarnLog)
                : null;

            if (hasGazeMappings)
            {
                InitializeGazeBundleState();
                RegisterGazeSources(ctx.InputSourceRegistry, slug, _mappings);
            }

            _buffer = new OscDoubleBuffer(runtimeMappings.Length);
            _bundleAccumulator = new OscBundleAccumulator(_buffer, settings.BundleAccumulationTimeoutMs);

            _helperHost = ctx.HostGameObject.AddComponent<OscReceiverHost>();
            _helperHost.Configure(
                settings.ListenEndpoint,
                settings.ListenPort,
                _buffer,
                runtimeMappings,
                settings.BundleMode == BundleInterpretationMode.AtomicSwap ? _bundleAccumulator : null,
                settings.BundleMode,
                ctx.TimeProvider);

            if (_helperHost.Receiver != null)
            {
                _helperHost.Receiver.SetMessageFilter(HandleIncomingOscMessage);
            }

            if (hasBlendShapeMappings)
            {
                _inputSource = new OscInputSource(
                    _buffer,
                    settings.StalenessSeconds,
                    ctx.TimeProvider,
                    settings.FailSafeMode,
                    null,
                    null,
                    mappingIndexToMeshIndex);
                ctx.InputSourceRegistry.Register(slug, _inputSource);
            }

            _started = true;
        }

        /// <inheritdoc />
        public override void OnFixedTick(float fixedDeltaTime)
        {
            if (!_started)
            {
                return;
            }

            // 受信スレッドが write バッファに積んだ値を read バッファに切り替える。
            // OscReceiver の Update / 個別タイマに依存せず binding 自前 tick で進める。
            if (_helperHost != null)
            {
                _helperHost.Tick();
            }

            PublishGazeForCurrentLifecycleState();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_helperHost != null)
            {
                if (_helperHost.Receiver != null)
                {
                    _helperHost.Receiver.SetMessageFilter(null);
                }

                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_helperHost);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_helperHost);
                }
                _helperHost = null;
            }

            _inputSource = null;
            _gazeSources = null;
            _gazeRuntimeEntries = null;
            _gazeRoutes = null;
            ClearGazeBundleState();
            _gazeBundleSync = null;
            _readyGazeFrames = null;
            _currentGazeBundleValues = null;
            _bareGazeValues = null;
            _currentGazeTimestampKey = 0UL;
            _currentGazeBundleFirstReceivedAtSeconds = 0d;
            _hasCurrentGazeBundle = false;
            _normalAddresses = null;
            _normalBlendShapeNames = null;
            _heartbeatChecker = null;
            _zombiePolicy = null;
            _bundleSenderDecisions = null;
            _bundleSenderDecisionOrder = null;
            _heartbeatScratch = null;
            _timeProvider = null;
            _currentSenderId = default;
            _hasCurrentSenderId = false;
            _hasBareSenderDecision = false;
            _bareSenderAccepted = false;
            _lastAcceptedPacketTime = 0d;
            _failSafeActive = false;

            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }

            _bundleAccumulator = null;
            _effectiveSettings = null;

            _started = false;
        }

        private static OscMapping[] CreateNormalBlendShapeMappings(List<OscMappingEntry> mappings)
        {
            if (mappings == null || mappings.Count == 0)
            {
                return Array.Empty<OscMapping>();
            }

            var result = new List<OscMapping>(mappings.Count);
            for (int i = 0; i < mappings.Count; i++)
            {
                OscMappingEntry entry = mappings[i];
                if (entry == null || entry.mode != OscMappingMode.Normal_BlendShape)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.expressionId) || string.IsNullOrEmpty(entry.addressPattern))
                {
                    continue;
                }

                result.Add(new OscMapping(entry.addressPattern, entry.expressionId, string.Empty));
            }

            return result.ToArray();
        }

        private void RegisterGazeSources(
            IInputSourceRegistry registry,
            AdapterSlug slug,
            List<OscMappingEntry> mappings)
        {
            _gazeSources = _gazeSources ?? new List<GazeVector2InputSource>();
            _gazeRuntimeEntries = _gazeRuntimeEntries ?? new List<GazeRuntimeEntry>();
            _gazeRoutes = _gazeRoutes ?? new Dictionary<string, List<GazeRoute>>(StringComparer.Ordinal);
            _gazeSources.Clear();
            _gazeRuntimeEntries.Clear();
            _gazeRoutes.Clear();

            for (int i = 0; i < mappings.Count; i++)
            {
                OscMappingEntry entry = mappings[i];
                if (entry == null || !IsGazeMode(entry.mode) || string.IsNullOrEmpty(entry.expressionId))
                {
                    continue;
                }

                if (entry.leftRightIndependent &&
                    (string.IsNullOrEmpty(entry.sourceIdLeft) || string.IsNullOrEmpty(entry.sourceIdRight)))
                {
                    Debug.LogWarning(
                        "[OscReceiverAdapterBinding] leftRightIndependent=true の Gaze entry は "
                        + $"sourceIdLeft/sourceIdRight が必須です。expressionId='{entry.expressionId}' をスキップします。");
                    continue;
                }

                if (entry.mode == OscMappingMode.Gaze_VRChat_XY && string.IsNullOrEmpty(entry.addressPattern))
                {
                    Debug.LogWarning(
                        $"[OscReceiverAdapterBinding] Gaze_VRChat_XY entry '{entry.expressionId}' の addressPattern が空のためスキップします。");
                    continue;
                }

                var runtime = new GazeRuntimeEntry(entry.mode);
                if (entry.mode == OscMappingMode.Gaze_ARKit_8BS || entry.leftRightIndependent)
                {
                    runtime.LeftSource = RegisterGazeSource(registry, slug, entry.expressionId + ".left");
                    runtime.RightSource = RegisterGazeSource(registry, slug, entry.expressionId + ".right");
                }
                else
                {
                    runtime.CommonSource = RegisterGazeSource(registry, slug, entry.expressionId);
                }

                if (!runtime.HasAnySource)
                {
                    continue;
                }

                _gazeRuntimeEntries.Add(runtime);
                RegisterGazeRoutes(entry, runtime);
            }
        }

        private GazeVector2InputSource RegisterGazeSource(
            IInputSourceRegistry registry,
            AdapterSlug slug,
            string sub)
        {
            string id = slug.Value + ":" + sub;
            if (!InputSourceId.TryParse(id, out InputSourceId sourceId))
            {
                Debug.LogWarning(
                    $"[OscReceiverAdapterBinding] Gaze source id '{id}' is not a valid InputSourceId. Skipping.");
                return null;
            }

            var source = new GazeVector2InputSource(sourceId);
            registry.Register(slug, sub, source);
            _gazeSources.Add(source);
            return source;
        }

        private void RegisterGazeRoutes(OscMappingEntry entry, GazeRuntimeEntry runtime)
        {
            if (entry.mode == OscMappingMode.Gaze_VRChat_XY)
            {
                AddGazeRoute(entry.addressPattern + "X", runtime, GazeRuntimeEntry.VrChatXIndex);
                AddGazeRoute(entry.addressPattern + "Y", runtime, GazeRuntimeEntry.VrChatYIndex);
                return;
            }

            for (int i = 0; i < PerfectSyncEyeLook.Count; i++)
            {
                AddGazeRoute(PerfectSyncEyeLook.ArKitAddressPrefix + PerfectSyncEyeLook.Names[i], runtime, i);
            }
        }

        private void AddGazeRoute(string address, GazeRuntimeEntry runtime, int axisIndex)
        {
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            if (!_gazeRoutes.TryGetValue(address, out var routes))
            {
                routes = new List<GazeRoute>();
                _gazeRoutes.Add(address, routes);
            }

            routes.Add(new GazeRoute(runtime, axisIndex));
        }

        private void InitializeGazeBundleState()
        {
            _gazeBundleSync = new object();
            _readyGazeFrames = new Queue<List<GazeSample>>();
            _currentGazeBundleValues = new List<GazeSample>();
            _bareGazeValues = new List<GazeSample>();
            _currentGazeTimestampKey = 0UL;
            _currentGazeBundleFirstReceivedAtSeconds = 0d;
            _hasCurrentGazeBundle = false;
        }

        private void ClearGazeBundleState()
        {
            if (_gazeBundleSync == null)
            {
                return;
            }

            lock (_gazeBundleSync)
            {
                _readyGazeFrames?.Clear();
                _currentGazeBundleValues?.Clear();
                _bareGazeValues?.Clear();
                _currentGazeTimestampKey = 0UL;
                _currentGazeBundleFirstReceivedAtSeconds = 0d;
                _hasCurrentGazeBundle = false;
            }
        }

        private bool HandleIncomingOscMessage(uOSC.Message message)
        {
            if (message.address == SenderIdentityAddress)
            {
                HandleSenderIdentityMessage(message);
                return false;
            }

            if (!IsAcceptedSenderMessage(message))
            {
                return false;
            }

            if (message.address == BlendShapeNamesAddress)
            {
                HandleHeartbeatMessage(message);
                return false;
            }

            bool handledGaze = TryHandleGazeMessage(message);
            if (handledGaze || IsKnownNormalBlendShapeMessage(message.address))
            {
                MarkAcceptedPacket();
            }

            return true;
        }

        private void HandleSenderIdentityMessage(uOSC.Message message)
        {
            if (!TryParseSenderIdentity(message.values, out SenderIdentity identity))
            {
                Debug.LogWarning("[OscReceiverAdapterBinding] sender_id message の payload を解釈できません。");
                return;
            }

            if (_zombiePolicy == null)
            {
                _zombiePolicy = new ZombieEvictionPolicy();
            }

            bool accepted = _zombiePolicy.Observe(identity);
            if (_zombiePolicy.HasCurrentSender)
            {
                _currentSenderId = _zombiePolicy.CurrentSender;
                _hasCurrentSenderId = true;
            }

            ulong timestampKey = message.timestamp.value;
            if (OscBundleAccumulator.IsBundleTimestamp(timestampKey))
            {
                RememberBundleSenderDecision(timestampKey, accepted);
            }
            else
            {
                _hasBareSenderDecision = true;
                _bareSenderAccepted = accepted;
            }
        }

        private bool IsAcceptedSenderMessage(uOSC.Message message)
        {
            ulong timestampKey = message.timestamp.value;
            if (OscBundleAccumulator.IsBundleTimestamp(timestampKey) &&
                _bundleSenderDecisions != null &&
                _bundleSenderDecisions.TryGetValue(timestampKey, out bool accepted))
            {
                return accepted;
            }

            if (!OscBundleAccumulator.IsBundleTimestamp(timestampKey) && _hasBareSenderDecision)
            {
                return _bareSenderAccepted;
            }

            return true;
        }

        private void RememberBundleSenderDecision(ulong timestampKey, bool accepted)
        {
            if (_bundleSenderDecisions == null)
            {
                _bundleSenderDecisions = new Dictionary<ulong, bool>();
                _bundleSenderDecisionOrder = new Queue<ulong>();
            }

            if (!_bundleSenderDecisions.ContainsKey(timestampKey))
            {
                _bundleSenderDecisionOrder.Enqueue(timestampKey);
            }

            _bundleSenderDecisions[timestampKey] = accepted;
            while (_bundleSenderDecisionOrder.Count > MaxCachedBundleSenderDecisions)
            {
                ulong old = _bundleSenderDecisionOrder.Dequeue();
                _bundleSenderDecisions.Remove(old);
            }
        }

        private void HandleHeartbeatMessage(uOSC.Message message)
        {
            if (_heartbeatChecker == null || message.values == null)
            {
                return;
            }

            _heartbeatScratch.Clear();
            for (int i = 0; i < message.values.Length; i++)
            {
                if (message.values[i] is string name && !string.IsNullOrEmpty(name))
                {
                    _heartbeatScratch.Add(name);
                }
            }

            _heartbeatChecker.UpdateFromHeartbeat(_heartbeatScratch);
        }

        private bool TryHandleGazeMessage(uOSC.Message message)
        {
            if (_gazeRoutes == null ||
                !_gazeRoutes.TryGetValue(message.address, out var routes) ||
                !TryGetFloat(message, out float value))
            {
                return false;
            }

            BundleInterpretationMode currentBundleMode = _effectiveSettings != null
                ? _effectiveSettings.BundleMode
                : BundleInterpretationMode.AtomicSwap;
            for (int i = 0; i < routes.Count; i++)
            {
                if (currentBundleMode == BundleInterpretationMode.AtomicSwap)
                {
                    RecordBufferedGazeMessage(message, routes[i], value);
                }
                else
                {
                    routes[i].Runtime.Record(routes[i].AxisIndex, value);
                }
            }

            return true;
        }

        private void RecordBufferedGazeMessage(uOSC.Message message, GazeRoute route, float value)
        {
            if (_gazeBundleSync == null)
            {
                route.Runtime.Record(route.AxisIndex, value);
                return;
            }

            ulong timestampKey = message.timestamp.value;
            double receivedAtSeconds = GetCurrentTimeSeconds();
            lock (_gazeBundleSync)
            {
                if (OscBundleAccumulator.IsBundleTimestamp(timestampKey))
                {
                    RecordGazeBundleMessageLocked(timestampKey, route, value, receivedAtSeconds);
                }
                else
                {
                    RecordBareGazeMessageLocked(route, value);
                }
            }
        }

        private void RecordGazeBundleMessageLocked(
            ulong timestampKey,
            GazeRoute route,
            float value,
            double receivedAtSeconds)
        {
            CompleteBareGazeMessagesLocked();

            if (!_hasCurrentGazeBundle)
            {
                StartGazeBundleLocked(timestampKey, receivedAtSeconds);
            }
            else if (_currentGazeTimestampKey != timestampKey)
            {
                CompleteCurrentGazeBundleLocked();
                StartGazeBundleLocked(timestampKey, receivedAtSeconds);
            }

            _currentGazeBundleValues.Add(new GazeSample(route.Runtime, route.AxisIndex, value));
        }

        private void RecordBareGazeMessageLocked(GazeRoute route, float value)
        {
            CompleteCurrentGazeBundleLocked();
            _bareGazeValues.Add(new GazeSample(route.Runtime, route.AxisIndex, value));
        }

        private void FlushBufferedGazeMessages(double nowSeconds)
        {
            BundleInterpretationMode currentBundleMode = _effectiveSettings != null
                ? _effectiveSettings.BundleMode
                : BundleInterpretationMode.AtomicSwap;
            if (currentBundleMode != BundleInterpretationMode.AtomicSwap || _gazeBundleSync == null)
            {
                return;
            }

            int frameCount = 0;
            while (true)
            {
                List<GazeSample> frame;
                lock (_gazeBundleSync)
                {
                    if (frameCount == 0)
                    {
                        if (IsCurrentGazeBundleTimedOutLocked(nowSeconds))
                        {
                            CompleteCurrentGazeBundleLocked();
                        }

                        CompleteBareGazeMessagesLocked();
                    }

                    if (_readyGazeFrames.Count == 0)
                    {
                        return;
                    }

                    frame = _readyGazeFrames.Dequeue();
                }

                ApplyGazeFrame(frame);
                frameCount++;
            }
        }

        private void StartGazeBundleLocked(ulong timestampKey, double receivedAtSeconds)
        {
            _currentGazeTimestampKey = timestampKey;
            _currentGazeBundleFirstReceivedAtSeconds = receivedAtSeconds;
            _hasCurrentGazeBundle = true;
        }

        private bool IsCurrentGazeBundleTimedOutLocked(double nowSeconds)
        {
            if (!_hasCurrentGazeBundle)
            {
                return false;
            }

            float timeoutMs = _effectiveSettings != null
                ? _effectiveSettings.BundleAccumulationTimeoutMs
                : OscRuntimeSettingsSO.DefaultBundleAccumulationTimeoutMs;
            double timeoutSeconds = timeoutMs * 0.001d;
            return nowSeconds - _currentGazeBundleFirstReceivedAtSeconds >= timeoutSeconds;
        }

        private void CompleteCurrentGazeBundleLocked()
        {
            if (!_hasCurrentGazeBundle)
            {
                return;
            }

            if (_currentGazeBundleValues.Count > 0)
            {
                _readyGazeFrames.Enqueue(_currentGazeBundleValues);
                _currentGazeBundleValues = new List<GazeSample>(_currentGazeBundleValues.Count);
            }

            _currentGazeTimestampKey = 0UL;
            _currentGazeBundleFirstReceivedAtSeconds = 0d;
            _hasCurrentGazeBundle = false;
        }

        private void CompleteBareGazeMessagesLocked()
        {
            if (_bareGazeValues.Count == 0)
            {
                return;
            }

            _readyGazeFrames.Enqueue(_bareGazeValues);
            _bareGazeValues = new List<GazeSample>(_bareGazeValues.Count);
        }

        private void ApplyGazeFrame(List<GazeSample> frame)
        {
            for (int i = 0; i < frame.Count; i++)
            {
                GazeSample sample = frame[i];
                sample.Runtime.Record(sample.AxisIndex, sample.Value);
            }
        }

        private double GetCurrentTimeSeconds()
        {
            return _timeProvider != null ? _timeProvider.UnscaledTimeSeconds : Time.unscaledTimeAsDouble;
        }

        private void PublishGazeForCurrentLifecycleState()
        {
            if (_gazeRuntimeEntries == null || _gazeRuntimeEntries.Count == 0)
            {
                return;
            }

            FlushBufferedGazeMessages(GetCurrentTimeSeconds());

            float stalenessSeconds = _effectiveSettings != null ? _effectiveSettings.StalenessSeconds : 0f;
            FailSafeMode currentFailSafe = _effectiveSettings != null
                ? _effectiveSettings.FailSafeMode
                : FailSafeMode.RevertToBase;
            bool stale = stalenessSeconds > 0f &&
                _timeProvider != null &&
                _timeProvider.UnscaledTimeSeconds - _lastAcceptedPacketTime > stalenessSeconds;

            if (stale && currentFailSafe == FailSafeMode.RevertToBase)
            {
                for (int i = 0; i < _gazeRuntimeEntries.Count; i++)
                {
                    _gazeRuntimeEntries[i].PublishZero();
                }
                _failSafeActive = true;
                return;
            }

            if (!stale)
            {
                _failSafeActive = false;
                for (int i = 0; i < _gazeRuntimeEntries.Count; i++)
                {
                    _gazeRuntimeEntries[i].PublishPending();
                }
            }
        }

        private void BuildNormalLookup(OscMapping[] runtimeMappings)
        {
            _normalAddresses = new HashSet<string>(StringComparer.Ordinal);
            _normalBlendShapeNames = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < runtimeMappings.Length; i++)
            {
                OscMapping mapping = runtimeMappings[i];
                if (!string.IsNullOrEmpty(mapping.OscAddress))
                {
                    _normalAddresses.Add(mapping.OscAddress);
                }

                if (!string.IsNullOrEmpty(mapping.BlendShapeName))
                {
                    _normalBlendShapeNames.Add(mapping.BlendShapeName);
                }
            }
        }

        private bool IsKnownNormalBlendShapeMessage(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }

            if (_normalAddresses != null && _normalAddresses.Contains(address))
            {
                return true;
            }

            string blendShapeName = OscReceiver.ExtractBlendShapeName(address);
            return blendShapeName != null &&
                _normalBlendShapeNames != null &&
                _normalBlendShapeNames.Contains(blendShapeName);
        }

        private static IReadOnlyList<string> ResolveMeshBlendShapeNames(
            IReadOnlyList<string> contextBlendShapeNames,
            OscMapping[] runtimeMappings)
        {
            if (contextBlendShapeNames != null && contextBlendShapeNames.Count > 0)
            {
                return contextBlendShapeNames;
            }

            string[] fallbackNames = new string[runtimeMappings.Length];
            for (int i = 0; i < runtimeMappings.Length; i++)
            {
                fallbackNames[i] = runtimeMappings[i].BlendShapeName ?? string.Empty;
            }

            return fallbackNames;
        }

        private static int[] BuildMappingIndexToMeshIndex(
            IReadOnlyList<string> meshBlendShapeNames,
            OscMapping[] runtimeMappings)
        {
            var nameToMeshIndex = new Dictionary<string, int>(meshBlendShapeNames.Count, StringComparer.Ordinal);
            for (int i = 0; i < meshBlendShapeNames.Count; i++)
            {
                string blendShapeName = meshBlendShapeNames[i];
                if (!string.IsNullOrEmpty(blendShapeName) && !nameToMeshIndex.ContainsKey(blendShapeName))
                {
                    nameToMeshIndex.Add(blendShapeName, i);
                }
            }

            int[] mappingIndexToMeshIndex = new int[runtimeMappings.Length];
            for (int i = 0; i < runtimeMappings.Length; i++)
            {
                string blendShapeName = runtimeMappings[i].BlendShapeName;
                if (!string.IsNullOrEmpty(blendShapeName) &&
                    nameToMeshIndex.TryGetValue(blendShapeName, out int meshIndex))
                {
                    mappingIndexToMeshIndex[i] = meshIndex;
                    continue;
                }

                mappingIndexToMeshIndex[i] = -1;
                Debug.LogWarning(
                    $"[OscReceiverAdapterBinding] OSC mapping '{blendShapeName}' was not found in ctx.BlendShapeNames and will be skipped.");
            }

            return mappingIndexToMeshIndex;
        }

        private void MarkAcceptedPacket()
        {
            if (_timeProvider != null)
            {
                _lastAcceptedPacketTime = _timeProvider.UnscaledTimeSeconds;
            }
        }

        private static bool HasGazeMappings(List<OscMappingEntry> mappings)
        {
            if (mappings == null)
            {
                return false;
            }

            for (int i = 0; i < mappings.Count; i++)
            {
                OscMappingEntry entry = mappings[i];
                if (entry != null && IsGazeMode(entry.mode) && !string.IsNullOrEmpty(entry.expressionId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGazeMode(OscMappingMode mode)
        {
            return mode == OscMappingMode.Gaze_VRChat_XY
                || mode == OscMappingMode.Gaze_ARKit_8BS;
        }

        private static bool TryGetFloat(uOSC.Message message, out float value)
        {
            value = default;
            if (message.values == null || message.values.Length == 0)
            {
                return false;
            }

            if (message.values[0] is float f)
            {
                value = f;
                return true;
            }

            if (message.values[0] is int i)
            {
                value = i;
                return true;
            }

            return false;
        }

        private static bool TryParseSenderIdentity(object[] values, out SenderIdentity identity)
        {
            identity = default;
            if (values == null || values.Length < 2)
            {
                return false;
            }

            Guid senderId;
            if (values[0] is byte[] bytes && bytes.Length == SenderIdentity.UuidByteLength)
            {
                senderId = new Guid(bytes);
            }
            else if (values[0] is string idText && Guid.TryParse(idText, out Guid parsed))
            {
                senderId = parsed;
            }
            else
            {
                return false;
            }

            long startedAtUnixMs;
            if (values[1] is long l)
            {
                startedAtUnixMs = l;
            }
            else if (values[1] is int i)
            {
                startedAtUnixMs = i;
            }
            else if (values[1] is string text && long.TryParse(text, out long parsedLong))
            {
                startedAtUnixMs = parsedLong;
            }
            else
            {
                return false;
            }

            try
            {
                identity = new SenderIdentity(senderId, startedAtUnixMs);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private readonly struct GazeRoute
        {
            public readonly GazeRuntimeEntry Runtime;
            public readonly int AxisIndex;

            public GazeRoute(GazeRuntimeEntry runtime, int axisIndex)
            {
                Runtime = runtime;
                AxisIndex = axisIndex;
            }
        }

        private readonly struct GazeSample
        {
            public readonly GazeRuntimeEntry Runtime;
            public readonly int AxisIndex;
            public readonly float Value;

            public GazeSample(GazeRuntimeEntry runtime, int axisIndex, float value)
            {
                Runtime = runtime;
                AxisIndex = axisIndex;
                Value = value;
            }
        }

        private sealed class GazeRuntimeEntry
        {
            public const int VrChatXIndex = 0;
            public const int VrChatYIndex = 1;

            private readonly object _sync = new object();
            private readonly OscMappingMode _mode;
            private readonly float[] _arkitValues;

            private float _vrChatX;
            private float _vrChatY;
            private bool _dirty;

            public GazeRuntimeEntry(OscMappingMode mode)
            {
                _mode = mode;
                if (mode == OscMappingMode.Gaze_ARKit_8BS)
                {
                    _arkitValues = new float[PerfectSyncEyeLook.Count];
                }
            }

            public GazeVector2InputSource CommonSource { get; set; }

            public GazeVector2InputSource LeftSource { get; set; }

            public GazeVector2InputSource RightSource { get; set; }

            public bool HasAnySource =>
                CommonSource != null || LeftSource != null || RightSource != null;

            public void Record(int axisIndex, float value)
            {
                lock (_sync)
                {
                    if (_mode == OscMappingMode.Gaze_VRChat_XY)
                    {
                        if (axisIndex == VrChatXIndex)
                        {
                            _vrChatX = value;
                        }
                        else if (axisIndex == VrChatYIndex)
                        {
                            _vrChatY = value;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (axisIndex < 0 || axisIndex >= _arkitValues.Length)
                        {
                            return;
                        }

                        _arkitValues[axisIndex] = value;
                    }

                    _dirty = true;
                }
            }

            public void PublishPending()
            {
                if (_mode == OscMappingMode.Gaze_VRChat_XY)
                {
                    float x;
                    float y;
                    lock (_sync)
                    {
                        if (!_dirty)
                        {
                            return;
                        }

                        x = _vrChatX;
                        y = _vrChatY;
                        _dirty = false;
                    }

                    PublishVrChat(x, y);
                }
                else
                {
                    Vector2 left;
                    Vector2 right;
                    lock (_sync)
                    {
                        if (!_dirty)
                        {
                            return;
                        }

                        PerfectSyncEyeLook.Decompose(_arkitValues, out left, out right);
                        _dirty = false;
                    }

                    PublishArKit(left, right);
                }
            }

            public void PublishZero()
            {
                lock (_sync)
                {
                    _vrChatX = 0f;
                    _vrChatY = 0f;
                    if (_arkitValues != null)
                    {
                        Array.Clear(_arkitValues, 0, _arkitValues.Length);
                    }

                    _dirty = false;
                }

                CommonSource?.PublishZero();
                LeftSource?.PublishZero();
                RightSource?.PublishZero();
            }

            private void PublishVrChat(float x, float y)
            {
                if (CommonSource != null)
                {
                    CommonSource.Publish(x, y);
                }

                if (LeftSource != null)
                {
                    LeftSource.Publish(x, y);
                }

                if (RightSource != null)
                {
                    RightSource.Publish(x, y);
                }
            }

            private void PublishArKit(Vector2 left, Vector2 right)
            {
                LeftSource?.Publish(left);
                RightSource?.Publish(right);
            }
        }
    }
}
