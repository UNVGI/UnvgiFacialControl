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
    /// FacialController の post-blend 出力を OSC bundle として単一 endpoint へ送信する binding。
    /// </summary>
    [Serializable]
    [FacialAdapterBinding(displayName: "OSC Sender")]
    public sealed class OscSenderAdapterBinding : AdapterBindingBase, IFacialOutputObserver
    {
        [SerializeField]
        private OscSenderEndpointConfig _endpoint = new OscSenderEndpointConfig();

        [SerializeField]
        private List<string> _blendShapeNames = new List<string>();

        [NonSerialized]
        private IFacialOutputBus _facialOutputBus;

        [NonSerialized]
        private OscSenderHost _helperHost;

        [NonSerialized]
        private OscMapping[] _runtimeMappings;

        [NonSerialized]
        private int[] _sourceBlendShapeIndices;

        [NonSerialized]
        private float[] _scratchPostBlendValues = Array.Empty<float>();

        [NonSerialized]
        private GazeSnapshot[] _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();

        [NonSerialized]
        private int _scratchPostBlendCount;

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
        /// Inspector の Add ドロップダウンから Activator.CreateInstance で生成できるようにする。
        /// </summary>
        public OscSenderAdapterBinding()
        {
        }

        public OscSenderEndpointConfig Endpoint
        {
            get
            {
                if (_endpoint == null)
                {
                    _endpoint = new OscSenderEndpointConfig();
                }

                return _endpoint;
            }
            set => _endpoint = value ?? new OscSenderEndpointConfig();
        }

        public List<string> BlendShapeNames
        {
            get => _blendShapeNames;
            set => _blendShapeNames = value ?? new List<string>();
        }

        public OscSenderHost HelperHost => _helperHost;

        public SenderIdentity Identity => _identity;

        public bool IsStarted => _started;

        public void Configure(string endpoint, int port)
        {
            Endpoint.endpoint = endpoint;
            Endpoint.port = port;
        }

        public void Configure(string endpoint, int port, IReadOnlyList<string> blendShapeNames)
        {
            Configure(endpoint, port);
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

            OscSenderEndpointConfig endpoint = Endpoint;
            if (!endpoint.enabled)
            {
                Debug.LogWarning("[OscSenderAdapterBinding] Endpoint is disabled. OSC Sender will not start.");
                return;
            }

            if (!TryBuildMappings(ctx.BlendShapeNames, endpoint.preset, out _runtimeMappings, out _sourceBlendShapeIndices))
            {
                Debug.LogWarning(
                    $"[OscSenderAdapterBinding] BlendShape mapping is empty. OSC Sender will not start. Slug='{Slug}'");
                return;
            }

            _scratchPostBlendValues = new float[_runtimeMappings.Length];
            _scratchPostBlendCount = 0;
            _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();
            _scratchGazeCount = 0;
            _hasPublishedFrame = false;
            _identity = SenderIdentityGenerator.Generate();
            _identityUuidBytes = _identity.Uuid.ToByteArray();
            _identityStartedAtUnixMs = _identity.StartedAtUnixMs.ToString(CultureInfo.InvariantCulture);

            _helperHost = ctx.HostGameObject.AddComponent<OscSenderHost>();
            _helperHost.Configure(endpoint.endpoint, endpoint.port, _runtimeMappings);

            _facialOutputBus = ctx.FacialOutputBus;
            _facialOutputBus.Subscribe(this);
            _subscribed = true;
            _started = true;
        }

        public override void OnLateTick(float deltaTime)
        {
            if (!_started || !_hasPublishedFrame || _helperHost == null)
            {
                return;
            }

            _helperHost.SendBundle(
                _identityUuidBytes,
                _identityStartedAtUnixMs,
                _scratchPostBlendValues,
                _scratchPostBlendCount);
        }

        public override void Dispose()
        {
            if (_subscribed && _facialOutputBus != null)
            {
                _facialOutputBus.Unsubscribe(this);
            }

            _subscribed = false;
            _facialOutputBus = null;

            if (_helperHost != null)
            {
                UnityEngine.Object.Destroy(_helperHost);
                _helperHost = null;
            }

            _runtimeMappings = null;
            _sourceBlendShapeIndices = null;
            _scratchPostBlendValues = Array.Empty<float>();
            _scratchGazeSnapshots = Array.Empty<GazeSnapshot>();
            _scratchPostBlendCount = 0;
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
            if (!_started || _sourceBlendShapeIndices == null)
            {
                return;
            }

            EnsurePostBlendCapacity(_sourceBlendShapeIndices.Length);
            for (int i = 0; i < _sourceBlendShapeIndices.Length; i++)
            {
                int sourceIndex = _sourceBlendShapeIndices[i];
                _scratchPostBlendValues[i] =
                    sourceIndex >= 0 && sourceIndex < postBlendValues.Length
                        ? postBlendValues[sourceIndex]
                        : 0f;
            }

            _scratchPostBlendCount = _sourceBlendShapeIndices.Length;

            EnsureGazeCapacity(gazeSnapshots.Length);
            for (int i = 0; i < gazeSnapshots.Length; i++)
            {
                _scratchGazeSnapshots[i] = gazeSnapshots[i];
            }

            _scratchGazeCount = gazeSnapshots.Length;
            _hasPublishedFrame = true;
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

        private void EnsurePostBlendCapacity(int count)
        {
            if (_scratchPostBlendValues == null || _scratchPostBlendValues.Length < count)
            {
                _scratchPostBlendValues = new float[count];
            }
        }

        private void EnsureGazeCapacity(int count)
        {
            if (_scratchGazeSnapshots == null || _scratchGazeSnapshots.Length < count)
            {
                _scratchGazeSnapshots = new GazeSnapshot[count];
            }
        }
    }
}
