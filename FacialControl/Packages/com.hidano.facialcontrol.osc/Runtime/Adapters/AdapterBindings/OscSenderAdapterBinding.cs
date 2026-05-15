using System;
using System.Collections.Generic;
using System.Globalization;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.AdapterBindings
{
    /// <summary>
    /// Sends FacialController post-blend output to multiple OSC endpoints as bundles.
    /// </summary>
    [Serializable]
    [FacialAdapterBinding(displayName: "OSC Sender")]
    public sealed class OscSenderAdapterBinding : AdapterBindingBase, IFacialOutputObserver
    {
        public const float DefaultHeartbeatIntervalSeconds = 5f;
        public const float MinHeartbeatIntervalSeconds = 0.5f;
        public const float MaxHeartbeatIntervalSeconds = 60f;

        private const int VrChatGazeMessageCount = 2;

        [SerializeField]
        private List<OscSenderEndpointConfig> _endpoints = new List<OscSenderEndpointConfig>
        {
            new OscSenderEndpointConfig()
        };

        [SerializeField]
        private List<string> _blendShapeNames = new List<string>();

        [SerializeField]
        private List<string> _gazeExpressionIds = new List<string>();

        [SerializeField]
        private float _heartbeatIntervalSeconds = DefaultHeartbeatIntervalSeconds;

        [SerializeField]
        private bool _suppressLoopback = true;

        [NonSerialized]
        private IFacialOutputBus _facialOutputBus;

        [NonSerialized]
        private List<SendSlot> _sendSlots;

        [NonSerialized]
        private Dictionary<(string name, AddressPresetKind preset), byte[]> _addressBytesPool;

        [NonSerialized]
        private LoopbackSuppressionPolicy _loopbackSuppressionPolicy;

        [NonSerialized]
        private GazeSnapshot[] _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();

        [NonSerialized]
        private int _scratchGazeCount;

        [NonSerialized]
        private SenderIdentity _identity;

        [NonSerialized]
        private byte[] _identityUuidBytes;

        [NonSerialized]
        private string _identityStartedAtUnixMs;

        [NonSerialized]
        private float _heartbeatElapsedSeconds;

        [NonSerialized]
        private bool _sendHeartbeatOnNextTick;

        [NonSerialized]
        private bool _hasPublishedFrame;

        [NonSerialized]
        private bool _subscribed;

        [NonSerialized]
        private bool _started;

        /// <summary>
        /// Supports Activator.CreateInstance from the inspector add dropdown.
        /// </summary>
        public OscSenderAdapterBinding()
        {
        }

        public OscSenderEndpointConfig Endpoint
        {
            get
            {
                List<OscSenderEndpointConfig> endpoints = EnsureEndpointList();
                if (endpoints.Count == 0)
                {
                    endpoints.Add(new OscSenderEndpointConfig());
                }

                if (endpoints[0] == null)
                {
                    endpoints[0] = new OscSenderEndpointConfig();
                }

                return endpoints[0];
            }
            set
            {
                List<OscSenderEndpointConfig> endpoints = EnsureEndpointList();
                endpoints.Clear();
                endpoints.Add(value ?? new OscSenderEndpointConfig());
            }
        }

        public List<OscSenderEndpointConfig> Endpoints
        {
            get => EnsureEndpointList();
            set => _endpoints = value ?? new List<OscSenderEndpointConfig>();
        }

        public List<string> BlendShapeNames
        {
            get => _blendShapeNames;
            set => _blendShapeNames = value ?? new List<string>();
        }

        public List<string> GazeExpressionIds
        {
            get => EnsureGazeExpressionIdList();
            set => _gazeExpressionIds = value ?? new List<string>();
        }

        public float HeartbeatIntervalSeconds
        {
            get => _heartbeatIntervalSeconds;
            set => _heartbeatIntervalSeconds = value;
        }

        public bool SuppressLoopback
        {
            get => _suppressLoopback;
            set => _suppressLoopback = value;
        }

        public OscSenderHost HelperHost => _sendSlots != null && _sendSlots.Count > 0
            ? _sendSlots[0].Host
            : null;

        public int HelperHostCount => _sendSlots != null ? _sendSlots.Count : 0;

        public SenderIdentity Identity => _identity;

        public LoopbackSuppressionPolicy LoopbackPolicy => _loopbackSuppressionPolicy;

        public bool IsStarted => _started;

        public OscSenderHost GetHelperHost(int index)
        {
            if (_sendSlots == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _sendSlots[index].Host;
        }

        public void Configure(string endpoint, int port)
        {
            List<OscSenderEndpointConfig> endpoints = EnsureEndpointList();
            endpoints.Clear();
            endpoints.Add(new OscSenderEndpointConfig(endpoint, port));
        }

        public void Configure(string endpoint, int port, IReadOnlyList<string> blendShapeNames)
        {
            Configure(endpoint, port);
            SetBlendShapeNames(blendShapeNames);
        }

        public void ConfigureEndpoints(IReadOnlyList<OscSenderEndpointConfig> endpoints)
        {
            SetEndpoints(endpoints);
        }

        public void ConfigureEndpoints(
            IReadOnlyList<OscSenderEndpointConfig> endpoints,
            IReadOnlyList<string> blendShapeNames)
        {
            SetEndpoints(endpoints);
            SetBlendShapeNames(blendShapeNames);
        }

        public void ConfigureGazeExpressionIds(IReadOnlyList<string> gazeExpressionIds)
        {
            SetGazeExpressionIds(gazeExpressionIds);
        }

        public override void OnStart(in AdapterBuildContext ctx)
        {
            if (_started)
            {
                return;
            }

            if (!AdapterSlug.TryParse(Slug, out _))
            {
                Debug.LogWarning(
                    $"[OscSenderAdapterBinding] Slug '{Slug}' is invalid. OSC Sender will not start.");
                return;
            }

            if (ctx.HostGameObject == null)
            {
                Debug.LogWarning("[OscSenderAdapterBinding] HostGameObject is null. OSC Sender will not start.");
                return;
            }

            _loopbackSuppressionPolicy = _suppressLoopback
                ? LoopbackSuppressionPolicy.FromBindings(ctx.AdapterBindings)
                : null;

            if (!TryBuildEndpointPlan(
                    _loopbackSuppressionPolicy,
                    out List<OscSenderEndpointConfig> endpoints,
                    out bool allEndpointsSuppressed))
            {
                return;
            }

            _addressBytesPool = new Dictionary<(string name, AddressPresetKind preset), byte[]>();
            var sendSlots = new List<SendSlot>(endpoints.Count);
            for (int i = 0; i < endpoints.Count; i++)
            {
                OscSenderEndpointConfig endpoint = endpoints[i];
                if (!TryBuildMappings(
                        ctx.BlendShapeNames,
                        endpoint.preset,
                        out OscMapping[] mappings,
                        out byte[][] addressUtf8,
                        out int[] sourceBlendShapeIndices,
                        out string[] heartbeatBlendShapeNames,
                        out string[] gazeExpressionIds))
                {
                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] OSC mapping is empty for endpoint '{endpoint.endpoint}:{endpoint.port}'. Skipping endpoint.");
                    continue;
                }

                OscSenderHost host = null;
                try
                {
                    host = ctx.HostGameObject.AddComponent<OscSenderHost>();
                    host.Configure(endpoint.endpoint, endpoint.port, mappings, addressUtf8);
                    sendSlots.Add(new SendSlot(
                        host,
                        endpoint.preset,
                        addressUtf8,
                        sourceBlendShapeIndices,
                        heartbeatBlendShapeNames,
                        gazeExpressionIds));
                }
                catch (Exception ex)
                {
                    if (host != null)
                    {
                        UnityEngine.Object.Destroy(host);
                    }

                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] Failed to start endpoint '{endpoint.endpoint}:{endpoint.port}'. {ex.Message}");
                }
            }

            if (sendSlots.Count == 0)
            {
                if (allEndpointsSuppressed)
                {
                    _addressBytesPool = null;
                    CompleteStart(in ctx, sendSlots);
                    return;
                }

                _addressBytesPool = null;
                _loopbackSuppressionPolicy = null;
                Debug.LogWarning("[OscSenderAdapterBinding] No endpoint could be started. OSC Sender will not start.");
                return;
            }

            CompleteStart(in ctx, sendSlots);
        }

        public override void OnLateTick(float deltaTime)
        {
            if (!_started || _sendSlots == null)
            {
                return;
            }

            if (_sendSlots.Count == 0)
            {
                return;
            }

            bool sendHeartbeat = ShouldSendHeartbeat(deltaTime);
            if (!_hasPublishedFrame && !sendHeartbeat)
            {
                return;
            }

            bool sentAny = false;
            for (int i = 0; i < _sendSlots.Count; i++)
            {
                SendSlot slot = _sendSlots[i];
                if (slot.Host == null)
                {
                    continue;
                }

                if (sendHeartbeat)
                {
                    slot.Host.SendBundle(
                        _identityUuidBytes,
                        _identityStartedAtUnixMs,
                        slot.ScratchAddressUtf8,
                        slot.ScratchFloatValues,
                        _hasPublishedFrame ? slot.ScratchFloatCount : 0,
                        slot.HeartbeatBlendShapeNames,
                        slot.HeartbeatBlendShapeNames.Length);
                }
                else
                {
                    slot.Host.SendBundle(
                        _identityUuidBytes,
                        _identityStartedAtUnixMs,
                        slot.ScratchAddressUtf8,
                        slot.ScratchFloatValues,
                        slot.ScratchFloatCount);
                }

                sentAny = true;
            }

            if (sendHeartbeat && sentAny)
            {
                _sendHeartbeatOnNextTick = false;
                _heartbeatElapsedSeconds = 0f;
            }
        }

        public override void Dispose()
        {
            if (_subscribed && _facialOutputBus != null)
            {
                _facialOutputBus.Unsubscribe(this);
            }

            _subscribed = false;
            _facialOutputBus = null;

            if (_sendSlots != null)
            {
                for (int i = 0; i < _sendSlots.Count; i++)
                {
                    OscSenderHost host = _sendSlots[i].Host;
                    if (host != null)
                    {
                        UnityEngine.Object.Destroy(host);
                    }
                }

                _sendSlots = null;
            }

            _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();
            _scratchGazeCount = 0;
            _identity = default;
            _identityUuidBytes = null;
            _identityStartedAtUnixMs = null;
            _addressBytesPool = null;
            _loopbackSuppressionPolicy = null;
            _heartbeatElapsedSeconds = 0f;
            _sendHeartbeatOnNextTick = false;
            _hasPublishedFrame = false;
            _started = false;
        }

        void IFacialOutputObserver.OnFacialOutputPublished(
            ReadOnlySpan<float> postBlendValues,
            ReadOnlySpan<GazeSnapshot> gazeSnapshots)
        {
            if (!_started || _sendSlots == null)
            {
                return;
            }

            EnsureGazeCapacity(gazeSnapshots.Length);
            for (int i = 0; i < gazeSnapshots.Length; i++)
            {
                _scratchGazeSnapshots[i] = gazeSnapshots[i];
            }

            _scratchGazeCount = gazeSnapshots.Length;

            for (int slotIndex = 0; slotIndex < _sendSlots.Count; slotIndex++)
            {
                SendSlot slot = _sendSlots[slotIndex];
                int[] sourceBlendShapeIndices = slot.SourceBlendShapeIndices;
                slot.EnsureScratchCapacity();
                int writeIndex = 0;
                for (int i = 0; i < sourceBlendShapeIndices.Length; i++)
                {
                    int sourceIndex = sourceBlendShapeIndices[i];
                    slot.ScratchAddressUtf8[writeIndex] = slot.ConfiguredAddressUtf8[i];
                    slot.ScratchFloatValues[writeIndex] =
                        sourceIndex >= 0 && sourceIndex < postBlendValues.Length
                            ? postBlendValues[sourceIndex]
                            : 0f;
                    writeIndex++;
                }

                AppendGazeMessages(slot, ref writeIndex);
                slot.ScratchFloatCount = writeIndex;
            }

            _hasPublishedFrame = true;
        }

        private List<OscSenderEndpointConfig> EnsureEndpointList()
        {
            if (_endpoints == null)
            {
                _endpoints = new List<OscSenderEndpointConfig>();
            }

            return _endpoints;
        }

        private List<string> EnsureGazeExpressionIdList()
        {
            if (_gazeExpressionIds == null)
            {
                _gazeExpressionIds = new List<string>();
            }

            return _gazeExpressionIds;
        }

        private void SetEndpoints(IReadOnlyList<OscSenderEndpointConfig> endpoints)
        {
            List<OscSenderEndpointConfig> target = EnsureEndpointList();
            target.Clear();
            if (endpoints == null)
            {
                return;
            }

            for (int i = 0; i < endpoints.Count; i++)
            {
                OscSenderEndpointConfig endpoint = endpoints[i];
                target.Add(endpoint == null
                    ? null
                    : new OscSenderEndpointConfig(
                        endpoint.endpoint,
                        endpoint.port,
                        endpoint.enabled,
                        endpoint.preset));
            }
        }

        private void CompleteStart(in AdapterBuildContext ctx, List<SendSlot> sendSlots)
        {
            _sendSlots = sendSlots;
            _heartbeatIntervalSeconds = ClampHeartbeatInterval(_heartbeatIntervalSeconds, logWarning: true);
            _heartbeatElapsedSeconds = 0f;
            _sendHeartbeatOnNextTick = true;
            _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();
            _scratchGazeCount = 0;
            _hasPublishedFrame = false;
            _identity = SenderIdentityGenerator.Generate();
            _identityUuidBytes = _identity.Uuid.ToByteArray();
            _identityStartedAtUnixMs = _identity.StartedAtUnixMs.ToString(CultureInfo.InvariantCulture);

            _facialOutputBus = ctx.FacialOutputBus;
            _facialOutputBus.Subscribe(this);
            _subscribed = true;
            _started = true;
        }

        private bool TryBuildEndpointPlan(
            LoopbackSuppressionPolicy loopbackPolicy,
            out List<OscSenderEndpointConfig> endpoints,
            out bool allEndpointsSuppressed)
        {
            List<OscSenderEndpointConfig> configuredEndpoints = EnsureEndpointList();
            endpoints = new List<OscSenderEndpointConfig>(configuredEndpoints.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool loggedDuplicate = false;
            int distinctEnabledCount = 0;
            int suppressedCount = 0;
            allEndpointsSuppressed = false;

            for (int i = 0; i < configuredEndpoints.Count; i++)
            {
                OscSenderEndpointConfig configuredEndpoint = configuredEndpoints[i];
                if (configuredEndpoint == null)
                {
                    continue;
                }

                if (!configuredEndpoint.enabled)
                {
                    continue;
                }

                string endpoint = NormalizeEndpoint(configuredEndpoint.endpoint);
                string key = endpoint + "\n" + configuredEndpoint.port.ToString(CultureInfo.InvariantCulture);
                if (!seen.Add(key))
                {
                    if (!loggedDuplicate)
                    {
                        Debug.LogWarning(
                            $"[OscSenderAdapterBinding] Duplicate endpoint '{endpoint}:{configuredEndpoint.port}' was normalized to one send slot.");
                        loggedDuplicate = true;
                    }

                    continue;
                }

                distinctEnabledCount++;
                if (loopbackPolicy != null && loopbackPolicy.IsSuppressed(endpoint, configuredEndpoint.port))
                {
                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] Endpoint '{endpoint}:{configuredEndpoint.port}' matches an OSC receiver in the same child scope and was suppressed.");
                    suppressedCount++;
                    continue;
                }

                endpoints.Add(new OscSenderEndpointConfig(
                    endpoint,
                    configuredEndpoint.port,
                    enabled: true,
                    configuredEndpoint.preset));
            }

            if (distinctEnabledCount == 0)
            {
                Debug.LogWarning("[OscSenderAdapterBinding] No enabled endpoints. OSC Sender will not start.");
                return false;
            }

            if (endpoints.Count == 0 && suppressedCount == distinctEnabledCount)
            {
                allEndpointsSuppressed = true;
                Debug.LogWarning(
                    "[OscSenderAdapterBinding] All endpoints were suppressed by loopback policy. OSC Sender remains live without sending.");
            }

            return true;
        }

        private void SetBlendShapeNames(IReadOnlyList<string> blendShapeNames)
        {
            if (_blendShapeNames == null)
            {
                _blendShapeNames = new List<string>();
            }

            _blendShapeNames.Clear();
            if (blendShapeNames == null)
            {
                return;
            }

            for (int i = 0; i < blendShapeNames.Count; i++)
            {
                _blendShapeNames.Add(blendShapeNames[i]);
            }
        }

        private void SetGazeExpressionIds(IReadOnlyList<string> gazeExpressionIds)
        {
            List<string> target = EnsureGazeExpressionIdList();
            target.Clear();
            if (gazeExpressionIds == null)
            {
                return;
            }

            for (int i = 0; i < gazeExpressionIds.Count; i++)
            {
                target.Add(gazeExpressionIds[i]);
            }
        }

        private bool TryBuildMappings(
            IReadOnlyList<string> contextBlendShapeNames,
            AddressPresetKind preset,
            out OscMapping[] mappings,
            out byte[][] addressUtf8,
            out int[] sourceIndices,
            out string[] heartbeatBlendShapeNames,
            out string[] gazeExpressionIds)
        {
            IReadOnlyList<string> names = _blendShapeNames != null && _blendShapeNames.Count > 0
                ? _blendShapeNames
                : contextBlendShapeNames;

            int blendShapeCapacity = names != null ? names.Count : 0;
            int gazeCapacity = _gazeExpressionIds != null ? _gazeExpressionIds.Count : 0;
            int gazeMessageCount = GetGazeMessageCount(preset);
            var mappingList = new List<OscMapping>(blendShapeCapacity + (gazeCapacity * gazeMessageCount));
            var addressBytesList = new List<byte[]>(blendShapeCapacity + (gazeCapacity * gazeMessageCount));
            var indexList = new List<int>(blendShapeCapacity);
            var heartbeatNameList = new List<string>(blendShapeCapacity);
            var gazeExpressionIdList = new List<string>(gazeCapacity);

            if (names != null)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    string blendShapeName = names[i];
                    if (string.IsNullOrEmpty(blendShapeName))
                    {
                        continue;
                    }

                    int sourceIndex = ResolveSourceIndex(contextBlendShapeNames, blendShapeName, i);
                    if (sourceIndex < 0)
                    {
                        Debug.LogWarning(
                            $"[OscSenderAdapterBinding] BlendShape '{blendShapeName}' was not found in context. Skipping.");
                        continue;
                    }

                    string address;
                    try
                    {
                        address = OscAddressFormatter.FormatBlendShapeAddress(preset, blendShapeName);
                    }
                    catch (NotSupportedException ex)
                    {
                        Debug.LogWarning($"[OscSenderAdapterBinding] {ex.Message}");
                        continue;
                    }

                    mappingList.Add(new OscMapping(address, blendShapeName, string.Empty));
                    addressBytesList.Add(OscAddressFormatter.GetOrAddBlendShapeAddressUtf8(
                        _addressBytesPool,
                        preset,
                        blendShapeName));
                    indexList.Add(sourceIndex);
                    heartbeatNameList.Add(blendShapeName);
                }
            }

            AppendGazeMappings(preset, mappingList, addressBytesList, gazeExpressionIdList);

            mappings = mappingList.ToArray();
            addressUtf8 = addressBytesList.ToArray();
            sourceIndices = indexList.ToArray();
            heartbeatBlendShapeNames = heartbeatNameList.ToArray();
            gazeExpressionIds = gazeExpressionIdList.ToArray();
            return mappings.Length > 0;
        }

        private void AppendGazeMappings(
            AddressPresetKind preset,
            List<OscMapping> mappingList,
            List<byte[]> addressBytesList,
            List<string> gazeExpressionIdList)
        {
            if (_gazeExpressionIds == null)
            {
                return;
            }

            for (int i = 0; i < _gazeExpressionIds.Count; i++)
            {
                string expressionId = _gazeExpressionIds[i];
                if (string.IsNullOrEmpty(expressionId))
                {
                    continue;
                }

                if (preset == AddressPresetKind.ARKit)
                {
                    AppendArKitGazeMappings(mappingList, addressBytesList);
                    gazeExpressionIdList.Add(expressionId);
                    continue;
                }

                string xAddress = OscAddressFormatter.FormatGazeAddress(
                    preset,
                    expressionId,
                    OscAddressFormatter.VRChatGazeXAxis);
                string yAddress = OscAddressFormatter.FormatGazeAddress(
                    preset,
                    expressionId,
                    OscAddressFormatter.VRChatGazeYAxis);

                mappingList.Add(new OscMapping(xAddress, expressionId + "X", string.Empty));
                addressBytesList.Add(OscAddressFormatter.GetOrAddGazeAddressUtf8(
                    _addressBytesPool,
                    preset,
                    expressionId,
                    OscAddressFormatter.VRChatGazeXAxis));

                mappingList.Add(new OscMapping(yAddress, expressionId + "Y", string.Empty));
                addressBytesList.Add(OscAddressFormatter.GetOrAddGazeAddressUtf8(
                    _addressBytesPool,
                    preset,
                    expressionId,
                    OscAddressFormatter.VRChatGazeYAxis));

                gazeExpressionIdList.Add(expressionId);
            }
        }

        private void AppendArKitGazeMappings(
            List<OscMapping> mappingList,
            List<byte[]> addressBytesList)
        {
            for (int i = 0; i < PerfectSyncEyeLook.Count; i++)
            {
                string name = PerfectSyncEyeLook.Names[i];
                string address = OscAddressFormatter.FormatBlendShapeAddress(AddressPresetKind.ARKit, name);

                mappingList.Add(new OscMapping(address, name, string.Empty));
                addressBytesList.Add(OscAddressFormatter.GetOrAddBlendShapeAddressUtf8(
                    _addressBytesPool,
                    AddressPresetKind.ARKit,
                    name));
            }
        }

        private void AppendGazeMessages(SendSlot slot, ref int writeIndex)
        {
            string[] gazeIds = slot.GazeExpressionIds;
            if (gazeIds.Length == 0 || _scratchGazeCount == 0)
            {
                return;
            }

            int addressIndex = slot.SourceBlendShapeIndices.Length;
            for (int i = 0; i < gazeIds.Length; i++)
            {
                if (TryFindGazeSnapshot(gazeIds[i], out GazeSnapshot snapshot))
                {
                    if (slot.Preset == AddressPresetKind.ARKit)
                    {
                        AppendArKitGazeMessages(slot, addressIndex, snapshot, ref writeIndex);
                    }
                    else
                    {
                        AppendVrChatGazeMessages(slot, addressIndex, snapshot, ref writeIndex);
                    }
                }

                addressIndex += slot.GazeMessageCount;
            }
        }

        private static void AppendVrChatGazeMessages(
            SendSlot slot,
            int addressIndex,
            GazeSnapshot snapshot,
            ref int writeIndex)
        {
            slot.ScratchAddressUtf8[writeIndex] = slot.ConfiguredAddressUtf8[addressIndex];
            slot.ScratchFloatValues[writeIndex] = snapshot.X;
            writeIndex++;

            slot.ScratchAddressUtf8[writeIndex] = slot.ConfiguredAddressUtf8[addressIndex + 1];
            slot.ScratchFloatValues[writeIndex] = snapshot.Y;
            writeIndex++;
        }

        private static void AppendArKitGazeMessages(
            SendSlot slot,
            int addressIndex,
            GazeSnapshot snapshot,
            ref int writeIndex)
        {
            Span<float> eyeLookValues = stackalloc float[PerfectSyncEyeLook.Count];
            var gaze = new Vector2(snapshot.X, snapshot.Y);
            PerfectSyncEyeLook.Compose(gaze, gaze, eyeLookValues);

            for (int i = 0; i < PerfectSyncEyeLook.Count; i++)
            {
                slot.ScratchAddressUtf8[writeIndex] = slot.ConfiguredAddressUtf8[addressIndex + i];
                slot.ScratchFloatValues[writeIndex] = eyeLookValues[i];
                writeIndex++;
            }
        }

        private bool TryFindGazeSnapshot(string expressionId, out GazeSnapshot snapshot)
        {
            for (int i = 0; i < _scratchGazeCount; i++)
            {
                GazeSnapshot candidate = _scratchGazeSnapshots[i];
                if (string.Equals(candidate.ExpressionId, expressionId, StringComparison.Ordinal))
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private bool ShouldSendHeartbeat(float deltaTime)
        {
            if (_sendHeartbeatOnNextTick)
            {
                return true;
            }

            if (deltaTime > 0f)
            {
                _heartbeatElapsedSeconds += deltaTime;
            }

            return _heartbeatElapsedSeconds >= _heartbeatIntervalSeconds;
        }

        private static float ClampHeartbeatInterval(float intervalSeconds, bool logWarning)
        {
            if (float.IsNaN(intervalSeconds) || intervalSeconds < MinHeartbeatIntervalSeconds)
            {
                if (logWarning)
                {
                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] heartbeatIntervalSeconds {intervalSeconds.ToString(CultureInfo.InvariantCulture)} is below {MinHeartbeatIntervalSeconds.ToString(CultureInfo.InvariantCulture)} and was clamped.");
                }

                return MinHeartbeatIntervalSeconds;
            }

            if (float.IsInfinity(intervalSeconds) || intervalSeconds > MaxHeartbeatIntervalSeconds)
            {
                if (logWarning)
                {
                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] heartbeatIntervalSeconds {intervalSeconds.ToString(CultureInfo.InvariantCulture)} is above {MaxHeartbeatIntervalSeconds.ToString(CultureInfo.InvariantCulture)} and was clamped.");
                }

                return MaxHeartbeatIntervalSeconds;
            }

            return intervalSeconds;
        }

        private static int ResolveSourceIndex(
            IReadOnlyList<string> contextBlendShapeNames,
            string blendShapeName,
            int defaultIndex)
        {
            if (contextBlendShapeNames == null || contextBlendShapeNames.Count == 0)
            {
                return defaultIndex;
            }

            for (int i = 0; i < contextBlendShapeNames.Count; i++)
            {
                if (string.Equals(contextBlendShapeNames[i], blendShapeName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return OscSenderEndpointConfig.DefaultEndpoint;
            }

            return endpoint.Trim();
        }

        private void EnsureGazeCapacity(int count)
        {
            if (_scratchGazeSnapshots == null || _scratchGazeSnapshots.Length < count)
            {
                _scratchGazeSnapshots = new GazeSnapshot[count];
            }
        }

        private static int GetGazeMessageCount(AddressPresetKind preset)
        {
            return preset == AddressPresetKind.ARKit
                ? PerfectSyncEyeLook.Count
                : VrChatGazeMessageCount;
        }

        private sealed class SendSlot
        {
            public readonly OscSenderHost Host;
            public readonly AddressPresetKind Preset;
            public readonly byte[][] ConfiguredAddressUtf8;
            public readonly int[] SourceBlendShapeIndices;
            public readonly string[] HeartbeatBlendShapeNames;
            public readonly string[] GazeExpressionIds;
            public readonly int GazeMessageCount;
            public byte[][] ScratchAddressUtf8;
            public float[] ScratchFloatValues;
            public int ScratchFloatCount;

            public SendSlot(
                OscSenderHost host,
                AddressPresetKind preset,
                byte[][] configuredAddressUtf8,
                int[] sourceBlendShapeIndices,
                string[] heartbeatBlendShapeNames,
                string[] gazeExpressionIds)
            {
                Host = host;
                Preset = preset;
                ConfiguredAddressUtf8 = configuredAddressUtf8 ?? Array.Empty<byte[]>();
                SourceBlendShapeIndices = sourceBlendShapeIndices ?? Array.Empty<int>();
                HeartbeatBlendShapeNames = heartbeatBlendShapeNames ?? Array.Empty<string>();
                GazeExpressionIds = gazeExpressionIds ?? Array.Empty<string>();
                GazeMessageCount = GetGazeMessageCount(preset);
                int scratchCapacity = SourceBlendShapeIndices.Length + (GazeExpressionIds.Length * GazeMessageCount);
                ScratchAddressUtf8 = scratchCapacity == 0
                    ? Array.Empty<byte[]>()
                    : new byte[scratchCapacity][];
                ScratchFloatValues = scratchCapacity == 0
                    ? Array.Empty<float>()
                    : new float[scratchCapacity];
            }

            public void EnsureScratchCapacity()
            {
                int required = SourceBlendShapeIndices.Length + (GazeExpressionIds.Length * GazeMessageCount);
                if (ScratchAddressUtf8 == null || ScratchAddressUtf8.Length < required)
                {
                    ScratchAddressUtf8 = new byte[required][];
                }

                if (ScratchFloatValues == null || ScratchFloatValues.Length < required)
                {
                    ScratchFloatValues = new float[required];
                }
            }
        }
    }
}
