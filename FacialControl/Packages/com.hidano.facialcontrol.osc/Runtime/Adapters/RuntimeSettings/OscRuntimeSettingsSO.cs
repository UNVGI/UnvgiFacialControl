using System.Collections.Generic;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.RuntimeSettings;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.RuntimeSettings
{
    /// <summary>
    /// OSC Receiver / Sender セクションを 1 sub-asset に統合した
    /// <see cref="AdapterRuntimeSettingsBase"/> 派生 SO。
    /// </summary>
    /// <remarks>
    /// task 3.1 ではフィールドと getter のみを公開する。
    /// <c>OnAfterDeserialize</c> による正規化は task 3.2、
    /// <c>ToJson</c> / <c>FromJson</c> の override は task 3.3 で実装する。
    /// <c>CreateAssetMenu</c> は付与しない (sub-asset 専用)。
    /// </remarks>
    public sealed class OscRuntimeSettingsSO : AdapterRuntimeSettingsBase
    {
        public const string DefaultListenEndpoint = "127.0.0.1";
        public const float DefaultBundleAccumulationTimeoutMs = 5f;
        public const float DefaultHeartbeatIntervalSeconds = 5f;

        // Receiver section
        [SerializeField]
        private string _listenEndpoint = DefaultListenEndpoint;

        [SerializeField]
        private int _listenPort = OscConfiguration.DefaultReceivePort;

        [SerializeField]
        private float _stalenessSeconds;

        [SerializeField]
        private FailSafeMode _failSafeMode = FailSafeMode.RevertToBase;

        [SerializeField]
        private bool _consistencyCheckWarnLog = true;

        [SerializeField]
        private BundleInterpretationMode _bundleMode = BundleInterpretationMode.AtomicSwap;

        [SerializeField]
        private float _bundleAccumulationTimeoutMs = DefaultBundleAccumulationTimeoutMs;

        // Sender section
        [SerializeField]
        private List<OscSenderEndpointConfig> _endpoints = new List<OscSenderEndpointConfig>();

        [SerializeField]
        private float _heartbeatIntervalSeconds = DefaultHeartbeatIntervalSeconds;

        [SerializeField]
        private bool _suppressLoopback = true;

        // Receiver getters
        public string ListenEndpoint => _listenEndpoint;

        public int ListenPort => _listenPort;

        public float StalenessSeconds => _stalenessSeconds;

        public FailSafeMode FailSafeMode => _failSafeMode;

        public bool ConsistencyCheckWarnLog => _consistencyCheckWarnLog;

        public BundleInterpretationMode BundleMode => _bundleMode;

        public float BundleAccumulationTimeoutMs => _bundleAccumulationTimeoutMs;

        // Sender getters
        public IReadOnlyList<OscSenderEndpointConfig> Endpoints => _endpoints;

        public float HeartbeatIntervalSeconds => _heartbeatIntervalSeconds;

        public bool SuppressLoopback => _suppressLoopback;
    }
}
