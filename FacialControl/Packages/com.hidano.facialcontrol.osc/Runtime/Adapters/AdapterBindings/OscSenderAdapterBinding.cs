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
        [SerializeField]
        private List<OscSenderEndpointConfig> _endpoints = new List<OscSenderEndpointConfig>
        {
            new OscSenderEndpointConfig()
        };

        [SerializeField]
        private List<string> _blendShapeNames = new List<string>();

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
                        out int[] sourceBlendShapeIndices))
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
                    sendSlots.Add(new SendSlot(host, sourceBlendShapeIndices));
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
            if (!_started || !_hasPublishedFrame || _sendSlots == null)
            {
                return;
            }

            for (int i = 0; i < _sendSlots.Count; i++)
            {
                SendSlot slot = _sendSlots[i];
                if (slot.Host == null)
                {
                    continue;
                }

                slot.Host.SendBundle(
                    _identityUuidBytes,
                    _identityStartedAtUnixMs,
                    slot.ScratchPostBlendValues,
                    slot.ScratchPostBlendCount);
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
            out int[] sourceIndices)
        {
            IReadOnlyList<string> names = _blendShapeNames != null && _blendShapeNames.Count > 0
                ? _blendShapeNames
                : contextBlendShapeNames;

            if (names == null || names.Count == 0)
            {
                mappings = Array.Empty<OscMapping>();
                sourceIndices = Array.Empty<int>();
                return false;
            }

            var mappingList = new List<OscMapping>(names.Count);
            var indexList = new List<int>(names.Count);
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
            }

            mappings = mappingList.ToArray();
            sourceIndices = indexList.ToArray();
            return mappings.Length > 0;
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
            public float[] ScratchPostBlendValues;
            public int ScratchPostBlendCount;

            public SendSlot(
                OscSenderHost host,
                int[] sourceBlendShapeIndices)
            {
                Host = host;
                SourceBlendShapeIndices = sourceBlendShapeIndices;
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
