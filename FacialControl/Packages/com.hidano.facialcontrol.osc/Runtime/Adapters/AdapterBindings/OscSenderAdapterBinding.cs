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

        [SerializeField]
        private List<OscSenderEndpointConfig> _endpoints = new List<OscSenderEndpointConfig>
        {
            new OscSenderEndpointConfig()
        };

        [SerializeField]
        private List<string> _blendShapeNames = new List<string>();

        [SerializeField]
        private float _heartbeatIntervalSeconds = DefaultHeartbeatIntervalSeconds;

        [NonSerialized]
        private IFacialOutputBus _facialOutputBus;

        [NonSerialized]
        private List<SendSlot> _sendSlots;

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
        private string[] _heartbeatBlendShapeNames = Array.Empty<string>();

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

        public float HeartbeatIntervalSeconds
        {
            get => _heartbeatIntervalSeconds;
            set => _heartbeatIntervalSeconds = value;
        }

        public OscSenderHost HelperHost => _sendSlots != null && _sendSlots.Count > 0
            ? _sendSlots[0].Host
            : null;

        public int HelperHostCount => _sendSlots != null ? _sendSlots.Count : 0;

        public SenderIdentity Identity => _identity;

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

            if (!TryBuildEndpointPlan(out List<OscSenderEndpointConfig> endpoints))
            {
                return;
            }

            var sendSlots = new List<SendSlot>(endpoints.Count);
            for (int i = 0; i < endpoints.Count; i++)
            {
                OscSenderEndpointConfig endpoint = endpoints[i];
                if (!TryBuildMappings(
                        ctx.BlendShapeNames,
                        endpoint.preset,
                        out OscMapping[] mappings,
                        out int[] sourceBlendShapeIndices,
                        out string[] heartbeatBlendShapeNames))
                {
                    Debug.LogWarning(
                        $"[OscSenderAdapterBinding] BlendShape mapping is empty for endpoint '{endpoint.endpoint}:{endpoint.port}'. Skipping endpoint.");
                    continue;
                }

                OscSenderHost host = null;
                try
                {
                    host = ctx.HostGameObject.AddComponent<OscSenderHost>();
                    host.Configure(endpoint.endpoint, endpoint.port, mappings);
                    sendSlots.Add(new SendSlot(host, sourceBlendShapeIndices, heartbeatBlendShapeNames));
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
                Debug.LogWarning("[OscSenderAdapterBinding] No endpoint could be started. OSC Sender will not start.");
                return;
            }

            _sendSlots = sendSlots;
            _heartbeatBlendShapeNames = sendSlots[0].HeartbeatBlendShapeNames;
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

        public override void OnLateTick(float deltaTime)
        {
            if (!_started || _sendSlots == null)
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
                        slot.ScratchPostBlendValues,
                        _hasPublishedFrame ? slot.ScratchPostBlendCount : 0,
                        _heartbeatBlendShapeNames,
                        _heartbeatBlendShapeNames.Length);
                }
                else
                {
                    slot.Host.SendBundle(
                        _identityUuidBytes,
                        _identityStartedAtUnixMs,
                        slot.ScratchPostBlendValues,
                        slot.ScratchPostBlendCount);
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
            _heartbeatBlendShapeNames = Array.Empty<string>();
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

            for (int slotIndex = 0; slotIndex < _sendSlots.Count; slotIndex++)
            {
                SendSlot slot = _sendSlots[slotIndex];
                int[] sourceBlendShapeIndices = slot.SourceBlendShapeIndices;
                slot.EnsurePostBlendCapacity(sourceBlendShapeIndices.Length);
                for (int i = 0; i < sourceBlendShapeIndices.Length; i++)
                {
                    int sourceIndex = sourceBlendShapeIndices[i];
                    slot.ScratchPostBlendValues[i] =
                        sourceIndex >= 0 && sourceIndex < postBlendValues.Length
                            ? postBlendValues[sourceIndex]
                            : 0f;
                }

                slot.ScratchPostBlendCount = sourceBlendShapeIndices.Length;
            }

            EnsureGazeCapacity(gazeSnapshots.Length);
            for (int i = 0; i < gazeSnapshots.Length; i++)
            {
                _scratchGazeSnapshots[i] = gazeSnapshots[i];
            }

            _scratchGazeCount = gazeSnapshots.Length;
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

        private bool TryBuildEndpointPlan(out List<OscSenderEndpointConfig> endpoints)
        {
            List<OscSenderEndpointConfig> configuredEndpoints = EnsureEndpointList();
            endpoints = new List<OscSenderEndpointConfig>(configuredEndpoints.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool loggedDuplicate = false;

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

                endpoints.Add(new OscSenderEndpointConfig(
                    endpoint,
                    configuredEndpoint.port,
                    enabled: true,
                    configuredEndpoint.preset));
            }

            if (endpoints.Count == 0)
            {
                Debug.LogWarning("[OscSenderAdapterBinding] No enabled endpoints. OSC Sender will not start.");
                return false;
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

        private bool TryBuildMappings(
            IReadOnlyList<string> contextBlendShapeNames,
            AddressPresetKind preset,
            out OscMapping[] mappings,
            out int[] sourceIndices,
            out string[] heartbeatBlendShapeNames)
        {
            IReadOnlyList<string> names = _blendShapeNames != null && _blendShapeNames.Count > 0
                ? _blendShapeNames
                : contextBlendShapeNames;

            if (names == null || names.Count == 0)
            {
                mappings = Array.Empty<OscMapping>();
                sourceIndices = Array.Empty<int>();
                heartbeatBlendShapeNames = Array.Empty<string>();
                return false;
            }

            var mappingList = new List<OscMapping>(names.Count);
            var indexList = new List<int>(names.Count);
            var heartbeatNameList = new List<string>(names.Count);
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
                indexList.Add(sourceIndex);
                heartbeatNameList.Add(blendShapeName);
            }

            mappings = mappingList.ToArray();
            sourceIndices = indexList.ToArray();
            heartbeatBlendShapeNames = heartbeatNameList.ToArray();
            return mappings.Length > 0;
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

        private sealed class SendSlot
        {
            public readonly OscSenderHost Host;
            public readonly int[] SourceBlendShapeIndices;
            public readonly string[] HeartbeatBlendShapeNames;
            public float[] ScratchPostBlendValues;
            public int ScratchPostBlendCount;

            public SendSlot(
                OscSenderHost host,
                int[] sourceBlendShapeIndices,
                string[] heartbeatBlendShapeNames)
            {
                Host = host;
                SourceBlendShapeIndices = sourceBlendShapeIndices;
                HeartbeatBlendShapeNames = heartbeatBlendShapeNames ?? Array.Empty<string>();
                ScratchPostBlendValues = sourceBlendShapeIndices.Length == 0
                    ? Array.Empty<float>()
                    : new float[sourceBlendShapeIndices.Length];
            }

            public void EnsurePostBlendCapacity(int count)
            {
                if (ScratchPostBlendValues == null || ScratchPostBlendValues.Length < count)
                {
                    ScratchPostBlendValues = new float[count];
                }
            }
        }
    }
}
