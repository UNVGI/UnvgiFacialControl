using System;
using System.Threading;
using Unity.Collections;

namespace Hidano.FacialControl.Domain.Services
{
    /// <summary>
    /// 入力源ウェイト (layerIdx, sourceIdx) → [0, 1] を保持するダブルバッファ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ランタイム <see cref="SetWeight"/> は任意スレッドから呼出可能であり、
    /// 値は 0〜1 に silent clamp される (Req 2.5)。書込は次回
    /// <see cref="SwapIfDirty"/> 以降の <see cref="GetWeight"/> から観測可能になる
    /// (Req 4.1, 4.2)。
    /// </para>
    /// <para>
    /// 本クラスは <c>OscDoubleBuffer</c> と同じ 2 本の <see cref="NativeArray{T}"/>
    /// + <c>writeIndex</c> + <c>dirtyTick</c> の構造を採る (Design D-7)。
    /// <see cref="SwapIfDirty"/> は index flip の後に新 readBuffer の内容を
    /// 新 writeBuffer へ <see cref="NativeArray{T}.CopyTo(NativeArray{T})"/> で
    /// copy-forward する (Design §SwapIfDirty)。これにより「あるフレームで書いた
    /// 値が次フレームで別インデックスを書いた際に消える」スタレデータバグ
    /// (Critical 1) を防ぐ。
    /// BulkScope (4.5) と範囲外キーの警告 (4.3) は後続タスクで実装する。
    /// </para>
    /// </remarks>
    public sealed class LayerInputSourceWeightBuffer : IDisposable
    {
        /// <summary>レイヤー本数（ctor 指定、不変）。</summary>
        public int LayerCount { get; }

        /// <summary>1 レイヤー当たりの入力源スロット数（ctor 指定、不変）。</summary>
        public int MaxSourcesPerLayer { get; }

        private NativeArray<float> _bufferA;
        private NativeArray<float> _bufferB;
        private int _writeIndex;
        private int _dirtyTick;
        private int _observedTick;
        private bool _disposed;

        /// <summary>
        /// 指定の (layerCount × maxSourcesPerLayer) サイズでダブルバッファを確保する。
        /// 両バッファは 0 で初期化される。
        /// </summary>
        /// <param name="layerCount">レイヤー本数。0 以上。</param>
        /// <param name="maxSourcesPerLayer">1 レイヤー当たりの最大入力源数。0 以上。</param>
        public LayerInputSourceWeightBuffer(int layerCount, int maxSourcesPerLayer)
        {
            if (layerCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(layerCount), layerCount,
                    "layerCount は 0 以上を指定してください。");
            }

            if (maxSourcesPerLayer < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSourcesPerLayer), maxSourcesPerLayer,
                    "maxSourcesPerLayer は 0 以上を指定してください。");
            }

            LayerCount = layerCount;
            MaxSourcesPerLayer = maxSourcesPerLayer;

            int size = layerCount * maxSourcesPerLayer;
            _bufferA = new NativeArray<float>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bufferB = new NativeArray<float>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _writeIndex = 0;
            _dirtyTick = 0;
            _observedTick = 0;
        }

        /// <summary>
        /// (layerIdx, sourceIdx) のウェイトを書込む。値は 0〜1 に silent clamp される。
        /// 書込は次回 <see cref="SwapIfDirty"/> 以降の <see cref="GetWeight"/> で観測可能。
        /// </summary>
        /// <param name="layerIdx">レイヤーインデックス。<see cref="LayerCount"/> 未満を指定。</param>
        /// <param name="sourceIdx">入力源インデックス。<see cref="MaxSourcesPerLayer"/> 未満を指定。</param>
        /// <param name="weight">ウェイト値。範囲外は silent clamp される (Req 2.5)。</param>
        public void SetWeight(int layerIdx, int sourceIdx, float weight)
        {
            if ((uint)layerIdx >= (uint)LayerCount ||
                (uint)sourceIdx >= (uint)MaxSourcesPerLayer)
            {
                return;
            }

            float clamped = weight < 0f ? 0f : (weight > 1f ? 1f : weight);

            int flatIdx = (layerIdx * MaxSourcesPerLayer) + sourceIdx;
            var writeBuffer = Volatile.Read(ref _writeIndex) == 0 ? _bufferA : _bufferB;
            writeBuffer[flatIdx] = clamped;

            Interlocked.Increment(ref _dirtyTick);
        }

        /// <summary>
        /// 書込が発生していればダブルバッファの read/write を入れ替え、
        /// index flip 後に新 readBuffer → 新 writeBuffer へ全内容を copy-forward する。
        /// 書込がなければ no-op。Aggregator がフレーム先頭で呼ぶことを想定。
        /// </summary>
        /// <remarks>
        /// copy-forward は <see cref="NativeArray{T}.CopyTo(NativeArray{T})"/> を用い
        /// GC フリーで O(layerCount × maxSourcesPerLayer) (Design §SwapIfDirty)。
        /// これにより writeBuffer は常に「現行 readBuffer の複製 + 最新 Set」の
        /// 状態を保ち、Critical 1 のスタレデータバグを防ぐ。
        /// </remarks>
        public void SwapIfDirty()
        {
            int dirty = Volatile.Read(ref _dirtyTick);
            if (dirty == _observedTick)
            {
                return;
            }

            int newWriteIndex = 1 - _writeIndex;
            Interlocked.Exchange(ref _writeIndex, newWriteIndex);

            var newReadBuffer = newWriteIndex == 0 ? _bufferB : _bufferA;
            var newWriteBuffer = newWriteIndex == 0 ? _bufferA : _bufferB;
            newReadBuffer.CopyTo(newWriteBuffer);

            _observedTick = dirty;
        }

        /// <summary>
        /// (layerIdx, sourceIdx) の現在の読取値を返す。範囲外は 0 を返す。
        /// </summary>
        public float GetWeight(int layerIdx, int sourceIdx)
        {
            if ((uint)layerIdx >= (uint)LayerCount ||
                (uint)sourceIdx >= (uint)MaxSourcesPerLayer)
            {
                return 0f;
            }

            int flatIdx = (layerIdx * MaxSourcesPerLayer) + sourceIdx;
            var readBuffer = Volatile.Read(ref _writeIndex) == 0 ? _bufferB : _bufferA;
            return readBuffer[flatIdx];
        }

        /// <summary>
        /// 両バッファを解放する。重複呼出しは無視される。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_bufferA.IsCreated)
            {
                _bufferA.Dispose();
            }
            if (_bufferB.IsCreated)
            {
                _bufferB.Dispose();
            }

            _disposed = true;
        }
    }
}
