using System;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.OSC
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Adapters.AdapterBindings.OscAdapterBinding"/> が
    /// <c>OnStart</c> 内で <c>ctx.HostGameObject.AddComponent</c> する OSC 受信用 helper
    /// MonoBehaviour。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 既存 <see cref="OscReceiver"/> + <c>uOSC.uOscServer</c> をホスト GameObject 上に
    /// AddComponent して bind し、binding 経由の <see cref="Configure"/> 呼び出しで
    /// <see cref="OscDoubleBuffer"/> に受信値を流し込む。
    /// </para>
    /// <para>
    /// HideFlags は <see cref="HideFlags.None"/> を維持し Inspector で見える状態を保つ
    /// （デバッグしやすさのため <see cref="HideFlags.HideInInspector"/> は使わない）。
    /// </para>
    /// <para>
    /// <see cref="OnDestroy"/> で同 GameObject 上に追加した <see cref="OscReceiver"/> および
    /// <c>uOSC.uOscServer</c> も破棄する（socket close を保証）。
    /// </para>
    /// </remarks>
    public sealed class OscReceiverHost : MonoBehaviour
    {
        private OscReceiver _receiver;
        private uOSC.uOscServer _server;
        private OscDoubleBuffer _buffer;
        private OscMapping[] _mappings;
        private string _endpoint;
        private int _port;
        private bool _configured;

        /// <summary>受信に用いる内部 <see cref="OscReceiver"/> 参照（テスト/デバッグ用）。</summary>
        public OscReceiver Receiver => _receiver;

        /// <summary>受信ダブルバッファ参照（binding 側で <see cref="OscDoubleBuffer.Swap"/> を呼ぶ）。</summary>
        public OscDoubleBuffer Buffer => _buffer;

        /// <summary>bind 対象のローカルエンドポイント（IP/host）。現状ログ/診断用途。</summary>
        public string Endpoint => _endpoint;

        /// <summary>受信 UDP ポート。</summary>
        public int Port => _port;

        /// <summary><see cref="Configure"/> が呼ばれて受信開始済みなら true。</summary>
        public bool IsConfigured => _configured;

        /// <summary>
        /// helper を構成し OSC 受信を開始する。
        /// </summary>
        /// <param name="endpoint">bind エンドポイント（現状 uOSC は port のみ使用、ログ用に保持）。</param>
        /// <param name="port">UDP 受信ポート。</param>
        /// <param name="buffer">受信値の書き込み先 <see cref="OscDoubleBuffer"/>。</param>
        /// <param name="mappings">OSC アドレス ↔ BlendShape マッピング配列。</param>
        public void Configure(string endpoint, int port, OscDoubleBuffer buffer, OscMapping[] mappings)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            _endpoint = endpoint;
            _port = port;
            _buffer = buffer;
            _mappings = mappings;

            if (_receiver == null)
            {
                _receiver = gameObject.AddComponent<OscReceiver>();
            }
            _receiver.Port = port;
            _receiver.Initialize(buffer, mappings);
            _receiver.StartReceiving();

            _server = _receiver.GetComponent<uOSC.uOscServer>();
            _configured = true;
        }

        /// <summary>
        /// <see cref="OscDoubleBuffer.Swap"/> を呼んで write→read を入れ替える。
        /// binding の <c>OnFixedTick</c> 等から呼ばれる。
        /// </summary>
        public void Tick()
        {
            if (_buffer != null)
            {
                _buffer.Swap();
            }
        }

        private void OnDestroy()
        {
            // helper を Destroy したら同 GO 上の OscReceiver / uOscServer も同期して破棄し、
            // socket を確実に close する。
            if (_receiver != null)
            {
                try
                {
                    _receiver.StopReceiving();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                UnityEngine.Object.Destroy(_receiver);
                _receiver = null;
            }

            if (_server != null)
            {
                UnityEngine.Object.Destroy(_server);
                _server = null;
            }

            _configured = false;
        }
    }
}
