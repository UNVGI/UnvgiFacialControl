using System;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.OSC
{
    /// <summary>
    /// OSC 送信用 helper MonoBehaviour。
    /// binding が <c>OnStart</c> 内で <c>ctx.HostGameObject.AddComponent</c> し、
    /// <see cref="Configure"/> で送信先と mapping を確定する。
    /// </summary>
    /// <remarks>
    /// HideFlags は <see cref="HideFlags.None"/>。<see cref="OnDestroy"/> で同 GameObject 上に
    /// 追加した <see cref="OscSender"/> および <c>uOSC.uOscClient</c> も破棄する。
    /// </remarks>
    public sealed class OscSenderHost : MonoBehaviour
    {
        private OscSender _sender;
        private uOSC.uOscClient _client;
        private OscMapping[] _mappings;
        private string _endpoint;
        private int _port;
        private bool _configured;

        /// <summary>送信に用いる内部 <see cref="OscSender"/> 参照。</summary>
        public OscSender Sender => _sender;

        /// <summary>送信先 IP アドレス / host。</summary>
        public string Endpoint => _endpoint;

        /// <summary>送信先 UDP ポート。</summary>
        public int Port => _port;

        /// <summary><see cref="Configure"/> 済みなら true。</summary>
        public bool IsConfigured => _configured;

        /// <summary>
        /// helper を構成し OSC 送信を開始する。
        /// </summary>
        /// <param name="endpoint">送信先 IP アドレス。</param>
        /// <param name="port">送信先 UDP ポート。</param>
        /// <param name="mappings">OSC アドレスマッピング配列。</param>
        public void Configure(string endpoint, int port, OscMapping[] mappings)
        {
            Configure(endpoint, port, mappings, addressUtf8: null);
        }

        public void Configure(string endpoint, int port, OscMapping[] mappings, byte[][] addressUtf8)
        {
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            _endpoint = endpoint;
            _port = port;
            _mappings = mappings;

            if (_sender == null)
            {
                _sender = gameObject.AddComponent<OscSender>();
            }
            _sender.Address = endpoint;
            _sender.Port = port;
            _sender.Initialize(mappings, addressUtf8);
            _sender.StartSending();

            _client = _sender.Client;
            _configured = true;
        }

        /// <summary>
        /// 全 BlendShape 値を OSC で送信する。
        /// </summary>
        public void SendAll(float[] values)
        {
            if (_sender != null)
            {
                _sender.SendAll(values);
            }
        }

        /// <summary>
        /// 単一 mapping index の値を OSC で送信する。
        /// </summary>
        public void SendSingle(int index, float value)
        {
            if (_sender != null)
            {
                _sender.SendSingle(index, value);
            }
        }

        /// <summary>
        /// 送信元識別ヘッダと BlendShape 値群を 1 つの OSC bundle として送信する。
        /// </summary>
        public void SendBundle(
            byte[] senderUuidBytes,
            string startedAtUnixMs,
            float[] values,
            int count)
        {
            SendBundle(
                senderUuidBytes,
                startedAtUnixMs,
                values,
                count,
                heartbeatNames: null,
                heartbeatNameCount: 0);
        }

        public void SendBundle(
            byte[] senderUuidBytes,
            string startedAtUnixMs,
            byte[][] addressUtf8,
            float[] values,
            int count)
        {
            SendBundle(
                senderUuidBytes,
                startedAtUnixMs,
                addressUtf8,
                values,
                count,
                heartbeatNames: null,
                heartbeatNameCount: 0);
        }

        /// <summary>
        /// 送信元識別ヘッダ、BlendShape 値群、必要なら heartbeat を 1 つの OSC bundle として送信する。
        /// </summary>
        public void SendBundle(
            byte[] senderUuidBytes,
            string startedAtUnixMs,
            float[] values,
            int count,
            string[] heartbeatNames,
            int heartbeatNameCount)
        {
            if (_sender != null)
            {
                _sender.SendBundle(
                    senderUuidBytes,
                    startedAtUnixMs,
                    values,
                    count,
                    heartbeatNames,
                    heartbeatNameCount);
            }
        }

        public void SendBundle(
            byte[] senderUuidBytes,
            string startedAtUnixMs,
            byte[][] addressUtf8,
            float[] values,
            int count,
            string[] heartbeatNames,
            int heartbeatNameCount)
        {
            if (_sender != null)
            {
                _sender.SendBundle(
                    senderUuidBytes,
                    startedAtUnixMs,
                    addressUtf8,
                    values,
                    count,
                    heartbeatNames,
                    heartbeatNameCount);
            }
        }

        private void OnDestroy()
        {
            if (_sender != null)
            {
                try
                {
                    _sender.StopSending();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_sender);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_sender);
                }
                _sender = null;
            }

            if (_client != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_client);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_client);
                }
                _client = null;
            }

            _configured = false;
        }
    }
}
