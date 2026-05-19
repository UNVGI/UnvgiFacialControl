using System;
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
    /// task 3.2 では <c>_receiverEnabled</c> / <c>_senderEnabled</c> トグルと
    /// <see cref="ISerializationCallbackReceiver.OnAfterDeserialize"/> による
    /// 不正値の既定値補正・enum 正規化までを実装する。
    /// <c>ToJson</c> / <c>FromJson</c> の override は task 3.3 で実装する。
    /// <c>CreateAssetMenu</c> は付与しない (sub-asset 専用)。
    /// </remarks>
    public sealed class OscRuntimeSettingsSO : AdapterRuntimeSettingsBase, ISerializationCallbackReceiver
    {
        public const string DefaultListenEndpoint = "127.0.0.1";
        public const float DefaultBundleAccumulationTimeoutMs = 5f;
        public const float DefaultHeartbeatIntervalSeconds = 5f;

        // Receiver section
        [SerializeField]
        private bool _receiverEnabled = true;

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
        private bool _senderEnabled = true;

        [SerializeField]
        private List<OscSenderEndpointConfig> _endpoints = new List<OscSenderEndpointConfig>();

        [SerializeField]
        private float _heartbeatIntervalSeconds = DefaultHeartbeatIntervalSeconds;

        [SerializeField]
        private bool _suppressLoopback = true;

        // Receiver getters
        public bool ReceiverEnabled => _receiverEnabled;

        public string ListenEndpoint => _listenEndpoint;

        public int ListenPort => _listenPort;

        public float StalenessSeconds => _stalenessSeconds;

        public FailSafeMode FailSafeMode => _failSafeMode;

        public bool ConsistencyCheckWarnLog => _consistencyCheckWarnLog;

        public BundleInterpretationMode BundleMode => _bundleMode;

        public float BundleAccumulationTimeoutMs => _bundleAccumulationTimeoutMs;

        // Sender getters
        public bool SenderEnabled => _senderEnabled;

        public IReadOnlyList<OscSenderEndpointConfig> Endpoints => _endpoints;

        public float HeartbeatIntervalSeconds => _heartbeatIntervalSeconds;

        public bool SuppressLoopback => _suppressLoopback;

        protected override void OnEnable()
        {
            base.OnEnable();
            NormalizeFields();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            NormalizeFields();
        }

        private void NormalizeFields()
        {
            if (string.IsNullOrWhiteSpace(_listenEndpoint))
            {
                _listenEndpoint = DefaultListenEndpoint;
            }
            else
            {
                _listenEndpoint = _listenEndpoint.Trim();
            }

            if (_listenPort <= 0 || _listenPort > 65535)
            {
                _listenPort = OscConfiguration.DefaultReceivePort;
            }

            if (_stalenessSeconds < 0f || float.IsNaN(_stalenessSeconds))
            {
                _stalenessSeconds = 0f;
            }

            if (!Enum.IsDefined(typeof(FailSafeMode), _failSafeMode))
            {
                _failSafeMode = FailSafeMode.RevertToBase;
            }

            if (!Enum.IsDefined(typeof(BundleInterpretationMode), _bundleMode))
            {
                _bundleMode = BundleInterpretationMode.AtomicSwap;
            }

            if (_bundleAccumulationTimeoutMs <= 0f || float.IsNaN(_bundleAccumulationTimeoutMs))
            {
                _bundleAccumulationTimeoutMs = DefaultBundleAccumulationTimeoutMs;
            }

            if (_heartbeatIntervalSeconds <= 0f || float.IsNaN(_heartbeatIntervalSeconds))
            {
                _heartbeatIntervalSeconds = DefaultHeartbeatIntervalSeconds;
            }

            if (_endpoints == null)
            {
                _endpoints = new List<OscSenderEndpointConfig>();
            }
        }
    }
}
