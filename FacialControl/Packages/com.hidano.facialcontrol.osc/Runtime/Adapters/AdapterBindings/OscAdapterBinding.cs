using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Adapters;
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
    [FacialAdapterBinding(displayName: "OSC")]
    public sealed class OscAdapterBinding : AdapterBindingBase
    {
        [SerializeField]
        private string _endpoint = "127.0.0.1";

        [SerializeField]
        private int _port = OscConfiguration.DefaultReceivePort;

        [SerializeField]
        private float _stalenessSeconds;

        [SerializeField]
        private List<OscMappingEntry> _mappings = new List<OscMappingEntry>();

        [SerializeField]
        private FailSafeMode _failSafeMode = FailSafeMode.RevertToBase;

        [SerializeField]
        private bool _consistencyCheckWarnLog = true;

        [SerializeField]
        private BundleInterpretationMode _bundleMode = BundleInterpretationMode.AtomicSwap;

        [SerializeField]
        private float _bundleAccumulationTimeoutMs = 5f;

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
        private bool _started;

        /// <summary>
        /// パラメータレスコンストラクタ。Inspector の Add ドロップダウンで <c>Activator.CreateInstance</c> から
        /// 生成される必要があるため明示する。
        /// </summary>
        public OscAdapterBinding()
        {
        }

        /// <summary>送信元エンドポイント（IP/host）。現状 uOSC は port のみ使用するが診断用に保持。</summary>
        public string Endpoint
        {
            get => _endpoint;
            set => _endpoint = value;
        }

        /// <summary>受信 UDP ポート。</summary>
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        /// <summary>staleness 判定秒数（0 で staleness 無効）。</summary>
        public float StalenessSeconds
        {
            get => _stalenessSeconds;
            set => _stalenessSeconds = value;
        }

        public List<OscMappingEntry> Mappings
        {
            get => _mappings;
            set => _mappings = value ?? new List<OscMappingEntry>();
        }

        public FailSafeMode FailSafeMode
        {
            get => _failSafeMode;
            set => _failSafeMode = value;
        }

        public bool ConsistencyCheckWarnLog
        {
            get => _consistencyCheckWarnLog;
            set => _consistencyCheckWarnLog = value;
        }

        public BundleInterpretationMode BundleMode
        {
            get => _bundleMode;
            set => _bundleMode = value;
        }

        public float BundleAccumulationTimeoutMs
        {
            get => _bundleAccumulationTimeoutMs;
            set => _bundleAccumulationTimeoutMs = value;
        }

        /// <summary>OnStart で確保した helper MonoBehaviour（テスト/診断用、未開始は null）。</summary>
        public OscReceiverHost HelperHost => _helperHost;

        public OscDoubleBuffer Buffer => _buffer;

        public OscBundleAccumulator BundleAccumulator => _bundleAccumulator;

        /// <summary>OnStart で構築した <see cref="OscInputSource"/>（テスト/診断用、未開始は null）。</summary>
        public OscInputSource InputSource => _inputSource;

        /// <summary>OnStart 済みかどうか。</summary>
        public bool IsStarted => _started;

        /// <summary>
        /// Runtime / テストから endpoint・port・mappings をまとめて設定する。
        /// </summary>
        /// <remarks>
        /// PropertyDrawer が完成するまでは inline serialized 化されたマッピングを直接持たないため、
        /// テストや手動配線では本メソッドで mappings を渡す（task 9.4 の PropertyDrawer 実装後に inline 化予定）。
        /// </remarks>
        public void Configure(string endpoint, int port, OscMapping[] mappings)
        {
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            _endpoint = endpoint;
            _port = port;
            _runtimeMappings = mappings;
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
                Debug.LogError("[OscAdapterBinding] HostGameObject が null のため OSC binding を起動できません。");
                return;
            }

            OscMapping[] runtimeMappings = _runtimeMappings ?? CreateNormalBlendShapeMappings(_mappings);
            if (runtimeMappings == null || runtimeMappings.Length == 0)
            {
                Debug.LogWarning(
                    $"[OscAdapterBinding] OSC mappings が未設定のため入力源を登録しません。slug='{Slug}'");
                return;
            }

            if (!AdapterSlug.TryParse(Slug, out var slug))
            {
                Debug.LogError(
                    $"[OscAdapterBinding] Slug '{Slug}' が AdapterSlug 規約を満たしません。InputSourceRegistry に登録できません。");
                return;
            }

            _buffer = new OscDoubleBuffer(runtimeMappings.Length);
            _bundleAccumulator = new OscBundleAccumulator(_buffer, _bundleAccumulationTimeoutMs);

            _helperHost = ctx.HostGameObject.AddComponent<OscReceiverHost>();
            _helperHost.Configure(
                _endpoint,
                _port,
                _buffer,
                runtimeMappings,
                _bundleMode == BundleInterpretationMode.AtomicSwap ? _bundleAccumulator : null,
                _bundleMode,
                ctx.TimeProvider);

            _inputSource = new OscInputSource(_buffer, _stalenessSeconds, ctx.TimeProvider);
            ctx.InputSourceRegistry.Register(slug, _inputSource);

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
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_helperHost != null)
            {
                UnityEngine.Object.Destroy(_helperHost);
                _helperHost = null;
            }

            _inputSource = null;

            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }

            _bundleAccumulator = null;

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
    }
}
