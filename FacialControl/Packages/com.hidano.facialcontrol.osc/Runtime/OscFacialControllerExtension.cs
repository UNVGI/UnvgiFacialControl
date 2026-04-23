using System.Collections.Generic;
using UnityEngine;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Osc
{
    /// <summary>
    /// FacialController と同じ GameObject に配置することで、OSC 入力を表情制御に接続する拡張。
    /// FacialController の初期化時に <see cref="IFacialControllerExtension.ConfigureFactory"/>
    /// が呼ばれ、紐付けた <see cref="OscReceiver"/> のバッファを <see cref="InputSourceFactory"/> に
    /// 登録する。
    /// </summary>
    [RequireComponent(typeof(FacialController))]
    [AddComponentMenu("FacialControl/OSC Facial Extension")]
    public class OscFacialControllerExtension : MonoBehaviour, IFacialControllerExtension
    {
        [Tooltip("OSC 受信用 OscReceiver（同 GameObject 上のものを自動取得）")]
        [SerializeField]
        private OscReceiver _receiver;

        [Tooltip("OSC 送信用 OscSender（オプション）")]
        [SerializeField]
        private OscSender _sender;

        private ITimeProvider _timeProvider;

        public OscReceiver Receiver
        {
            get => _receiver;
            set => _receiver = value;
        }

        public OscSender Sender
        {
            get => _sender;
            set => _sender = value;
        }

        private void Awake()
        {
            if (_receiver == null)
            {
                _receiver = GetComponent<OscReceiver>();
            }
            if (_sender == null)
            {
                _sender = GetComponent<OscSender>();
            }
        }

        public void ConfigureFactory(
            InputSourceFactory factory,
            FacialProfile profile,
            IReadOnlyList<string> blendShapeNames)
        {
            if (_receiver == null || _receiver.Buffer == null)
            {
                Debug.LogWarning(
                    $"[OscFacialControllerExtension] OscReceiver もしくは OscDoubleBuffer が未設定のため OSC 入力源を登録しません。");
                return;
            }

            if (_timeProvider == null)
            {
                _timeProvider = new UnityTimeProvider();
            }

            OscRegistration.Register(factory, _receiver.Buffer, _timeProvider);
        }
    }
}
